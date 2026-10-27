using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace AirwayHVAC.SmsAi.Api.Functions;

public sealed class SendMailFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ILogger<SendMailFunction> _logger;
    private readonly IConfiguration _configuration;

    public SendMailFunction(ILogger<SendMailFunction> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [Function("SendMail")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "notifications/email")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        SendMailRequest? body;
        try
        {
            body = await ParseRequestAsync(request, cancellationToken);
        }
        catch (JsonException)
        {
            return await ErrorAsync(request, HttpStatusCode.BadRequest, "invalid_payload", "Request body must be valid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Content))
        {
            return await ErrorAsync(request, HttpStatusCode.BadRequest, "missing_fields", "email and content are required.", cancellationToken);
        }

        var apiKey = _configuration["SendGrid:ApiKey"] ?? Environment.GetEnvironmentVariable("SENDGRID_API_KEY");
        var fromEmail = _configuration["SendGrid:FromEmail"] ?? Environment.GetEnvironmentVariable("SENDGRID_FROM_EMAIL");
        var fromName = _configuration["SendGrid:FromName"] ?? Environment.GetEnvironmentVariable("SENDGRID_FROM_NAME") ?? "AirwayHVAC";

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(fromEmail))
        {
            _logger.LogError("Missing SendGrid settings. SENDGRID_API_KEY and SENDGRID_FROM_EMAIL must be configured.");
            return await ErrorAsync(request, HttpStatusCode.InternalServerError, "missing_configuration", "SendGrid is not configured.", cancellationToken);
        }

        var client = new SendGridClient(apiKey);
        var subject = string.IsNullOrWhiteSpace(body.Subject) ? "AirwayHVAC Notification" : body.Subject.Trim();
        var plainTextContent = body.Content.Trim();
        var htmlContent = $"<p>{HtmlEncoder.Default.Encode(plainTextContent)}</p>";

        var message = MailHelper.CreateSingleEmail(
            new EmailAddress(fromEmail, fromName),
            new EmailAddress(body.Email.Trim()),
            subject,
            plainTextContent,
            htmlContent);

        var sendResult = await client.SendEmailAsync(message, cancellationToken);

        if ((int)sendResult.StatusCode >= 400)
        {
            var details = await sendResult.Body.ReadAsStringAsync(cancellationToken);
            _logger.LogError("SendGrid rejected message. StatusCode={StatusCode}, Details={Details}", sendResult.StatusCode, details);
            return await ErrorAsync(request, HttpStatusCode.BadGateway, "send_failed", "Unable to send email.", cancellationToken);
        }

        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            success = true,
            to = body.Email.Trim(),
            subject,
        }, cancellationToken);

        return response;
    }


    private static async Task<SendMailRequest?> ParseRequestAsync(HttpRequestData request, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(rawBody))
        {
            return JsonSerializer.Deserialize<SendMailRequest>(rawBody, JsonOptions);
        }

        var queryData = ParseQueryString(request.Url.Query);
        queryData.TryGetValue("email", out var email);
        queryData.TryGetValue("content", out var content);
        queryData.TryGetValue("subject", out var subject);

        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(content) && string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        return new SendMailRequest(email ?? string.Empty, content ?? string.Empty, subject);
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var normalized = query.StartsWith('?') ? query[1..] : query;

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return data;
        }

        var pairs = normalized.Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0].Replace('+', ' '));
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : string.Empty;

            if (!string.IsNullOrWhiteSpace(key))
            {
                data[key] = value;
            }
        }

        return data;
    }

    private static async Task<HttpResponseData> ErrorAsync(
        HttpRequestData request,
        HttpStatusCode statusCode,
        string code,
        string message,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(statusCode);
        await response.WriteAsJsonAsync(new
        {
            error = new
            {
                code,
                message,
            },
        }, cancellationToken);

        return response;
    }

    private sealed record SendMailRequest(string Email, string Content, string? Subject);
}
