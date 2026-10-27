# 00 — NHIỆM VỤ CỐT LÕI & RÀO CHẮN CHỐNG ẢO GIÁC (API GATEWAY)
> **Trạng thái triển khai (2026-10-27):** ✅ Hoàn tất — 15/15 controllers đầy đủ actions, 46 seed JSON fixtures trong `Seeds/`, 372 test cases pass 100%. Cơ chế giao tiếp downstream đã chuyển đổi hoàn toàn sang RESTful HTTP/JSON qua `HttpBackendCaller` và `GatewayBaseController.ForwardAsync`.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-27.
---
## 1. NHIỆM VỤ & DANH TÍNH CỦA REPOSITORY
Repository này chứa mã nguồn của **Nexus API Gateway (.NET 8)**:
- Đóng vai trò là **Edge Reverse Proxy** duy nhất đứng trước toàn bộ 8 Microservices backend.
- Cung cấp giao diện RESTful JSON cho các ứng dụng Client (Web, Mobile).
- Chuyển đổi và định tuyến các HTTP request sang các cuộc gọi **RESTful HTTP/JSON downstream** tới 8 backend microservices qua `HttpBackendCaller` và `IHttpClientFactory` (đồng thời vẫn giữ khả năng tương thích gRPC legacy).
- Đảm bảo an ninh vòng ngoài: Kiểm tra token JWT, kiểm soát quyền truy cập RBAC, áp đặt giới hạn tần suất gọi (Distributed Rate Limiting qua Redis), giới hạn kích thước gói tin (Kestrel MaxRequestBodySize = 64KB).
- Hỗ trợ độc lập phát triển cho Frontend qua **Mock Engine** (declarative `Mocks/*.mock.json` + seed `Seeds/`) và **AuthMode hai trạng thái** (`Enforce`/`Standard` vs `Bypass`, Bypass chỉ ở Development).
Bounded context: cổng `8000`, **0 database nghiệp vụ**, 1 Redis cho rate limit (`localhost:6379`).
---
## 2. NHỮNG ĐIỀU CẤM KỴ ĐỐI VỚI AI AGENTS (ANTI-HALLUCINATION GUARDRAILS)
1. **TUYỆT ĐỐI KHÔNG VIẾT IF/ELSE MOCK HAY REAL TRONG CONTROLLER**:
   - Controller chỉ là lớp đầu vào (Input) mỏng, chỉ kiểm tra tính hợp lệ cơ bản của DTOs và gọi duy nhất 1 dòng sang `ForwardAsync` hoặc `ForwardPagedAsync` (từ `GatewayBaseController`).
   - Toàn bộ việc phân giải Mock vs Real, đọc Seed JSON, gọi downstream HTTP, forward headers thuộc về `Dispatching Core` (`ModeEvaluator`, `MockDataMapper`, `HttpBackendCaller`, `GatewayDispatcher`).
2. **TUYỆT ĐỐI KHÔNG ĐƯA DATABASE IN-MEMORY HAY LOGIC TÍNH TOÁN VÀO GATEWAY**:
   - Gateway không sở hữu database nghiệp vụ.
   - Khi ở chế độ Mock, Gateway chỉ đọc các file fixture tĩnh từ thư mục `Seeds/{service}/{entity}.json`. Không được viết code in-memory DB lưu trữ state giả lập nghiệp vụ backend.
3. **TUYỆT ĐỐI KHÔNG THAY ĐỔI CỔNG 8000**:
   - `Properties/launchSettings.json`, `Dockerfile` (`EXPOSE 8000`, `ASPNETCORE_URLS=http://+:8000`) và `docker-compose.yml` đều dùng cổng `8000`.
4. **TUYỆT ĐỐI KHÔNG LÀM HỎNG BỘ KIỂM THỬ TỰ ĐỘNG**:
   - Mọi thay đổi mã nguồn trong Gateway bắt buộc phải vượt qua toàn bộ test trong `Nexus.Gateway.Tests/` (372 test cases).
---
## 3. HIỆN TRẠNG SỐ LIỆU ĐÃ KIỂM CHỨNG
| Hạng mục | Giá trị thật | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|
| Controller | 15/15 controllers đã hoàn thiện đầy đủ action | ✅ HOÀN TẤT | `Nexus.Gateway/Controllers/*.cs` |
| Giao tiếp downstream | RESTful HTTP/JSON qua `HttpBackendCaller` | ✅ HOÀN TẤT | `Nexus.Gateway/Dispatching/HttpBackendCaller.cs` |
| Helper Controller | `GatewayBaseController.ForwardAsync` / `ForwardPagedAsync` | ✅ HOÀN TẤT | `Nexus.Gateway/Controllers/GatewayBaseController.cs` |
| Seed fixture | 46 file JSON phủ khắp 8 backend services | ✅ HOÀN TẤT | `Nexus.Gateway/Seeds/` |
| Test suite | 372/372 test cases PASS 100% | ✅ HOÀN TẤT | `Nexus.Gateway.Tests/` |