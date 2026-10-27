# Voice AI Implementation Plan

## Nguyên tắc

- Không phá cấu trúc SMS hiện tại.
- Không copy business flow giữa SMS và Voice.
- Thêm Voice AI theo hướng adapter: SMS adapter và Voice adapter dùng chung orchestration.
- MVP ưu tiên Twilio Voice + TwiML + Speech Gather trước realtime WebSocket.
- Realtime gateway chỉ thêm sau khi domain/session/handoff/metrics ổn định.

## Phase 1 - Multi-channel foundation

- Thêm channel vào `Conversation`, default `sms` để tương thích dữ liệu cũ.
- Cho phép `Lead` nhận `source` từ conversation channel thay vì hard-code `sms`.
- Thêm `ConversationOrchestrator` dùng chung cho SMS và Voice.
- Giữ `HandleInboundSmsUseCase` là SMS adapter, không đổi route/API behavior.
- Thêm voice-specific wording trong `ConversationFlowEngine`.

## Phase 2 - Voice MVP

- Thêm `VoiceSession` domain entity.
- Thêm `IVoiceSessionRepository`.
- Thêm Cosmos `VoiceSessionRepository` và document model.
- Thêm `TwilioVoiceOptions`.
- Thêm use cases:
  - `HandleInboundVoiceUseCase`
  - `HandleVoiceGatherUseCase`
  - `HandleVoiceStatusUseCase`
- Thêm Azure Function webhook:
  - `POST /api/webhooks/twilio/voice/inbound`
  - `POST /api/webhooks/twilio/voice/gather`
  - `POST /api/webhooks/twilio/voice/status`
- Trả TwiML XML cho Twilio Voice.

## Phase 3 - Hardening

- Validate Twilio request signature cho SMS và Voice.
- Thêm retry policy theo stage.
- Thêm emergency/handoff classifier tốt hơn.
- Thêm transcript segment persistence.
- Thêm dashboard metrics theo channel.

## Phase 4 - Realtime

- Thêm service riêng cho WebSocket gateway.
- Bridge Twilio Media Streams/Conversation Relay với OpenAI Realtime.
- Dùng lại `ConversationOrchestrator` cho transcript final turns.

## MVP acceptance criteria

- SMS tests cũ vẫn pass.
- SMS behavior không đổi.
- Voice inbound trả TwiML hợp lệ.
- Voice gather xử lý `SpeechResult` qua cùng HVAC flow.
- Lead từ voice có source `voice`.
- VoiceSession được tạo/cập nhật theo `CallSid`.
- Có handoff bằng TwiML `<Dial>` khi khách yêu cầu người thật hoặc không có speech.
