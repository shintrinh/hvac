# Voice AI Plan for AirwayHVAC

## Mục tiêu

Tài liệu này theo dõi phân tích và định hướng triển khai Voice AI cho dự án AirwayHVAC call center. Mục tiêu là thêm khả năng nhận cuộc gọi inbound, hội thoại với khách hàng về nhu cầu HVAC, thu thập thông tin lead, chốt lịch hẹn ở mức request/confirmation, và handoff sang người thật khi cần.

## Bối cảnh dự án hiện tại

Workspace hiện là hệ thống .NET 8 gồm các project chính:

- `AirwayHVAC.SmsAi.Api`: Azure Functions HTTP endpoints.
- `AirwayHVAC.SmsAi.Application`: use cases, services, conversation flow.
- `AirwayHVAC.SmsAi.Domain`: entities, enums, interfaces.
- `AirwayHVAC.SmsAi.Infrastructure`: Cosmos DB repositories, Twilio SMS, OpenAI responder, tenant routing.
- `AirwayHVAC.SmsAi.Shared`: shared abstractions.
- `AirwayHVAC.SmsAi.Application.Tests`: tests cho flow application/routing/dashboard.

Các capability đã có:

- Twilio inbound SMS webhook.
- Tenant routing theo header hoặc số Twilio `To`.
- Conversation stage engine cho HVAC.
- Lead creation/update.
- Event tracking.
- Dashboard service.
- Cosmos persistence.
- Twilio outbound SMS.
- OpenAI chat responder có sẵn nhưng inbound SMS use case hiện đang dùng rule-based `ConversationFlowEngine` thay vì gọi AI responder.

Các stage nghiệp vụ hiện tại:

1. `Issue`
2. `Address`
3. `Urgency`
4. `Appointment`
5. `Confirm`
6. `Completed`

Đây là nền tảng tốt để mở rộng sang voice vì flow HVAC đã deterministic và có thể dùng chung cho nhiều channel.

## Tài liệu/thông tin đã tham khảo

### OpenAI voice/realtime direction

Các model/nhánh kiến trúc được cân nhắc:

- Speech-to-speech realtime, ví dụ GPT Realtime, phù hợp cuộc gọi live cần độ trễ thấp.
- Pipeline rời STT -> LLM -> TTS, phù hợp fallback, batch, post-call summary, hoặc cần kiểm soát từng bước.

Kết luận từ hướng OpenAI:

- Realtime-first là kiến trúc đích tốt nhất cho live call.
- STT/TTS pipeline vẫn hữu ích cho fallback và post-call intelligence.
- Cần guardrail bằng business flow hiện có, không để model tự điều hướng hoàn toàn.

### Twilio Programmable Voice inbound

Twilio Voice inbound hoạt động như sau:

1. Caller gọi vào số Twilio.
2. Twilio gửi HTTP webhook đến app.
3. App trả XML TwiML.
4. Twilio thực thi TwiML: `<Say>`, `<Gather>`, `<Dial>`, `<Connect>`, `<Stream>`, v.v.

Điểm quan trọng cho dự án:

- Azure Functions hiện tại có thể nhận webhook voice giống SMS.
- Voice webhook phải trả TwiML XML, không trả text thường.
- Twilio docs khuyến nghị bảo vệ webhook bằng validation/authentication.
- Với MVP không realtime, có thể dùng `<Gather input="speech">` để nhận speech result từng lượt.
- Với realtime voice, cần Twilio Media Streams hoặc Conversation Relay/WebSocket flow.

## Phân tích kiến trúc khả dụng

## Option A: Twilio Voice + TwiML + Speech Gather

### Mô tả

Azure Function nhận call webhook, trả TwiML chào khách và dùng `<Gather input="speech">` để thu câu trả lời. Sau mỗi lượt, Twilio POST `SpeechResult` về một endpoint gather. Endpoint này đưa text vào flow engine hiện có, sinh câu hỏi tiếp theo, trả TwiML mới, và lặp lại.

