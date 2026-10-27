# Voice AI Setup Checklist cho HVAC Contractor

Tài liệu này dùng để setup nhanh cho một HVAC contractor chạy tính năng Voice AI inbound call.

## Checklist ngắn gọn

### 1. Thêm số contractor vào bảng nào?

Thêm contractor vào Cosmos DB container:

```plaintext
tenants
```

Field quan trọng nhất:

```plaintext
phoneNumber
```

`phoneNumber` phải là **số Twilio mà customer sẽ gọi vào**.

Ví dụ customer gọi vào số Twilio:

```plaintext
+18777804236
```

Thì tenant trong bảng `tenants` phải có:

```json
{
  "id": "tenant_airway_hvac",
  "tenantId": "tenant_airway_hvac",
  "companyName": "Airway HVAC",
  "phoneNumber": "+18777804236",
  "serviceArea": "Houston TX",
  "timezone": "America/Chicago",
  "businessHours": {
    "open": "08:00",
    "close": "18:00"
  },
  "createdAt": "2026-01-01T00:00:00Z"
}
```

Lưu ý:

- `phoneNumber` = số Twilio nhận cuộc gọi.
- Hệ thống dùng số `To` từ Twilio để tìm tenant.
- Nếu tenant không có số này, voice webhook sẽ báo lỗi tenant không resolve được.

---

### 2. Customer gọi vào số nào?

Customer chỉ cần gọi vào **số Twilio của contractor**.

Ví dụ:

```plaintext
+18777804236
```

Luồng sẽ là:

1. Customer gọi `+18777804236`.
2. Twilio gọi webhook của hệ thống.
3. Hệ thống tìm contractor trong bảng `tenants` bằng `phoneNumber = +18777804236`.
4. Voice AI hỏi khách:
   - vấn đề HVAC là gì,
   - địa chỉ service,
   - mức độ khẩn cấp,
   - thời gian mong muốn,
   - xác nhận lại thông tin.
5. Hệ thống tạo/update lead với source `voice`.

---

### 3. Cấu hình webhook trong Twilio

Trong Twilio Console, mở số điện thoại của contractor.

Phần **A call comes in** cấu hình:

```plaintext
Webhook
POST
https://<your-api-domain>/api/webhooks/twilio/voice/inbound
```

Ví dụ local qua ngrok:

```plaintext
https://abc123.ngrok-free.app/api/webhooks/twilio/voice/inbound
```

Status callback:

```plaintext
POST
https://<your-api-domain>/api/webhooks/twilio/voice/status
```

---

### 4. Cấu hình số handoff cho contractor

Nếu muốn chuyển khách sang người thật, cấu hình:

```plaintext
TWILIO_VOICE_HANDOFF_NUMBER=+15551234567
```

Đây là số nhân viên/dispatcher của contractor.

Khi khách nói:

```plaintext
I want to talk to a human
```

hệ thống sẽ trả TwiML `<Dial>` đến số này.

Lưu ý hiện tại:

- Handoff number đang là config chung.
- Chưa lưu per-contractor trong bảng `tenants`.

---

### 5. Các config cần có

```plaintext
TWILIO_SID=<Twilio Account SID>
TWILIO_TOKEN=<Twilio Auth Token>
TWILIO_NUMBER=<Twilio number>

TWILIO_VOICE_PUBLIC_BASE_URL=<public API URL>
TWILIO_VOICE_HANDOFF_NUMBER=<contractor human phone>
TWILIO_VOICE_DISCLOSURE_MESSAGE=This call may be handled by an AI assistant for scheduling HVAC service.

COSMOS_ENDPOINT=<Cosmos endpoint>
COSMOS_KEY=<Cosmos key>
COSMOS_DATABASE=hvac-saas
COSMOS_TENANTS_CONTAINER=tenants
COSMOS_CONVERSATIONS_CONTAINER=conversations
COSMOS_LEADS_CONTAINER=leads
COSMOS_EVENTS_CONTAINER=events
COSMOS_VOICE_SESSIONS_CONTAINER=voiceSessions
```

---

### 6. Các bảng/container cần có

```plaintext
tenants
conversations
leads
events
voiceSessions
```

Trong đó:

- `tenants`: lưu contractor và số Twilio của contractor.
- `conversations`: lưu hội thoại SMS/Voice.
- `leads`: lưu lead được tạo từ khách gọi.
- `events`: lưu tracking events.
- `voiceSessions`: lưu session cuộc gọi voice.

Nếu `voiceSessions` chưa tồn tại, code hiện tại sẽ tự tạo với partition key:

```plaintext
/tenantId
```

---

### 7. Test nhanh local

Chạy API:

```powershell
cd "C:\Data\Project\New folder\AirwayHVAC\src\AirwayHVAC.SmsAi.Api"
func start
```

Test inbound voice:

```powershell
Invoke-WebRequest -Method Post -Uri "http://localhost:7071/api/webhooks/twilio/voice/inbound" -ContentType "application/x-www-form-urlencoded" -Body "From=%2B15555550123&To=%2B18777804236&CallSid=CA_TEST_001"
```

Kỳ vọng trả về XML TwiML có:

```xml
<Gather input="speech">
```

Test khách nói issue:

```powershell
Invoke-WebRequest -Method Post -Uri "http://localhost:7071/api/webhooks/twilio/voice/gather" -ContentType "application/x-www-form-urlencoded" -Body "CallSid=CA_TEST_001&SpeechResult=My AC is blowing warm air&Confidence=0.92"
```

Kỳ vọng hệ thống hỏi:

```plaintext
Thanks. What is the service address?
```

---

### 8. Test bằng cuộc gọi thật

Checklist:

- [ ] API đã deploy public hoặc đang expose bằng ngrok.
- [ ] Twilio Voice webhook trỏ đến `/api/webhooks/twilio/voice/inbound`.
- [ ] Container `tenants` có contractor với `phoneNumber` = số Twilio.
- [ ] `TWILIO_VOICE_HANDOFF_NUMBER` đã set nếu muốn chuyển người thật.
- [ ] Customer gọi vào số Twilio.

Kịch bản test:

```plaintext
Customer gọi số Twilio
Customer nói: My AC is blowing warm air
Customer nói: 123 Main Street
Customer nói: emergency
Customer nói: tomorrow 9 AM
Customer nói: yes
```

Kỳ vọng:

- Lead được tạo trong `leads`.
- Lead source = `voice`.
- Conversation được tạo trong `conversations`.
- Voice session được tạo trong `voiceSessions`.
- Events được ghi trong `events`.

---

## Tóm tắt cực ngắn

1. Thêm contractor vào bảng/container `tenants`.
2. Field `phoneNumber` phải là số Twilio của contractor.
3. Cấu hình Twilio webhook về `/api/webhooks/twilio/voice/inbound`.
4. Customer chỉ cần gọi vào số Twilio đó.
5. Voice AI sẽ tự hỏi thông tin HVAC và tạo lead.
