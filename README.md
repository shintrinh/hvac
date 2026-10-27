# AirwayHVAC

Backend Azure Functions (.NET 8, isolated worker) cho luồng SMS tự động trong bài toán HVAC:
1. Nhận SMS inbound từ Twilio webhook.
2. Lưu hội thoại + sự kiện + lead vào Cosmos DB.
3. Gọi Azure OpenAI để sinh phản hồi.
4. Gửi SMS outbound lại cho khách qua Twilio.

## Luồng xử lý chính
Endpoint webhook:
- **Function name:** `InboundSmsWebhook`
- **Route:** `POST /api/webhooks/twilio/inbound`
- **Payload:** `application/x-www-form-urlencoded` với các field tối thiểu `From`, `To`, `Body`

Luồng xử lý:

`Twilio -> InboundSmsWebhook -> HandleInboundSmsUseCase -> (Conversation/Lead/Event) -> Azure OpenAI -> Twilio gửi outbound`

### Yêu cầu
- .NET SDK 8.0+
- Azure Functions Core Tools 4+
- Tài khoản Twilio + Azure OpenAI + Cosmos DB

## Dashboard API (mini)

Các endpoint dashboard mới (đa tenant, bắt buộc `tenantId`):
- `GET /api/dashboard/conversations`
- `GET /api/dashboard/messages`
- `GET /api/dashboard/leads`
- `PATCH /api/dashboard/leads/{leadId}/status`
- `GET /api/dashboard/analytics`

Các API này phục vụ mini dashboard vận hành với phân trang, cập nhật trạng thái lead theo luồng `new -> contacted -> booked -> closed`, và số liệu tổng quan theo tenant.

## Email API (SendGrid)

Đã bổ sung function gửi email qua SendGrid:
- **Function name:** `SendMail`
- **Route:** `POST /api/notifications/email`
- **Authorization:** `Function`
- **Content-Type:** `application/json`

### Request body
```json
{
  "email": "customer@example.com",
  "content": "Xin chào, đây là nội dung email.",
  "subject": "Thong bao tu AirwayHVAC"
}
```

Trong đó:
- `email` (bắt buộc): email người nhận.
- `content` (bắt buộc): nội dung text email.
- `subject` (tuỳ chọn): tiêu đề email. Nếu bỏ qua sẽ dùng mặc định `AirwayHVAC Notification`.


Ngoài JSON body, function cũng hỗ trợ truyền `email`, `content`, `subject` qua **query parameters** (hữu ích khi test trực tiếp trong Azure Portal "Test/Run" tab).

Ví dụ:
`POST /api/notifications/email?email=customer@example.com&content=Hello&subject=Test`

### Cấu hình môi trường
Thiết lập các biến môi trường sau:
- `SENDGRID_API_KEY`
- `SENDGRID_FROM_EMAIL`
- `SENDGRID_FROM_NAME` (tuỳ chọn, mặc định: `AirwayHVAC`)

Bạn cũng có thể cấu hình tương ứng trong `appsettings.json` theo key:
- `SendGrid:ApiKey`
- `SendGrid:FromEmail`
- `SendGrid:FromName`

### Response
- Thành công: trả về `200 OK` với JSON chứa `success`, `to`, `subject`.
- Lỗi validate payload: `400 BadRequest`.
- Thiếu cấu hình SendGrid: `500 InternalServerError`.
- Gửi mail thất bại từ SendGrid: `502 BadGateway`.