### Flow mẫu

1. `POST /api/webhooks/twilio/voice/inbound`
2. Tạo/load conversation và voice session.
3. Track `voice_call_started`.
4. Trả TwiML:
   - disclosure AI/recording nếu cần,
   - hỏi issue.
5. Twilio gửi `POST /api/webhooks/twilio/voice/gather` với `SpeechResult`.
6. App gọi chung orchestration:
   - append customer message,
   - run `ConversationFlowEngine`,
   - update lead,
   - track events.
7. Trả TwiML `<Say>` + `<Gather>` cho stage tiếp theo.
8. Nếu completed: nói kết thúc và hangup hoặc chuyển team.
9. Nếu handoff: `<Dial>` đến agent/dispatch number.

### Ưu điểm

- Phù hợp codebase hiện tại nhất.
- Có thể triển khai trong Azure Functions, không cần service WebSocket riêng.
- Tận dụng tối đa `ConversationFlowEngine`, `LeadService`, `EventService`, tenant routing.
- Dễ test bằng webhook ngrok/Twilio.
- Rủi ro kỹ thuật thấp cho MVP.

### Nhược điểm

- Trải nghiệm không tự nhiên bằng realtime voice.
- Mỗi turn là một webhook, latency cao hơn.
- Turn-taking cứng, caller có thể cảm thấy giống IVR.
- Speech recognition phụ thuộc Twilio gather.
- Khó tối ưu barge-in/conversational latency.

### Phù hợp khi

- Cần MVP nhanh.
- Muốn validate business flow voice trước khi đầu tư realtime.
- Chưa muốn thêm service vận hành riêng.

## Option B: Twilio Media Streams + OpenAI Realtime gateway

### Mô tả

Twilio kết nối audio stream qua WebSocket đến một Voice Gateway. Gateway bridge audio/event giữa Twilio và OpenAI Realtime. Khi có transcript final hoặc intent event, gateway gọi application orchestrator để cập nhật conversation/lead/stage. AI trả audio realtime về caller.

### Thành phần cần có

- `VoiceGateway` service riêng, nên là ASP.NET Core app chạy trên Azure App Service hoặc Azure Container Apps.
- WebSocket endpoint nhận Twilio media events.
- OpenAI Realtime session client.
- Session state in-memory + persistent checkpoints.
- Orchestrator client hoặc shared application package.
- Metrics/logging cho latency, errors, reconnect.

### Ưu điểm

- Trải nghiệm tốt nhất cho live call.
- Độ trễ thấp hơn.
- Có thể hỗ trợ natural turn-taking, interrupt/barge-in tốt hơn.
- Phù hợp mục tiêu Voice AI thực sự.

### Nhược điểm

- Phức tạp hơn đáng kể.
- Azure Functions không phải nơi tốt nhất để giữ WebSocket streaming dài.
- Cần vận hành thêm service.
- Cần xử lý audio format, reconnect, stream lifecycle, session timeout, backpressure.
- Test/debug khó hơn.

### Phù hợp khi

- MVP speech gather đã chứng minh value.
- Cần trải nghiệm voice giống người thật.
- Có thời gian đầu tư reliability/observability.

## Option C: STT -> LLM -> TTS pipeline

### Mô tả

Nhận audio, transcribe bằng STT, gọi LLM xử lý, synthesize voice bằng TTS, phát lại cho caller.

### Ưu điểm

- Kiểm soát từng bước rõ.
- Dễ ghi log/debug.
- Có thể tối ưu cost từng component.
- Hữu ích cho fallback khi realtime lỗi hoặc audio khó.

### Nhược điểm

- Latency thường cao hơn realtime speech-to-speech.
- Cần tự orchestration nhiều hơn.
- Với live call, trải nghiệm có thể kém nếu không tối ưu tốt.

