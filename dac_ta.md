# ĐẶC TẢ KỸ THUẬT & KIẾN TRÚC TOÀN DIỆN API GATEWAY (.NET 8)
## HỆ SINH THÁI MICROSERVICES NEXUS

> **Dành cho độc giả**: Tài liệu này đóng vai trò là bản đặc tả kỹ thuật hoàn chỉnh (Master Technical Specification). Bất kỳ ai — từ lập trình viên Frontend, Backend, DevOps, Solution Architect đến các AI Coding Agent — khi đọc tài liệu này đều sẽ hiểu rõ: **Gateway làm được những gì, cấu tạo gồm những thành phần nào, nguyên lý hoạt động chi tiết của từng tính năng (Mock, CORS, Rate Limit, Auth, gRPC Translation...), và cách vận hành trong thực tế.**

---

# MỤC LỤC TỔNG QUAN

1. [Tổng Quan Vai Trò & Năng Lực Cốt Lõi Của ApiGateway](#1-tổng-quan-vai-trò--năng-lực-cốt-lõi-của-apigateway)
2. [Kiến Trúc 3 Khối Độc Lập (3-Tier Decoupling Architecture)](#2-kiến-trúc-3-khối-độc-lập-3-tier-decoupling-architecture)
3. [Giải Thích Chi Tiết Từng Tính Năng & Cơ Chế Hoạt Động](#3-giải-thích-chi-tiết-từng-tính-năng--cơ-chế-hoạt-động)
   - [3.1. Tính Năng Mock Engine & Seed Fixtures (Phát Triển Độc Lập)](#31-tính-năng-mock-engine--seed-fixtures-phát-triển-độc-lập)
   - [3.2. Tính Năng Chuyển Đổi Trạng Thái Gateway (Standard vs Bypass Mode)](#32-tính-năng-chuyển-đổi-trạng-thái-gateway-standard-vs-bypass-mode)
   - [3.3. Tính Năng Chuyển Đổi Mock $\leftrightarrow$ Real Cho Từng Service (Hot-Reload)](#33-tính-năng-chuyển-đổi-mock--real-cho-từng-service-hot-reload)
   - [3.4. Tính Năng CORS (Cross-Origin Resource Sharing)](#34-tính-năng-cors-cross-origin-resource-sharing)
   - [3.5. Tính Năng Distributed Rate Limiting (Giới Hạn Tần Suất Gọi Qua Redis)](#35-tính-năng-distributed-rate-limiting-giới-hạn-tần-suất-gọi-qua-redis)
   - [3.6. Tính Năng Bảo Mật, JWT Authentication & Phân Quyền RBAC](#36-tính-năng-bảo-mật-jwt-authentication--phân-quyền-rbac)
   - [3.7. Tính Năng Chuyển Đổi Giao Thức REST $\rightarrow$ gRPC (Protocol Translation)](#37-tính-năng-chuyển-đổi-giao-thức-rest--grpc-protocol-translation)
   - [3.8. Tính Năng Bảo Vệ Kestrel Server & Request Timeouts](#38-tính-năng-bảo-vệ-kestrel-server--request-timeouts)
   - [3.9. Tính Năng Xử Lý Lỗi Tập Trung & Chuẩn Hóa Phản Hồi (RFC 9110 / RFC 6585)](#39-tính-năng-xử-lý-lỗi-tập-trung--chuẩn-hóa-phản-hồi-rfc-9110--rfc-6585)
   - [3.10. Tính Năng Phân Tán Trace & Giám Sát (OpenTelemetry Tracing)](#310-tính-năng-phân-tán-trace--giám-sát-opentelemetry-tracing)
   - [3.11. Chiến Lược Phòng Thủ Xác Thực Dữ Liệu 3 Tầng (3-Tier Validation Defense-in-Depth)](#311-chiến-lược-phòng-thủ-xác-thực-dữ-liệu-3-tầng-3-tier-validation-defense-in-depth)
   - [3.12. Tính Năng Health Prober Daemon, Giám Sát Sức Khỏe & Tự Động Hồi Phục (Self-Healing)](#312-tính-năng-health-prober-daemon-giám-sát-sức-khỏe--tự-động-hồi-phục-self-healing)
4. [Bản Đồ Cấu Trúc Mã Nguồn & Phân Bổ Trách Nhiệm](#4-bản-đồ-cấu-trúc-mã-nguồn--phân-bổ-trách-nhiệm)
5. [Bảng Tổng Hợp 11 Controllers & Danh Mục 17 Nhóm Route](#5-bảng-tổng-hợp-11-controllers--danh-mục-17-nhóm-route)
6. [Quy Trình 3 Bước Thêm API Mới Dành Cho Developer](#6-quy-trình-3-bước-thêm-api-mới-dành-cho-developer)
7. [Bảo Đảm Chất Lượng & Kết Quả Kiểm Thử Tự Động (120/120 Tests)](#7-bảo-đảm-chất-lượng--kết-quả-kiểm-thử-tự-động-120120-tests)

---

# 1. TỔNG QUAN VAI TRÒ & NĂNG LỰC CỐT LÕI CỦA APIGATEWAY

Trong kiến trúc Microservices của hệ sinh thái Nexus, **ApiGateway** đóng vai trò là **Edge Reverse Proxy** (Cổng vào biên duy nhất) đứng phía trước toàn bộ 8 Microservices nội bộ:

```
[ Khách Hàng: Web Browser / Next.js / Mobile App ]
                       │
                       ▼ (HTTP/REST JSON: /api/v1/... - Cổng 8000)
           ┌────────────────────────┐
           │       ApiGateway       │ ──(RateLimit Cache)──▶ [ Redis : 6379 ]
           └────────────────────────┘
                       │
                       ├── (gRPC Channel - Protobuf v3 - Mạng Nội Bộ Tốc Độ Cao)
                       ▼
   ┌───────────────┬─┴─────────────┬───────────────┬───────────────┐
   ▼               ▼               ▼               ▼               ▼
UsersService    CatalogService  CommerceService  LogService     AuctionService ... (+ Fulfillment, Notification, Wallet)
(Cổng 8001)     (Cổng 8002)     (Cổng 8003)      (Cổng 8004)    (Cổng 8005)        (Cổng 8006, 8007, 8008)
```

### 1.1. Tại sao hệ thống cần ApiGateway?
Nếu không có Gateway, các ứng dụng Client (Frontend, Mobile) sẽ phải kết nối trực tiếp đến từng cổng của 8 service khác nhau. Điều này gây ra:
- **Lỗ hổng bảo mật**: Lộ toàn bộ sơ đồ mạng và cổng nội bộ ra Internet.
- **Rối loạn xác thực**: Từng service backend phải tự cài đặt bộ kiểm tra JWT, CORS và Rate Limiting lặp đi lặp lại.
- **Bất đồng bộ giao thức**: Frontend chỉ hiểu HTTP/REST JSON, trong khi các microservices backend giao tiếp hiệu năng cao bằng gRPC Protobuf v3.
- **Khó khăn khi phát triển giao diện (FE bị block)**: Nếu Backend chưa code xong hoặc chưa dựng database, Frontend hoàn toàn bị tê liệt vì không có API để test.

### 1.2. Những gì ApiGateway làm được (Bảng Tóm Tắt Năng Lực):
1. **Cổng vào tập trung duy nhất (Single Point of Entry)**: Toàn bộ request ngoại vi đều bắt đầu bằng tiền tố thống nhất: `/api/v1/{service}/...` tại cổng `8000`.
2. **Chuyển đổi giao thức thông minh (Protocol Translation)**: Tiếp nhận RESTful JSON từ Client, tự động chuyển thành lời gọi gRPC nhị phân gửi xuống backend, nhận kết quả Protobuf và format ngược lại thành JSON chuẩn.
3. **Môi trường phát triển độc lập (Mock Engine tích hợp)**: Frontend có thể chạy và tương tác với toàn bộ 8 services với dữ liệu seed mẫu mà không cần bật bất kỳ backend service hay database nào.
4. **Chuyển đổi trạng thái Gateway linh hoạt (`Standard` vs `Bypass`)**: Hỗ trợ chuyển sang chế độ `Bypass` giúp Frontend dev test giao diện không cần đăng nhập hay truyền token.
5. **Hot-Reload Mock $\leftrightarrow$ Real theo từng service**: Cho phép bật 1 service backend thật (ví dụ `catalog: Real`), trong khi 7 service còn lại vẫn chạy `Mock` mượt mà.
6. **Bảo mật và phân quyền biên (Edge Security & RBAC)**: Xác thực JWT Bearer token và kiểm soát quyền hạn 5 vai trò (`buyer`, `seller`, `staff`, `admin`, `gateway-admin`).
7. **Bảo vệ hệ thống bằng Rate Limiting phân tán**: Tích hợp Redis để chặn các cuộc tấn công DoS, brute force và giới hạn tần suất gọi cho từng IP / User.
8. **Chính sách CORS chuẩn mực**: Ngăn ngừa lỗi Cross-Origin trên trình duyệt, cho phép cấu hình linh hoạt danh sách domain Frontend được phép truy cập.
9. **Chống treo hệ thống và tràn bộ nhớ**: Giới hạn payload tối đa 64KB (Kestrel Request Size Limit) và áp đặt thời gian timeout 65s (chống tấn công Slowloris).
10. **Chuẩn hóa phản hồi & lỗi quốc tế**: Đóng gói mọi kết quả vào envelope `ApiResponse<T>` chuẩn REST và ánh xạ lỗi sang `Problem Details RFC 9110 / RFC 6585`.
11. **Quan sát phân tán (Observability)**: Tích hợp OpenTelemetry 1.18.0, tự động gắn `x-trace-id` (W3C TraceContext) xuyên suốt toàn bộ vòng đời của request.

---

# 2. KIẾN TRÚC 3 KHỐI ĐỘC LẬP (3-TIER DECOUPLING ARCHITECTURE)

Để khắc phục hoàn toàn tình trạng code bị dồn cục, khó bảo trì, hệ thống Gateway được chia tách thành **3 khối độc lập** với ranh giới trách nhiệm cực kỳ rành mạch:

```
[ Client Request: /api/v1/... ]
                │
                ▼
┌────────────────────────────────────────────────────────────────────────┐
│ 1. KHỐI ĐẦU VÀO (INPUT TIER) — Controllers/ & Dtos/                    │
│    - Gồm 10 Controllers tiêu chuẩn kế thừa [ApiController]             │
│      ControllerBase gom nhóm theo 8 microservices.                     │
│    - Tiếp nhận DTOs (trong Dtos/{Service}/*Requests.cs).               │
│    - Model binding kiểm tra định dạng; action kiểm tra input đặc thù.    │
│    - Không chứa if/else Mock/Real; gọi DispatchAsync/DispatchPagedAsync. │
│    - Validation riêng có thể throw GatewayInputException.              │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ 2. KHỐI PHẦN GIỮA (DISPATCHING CORE) — Dispatching/                    │
│    - GatewayRequestResolution: snapshot endpoint/mode theo request      │
│      trước authentication; ModeEvaluator đọc lại snapshot cho dispatch.│
│    - MockDataMapper: "Người đọc Mock" — Đọc file Seed JSON, thẩm định  │
│      cú pháp qua Protobuf MessageParser.ParseJson() nghiêm ngặt.       │
│    - GrpcServiceCaller: "Người gọi Backend" — Tự động forward Metadata │
│      (Token, Idempotency-Key, TraceId) và gọi gRPC typed client.       │
│    - GatewayDispatcher: "Bộ điều phối tổng" — Nhận lệnh từ Controller, │
│      phối hợp 3 người trên và chuyển data gốc sang Khối Đầu Ra.        │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ 3. KHỐI ĐẦU RA (OUTPUT TIER) — Responses/ & Middleware/                │
│    - ApiResponseFactory: Không quan tâm dữ liệu từ Mock hay Real, tự   │
│      động bọc kết quả vào ApiResponse<T> hoặc ApiResponse<PagedData<T>>,│
│      tự động cấp mã TraceId và ISO timestamp.                          │
│    - GatewayExceptionHandler: bắt input, gRPC, timeout và lỗi nội bộ.    │
│      Model binding, auth và rate-limit có hook dùng cùng error schema.   │
└────────────────────────────────────────────────────────────────────────┘
                │
                ▼
[ Client Response JSON ]
```

---

# 3. GIẢI THÍCH CHI TIẾT TỪNG TÍNH NĂNG & CƠ CHẾ HOẠT ĐỘNG

Dưới đây là phần mô tả chuyên sâu cách thức hoạt động của từng tính năng bên trong Gateway:

---

## 3.1. Tính Năng Mock Engine & Seed Fixtures (Phát Triển Độc Lập)

### Vấn đề thực tế:
Trong các dự án Microservices, việc Frontend phải chờ Backend code xong API, tạo database và dựng môi trường mới có thể bắt đầu làm việc là nguyên nhân hàng đầu gây chậm trễ tiến độ dự án.

### Giải pháp của Gateway:
Gateway tích hợp sẵn một **Hệ Thống Mock Kép (Dual Mock Engine)**:

```
                          ┌────────────────────────┐
                          │   Client Gửi Request   │
                          └───────────┬────────────┘
                                      │
                         [Kiểm tra Effective Mode]
                                      │
                   ┌──────────────────┴──────────────────┐
                   ▼                                     ▼
           [ Chế độ MOCK ]                        [ Chế độ REAL ]
                   │                                     │
     Đọc file Seeds/{service}/*.json           Tự động gọi gRPC Channel
                   │                           tới Backend Microservice
                   ▼                                     │
   Ép qua Protobuf MessageParser                         ▼
   Đảm bảo đúng schema 100%                    Nhận kết quả Protobuf
                   │                                     │
                   └──────────────────┬──────────────────┘
                                      │
                                      ▼
                      Bọc vào ApiResponseFactory chuẩn
```

1. **Static Protobuf Fixtures (`Seeds/{service}/{operation}.json`)**:
   - Khi một service ở chế độ Mock, `MockDataMapper` sẽ đọc file JSON tương ứng (ví dụ: `Seeds/catalog/products.json`).
   - **Cơ chế thẩm định hợp đồng nghiêm ngặt (Strict Contract Verification)**:
     ```csharp
     var protoMessage = parser.ParseJson(json);
     ```
     Dữ liệu JSON mẫu bắt buộc phải được ép qua trình phân tích của Protobuf. Nếu dev viết sai kiểu dữ liệu (ví dụ: gõ string vào trường số nguyên), hệ thống sẽ báo lỗi khi parse fixture. Proto3 cho phép một số trường vắng mặt nhận giá trị mặc định; parse hợp lệ không bảo đảm fixture đúng nghiệp vụ hoặc backend thật. Hãy có test contract/mock và test tích hợp backend.
   - **Giả lập độ trễ mạng (`MockDelayMs`)**: Có thể cấu hình giả lập độ trễ mạng (ví dụ 500ms) để Frontend kiểm tra hiệu ứng Loading / Spinner của giao diện.
   - **Giả lập mã lỗi (`MockStatus`)**: Có thể cấu hình trả về mã lỗi 503 để Frontend kiểm tra màn hình hiển thị lỗi mạng.

2. **Dynamic Declarative Mock Engine (`*.mock.json`)**:
   - Cho phép dev tạo nhanh các API REST mock động chỉ bằng cách tạo một file JSON trong thư mục `Nexus.Gateway/Mocks/**/*.mock.json` mà **không cần viết bất kỳ dòng code C# nào**.
   - Hỗ trợ tham số URL động (ví dụ: `/api/v1/catalog/products/{id}`).
   - Tự động quét file và nạp nóng (Hot-Reload qua `FileSystemWatcher`).
   - Tự động sinh tài liệu trên Swagger UI qua `MockEngineDocumentFilter`.

---

## 3.2. Tính Năng Chuyển Đổi Trạng Thái Gateway (Standard vs Bypass Mode)

### Vấn đề thực tế:
Các API kinh doanh như xem giỏ hàng (`/commerce/cart`), thông tin cá nhân (`/users/me`), hay lịch sử đơn hàng đều yêu cầu đăng nhập và có token JWT mang vai trò tương ứng. Khi Frontend dev đang code giao diện, việc token bị hết hạn hoặc phải login đi login lại để lấy token test rất mất thời gian.

### Giải pháp của Gateway:
Gateway cung cấp **Cơ chế 2 Trạng Thái Gateway (Gateway Mode State)**:

```
                       ┌──────────────────────────────┐
                       │  Client Request Đến Gateway  │
                       └──────────────┬───────────────┘
                                      │
                         [Kiểm tra GatewayMode]
                                      │
                   ┌──────────────────┴──────────────────┐
                   ▼                                     ▼
      [ Mode: "Standard" (Mặc định) ]         [ Mode: "Bypass" (Development) ]
                   │                                     │
       Bắt buộc phải có JWT Token hợp lệ      Nếu gọi API Mock: Bỏ qua kiểm tra auth,
       trên header Authorization: Bearer.     tự động gán danh tính giả lập tương ứng.
       Không có token -> 401 Unauthorized.    ────────────────────────────────────────
       Sai quyền -> 403 Forbidden.            Ngoại lệ: /gateway/... và /mock-engine/
                                              vẫn bắt buộc Token Admin thật.
```

1. **Trạng Thái Mặc Định (`"Standard"`)**:
   - Mọi quy tắc an ninh hoạt động bình thường và nghiêm ngặt.
   - Chưa đăng nhập (không gửi header Authorization) $\rightarrow$ Chặn với mã `401 Unauthorized`.
   - Đăng nhập qua `POST /api/v1/auth/login` lấy token gửi vào request $\rightarrow$ Thành công `200 OK`.
   - Token không đủ quyền (ví dụ buyer gọi endpoint staff) $\rightarrow$ Chặn với mã `403 Forbidden`.

2. **Trạng Thái Mock Bypass (`"Bypass"` — Dành riêng cho Development)**:
   - Khi bật `Bypass`, lập trình viên có thể click test trên Swagger UI hoặc gọi từ code Frontend tới **bất kỳ API Mock nào mà không cần đăng nhập hay mang token**.
   - Middleware `MockSecurityAdapterMiddleware` sẽ tự động phát hiện quyền hạn yêu cầu của API và cấp phát một danh tính giả lập `ClaimsPrincipal` tương ứng (ví dụ: tự động gắn claim `role = seller` khi gọi `/api/v1/seller/inventory`).
   - **Rào chắn an toàn tối thượng (`[DisableMockAuthorizationBypass]`)**: Các API điều khiển hệ thống (`/api/v1/gateway/...` và `/mock-engine/...`) tuyệt đối không thể bypass, bắt buộc phải có token quản trị viên thật với role `gateway-admin`.
   - Trên môi trường `Production`, chế độ Bypass bị vô hiệu hóa hoàn toàn trong code.

---

## 3.3. Tính Năng Chuyển Đổi Mock $\leftrightarrow$ Real Cho Từng Service (Hot-Reload)

### Vấn đề thực tế:
Khi một lập trình viên Backend hoàn thành xong `CatalogService:8002`, họ muốn kiểm thử tích hợp thực tế với Gateway, nhưng các service khác (`UserService`, `CommerceService`) vẫn chưa hoàn thành hoặc chưa bật.

### Giải pháp của Gateway:
Gateway cho phép cô lập trạng thái Mock/Real độc lập cho từng service trong số 8 services:

| Cách Chuyển Đổi | Thao Tác Thực Hiện | Tác Động |
|:---|:---|:---|
| **Qua File Runtime (Hot-Reload)** | Sửa file [gateway.runtime.json](src/Services/ApiGateway/Nexus.Gateway/gateway.runtime.json):<br>`"catalog": { "Mode": "Real", "Address": "http://localhost:8002" }` | Có hiệu lực tức thì cho toàn bộ request tiếp theo, **KHÔNG CẦN restart Gateway**. |
| **Qua API Quản Trị** | Gửi request `PUT /api/v1/gateway/modes/catalog`<br>Body: `{"mode": "Real"}` | Gateway tự động cập nhật bộ nhớ và ghi đè vào file runtime. |
| **Qua Request Header (Dev)** | Gửi header HTTP trên request riêng lẻ:<br>`X-Service-Mode-catalog: Real` | Chỉ ép buộc chuyển sang Real cho duy nhất request đó, không ảnh hưởng tới người khác. |

---

## 3.4. Tính Năng CORS (Cross-Origin Resource Sharing)

### Vấn đề thực tế:
Khi ứng dụng Frontend chạy tại `http://localhost:3000` (React/Next.js) gọi API sang Gateway chạy tại `http://localhost:8000`, trình duyệt web sẽ tự động chặn request theo chính sách bảo mật Same-Origin Policy nếu Gateway không cấu hình CORS chuẩn.

### Cách Gateway xử lý CORS:
Triển khai trong [EdgeMiddlewareConfiguration.cs](src/Services/ApiGateway/Nexus.Gateway/Security/EdgeMiddlewareConfiguration.cs):
- **Cấu hình nguồn cho phép (`CorsOrigins`)**: Đọc từ cấu hình `Gateway:CorsOrigins` (hỗ trợ nhiều origin như `http://localhost:3000`, `http://localhost:5173`).
- **Phương thức cho phép (`CorsMethods`)**: Cho phép đầy đủ các động từ RESTful: `GET`, `POST`, `PUT`, `DELETE`, `PATCH`, `OPTIONS`.
- **Headers cho phép (`CorsHeaders`)**: Hỗ trợ toàn bộ header chuẩn và các custom header như `Authorization`, `Content-Type`, `Idempotency-Key`, `X-Trace-Id`, `X-Gateway-Mode`, `X-Service-Mode-*`.
- **Exposed Headers**: Công khai các header phản hồi quan trọng để JavaScript trên trình duyệt có thể đọc được: `X-Trace-Id`, `X-Gateway-Mode`, `Retry-After`.
- **Hỗ trợ Preflight (OPTIONS Request)**: Tự động trả về HTTP `204 No Content` cho các preflight request của trình duyệt trước khi thực thi lệnh ghi dữ liệu.

---

## 3.5. Tính Năng Distributed Rate Limiting (Giới Hạn Tần Suất Gọi Qua Redis)

### Vấn đề thực tế:
Nếu một Client gửi liên tục hàng nghìn request mỗi giây (do lỗi lặp vô tận từ Frontend, bị kẻ xấu tấn công DoS, hoặc quét dữ liệu), hệ thống backend sẽ nhanh chóng bị nghẽn mạng và sập cơ sở dữ liệu.

### Cách Gateway xử lý Rate Limiting:
Triển khai qua [RedisRateLimitMiddleware.cs](src/Services/ApiGateway/Nexus.Gateway/Middleware/RedisRateLimitMiddleware.cs):

```
Client Request
      │
      ▼
[ RedisRateLimitMiddleware ] ──(Kiểm tra Counter)──▶ [ Redis Cache ]
      │
      ├── Đạt giới hạn cho phép?
      │     ├── [CÒN LƯỢT] ──▶ Chuyển request tới Controller xử lý.
      │     └── [VƯỢT NGƯỠNG] ─▶ Chặn ngay lập tức tại Gateway:
      │                           - Trả về mã HTTP 429 Too Many Requests
      │                           - Đính kèm Header: Retry-After: <số giây>
      │                           - Trả JSON chuẩn ApiErrorResponse RFC 6585
```

- **Phân vùng giới hạn (Partitioning)**:
  - Nếu đã đăng nhập: Giới hạn theo mã định danh người dùng (`sub` claim trong JWT).
  - Nếu là khách vãng lai: Giới hạn theo địa chỉ IP thực của Client (`RemoteIpAddress`).
- **Thuật toán Sliding/Fixed Window**: Đếm số lượt gọi trong một cửa sổ thời gian (mặc định: tối đa `100` requests trong `60` giây).
- **Chính sách Fail-Open ở Development**: Nếu trong môi trường lập trình local mà Redis chưa được bật hoặc gặp sự cố kết nối, Gateway sẽ tự động **cho request đi qua và ghi log cảnh báo**, tuyệt đối không chặn đứng công việc của lập trình viên.

---

## 3.6. Tính Năng Bảo Mật, JWT Authentication & Phân Quyền RBAC

### Quy trình xác thực JWT Bearer:
1. Client gửi request mang header `Authorization: Bearer <token>`.
2. Gateway kiểm tra chữ ký token bằng thuật toán HMAC-SHA256 với Secret Key bí mật (chuỗi an toàn tối thiểu 32 ký tự).
3. Gateway kiểm định tính hợp lệ của các trường: Nhà phát hành (`iss`), đối tượng phục vụ (`aud`), và thời hạn sống (`exp`).
4. Nếu hợp lệ, Gateway nạp thông tin danh tính thành `ClaimsPrincipal` và chuyển tiếp header `Authorization` nguyên vẹn vào gRPC metadata khi gọi xuống các microservices backend.

### Ma trận phân quyền 5 vai trò (RBAC Matrix):
Hệ thống áp dụng mô hình phân quyền có tính kế thừa:

```
[ anonymous ] (Chưa đăng nhập)
      │
      ▼
   [ buyer ] (Khách hàng đã đăng ký)
      │
      ├───────────────────────┐
      ▼                       ▼
  [ seller ]              [ staff ]
(Người bán hàng)      (Nhân viên vận hành)
      │                       │
      └───────────┬───────────┘
                  ▼
              [ admin ] (Quản trị viên toàn sàn)
                  │
                  ▼
          [ gateway-admin ] (Quản trị viên kỹ thuật Gateway)
```

- **`buyer`**: Xem/sửa giỏ hàng của chính mình, đặt đơn hàng, nạp/rút ví của mình, đặt giá đấu giá.
- **`seller`**: Đăng bán sản phẩm, điều chỉnh kho ảo của shop, tạo phiên đấu giá, xem đơn hàng của shop mình.
- **`staff`**: Xem danh sách toàn bộ người dùng sàn, duyệt/từ chối đơn đăng ký làm seller, xem toàn bộ đơn hàng sàn, tra cứu nhật ký kiểm toán.
- **`admin`**: Tạo tài khoản admin/staff, gán role cho user, cấu hình danh mục sản phẩm, cấu hình bật/tắt event kiểm toán.
- **`gateway-admin`**: Đổi trạng thái Mock/Real các service, bật/tắt chế độ Bypass, nạp lại mock definitions nóng.

---

## 3.7. Tính Năng Chuyển Đổi Giao Thức REST $\rightarrow$ gRPC (Protocol Translation)

### Tại sao lại dùng mô hình lai này?
- **Phía ngoài (Client $\rightarrow$ Gateway)**: Trình duyệt web và ứng dụng di động hỗ trợ tốt nhất với giao thức **RESTful JSON** (dễ debug, dễ tích hợp trên mọi nền tảng).
- **Phía trong (Gateway $\rightarrow$ Microservices)**: Giao tiếp giữa các server nội bộ cần tốc độ cao, độ trễ thấp và băng thông nhỏ, do đó **gRPC trên HTTP/2 nhị phân** là sự lựa chọn vượt trội.

### Cơ chế hoạt động:
Khi Client gọi `POST /api/v1/commerce/orders`:
1. `CommerceController` nhận `CheckoutRequestDto` từ JSON request body.
2. `GatewayDispatcher` kích hoạt `GrpcServiceCaller`.
3. `GrpcServiceCaller` tạo đối tượng Protobuf `CreateOrderRequest`, đóng gói nhị phân và gửi qua HTTP/2 Multiplexing tới `CommerceService:8003`.
4. `CommerceService` xử lý và trả về Protobuf `Order`.
5. `GatewayDispatcher` sử dụng `JsonFormatter.Default` chuyển đối tượng Protobuf thành `JsonElement`.
6. `ApiResponseFactory` bọc `JsonElement` đó vào `ApiResponse<Order>` và gửi trả về Client với mã HTTP `201 Created`.

---

## 3.8. Tính Năng Bảo Vệ Kestrel Server & Request Timeouts

1. **Giới Hạn Kích Thước Body Request (64KB Limit)**:
   - Được cấu hình tại [Program.cs](src/Services/ApiGateway/Nexus.Gateway/Program.cs):
     ```csharp
     builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 64 * 1024);
     ```
   - Mục đích: Gateway chỉ phục vụ các API nghiệp vụ dữ liệu JSON thông thường. Việc chặn request vượt quá 64KB giúp ngăn chặn các đợt tấn công cố tình gửi file rác khổng lồ làm cạn kiệt RAM của Gateway.

2. **Áp Đặt Thời Gian Chờ Xử Lý (Request Timeout 65s)**:
   - Cấu hình qua middleware `RequestTimeouts`:
     ```csharp
     services.AddRequestTimeouts(o => o.DefaultPolicy = new RequestTimeoutPolicy
     {
         Timeout = TimeSpan.FromSeconds(65),
         TimeoutStatusCode = StatusCodes.Status504GatewayTimeout
     });
     ```
   - Nếu một backend service bị treo hoặc database bị deadlock quá thời gian cho phép, Gateway sẽ chủ động cắt kết nối và trả về mã HTTP `504 Gateway Timeout`, không để kết nối bị treo vĩnh viễn gây cạn kiệt thread pool (chống tấn công Slowloris).

---

## 3.9. Tính Năng Xử Lý Lỗi Tập Trung & Chuẩn Hóa Phản Hồi (RFC 9110 / RFC 6585)

### Envelope Phản Hồi Thành Công Đơn Lẻ (`ApiResponse<T>`):
```json
{
  "success": true,
  "statusCode": 200,
  "message": "User retrieved successfully.",
  "data": {
    "id": "usr_9981",
    "fullName": "Nguyen Van A",
    "email": "vana@example.com",
    "status": "Active"
  },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "timestamp": "2026-10-07T08:25:00Z"
}
```

### Envelope Phản Hồi Phân Trang (`ApiResponse<PagedData<T>>`):
```json
{
  "success": true,
  "statusCode": 200,
  "data": {
    "items": [
      { "id": "101", "name": "Bàn phím cơ", "price": "1250000.00" }
    ],
    "pagination": {
      "pageIndex": 1,
      "pageSize": 20,
      "totalCount": 245,
      "totalPages": 13,
      "hasPreviousPage": false,
      "hasNextPage": true
    }
  },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "timestamp": "2026-10-07T08:25:00Z"
}
```

### Envelope Phản Hồi Thất Bại (`ApiErrorResponse`):
Được tạo qua `IApiResponseFactory` bởi exception handler hoặc hook tương ứng (MVC model binding, auth, rate-limit). Cấu trúc theo tinh thần Problem Details với `errorCode` và `errors` mở rộng; **không** lấy ví dụ `ApiResponse<T>.Error()` của dự án khác làm contract cho Gateway:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errorCode": "VAL_INVALID_INPUT",
  "detail": "Please refer to the errors property for additional details.",
  "errors": {
    "Email": ["The Email field is not a valid e-mail address."]
  },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "timestamp": "2026-10-07T08:25:00Z"
}
```

#### Bảng Ánh Xạ Mã Lỗi gRPC Sang HTTP Status Code:
| Mã Lỗi gRPC (`StatusCode`) | Mã Lỗi HTTP | Ý Nghĩa Nghiệp Vụ |
|:---|:---:|:---|
| `InvalidArgument` | **400 Bad Request** | Tham số đầu vào không hợp lệ hoặc thiếu trường bắt buộc. |
| `Unauthenticated` | **401 Unauthorized** | Chưa đăng nhập hoặc token không hợp lệ. |
| `PermissionDenied` | **403 Forbidden** | Không có quyền truy cập tài nguyên. |
| `NotFound` | **404 Not Found** | Không tìm thấy bản ghi dữ liệu yêu cầu. |
| `AlreadyExists` | **409 Conflict** | Bản ghi đã tồn tại (trùng email, username, slug...). |
| `FailedPrecondition` | **422 Unprocessable Content** | Điều kiện nghiệp vụ phía backend chưa thỏa (ví dụ xóa category đang có hàng). |
| `DeadlineExceeded` | **504 Gateway Timeout** | Backend xử lý quá hạn cho phép. |
| `Unavailable` | **503 Service Unavailable** | Service backend chưa bật hoặc bị sự cố mạng. |

---

## 3.10. Tính Năng Phân Tán Trace & Giám Sát (OpenTelemetry Tracing)

- Gateway tích hợp **OpenTelemetry 1.18.0** theo chuẩn quốc tế W3C TraceContext.
- Mỗi request khi đi vào Gateway được gán một định danh theo dõi duy nhất: `traceId`.
- Gateway tự động sinh `Activity` mang tên `${service}.${operation}` gắn kèm các tags chẩn đoán:
  - `backend.service`: Tên service tiếp nhận (ví dụ: `catalog`, `commerce`).
  - `backend.mode`: Trạng thái xử lý (`Mock` hoặc `Real`).
- Header `x-trace-id` được tự động chuyển tiếp qua gRPC Metadata xuống toàn bộ các backend microservices, giúp lập trình viên có thể truy vết toàn bộ hành trình của một request từ Frontend $\rightarrow$ Gateway $\rightarrow$ Service $\rightarrow$ RabbitMQ $\rightarrow$ Database trên Jaeger hoặc Zipkin.

---

## 3.11. Chiến Lược Phòng Thủ Xác Thực Dữ Liệu 3 Tầng (3-Tier Validation Defense-in-Depth)

Hệ thống Nexus phân định rạch ròi 3 tầng phòng vệ dữ liệu (Separation of Concerns), ngăn ngừa Gateway bị quá tải bởi các quy tắc nghiệp vụ domain riêng lẻ:

```
[ Client Request ]
       │
       ▼
┌────────────────────────────────────────────────────────────────────────┐
│ TẦNG 1: GATEWAY — KIỂM TRA MỨC HTTP & HẠ TẦNG                         │
│ - Request có đúng định dạng JSON cơ bản không (Malformed JSON).        │
│ - Kích thước gói tin có vượt quá giới hạn 64KB không.                  │
│ - Có đúng kiểu Content-Type: application/json không.                   │
│ - Rate limiting, giới hạn tần suất gọi request qua Redis.             │
│ - Kiểm tra chữ ký, thời hạn JWT và quyền RBAC cơ bản.                 │
│ - Kiểm tra header hạ tầng bắt buộc: Idempotency-Key (UUID),            │
│   biên phân trang query string (page >= 1, pageSize 1..100).           │
│ * Lưu ý: Trong Mock Mode khi backend chưa chạy, Gateway đóng vai trò   │
│   "Mock Gatekeeper" để chặn payload rác, không can thiệp sâu domain.   │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ (gRPC Channel)
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ TẦNG 2: SERVICE API & APPLICATION — VALIDATE DỮ LIỆU ĐẦU VÀO (DTOs)    │
│ - Sử dụng FluentValidation hoặc Data Annotations tại tầng API/App:    │
│   + quantity > 0.                                                      │
│   + price >= 0 (hoặc decimal hợp lệ).                                 │
│   + Email đúng định dạng regex.                                       │
│   + Các trường bắt buộc không được null/empty.                        │
│   + Độ dài chuỗi tối đa/tối thiểu, regex slug/mã sản phẩm.             │
│ - Backend trả gRPC InvalidArgument: Gateway map sang HTTP 400.         │
│   Chưa có cơ chế tự động chuyển metadata backend thành `errors` map.   │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ (CQRS / MediatR Handler)
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ TẦNG 3: DOMAIN VÀ DATABASE — BẢO VỆ TÍNH ĐÚNG ĐẮN BẤT BIẾN             │
│ - Domain Entities bảo vệ bất biến (Business Invariants):               │
│   + Số lượng đơn hàng phải lớn hơn 0 (order.AddItem).                  │
│   + Không cho phép trạng thái đơn hàng chuyển đổi trái quy tắc.        │
│   + Giá trị tồn kho không được âm khi trừ (inventory.Deduct).         │
│ - Database Constraints (PostgreSQL):                                   │
│   + CHECK constraints (CHECK (available >= 0), CHECK (price >= 0)).    │
│   + NOT NULL, UNIQUE (email, username, slug, idempotency_key).        │
│   + Khóa ngoại (FOREIGN KEY) bảo toàn quan hệ bảng.                    │
│ * Đảm bảo tối cao: Dù request đi qua Gateway, gọi gRPC nội bộ giữa     │
│   các service, hay từ RabbitMQ Consumer, dữ liệu luôn toàn vẹn 100%.   │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 3.12. Tính Năng Health Prober Daemon, Giám Sát Sức Khỏe & Tự Động Hồi Phục (Self-Healing)

Nhằm đảm bảo tính sẵn sàng cao (High Availability) cho toàn bộ hệ sinh thái Microservices, API Gateway tích hợp sẵn **Health Prober Daemon Engine** chạy nền, đóng vai trò là một External Prober chủ động giám sát trạng thái của 8 backend services (`users`, `catalog`, `commerce`, `log`, `auction`, `fulfillment`, `notification`, `wallet`).

### 3.12.1. Bốn Tham Số Điều Khiển Chuẩn (Configuration Standards)
Hệ thống điều khiển quy trình kiểm tra sức khỏe thông qua 4 tham số chuẩn (cấu hình qua `appsettings.json` hoặc biến môi trường `Gateway__HealthProber__...`):
- **Interval (Chu kỳ kiểm tra)**: Mặc định `60s` (1 phút). Khoảng cách thời gian giữa hai lần gõ cửa kiểm tra service.
- **Timeout (Thời gian chờ tối đa)**: Mặc định `30s`. Thời gian tối đa chờ phản hồi HTTP/gRPC từ service; nếu quá 30s coi như lần probe thất bại.
- **Retries / FailureThreshold (Ngưỡng thất bại)**: Mặc định `5 lần`. Số lần kiểm tra thất bại liên tiếp trước khi chuyển trạng thái sang `Unhealthy`.
- **StartPeriod (Thời gian ân hạn khởi động)**: Mặc định `60s` (1 phút). Khi container mới khởi động lại, trong 60s đầu tiên nếu probe thất bại sẽ không tính vào số lần Retries (để container có thời gian khởi tạo runtime và kết nối DB).

### 3.12.2. State Machine Quản Lý Vòng Đời Trạng Thái (Health State Machine)
Mỗi service được quản lý bởi một State Machine chặt chẽ:
```
    [ DISABLED ] ──(Gọi API kích hoạt)──► [ STARTING (StartPeriod 60s) ]
                                                    │
                 ┌──────────────────────────────────┴──────────────────┐
                 │ Nhận HTTP 200 OK                                    │ Hết 60s vẫn lỗi
                 ▼                                                     ▼
           [ HEALTHY ] ◄─────────────(Nhận 200 OK)───────────── [ DEGRADED (1-4 lỗi) ]
                 │                                                     │
                 │ Probe Timeout / 5xx                                 │ Lỗi tiếp chạm 5/5
                 └────────────► [ DEGRADED (1/5) ]                    ▼
                                                                [ UNHEALTHY ]
                                                                      │
                              ┌───────────────────────────────────────┴───────────────────────┐
                              │ 1. Circuit Breaker Fast-Fail: Chặn ngay request tới service   │
                              │ 2. Kích hoạt Saga Rollback: Bắn Event lên RabbitMQ            │
                              │ 3. Docker Auto-Recovery: Xóa & Recreate Container             │
                              │ 4. Ghi log sự cố vào File theo cơ chế giới hạn dòng          │
                              └───────────────────────────────────────┬───────────────────────┘
                                                                      ▼
                                                           [ RECOVERING (Recreated) ]
                                                                      │
                                                                      ▼
                                                           [ STARTING (Grace Period 60s) ]
```

### 3.12.3. Chiến Lược Phân Tầng Lưu Vết Log Sự Cố (Incident File Logging & Batching)
- **Với các lần check Healthy bình thường:** Tuyệt đối **không ghi ra file log** để tránh làm nghẽn Disk I/O và gây rác log (Log Pollution). Gateway lưu trữ 30 mẫu kiểm tra gần nhất trong **In-Memory Ring Buffer** phục vụ API truy vấn tức thì và đẩy metrics OpenTelemetry.
- **Khi xảy ra sự cố (Degraded 1/5 - 4/5 và Unhealthy 5/5):**
  - Gateway lưu log sự cố vào thư mục riêng: `logs/health/incident-YYYYMMDD-XXXX.log`.
  - **Cơ chế giới hạn dòng trên mỗi file (`MaxLinesPerLogFile = 1000`)**: Khi file log đạt đủ 1000 dòng, hệ thống tự động ngắt và mở file mới (`incident-YYYYMMDD-0002.log`).
- **Định hướng tương lai cho Code Agent / Developer khi triển khai `LogService` tập trung (Port 8004, `log_db`):**
  - Khi triển khai gom log tập trung về `LogService`, Gateway áp dụng cơ chế **Gom cụm tối thiểu 1000 dòng log (Batching Sink)** mới gửi bulk sang LogService qua HTTP `POST /api/v1/log/batch` hoặc RabbitMQ. Tuyệt đối không gửi từng dòng log đơn lẻ qua mạng.

### 3.12.4. Phản Ứng Tự Động Khi Service Rơi Vào Trạng Thái Unhealthy
1. **Circuit Breaker Fast-Fail Tại `GatewayDispatcher`:**
   - Khi service ở trạng thái `Unhealthy` hoặc `Recovering`, Gateway lập tức ngắt mạch trong 0ms, không đẩy request xuống mạng gRPC.
   - Trả về mã lỗi chuẩn RFC 9110: **HTTP 503 Service Unavailable** (`errorCode: "DOWNSTREAM_UNHEALTHY"`).
2. **Kích Hoạt Saga Rollback Toàn Bộ:**
   - Gateway publish sự kiện `ServiceUnhealthyIntegrationEvent` lên RabbitMQ để các orchestrator (như Checkout Saga của CommerceService) lập tức hủy và bù trừ (compensate) các giao dịch đang treo liên quan đến service bị sập.
3. **Docker Container Auto-Recovery (Tự Động Recreate Container):**
   - Gateway giao tiếp với Docker Engine qua socket `/var/run/docker.sock`, tự động gọi `Stop` $\rightarrow$ `Remove` $\rightarrow$ `Recreate` container tương ứng (`nexus-{service}-service`).
   - Sau khi container được tạo lại, chuyển trạng thái sang `Recovering` $\rightarrow$ `Starting` (ân hạn 60s).
   - **Chống lặp vô hạn (CrashLoopBackOff Protection):** Giới hạn tối đa 3 lần tự động restart liên tiếp (`MaxAutoRecoverAttempts = 3`). Nếu vượt quá, Prober ngừng restart và bắn cảnh báo khẩn cấp `CRITICAL_INTERVENTION_NEEDED`.

### 3.12.5. Danh Mục Endpoint Quản Trị Health Prober
Được phân quyền nghiêm ngặt theo chính sách `[Authorize(GatewayPolicies.GatewayAdmin)]` và chặn hoàn toàn bypass:
- `GET /api/v1/gateway/prober/status`: Tra cứu trạng thái toàn bộ 8 services, số lần lỗi liên tiếp, và danh sách các mẫu check gần nhất.
- `GET /api/v1/gateway/prober/status/{service}`: Tra cứu chi tiết sức khỏe của 1 service cụ thể.
- `PUT /api/v1/gateway/prober/{service}`: Bật hoặc tắt prober daemon của 1 service (hỗ trợ tùy chỉnh interval, threshold).
- `PUT /api/v1/gateway/prober/bulk`: Bật hoặc tắt hàng loạt toàn bộ 8 prober daemons cùng lúc.
- `POST /api/v1/gateway/prober/{service}/check`: **API kiểm tra sức khỏe thủ công tức thì (Manual Check on demand)** — thực thi probe ngay lập tức và trả về latency (ms), HTTP status code và kết quả.

---

# 4. BẢN ĐỒ CẤU TRÚC MÃ NGUỒN & PHÂN BỔ TRÁCH NHIỆM

```
src/Services/ApiGateway/
├── ApiGateway.sln                        # Visual Studio / Rider Solution file
├── ADR.md                                # Ghi chép quyết định kiến trúc (Architecture Decision Records)
├── CONFIG_REFERENCE.md                   # Cẩm nang tra cứu cấu hình & biến môi trường
├── PLAN.md                               # Kế hoạch phát triển và tiến trình kiểm thử
├── README.md                             # Hướng dẫn nhanh cài đặt và khởi chạy
├── SETUP_GUIDE_SERVICES.md               # Hướng dẫn kết nối 8 microservices backend
├── dac_ta.md                             # Bản đặc tả kỹ thuật toàn diện này
├── input.md                              # Quy chuẩn dữ liệu đầu vào & format response
├── global.json                           # Cố định phiên bản .NET SDK (8.0.x)
├── docker-compose.yml                    # Khởi động nhanh Gateway kèm Redis
├── run-dev.sh / run-dev.ps1              # 1-Click script khởi chạy môi trường Development
│
├── Nexus.Gateway/                        # PROJECT CHÍNH (ASP.NET Core 8 Web API / Gateway)
│   ├── Program.cs                        # Khởi động ứng dụng siêu tinh gọn (< 35 dòng)
│   ├── BackendDispatcher.cs              # Cầu nối tương thích ủy quyền sang IGatewayDispatcher
│   ├── GatewayOptions.cs                 # Lớp cấu hình strongly-typed (CORS, RateLimit, Services...)
│   ├── Dockerfile                        # File đóng gói Docker Image cho production
│   ├── appsettings.json                  # Cấu hình gốc (Base/Production)
│   ├── appsettings.Development.json      # Cấu hình môi trường Development (mặc định Mock bật)
│   ├── gateway.runtime.json              # Cấu hình runtime động (hot-reload mode Real/Mock)
│   │
│   ├── Contracts/
│   │   └── shop.proto                    # Nguồn sự thật duy nhất (Single Source of Truth) định nghĩa gRPC proto
│   │
│   ├── Common/                           # TẦNG DÙNG CHUNG (VALIDATION & HELPERS)
│   │   └── ValidationHelpers.cs          # Validation helpers: Bad(), PageOk(), GuidOk(), Roles, Consts
│   │
│   ├── Controllers/                      # ĐẦU VÀO: 15 CONTROLLERS TIÊU CHUẨN [ApiController] ControllerBase
│   │   ├── AuthController.cs             # Routes: /api/v1/auth/... (login, register, otp)
│   │   ├── UsersController.cs            # Routes: /api/v1/users/... (me, seller-upgrade)
│   │   ├── AdminUsersController.cs       # Routes: /api/v1/admin/users, roles, permissions, approve seller
│   │   ├── CartController.cs             # Routes: /api/v1/cart, /api/v1/commerce/cart (giỏ hàng)
│   │   ├── OrdersController.cs           # Routes: /api/v1/orders, /api/v1/commerce/orders, seller/admin orders
│   │   ├── ProductsController.cs         # Routes: /api/v1/products, /api/v1/catalog/products, seller products
│   │   ├── CategoriesController.cs       # Routes: /api/v1/categories, /api/v1/catalog/categories
│   │   ├── InventoryController.cs        # Routes: /api/v1/seller/inventory (quản lý tồn kho seller)
│   │   ├── AuctionController.cs          # Routes: /api/v1/auctions/... (auctions, bids)
│   │   ├── FulfillmentController.cs      # Routes: /api/v1/fulfillment/... (shipping, inventory, tracking)
│   │   ├── NotificationController.cs     # Routes: /api/v1/notifications/... (list, read, preferences)
│   │   ├── LogController.cs              # Routes: /api/v1/log/... (logs, event types)
│   │   ├── WalletController.cs           # Routes: /api/v1/wallet/... (balance, deposit, withdraw, transfer)
│   │   ├── GatewayControlController.cs   # Routes: /api/v1/gateway/..., /mock-engine/... (control plane)
│   │   └── HealthProberController.cs     # Routes: /api/v1/gateway/prober/... (status, toggle, bulk, manual check)
│   │
│   ├── Dtos/                             # CÁC DTO ĐƯỢC TÁCH BIỆT RÀNH MẠCH THEO SERVICE
│   │   ├── Auth/                         # AuthRequests.cs, AuthResponses.cs
│   │   ├── Users/                        # UserRequests.cs, UserResponses.cs
│   │   ├── Catalog/                      # CatalogRequests.cs, CatalogResponses.cs
│   │   ├── Commerce/                     # CommerceRequests.cs, CommerceResponses.cs
│   │   ├── Auction/                      # AuctionRequests.cs, AuctionResponses.cs
│   │   ├── Fulfillment/                  # FulfillmentRequests.cs, FulfillmentResponses.cs
│   │   ├── Notification/                 # NotificationRequests.cs, NotificationResponses.cs
│   │   ├── Log/                          # LogRequests.cs, LogResponses.cs
│   │   ├── Wallet/                       # WalletRequests.cs, WalletResponses.cs
│   │   ├── GatewayControl/               # GatewayControlRequests.cs, GatewayControlResponses.cs
│   │   └── HealthProber/                 # HealthProberDtos.cs (ToggleDto, BulkDto, StatusDto)
│   │
│   ├── HealthProber/                     # ENGINE GIÁM SÁT SỨC KHỎE & HỒI PHỤC (PROBER DAEMON)
│   │   ├── HealthProberOptions.cs        # Cấu hình Interval 60s, Timeout 30s, Retries 5, MaxLines 1000
│   │   ├── ProberRunStatus.cs            # State Machine: Disabled, Starting, Healthy, Degraded, Unhealthy, Recovering
│   │   ├── ServiceHealthState.cs         # Ring Buffer 30 mẫu gần nhất, biến đếm lỗi liên tiếp
│   │   ├── HealthCheckSample.cs          # DTO lưu vết từng mẫu kiểm tra (latency, status code, error)
│   │   ├── IHealthProberManager.cs       # Interface quản trị vòng đời các Prober Daemon
│   │   ├── HealthProberManager.cs        # Background Service điều khiển chu kỳ thăm dò & kích hoạt sự cố
│   │   ├── IHealthIncidentLogger.cs      # Interface ghi vết sự cố sức khỏe
│   │   ├── FileRollingHealthIncidentLogger.cs # Ghi log Unhealthy ra file logs/health/, xoay file mỗi 1000 dòng
│   │   ├── IDockerContainerRecoveryService.cs # Interface khôi phục container Docker
│   │   ├── DockerContainerRecoveryService.cs  # Tự động restart/recreate container qua Docker socket
│   │   ├── ISagaRollbackPublisher.cs     # Interface phát sự kiện Saga Rollback
│   │   ├── SagaRollbackPublisher.cs      # Publish ServiceUnhealthyIntegrationEvent lên RabbitMQ
│   │   └── HealthProberExtensions.cs     # Đăng ký DI cho toàn bộ khối Health Prober
│   │
│   ├── Dispatching/                      # PHẦN GIỮA: THE DISPATCHING CORE (ĐIỀU PHỐI KHÔNG IF/ELSE Ở CONTROLLER)
│   │   ├── IModeEvaluator.cs / ModeEvaluator.cs       # Đánh giá mode Mock/Real, hot-reload & header override
│   │   ├── IMockDataMapper.cs / MockDataMapper.cs     # Đọc Seed JSON, kiểm định hợp đồng qua Protobuf Parser
│   │   ├── IGrpcServiceCaller.cs / GrpcServiceCaller.cs # Forward metadata (JWT, Idempotency-Key, TraceId) & gọi gRPC
│   │   └── IGatewayDispatcher.cs / GatewayDispatcher.cs # Bộ điều phối chính kết hợp 3 khối trên
│   │
│   ├── Extensions/                       # MỞ RỘNG DI & PIPELINE TẬP TRUNG
│   │   ├── ConfigurationExtensions.cs    # Cấu hình Options & logging
│   │   ├── CoreServicesExtension.cs      # Đăng ký Controller, Dispatching core, Redis, ExceptionHandler
│   │   ├── GrpcClientsExtension.cs       # Đăng ký 8 typed gRPC clients
│   │   ├── PipelineExtensions.cs         # Cấu hình Middleware pipeline chuẩn REST
│   │   ├── SwaggerExtension.cs           # Swagger UI + Bearer Auth + MockEngineDocumentFilter
│   │   └── TelemetryExtension.cs         # Cấu hình OpenTelemetry tracing & metrics
│   │
│   ├── Middleware/
│   │   ├── GatewayExceptionHandler.cs    # Xử lý ngoại lệ tập trung, map gRPC RpcException sang HTTP RFC 9110
│   │   └── RedisRateLimitMiddleware.cs   # Giới hạn tần suất gọi phân tán bằng Redis (Fixed Window)
│   │
│   ├── MockEngine/
│   │   ├── IMockEngine.cs                # Interface quản lý nạp, khớp và hot-reload mock definitions
│   │   ├── MockEngine.cs                 # Triển khai MockEngine đọc file *.mock.json, FileSystemWatcher debounce
│   │   ├── MockEndpointDefinition.cs     # Schema POCO của một mock endpoint khai báo động
│   │   ├── MockRouteMatcher.cs           # Thuật toán so khớp URL động (hỗ trợ tham số URL {id}, regex)
│   │   ├── MockRouteMatch.cs             # Kết quả sau khi so khớp route
│   │   ├── MockEngineMiddleware.cs       # Middleware chặn request trước và phục vụ dữ liệu mock
│   │   └── MockEngineDocumentFilter.cs   # Tự động đẩy các mock endpoint động lên Swagger UI
│   │
│   ├── Seeds/                            # DỮ LIỆU SEED CHUẨN HÓA THEO SERVICE & ENTITY
│   │   ├── users/                        # Fixtures JSON cho UserService (login, register, profile, roles...)
│   │   ├── catalog/                      # Fixtures JSON cho CatalogService (products, categories, inventory...)
│   │   ├── commerce/                     # Fixtures JSON cho CommerceService (cart, orders, checkout...)
│   │   ├── log/                          # Fixtures JSON cho LogService (system logs, event types...)
│   │   ├── auction/                      # Fixtures JSON cho AuctionService (auctions, bids...)
│   │   ├── fulfillment/                  # Fixtures JSON cho FulfillmentService (inventory, shipping quotes...)
│   │   ├── notification/                 # Fixtures JSON cho NotificationService (notifications, preferences...)
│   │   ├── wallet/                       # Fixtures JSON cho WalletService (balance, deposit, withdraw, transfer...)
│   │   └── declarative/                  # Các file *.mock.json khai báo endpoint độc lập
│   │
│   ├── Responses/                        # ĐẦU RA: UNIFIED OUTPUT FACTORY
│   │   ├── ApiResponse.cs                # Envelope định dạng phản hồi thành công chuẩn REST
│   │   ├── ApiErrorResponse.cs           # Envelope định dạng lỗi theo chuẩn IETF RFC 9110 / RFC 6585
│   │   ├── PagedData.cs                  # Envelope dữ liệu phân trang
│   │   ├── PaginationMetadata.cs         # Metadata phân trang (pageIndex, pageSize, totalPages, hasNext...)
│   │   ├── IApiResponseFactory.cs        # Interface factory tạo response
│   │   ├── ApiResponseFactory.cs         # Triển khai factory chuẩn hóa response & tự động resolve TraceId
│   │   └── HttpContextUserExtensions.cs  # Tiện ích đọc ClaimsPrincipal (sub, role) chuyển sang UserRequest
│   │
│   ├── Security/
│   │   ├── GatewayAuthorization.cs       # Định nghĩa các Policy phân quyền RBAC & đánh dấu Bypass
│   │   ├── GatewaySecurityHelper.cs      # Helper kiểm tra trạng thái Mock/Real của request, resolve policy
│   │   ├── SecurityConfiguration.cs      # Cấu hình xác thực JWT Bearer, token validation, Mock token handler
│   │   ├── MockSecurityAdapterMiddleware.cs # Middleware cấp danh tính giả lập (ClaimsPrincipal) khi test mock
│   │   └── EdgeMiddlewareConfiguration.cs   # Đăng ký CORS và RateLimiter built-in của ASP.NET Core
│   │
│   └── Services/
│       ├── IGatewayRuntimeManager.cs     # Interface quản lý runtime mode (Mock/Real, AuthMode)
│       └── GatewayRuntimeManager.cs      # Quản lý trạng thái Real/Mock theo service, ghi file gateway.runtime.json
│
└── Nexus.Gateway.Tests/                  # PROJECT KIỂM THỬ TỰ ĐỘNG (111/111 PASS 100%)
    ├── Factory.cs                        # Custom WebApplicationFactory dựng TestServer in-memory
    ├── ApiResponseFactoryTests.cs        # Unit tests kiểm tra định dạng envelope response & error
    ├── GatewayTests.cs                   # Integration tests kiểm tra Kestrel limits, timeouts, Redis failover
    ├── GatewaySurfaceTests.cs            # Tests kiểm tra toàn bộ bề mặt API các service
    ├── MockEngineTests.cs                # Tests kiểm tra thuật toán matcher, route precedence, dynamic mocks
    ├── SevenServicesGatewayTests.cs      # Tests kiểm tra chi tiết cả 8 microservices ở 2 chế độ Standard & Bypass
    ├── HealthProberUnitTests.cs          # Tests kiểm tra State Machine, Ring Buffer và File Rolling xoay 1000 dòng
    ├── HealthProberControllerTests.cs    # Tests kiểm tra API quản trị Prober, Manual Check & Circuit Breaker
    └── EnumConversionTests.cs            # Tests kiểm tra chuyển đổi Enum 2 chiều PascalCase String và bắt lỗi 400
```

---

# 5. BẢNG TỔNG HỢP 15 CONTROLLERS & DANH MỤC CÁC NHÓM ROUTE

Toàn bộ các endpoint trong Gateway đều bắt đầu bằng tiền tố duy nhất:
$$\mathbf{/api/v1/...}$$

| STT | Controller | File Code | Route Prefix | Quyền Hạn (RBAC) | Chức Năng Nghiệp Vụ Chính | Service Đích |
|:---:|:---|:---|:---|:---|:---|:---:|
| 1 | `AuthController` | `Controllers/AuthController.cs` | `/api/v1/auth` | Anonymous | Đăng ký tài khoản (`/register`), xác thực OTP (`/verify-otp`), gửi lại OTP (`/resend-otp`), đăng nhập (`/login`). | `users` |
| 2 | `UsersController` | `Controllers/UsersController.cs` | `/api/v1/users/me` | Authenticated | Xem thông tin cá nhân (`/`), cập nhật hồ sơ, yêu cầu nâng cấp người bán (`/seller-upgrade`). | `users` |
| 3 | `AdminUsersController` | `Controllers/AdminUsersController.cs` | `/api/v1/admin` | Staff / Admin | Xem danh sách người dùng (`/users`), duyệt hồ sơ seller (`/sellers/{id}/approve`), gán role, xem roles/permissions. | `users` |
| 4 | `CartController` | `Controllers/CartController.cs` | `/api/v1/cart`, `/api/v1/commerce/cart` | Authenticated | Thao tác giỏ hàng: xem giỏ, thêm hàng (`/items`), sửa số lượng, xóa sản phẩm, xóa trắng giỏ. | `commerce` |
| 5 | `OrdersController` | `Controllers/OrdersController.cs` | `/api/v1/orders`, `/api/v1/commerce/orders` | Authenticated | Tạo đơn hàng checkout, xem danh sách đơn của tôi, xem chi tiết đơn hàng theo ID, seller/admin orders. | `commerce` |
| 6 | `ProductsController` | `Controllers/ProductsController.cs` | `/api/v1/products`, `/api/v1/catalog/products` | Anonymous / Seller | Xem danh sách sản phẩm, xem chi tiết sản phẩm. Seller tạo/sửa/xóa sản phẩm (`/seller/products`). | `catalog` |
| 7 | `CategoriesController` | `Controllers/CategoriesController.cs` | `/api/v1/categories`, `/api/v1/catalog/categories` | Anonymous / Admin | Xem danh mục sản phẩm. Admin tạo/sửa/xóa danh mục. | `catalog` |
| 8 | `InventoryController` | `Controllers/InventoryController.cs` | `/api/v1/seller/inventory` | Seller | Quản lý kho ảo seller, điều chỉnh số lượng tồn kho ảo. | `catalog` |
| 9 | `AuctionController` | `Controllers/AuctionController.cs` | `/api/v1/auctions` | Public / Seller / Auth | Xem danh sách đấu giá, xem chi tiết, seller tạo phiên đấu giá mới, người mua đặt giá trả (`/bids`), xem lịch sử giá bid. | `auction` |
| 10 | `FulfillmentController` | `Controllers/FulfillmentController.cs`| `/api/v1/fulfillment` | Anonymous / Public | Tra cứu tồn kho kho bãi vật lý (`/inventory/{skuId}`), tính phí giao hàng (`/shipping/quote`), tra cứu mã vận đơn (`/shipments/{code}`). | `fulfillment` |
| 11 | `NotificationController`| `Controllers/NotificationController.cs`| `/api/v1/notifications`| Authenticated | Xem danh sách thông báo cá nhân, đánh dấu đã đọc (`/read`), xem/cập nhật tùy chọn nhận thông báo (`/preferences`). | `notification` |
| 12 | `LogController` | `Controllers/LogController.cs` | `/api/v1/log` | Staff / Admin | Tra cứu nhật ký hệ thống (`/logs`), xem chi tiết log, xem danh mục sự kiện log (`/event-types`), bật/tắt event type. | `log` |
| 13 | `WalletController` | `Controllers/WalletController.cs` | `/api/v1/wallet` | Authenticated | Quản lý ví điện tử: tra cứu số dư (`/balance`), nạp tiền (`/deposit`), rút tiền (`/withdraw`), chuyển tiền nội bộ (`/transfer`), xem lịch sử giao dịch. | `wallet` |
| 14 | `GatewayControlController`| `Controllers/GatewayControlController.cs`| `/api/v1/gateway`, `/api/v1/mock-engine` | GatewayAdmin (chặn bypass)| Tra cứu trạng thái 8 services (`GET /modes`), đổi auth mode (`PUT /mode`), đổi mode Mock/Real, quản lý mock definitions. | *Nội bộ Gateway* |
| 15 | `HealthProberController` | `Controllers/HealthProberController.cs` | `/api/v1/gateway/prober`| GatewayAdmin (chặn bypass)| Tra cứu trạng thái Prober 8 services (`GET /status`), bật/tắt daemon từng service hoặc bulk, kiểm tra sức khỏe thủ công tức thì. | *Nội bộ Gateway* |

---

# 6. QUY TRÌNH 3 BƯỚC THÊM API MỚI DÀNH CHO DEVELOPER

Nhờ kiến trúc 3 khối độc lập và bộ điều phối `GatewayDispatcher`, quy trình bổ sung một API mới diễn ra cực kỳ nhanh chóng và không làm xáo trộn mã nguồn:

### Bước 1: Khai báo Contract trong `Contracts/shop.proto`
Thêm RPC method và message tương ứng trong file Protobuf:
```protobuf
service CatalogApi {
  // Thêm mới RPC method
  rpc ListBrands(PageRequest) returns (BrandPage);
}

message BrandPage {
  repeated Brand items = 1;
  int32 total = 2;
}

message Brand {
  string id = 1;
  string name = 2;
  string logo_url = 3;
}
```
*(Ngay sau khi lưu file, trình biên dịch `protoc` sẽ tự động sinh mã C# cho `BrandPage`, `Brand`, `CatalogApiClient`).*

---

### Bước 2: Tạo File Seed Mẫu Tại `Seeds/catalog/brands.json`
Tạo file JSON mẫu chứa dữ liệu phản hồi tại `Nexus.Gateway/Seeds/catalog/brands.json`:
```json
{
  "items": [
    { "id": "brand-01", "name": "Keychron", "logoUrl": "https://example.com/keychron.png" },
    { "id": "brand-02", "name": "Akko", "logoUrl": "https://example.com/akko.png" }
  ],
  "total": 2
}
```
*(File này sẽ được Protobuf MessageParser tự động thẩm định schema, bảo đảm không bao giờ sai lệch với Backend).*

---

### Bước 3: Thêm Action Trong Controller Tương Ứng (`Controllers/CatalogController.cs`)
`CatalogController` đã khai báo `[Route("api/v1")]` và `[BackendTag("catalog")]`. Action chỉ khai báo route **tương đối**, policy, request HTTP và lời gọi gRPC typed; không tự dựng JSON response hoặc tự xét Mock/Real:
```csharp
[HttpGet("catalog/brands")]
[AllowAnonymous]
[ProducesResponseType(typeof(ApiResponse<PagedData<Brand>>), StatusCodes.Status200OK)]
public Task<IResult> ListBrands([FromQuery] PaginationRequest pagination)
{
    if (!pagination.IsValid)
        throw new GatewayInputException(ValidationHelpers.PageMessage, field: "Pagination");

    return _dispatcher.DispatchPagedAsync(
        HttpContext, "catalog", "brands", pagination.Page, pagination.PageSize,
        o => _client.ListBrandsAsync(new PageRequest
        {
            Page = pagination.Page,
            PageSize = pagination.PageSize
        }, o),
        BrandPage.Parser);
}
```
`PaginationRequest` là HTTP query model (`page`, `pageSize` mặc định `1`, `20`); **không thay** các request protobuf. `GatewayInputException` là tùy chọn gọn cho input validation; các action đang dùng `ValidationHelpers.Bad(...)` vẫn đúng schema. RPC và seed của ví dụ chỉ hoạt động khi bạn đã bổ sung contract và backend tương ứng.

👉 **Hoàn tất!**
- Khi `catalog` ở mode `Mock`: Gateway tự động đọc file `Seeds/catalog/brands.json`, validate schema và trả về `ApiResponse<PagedData<Brand>>`.
- Khi `catalog` ở mode `Real`: Gateway tự động forward gRPC call sang `CatalogService:8002`, đính kèm JWT token, idempotency key và traceId.
- Endpoint mới tự động hiển thị đầy đủ tài liệu trên Swagger UI.

---

# 7. BẢO ĐẢM CHẤT LƯỢNG & KIỂM THỬ TỰ ĐỘNG

Hệ sinh thái Gateway đi kèm bộ kiểm thử tự động toàn diện trong project `Nexus.Gateway.Tests/`, bao phủ mọi khía cạnh an ninh, hiệu năng và tính đúng đắn của nghiệp vụ:

- **`ApiResponseFactoryTests.cs`**: Đảm bảo envelope thành công và lỗi Problem Details RFC 9110 luôn chuẩn định dạng.
- **`GatewayTests.cs`**: Kiểm tra Kestrel limits (64KB), timeout 65s, và Rate Limiting phân tán qua Redis.
- **`MockEngineTests.cs`**: Kiểm tra thuật toán khớp route động, ưu tiên route tĩnh, và nạp mock JSON.
- **`GatewaySurfaceTests.cs`**: Kiểm tra toàn bộ bề mặt API của 8 services.
- **`SevenServicesGatewayTests.cs`**: Kiểm tra một số luồng Mock/Real và auth Enforce/Bypass; không thay thế integration tests của 8 backend.
- **`HealthProberUnitTests.cs`**: Kiểm tra logic State Machine, Ring Buffer, và cơ chế File Rolling tự động xoay file khi đạt ngưỡng 1000 dòng.
- **`HealthProberControllerTests.cs`**: Kiểm tra phân quyền Admin cho API Prober, bật/tắt daemon từng service & bulk, API check thủ công (Manual Check), và Circuit Breaker ngắt mạch trong 0ms khi Unhealthy (HTTP 503 `DOWNSTREAM_UNHEALTHY`).
- **`EnumConversionTests.cs`**: Kiểm chứng chuyển đổi Enum 2 chiều (Serialize Enum số ➔ PascalCase String, Deserialize case-insensitive String ➔ Enum, và bắt lỗi validation 400 ProblemDetails khi FE gửi sai).

### Lệnh chạy kiểm thử:
```bash
dotnet test src/Services/ApiGateway/ApiGateway.sln -c Release
```
👉 Hiện tại toàn bộ **120/120 bài kiểm thử tự động** đều vượt qua tuyệt đối (100% Passed). Chạy lại lệnh sau mỗi thay đổi; không dùng số bài test cố định làm bảo đảm chất lượng. Đặc biệt kiểm tra auth Standard/Bypass, mode Mock/Real, HTTP status 200/201/4xx, lỗi typed/malformed JSON, route phân trang và response trace ID.
