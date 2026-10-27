using System.Net;
using System.Text;
using AirwayHVAC.SmsAi.Application.UseCases;
using AirwayHVAC.SmsAi.Infrastructure.Configuration;
using AirwayHVAC.SmsAi.Shared.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AirwayHVAC.SmsAi.Api.Functions;

public sealed class InboundSmsWebhook
{
    private readonly HandleInboundSmsUseCase _useCase;
    private readonly ITenantResolver _tenantResolver;
    private readonly TenantRoutingOptions _routingOptions;
    private readonly ILogger<InboundSmsWebhook> _logger;

    public InboundSmsWebhook(
        HandleInboundSmsUseCase useCase,
        ITenantResolver tenantResolver,
        IOptions<TenantRoutingOptions> routingOptions,
        ILogger<InboundSmsWebhook> logger)
    {
        _useCase = useCase;
        _tenantResolver = tenantResolver;
        _routingOptions = routingOptions.Value;
        _logger = logger;
    }

    [Function("InboundSmsWebhook")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "webhooks/twilio/inbound")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var formData = await ParseFormUrlEncodedBodyAsync(request, cancellationToken);

        if (!formData.TryGetValue("From", out var from) || string.IsNullOrWhiteSpace(from) ||
            !formData.TryGetValue("To", out var to) || string.IsNullOrWhiteSpace(to) ||
            !formData.TryGetValue("Body", out var body) || string.IsNullOrWhiteSpace(body))
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("Invalid Twilio payload.", cancellationToken);
            return badRequest;
        }

        var tenantIdHeader = GetHeaderValue(request, _routingOptions.TenantIdHeaderName);
        var verificationKeyHeader = GetHeaderValue(request, _routingOptions.VerificationKeyHeaderName);

        var routingDecision = _tenantResolver.Resolve(
            request.Url.Host,
            from,
            to,
            tenantIdHeader,
            verificationKeyHeader);

        _logger.LogInformation(
            "Inbound tenant routing decision. Source={Source}, Reason={Reason}, TenantId={TenantId}, TenantPhone={TenantPhone}",
            routingDecision.Source,
            routingDecision.Reason,
            routingDecision.TenantId,
            routingDecision.TenantPhoneNumber);

        if (routingDecision.Rejected)
        {
            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("Tenant routing failed.", cancellationToken);
            return badRequest;
        }

        _logger.LogInformation("Inbound SMS webhook invoked. From={From}, To={To}", from, to);

        HandleInboundSmsResult result;
        try
        {
            result = await _useCase.ExecuteAsync(
                new HandleInboundSmsCommand(routingDecision.TenantId ?? string.Empty, routingDecision.TenantPhoneNumber ?? string.Empty, from, body),
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "Inbound SMS rejected due to tenant resolution/data issue. TenantId={TenantId}, TenantPhone={TenantPhone}, From={From}, To={To}",
                routingDecision.TenantId,
                routingDecision.TenantPhoneNumber,
                from,
                to);

            var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("Inbound SMS rejected: tenant could not be resolved.", cancellationToken);
            return badRequest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error while processing inbound SMS. From={From}, To={To}", from, to);
            throw;
        }

        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync($"Message processed. ConversationId={result.ConversationId}; LeadId={result.LeadId}", cancellationToken);
        return response;
    }

    private static string? GetHeaderValue(HttpRequestData request, string headerName)
    {
        return request.Headers.TryGetValues(headerName, out var values)
            ? values.FirstOrDefault()
            : null;
    }

    private static async Task<Dictionary<string, string>> ParseFormUrlEncodedBodyAsync(HttpRequestData request, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        var content = await reader.ReadToEndAsync(cancellationToken);

        var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pairs = content.Split('&', StringSplitOptions.RemoveEmptyEntries);

        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0].Replace('+', ' '));
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : string.Empty;
            dictionary[key] = value;
        }

        return dictionary;
    }
}
