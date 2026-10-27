using AirwayHVAC.SmsAi.Domain.Entities;
using AirwayHVAC.SmsAi.Domain.Enums;

namespace AirwayHVAC.SmsAi.Application.Services;

public sealed class ConversationFlowEngine
{
    public ConversationFlowResult ProcessCustomerMessage(Conversation conversation, string customerMessage)
        => ProcessCustomerMessage(conversation, customerMessage, ConversationChannel.Sms);

    public ConversationFlowResult ProcessCustomerMessage(Conversation conversation, string customerMessage, ConversationChannel channel)
    {
        var trimmed = customerMessage.Trim();

        CaptureFields(conversation, trimmed);
        AdvanceStage(conversation, trimmed);

        var reply = BuildReply(conversation, channel);
        return new ConversationFlowResult(reply, conversation.Stage);
    }

    private static void CaptureFields(Conversation conversation, string text)
    {
        if (string.IsNullOrWhiteSpace(conversation.Issue) && LooksLikeIssue(text))
        {
            conversation.SetIssue(text);
        }

        if (string.IsNullOrWhiteSpace(conversation.Address) && LooksLikeAddress(text))
        {
            conversation.SetAddress(text);
        }

        if (string.IsNullOrWhiteSpace(conversation.Urgency))
        {
            var urgency = ParseUrgency(text);
            if (urgency is not null)
            {
                conversation.SetUrgency(urgency);
            }
        }

        if (string.IsNullOrWhiteSpace(conversation.PreferredTime) && LooksLikeAppointment(text))
        {
            conversation.SetPreferredTime(text);
        }
    }

    private static void AdvanceStage(Conversation conversation, string customerText)
    {
        if (!string.IsNullOrWhiteSpace(conversation.Issue)
            && !string.IsNullOrWhiteSpace(conversation.Address)
            && !string.IsNullOrWhiteSpace(conversation.Urgency)
            && !string.IsNullOrWhiteSpace(conversation.PreferredTime))
        {
            conversation.SetStage(ConversationStage.Confirm);
        }

        if (conversation.Stage == ConversationStage.Confirm)
        {
            var confirmation = ParseConfirmation(customerText);
            if (confirmation.HasValue)
            {
                conversation.SetConfirmation(confirmation.Value);
                if (confirmation.Value)
                {
                    conversation.SetStage(ConversationStage.Completed);
                }
                else
                {
                    conversation.SetStage(ConversationStage.Appointment);
                    conversation.ClearPreferredTime();
                }

                return;
            }
        }

        if (string.IsNullOrWhiteSpace(conversation.Issue))
        {
            conversation.SetStage(ConversationStage.Issue);
        }
        else if (string.IsNullOrWhiteSpace(conversation.Address))
        {
            conversation.SetStage(ConversationStage.Address);
        }
        else if (string.IsNullOrWhiteSpace(conversation.Urgency))
        {
            conversation.SetStage(ConversationStage.Urgency);
        }
        else if (string.IsNullOrWhiteSpace(conversation.PreferredTime))
        {
            conversation.SetStage(ConversationStage.Appointment);
        }
        else if (conversation.Stage != ConversationStage.Completed)
        {
            conversation.SetStage(ConversationStage.Confirm);
        }
    }

    private static string BuildReply(Conversation conversation, ConversationChannel channel)
        => channel == ConversationChannel.Voice ? BuildVoiceReply(conversation) : BuildSmsReply(conversation);

    private static string BuildSmsReply(Conversation conversation)
        => conversation.Stage switch
        {
            ConversationStage.Issue => "I can help with that. What issue are you having with your HVAC system?",
            ConversationStage.Address => "Thanks. What is the service address?",
            ConversationStage.Urgency => "How urgent is this: emergency, same-day, or flexible?",
            ConversationStage.Appointment => "What appointment time works best for you?",
            ConversationStage.Confirm => $"Please confirm: issue '{conversation.Issue}', address '{conversation.Address}', urgency '{conversation.Urgency}', and preferred time '{conversation.PreferredTime}'. Reply YES to confirm or NO to change.",
            ConversationStage.Completed => "Great, your request is confirmed. Our team will contact you shortly to finalize the appointment.",
            _ => "Could you share more details so I can help schedule your HVAC service?"
        };

