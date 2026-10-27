using System.Net;
using System.Text.Json;
using AirwayHVAC.SmsAi.Application.Services;
using AirwayHVAC.SmsAi.Domain.Enums;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AirwayHVAC.SmsAi.Api.Functions;

public sealed class DashboardFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly DashboardService _dashboardService;

    public DashboardFunction(DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [Function("GetConversations")]
    public async Task<HttpResponseData> GetConversations(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "dashboard/conversations")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var tenantId = GetRequiredQuery(request, "tenantId");
        if (tenantId is null)
        {
            return await BadRequestAsync(request, "missing_tenant_id", "tenantId is required.", cancellationToken);
        }

        var page = GetPositiveIntQuery(request, "page", 1);
        var pageSize = GetPositiveIntQuery(request, "pageSize", 20, 100);
        var status = NormalizeConversationStatusFilter(GetOptionalQuery(request, "status"));

        var (items, totalCount) = await _dashboardService.GetConversationsAsync(tenantId, page, pageSize, status, cancellationToken);

        var response = request.CreateResponse(HttpStatusCode.OK);
        await WriteJsonAsync(response, new
        {
            data = items.Select(x => new
            {
                id = x.Id,
                tenantId = x.TenantId,
                customerPhone = x.Phone,
                lastMessage = x.Messages.OrderByDescending(m => m.Timestamp).FirstOrDefault()?.Text,
                status = NormalizeConversationStatus(x.Status),
                createdAt = x.Messages.OrderBy(m => m.Timestamp).FirstOrDefault()?.Timestamp ?? x.LastMessageAt,
                updatedAt = x.LastMessageAt,
            }),
            paging = ToPaging(page, pageSize, totalCount),
        }, cancellationToken);

        return response;
    }

    [Function("GetMessages")]
    public async Task<HttpResponseData> GetMessages(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "dashboard/messages")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var tenantId = GetRequiredQuery(request, "tenantId");
        if (tenantId is null)
        {
            return await BadRequestAsync(request, "missing_tenant_id", "tenantId is required.", cancellationToken);
        }

        var conversationId = GetRequiredQuery(request, "conversationId");
        if (conversationId is null)
        {
            return await BadRequestAsync(request, "missing_conversation_id", "conversationId is required.", cancellationToken);
        }

        var page = GetPositiveIntQuery(request, "page", 1);
        var pageSize = GetPositiveIntQuery(request, "pageSize", 50, 200);

        var (items, totalCount) = await _dashboardService.GetMessagesAsync(tenantId, conversationId, page, pageSize, cancellationToken);

        var response = request.CreateResponse(HttpStatusCode.OK);
        await WriteJsonAsync(response, new
        {
            data = items.Select((x, index) => new
            {
                id = $"{conversationId}:{(page - 1) * pageSize + index + 1}",
                tenantId,
                conversationId,
                sender = x.Role,
                content = x.Text,
                createdAt = x.Timestamp,
            }),
            paging = ToPaging(page, pageSize, totalCount),
        }, cancellationToken);

        return response;
    }

    [Function("GetLeads")]
    public async Task<HttpResponseData> GetLeads(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "dashboard/leads")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var tenantId = GetRequiredQuery(request, "tenantId");
        if (tenantId is null)
        {
            return await BadRequestAsync(request, "missing_tenant_id", "tenantId is required.", cancellationToken);
        }

        var page = GetPositiveIntQuery(request, "page", 1);
        var pageSize = GetPositiveIntQuery(request, "pageSize", 20, 100);
        var status = GetOptionalQuery(request, "status");

        var (items, totalCount) = await _dashboardService.GetLeadsAsync(tenantId, page, pageSize, status, cancellationToken);

        var response = request.CreateResponse(HttpStatusCode.OK);
        await WriteJsonAsync(response, new
        {
            data = items.Select(x => new
            {
                id = x.Id,
                tenantId = x.TenantId,
                name = x.Name,
                phone = x.Phone,
                status = NormalizeLeadStatus(x.Status),
                createdAt = x.CreatedAt,
                updatedAt = x.UpdatedAt,
            }),
            paging = ToPaging(page, pageSize, totalCount),
        }, cancellationToken);

        return response;
    }

    [Function("UpdateLeadStatus")]
    public async Task<HttpResponseData> UpdateLeadStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "dashboard/leads/{leadId}/status")] HttpRequestData request,
        string leadId,
        CancellationToken cancellationToken)
    {
        UpdateLeadStatusRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<UpdateLeadStatusRequest>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return await BadRequestAsync(request, "invalid_payload", "Request body must be valid JSON.", cancellationToken);
        }

        if (body is null || string.IsNullOrWhiteSpace(body.TenantId))
        {
            return await BadRequestAsync(request, "missing_tenant_id", "tenantId is required.", cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(body.Status) || !TryParseLeadStatus(body.Status, out var targetStatus))
        {
            return await BadRequestAsync(request, "invalid_status", "status must be one of: new, contacted, booked, closed.", cancellationToken);
        }

        try
        {
            var lead = await _dashboardService.UpdateLeadStatusAsync(body.TenantId, leadId, targetStatus, cancellationToken);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await WriteJsonAsync(response, new
            {
                id = lead.Id,
                tenantId = lead.TenantId,
                status = NormalizeLeadStatus(lead.Status),
                updatedAt = lead.UpdatedAt,
            }, cancellationToken);
            return response;
        }
        catch (KeyNotFoundException)
        {
            return await ErrorAsync(request, HttpStatusCode.NotFound, "lead_not_found", "Lead not found for this tenant.", cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await ErrorAsync(request, HttpStatusCode.Conflict, "invalid_transition", ex.Message, cancellationToken);
        }
    }

    [Function("GetDashboardAnalytics")]
    public async Task<HttpResponseData> GetDashboardAnalytics(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "dashboard/analytics")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var tenantId = GetRequiredQuery(request, "tenantId");
        if (tenantId is null)
        {
            return await BadRequestAsync(request, "missing_tenant_id", "tenantId is required.", cancellationToken);
        }

        var fromRaw = GetOptionalQuery(request, "from");
        var toRaw = GetOptionalQuery(request, "to");

        if (!DateOnly.TryParse(fromRaw, out var from) || !DateOnly.TryParse(toRaw, out var to))
        {
            return await BadRequestAsync(request, "invalid_date_range", "from and to are required in yyyy-MM-dd format.", cancellationToken);
        }

        try
        {
            var analytics = await _dashboardService.GetAnalyticsAsync(tenantId, from, to, cancellationToken);

            var response = request.CreateResponse(HttpStatusCode.OK);
            await WriteJsonAsync(response, new
            {
                tenantId,
                range = new { from, to },
                metrics = new
                {
                    totalConversations = analytics.TotalConversations,
                    newLeadsToday = analytics.NewLeadsToday,
                    conversionRate = analytics.ConversionRate,
                    averageResponseTimeSeconds = analytics.AverageResponseTimeSeconds,
                },
                leadsPerDay = analytics.LeadsPerDay.Select(x => new { date = x.Date, count = x.Count }),
            }, cancellationToken);
            return response;
        }
        catch (ArgumentException ex)
        {
            return await BadRequestAsync(request, "invalid_date_range", ex.Message, cancellationToken);
        }
    }

    private static object ToPaging(int page, int pageSize, int totalCount)
    {
        var hasNext = page * pageSize < totalCount;
        return new
        {
            page,
            pageSize,
            total = totalCount,
            hasNext,
        };
    }

    private static async Task<HttpResponseData> BadRequestAsync(HttpRequestData request, string code, string message, CancellationToken cancellationToken)
        => await ErrorAsync(request, HttpStatusCode.BadRequest, code, message, cancellationToken);

    private static async Task<HttpResponseData> ErrorAsync(HttpRequestData request, HttpStatusCode statusCode, string code, string message, CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(statusCode);
        await WriteJsonAsync(response, new
        {
            error = new
            {
                code,
                message,
            },
        }, cancellationToken);

        return response;
    }

    private static string? GetRequiredQuery(HttpRequestData request, string key)
    {
        var value = GetOptionalQuery(request, key);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? GetOptionalQuery(HttpRequestData request, string key)
    {
        var query = request.Url.Query;
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var trimmed = query.TrimStart('?');
        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var queryKey = Uri.UnescapeDataString(parts[0]);
            if (!string.Equals(queryKey, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
        }

        return null;
    }

    private static int GetPositiveIntQuery(HttpRequestData request, string key, int defaultValue, int? maxValue = null)
    {
        var raw = GetOptionalQuery(request, key);
        if (!int.TryParse(raw, out var value) || value <= 0)
        {
            value = defaultValue;
        }

        if (maxValue.HasValue && value > maxValue.Value)
        {
            value = maxValue.Value;
        }

        return value;
    }

    private static bool TryParseLeadStatus(string raw, out LeadStatus status)
    {
        status = raw.Trim().ToLowerInvariant() switch
        {
            "new" => LeadStatus.New,
            "contacted" => LeadStatus.Contacted,
            "booked" => LeadStatus.Booked,
            "closed" => LeadStatus.Closed,
            _ => LeadStatus.New,
        };

        return raw.Trim().ToLowerInvariant() is "new" or "contacted" or "booked" or "closed";
    }

    private static string NormalizeLeadStatus(LeadStatus status)
        => status switch
        {
            LeadStatus.Qualified => "contacted",
            _ => status.ToString().ToLowerInvariant(),
        };

    private static string NormalizeConversationStatus(string status)
    {
        if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
        {
            return "open";
        }

        return status;
    }

    private static string? NormalizeConversationStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return status.Trim().ToLowerInvariant() switch
        {
            "open" => "active",
            _ => status,
        };
    }

    private sealed record UpdateLeadStatusRequest(string TenantId, string Status);

    private static Task WriteJsonAsync(HttpResponseData response, object payload, CancellationToken cancellationToken)
    {
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return response.WriteStringAsync(json, cancellationToken);
    }
}