### Phù hợp khi

- Fallback cho realtime.
- Post-call processing.
- Offline summary/tagging.
- Các bước cần kiểm soát chặt, ví dụ trích xuất appointment details.

## Kết luận kiến trúc tốt nhất

Khuyến nghị tốt nhất cho dự án này là triển khai theo 2 tầng:

### Tầng 1: Voice MVP bằng Twilio TwiML + Speech Gather

Lý do:

- Ít rủi ro nhất.
- Dùng được Azure Functions hiện tại.
- Validate được tenant routing, conversation flow, lead update, handoff logic.
- Tạo foundation domain/application đúng trước khi realtime.

### Tầng 2: Realtime Voice Gateway khi MVP ổn định

Lý do:

- Đây là hướng đích cho trải nghiệm call center tự nhiên.
- Nên triển khai sau khi đã có `VoiceSession`, channel-agnostic orchestrator, event model và handoff policy.

Nói ngắn gọn: không nên nhảy thẳng vào realtime gateway khi core domain còn SMS-first. Nên refactor core thành multi-channel trước, rồi nâng cấp transport voice.

## Các vấn đề hiện tại cần xử lý trước khi thêm Voice AI

## 1. SMS use case đang chứa orchestration nghiệp vụ

`HandleInboundSmsUseCase` hiện đang làm nhiều việc:

- Resolve tenant.
- Load/create conversation.
- Append customer message.
- Track event.
- Run flow engine.
- Append AI message.
- Create/update lead.
- Save conversation.
- Send SMS.
- Track sent event.

Nếu thêm voice bằng cách copy logic này, sẽ tạo duplication. Nên tách phần chung thành một service/use case channel-agnostic.

### Đề xuất

Thêm application service:

- `ConversationOrchestrator`

Input đề xuất:

- tenant id hoặc resolved tenant,
- customer phone,
- inbound text,
- channel: `sms` hoặc `voice`,
- correlation id/provider session id,
- timestamp.

Output đề xuất:

- conversation id,
- lead id,
- reply text,
- stage,
- flags: completed, handoff requested.

SMS use case chỉ còn:

- parse/resolve Twilio SMS,
- gọi orchestrator,
- gửi SMS.

Voice gather use case chỉ còn:

- parse Twilio Voice gather,
- gọi orchestrator,
- build TwiML response.

## 2. Conversation chưa có channel

`Conversation` hiện không có `Channel`. ID hiện dựa theo phone: `conv_{NormalizePhone(phone)}`. Với cùng khách, SMS và voice có thể đụng cùng conversation hoặc cần thread khác.

### Lựa chọn

- Nếu muốn unified customer journey: cùng phone có thể dùng chung conversation, nhưng cần channel per message/session.
- Nếu muốn tách channel: conversation id nên gồm channel, ví dụ `conv_voice_{phone}` và `conv_sms_{phone}`.

### Khuyến nghị MVP

Thêm `Channel` vào `Conversation`, default `sms` cho backward compatibility. Với voice, tạo conversation channel `voice`. Về sau có thể unify bằng customer profile.

## 3. Lead source đang hard-code `sms`

`Lead.CreateNew` hiện tạo lead với source `sms`. Voice cần source `voice`.

### Đề xuất

Sửa factory:

- `Lead.CreateNew(string tenantId, string phone, string conversationId, DateTimeOffset now, string source = "sms")`

Hoặc tạo value object/enum channel/source nếu muốn chặt hơn.

## 4. Reply hiện tối ưu cho SMS, chưa tối ưu cho Voice

`ConversationFlowEngine.BuildReply` hiện tạo text kiểu SMS, ví dụ `Reply YES to confirm or NO to change`. Với voice cần câu ngắn, tự nhiên, không nhắc `Reply`.

### Đề xuất

Không nên hard-code một reply cho mọi channel. Có 2 hướng:

