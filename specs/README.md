# API GATEWAY SPEC KIT — CHỈ DẪN KỸ THUẬT CHO AI AGENT & DEVELOPER
> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — 4/15 controller có action (16 action), 1/8 gRPC client, 1 service/5 rpc trong `shop.proto`, 62 attribute test trên 19 file.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-24.
Chào mừng bạn (Developer hoặc AI Coding Agent) đến với repository **ApiGateway**. Đây là tài liệu điều hướng trung tâm của bộ **Spec Kit** giúp bạn nắm bắt ngay lập tức kiến trúc, nhiệm vụ, các quy chuẩn code và các rào chắn kỹ thuật (guardrails) của Edge API Gateway.
---
## 1. MỤC LỤC BỘ ĐẶC TẢ GATEWAY (SPECS/)
| File Đặc Tả | Trọng Tâm Nội Dung | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|
| [00_AGENT_MISSION_GUARDRAILS.md](00_AGENT_MISSION_GUARDRAILS.md) | Nhiệm vụ chính, Bounded Context của Edge Gateway, 5 điều cấm kỵ chống ảo giác, bảng số liệu thật. | ✅ CÓ | file trong `specs/` |
| [01_ARCHITECTURAL_BLUEPRINT.md](01_ARCHITECTURAL_BLUEPRINT.md) | Kiến trúc 3 khối độc lập (Input Controllers $\rightarrow$ Dispatching Core $\rightarrow$ Output Factory) + pipeline 8 bước. | ✅ CÓ | `Nexus.Gateway/Dispatching/`, `Extensions/PipelineExtensions.cs` |
| [02_GATEWAY_CONTROLLERS_ROUTING.md](02_GATEWAY_CONTROLLERS_ROUTING.md) | **15 file controller** (4 có action = 16 action; 11 stub 0 action), route prefix thật `/api/v1/...`, DTOs Requests/Responses. | ⚠️ MỘT PHẦN | `Nexus.Gateway/Controllers/*.cs` |
| [03_GATEWAY_STATE_SECURITY.md](03_GATEWAY_STATE_SECURITY.md) | JWT Bearer, phân quyền RBAC, hai trục cấu hình `Gateway:AuthMode` (`Enforce`/`Standard` vs `Bypass` — chỉ Development) và `Services:{name}:Mode` (`Real`/`Mock`). | ⚠️ MỘT PHẦN | `Nexus.Gateway/GatewayOptions.cs:38-115` |
| [04_MOCK_ENGINE_SEED_DATA.md](04_MOCK_ENGINE_SEED_DATA.md) | Seed fixtures `Seeds/{service}/{operation}.json` (9 file thật: auth 4 + users 5), kiểm định Protobuf Parser, declarative mock `Mocks/*.mock.json` (6 file). | ⚠️ MỘT PHẦN | `Nexus.Gateway/Seeds/`, `Nexus.Gateway/Mocks/`, `MockEngine/` |
| [05_DEV_VERIFICATION_CHECKLIST.md](05_DEV_VERIFICATION_CHECKLIST.md) | Danh sách kiểm tra nghiệm thu, lệnh chạy test/build, sanity check bằng grep. | ✅ CÓ | file trong `specs/` |
---
## 2. TÓM TẮT THÔNG SỐ VẬN HÀNH GATEWAY
| Hạng mục | Giá trị thật | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|
| Ngôn ngữ & framework | .NET 8 Web API (C# 12), `TargetFramework net8.0` | ✅ CÓ | `Nexus.Gateway/Nexus.Gateway.csproj` |
| Cổng lắng nghe | `8000` (`http://localhost:8000`) | ✅ CÓ | `Properties/launchSettings.json`, `Dockerfile` (`EXPOSE 8000`), `docker-compose.yml` |
| Phụ thuộc hạ tầng | Redis `localhost:6379` (distributed rate limiting) | ✅ CÓ | `appsettings.json`; `Middleware/RedisRateLimitMiddleware.cs` |
| Database nghiệp vụ | **0** (không `DbContext`, không `DbSet`) | ✅ CÓ | grep `DbSet` trong `Nexus.Gateway/` |
| Tiền tố route | `/api/v1/{service}/...` (controller thật: `api/v1/auth`, `api/v1/catalog/products`, ...) | ⚠️ MỘT PHẦN | `Controllers/*.cs` |
| Bộ kiểm thử | 19 file, 49 `[Fact]` + 13 `[Theory]` = 62 attribute test | ⚠️ MỘT PHẦN | `Nexus.Gateway.Tests/` |
| gRPC client | 1/8 (`users`) | ⚠️ MỘT PHẦN | `Extensions/GrpcClientsExtension.cs` |