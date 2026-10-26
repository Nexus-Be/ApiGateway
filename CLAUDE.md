<!-- gitnexus:start -->
# GitNexus — Code Intelligence
This project is indexed by GitNexus as **ApiGateway** (1252 symbols, 2601 relationships, 100 execution flows). Use the GitNexus MCP tools to understand code, assess impact, and navigate safely.
> Index stale? Run `node .gitnexus/run.cjs analyze` from the project root — it auto-selects an available runner. No `.gitnexus/run.cjs` yet? `npx gitnexus analyze` (npm 11 crash → `npm i -g gitnexus`; #1939).
## Always Do
- **MUST run impact analysis before editing any symbol.** Before modifying a function, class, or method, run `impact({target: "symbolName", direction: "upstream"})` and report the blast radius (direct callers, affected processes, risk level) to the user.
- **MUST run `detect_changes()` before committing** to verify your changes only affect expected symbols and execution flows. For regression review, compare against the default branch: `detect_changes({scope: "compare", base_ref: "main"})`.
- **MUST warn the user** if impact analysis returns HIGH or CRITICAL risk before proceeding with edits.
- When exploring unfamiliar code, use `query({search_query: "concept"})` to find execution flows instead of grepping. It returns process-grouped results ranked by relevance.
- When you need full context on a specific symbol — callers, callees, which execution flows it participates in — use `context({name: "symbolName"})`.
- For security review, `explain({target: "fileOrSymbol"})` lists taint findings (source→sink flows; needs `analyze --pdg`).
## Never Do
- NEVER edit a function, class, or method without first running `impact` on it.
- NEVER ignore HIGH or CRITICAL risk warnings from impact analysis.
- NEVER rename symbols with find-and-replace — use `rename` which understands the call graph.
- NEVER commit changes without running `detect_changes()` to check affected scope.
## Resources
| Resource | Use for |
|----------|---------|
| `gitnexus://repo/ApiGateway/context` | Codebase overview, check index freshness |
| `gitnexus://repo/ApiGateway/clusters` | All functional areas |
| `gitnexus://repo/ApiGateway/processes` | All execution flows |
| `gitnexus://repo/ApiGateway/process/{name}` | Step-by-step execution trace |
## CLI
| Task | Read this skill file |
|------|---------------------|
| Understand architecture / "How does X work?" | `.claude/skills/gitnexus/gitnexus-exploring/SKILL.md` |
| Blast radius / "What breaks if I change X?" | `.claude/skills/gitnexus/gitnexus-impact-analysis/SKILL.md` |
| Trace bugs / "Why is X failing?" | `.claude/skills/gitnexus/gitnexus-debugging/SKILL.md` |
| Rename / extract / split / refactor | `.claude/skills/gitnexus/gitnexus-refactoring/SKILL.md` |
| Tools, resources, schema reference | `.claude/skills/gitnexus/gitnexus-guide/SKILL.md` |
| Index, status, clean, wiki CLI commands | `.claude/skills/gitnexus/gitnexus-cli/SKILL.md` |
<!-- gitnexus:end -->
---
# QUY CHUẨN VẬN HÀNH DÀNH CHO AI CODING AGENTS (API GATEWAY)
> **Trạng thái triển khai (2026-10-25):** ⚠️ Một phần — 9/15 controller có action (114 action), 3/8 gRPC client (`users`, `catalog`, `auction`), 4 file proto (`shop.proto` 5 rpc + `catalog.proto` 27 rpc + `user.proto` 31 rpc + `auction.proto` 16 rpc), `dotnet test` 310+ passed trên 24 file.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-25.
File này là bản sao **giống hệt** `CLAUDE.md` trong cùng thư mục — mọi sửa đổi phải áp dụng cho cả hai file.
---
## 🛑 NGUYÊN TẮC 1: BẮT BUỘC LẬP KẾ HOẠCH TRƯỚC KHI HÀNH ĐỘNG (PLAN-FIRST RULE)
Mọi AI Agent khi nhận nhiệm vụ kỹ thuật tại ApiGateway **BẮT BUỘC PHẢI LẬP VÀ TRÌNH BÀY PLAN** trước khi thực hiện bất kỳ hành động sửa đổi mã nguồn, thêm/xóa file, refactor hay thay đổi cấu hình nào.
Một Plan chuẩn bắt buộc phải có:
1. **Mục tiêu (Objective)**: Tóm tắt chính xác điều cần làm.
2. **Context & Files cần đọc**: Liệt kê các file nội bộ của ApiGateway cần đọc theo đúng ma trận đọc file tại Nguyên tắc 3.
3. **Phân tích rủi ro & Phạm vi ảnh hưởng (Impact Analysis)**:
   - Dùng GitNexus `impact` đánh giá blast radius các symbol bị ảnh hưởng.
   - Cảnh báo rõ nếu mức độ rủi ro là HIGH hoặc CRITICAL.
4. **Checklist các bước thực hiện (Step-by-Step Implementation)**: Từng bước logic, chi tiết.
5. **Kế hoạch kiểm chứng (Verification)**: `dotnet test ApiGateway.sln -c Release`.
---
## 🧭 NGUYÊN TẮC 2: QUY TẮC PHẠM VI NỘI BỘ (INWARD-ONLY / SELF-CONTAINED)
ApiGateway là một Git Repository độc lập (`ApiGateway.git`):
- **TUYỆT ĐỐI CHỈ ĐƯỢC trỏ tới các tài nguyên nội bộ của ApiGateway**.
- **TUYỆT ĐỐI KHÔNG ĐƯỢC trỏ ngược ra ngoài Root** (`../../../docs/...`) hay sang các Service khác (`../../Catalog/...`).
- Toàn bộ tài liệu, hướng dẫn và cấu hình trong repo này phải 100% tự cung tự cấp (Self-contained).
---
## 📚 NGUYÊN TẮC 3: DANH MỤC FILE NỘI BỘ BẮT BUỘC PHẢI ĐỌC TẠI APIGATEWAY
Khi bắt đầu làm việc tại ApiGateway, AI Agent **BẮT BUỘC PHẢI ĐỌC** các file nội bộ sau theo thứ tự:
1. `specs/00_AGENT_MISSION_GUARDRAILS.md`: Bounded context gateway, port `8000`, 0 DB, rào chắn chống ảo giác.
2. `specs/01_ARCHITECTURAL_BLUEPRINT.md`: Kiến trúc 3 khối (Input → Dispatching Core → Output), Circuit Breaker, Health Prober, Redis Rate Limiting.
3. `specs/02_GATEWAY_CONTROLLERS_ROUTING.md`: **15 file controller — 9 file có action (tổng 114 action), 6 file là stub 0 action**; chuẩn viết Action siêu mỏng.
4. `specs/03_GATEWAY_STATE_SECURITY.md`: Hai trục cấu hình `Gateway:AuthMode` (`Enforce`/`Standard` vs `Bypass` — chỉ Development) và `Gateway:Services:{name}:Mode` (`Real`/`Mock`).
5. `specs/04_MOCK_ENGINE_SEED_DATA.md`: Seed fixture `Seeds/{service}/{operation}.json` + declarative mock `Mocks/*.mock.json`.
6. `MOCK_GUIDE.md`: Hai cơ chế mock đang chạy song song (Controller Action + Seeds, và declarative `*.mock.json`).
7. `DEV_GUIDE.md`: Hướng dẫn thêm API mới vào Gateway.
8. `Nexus.Gateway/Contracts/shop.proto`: Hợp đồng Protobuf của Gateway — hiện chỉ có `service UsersApi` (5 rpc), **không phải** hợp đồng đầy đủ 8 service.
9. `specs/05_DEV_VERIFICATION_CHECKLIST.md`: Checklist nghiệm thu: `dotnet test ApiGateway.sln -c Release`.
---
## 🔒 NGUYÊN TẮC 4: RÀO CHẮN BẤT BIẾN TẠI APIGATEWAY
1. **CONTROLLER KHÔNG CHỨA LOGIC PHÂN NHÁNH MOCK/REAL**: Mọi điều phối đều qua `_dispatcher.DispatchAsync` / `DispatchPagedAsync`.
2. **GATEWAY KHÔNG CHỨA DATABASE HOẶC STATEFUL LOGIC**: Gateway là Edge Proxy không trạng thái.
3. **CẤU HÌNH PORT 8000 LÀ BẤT BIẾN**.
4. **HỢP ĐỒNG PROTOBUF GỐC `Contracts/shop.proto`**: Không thay đổi tag number đã có.
5. **CẤM TỰ Ý SỬA ĐỔI BIẾN MÔI TRƯỜNG, SECRET VÀ KEY BÍ MẬT (MANDATORY HUMAN-IN-THE-LOOP FOR SECRETS)**:
   - **Tuyệt đối CẤM mọi AI Coding Agent tự ý sửa đổi, thay thế, xóa hoặc ghi đè các biến môi trường (`.env`, `.env.*`), Connection Strings, API Keys, JWT Secrets, chứng chỉ hoặc bất kỳ thông tin bí mật (credentials/secrets) nào trong toàn bộ hệ thống.**
   - **Bắt buộc phải xin ý kiến và nhận được sự đồng ý rõ ràng (explicit approval) từ người dùng trước khi thực hiện bất kỳ thay đổi nào liên quan đến biến môi trường hoặc tài khoản bí mật.**
   - Khi phát hiện cấu hình môi trường, connection string hoặc secret key có vấn đề hoặc cần cập nhật, Agent **CHỈ ĐƯỢC PHÉP báo cáo hiện trạng, đề xuất phương án và chờ người dùng xác nhận**, tuyệt đối không tự ý can thiệp.
---
## 💬 NGUYÊN TẮC 5: QUY CHUẨN THÔNG BÁO LỖI TOÀN HỆ THỐNG (MANDATORY ERROR RULES)
Tất cả các thông báo lỗi (Error Messages, Exceptions, ProblemDetails, Validation Errors, Response Details) **BẮT BUỘC** phải tuân theo 3 nguyên tắc sau:
1. **Ngôn ngữ (100% English)**: Toàn bộ thông báo lỗi phải được viết bằng tiếng Anh. Tuyệt đối không dùng tiếng Việt trong chuỗi thông báo lỗi trả về cho client.
2. **Quy cách viết hoa (Sentence case)**: Viết hoa duy nhất chữ cái đầu câu, các từ tiếp theo viết thường (ngoại trừ tên riêng, từ viết tắt hoặc định danh kỹ thuật như `ID`, `OTP`, `JWT`, `URL`, `UUID`, `Email`...).
   - ✅ Đúng: `"Email is already registered in the system."`
   - ✅ Đúng: `"OTP code has expired or does not exist."`
   - ✅ Đúng: `"OTP code is invalid."`
   - ✅ Đúng: `"This OTP code has already been used."`
   - ✅ Đúng: `"Username, valid email, and password with at least 8 characters are required."`
   - ❌ Sai: `"Lỗi mã OTP không hợp lệ"` (Không dùng tiếng Việt)
   - ❌ Sai: `"invalid email address"` (Chữ cái đầu không viết hoa)
   - ❌ Sai: `"INVALID EMAIL ADDRESS"` (Cấm viết hoa toàn bộ)
   - ❌ Sai: `"Invalid Email Address Provided"` (Không dùng Title Case)
3. **Mã lỗi (Error Codes — SCREAMING_SNAKE_CASE)**: Mã lỗi luôn viết hoa toàn bộ và nối bằng dấu gạch dưới:
   - Ví dụ: `EMAIL_ALREADY_EXISTS`, `OTP_EXPIRED`, `OTP_INVALID`, `OTP_ALREADY_USED`, `USER_NOT_FOUND`, `VAL_INVALID_INPUT`, `UNAUTHORIZED`, `FORBIDDEN`.
---
## 🎯 NGUYÊN TẮC 6: ĐIỀU KIỆN HOÀN THÀNH NHIỆM VỤ (DEFINITION OF DONE)
Một task tại ApiGateway chỉ hoàn tất khi:
- [ ] Đã lập Plan chi tiết trước khi thực hiện.
- [ ] Không chứa bất kỳ liên kết trỏ ngược ra ngoài ApiGateway.
- [ ] `dotnet build ApiGateway.sln -c Release` thành công 100% (0 error).
- [ ] `dotnet test ApiGateway.sln -c Release` vượt qua 100% các bài test **của lần chạy hiện tại** — lấy số liệu từ output thật, không trích dẫn con số cố định trong tài liệu.
- [ ] Trạng thái Git sạch, không commit file rác (`bin/`, `obj/`, `.vs/`, `.idea/`).
---
## 🧪 NGUYÊN TẮC 7: HIỆN TRẠNG ĐÃ KIỂM CHỨNG (BẮT BUỘC ĐỌC TRƯỚC KHI CODE)
> Số liệu đo trực tiếp trong repo này ngày 2026-10-24. Đây là **sự thật đã kiểm chứng**, không phải mô tả kiến trúc mục tiêu.
| Hạng mục | Giá trị thật | Trạng thái | Bằng chứng |
|:---|:---|:---|:---|
| Mã nguồn | 98 file `.cs` / 7468 dòng (`Nexus.Gateway` 79 file, `Nexus.Gateway.Tests` 19 file) | ✅ CÓ | `find` + `wc -l` |
| Controller | 15 file; **9 file có action (114 action)**, 6 stub 10 dòng 0 action | ⚠️ MỘT PHẦN | `Nexus.Gateway/Controllers/*.cs` |
| gRPC client | 3/8 — `UsersApi.UsersApiClient` / `UserApi.UserApiClient` (`users`), `CatalogApi.CatalogApiClient` (`catalog`), `AuctionApi.AuctionApiClient` (`auction`) | ⚠️ MỘT PHẦN | `Nexus.Gateway/Extensions/GrpcClientsExtension.cs` |
| Protobuf | 4 file: `shop.proto` (`UsersApi`, 5 rpc) + `catalog.proto` (`CatalogApi`, 27 rpc) + `user.proto` (`UserApi`, 31 rpc) + `auction.proto` (`AuctionApi`, 16 rpc) | ⚠️ MỘT PHẦN | `Nexus.Gateway/Contracts/` |
| Seed fixture | 107 file (`Seeds/auth` 4, `Seeds/users` 60, `Seeds/catalog` 27, `Seeds/auction` 16) | ⚠️ MỘT PHẦN | `Nexus.Gateway/Seeds/` |
| Declarative mock | 5 file `*.mock.json` | ✅ CÓ | `Nexus.Gateway/Mocks/` |
| Database nghiệp vụ | 0 `DbContext`, 0 `DbSet` | ✅ CÓ (đúng thiết kế) | grep `DbSet` |
| RabbitMQ / MassTransit | 0 package, 0 publish thật (chỉ log) | ❌ CHƯA CÓ | `HealthProber/SagaRollbackPublisher.cs` |
| Docker auto-recovery | gọi CLI `docker restart` nhưng image runtime không có docker CLI | ⚠️ MỘT PHẦN | `HealthProber/DockerContainerRecoveryService.cs:26-55`, `Dockerfile` |
| Batch log sink | POST `/api/v1/log/batch` — endpoint 404, response bị bỏ qua, `catch {}` rỗng | ⚠️ MỘT PHẦN | `Logging/GatewayBatchLogSink.cs:56-63` |
| Test | 24 file; `dotnet test ApiGateway.sln -c Release` = **313 passed** (2026-10-25) | ✅ CÓ | `Nexus.Gateway.Tests/` |
| Bí mật JWT | khóa hardcode **chỉ** trong `appsettings*.json` (bị `.gitignore:81` ignore, chưa commit); bản mẫu dùng placeholder | ⚠️ MỘT PHẦN (rủi ro local) | `.gitignore:81`; `git check-ignore` |