- Minimal: thêm `ConversationChannel` param vào flow engine và build reply theo channel.
- Tốt hơn: flow engine trả `Stage` + captured data, formatter riêng quyết định wording.

MVP có thể dùng formatter:

- `SmsConversationResponseFormatter`
- `VoiceConversationResponseFormatter`

## 5. Chưa có handoff state

Voice call center bắt buộc có handoff rõ ràng.

### Handoff trigger đề xuất

- Caller nói: `human`, `agent`, `representative`, `operator`, `talk to someone`.
- Emergency keywords: gas smell, electrical burning, fire, carbon monoxide, no heat with vulnerable occupant.
- Không hiểu sau N lần.
- Caller im lặng quá lâu.
- Caller từ chối AI.
- Appointment conflict hoặc ngoài khả năng đặt lịch.

### Domain fields đề xuất

Trong `Conversation` hoặc entity riêng:

- `HandoffRequested`
- `HandoffReason`
- `HandoffRequestedAt`

Với voice nên lưu trong `VoiceSession` trước, sau đó có thể đồng bộ vào conversation.

## 6. Chưa có VoiceSession

Voice call là sessionful. Một phone có thể có nhiều call, mỗi call có lifecycle riêng. Không nên chỉ dựa vào `Conversation`.

### Entity đề xuất

`VoiceSession`:

- `Id`
- `TenantId`
- `ConversationId`
- `Provider`: `twilio`
- `ProviderCallId`: Twilio `CallSid`
- `FromPhone`
- `ToPhone`
- `Status`: `initiated`, `in-progress`, `completed`, `failed`, `handoff`, `abandoned`
- `StartedAt`
- `EndedAt`
- `LastActivityAt`
- `CurrentStage`
- `TurnCount`
- `RetryCount`
- `SilenceTimeoutCount`
- `HandoffReason`
- `AverageLatencyMs`
- `MaxLatencyMs`
- `FallbackCount`

Repository:

- `IVoiceSessionRepository`
- `VoiceSessionRepository`
- `VoiceSessionDocument`

Cosmos container option:

- `VoiceSessionsContainer`

## 7. Chưa có transcript segment model

`Conversation.Messages` đủ cho SMS nhưng voice cần transcript segment.

### Model đề xuất

`TranscriptSegment`:

- `Id`
- `TenantId`
- `VoiceSessionId`
- `ConversationId`
- `Role`: `customer`, `ai`, `agent`
- `Text`
- `Confidence`
- `IsFinal`
- `StartOffsetMs`
- `EndOffsetMs`
- `Source`: `twilio_gather`, `openai_realtime`, `fallback_stt`, `manual`
- `CreatedAt`

MVP có thể chưa cần container riêng; có thể lưu vào messages trước. Nhưng để analytics tốt, nên có entity riêng ở phase sau.

## 8. Webhook security chưa đủ

SMS webhook hiện `AuthorizationLevel.Anonymous`, phù hợp Twilio webhook nhưng cần signature validation.

### Rủi ro

Người ngoài có thể gọi endpoint giả để:

- tạo conversation/lead giả,
- spam SMS outbound,
- tạo voice sessions giả,
- gây tốn chi phí.

### Đề xuất

- Validate `X-Twilio-Signature` cho SMS và Voice.
- Dùng Twilio `AuthToken` từ config.
- Reject request invalid bằng 403.
- Vẫn giữ tenant routing verification cho internal/manual calls nếu cần.

## 9. Config hiện chưa có Voice settings

Cần thêm config riêng cho voice.

### Đề xuất

`VoiceAi` hoặc `TwilioVoice` config:

- `Enabled`
- `PublicBaseUrl`
- `HumanHandoffPhoneNumber`
- `GatherTimeoutSeconds`
- `MaxRetriesPerStage`
- `MaxCallDurationSeconds`
- `DisclosureMessage`
- `VoiceName`
- `Language`
- `RealtimeStreamUrl`

