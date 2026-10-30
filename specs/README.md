# API GATEWAY SPEC KIT — CHỈ DẪN KỸ THUẬT CHO AI AGENT & DEVELOPER
> **Trạng thái triển khai (2026-10-27):** ✅ Hoàn tất — 15/15 Controllers đã hoàn thiện đầy đủ action, 46 seed JSON fixtures, 384/384 test cases PASS 100%. Đã chuyển đổi hoàn toàn cơ chế giao tiếp downstream từ gRPC sang RESTful HTTP/JSON qua `HttpBackendCaller` và `GatewayBaseController.ForwardAsync` (CẤM gRPC).
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-27.

Chào mừng bạn (Developer hoặc AI Coding Agent) đến với repository **ApiGateway**. Đây là tài liệu điều hướng trung tâm của bộ **Spec Kit** giúp bạn nắm bắt ngay lập tức kiến trúc, nhiệm vụ, các quy chuẩn code và các rào chắn kỹ thuật (guardrails) của Edge API Gateway.

---

## 1. MỤC LỤC BỘ ĐẶC TẢ GATEWAY (SPECS/)
| File Đặc Tả | Trọng Tâm Nội Dung | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|
| [00_AGENT_MISSION_GUARDRAILS.md](00_AGENT_MISSION_GUARDRAILS.md) | Nhiệm vụ chính, Bounded Context của Edge Gateway, 5 điều cấm kỵ chống ảo giác, bảng số liệu thật. | ✅ CÓ | file trong `specs/` |
| [01_ARCHITECTURAL_BLUEPRINT.md](01_ARCHITECTURAL_BLUEPRINT.md) | Kiến trúc 3 khối độc lập (Input Controllers $\rightarrow$ Dispatching Core $\rightarrow$ Output Factory) + pipeline 8 bước, REST downstream. | ✅ CÓ | `Nexus.Gateway/Dispatching/`, `Extensions/PipelineExtensions.cs` |
| [02_GATEWAY_CONTROLLERS_ROUTING.md](02_GATEWAY_CONTROLLERS_ROUTING.md) | **15 file controller** đầy đủ actions, route prefix `/api/v1/...`, kế thừa `GatewayBaseController`. | ✅ CÓ | `Nexus.Gateway/Controllers/*.cs` |
| [03_GATEWAY_STATE_SECURITY.md](03_GATEWAY_STATE_SECURITY.md) | JWT Bearer, phân quyền RBAC, hai trục cấu hình `Gateway:AuthMode` (`Enforce`/`Standard` vs `Bypass` — chỉ Development) và `Services:{name}:Mode` (`Real`/`Mock`). | ✅ CÓ | `Nexus.Gateway/GatewayOptions.cs:38-115` |
| [04_MOCK_ENGINE_SEED_DATA.md](04_MOCK_ENGINE_SEED_DATA.md) | Seed fixtures `Seeds/{service}/{operation}.json` (163 file thật phủ 9 nhóm service) — cơ chế mock duy nhất. | ✅ CÓ | `Nexus.Gateway/Seeds/`, `Dispatching/MockDataMapper.cs` |
| [05_DEV_VERIFICATION_CHECKLIST.md](05_DEV_VERIFICATION_CHECKLIST.md) | Danh sách kiểm tra nghiệm thu, lệnh chạy test/build, sanity check bằng grep. | ✅ CÓ | file trong `specs/` |

---

## 2. TÓM TẮT THÔNG SỐ VẬN HÀNH GATEWAY
| Hạng mục | Giá trị thật | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|
| Ngôn ngữ & framework | .NET 8 Web API (C# 12), `TargetFramework net8.0` | ✅ CÓ | `Nexus.Gateway/Nexus.Gateway.csproj` |
| Cổng lắng nghe | `8080` (`http://localhost:8080`) | ✅ CÓ | `Properties/launchSettings.json`, `Dockerfile` (`EXPOSE 8080`), `docker-compose.yml` |
| Phụ thuộc hạ tầng | Redis `localhost:6379` (distributed rate limiting) | ✅ CÓ | `appsettings.json`; `Middleware/RedisRateLimitMiddleware.cs` |
| Database nghiệp vụ | **0** (không `DbContext`, không `DbSet`) | ✅ CÓ | grep `DbSet` trong `Nexus.Gateway/` |
| Tiền tố route | `/api/v1/{service}/...` | ✅ CÓ | `Controllers/*.cs` |
| Bộ kiểm thử | 384 test cases PASS 100% | ✅ CÓ | `Nexus.Gateway.Tests/` |
| Giao tiếp downstream | RESTful HTTP/JSON qua `HttpBackendCaller` (Port 8001-8008, CẤM gRPC) | ✅ CÓ | `Dispatching/HttpBackendCaller.cs` |