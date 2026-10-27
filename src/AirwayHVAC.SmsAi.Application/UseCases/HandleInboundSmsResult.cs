namespace AirwayHVAC.SmsAi.Application.UseCases;

public sealed record HandleInboundSmsResult(string ConversationId, string LeadId, string ReplyMessage);