Ví dụ:

```json
"TwilioVoice": {
  "Enabled": false,
  "PublicBaseUrl": "",
  "HumanHandoffPhoneNumber": "",
  "GatherTimeoutSeconds": 5,
  "MaxRetriesPerStage": 2,
  "MaxCallDurationSeconds": 900,
  "DisclosureMessage": "This call may be handled by an AI assistant and recorded for quality purposes.",
  "VoiceName": "alice",
  "Language": "en-US"
}
```

## Đề xuất thiết kế MVP chi tiết

## API endpoints

### 1. `POST /api/webhooks/twilio/voice/inbound`

Input Twilio form fields:

- `From`
- `To`
- `CallSid`
- `AccountSid`
- `CallStatus`
- `Direction`
- `FromCity`, `FromState` nếu có.

Responsibility:

- Validate Twilio signature.
- Resolve tenant giống SMS, ưu tiên `To` phone mapping.
- Create `VoiceSession`.
- Track `voice_call_started`.
- Return TwiML greeting + first gather.

TwiML behavior:

- `<Say>` disclosure/greeting.
- `<Gather input="speech" action="/api/webhooks/twilio/voice/gather" method="POST" timeout="5">`
- prompt theo stage `Issue`.
- fallback nếu không nói gì: `<Redirect>` hoặc handoff.

### 2. `POST /api/webhooks/twilio/voice/gather`

Input Twilio form fields:

- `CallSid`
- `From`
- `To`
- `SpeechResult`
- `Confidence` nếu Twilio gửi.

Responsibility:

- Validate signature.
- Load `VoiceSession` by `CallSid`.
- Nếu không có `SpeechResult`: retry hoặc handoff.
- Call `ConversationOrchestrator` với channel `voice`.
- Update session turn count/current stage.
- Build TwiML voice reply.
- Nếu completed: `<Say>` closing + maybe `<Hangup>`.
- Nếu handoff: `<Say>` + `<Dial>`.
- Else: `<Say>` reply + next `<Gather>`.

### 3. `POST /api/webhooks/twilio/voice/status`

Input Twilio status callback:

- `CallSid`
- `CallStatus`
- `CallDuration`

Responsibility:

- Update `VoiceSession` status.
- Track:
  - `voice_call_completed`
  - `voice_call_failed`
  - `voice_call_abandoned`
- Trigger post-call summary later.

## TwiML patterns

### Greeting + first gather

```xml
<Response>
  <Say>This call may be handled by an AI assistant for scheduling HVAC service.</Say>
  <Gather input="speech" action="https://public-host/api/webhooks/twilio/voice/gather" method="POST" timeout="5">
    <Say>Please tell me what issue you are having with your heating or cooling system.</Say>
  </Gather>
  <Say>I did not hear a response. Please hold while I connect you to our team.</Say>
  <Dial>+15551234567</Dial>
</Response>
```

### Continue next stage

```xml
<Response>
  <Gather input="speech" action="https://public-host/api/webhooks/twilio/voice/gather" method="POST" timeout="5">
    <Say>Thanks. What is the service address?</Say>
  </Gather>
  <Say>I did not hear a response. Please hold while I connect you to our team.</Say>
  <Dial>+15551234567</Dial>
</Response>
```

### Handoff

```xml
<Response>
  <Say>Please hold while I connect you to our team.</Say>
  <Dial>+15551234567</Dial>
</Response>
```

### Completed

```xml
<Response>
  <Say>Great. Your service request is confirmed. Our team will contact you shortly to finalize the appointment.</Say>
  <Hangup />
</Response>
```

## Voice-specific conversation wording

Voice replies should be short and natural.

Suggested stage prompts:

