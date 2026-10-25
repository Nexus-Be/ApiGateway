namespace Nexus.Gateway.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Auction;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using ShoppingCart.Contracts.V1;

/// <summary>
/// Auction HTTP API — the gateway-side slice of the auction flow contract (F3, AUC-01 through AUC-19).
/// </summary>
/// <remarks>
/// Response thành công bọc trong <c>{ success, statusCode, message, data, traceId, timestamp }</c>; danh sách có thêm <c>data.pagination</c>. Lỗi trả <c>errorCode</c> + <c>detail</c>.
/// Trạng thái phiên (AuctionStatus): DRAFT, SCHEDULED, RUNNING, ENDED, SETTLED, FAILED, CANCELLED.
/// Thời gian dạng ISO 8601 (UTC). Tiền là số (VND). Mock đọc Seeds/auction/*.json; Real gọi gRPC AuctionService.
/// <para>Phạm vi: 16 route REST tương ứng AUC-01..AUC-19. Ba mục còn lại của F3 nằm ngoài phạm vi gateway: AUC-20 là gRPC <c>Auction.CheckPaymentDeadline</c>, AUC-34/AUC-35 là route <c>/internal/v1/auctions/*</c> chỉ dùng cho gọi nội bộ service-to-service.</para>
/// </remarks>
[ApiController]
[Route("api/v1")]
[BackendTag("auction")]
[EnableRateLimiting("api")]
public sealed class AuctionController : ControllerBase
{
    private const string Service = "auction";

    private readonly IGatewayDispatcher _dispatcher;
    private readonly AuctionApi.AuctionApiClient _client;

    public AuctionController(IGatewayDispatcher dispatcher, AuctionApi.AuctionApiClient client)
    {
        _dispatcher = dispatcher;
        _client = client;
    }

    // ---------------------------------------------------------------- Public / buyer

