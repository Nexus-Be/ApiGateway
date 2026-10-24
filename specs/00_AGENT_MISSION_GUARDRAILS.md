# 00 — NHIỆM VỤ CỐT LÕI & RÀO CHẮN CHỐNG ẢO GIÁC (API GATEWAY)
> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — 4/15 controller có action (16 action thật), 1/8 gRPC client, 1 service/5 rpc trong `shop.proto`, 62 attribute test trên 19 file.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-24.
---
## 1. NHIỆM VỤ & DANH TÍNH CỦA REPOSITORY
Repository này chứa mã nguồn của **Nexus API Gateway (.NET 8)**:
- Đóng vai trò là **Edge Reverse Proxy** duy nhất đứng trước toàn bộ 8 Microservices backend.
- Cung cấp giao diện RESTful JSON cho các ứng dụng Client (Web, Mobile).
- Chuyển đổi và định tuyến các HTTP request sang các cuộc gọi **gRPC strongly-typed** dựa trên Protobuf v3 (`Nexus.Gateway/Contracts/shop.proto`).
- Đảm bảo an ninh vòng ngoài: Kiểm tra token JWT, kiểm soát quyền truy cập RBAC, áp đặt giới hạn tần suất gọi (Distributed Rate Limiting qua Redis), giới hạn kích thước gói tin (Kestrel MaxRequestBodySize = 64KB).
- Hỗ trợ độc lập phát triển cho Frontend qua **Mock Engine** (declarative `Mocks/*.mock.json` + seed `Seeds/`) và **AuthMode hai trạng thái** (`Enforce`/`Standard` vs `Bypass`, Bypass chỉ ở Development).
Bounded context: cổng `8000`, **0 database nghiệp vụ**, 1 Redis cho rate limit (`localhost:6379`).
---
## 2. NHỮNG ĐIỀU CẤM KỴ ĐỐI VỚI AI AGENTS (ANTI-HALLUCINATION GUARDRAILS)
1. **TUYỆT ĐỐI KHÔNG VIẾT IF/ELSE MOCK HAY REAL TRONG CONTROLLER**:
   - Controller chỉ là lớp đầu vào (Input) mỏng, chỉ kiểm tra tính hợp lệ cơ bản của DTOs và gọi duy nhất 1 dòng sang `_dispatcher.DispatchAsync` hoặc `_dispatcher.DispatchPagedAsync`.
   - Toàn bộ việc phân giải Mock vs Real, đọc Seed JSON, gọi gRPC, forward headers thuộc về `Dispatching Core` (`ModeEvaluator`, `MockDataMapper`, `GrpcServiceCaller`, `GatewayDispatcher`).
2. **TUYỆT ĐỐI KHÔNG ĐƯA DATABASE IN-MEMORY HAY LOGIC TÍNH TOÁN VÀO GATEWAY**:
   - Gateway không sở hữu database nghiệp vụ.
   - Khi ở chế độ Mock, Gateway chỉ đọc các file fixture tĩnh từ thư mục `Seeds/{service}/{entity}.json` và kiểm tra cú pháp bằng Protobuf `MessageParser.ParseJson()`. Không được viết code in-memory DB lưu trữ state giả lập nghiệp vụ backend.
3. **TUYỆT ĐỐI KHÔNG SỬA ĐỔI PROTOBUF FIELD TAGS CỦA `shop.proto`**:
   - File `Nexus.Gateway/Contracts/shop.proto` là hợp đồng Gateway đang dùng. Nếu thêm trường mới, phải dùng tag number tiếp theo. Tuyệt đối không xóa hoặc thay đổi tag number đã có.
   - Lưu ý hiện trạng: file này chỉ khai báo `service UsersApi` (5 rpc), **không phải** hợp đồng đầy đủ của 8 service.
4. **TUYỆT ĐỐI KHÔNG THAY ĐỔI CỔNG 8000**:
   - `Properties/launchSettings.json`, `Dockerfile` (`EXPOSE 8000`, `ASPNETCORE_URLS=http://+:8000`) và `docker-compose.yml` đều dùng cổng `8000`.
5. **TUYỆT ĐỐI KHÔNG LÀM HỎNG BỘ KIỂM THỬ TỰ ĐỘNG**:
   - Mọi thay đổi mã nguồn trong Gateway bắt buộc phải vượt qua toàn bộ test trong `Nexus.Gateway.Tests/` (19 file, 49 `[Fact]` + 13 `[Theory]` = 62 attribute test).
   - **Không trích dẫn con số "N/N passed" trong tài liệu**: chạy `dotnet test ApiGateway.sln -c Release` để lấy kết quả thật của lần chạy hiện tại.
---
## 3. HIỆN TRẠNG SỐ LIỆU ĐÃ KIỂM CHỨNG
| Hạng mục | Giá trị thật | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|
| File `.cs` / dòng code | 98 file / 7468 dòng (`Nexus.Gateway` 79 file, `Nexus.Gateway.Tests` 19 file) | ✅ CÓ | `find` + `wc -l` trên repo |
| Controller | 15 file; 4 controller có action (16 action); 11 stub 10 dòng, 0 action | ⚠️ MỘT PHẦN | `Nexus.Gateway/Controllers/*.cs` |
| gRPC client đã đăng ký | 1 (`UsersApi.UsersApiClient`, tag `users`) | ⚠️ MỘT PHẦN | `Nexus.Gateway/Extensions/GrpcClientsExtension.cs` |
| RPC trong `shop.proto` | 5 (`Register`, `Login`, `VerifyOtp`, `ResendOtp`, `GetProfile`) | ⚠️ MỘT PHẦN | `Nexus.Gateway/Contracts/shop.proto` |
| Seed fixture | 9 file JSON (`Seeds/auth` 4, `Seeds/users` 5) | ⚠️ MỘT PHẦN | `Nexus.Gateway/Seeds/` |
| Declarative mock | 6 file `*.mock.json` | ✅ CÓ | `Nexus.Gateway/Mocks/` |
| Database nghiệp vụ | 0 `DbSet`, 0 `DbContext` | ✅ CÓ (đúng thiết kế) | grep `DbSet` trong `Nexus.Gateway/` |
| RabbitMQ / MassTransit | 0 package, 0 publish thật | ❌ CHƯA CÓ | `Nexus.Gateway/Nexus.Gateway.csproj`, `HealthProber/SagaRollbackPublisher.cs` |
| Docker auto-recovery trong image | `DockerContainerRecoveryService` gọi CLI `docker` nhưng image `aspnet:8.0` không có CLI | ⚠️ MỘT PHẦN | `HealthProber/DockerContainerRecoveryService.cs:26-55`, `Dockerfile` |