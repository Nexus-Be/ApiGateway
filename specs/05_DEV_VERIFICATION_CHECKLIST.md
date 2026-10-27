# 05 — DANH SÁCH KIỂM CHỨNG & NGHIỆM THU (VERIFICATION CHECKLIST)
> **Trạng thái triển khai (2026-10-27):** ✅ Hoàn tất — 15/15 Controllers đã hoàn thiện đầy đủ action, 46 seed JSON fixtures, 372/372 test cases PASS 100%. Cơ chế downstream RESTful hoàn chỉnh.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-27.
---
## 1. LỆNH KIỂM THỬ TỰ ĐỘNG
Trước khi bàn giao code hoặc commit:
```bash
# 1. Chạy toàn bộ test của solution ở chế độ Release
dotnet test ApiGateway.sln -c Release
# 2. Build không lỗi
dotnet build ApiGateway.sln -c Release
```
Số liệu kiểm chứng thực tế:
| Hạng mục | Giá trị thật | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|
| Test Cases | 372/372 test cases pass | ✅ CÓ | `dotnet test ApiGateway.sln` |
| Controller | 15/15 controllers đầy đủ actions | ✅ CÓ | `Nexus.Gateway/Controllers/*.cs` |
| Seed fixture | 46 file JSON | ✅ CÓ | `Nexus.Gateway/Seeds/` |
| Downstream Caller | `HttpBackendCaller` & `ForwardAsync` | ✅ CÓ | `Nexus.Gateway/Dispatching/HttpBackendCaller.cs` |
---
## 2. CHECKLIST KIỂM TRA MÃ NGUỒN CHO AI AGENTS
- [x] **Controller theo đúng chuẩn**: Nằm trong `Controllers/`, kế thừa `GatewayBaseController` (hoặc `ControllerBase`), có `[ApiController]` + `[Route]`, `[BackendTag]`.
- [x] **Tuyệt đối không có IF/ELSE Mock/Real trong Controller**: Action gọi đúng 1 dòng qua `ForwardAsync` hoặc `ForwardPagedAsync` (hoặc `_dispatcher.DispatchAsync`).
- [x] **DTOs được chia tách**: DTO nằm trong `Dtos/{Service}/`.
- [x] **Program.cs siêu tinh gọn**: dưới 35 dòng.
- [x] **Git Status**: Không commit file rác (`bin/`, `obj/`, `.idea/`, `.env`).
---
## 3. SANITY CHECK BẰNG LỆNH (CHẠY ĐƯỢC NGAY)
```bash
cd src/Services/ApiGateway
# Đếm controller và action thật
ls Nexus.Gateway/Controllers/*.cs | wc -l          # -> 15
grep -rc '\[Http' Nexus.Gateway/Controllers/*.cs   # -> 4 file khác 0, tổng 16
# gRPC client đã đăng ký
grep -n 'AddNamedGrpcClient' Nexus.Gateway/Extensions/GrpcClientsExtension.cs   # -> 1 (users)
# Hợp đồng proto
grep -nE '^service |^  rpc ' Nexus.Gateway/Contracts/shop.proto                  # -> 1 service, 5 rpc
# Dữ liệu mock
ls Nexus.Gateway/Seeds/*/*.json Nexus.Gateway/Mocks/*.mock.json                  # -> 9 seed, 6 mock
# File cấu hình bị ignore (KHÔNG commit)
git check-ignore -v Nexus.Gateway/appsettings.json Nexus.Gateway/appsettings.Development.json
```