- `Issue`: `Please tell me what issue you are having with your heating or cooling system.`
- `Address`: `Thanks. What is the service address?`
- `Urgency`: `Is this an emergency, same-day, or flexible?`
- `Appointment`: `What day and time works best for a technician visit?`
- `Confirm`: `Let me confirm. You need help with {issue}, at {address}. The urgency is {urgency}, and your preferred time is {preferredTime}. Is that correct?`
- `Completed`: `Great. Your service request is confirmed. Our team will contact you shortly to finalize the appointment.`

Avoid SMS-specific wording like:

- `Reply YES`
- `Reply NO`

Use instead:

- `Please say yes to confirm, or no to change it.`

## Domain/application changes recommended before code implementation

## Phase 1: Multi-channel foundation

Tasks:

- Add `ConversationChannel` concept or string channel.
- Add source parameter to `Lead.CreateNew`.
- Introduce `ConversationOrchestrator` to share business logic.
- Keep `HandleInboundSmsUseCase` behavior unchanged but delegate common work.
- Add tests ensuring SMS flow still passes.

Expected result:

- SMS works exactly as before.
- Voice can reuse same orchestration without copying SMS use case.

## Phase 2: Voice MVP with TwiML Gather

Tasks:

- Add `TwilioVoiceOptions`.
- Add `VoiceSession` entity.
- Add `IVoiceSessionRepository` and in-memory/Cosmos implementation.
- Add `InboundVoiceFunction`.
- Add `VoiceGatherFunction`.
- Add `VoiceStatusFunction`.
- Add `VoiceTwiMLBuilder` or TwiML response service.
- Add event tracking for voice.
- Add tests for use cases and TwiML generation.

Expected result:

- Caller can call Twilio number.
- System gathers HVAC info over speech gather.
- Lead is created/updated with source `voice`.
- Handoff works through `<Dial>`.

## Phase 3: Handoff and safety hardening

Tasks:

- Add handoff detection service.
- Add retry counters per stage.
- Add emergency keyword escalation.
- Add silence timeout policy.
- Add max call duration policy.
- Add Twilio signature validation.
- Add compliance disclosure configuration.

Expected result:

- Voice flow is safer for production trial.
- Human fallback is reliable.

## Phase 4: Post-call intelligence

Tasks:

- Add post-call summary job/use case.
- Add lead quality tags.
- Add appointment detail extraction.
- Add transcript persistence.
- Add dashboard metrics by channel.

Expected result:

- Better lead qualification.
- Better analytics for HVAC business.

## Phase 5: Realtime gateway

Tasks:

- Add separate `AirwayHVAC.SmsAi.VoiceGateway` ASP.NET Core service.
- Add WebSocket endpoint for Twilio Media Streams/Conversation Relay.
- Add OpenAI Realtime adapter.
- Bridge realtime audio and events.
- Use same `ConversationOrchestrator` for final transcript turns.
- Track latency metrics.
- Add fallback to TwiML handoff on gateway failures.

Expected result:

- Natural realtime AI voice assistant.
- Business logic still shared with SMS and Gather MVP.

## Event taxonomy proposal

Existing events:

- `sms_received`
- `sms_sent`
- `lead_created`
- `stage_changed`
- `appointment_requested`
- `booking_confirmed`

Add voice events:

- `voice_call_started`
- `voice_call_connected`
- `voice_gather_prompted`
- `voice_speech_received`
- `voice_speech_empty`
- `voice_retry_prompted`
- `voice_handoff_requested`
- `voice_handoff_started`
- `voice_handoff_failed`
- `voice_call_completed`
- `voice_call_failed`
- `voice_call_abandoned`
- `voice_latency_recorded`
- `voice_stt_fallback_used`
- `post_call_summary_created`

## Analytics proposal

Add dashboard metrics:

- Conversations by channel.
- Leads by source.
- Voice call count.
- Average voice call duration.
- Voice handoff rate.
- Voice completion rate.
- Voice booking/request conversion rate.
- Average turn latency.
- Silence/no-response count.
- Retry count by stage.
- Emergency escalation count.

