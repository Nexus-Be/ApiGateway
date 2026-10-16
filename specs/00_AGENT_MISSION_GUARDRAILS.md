# 00 — NHIỆM VỤ CỐT LÕI & RÀO CHẮN CHỐNG ẢO GIÁC (API GATEWAY)

---

## 1. NHIỆM VỤ & DANH TÍNH CỦA REPOSITORY

Repository này chứa mã nguồn của **Nexus API Gateway (.NET 8)**:
- Đóng vai trò là **Edge Reverse Proxy** duy nhất đứng trước toàn bộ 8 Microservices backend.
- Cung cấp giao diện RESTful JSON cho các ứng dụng Client (Web, Mobile).
- Chuyển đổi và định tuyến các HTTP request sang các cuộc gọi **gRPC strongly-typed** dựa trên Protobuf v3 (`Contracts/shop.proto`).
- Đảm bảo an ninh vòng ngoài: Kiểm tra token JWT, kiểm soát quyền truy cập RBAC, áp đặt giới hạn tần suất gọi (Distributed Rate Limiting qua Redis), giới hạn kích thước gói tin (Kestrel MaxRequestBodySize = 64KB).
- Hỗ trợ độc lập phát triển cho Frontend qua **Mock Engine** và **Cơ chế 2 trạng thái Gateway** (`Standard` vs `Bypass`).

---

## 2. NHỮNG ĐIỀU CẤM KỴ ĐỐI VỚI AI AGENTS (ANTI-HALLUCINATION GUARDRAILS)

1. **TUYỆT ĐỐI KHÔNG VIẾT IF/ELSE MOCK HAY REAL TRONG CONTROLLER**:
   - Controller chỉ là lớp đầu vào (Input) mỏng, chỉ kiểm tra tính hợp lệ cơ bản của DTOs và gọi duy nhất 1 dòng sang `_dispatcher.DispatchAsync` hoặc `_dispatcher.DispatchPagedAsync`.
   - Toàn bộ việc phân giải Mock vs Real, đọc Seed JSON, gọi gRPC, forward headers thuộc về `Dispatching Core` (`ModeEvaluator`, `MockDataMapper`, `GrpcServiceCaller`, `GatewayDispatcher`).

2. **TUYỆT ĐỐI KHÔNG ĐƯA DATABASE IN-MEMORY HAY LOGIC TÍNH TOÁN VÀO GATEWAY**:
   - Gateway không sở hữu database nghiệp vụ.
   - Khi ở chế độ Mock, Gateway chỉ đọc các file fixture tĩnh từ thư mục `Seeds/{service}/{entity}.json` và kiểm tra cú pháp bằng Protobuf `MessageParser.ParseJson()`. Không được viết code in-memory DB (như `ConcurrentDictionary` lưu trữ state) giả lập nghiệp vụ backend.

3. **TUYỆT ĐỐI KHÔNG SỬA ĐỔI PROTOBUF FIELD TAGS CỦA `shop.proto`**:
   - File `Contracts/shop.proto` là nguồn sự thật duy nhất. Nếu thêm trường mới, phải dùng tag number tiếp theo. Tuyệt đối không xóa hoặc thay đổi tag number đã có.

4. **TUYỆT ĐỐI KHÔNG THAY ĐỔI CỔNG 8000**:
   - Mọi Client và tài liệu đều đã cấu hình Gateway ở cổng `8000`.

5. **TUYỆT ĐỐI KHÔNG LÀM HỎNG 46 BÀI KIỂM THỬ TỰ ĐỘNG**:
   - Mọi thay đổi mã nguồn trong Gateway bắt buộc phải vượt qua toàn bộ 46 tests trong `Nexus.Gateway.Tests`.
