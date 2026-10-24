# 05 — DANH SÁCH KIỂM CHỨNG & NGHIỆM THU (VERIFICATION CHECKLIST)
> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — chỉ 4/15 controller có action (16 action), 1/8 gRPC client, 19 file test với 62 attribute test.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-24.
---
## 1. LỆNH KIỂM THỬ TỰ ĐỘNG
Trước khi bàn giao code hoặc commit:
```bash
# 1. Chạy toàn bộ test của solution ở chế độ Release
dotnet test ApiGateway.sln -c Release
# 2. Build không lỗi
dotnet build ApiGateway.sln -c Release
```
Không ghi trước con số "N/N passed" vào tài liệu. Số liệu kiểm chứng được (đếm tĩnh, ngày 2026-10-24):
| Hạng mục | Giá trị thật | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|
| File test | 19 file `.cs` / 2012 dòng | ✅ CÓ | `Nexus.Gateway.Tests/*.cs` |
| Attribute test | 49 `[Fact]` + 13 `[Theory]` = **62** | ✅ CÓ | `grep -c '\[Fact\]'`, `grep -c '\[Theory\]'` |
| Dòng `InlineData` của `[Theory]` | 49 dòng (trên 9 file) | ✅ CÓ | `grep -rc InlineData` |
| Số ca test thực thi | phụ thuộc `InlineData` (36 `[Fact]` + 49 dòng `InlineData` ≈ 85 ca) — **phải lấy từ output `dotnet test`** | ⚠️ MỘT PHẦN | chưa chạy trong lần cập nhật tài liệu này |
---
## 2. CHECKLIST KIỂM TRA MÃ NGUỒN CHO AI AGENTS
- [ ] **Controller theo đúng chuẩn**: Nằm trong `Controllers/`, kế thừa `ControllerBase`, có `[ApiController]` + `[Route]`. Hiện trạng: 15 file; **4 file có action** dùng thêm `[BackendTag]` + `[EnableRateLimiting("api")]`; 11 file là stub 10 dòng chưa có action (`// TODO (Team)`).
- [ ] **Health Prober Daemon & Logging chuẩn**: Khởi tạo mặc định `Disabled`, Interval 60s, Timeout 30s, Retries 5, StartPeriod 60s, xoay file tối đa 1000 dòng/file trong `logs/health/` — khớp `HealthProber` trong `appsettings.json` và `HealthProber/HealthProberManager.cs:53`.
- [ ] **Tuyệt đối không có IF/ELSE Mock/Real trong Controller**: Action gọi đúng 1 dòng qua `_dispatcher.DispatchAsync` hoặc `_dispatcher.DispatchPagedAsync`.
- [ ] **DTOs được chia tách**: DTO nằm trong `Dtos/{Service}/`, tách `*Requests.cs` / `*Responses.cs`. Hiện có 4 thư mục: `Auth`, `Users`, `GatewayControl`, `HealthProber` (không phải 8–10 domain).
- [ ] **Program.cs siêu tinh gọn**: dưới 35 dòng — hiện tại `Nexus.Gateway/Program.cs` là 30 dòng ✅.
- [ ] **Tài nguyên Seeds/Mocks**: `.csproj` phải có `<Content Update="Mocks/**/*.json" ... />` và `<Content Update="Seeds/**/*.json" ... />` — hiện có ở `Nexus.Gateway/Nexus.Gateway.csproj:28-29` ✅.
- [ ] **Hợp đồng Protobuf**: chỉ `Contracts/shop.proto`, `GrpcServices="Both"` (`Nexus.Gateway.csproj:27`); hiện chỉ có `service UsersApi` (5 rpc) ⚠️.
- [ ] **Git Status**: Không commit file rác (`bin/`, `obj/`, `.idea/`, `.env`). Hiện trạng vi phạm: `Nexus.Gateway/logs/batches/*.json` (9 file) **đang được track** ⚠️; `run-dev.sh` / `run-dev.ps1` nằm ở root repo.
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