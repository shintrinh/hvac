using System.Net;
using System.Security;
using System.Text;
using AirwayHVAC.SmsAi.Application.UseCases;
using AirwayHVAC.SmsAi.Domain.Enums;
using AirwayHVAC.SmsAi.Infrastructure.Configuration;
using AirwayHVAC.SmsAi.Shared.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AirwayHVAC.SmsAi.Api.Functions;

public sealed class InboundVoiceFunction
{
    private readonly HandleInboundVoiceUseCase _inboundUseCase;
    private readonly HandleVoiceGatherUseCase _gatherUseCase;
    private readonly HandleVoiceStatusUseCase _statusUseCase;
    private readonly ITenantResolver _tenantResolver;
    private readonly TenantRoutingOptions _routingOptions;
    private readonly TwilioVoiceOptions _voiceOptions;
    private readonly ILogger<InboundVoiceFunction> _logger;

    public InboundVoiceFunction(
        HandleInboundVoiceUseCase inboundUseCase,
        HandleVoiceGatherUseCase gatherUseCase,
        HandleVoiceStatusUseCase statusUseCase,
        ITenantResolver tenantResolver,
        IOptions<TenantRoutingOptions> routingOptions,
        IOptions<TwilioVoiceOptions> voiceOptions,
        ILogger<InboundVoiceFunction> logger)
    {
        _inboundUseCase = inboundUseCase;
        _gatherUseCase = gatherUseCase;
        _statusUseCase = statusUseCase;
        _tenantResolver = tenantResolver;
        _routingOptions = routingOptions.Value;
        _voiceOptions = voiceOptions.Value;
        _logger = logger;
    }