    /// <summary>Xem danh sách phiên đấu giá (AUC-01)</summary>
    /// <remarks>
    /// <para>Quyền: không cần đăng nhập. Dùng cho trang chủ / trang danh sách đấu giá.</para>
    /// <para>Query: <c>page</c> (mặc định 1), <c>pageSize</c> (mặc định 20, tối đa 100), <c>status</c> (tùy chọn: DRAFT, SCHEDULED, RUNNING, ENDED, SETTLED, FAILED, CANCELLED). Ô <c>IsValid</c> trên Swagger là tham số nội bộ, bỏ qua.</para>
    /// <para>Mỗi item: auctionId, productId, skuId, sellerId, productName, primaryImageUrl (null nếu chưa có ảnh), status, startPriceAmount, currentPriceAmount (null nếu chưa có bid), bidCount, scheduledStartAt, scheduledEndAt.</para>
    /// <para>Response: <c>data.items[]</c> + <c>data.pagination</c> (pageIndex, pageSize, totalCount, totalPages, hasPreviousPage, hasNextPage). Field có giá trị null sẽ KHÔNG xuất hiện trong JSON, FE hiểu là null.</para>
    /// <para>Mock: trả dữ liệu cố định, chưa lọc theo status.</para>
    /// <para>Lỗi (Mock chỉ trả 400/401/403 do Gateway; các mã có ghi (Real) do AuctionService trả khi chuyển sang Real). Định dạng lỗi: errorCode + detail.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: page hoặc pageSize không hợp lệ, hoặc status không thuộc AuctionStatus</response>
    /// <param name="pagination">Trang và kích thước trang</param>
    /// <param name="status">Lọc theo trạng thái phiên (tùy chọn)</param>
    // AUC-01 | seed: Seeds/auction/*.json
    [HttpGet("auctions")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedData<AuctionSummary>>), StatusCodes.Status200OK)]
    public Task<IResult> ListAuctions([FromQuery] PaginationRequest pagination, [FromQuery] string? status)
    {
        pagination.EnsureValid();
        AuctionValidation.EnsureStatus(status);

        return _dispatcher.DispatchPagedAsync(
            HttpContext, Service, "auctions",
            pagination.Page, pagination.PageSize,
            o => _client.ListAuctionsAsync(BuildListRequest(pagination, status), o),
            AuctionListResponse.Parser);
    }

    /// <summary>Xem chi tiết 1 phiên đấu giá (AUC-02)</summary>
    /// <remarks>
    /// <para>Quyền: không cần đăng nhập. Dùng cho trang chi tiết phiên.</para>
    /// <para>Path: <c>auctionId</c> (UUID).</para>
    /// <para>Response: thông tin sản phẩm (productName, description, images[{url,isPrimary}], specifications), giá (startPriceAmount, bidStepAmount, currentPriceAmount), bidCount, leadingBidderDisplayName (tên đã che, null nếu chưa có bid), thời gian (scheduledStartAt, originalEndAt, scheduledEndAt, startedAt, endedAt), chống bid phút chót (extensionCount, maxExtensionCount, antiSnipingExtensionMinutes), minReputationToBid, recreatedFromAuctionId (null nếu không phải phiên tạo lại), createdAt.</para>
    /// <para>Mẹo: <c>scheduledEndAt</c> có thể dời khi có bid phút chót, FE dùng giá trị này để đếm ngược.</para>
    /// <para>Lỗi (Mock chỉ trả 400/401/403 do Gateway; các mã có ghi (Real) do AuctionService trả khi chuyển sang Real). Định dạng lỗi: errorCode + detail.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: auctionId sai định dạng UUID</response>
    /// <response code="404">AUCTION_NOT_FOUND: phiên không tồn tại (do AuctionService trả khi chạy Real)</response>
    /// <param name="auctionId">UUID của phiên</param>
    // AUC-02 | seed: Seeds/auction/*.json
    [HttpGet("auctions/{auctionId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuctionDetailResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetAuction(string auctionId)
    {
        AuctionValidation.EnsureAuctionId(auctionId);

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "auction",
            o => _client.GetAuctionAsync(new AuctionIdRequest { AuctionId = auctionId }, o),
            AuctionDetailResponse.Parser,
            successMessage: "Auction retrieved successfully.");
    }

    /// <summary>Xem lịch sử đặt giá của 1 phiên (AUC-03)</summary>
    /// <remarks>
    /// <para>Quyền: không cần đăng nhập.</para>
    /// <para>Path: <c>auctionId</c>. Query: <c>page</c> (mặc định 1), <c>pageSize</c> (mặc định 20, tối đa 100).</para>
    /// <para>Mỗi item: bidId, sequenceNo (thứ tự bid), bidderId, bidderDisplayName (tên đã che), amount, placedAt, isOutbid (đã bị vượt giá), isWinning (đang dẫn đầu / thắng), isInvalidated (bid bị vô hiệu).</para>
    /// <para>Response: <c>data.items[]</c> + <c>data.pagination</c> (pageIndex, pageSize, totalCount, totalPages, hasPreviousPage, hasNextPage). Field có giá trị null sẽ KHÔNG xuất hiện trong JSON, FE hiểu là null.</para>
    /// <para>Lỗi (Mock chỉ trả 400/401/403 do Gateway; các mã có ghi (Real) do AuctionService trả khi chuyển sang Real). Định dạng lỗi: errorCode + detail.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: auctionId sai UUID hoặc page/pageSize không hợp lệ</response>
    /// <response code="404">AUCTION_NOT_FOUND: phiên không tồn tại</response>
    /// <param name="auctionId">UUID của phiên</param>
    /// <param name="pagination">Trang và kích thước trang</param>
    // AUC-03 | seed: Seeds/auction/*.json
    [HttpGet("auctions/{auctionId}/bids")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedData<BidItem>>), StatusCodes.Status200OK)]
    public Task<IResult> ListAuctionBids(string auctionId, [FromQuery] PaginationRequest pagination)
    {
        AuctionValidation.EnsureAuctionId(auctionId);
        pagination.EnsureValid();

        return _dispatcher.DispatchPagedAsync(
            HttpContext, Service, "auction-bids",
            pagination.Page, pagination.PageSize,
            o => _client.ListAuctionBidsAsync(new ListAuctionBidsRequest
            {
                AuctionId = auctionId,
                Page = pagination.Page,
                PageSize = pagination.PageSize
            }, o),
            BidListResponse.Parser);
    }

    /// <summary>Đặt giá cho 1 phiên (AUC-07)</summary>
    /// <remarks>
    /// <para>Quyền: phải đăng nhập (Buyer). Điểm uy tín phải đạt ngưỡng đặt giá (mặc định 40) và tài khoản không bị khóa.</para>
    /// <para>Path: <c>auctionId</c>. Body: <c>{ "amount": number }</c>, amount lớn hơn 0 và tối đa 2 chữ số thập phân.</para>
    /// <para>BẮT BUỘC 2 header: <c>Idempotency-Key</c> (chuỗi duy nhất mỗi lần bấm đặt giá, gửi lại cùng key khi retry) và <c>If-Match</c> (rowVersion của phiên lấy từ lần xem trước, dùng để phát hiện có người đặt giá trước).</para>
    /// <para>Response 201: bidId, auctionId, bidSeq, amount, placedAt, currentPriceAmount, bidCount, scheduledEndAt (có thể đã dời), extensionCount.</para>
    /// <para>Mock: không kiểm tra giá tối thiểu / trạng thái phiên, luôn trả bid thành công.</para>
    /// <para>Lỗi (Mock chỉ trả 400/401/403 do Gateway; các mã có ghi (Real) do AuctionService trả khi chuyển sang Real). Định dạng lỗi: errorCode + detail.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: auctionId sai UUID, thiếu Idempotency-Key hoặc If-Match, amount không hợp lệ</response>
    /// <response code="401">UNAUTHORIZED: thiếu token hoặc token hết hạn</response>
    /// <response code="403">BIDDER_NOT_ELIGIBLE: uy tín thấp hơn ngưỡng đặt giá hoặc tài khoản bị khóa (Real)</response>
    /// <response code="404">AUCTION_NOT_FOUND (Real)</response>
    /// <response code="409">CONCURRENT_UPDATE: If-Match lệch hoặc có người đặt giá trước, FE tải lại phiên rồi thử lại (Real)</response>
    /// <response code="410">AUCTION_ENDED: phiên đã kết thúc hoặc quá giờ (Real)</response>
    /// <response code="422">AUCTION_NOT_RUNNING / BID_TOO_LOW (kèm minNextBidAmount) / IDEMPOTENCY_KEY_REUSED (Real)</response>
    /// <response code="503">USER_SERVICE_UNAVAILABLE (Real)</response>
    /// <param name="auctionId">UUID của phiên</param>
    /// <param name="idempotencyKey">Khóa chống gửi trùng (bắt buộc)</param>
    /// <param name="ifMatch">rowVersion của phiên (bắt buộc)</param>
    /// <param name="body">Nội dung JSON gửi lên, xem chi tiết ở phần mô tả</param>
    // AUC-07 | seed: Seeds/auction/*.json
    [HttpPost("auctions/{auctionId}/bids")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<PlaceBidResponse>), StatusCodes.Status201Created)]
    public Task<IResult> PlaceBid(
        string auctionId,
        [FromBody] PlaceBidRequestDto body,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "If-Match")] string? ifMatch)
    {
        AuctionValidation.EnsureAuctionId(auctionId);
        AuctionValidation.EnsureBidHeaders(idempotencyKey, ifMatch);
        AuctionValidation.EnsureBidAmount(body.Amount);

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "auction-bid-place",
            o => _client.PlaceBidAsync(new PlaceBidRequest
            {
                AuctionId = auctionId,
                Amount = (double)body.Amount!.Value,
                IfMatch = ifMatch!
            }, o),
            PlaceBidResponse.Parser,
            successMessage: "Bid placed successfully.",
            successStatusCode: StatusCodes.Status201Created);
    }

    /// <summary>Xem các phiên mình đã tham gia đấu giá (AUC-08)</summary>
    /// <remarks>
    /// <para>Quyền: phải đăng nhập (Buyer).</para>
    /// <para>Query: <c>page</c>, <c>pageSize</c> (tối đa 100), <c>status</c> (tùy chọn, lọc theo trạng thái phiên).</para>
    /// <para>Mỗi item: auctionId, productId, productName, primaryImageUrl, auctionStatus, myHighestBidAmount (giá cao nhất của mình), currentPriceAmount, isLeading (mình đang dẫn đầu), endsAt, lastBidAt.</para>
    /// <para>Response: <c>data.items[]</c> + <c>data.pagination</c> (pageIndex, pageSize, totalCount, totalPages, hasPreviousPage, hasNextPage). Field có giá trị null sẽ KHÔNG xuất hiện trong JSON, FE hiểu là null.</para>
    /// <para>Lỗi (Mock chỉ trả 400/401/403 do Gateway; các mã có ghi (Real) do AuctionService trả khi chuyển sang Real). Định dạng lỗi: errorCode + detail.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: page/pageSize không hợp lệ hoặc status sai</response>
    /// <response code="401">UNAUTHORIZED: thiếu token hoặc token hết hạn</response>
    /// <param name="pagination">Trang và kích thước trang</param>
    /// <param name="status">Lọc theo trạng thái phiên (tùy chọn)</param>
    // AUC-08 | seed: Seeds/auction/*.json
    [HttpGet("me/bids")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<PagedData<MyBidItem>>), StatusCodes.Status200OK)]
    public Task<IResult> ListMyBids([FromQuery] PaginationRequest pagination, [FromQuery] string? status)
    {
        pagination.EnsureValid();
        AuctionValidation.EnsureStatus(status);

        return _dispatcher.DispatchPagedAsync(
            HttpContext, Service, "me-bids",
            pagination.Page, pagination.PageSize,
            o =>
            {
                var request = new ListMyBidsRequest { Page = pagination.Page, PageSize = pagination.PageSize };
                if (status is not null) request.Status = status;
                return _client.ListMyBidsAsync(request, o);
            },
            MyBidListResponse.Parser);
    }

    /// <summary>Xem kết quả của 1 phiên đấu giá (AUC-09)</summary>
    /// <remarks>
    /// <para>Quyền: phải đăng nhập. Chỉ người thắng, Seller của phiên hoặc Admin xem được (AuctionService kiểm tra khi chạy Real).</para>
    /// <para>Path: <c>auctionId</c>.</para>
    /// <para>Response: status, hasWinner, winnerId / winnerDisplayName / winningBidId / finalPriceAmount (null nếu không có người thắng), sellerId, determinedAt, orderId (đơn hàng tạo sau khi chốt), paymentDeadlineAt (hạn thanh toán 24 giờ), paidAt (null nếu chưa trả).</para>
    /// <para>Lỗi (Mock chỉ trả 400/401/403 do Gateway; các mã có ghi (Real) do AuctionService trả khi chuyển sang Real). Định dạng lỗi: errorCode + detail.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: auctionId sai UUID</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN: không phải người thắng, Seller của phiên hoặc Admin (Real)</response>
    /// <response code="404">AUCTION_NOT_FOUND</response>
    /// <param name="auctionId">UUID của phiên</param>
    // AUC-09 | seed: Seeds/auction/*.json
    [HttpGet("auctions/{auctionId}/result")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<AuctionResultResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetAuctionResult(string auctionId)
    {
        AuctionValidation.EnsureAuctionId(auctionId);

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "auction-result",
            o => _client.GetAuctionResultAsync(new AuctionIdRequest { AuctionId = auctionId }, o),
            AuctionResultResponse.Parser,
            successMessage: "Auction result retrieved successfully.");
    }

    // ---------------------------------------------------------------- Seller

    /// <summary>Seller tạo phiên đấu giá (AUC-10)</summary>
    /// <remarks>
    /// <para>Quyền: Seller đã được duyệt gian hàng (role seller hoặc admin). Uy tín phải đạt ngưỡng tổ chức đấu giá (mặc định 50).</para>
    /// <para>Body: <c>productId</c> (UUID), <c>skuId</c> (UUID), <c>startPriceAmount</c> (từ 0 trở lên), <c>bidStepAmount</c> (tùy chọn, lớn hơn 0, bỏ trống thì dùng bước giá mặc định theo cấu hình), <c>scheduledStartAt</c> (phải ở tương lai), <c>scheduledEndAt</c> (sau giờ bắt đầu). Giờ dạng ISO 8601, ví dụ 2026-10-30T08:00:00Z.</para>
    /// <para>Quy tắc do AuctionService kiểm tra (Real): giờ bắt đầu không quá 30 ngày kể từ lúc tạo, thời lượng từ 60 phút đến 168 giờ, sản phẩm đang hiển thị và thuộc Seller, mỗi sản phẩm chỉ có 1 phiên chưa kết thúc.</para>
    /// <para>Response 201: auctionId, productId, skuId, productName, status (SCHEDULED), startPriceAmount, bidStepAmount, scheduledStartAt, scheduledEndAt, minReputationToBid, antiSnipingExtensionMinutes, maxExtensionCount, createdAt.</para>
    /// <para>Lỗi (Mock chỉ trả 400/401/403 do Gateway; các mã có ghi (Real) do AuctionService trả khi chuyển sang Real). Định dạng lỗi: errorCode + detail.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: UUID sai, giá khởi điểm nhỏ hơn 0, bước giá không lớn hơn 0, giờ bắt đầu ở quá khứ, giờ kết thúc không sau giờ bắt đầu</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">SELLER_NOT_ELIGIBLE: chưa được duyệt gian hàng hoặc uy tín thấp (Real)</response>
    /// <response code="409">PRODUCT_ALREADY_IN_AUCTION / INVENTORY_UNAVAILABLE (Real)</response>
    /// <response code="422">PRODUCT_NOT_ELIGIBLE / INVALID_SCHEDULE (Real)</response>
    /// <response code="503">USER_SERVICE_UNAVAILABLE (Real)</response>
    /// <param name="body">Nội dung JSON gửi lên, xem chi tiết ở phần mô tả</param>
    // AUC-10 | seed: Seeds/auction/*.json
    [HttpPost("auctions")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<AuctionCreatedResponse>), StatusCodes.Status201Created)]
    public Task<IResult> CreateAuction([FromBody] CreateAuctionRequestDto body)
    {
        AuctionValidation.EnsureCreate(body);

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "auction-create",
            o =>
            {
                var request = new CreateAuctionRequest
                {
                    ProductId = body.ProductId!,
                    SkuId = body.SkuId!,
                    StartPriceAmount = (double)body.StartPriceAmount!.Value,
                    ScheduledStartAt = AuctionValidation.ToIso(body.ScheduledStartAt!.Value),
                    ScheduledEndAt = AuctionValidation.ToIso(body.ScheduledEndAt!.Value)
                };
                if (body.BidStepAmount.HasValue) request.BidStepAmount = (double)body.BidStepAmount.Value;
                return _client.CreateAuctionAsync(request, o);
            },
            AuctionCreatedResponse.Parser,
            successMessage: "Auction created successfully.",
            successStatusCode: StatusCodes.Status201Created);
    }

    /// <summary>Seller sửa phiên đấu giá khi chưa bắt đầu (AUC-11)</summary>
    /// <remarks>
    /// <para>Quyền: Seller sở hữu phiên (role seller hoặc admin). Chỉ sửa được khi phiên còn DRAFT hoặc SCHEDULED.</para>
    /// <para>Path: <c>auctionId</c>. Body: gửi ít nhất 1 trong <c>startPriceAmount</c>, <c>bidStepAmount</c>, <c>scheduledStartAt</c>, <c>scheduledEndAt</c>. Field không gửi thì giữ nguyên.</para>
    /// <para>Response: auctionId, status, startPriceAmount, bidStepAmount, scheduledStartAt, scheduledEndAt, updatedAt.</para>
    /// <para>Lỗi (Mock chỉ trả 400/401/403 do Gateway; các mã có ghi (Real) do AuctionService trả khi chuyển sang Real). Định dạng lỗi: errorCode + detail.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: auctionId sai UUID, không gửi field nào, giá hoặc giờ không hợp lệ</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN: không phải Seller của phiên (Real)</response>
    /// <response code="404">AUCTION_NOT_FOUND</response>
    /// <response code="409">AUCTION_NOT_EDITABLE: phiên đã bắt đầu (Real)</response>
    /// <response code="422">INVALID_SCHEDULE (Real)</response>
    /// <param name="auctionId">UUID của phiên</param>
    /// <param name="body">Nội dung JSON gửi lên, xem chi tiết ở phần mô tả</param>
    // AUC-11 | seed: Seeds/auction/*.json
    [HttpPut("auctions/{auctionId}")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<AuctionUpdatedResponse>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateAuction(string auctionId, [FromBody] UpdateAuctionRequestDto body)
    {
        AuctionValidation.EnsureAuctionId(auctionId);
        AuctionValidation.EnsureUpdate(body);

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "auction-update",
            o =>
            {
                var request = new UpdateAuctionRequest { AuctionId = auctionId };
                if (body.StartPriceAmount.HasValue) request.StartPriceAmount = (double)body.StartPriceAmount.Value;
                if (body.BidStepAmount.HasValue) request.BidStepAmount = (double)body.BidStepAmount.Value;
                if (body.ScheduledStartAt.HasValue) request.ScheduledStartAt = AuctionValidation.ToIso(body.ScheduledStartAt.Value);
                if (body.ScheduledEndAt.HasValue) request.ScheduledEndAt = AuctionValidation.ToIso(body.ScheduledEndAt.Value);
                return _client.UpdateAuctionAsync(request, o);
            },
            AuctionUpdatedResponse.Parser,
            successMessage: "Auction updated successfully.");
    }

    /// <summary>Seller hủy phiên đấu giá (AUC-12)</summary>
    /// <remarks>
    /// <para>Quyền: Seller sở hữu phiên. Chỉ hủy được khi phiên chưa có lượt đặt giá; đã có bid thì chỉ Admin hủy được (dùng force-cancel).</para>
    /// <para>Path: <c>auctionId</c>. Body (tùy chọn): <c>{ "reason": "string" }</c>. Có thể không gửi body; nếu gửi reason thì không được rỗng.</para>
    /// <para>Response: auctionId, fromStatus (trạng thái trước khi hủy), status (CANCELLED), reason (null nếu không nhập), cancelledAt.</para>
    /// <para>Lỗi (Mock chỉ trả 400/401/403 do Gateway; các mã có ghi (Real) do AuctionService trả khi chuyển sang Real). Định dạng lỗi: errorCode + detail.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: auctionId sai UUID hoặc reason rỗng</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN: không phải Seller của phiên (Real)</response>
    /// <response code="404">AUCTION_NOT_FOUND</response>
    /// <response code="409">AUCTION_HAS_BIDS (phiên đã có bid, chỉ Admin hủy được) / CONCURRENT_UPDATE (Real)</response>
    /// <response code="422">INVALID_STATE_TRANSITION: phiên không ở DRAFT, SCHEDULED hoặc RUNNING (Real)</response>
    /// <param name="auctionId">UUID của phiên</param>
    /// <param name="body">Nội dung JSON gửi lên (tùy chọn), xem chi tiết ở phần mô tả</param>
    // AUC-12 | seed: Seeds/auction/*.json
    [HttpPost("auctions/{auctionId}/cancel")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<AuctionCancelResponse>), StatusCodes.Status200OK)]
    public Task<IResult> CancelAuction(
        string auctionId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CancelAuctionRequestDto? body)
    {
        AuctionValidation.EnsureAuctionId(auctionId);
        AuctionValidation.EnsureOptionalReason(body?.Reason);

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "auction-cancel",
            o =>
            {
                var request = new CancelAuctionRequest { AuctionId = auctionId };
                if (body?.Reason is not null) request.Reason = body.Reason.Trim();
                return _client.CancelAuctionAsync(request, o);
            },
            AuctionCancelResponse.Parser,
            successMessage: "Auction cancelled successfully.");
    }

    /// <summary>Tạo lại phiên mới từ phiên đấu giá thất bại (AUC-13)</summary>
    /// <remarks>
    /// <para>Quyền: Seller sở hữu phiên cũ. Phiên cũ phải đang ở trạng thái FAILED (không ai đặt giá).</para>
    /// <para>Path: <c>auctionId</c> (UUID phiên cũ). Body: <c>startPriceAmount</c> (bắt buộc), <c>bidStepAmount</c> (tùy chọn), <c>scheduledStartAt</c> (tương lai), <c>scheduledEndAt</c> (sau giờ bắt đầu).</para>
    /// <para>Response 201: auctionId (phiên MỚI), recreatedFromAuctionId (phiên cũ), productId, skuId, status (SCHEDULED), startPriceAmount, bidStepAmount, scheduledStartAt, scheduledEndAt, createdAt.</para>
    /// <para>Lỗi (Mock chỉ trả 400/401/403 do Gateway; các mã có ghi (Real) do AuctionService trả khi chuyển sang Real). Định dạng lỗi: errorCode + detail.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: UUID sai, giá hoặc giờ không hợp lệ</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN / SELLER_NOT_ELIGIBLE (Real)</response>
    /// <response code="404">AUCTION_NOT_FOUND</response>
    /// <response code="409">PRODUCT_ALREADY_IN_AUCTION / INVENTORY_UNAVAILABLE (Real)</response>
    /// <response code="422">INVALID_RECREATE_SOURCE (phiên cũ không ở FAILED) / PRODUCT_NOT_ELIGIBLE / INVALID_SCHEDULE (Real)</response>
    /// <response code="503">USER_SERVICE_UNAVAILABLE (Real)</response>
    /// <param name="auctionId">UUID của phiên cũ</param>
    /// <param name="body">Nội dung JSON gửi lên, xem chi tiết ở phần mô tả</param>
    // AUC-13 | seed: Seeds/auction/*.json
    [HttpPost("auctions/{auctionId}/recreate")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<AuctionRecreatedResponse>), StatusCodes.Status201Created)]
    public Task<IResult> RecreateAuction(string auctionId, [FromBody] RecreateAuctionRequestDto body)
    {
        AuctionValidation.EnsureAuctionId(auctionId);
        AuctionValidation.EnsureRecreate(body);

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "auction-recreate",
            o =>
            {
                var request = new RecreateAuctionRequest
                {
                    AuctionId = auctionId,
                    StartPriceAmount = (double)body.StartPriceAmount!.Value,
                    ScheduledStartAt = AuctionValidation.ToIso(body.ScheduledStartAt!.Value),
                    ScheduledEndAt = AuctionValidation.ToIso(body.ScheduledEndAt!.Value)
                };
                if (body.BidStepAmount.HasValue) request.BidStepAmount = (double)body.BidStepAmount.Value;
                return _client.RecreateAuctionAsync(request, o);
            },
            AuctionRecreatedResponse.Parser,
            successMessage: "Auction recreated successfully.",
            successStatusCode: StatusCodes.Status201Created);
    }

    /// <summary>Seller xem toàn bộ phiên đấu giá của mình (AUC-14)</summary>
    /// <remarks>
    /// <para>Quyền: Seller (role seller hoặc admin). Chỉ trả phiên của chính người gọi.</para>
    /// <para>Query: <c>page</c>, <c>pageSize</c> (tối đa 100), <c>status</c> (tùy chọn).</para>
    /// <para>Mỗi item giống danh sách public (AUC-01) nhưng có thêm <c>createdAt</c>, không có sellerId.</para>
    /// <para>Response: <c>data.items[]</c> + <c>data.pagination</c> (pageIndex, pageSize, totalCount, totalPages, hasPreviousPage, hasNextPage). Field có giá trị null sẽ KHÔNG xuất hiện trong JSON, FE hiểu là null.</para>
    /// <para>Lỗi (Mock chỉ trả 400/401/403 do Gateway; các mã có ghi (Real) do AuctionService trả khi chuyển sang Real). Định dạng lỗi: errorCode + detail.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: page/pageSize hoặc status không hợp lệ</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN: người gọi không phải Seller</response>
    /// <param name="pagination">Trang và kích thước trang</param>
    /// <param name="status">Lọc theo trạng thái phiên (tùy chọn)</param>
    // AUC-14 | seed: Seeds/auction/*.json
    [HttpGet("seller/auctions")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<PagedData<AuctionSummary>>), StatusCodes.Status200OK)]
    public Task<IResult> ListSellerAuctions([FromQuery] PaginationRequest pagination, [FromQuery] string? status)
    {
        pagination.EnsureValid();
        AuctionValidation.EnsureStatus(status);

        return _dispatcher.DispatchPagedAsync(
            HttpContext, Service, "seller-auctions",
            pagination.Page, pagination.PageSize,
            o => _client.ListSellerAuctionsAsync(BuildListRequest(pagination, status), o),
            AuctionListResponse.Parser);
    }

    // ---------------------------------------------------------------- Admin / staff

    /// <summary>Nhật ký đổi trạng thái của 1 phiên (AUC-18)</summary>
    /// <remarks>
    /// <para>Quyền: Admin hoặc Staff (role staff hoặc admin).</para>
    /// <para>Path: <c>auctionId</c>.</para>
    /// <para>Response: auctionId và items[] gồm id, fromStatus (null ở lần đầu), toStatus, reason (null nếu không có), changedBy (UUID người đổi, null nếu hệ thống tự đổi), changedAt. Không phân trang.</para>
    /// <para>Lỗi (Mock chỉ trả 400/401/403 do Gateway; các mã có ghi (Real) do AuctionService trả khi chuyển sang Real). Định dạng lỗi: errorCode + detail.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: auctionId sai UUID</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN: không phải Admin hoặc Staff</response>
    /// <response code="404">AUCTION_NOT_FOUND</response>
    /// <param name="auctionId">UUID của phiên</param>
    // AUC-18 | seed: Seeds/auction/*.json
    [HttpGet("auctions/{auctionId}/status-history")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<AuctionStatusHistoryResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetAuctionStatusHistory(string auctionId)
    {
        AuctionValidation.EnsureAuctionId(auctionId);

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "auction-status-history",
            o => _client.GetAuctionStatusHistoryAsync(new AuctionIdRequest { AuctionId = auctionId }, o),
            AuctionStatusHistoryResponse.Parser,
            successMessage: "Auction status history retrieved successfully.");
    }

    /// <summary>Admin cưỡng chế hủy phiên đấu giá (AUC-15)</summary>
    /// <remarks>
    /// <para>Quyền: CHỈ Admin (Support Staff không có quyền này). Dùng khi phiên có bất thường hoặc đã có bid.</para>
    /// <para>Path: <c>auctionId</c>. Body: <c>{ "reason": "string" }</c>, reason BẮT BUỘC và không rỗng.</para>
    /// <para>Hệ quả (do AuctionService xử lý): nhả kho, thông báo người đã bid và Seller kèm lý do; không trừ uy tín Seller.</para>
    /// <para>Response: auctionId, fromStatus, status (CANCELLED), reason, cancelledBy (UUID admin), cancelledAt.</para>
    /// <para>Lỗi (Mock chỉ trả 400/401/403 do Gateway; các mã có ghi (Real) do AuctionService trả khi chuyển sang Real). Định dạng lỗi: errorCode + detail.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: auctionId sai UUID hoặc reason rỗng</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN: không phải Admin</response>
    /// <response code="404">AUCTION_NOT_FOUND</response>
    /// <response code="409">CONCURRENT_UPDATE (Real)</response>
    /// <response code="422">INVALID_STATE_TRANSITION: từ ENDED trở đi không hủy được (Real)</response>
    /// <param name="auctionId">UUID của phiên</param>
    /// <param name="body">Nội dung JSON gửi lên, xem chi tiết ở phần mô tả</param>
    // AUC-15 | seed: Seeds/auction/*.json
    [HttpPost("admin/auctions/{auctionId}/force-cancel")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AuctionForceCancelResponse>), StatusCodes.Status200OK)]
    public Task<IResult> ForceCancelAuction(string auctionId, [FromBody] ForceCancelAuctionRequestDto body)
    {
        AuctionValidation.EnsureAuctionId(auctionId);
        AuctionValidation.EnsureRequiredReason(body.Reason);

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "auction-force-cancel",
            o => _client.ForceCancelAuctionAsync(new ForceCancelAuctionRequest
            {
                AuctionId = auctionId,
                Reason = body.Reason!.Trim()
            }, o),
            AuctionForceCancelResponse.Parser,
            successMessage: "Auction cancelled by administrator successfully.");
    }

    /// <summary>Admin theo dõi phiên đang ở bước nào của luồng đơn hàng (AUC-19)</summary>
    /// <remarks>
    /// <para>Quyền: Admin hoặc Staff.</para>
    /// <para>Path: <c>auctionId</c>. Chỉ có dữ liệu sau khi chốt người thắng (luồng chuyển sang Commerce).</para>
    /// <para>Response: auctionId, sagaType, currentState, stateData (object, nội dung thay đổi theo từng bước), createdAt, updatedAt.</para>
    /// <para>Lỗi (Mock chỉ trả 400/401/403 do Gateway; các mã có ghi (Real) do AuctionService trả khi chuyển sang Real). Định dạng lỗi: errorCode + detail.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: auctionId sai UUID</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN: không phải Admin hoặc Staff</response>
    /// <response code="404">AUCTION_NOT_FOUND: phiên không tồn tại hoặc không có saga (không có người thắng)</response>
    /// <param name="auctionId">UUID của phiên</param>
    // AUC-19 | seed: Seeds/auction/*.json
    [HttpGet("admin/auctions/{auctionId}/saga")]
    [Authorize(GatewayPolicies.Staff)]
    [ProducesResponseType(typeof(ApiResponse<AuctionSagaResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetAuctionSaga(string auctionId)
    {
        AuctionValidation.EnsureAuctionId(auctionId);

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "auction-saga",
            o => _client.GetAuctionSagaAsync(new AuctionIdRequest { AuctionId = auctionId }, o),
            AuctionSagaResponse.Parser,
            successMessage: "Auction saga retrieved successfully.");
    }

    /// <summary>Admin xem các luật đấu giá đang hiệu lực (AUC-16)</summary>
    /// <remarks>
    /// <para>Quyền: CHỈ Admin. Không có tham số.</para>
    /// <para>Response: items[] gồm configKey, configValue (JSON, kiểu tùy từng key: số, chuỗi, object...), description (null nếu không có), version, updatedBy (null nếu chưa ai sửa), updatedAt. Ví dụ key: ngưỡng uy tín tổ chức đấu giá, ngưỡng đặt giá, số lần gia hạn, số phút gia hạn.</para>
    /// <para>Lỗi (Mock chỉ trả 400/401/403 do Gateway; các mã có ghi (Real) do AuctionService trả khi chuyển sang Real). Định dạng lỗi: errorCode + detail.</para>
    /// </remarks>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN: không phải Admin</response>
    // AUC-16 | seed: Seeds/auction/*.json
    [HttpGet("admin/auction-configs")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AuctionConfigListResponse>), StatusCodes.Status200OK)]
    public Task<IResult> ListAuctionConfigs()
    {
        return _dispatcher.DispatchAsync(
            HttpContext, Service, "auction-configs",
            o => _client.ListAuctionConfigsAsync(new ListAuctionConfigsRequest(), o),
            AuctionConfigListResponse.Parser,
            successMessage: "Auction configs retrieved successfully.");
    }

    /// <summary>Admin cập nhật cấu hình đấu giá (AUC-17)</summary>
    /// <remarks>
    /// <para>Quyền: CHỈ Admin. Chỉ ảnh hưởng các phiên tạo SAU thời điểm cập nhật.</para>
    /// <para>Path: <c>key</c> (configKey cần sửa). Body: <c>configValue</c> (bắt buộc, JSON, kiểu phải đúng theo từng key), <c>description</c> (tùy chọn).</para>
    /// <para>Response: configKey, configValue, description, version (tăng sau mỗi lần sửa), isActive, updatedBy, createdAt, updatedAt.</para>
    /// <para>Lỗi (Mock chỉ trả 400/401/403 do Gateway; các mã có ghi (Real) do AuctionService trả khi chuyển sang Real). Định dạng lỗi: errorCode + detail.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: thiếu configValue hoặc giá trị không hợp lệ theo từng key</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN: không phải Admin</response>
    /// <response code="404">NOT_FOUND: key không tồn tại (Real)</response>
    /// <response code="409">CONFLICT: xung đột khi cập nhật (Real)</response>
    /// <param name="key">Khóa cấu hình cần sửa</param>
    /// <param name="body">Nội dung JSON gửi lên, xem chi tiết ở phần mô tả</param>
    // AUC-17 | seed: Seeds/auction/*.json
    [HttpPut("admin/auction-configs/{key}")]
    [Authorize(GatewayPolicies.Admin)]
    [ProducesResponseType(typeof(ApiResponse<AuctionConfigResponse>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateAuctionConfig(string key, [FromBody] UpdateAuctionConfigRequestDto body)
    {
        AuctionValidation.EnsureConfig(key, body.ConfigValue);

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "auction-config-update",
            o =>
            {
                var request = new UpdateAuctionConfigRequest
                {
                    Key = key,
                    ConfigValue = Google.Protobuf.WellKnownTypes.Value.Parser.ParseJson(body.ConfigValue!.Value.GetRawText())
                };
                if (body.Description is not null) request.Description = body.Description;
                return _client.UpdateAuctionConfigAsync(request, o);
            },
            AuctionConfigResponse.Parser,
            successMessage: "Auction config updated successfully.");
    }

    // ---------------------------------------------------------------- Helpers

    private static ListAuctionsRequest BuildListRequest(PaginationRequest pagination, string? status)
    {
        var request = new ListAuctionsRequest { Page = pagination.Page, PageSize = pagination.PageSize };
        if (status is not null) request.Status = status;
        return request;
    }
}