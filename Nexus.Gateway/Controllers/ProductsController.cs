namespace Nexus.Gateway.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Catalog;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using ShoppingCart.Contracts.V1;

/// <summary>
/// Product HTTP API — gateway-side slice of the F2 product flow (CAT-01 .. CAT-13, CAT-30/31, CAT-40/42/43).
/// </summary>
/// <remarks>
/// <para>Response thành công bọc trong <c>{ success, statusCode, message, data, traceId, timestamp }</c>; danh sách có thêm <c>data.pagination</c>. Lỗi trả <c>errorCode</c> + <c>detail</c>.</para>
/// <para>Route bám đúng hợp đồng F2: <c>/api/v1/products</c>, <c>/api/v1/product/{productId}</c>, <c>/api/v1/product/{productId}pics</c>, <c>/api/v1/variant/{variantId}</c>, <c>/api/v1/product/auction*</c>. Không có segment <c>/catalog</c>.</para>
/// <para>Mock đọc <c>Seeds/catalog/*.json</c>; Real gọi gRPC <c>CatalogApi</c>. Xoá trả <c>204 No Content</c> (không có body, không có envelope).</para>
/// <para>Quyền: đọc công khai; mọi thao tác ghi yêu cầu role <c>seller</c> hoặc <c>admin</c> (policy <c>seller</c> đã bao gồm admin).</para>
/// </remarks>
[ApiController]
[Route("api/v1")]
[BackendTag("catalog")]
[EnableRateLimiting("api")]
public sealed class ProductsController : ControllerBase
{
    private const string Service = "catalog";

    private readonly IGatewayDispatcher _dispatcher;
    private readonly CatalogApi.CatalogApiClient _client;

    public ProductsController(IGatewayDispatcher dispatcher, CatalogApi.CatalogApiClient client)
    {
        _dispatcher = dispatcher;
        _client = client;
    }

    // ---------------------------------------------------------------- list / search