## Security and compliance checklist

- Validate Twilio request signature for every Twilio webhook.
- Do not trust `From`, `To`, `CallSid` without signature validation.
- Keep `AuthorizationLevel.Anonymous` only because Twilio needs to call it, but enforce signature validation internally.
- Never commit real API keys/secrets in appsettings.
- Move SendGrid/Twilio/OpenAI secrets to environment variables or Key Vault.
- Add AI/recording disclosure message.
- Avoid collecting payment/medical/sensitive info in AI flow.
- Add retention policy for transcripts/recordings.
- Add handoff for emergencies or uncertain cases.

## Operational concerns

### Latency budget

For TwiML Gather MVP:

- Target after speech submit: under 2-3 seconds to next prompt.
- Acceptable for MVP, but not ideal for natural conversation.

For realtime gateway:

- Target turn latency: under 1.5-2 seconds.
- Track separately:
  - ASR latency,
  - model latency,
  - TTS/audio response latency,
  - end-to-end turn latency.

### Reliability

- Voice session must survive function restarts.
- Gather endpoints should be idempotent enough for retries.
- Status callback should close sessions even if gather flow ends unexpectedly.
- Handoff number must be configurable per tenant eventually.

### Cost control

- Max call duration.
- Max retry count.
- Silence timeout.
- Early handoff for non-service calls.
- Avoid unnecessary OpenAI calls during rule-based MVP.
- Use post-call AI asynchronously instead of during live path when possible.

## Tenant-specific considerations

Current tenant model has:

- company name,
- phone number,
- service area,
- timezone,
- business hours.

Voice should eventually add tenant-level settings:

- handoff phone number,
- after-hours message,
- emergency policy,
- service categories,
- booking windows,
- disclosure message,
- voice/language.

MVP can use global `TwilioVoiceOptions.HumanHandoffPhoneNumber`, then move to tenant config later.

## Recommended implementation order

1. Refactor common conversation handling into `ConversationOrchestrator`.
2. Add channel/source support with backward-compatible defaults.
3. Add voice response formatter.
4. Add Twilio voice options.
5. Add voice session domain/repository.
6. Add inbound voice webhook returning TwiML.
7. Add gather webhook processing speech result.
8. Add status webhook.
9. Add handoff policy.
10. Add Twilio signature validation.
11. Add post-call summary.
12. Add realtime gateway only after MVP validation.

## Initial acceptance criteria for MVP

- Incoming Twilio voice webhook returns valid TwiML XML.
- The caller hears AI disclosure and first HVAC question.
- Speech result is processed through the same HVAC stage engine.
- Voice replies do not use SMS wording.
- Lead created from voice has source `voice`.
- Conversation and events are persisted.
- Handoff to configured human number works.
- Status callback closes voice session.
- SMS tests continue passing.
- New voice use case tests cover happy path, no speech, handoff, and completed flow.

## Open questions

- Should SMS and voice share a conversation per phone, or keep separate conversations per channel?
- Should handoff number be global for MVP or per tenant from day one?
- Should recordings be enabled? If yes, what retention and disclosure policy?
- Which Twilio voice product should be used for realtime phase: Media Streams or Conversation Relay?
- Which OpenAI endpoint/provider will be used in production: OpenAI platform realtime or Azure OpenAI realtime if available?
- Should booking confirmation create a real calendar/job entry, or just mark lead as booked/requested?

## Current recommendation

Best solution for this repository:

1. Do not implement realtime first.
2. First make the core application multi-channel.
3. Implement Twilio Voice MVP with TwiML + speech Gather inside existing Azure Functions.
4. Add voice session and handoff capabilities.
5. Validate with real calls.
6. Then add a dedicated realtime Voice Gateway service when the business flow and metrics are stable.

This approach reduces risk, reuses the strongest existing parts of the codebase, and avoids locking the business logic to a specific voice transport.