    private static string BuildVoiceReply(Conversation conversation)
        => conversation.Stage switch
        {
            ConversationStage.Issue => "Please tell me what issue you are having with your heating or cooling system.",
            ConversationStage.Address => "Thanks. What is the service address?",
            ConversationStage.Urgency => "Is this an emergency, same-day, or flexible?",
            ConversationStage.Appointment => "What day and time works best for a technician visit?",
            ConversationStage.Confirm => $"Let me confirm. You need help with {conversation.Issue}, at {conversation.Address}. The urgency is {conversation.Urgency}, and your preferred time is {conversation.PreferredTime}. Is that correct?",
            ConversationStage.Completed => "Great. Your service request is confirmed. Our team will contact you shortly to finalize the appointment.",
            _ => "Could you share more details so I can help schedule your HVAC service?"
        };

    private static bool LooksLikeIssue(string text)
        => text.Contains("ac", StringComparison.OrdinalIgnoreCase)
           || text.Contains("heat", StringComparison.OrdinalIgnoreCase)
           || text.Contains("furnace", StringComparison.OrdinalIgnoreCase)
           || text.Contains("cool", StringComparison.OrdinalIgnoreCase)
           || text.Contains("hvac", StringComparison.OrdinalIgnoreCase)
           || text.Contains("not working", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeAddress(string text)
        => text.Any(char.IsDigit)
           && (text.Contains("st", StringComparison.OrdinalIgnoreCase)
               || text.Contains("street", StringComparison.OrdinalIgnoreCase)
               || text.Contains("ave", StringComparison.OrdinalIgnoreCase)
               || text.Contains("avenue", StringComparison.OrdinalIgnoreCase)
               || text.Contains("rd", StringComparison.OrdinalIgnoreCase)
               || text.Contains("road", StringComparison.OrdinalIgnoreCase)
               || text.Contains("dr", StringComparison.OrdinalIgnoreCase)
               || text.Contains("drive", StringComparison.OrdinalIgnoreCase)
               || text.Contains("blvd", StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeAppointment(string text)
        => text.Contains("am", StringComparison.OrdinalIgnoreCase)
           || text.Contains("pm", StringComparison.OrdinalIgnoreCase)
           || text.Contains("today", StringComparison.OrdinalIgnoreCase)
           || text.Contains("tomorrow", StringComparison.OrdinalIgnoreCase)
           || text.Contains("morning", StringComparison.OrdinalIgnoreCase)
           || text.Contains("afternoon", StringComparison.OrdinalIgnoreCase)
           || text.Contains("evening", StringComparison.OrdinalIgnoreCase);

    private static string? ParseUrgency(string text)
    {
        if (text.Contains("emergency", StringComparison.OrdinalIgnoreCase) || text.Contains("urgent", StringComparison.OrdinalIgnoreCase) || text.Contains("asap", StringComparison.OrdinalIgnoreCase))
        {
            return "Emergency";
        }

        if (text.Contains("same day", StringComparison.OrdinalIgnoreCase) || text.Contains("today", StringComparison.OrdinalIgnoreCase))
        {
            return "Same-day";
        }

        if (text.Contains("flexible", StringComparison.OrdinalIgnoreCase) || text.Contains("anytime", StringComparison.OrdinalIgnoreCase))
        {
            return "Flexible";
        }

        return null;
    }

    private static bool? ParseConfirmation(string text)
    {
        if (text.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || text.Equals("y", StringComparison.OrdinalIgnoreCase)
            || text.Contains("confirm", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (text.Equals("no", StringComparison.OrdinalIgnoreCase)
            || text.Equals("n", StringComparison.OrdinalIgnoreCase)
            || text.Contains("change", StringComparison.OrdinalIgnoreCase)
            || text.Contains("not correct", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return null;
    }
}

public sealed record ConversationFlowResult(string ReplyMessage, ConversationStage Stage);