    /// <summary>Xem danh sách sản phẩm của shop (CAT-06)</summary>
    /// <remarks>
    /// <para>Quyền: không bắt buộc đăng nhập (token tuỳ chọn; token sai/hết hạn vẫn trả 401).</para>
    /// <para>Query: <c>page</c>, <c>pageSize</c> (tối đa 100), <c>keyword</c>, <c>categoryId</c>, <c>status</c> (DRAFT|ACTIVE|INACTIVE), <c>sellerId</c>, <c>saleType</c> (REGULAR|AUCTION), <c>sortBy</c> (newest|priceAsc|priceDesc|rating), <c>sortDir</c> (asc|desc).</para>
    /// <para>Response: <c>data.items[]</c> (productId, name, brand, saleType, minPrice, maxPrice, primaryImageUrl, avgRating, status, auctionStatus, isOpen, isSold) + <c>data.pagination</c>.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: page/pageSize/sortBy/sortDir/saleType/status không hợp lệ</response>
    [HttpGet("products")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedData<ProductListItem>>), StatusCodes.Status200OK)]
    public Task<IResult> ListProducts(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] string? keyword,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? status,
        [FromQuery] Guid? sellerId,
        [FromQuery] string? saleType,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir)
    {
        CatalogValidation.EnsurePaged(pagination.Page, pagination.PageSize);
        CatalogValidation.EnsureOptionalStatus(status);
        CatalogValidation.EnsureOptionalSaleType(saleType);
        CatalogValidation.EnsureOptionalSortBy(sortBy);
        CatalogValidation.EnsureOptionalSortDir(sortDir);

        return _dispatcher.DispatchPagedAsync(
            HttpContext, Service, "products", pagination.Page, pagination.PageSize,
            o => _client.ListProductsAsync(new ListProductsRequest
            {
                Page = pagination.Page,
                PageSize = pagination.PageSize,
                Keyword = keyword ?? string.Empty,
                CategoryId = categoryId?.ToString() ?? string.Empty,
                Status = status ?? string.Empty,
                SellerId = sellerId?.ToString() ?? string.Empty,
                SaleType = saleType ?? string.Empty,
                SortBy = sortBy ?? string.Empty,
                SortDir = sortDir ?? string.Empty
            }, o),
            ProductListResponse.Parser);
    }

    /// <summary>Tìm kiếm, lọc sản phẩm của shop (CAT-06 + CAT-21)</summary>
    /// <remarks>
    /// <para>Quyền: Seller hoặc Admin (thiếu token 401, sai role 403).</para>
    /// <para>Query: <c>keyword</c>, <c>categoryId</c>, <c>minPrice</c>, <c>maxPrice</c>, <c>brand</c>, <c>saleType</c>, <c>attributes</c> (AttrName=AttrValue; nhiều cặp cách nhau dấu phẩy), <c>minRating</c> (0..5), <c>status</c>, <c>sortBy</c>, <c>page</c>, <c>pageSize</c>.</para>
    /// <para>Response: như danh sách sản phẩm (<c>data.items</c> + <c>data.pagination</c>).</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: minPrice &gt; maxPrice, minRating ngoài 0..5, attributes sai dạng, page/pageSize không hợp lệ</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN: người gọi không phải Seller hoặc Admin</response>
    [HttpGet("product/search")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<PagedData<ProductListItem>>), StatusCodes.Status200OK)]
    public Task<IResult> SearchProducts(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] string? keyword,
        [FromQuery] Guid? categoryId,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? brand,
        [FromQuery] string? saleType,
        [FromQuery] string? attributes,
        [FromQuery] decimal? minRating,
        [FromQuery] string? status,
        [FromQuery] string? sortBy)
    {
        CatalogValidation.EnsurePaged(pagination.Page, pagination.PageSize);
        CatalogValidation.EnsurePriceRange(minPrice, maxPrice);
        CatalogValidation.EnsureRatingRange(minRating);
        CatalogValidation.EnsureOptionalSaleType(saleType);
        CatalogValidation.EnsureOptionalStatus(status);
        CatalogValidation.EnsureOptionalSortBy(sortBy);
        EnsureAttributeFilter(attributes);

        var request = new SearchProductsRequest
        {
            Keyword = keyword ?? string.Empty,
            CategoryId = categoryId?.ToString() ?? string.Empty,
            Brand = brand ?? string.Empty,
            SaleType = saleType ?? string.Empty,
            Attributes = attributes ?? string.Empty,
            Status = status ?? string.Empty,
            SortBy = sortBy ?? string.Empty,
            Page = pagination.Page,
            PageSize = pagination.PageSize
        };
        if (minPrice.HasValue) request.MinPrice = (double)minPrice.Value;
        if (maxPrice.HasValue) request.MaxPrice = (double)maxPrice.Value;
        if (minRating.HasValue) request.MinRating = (double)minRating.Value;

        return _dispatcher.DispatchPagedAsync(
            HttpContext, Service, "product-search", pagination.Page, pagination.PageSize,
            o => _client.SearchProductsAsync(request, o),
            ProductListResponse.Parser);
    }

    // ---------------------------------------------------------------- detail / write

    /// <summary>Xem chi tiết một sản phẩm (CAT-05)</summary>
    /// <remarks>
    /// <para>Quyền: công khai. Query <c>auctionId</c> tuỳ chọn khi mở sản phẩm từ trang phiên đấu giá.</para>
    /// <para>Response: <c>data.product</c> (productId, sellerId, categoryId, name, description, brand, saleType, status, statusReason, specifications, publishedAt), <c>data.category</c>, <c>data.variants[]</c>, <c>data.images[]</c>, <c>data.rating</c>.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: productId hoặc auctionId sai UUID</response>
    /// <response code="404">PRODUCT_NOT_FOUND (Real)</response>
    [HttpGet("product/{productId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ProductDetailResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetProduct([FromRoute] string productId, [FromQuery] string? auctionId)
    {
        CatalogValidation.EnsureProductId(productId);
        if (!string.IsNullOrWhiteSpace(auctionId)) CatalogValidation.EnsureAuctionId(auctionId);

        var request = new GetProductRequest { ProductId = productId };
        if (!string.IsNullOrWhiteSpace(auctionId)) request.AuctionId = auctionId;

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "product",
            o => _client.GetProductAsync(request, o),
            ProductDetailResponse.Parser);
    }

    /// <summary>Tạo sản phẩm mới kèm biến thể đầu tiên (CAT-01)</summary>
    /// <remarks>
    /// <para>Quyền: Seller hoặc Admin. Danh tính người bán lấy từ JWT (<c>sub</c>), không nhận từ body.</para>
    /// <para>Body: categoryId, name, description, brand, specifications (object), variants[] (skuCode, name, price &gt; 0, currency, isActive, attributes[]). Đúng một variant phải có <c>isActive = true</c>.</para>
    /// <para>Response 201: <c>data.productId</c>, <c>data.status</c> = DRAFT, <c>data.saleType</c> = REGULAR, <c>data.variants[]</c> (variantId, skuCode).</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: name rỗng, không có variant, price &lt;= 0, không đúng 1 variant mặc định, attributes lặp attrName</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN / SELLER_NOT_ALLOWED (Real)</response>
    /// <response code="409">SKU_CODE_DUPLICATED (Real)</response>
    [HttpPost("products")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<ProductCreatedResponse>), StatusCodes.Status201Created)]
    public Task<IResult> CreateProduct([FromBody] CreateProductDto body)
    {
        CatalogValidation.EnsureCreateProduct(body);

        var request = new CreateProductRequest
        {
            CategoryId = body.CategoryId!.Value.ToString(),
            Name = body.Name!,
            Description = body.Description ?? string.Empty,
            Brand = body.Brand ?? string.Empty,
            Specifications = CatalogValidation.ParseSpecifications(body.Specifications),
            Caller = CatalogValidation.Caller(HttpContext)
        };
        foreach (var variant in body.Variants!)
        {
            request.Variants.Add(new CreateVariantItem
            {
                SkuCode = variant.SkuCode!,
                Name = variant.Name!,
                Price = (double)variant.Price!.Value,
                Currency = string.IsNullOrWhiteSpace(variant.Currency) ? ValidationHelpers.DefaultCurrency : variant.Currency,
                IsActive = variant.IsActive,
                Attributes = { CatalogValidation.ToAttributes(variant.Attributes) }
            });
        }

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "product-create",
            o => _client.CreateProductAsync(request, o),
            ProductCreatedResponse.Parser,
            successMessage: "Product created successfully.",
            successStatusCode: StatusCodes.Status201Created);
    }

    /// <summary>Cập nhật thông tin sản phẩm (CAT-02)</summary>
    /// <remarks>
    /// <para>Quyền: chủ sản phẩm hoặc Admin. Body: gửi ít nhất một trong categoryId, name, description, brand, specifications.</para>
    /// <para>Response: productId, categoryId, name, description, brand, saleType, status, specifications, updatedAt.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: body rỗng, name rỗng, productId sai UUID</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN (Real)</response>
    /// <response code="404">PRODUCT_NOT_FOUND (Real)</response>
    [HttpPatch("product/{productId}")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<ProductUpdatedResponse>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateProduct([FromRoute] string productId, [FromBody] UpdateProductDto body)
    {
        CatalogValidation.EnsureProductId(productId);
        CatalogValidation.EnsureUpdateProduct(body);

        var request = new UpdateProductRequest
        {
            ProductId = productId,
            CategoryId = body.CategoryId?.ToString() ?? string.Empty,
            Name = body.Name ?? string.Empty,
            Description = body.Description ?? string.Empty,
            Brand = body.Brand ?? string.Empty,
            Specifications = CatalogValidation.ParseSpecifications(body.Specifications),
            Caller = CatalogValidation.Caller(HttpContext)
        };

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "product-update",
            o => _client.UpdateProductAsync(request, o),
            ProductUpdatedResponse.Parser);
    }

    /// <summary>Xoá sản phẩm (CAT-04)</summary>
    /// <remarks>
    /// <para>Quyền: chủ sản phẩm hoặc Admin. Trả <c>204 No Content</c> — không có body, không có envelope.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: productId sai UUID</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN (Real)</response>
    /// <response code="404">PRODUCT_NOT_FOUND (Real)</response>
    /// <response code="409">PRODUCT_HAS_ORDER / PRODUCT_IN_AUCTION (Real)</response>
    [HttpDelete("product/{productId}")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IResult> DeleteProduct([FromRoute] string productId)
    {
        CatalogValidation.EnsureProductId(productId);

        return _dispatcher.DispatchNoContentAsync(
            HttpContext, Service, "product-delete",
            o => _client.DeleteProductAsync(new DeleteProductRequest
            {
                ProductId = productId,
                Caller = CatalogValidation.Caller(HttpContext)
            }, o),
            DeleteResponse.Parser);
    }

    // ---------------------------------------------------------------- images

    /// <summary>Thêm ảnh cho sản phẩm (CAT-12)</summary>
    /// <remarks>
    /// <para>Quyền: chủ sản phẩm hoặc Admin. Body <c>multipart/form-data</c>: <c>files</c> (jpg/png/webp), <c>variantId</c> (tuỳ chọn), <c>altText</c>, <c>isActive</c>.</para>
    /// <para>Gateway chỉ validate và chuyển metadata (tên file) xuống backend: phần byte của file không đi qua gRPC unary — upload thật thuộc object storage, xem specs/02.</para>
    /// <para>Response 201: <c>data.items[]</c> (imageId, url, isActive, sortOrder).</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: files rỗng, quá 5 file, variantId sai UUID</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN (Real)</response>
    /// <response code="404">PRODUCT_NOT_FOUND (Real)</response>
    /// <response code="409">IMAGE_LIMIT_EXCEEDED (Real)</response>
    [HttpPost("product/{productId}pics")]
    [Authorize(GatewayPolicies.Seller)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<ProductImageAddResponse>), StatusCodes.Status201Created)]
    public Task<IResult> AddProductImages([FromRoute] string productId, [FromForm] AddProductImagesForm form)
    {
        CatalogValidation.EnsureProductId(productId);
        CatalogValidation.EnsureImageFiles(form.Files);
        if (form.VariantId.HasValue) CatalogValidation.EnsureVariantId(form.VariantId.Value.ToString());

        var request = new AddProductImagesRequest
        {
            ProductId = productId,
            AltText = form.AltText ?? string.Empty,
            IsActive = form.IsActive,
            Caller = CatalogValidation.Caller(HttpContext)
        };
        if (form.VariantId.HasValue) request.VariantId = form.VariantId.Value.ToString();
        foreach (var file in form.Files!) request.FileNames.Add(file.FileName);

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "product-images-add",
            o => _client.AddProductImagesAsync(request, o),
            ProductImageAddResponse.Parser,
            successMessage: "Images uploaded successfully.",
            successStatusCode: StatusCodes.Status201Created);
    }

    /// <summary>Xem ảnh của sản phẩm (CAT-40)</summary>
    /// <remarks>
    /// <para>Quyền: công khai. Query <c>variantId</c> tuỳ chọn để lọc ảnh của một biến thể.</para>
    /// <para>Response: <c>data.items[]</c> (imageId, url, altText, isActive, sortOrder, variantId).</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: productId hoặc variantId sai UUID</response>
    /// <response code="404">PRODUCT_NOT_FOUND (Real)</response>
    [HttpGet("product/{productId}pics")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ProductImageListResponse>), StatusCodes.Status200OK)]
    public Task<IResult> ListProductImages([FromRoute] string productId, [FromQuery] string? variantId)
    {
        CatalogValidation.EnsureProductId(productId);
        if (!string.IsNullOrWhiteSpace(variantId)) CatalogValidation.EnsureVariantId(variantId);

        var request = new ListProductImagesRequest { ProductId = productId };
        if (!string.IsNullOrWhiteSpace(variantId)) request.VariantId = variantId;

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "product-images",
            o => _client.ListProductImagesAsync(request, o),
            ProductImageListResponse.Parser);
    }

    /// <summary>Xoá danh sách ảnh của sản phẩm (CAT-13)</summary>
    /// <remarks>
    /// <para>Quyền: chủ sản phẩm hoặc Admin. Query <c>ids</c> bắt buộc: danh sách imageId cách nhau dấu phẩy.</para>
    /// <para>Trả <c>204 No Content</c>.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: ids rỗng hoặc có phần tử sai UUID</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN (Real)</response>
    /// <response code="404">IMAGE_NOT_FOUND (Real)</response>
    [HttpDelete("product/{productId}pics")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IResult> DeleteProductImages([FromRoute] string productId, [FromQuery] string? ids)
    {
        CatalogValidation.EnsureProductId(productId);
        CatalogValidation.EnsureImageIds(ids);

        var request = new DeleteProductImagesRequest
        {
            ProductId = productId,
            Caller = CatalogValidation.Caller(HttpContext)
        };
        request.ImageIds.AddRange(CatalogValidation.ParseImageIds(ids!));

        return _dispatcher.DispatchNoContentAsync(
            HttpContext, Service, "product-images-delete",
            o => _client.DeleteProductImagesAsync(request, o),
            DeleteResponse.Parser);
    }

    // ---------------------------------------------------------------- rating

    /// <summary>Điểm đánh giá trung bình của sản phẩm (CAT-31)</summary>
    /// <remarks>
    /// <para>Quyền: công khai. Response: productId, avgRating (null khi chưa có đánh giá), reviewCount.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: productId sai UUID</response>
    /// <response code="404">PRODUCT_NOT_FOUND (Real)</response>
    [HttpGet("products/{productId}/totalRating")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ProductRatingSummaryResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetProductTotalRating([FromRoute] string productId)
    {
        CatalogValidation.EnsureProductId(productId);

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "product-rating-total",
            o => _client.GetProductTotalRatingAsync(new ProductIdRequest { ProductId = productId }, o),
            ProductRatingSummaryResponse.Parser);
    }

    /// <summary>Điểm trung bình và danh sách đánh giá của sản phẩm (CAT-30 + CAT-31)</summary>
    /// <remarks>
    /// <para>Quyền: công khai. Query: <c>page</c>, <c>pageSize</c>, <c>rating</c> (1..5), <c>sortBy</c> (newest|ratingDesc|ratingAsc).</para>
    /// <para>Response: <c>data.rating</c>, <c>data.items[]</c>, <c>data.page</c>, <c>data.pageSize</c>, <c>data.total</c> — giữ nguyên page/size trong data vì response còn field anh em <c>rating</c>.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: page/pageSize/rating/sortBy không hợp lệ</response>
    /// <response code="404">PRODUCT_NOT_FOUND (Real)</response>
    [HttpGet("products/{productId}/rating")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ProductRatingResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetProductRating(
        [FromRoute] string productId,
        [FromQuery] PaginationRequest pagination,
        [FromQuery] int? rating,
        [FromQuery] string? sortBy)
    {
        CatalogValidation.EnsureProductId(productId);
        CatalogValidation.EnsurePaged(pagination.Page, pagination.PageSize);
        if (rating is < 1 or > 5)
            throw new GatewayInputException("rating must be between 1 and 5.", "rating", CatalogValidation.ErrorCode);
        CatalogValidation.EnsureOptionalSortBy(sortBy, CatalogValidation.RatingSortByValues);

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "product-rating",
            o => _client.GetProductRatingAsync(new GetProductRatingRequest
            {
                ProductId = productId,
                Page = pagination.Page,
                PageSize = pagination.PageSize,
                Rating = rating ?? 0,
                SortBy = sortBy ?? string.Empty
            }, o),
            ProductRatingResponse.Parser);
    }

    // ---------------------------------------------------------------- status

    /// <summary>Đổi trạng thái sản phẩm (CAT-03)</summary>
    /// <remarks>
    /// <para>Quyền: chủ sản phẩm. Body: <c>status</c> (DRAFT|ACTIVE|INACTIVE) và <c>statusReason</c> (bắt buộc khi INACTIVE, các trạng thái khác phải để trống).</para>
    /// <para>Response: productId, status, statusReason, publishedAt.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: status không hợp lệ, thiếu statusReason khi INACTIVE, hoặc gửi statusReason khi không INACTIVE</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN (Real)</response>
    /// <response code="404">PRODUCT_NOT_FOUND (Real)</response>
    /// <response code="409">PRODUCT_NOT_PUBLISHABLE / PRODUCT_IN_AUCTION (Real)</response>
    [HttpPatch("product/{productId}/status")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<ProductStatusResponse>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateProductStatus([FromRoute] string productId, [FromBody] UpdateProductStatusDto body)
    {
        CatalogValidation.EnsureProductId(productId);
        CatalogValidation.EnsureProductStatus(body);

        var request = new UpdateProductStatusRequest
        {
            ProductId = productId,
            Status = body.Status!.ToUpperInvariant(),
            Caller = CatalogValidation.Caller(HttpContext)
        };
        if (!string.IsNullOrWhiteSpace(body.StatusReason)) request.StatusReason = body.StatusReason.ToUpperInvariant();

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "product-status",
            o => _client.UpdateProductStatusAsync(request, o),
            ProductStatusResponse.Parser);
    }

    // ---------------------------------------------------------------- variants

    /// <summary>Thêm biến thể cho sản phẩm (CAT-07)</summary>
    /// <remarks>
    /// <para>Quyền: chủ sản phẩm hoặc Admin. Body: skuCode, name, price &gt; 0, currency, attributes[].</para>
    /// <para>Response 201: <c>data.variantId</c>, <c>data.skuCode</c>, <c>data.status</c>.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: price &lt;= 0, attributes lặp attrName, productId sai UUID</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN (Real)</response>
    /// <response code="404">PRODUCT_NOT_FOUND (Real)</response>
    /// <response code="409">SKU_CODE_DUPLICATED / AUCTION_PRODUCT_SINGLE_VARIANT (Real)</response>
    [HttpPost("product/{productId}/variants")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<VariantCreatedResponse>), StatusCodes.Status201Created)]
    public Task<IResult> CreateVariant([FromRoute] string productId, [FromBody] CreateVariantDto body)
    {
        CatalogValidation.EnsureCreateVariant(productId, body.SkuCode, body.Name, body.Price, body.Attributes);

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "variant-create",
            o => _client.CreateVariantAsync(new CreateVariantRequest
            {
                ProductId = productId,
                SkuCode = body.SkuCode!,
                Name = body.Name!,
                Price = (double)body.Price!.Value,
                Currency = string.IsNullOrWhiteSpace(body.Currency) ? ValidationHelpers.DefaultCurrency : body.Currency,
                Attributes = { CatalogValidation.ToAttributes(body.Attributes) },
                Caller = CatalogValidation.Caller(HttpContext)
            }, o),
            VariantCreatedResponse.Parser,
            successMessage: "Variant created successfully.",
            successStatusCode: StatusCodes.Status201Created);
    }

    /// <summary>Xoá một biến thể (CAT-11)</summary>
    /// <remarks>
    /// <para>Quyền: chủ sản phẩm hoặc Admin. Trả <c>204 No Content</c>.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: variantId sai UUID</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN (Real)</response>
    /// <response code="404">VARIANT_NOT_FOUND (Real)</response>
    /// <response code="409">LAST_VARIANT / VARIANT_HAS_ORDER (Real)</response>
    [HttpDelete("variant/{variantId}")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IResult> DeleteVariant([FromRoute] string variantId)
    {
        CatalogValidation.EnsureVariantId(variantId);

        return _dispatcher.DispatchNoContentAsync(
            HttpContext, Service, "variant-delete",
            o => _client.DeleteVariantAsync(new DeleteVariantRequest
            {
                VariantId = variantId,
                Caller = CatalogValidation.Caller(HttpContext)
            }, o),
            DeleteResponse.Parser);
    }

    /// <summary>Cập nhật biến thể (CAT-08)</summary>
    /// <remarks>
    /// <para>Quyền: chủ sản phẩm hoặc Admin. Body: gửi ít nhất một trong name, price, currency, attributes (price phải &gt; 0 với sản phẩm REGULAR).</para>
    /// <para>Response: variantId, productId, skuCode, name, price, currency, status, attributes[], updatedAt.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: body rỗng, price &lt;= 0, attributes lặp attrName</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN (Real)</response>
    /// <response code="404">VARIANT_NOT_FOUND (Real)</response>
    [HttpPatch("variant/{variantId}")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<VariantUpdatedResponse>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateVariant([FromRoute] string variantId, [FromBody] UpdateVariantDto body)
    {
        CatalogValidation.EnsureUpdateVariant(variantId, body.Name, body.Price, body.Attributes);

        var request = new UpdateVariantRequest
        {
            VariantId = variantId,
            Name = body.Name ?? string.Empty,
            Currency = string.IsNullOrWhiteSpace(body.Currency) ? ValidationHelpers.DefaultCurrency : body.Currency,
            Attributes = { CatalogValidation.ToAttributes(body.Attributes) },
            Caller = CatalogValidation.Caller(HttpContext)
        };
        if (body.Price.HasValue) request.Price = (double)body.Price.Value;

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "variant-update",
            o => _client.UpdateVariantAsync(request, o),
            VariantUpdatedResponse.Parser);
    }

    /// <summary>Xem chi tiết một biến thể (CAT-42)</summary>
    /// <remarks>
    /// <para>Quyền: chủ sản phẩm hoặc Admin.</para>
    /// <para>Response: variantId, productId, skuCode, name, price, currency, status, statusReason, isDefault, hasOrder, attributes[], images[].</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: variantId sai UUID</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN (Real)</response>
    /// <response code="404">VARIANT_NOT_FOUND (Real)</response>
    [HttpGet("variant/{variantId}")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<VariantDetailResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetVariant([FromRoute] string variantId)
    {
        CatalogValidation.EnsureVariantId(variantId);

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "variant",
            o => _client.GetVariantAsync(new VariantIdRequest { VariantId = variantId }, o),
            VariantDetailResponse.Parser);
    }

    // ---------------------------------------------------------------- auction products

    /// <summary>Tạo sản phẩm đấu giá (CAT-43)</summary>
    /// <remarks>
    /// <para>Quyền: Seller (tài khoản đã được duyệt bán đấu giá — kiểm ở backend). Danh tính người bán lấy từ JWT.</para>
    /// <para>Body: categoryId, name, description, brand, specifications, variant {skuCode, name, attributes[]} — <c>variant</c> là object đơn và <b>không</b> gửi price.</para>
    /// <para>Response 201: productId, status = DRAFT, saleType = AUCTION, variant {variantId, skuCode}.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: name rỗng, thiếu variant, attributes lặp attrName</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">SELLER_NOT_ALLOWED (Real)</response>
    /// <response code="409">SKU_CODE_DUPLICATED (Real)</response>
    [HttpPost("product/auction")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<AuctionProductCreatedResponse>), StatusCodes.Status201Created)]
    public Task<IResult> CreateAuctionProduct([FromBody] CreateAuctionProductDto body)
    {
        CatalogValidation.EnsureCreateAuctionProduct(body);

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "auction-product-create",
            o => _client.CreateAuctionProductAsync(new CreateAuctionProductRequest
            {
                CategoryId = body.CategoryId!.Value.ToString(),
                Name = body.Name!,
                Description = body.Description ?? string.Empty,
                Brand = body.Brand ?? string.Empty,
                Specifications = CatalogValidation.ParseSpecifications(body.Specifications),
                Variant = new CreateAuctionVariantItem
                {
                    SkuCode = body.Variant!.SkuCode!,
                    Name = body.Variant.Name!,
                    Attributes = { CatalogValidation.ToAttributes(body.Variant.Attributes) }
                },
                Caller = CatalogValidation.Caller(HttpContext)
            }, o),
            AuctionProductCreatedResponse.Parser,
            successMessage: "Auction product created successfully.",
            successStatusCode: StatusCodes.Status201Created);
    }

    /// <summary>Tìm kiếm sản phẩm đấu giá của shop (CAT-06 + CAT-21)</summary>
    /// <remarks>
    /// <para>Quyền: Seller hoặc Admin. Route theo hợp đồng là <c>/product/auction/search</c> (hợp đồng ghi <c>aucction</c> — lỗi chính tả đã sửa).</para>
    /// <para>Query: keyword, categoryId, status, brand, sortBy, sortDir, page, pageSize.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: page/pageSize/sortBy/sortDir không hợp lệ</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN: không phải Seller hoặc Admin</response>
    [HttpGet("product/auction/search")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<PagedData<ProductListItem>>), StatusCodes.Status200OK)]
    public Task<IResult> SearchAuctionProducts(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] string? keyword,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? status,
        [FromQuery] string? brand,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir)
    {
        CatalogValidation.EnsurePaged(pagination.Page, pagination.PageSize);
        CatalogValidation.EnsureOptionalAuctionStatus(status);
        CatalogValidation.EnsureOptionalSortBy(sortBy);
        CatalogValidation.EnsureOptionalSortDir(sortDir);

        return _dispatcher.DispatchPagedAsync(
            HttpContext, Service, "auction-product-search", pagination.Page, pagination.PageSize,
            o => _client.SearchAuctionProductsAsync(new SearchAuctionProductsRequest
            {
                Keyword = keyword ?? string.Empty,
                CategoryId = categoryId?.ToString() ?? string.Empty,
                Status = status ?? string.Empty,
                Brand = brand ?? string.Empty,
                SortBy = sortBy ?? string.Empty,
                SortDir = sortDir ?? string.Empty,
                Page = pagination.Page,
                PageSize = pagination.PageSize
            }, o),
            ProductListResponse.Parser);
    }

    /// <summary>Danh sách sản phẩm đấu giá của shop (CAT-06)</summary>
    /// <remarks>
    /// <para>Quyền: Seller hoặc Admin. Query: status, page, pageSize.</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: page/pageSize/status không hợp lệ</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN: không phải Seller hoặc Admin</response>
    [HttpGet("product/auctions")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<PagedData<ProductListItem>>), StatusCodes.Status200OK)]
    public Task<IResult> ListAuctionProducts(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] string? status)
    {
        CatalogValidation.EnsurePaged(pagination.Page, pagination.PageSize);
        CatalogValidation.EnsureOptionalAuctionStatus(status);

        return _dispatcher.DispatchPagedAsync(
            HttpContext, Service, "auction-products", pagination.Page, pagination.PageSize,
            o => _client.ListAuctionProductsAsync(new ListAuctionProductsRequest
            {
                Status = status ?? string.Empty,
                Page = pagination.Page,
                PageSize = pagination.PageSize
            }, o),
            ProductListResponse.Parser);
    }

    /// <summary>Chi tiết sản phẩm đấu giá theo phiên (AUC-35 + CAT-05)</summary>
    /// <remarks>
    /// <para>Quyền: Seller của phiên hoặc Admin.</para>
    /// <para>Response: <c>data.auction</c> (auctionId, status, startingPriceAmount, bidStepAmount, currentPriceAmount, currentLeadingBidderMasked, bidCount, startsAt, endsAt, extensionCount) + <c>data.product</c> (như chi tiết sản phẩm).</para>
    /// </remarks>
    /// <response code="400">VAL_INVALID_INPUT: auctionId sai UUID</response>
    /// <response code="401">UNAUTHORIZED</response>
    /// <response code="403">FORBIDDEN (Real)</response>
    /// <response code="404">AUCTION_NOT_FOUND / PRODUCT_NOT_FOUND (Real)</response>
    [HttpGet("product/auction/{auctionId}")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<AuctionProductDetailResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetAuctionProduct([FromRoute] string auctionId)
    {
        CatalogValidation.EnsureAuctionId(auctionId);

        return _dispatcher.DispatchAsync(
            HttpContext, Service, "auction-product",
            o => _client.GetAuctionProductAsync(new GetAuctionProductRequest { AuctionId = auctionId }, o),
            AuctionProductDetailResponse.Parser);
    }

    private static void EnsureAttributeFilter(string? attributes)
    {
        if (string.IsNullOrWhiteSpace(attributes)) return;

        foreach (var pair in attributes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                throw new GatewayInputException("attributes must use the AttrName=AttrValue form.", "attributes", CatalogValidation.ErrorCode);
        }
    }
}
