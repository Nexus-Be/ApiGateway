# API GATEWAY SPEC KIT — CHỈ DẪN KỸ THUẬT CHO AI AGENT & DEVELOPER

Chào mừng bạn (Developer hoặc AI Coding Agent) đến với repository **ApiGateway**. Đây là tài liệu điều hướng trung tâm của bộ **Spec Kit** giúp bạn nắm bắt ngay lập tức kiến trúc, nhiệm vụ, các quy chuẩn code và các rào chắn kỹ thuật (guardrails) của Edge API Gateway.

---

## 1. MỤC LỤC BỘ ĐẶC TẢ GATEWAY (SPECS/)

| File Đặc Tả | Trọng Tâm Nội Dung |
|:---|:---|
| [00_AGENT_MISSION_GUARDRAILS.md](00_AGENT_MISSION_GUARDRAILS.md) | Nhiệm vụ chính, Bounded Context của Edge Gateway, 5 điều cấm kỵ chống ảo giác. |
| [01_ARCHITECTURAL_BLUEPRINT.md](01_ARCHITECTURAL_BLUEPRINT.md) | Kiến trúc 3 khối độc lập (3-Tier Decoupling: Input Controllers $\rightarrow$ Dispatching Core $\rightarrow$ Output Factory). |
| [02_GATEWAY_CONTROLLERS_ROUTING.md](02_GATEWAY_CONTROLLERS_ROUTING.md) | 11 Controllers tiêu chuẩn, danh mục route `/api/v1/...`, cấu trúc DTOs Requests/Responses. |
| [03_GATEWAY_STATE_SECURITY.md](03_GATEWAY_STATE_SECURITY.md) | JWT Bearer, phân quyền RBAC, cơ chế chuyển đổi 2 trạng thái Gateway (`Standard` vs `Bypass`). |
| [04_MOCK_ENGINE_SEED_DATA.md](04_MOCK_ENGINE_SEED_DATA.md) | Dữ liệu Seed fixtures `Seeds/{service}/{entity}.json`, kiểm định Protobuf Parser, Declarative Mock Engine. |
| [05_DEV_VERIFICATION_CHECKLIST.md](05_DEV_VERIFICATION_CHECKLIST.md) | Danh sách kiểm tra nghiệm thu, lệnh chạy test (120/120 tests), build và sanity checks. |

---

## 2. TÓM TẮT THÔNG SỐ VẬN HÀNH GATEWAY

- **Ngôn ngữ & Framework**: .NET 8 Web API (C# 12).
- **Cổng Lắng Nghe**: Port `8000` (Local: `http://localhost:8000`).
- **Phụ Thuộc Hạ Tầng**: Redis `localhost:6379` (Distributed Rate Limiting).
- **Tiền Tố Route Duy Nhất**: `/api/v1/{service}/...`
- **Bộ Kiểm Thử Tự Động**: 120/120 automated tests tại `Nexus.Gateway.Tests/`.
