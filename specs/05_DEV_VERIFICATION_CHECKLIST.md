# 05 — DANH SÁCH KIỂM CHỨNG & NGHIỆM THU (VERIFICATION CHECKLIST)

---

## 1. LỆNH KIỂM THỬ TỰ ĐỘNG

Trước khi bàn giao code hoặc commit:
```bash
# 1. Chạy toàn bộ 120 bài test ở chế độ Release
dotnet test ApiGateway.sln -c Release

# 2. Kiểm tra log kết quả: Bắt buộc Passed: 120, Failed: 0
```

---

## 2. CHECKLIST KIỂM TRA MÃ NGUỒN CHO AI AGENTS

- [ ] **15 Controllers chuẩn MVC**: Nằm trong thư mục `Controllers/`, kế thừa `ControllerBase`, có thuộc tính `[ApiController]`, `[BackendTag]`, `[EnableRateLimiting]`.
- [ ] **Health Prober Daemon & Logging chuẩn**: Khởi tạo mặc định `Disabled`, Interval 60s, Timeout 30s, Retries 5, StartPeriod 60s, xoay file tối đa 1000 dòng/file trong `logs/health/`.
- [ ] **Tuyệt đối không có IF/ELSE Mock/Real trong Controller**: Action gọi đúng 1 dòng qua `_dispatcher.DispatchAsync` hoặc `_dispatcher.DispatchPagedAsync`.
- [ ] **DTOs được chia tách**: Các DTO nằm trong `Dtos/{Service}/`, tách rành mạch thành `*Requests.cs` và `*Responses.cs`.
- [ ] **Program.cs siêu tinh gọn**: Dưới 35 dòng, không chứa logic mapping endpoint thủ công dồn cục.
- [ ] **Tài nguyên Seeds**: File `.csproj` có dòng nạp `<Content Update="Seeds/**/*.json" CopyToOutputDirectory="PreserveNewest" />`.
- [ ] **Git Status**: Không commit file rác (`bin/`, `obj/`, `.idea/`, `.env`).