    [Function("InboundVoiceWebhook")]
    public async Task<HttpResponseData> InboundAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "webhooks/twilio/voice/inbound")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var formData = await ParseFormUrlEncodedBodyAsync(request, cancellationToken);
        if (!formData.TryGetValue("From", out var from) || string.IsNullOrWhiteSpace(from) ||
            !formData.TryGetValue("To", out var to) || string.IsNullOrWhiteSpace(to) ||
            !formData.TryGetValue("CallSid", out var callSid) || string.IsNullOrWhiteSpace(callSid))
        {
            return await WriteTextAsync(request, HttpStatusCode.BadRequest, "Invalid Twilio voice payload.", cancellationToken);
        }

        var routingDecision = ResolveTenant(request, from, to);
        if (routingDecision.Rejected)
        {
            return await WriteTextAsync(request, HttpStatusCode.BadRequest, "Tenant routing failed.", cancellationToken);
        }

        try
        {
            var result = await _inboundUseCase.ExecuteAsync(
                new HandleInboundVoiceCommand(routingDecision.TenantId ?? string.Empty, routingDecision.TenantPhoneNumber ?? string.Empty, from, to, callSid),
                cancellationToken);

            var twiml = BuildGatherTwiml(_voiceOptions.DisclosureMessage, result.ReplyMessage);
            return await WriteTwimlAsync(request, twiml, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Inbound voice rejected. From={From}, To={To}, CallSid={CallSid}", from, to, callSid);
            return await WriteTextAsync(request, HttpStatusCode.BadRequest, "Inbound voice rejected: tenant could not be resolved.", cancellationToken);
        }
    }

    [Function("VoiceGatherWebhook")]
    public async Task<HttpResponseData> GatherAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "webhooks/twilio/voice/gather")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var formData = await ParseFormUrlEncodedBodyAsync(request, cancellationToken);
        if (!formData.TryGetValue("CallSid", out var callSid) || string.IsNullOrWhiteSpace(callSid))
        {
            return await WriteTextAsync(request, HttpStatusCode.BadRequest, "Invalid Twilio gather payload.", cancellationToken);
        }

        formData.TryGetValue("SpeechResult", out var speechResult);
        var confidence = formData.TryGetValue("Confidence", out var confidenceValue) && decimal.TryParse(confidenceValue, out var parsedConfidence)
            ? parsedConfidence
            : (decimal?)null;

        try
        {
            var result = await _gatherUseCase.ExecuteAsync(new HandleVoiceGatherCommand(callSid, speechResult ?? string.Empty, confidence), cancellationToken);

            if (result.HandoffRequested)
            {
                return await WriteTwimlAsync(request, BuildHandoffTwiml(result.ReplyMessage), cancellationToken);
            }

            if (result.Completed)
            {
                return await WriteTwimlAsync(request, BuildCompletedTwiml(result.ReplyMessage), cancellationToken);
            }

            return await WriteTwimlAsync(request, BuildGatherTwiml(null, result.ReplyMessage), cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Voice gather rejected. CallSid={CallSid}", callSid);
            return await WriteTwimlAsync(request, BuildHandoffTwiml("Please hold while I connect you to our team."), cancellationToken);
        }
    }

    [Function("VoiceStatusWebhook")]
    public async Task<HttpResponseData> StatusAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "webhooks/twilio/voice/status")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var formData = await ParseFormUrlEncodedBodyAsync(request, cancellationToken);
        if (formData.TryGetValue("CallSid", out var callSid) && !string.IsNullOrWhiteSpace(callSid))
        {
            formData.TryGetValue("CallStatus", out var callStatus);
            await _statusUseCase.ExecuteAsync(new HandleVoiceStatusCommand(callSid, callStatus ?? string.Empty), cancellationToken);
        }

        return await WriteTextAsync(request, HttpStatusCode.OK, "OK", cancellationToken);
    }

    private TenantRoutingDecision ResolveTenant(HttpRequestData request, string from, string to)
    {
        var tenantIdHeader = GetHeaderValue(request, _routingOptions.TenantIdHeaderName);
        var verificationKeyHeader = GetHeaderValue(request, _routingOptions.VerificationKeyHeaderName);

        var routingDecision = _tenantResolver.Resolve(request.Url.Host, from, to, tenantIdHeader, verificationKeyHeader);
        _logger.LogInformation(
            "Inbound voice tenant routing decision. Source={Source}, Reason={Reason}, TenantId={TenantId}, TenantPhone={TenantPhone}",
            routingDecision.Source,
            routingDecision.Reason,
            routingDecision.TenantId,
            routingDecision.TenantPhoneNumber);
        return routingDecision;
    }

    private string BuildGatherTwiml(string? preface, string prompt)
    {
        var actionUrl = BuildUrl("/api/webhooks/twilio/voice/gather");
        var fallback = string.IsNullOrWhiteSpace(_voiceOptions.HumanHandoffPhoneNumber)
            ? "<Hangup />"
            : $"<Dial>{Escape(_voiceOptions.HumanHandoffPhoneNumber)}</Dial>";
        var prefaceSay = string.IsNullOrWhiteSpace(preface) ? string.Empty : BuildSay(preface);

        return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><Response>{prefaceSay}<Gather input=\"speech\" action=\"{Escape(actionUrl)}\" method=\"POST\" timeout=\"{_voiceOptions.GatherTimeoutSeconds}\">{BuildSay(prompt)}</Gather>{BuildSay("I did not hear a response. Please hold while I connect you to our team.")}{fallback}</Response>";
    }

    private string BuildHandoffTwiml(string message)
    {
        var handoff = string.IsNullOrWhiteSpace(_voiceOptions.HumanHandoffPhoneNumber)
            ? "<Hangup />"
            : $"<Dial>{Escape(_voiceOptions.HumanHandoffPhoneNumber)}</Dial>";
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><Response>{BuildSay(message)}{handoff}</Response>";
    }

    private string BuildCompletedTwiml(string message)
        => $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><Response>{BuildSay(message)}<Hangup /></Response>";

    private string BuildSay(string text)
        => $"<Say voice=\"{Escape(_voiceOptions.VoiceName)}\" language=\"{Escape(_voiceOptions.Language)}\">{Escape(text)}</Say>";

    private string BuildUrl(string path)
    {
        if (string.IsNullOrWhiteSpace(_voiceOptions.PublicBaseUrl))
        {
            return path;
        }

        return $"{_voiceOptions.PublicBaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
    }

    private static string Escape(string text)
        => SecurityElement.Escape(text) ?? string.Empty;

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

    private static async Task<HttpResponseData> WriteTwimlAsync(HttpRequestData request, string twiml, CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/xml; charset=utf-8");
        await response.WriteStringAsync(twiml, cancellationToken);
        return response;
    }

    private static async Task<HttpResponseData> WriteTextAsync(HttpRequestData request, HttpStatusCode statusCode, string text, CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(statusCode);
        await response.WriteStringAsync(text, cancellationToken);
        return response;
    }
}
