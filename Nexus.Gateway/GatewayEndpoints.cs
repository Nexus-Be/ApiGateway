using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using ShoppingCart.Contracts.V1;
using Nexus.Gateway.Services;
using Nexus.Gateway.MockEngine;

namespace Nexus.Gateway;

public sealed record CheckoutLine(string ProductId, int Quantity);
public sealed record CheckoutBody(string? CartId, IReadOnlyList<CheckoutLine>? Items);
public sealed record AddCartItemBody(string ProductId, int Quantity);
public sealed record UpdateCartItemBody(int Quantity);
public sealed record RegisterBody(string Username, string Email, string Password, string? DisplayName);
public sealed record CreateUserBody(string Username, string Email, string Password, string? DisplayName, string[]? Roles);
public sealed record LoginBody(string Username, string Password);
public sealed record UpdateProfileBody(string? DisplayName, string? Email);
public sealed record CategoryBody(string Name, string? ParentId);
public sealed record ProductBody(string Name, string? Description, string? CategoryId, string Price,
    string? Currency, int Available, string? Slug);
public sealed record AdjustInventoryBody(int Available, string? Reason);
public sealed record AssignRolesBody(string[] Roles);
public sealed record ApproveSellerBody(bool Approve, string? Reason);
public sealed record AuditEventTypeToggleBody(bool Enabled);
public sealed record CreateAuctionBody(string ProductId, string StartingPrice, string StepPrice, string StartTime, string EndTime, string Title);
public sealed record PlaceBidBody(string Amount);
public sealed record ShippingQuoteBody(string SellerAddress, string BuyerAddress, int WeightGrams);
public sealed record UpdatePreferencesBody(bool EmailEnabled, bool PushEnabled, bool OrderUpdates, bool AuctionAlerts);
public sealed record SellerUpgradeBody(string ShopName, string? Description, string? ContactEmail,
    string? ContactPhone, string? Address);
public sealed record WalletDepositBody(string Amount, string? Currency, string? PaymentMethod);
public sealed record WalletWithdrawBody(string Amount, string? Currency, string BankAccount);
public sealed record WalletTransferBody(string RecipientUserId, string Amount, string? Note);

public static class GatewayEndpoints
{
    private const string DefaultCurrency = "VND";
    private const string PageMessage = "page >= 1 và pageSize trong khoảng 1..100";
    private static readonly string[] KnownRoles = ["buyer", "seller", "staff", "admin"];
    private const int MaxCheckoutLines = 50;
    private const int MaxCheckoutQuantity = 1000;

    public static void Map(WebApplication app)
    {
        MapGatewayModes(app);
        MapMockEngine(app);
        MapAuth(app);
        MapUsers(app);
        MapCatalog(app);
        MapSellerCatalog(app);
        MapCommerce(app);
        MapAdmin(app);
        MapAudit(app);
        MapAuction(app);
        MapFulfillment(app);
        MapNotification(app);
        MapWallet(app);
    }

    // ---------- auth (public) ----------
    private static void MapAuth(WebApplication app)
    {
        var auth = app.MapGroup("/api/v1/auth").WithTags("Auth").WithMetadata(new BackendTag("users")).RequireRateLimiting("api");

        auth.MapPost("/register", (RegisterBody body, HttpContext c, BackendDispatcher d, UsersApi.UsersApiClient client) =>
        {
            if (string.IsNullOrWhiteSpace(body.Username) || !body.Email.Contains('@') || (body.Password?.Length ?? 0) < 8)
                return Bad(c, "username, email hợp lệ và password >= 8 ký tự là bắt buộc");

            return d.Execute(c, "users", "register", o => client.RegisterAsync(new RegisterRequest
            {
                Username = body.Username,
                Email = body.Email,
                Password = body.Password,
                DisplayName = body.DisplayName ?? body.Username
            }, o), UserProfile.Parser, successMessage: "User registered successfully.");
        }).Produces<ApiResponse<UserProfile>>().AllowAnonymous();

        auth.MapPost("/login", (LoginBody body, HttpContext c, BackendDispatcher d, UsersApi.UsersApiClient client) =>
        {
            c.Response.Headers.CacheControl = "no-store";
            if (string.IsNullOrWhiteSpace(body.Username) || string.IsNullOrEmpty(body.Password))
                return Bad(c, "username và password là bắt buộc");

            return d.Execute(c, "users", "login", o => client.LoginAsync(new LoginRequest
            {
                Username = body.Username,
                Password = body.Password
            }, o), LoginResponse.Parser, successMessage: "Login successful.");
        }).Produces<ApiResponse<LoginResponse>>().AllowAnonymous();
    }

    // ---------- users (đã đăng nhập) ----------
    private static void MapUsers(WebApplication app)
    {
        var me = app.MapGroup("/api/v1/users/me").WithTags("Users").WithMetadata(new BackendTag("users"))
            .RequireRateLimiting("api").RequireAuthorization("authenticated");

        me.MapGet("", (HttpContext c, BackendDispatcher d, UsersApi.UsersApiClient client) =>
            d.Execute(c, "users", "profile", o => client.GetProfileAsync(UserOf(c), o), UserProfile.Parser, successMessage: "User retrieved successfully."))
            .Produces<ApiResponse<UserProfile>>();

        me.MapPut("", (UpdateProfileBody body, HttpContext c, BackendDispatcher d, UsersApi.UsersApiClient client) =>
        {
            if (body.DisplayName is null && body.Email is null) return Bad(c, "displayName hoặc email phải có");
            if (body.Email is not null && !body.Email.Contains('@')) return Bad(c, "email không hợp lệ", field: "Email");

            var user = UserOf(c);
            return d.Execute(c, "users", "profile-update", o => client.UpdateProfileAsync(new UpdateProfileRequest
            {
                UserId = user.UserId,
                DisplayName = body.DisplayName ?? "",
                Email = body.Email ?? ""
            }, o), UserProfile.Parser, successMessage: "Profile updated successfully.");
        }).Produces<ApiResponse<UserProfile>>();

        me.MapPost("/seller-upgrade", (SellerUpgradeBody body, HttpContext c, BackendDispatcher d, UsersApi.UsersApiClient client) =>
        {
            if (string.IsNullOrWhiteSpace(body.ShopName)) return Bad(c, "shopName là bắt buộc", field: "ShopName");
            if (!string.IsNullOrWhiteSpace(body.ContactEmail) && !body.ContactEmail.Contains('@'))
                return Bad(c, "contactEmail không hợp lệ", field: "ContactEmail");

            var user = UserOf(c);
            return d.Execute(c, "users", "seller-upgrade", o => client.RequestSellerUpgradeAsync(new SellerUpgradeRequest
            {
                UserId = user.UserId,
                ShopName = body.ShopName,
                Description = body.Description ?? "",
                ContactEmail = body.ContactEmail ?? "",
                ContactPhone = body.ContactPhone ?? "",
                Address = body.Address ?? ""
            }, o), SellerRegistrationInfo.Parser, successMessage: "Seller upgrade requested successfully.");
        }).Produces<ApiResponse<SellerRegistrationInfo>>();
    }

    // ---------- catalog: đọc công khai + quản category (admin) ----------
    private static void MapCatalog(WebApplication app)
    {
        var catalog = app.MapGroup("/api/v1/catalog").WithTags("Catalog").WithMetadata(new BackendTag("catalog")).RequireRateLimiting("api");

        catalog.MapGet("/categories", (HttpContext c, BackendDispatcher d, CatalogApi.CatalogApiClient client, int page = 1, int pageSize = 20) =>
            PageOk(page, pageSize)
                ? d.ExecutePaged(c, "catalog", "categories", page, pageSize, o => client.ListCategoriesAsync(new PageRequest { Page = page, PageSize = pageSize }, o), CategoryPage.Parser)
                : Bad(c, PageMessage, field: "Pagination")).Produces<ApiResponse<PagedData<Category>>>().AllowAnonymous();

        catalog.MapGet("/products", (HttpContext c, BackendDispatcher d, CatalogApi.CatalogApiClient client,
            string? categoryId = null, string? sellerId = null, string? q = null, int page = 1, int pageSize = 20) =>
        {
            if (!PageOk(page, pageSize)) return Bad(c, PageMessage, field: "Pagination");
            if (!GuidOk(categoryId) || !GuidOk(sellerId)) return Bad(c, "categoryId/sellerId phải là UUID");

            return d.ExecutePaged(c, "catalog", "products", page, pageSize, o => client.ListProductsAsync(new ProductQuery
            {
                Page = page,
                PageSize = pageSize,
                CategoryId = categoryId ?? "",
                SellerId = sellerId ?? "",
                Q = q ?? ""
            }, o), ProductPage.Parser);
        }).Produces<ApiResponse<PagedData<Product>>>().AllowAnonymous();

        catalog.MapGet("/products/{id:guid}", (Guid id, HttpContext c, BackendDispatcher d, CatalogApi.CatalogApiClient client) =>
            d.Execute(c, "catalog", "product", o => client.GetProductAsync(new IdRequest { Id = id.ToString() }, o), Product.Parser, successMessage: "Product retrieved successfully."))
            .Produces<ApiResponse<Product>>().AllowAnonymous();

        var admin = catalog.MapGroup("").RequireAuthorization("admin");

        admin.MapPost("/categories", (CategoryBody body, HttpContext c, BackendDispatcher d, CatalogApi.CatalogApiClient client) =>
        {
            if (string.IsNullOrWhiteSpace(body.Name)) return Bad(c, "name là bắt buộc", field: "Name");
            if (!GuidOk(body.ParentId)) return Bad(c, "parentId phải là UUID", field: "ParentId");

            return d.Execute(c, "catalog", "category-create", o => client.CreateCategoryAsync(new CategoryInput
            {
                Name = body.Name,
                ParentId = body.ParentId ?? ""
            }, o), Category.Parser, successMessage: "Category created successfully.");
        }).Produces<ApiResponse<Category>>();

        admin.MapPut("/categories/{id:guid}", (Guid id, CategoryBody body, HttpContext c, BackendDispatcher d, CatalogApi.CatalogApiClient client) =>
        {
            if (string.IsNullOrWhiteSpace(body.Name)) return Bad(c, "name là bắt buộc", field: "Name");
            if (!GuidOk(body.ParentId)) return Bad(c, "parentId phải là UUID", field: "ParentId");

            return d.Execute(c, "catalog", "category-update", o => client.UpdateCategoryAsync(new UpdateCategoryRequest
            {
                Id = id.ToString(),
                Name = body.Name,
                ParentId = body.ParentId ?? ""
            }, o), Category.Parser, successMessage: "Category updated successfully.");
        }).Produces<ApiResponse<Category>>();

        admin.MapDelete("/categories/{id:guid}", (Guid id, HttpContext c, BackendDispatcher d, CatalogApi.CatalogApiClient client) =>
            d.Execute(c, "catalog", "category-delete", o => client.DeleteCategoryAsync(new IdRequest { Id = id.ToString() }, o), DeleteResponse.Parser, successMessage: "Category deleted successfully."))
            .Produces<ApiResponse<DeleteResponse>>();
    }

    // ---------- catalog: phần của seller (sản phẩm + kho ảo) ----------
    private static void MapSellerCatalog(WebApplication app)
    {
        var seller = app.MapGroup("/api/v1/seller").WithTags("Seller").WithMetadata(new BackendTag("catalog"))
            .RequireRateLimiting("api").RequireAuthorization("seller");

        seller.MapGet("/products", (HttpContext c, BackendDispatcher d, CatalogApi.CatalogApiClient client, int page = 1, int pageSize = 20) =>
        {
            if (!PageOk(page, pageSize)) return Bad(c, PageMessage, field: "Pagination");
            return d.ExecutePaged(c, "catalog", "seller-products", page, pageSize, o => client.ListProductsBySellerAsync(new ProductQuery
            {
                Page = page,
                PageSize = pageSize,
                SellerId = UserOf(c).UserId
            }, o), ProductPage.Parser);
        }).Produces<ApiResponse<PagedData<Product>>>();

        seller.MapPost("/products", (ProductBody body, HttpContext c, BackendDispatcher d, CatalogApi.CatalogApiClient client) =>
        {
            if (!ProductValid(body, out var price, out var error)) return Bad(c, error);

            return d.Execute(c, "catalog", "product-create", o => client.CreateProductAsync(new ProductInput
            {
                SellerId = UserOf(c).UserId,
                Name = body.Name,
                Description = body.Description ?? "",
                CategoryId = body.CategoryId ?? "",
                Price = price,
                Currency = body.Currency ?? DefaultCurrency,
                Available = body.Available,
                Slug = body.Slug ?? ""
            }, o), Product.Parser, successMessage: "Product created successfully.");
        }).Produces<ApiResponse<Product>>();

        seller.MapPut("/products/{id:guid}", (Guid id, ProductBody body, HttpContext c, BackendDispatcher d, CatalogApi.CatalogApiClient client) =>
        {
            if (!ProductValid(body, out var price, out var error)) return Bad(c, error);
            if (body.Available != 0)
                return Bad(c, "available không đổi qua cập nhật sản phẩm — dùng PUT /api/v1/seller/inventory/{productId}", field: "Available");

            return d.Execute(c, "catalog", "product-update", o => client.UpdateProductAsync(new UpdateProductRequest
            {
                Id = id.ToString(),
                SellerId = UserOf(c).UserId,
                Name = body.Name,
                Description = body.Description ?? "",
                CategoryId = body.CategoryId ?? "",
                Price = price,
                Currency = body.Currency ?? DefaultCurrency,
                Available = body.Available,
                Slug = body.Slug ?? ""
            }, o), Product.Parser, successMessage: "Product updated successfully.");
        }).Produces<ApiResponse<Product>>();

        seller.MapDelete("/products/{id:guid}", (Guid id, HttpContext c, BackendDispatcher d, CatalogApi.CatalogApiClient client) =>
            d.Execute(c, "catalog", "product-delete", o => client.DeleteProductAsync(new IdRequest { Id = id.ToString() }, o), DeleteResponse.Parser, successMessage: "Product deleted successfully."))
            .Produces<ApiResponse<DeleteResponse>>();

        seller.MapGet("/inventory", (HttpContext c, BackendDispatcher d, CatalogApi.CatalogApiClient client) =>
            d.Execute(c, "catalog", "inventory", o => client.GetInventoryAsync(UserOf(c), o), InventoryPage.Parser, successMessage: "Inventory retrieved successfully."))
            .Produces<ApiResponse<InventoryPage>>();

        seller.MapPut("/inventory/{productId:guid}", (Guid productId, AdjustInventoryBody body, HttpContext c, BackendDispatcher d, CatalogApi.CatalogApiClient client) =>
        {
            if (body.Available < 0) return Bad(c, "available phải >= 0", field: "Available");

            return d.Execute(c, "catalog", "inventory-adjust", o => client.AdjustInventoryAsync(new AdjustInventoryRequest
            {
                ProductId = productId.ToString(),
                SellerId = UserOf(c).UserId,
                Available = body.Available,
                Reason = body.Reason ?? ""
            }, o), Inventory.Parser, successMessage: "Inventory adjusted successfully.");
        }).Produces<ApiResponse<Inventory>>();
    }

    // ---------- commerce: giỏ + đơn ----------
    private static void MapCommerce(WebApplication app)
    {
        var cart = app.MapGroup("/api/v1/commerce/cart").WithTags("Commerce").WithMetadata(new BackendTag("commerce"))
            .RequireRateLimiting("api").RequireAuthorization("authenticated");

        cart.MapGet("", (HttpContext c, BackendDispatcher d, CommerceApi.CommerceApiClient client) =>
            d.Execute(c, "commerce", "cart", o => client.GetCartAsync(UserOf(c), o), Cart.Parser, successMessage: "Cart retrieved successfully."))
            .Produces<ApiResponse<Cart>>();

        cart.MapPost("/items", (AddCartItemBody body, HttpContext c, BackendDispatcher d, CommerceApi.CommerceApiClient client) =>
        {
            if (!Guid.TryParse(body.ProductId, out _)) return Bad(c, "productId phải là UUID", field: "ProductId");
            if (body.Quantity < 1) return Bad(c, "quantity phải >= 1", field: "Quantity");

            return d.Execute(c, "commerce", "cart-item-add", o => client.AddCartItemAsync(new CartItemRequest
            {
                UserId = UserOf(c).UserId,
                ProductId = body.ProductId,
                Quantity = body.Quantity
            }, o), Cart.Parser, successMessage: "Item added to cart.");
        }).Produces<ApiResponse<Cart>>();

        cart.MapPatch("/items/{productId:guid}", (Guid productId, UpdateCartItemBody body, HttpContext c, BackendDispatcher d, CommerceApi.CommerceApiClient client) =>
        {
            if (body.Quantity < 1) return Bad(c, "quantity phải >= 1", field: "Quantity");

            return d.Execute(c, "commerce", "cart-item-update", o => client.UpdateCartItemAsync(new UpdateCartItemRequest
            {
                UserId = UserOf(c).UserId,
                ProductId = productId.ToString(),
                Quantity = body.Quantity
            }, o), Cart.Parser, successMessage: "Cart item updated.");
        }).Produces<ApiResponse<Cart>>();

        cart.MapDelete("/items/{productId:guid}", (Guid productId, HttpContext c, BackendDispatcher d, CommerceApi.CommerceApiClient client) =>
            d.Execute(c, "commerce", "cart-item-remove", o => client.RemoveCartItemAsync(new RemoveCartItemRequest
            {
                UserId = UserOf(c).UserId,
                ProductId = productId.ToString()
            }, o), Cart.Parser, successMessage: "Item removed from cart."))
            .Produces<ApiResponse<Cart>>();

        cart.MapDelete("", (HttpContext c, BackendDispatcher d, CommerceApi.CommerceApiClient client) =>
            d.Execute(c, "commerce", "cart-clear", o => client.ClearCartAsync(UserOf(c), o), Cart.Parser, successMessage: "Cart cleared successfully."))
            .Produces<ApiResponse<Cart>>();

        var orders = app.MapGroup("/api/v1/commerce/orders").WithTags("Commerce").WithMetadata(new BackendTag("commerce"))
            .RequireRateLimiting("api").RequireAuthorization("authenticated");

        orders.MapGet("", (HttpContext c, BackendDispatcher d, CommerceApi.CommerceApiClient client, int page = 1, int pageSize = 20) =>
        {
            if (!PageOk(page, pageSize)) return Bad(c, PageMessage, field: "Pagination");

            return d.ExecutePaged(c, "commerce", "orders", page, pageSize, o => client.ListOrdersAsync(new MyOrdersRequest
            {
                UserId = UserOf(c).UserId,
                Page = page,
                PageSize = pageSize
            }, o), OrderPage.Parser);
        }).Produces<ApiResponse<PagedData<Order>>>();

        orders.MapGet("/{id:guid}", (Guid id, HttpContext c, BackendDispatcher d, CommerceApi.CommerceApiClient client) =>
            d.Execute(c, "commerce", "order", o => client.GetOrderAsync(new OrderRequest
            {
                OrderId = id.ToString(),
                UserId = UserOf(c).UserId,
                Role = RoleOf(c)
            }, o), Order.Parser, successMessage: "Order retrieved successfully."))
            .Produces<ApiResponse<Order>>();

        orders.MapPost("", (CheckoutBody body, HttpContext c, BackendDispatcher d, CommerceApi.CommerceApiClient client) =>
        {
            var key = c.Request.Headers["Idempotency-Key"].ToString();
            if (!Guid.TryParse(key, out _)) return Bad(c, "Idempotency-Key phải là UUID", field: "Idempotency-Key");

            var lines = body.Items ?? Array.Empty<CheckoutLine>();
            if (lines.Count == 0)
            {
                if (!Guid.TryParse(body.CartId, out _))
                    return Bad(c, "cần 'items' (mua ngay) hoặc 'cartId' (checkout cả giỏ)");
            }
            else
            {
                if (lines.Count > MaxCheckoutLines) return Bad(c, $"tối đa {MaxCheckoutLines} dòng hàng mỗi đơn");
                foreach (var line in lines)
                {
                    if (!Guid.TryParse(line.ProductId, out _)) return Bad(c, "productId phải là UUID", field: "ProductId");
                    if (line.Quantity < 1 || line.Quantity > MaxCheckoutQuantity)
                        return Bad(c, $"quantity phải trong khoảng 1..{MaxCheckoutQuantity}", field: "Quantity");
                }
            }

            var request = new CreateOrderRequest
            {
                UserId = UserOf(c).UserId,
                CartId = body.CartId ?? string.Empty,
                IdempotencyKey = key,
            };
            request.Items.AddRange(lines.Select(l => new CreateOrderLine { ProductId = l.ProductId, Quantity = l.Quantity }));

            return d.Execute(c, "commerce", "order-created", o => client.CreateOrderAsync(request, o), Order.Parser, successMessage: "Order created successfully.", successStatusCode: StatusCodes.Status201Created);
        }).Produces<ApiResponse<Order>>();

        var sellerOrders = app.MapGroup("/api/v1/seller/orders").WithTags("Seller").WithMetadata(new BackendTag("commerce"))
            .RequireRateLimiting("api").RequireAuthorization("seller");

        sellerOrders.MapGet("", (HttpContext c, BackendDispatcher d, CommerceApi.CommerceApiClient client, int page = 1, int pageSize = 20) =>
        {
            if (!PageOk(page, pageSize)) return Bad(c, PageMessage, field: "Pagination");

            return d.ExecutePaged(c, "commerce", "seller-orders", page, pageSize, o => client.ListOrdersBySellerAsync(new SellerOrdersRequest
            {
                SellerId = UserOf(c).UserId,
                Page = page,
                PageSize = pageSize
            }, o), OrderPage.Parser);
        }).Produces<ApiResponse<PagedData<Order>>>();
    }

    // ---------- admin/staff ----------
    private static void MapAdmin(WebApplication app)
    {
        var staffArea = app.MapGroup("/api/v1/admin").WithTags("Admin").WithMetadata(new BackendTag("users"))
            .RequireRateLimiting("api").RequireAuthorization("staff");

        staffArea.MapGet("/users", (HttpContext c, BackendDispatcher d, UsersApi.UsersApiClient client,
            int page = 1, int pageSize = 20, string? q = null, string? role = null) =>
        {
            if (!PageOk(page, pageSize)) return Bad(c, PageMessage, field: "Pagination");

            return d.ExecutePaged(c, "users", "users", page, pageSize, o => client.ListUsersAsync(new ListUsersRequest
            {
                Page = page,
                PageSize = pageSize,
                Q = q ?? "",
                Role = role ?? ""
            }, o), UserPage.Parser);
        }).Produces<ApiResponse<PagedData<UserProfile>>>();

        staffArea.MapPost("/sellers/{id:guid}/approve", (Guid id, ApproveSellerBody body, HttpContext c, BackendDispatcher d, UsersApi.UsersApiClient client) =>
        {
            if (!body.Approve && string.IsNullOrWhiteSpace(body.Reason)) return Bad(c, "từ chối đơn đăng ký phải kèm reason", field: "Reason");

            return d.Execute(c, "users", "seller-approve", o => client.ApproveSellerAsync(new ApproveSellerRequest
            {
                RegistrationId = id.ToString(),
                Approve = body.Approve,
                Reason = body.Reason ?? ""
            }, o), SellerRegistrationInfo.Parser, successMessage: "Seller application processed.");
        }).Produces<ApiResponse<SellerRegistrationInfo>>();

        var staffOrders = app.MapGroup("/api/v1/admin/orders").WithTags("Admin").WithMetadata(new BackendTag("commerce"))
            .RequireRateLimiting("api").RequireAuthorization("staff");

        staffOrders.MapGet("", (HttpContext c, BackendDispatcher d, CommerceApi.CommerceApiClient client, int page = 1, int pageSize = 20) =>
        {
            if (!PageOk(page, pageSize)) return Bad(c, PageMessage, field: "Pagination");

            return d.ExecutePaged(c, "commerce", "admin-orders", page, pageSize, o => client.ListAllOrdersAsync(new PageRequest
            {
                Page = page,
                PageSize = pageSize
            }, o), OrderPage.Parser);
        }).Produces<ApiResponse<PagedData<Order>>>();

        var adminOnly = app.MapGroup("/api/v1/admin").WithTags("Admin").WithMetadata(new BackendTag("users"))
            .RequireRateLimiting("api").RequireAuthorization("admin");

        adminOnly.MapPost("/users", (CreateUserBody body, HttpContext c, BackendDispatcher d, UsersApi.UsersApiClient client) =>
        {
            var roles = body.Roles ?? Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(body.Username) || !body.Email.Contains('@') || (body.Password?.Length ?? 0) < 8)
                return Bad(c, "username, email hợp lệ và password >= 8 ký tự là bắt buộc");
            if (roles.Any(r => !KnownRoles.Contains(r.Trim().ToLowerInvariant())))
                return Bad(c, "roles chỉ được chứa buyer|seller|staff|admin", field: "Roles");

            var request = new CreateUserRequest
            {
                Username = body.Username,
                Email = body.Email,
                Password = body.Password,
                DisplayName = body.DisplayName ?? body.Username,
            };
            request.Roles.AddRange(roles.Select(r => r.Trim().ToLowerInvariant()));

            return d.Execute(c, "users", "user-create", o => client.CreateUserAsync(request, o), UserProfile.Parser, successMessage: "User created successfully.", successStatusCode: StatusCodes.Status201Created);
        }).Produces<ApiResponse<UserProfile>>();

        adminOnly.MapPost("/users/{id:guid}/roles", (Guid id, AssignRolesBody body, HttpContext c, BackendDispatcher d, UsersApi.UsersApiClient client) =>
        {
            if (body.Roles is null || body.Roles.Length == 0 || body.Roles.Any(r => !KnownRoles.Contains(r.Trim().ToLowerInvariant())))
                return Bad(c, "roles phải là tập con của buyer|seller|staff|admin", field: "Roles");

            return d.Execute(c, "users", "user-roles", o => client.AssignRolesAsync(new AssignRolesRequest
            {
                UserId = id.ToString(),
                Roles = { body.Roles }
            }, o), UserProfile.Parser, successMessage: "Roles assigned successfully.");
        }).Produces<ApiResponse<UserProfile>>();

        adminOnly.MapGet("/roles", (HttpContext c, BackendDispatcher d, UsersApi.UsersApiClient client) =>
            d.Execute(c, "users", "roles", o => client.ListRolesAsync(new Empty(), o), RoleList.Parser, successMessage: "Roles retrieved successfully."))
            .Produces<ApiResponse<RoleList>>();

        adminOnly.MapGet("/permissions", (HttpContext c, BackendDispatcher d, UsersApi.UsersApiClient client) =>
            d.Execute(c, "users", "permissions", o => client.ListPermissionsAsync(new Empty(), o), PermissionList.Parser, successMessage: "Permissions retrieved successfully."))
            .Produces<ApiResponse<PermissionList>>();
    }

    // ---------- audit (staff đọc; admin bật/tắt từ điển) ----------
    private static void MapAudit(WebApplication app)
    {
        var audit = app.MapGroup("/api/v1/audit").WithTags("Audit").WithMetadata(new BackendTag("audit"))
            .RequireRateLimiting("api").RequireAuthorization("staff");

        audit.MapGet("/logs", (HttpContext c, BackendDispatcher d, AuditApi.AuditApiClient client,
            int page = 1, int pageSize = 20, string? from = null, string? to = null, string? service = null,
            string? eventType = null, string? entityName = null, string? entityId = null,
            string? actorUserId = null, string? correlationId = null) =>
        {
            if (!PageOk(page, pageSize)) return Bad(c, PageMessage, field: "Pagination");
            if (!GuidOk(actorUserId) || !GuidOk(correlationId)) return Bad(c, "actorUserId/correlationId phải là UUID");

            return d.ExecutePaged(c, "audit", "audit-logs", page, pageSize, o => client.QueryAuditLogsAsync(new QueryAuditLogsRequest
            {
                Page = page,
                PageSize = pageSize,
                From = from ?? "",
                To = to ?? "",
                ServiceName = service ?? "",
                EventType = eventType ?? "",
                EntityName = entityName ?? "",
                EntityId = entityId ?? "",
                ActorUserId = actorUserId ?? "",
                CorrelationId = correlationId ?? "",
            }, o), AuditLogPage.Parser);
        }).Produces<ApiResponse<PagedData<AuditLog>>>();

        audit.MapGet("/logs/{id:guid}", (Guid id, HttpContext c, BackendDispatcher d, AuditApi.AuditApiClient client) =>
            d.Execute(c, "audit", "audit-log", o => client.GetAuditLogAsync(new IdRequest { Id = id.ToString() }, o),
                AuditLog.Parser, successMessage: "Audit log retrieved successfully."))
            .Produces<ApiResponse<AuditLog>>();

        audit.MapGet("/event-types", (HttpContext c, BackendDispatcher d, AuditApi.AuditApiClient client) =>
            d.Execute(c, "audit", "audit-event-types", o => client.ListAuditEventTypesAsync(new Empty(), o),
                AuditEventTypeList.Parser, successMessage: "Audit event types retrieved successfully."))
            .Produces<ApiResponse<AuditEventTypeList>>();

        var auditAdminOnly = audit.MapGroup("").RequireAuthorization("admin");

        auditAdminOnly.MapPut("/event-types/{code}", (string code, AuditEventTypeToggleBody body, HttpContext c,
            BackendDispatcher d, AuditApi.AuditApiClient client) =>
            d.Execute(c, "audit", "audit-event-type-toggle", o => client.SetAuditEventTypeEnabledAsync(
                new SetAuditEventTypeEnabledRequest { Code = code, Enabled = body.Enabled }, o),
                AuditEventType.Parser, successMessage: "Audit event type toggled successfully."))
            .Produces<ApiResponse<AuditEventType>>();
    }

    // ---------- gateway control plane (administrator only) ----------
    private static void MapGatewayModes(WebApplication app)
    {
        var group = app.MapGroup("/api/v1/gateway")
            .WithTags("Gateway")
            .RequireRateLimiting("api")
            .RequireAuthorization(GatewayPolicies.GatewayAdmin)
            .WithMetadata(new DisableMockAuthorizationBypass());

        group.MapGet("/modes", (IGatewayRuntimeManager manager, IApiResponseFactory factory, HttpContext c) =>
        {
            var state = new GatewayStateResponse(manager.GetAuthMode(c), manager.GetServiceModes());
            return factory.SuccessResult(c, state, "Gateway service modes retrieved.");
        });

        group.MapPut("/mode", async (AuthModeUpdateBody body, IGatewayRuntimeManager manager, IApiResponseFactory factory, HttpContext c) =>
        {
            var success = await manager.SetAuthModeAsync(body.Mode);
            if (!success)
            {
                return ApiResponseFactory.Failure(c, "Invalid auth mode. Must be 'Enforce' or Development-only 'Bypass'.", StatusCodes.Status400BadRequest, errorCode: "INVALID_AUTH_MODE");
            }
            return factory.SuccessResult(c, new { authMode = manager.GetAuthMode(c) }, $"Gateway auth mode updated to '{body.Mode}'.");
        });

        group.MapPut("/modes/{service}", async (string service, ServiceModeUpdateBody body, IGatewayRuntimeManager manager, IApiResponseFactory factory, HttpContext c) =>
        {
            var success = await manager.SetServiceModeAsync(service, body.Mode);
            if (!success)
            {
                return ApiResponseFactory.Failure(c, "Invalid service mode. Must be 'Mock' or 'Real'.", StatusCodes.Status400BadRequest, errorCode: "INVALID_MODE");
            }
            return factory.SuccessResult(c, new { service, mode = body.Mode }, $"Service '{service}' mode updated to '{body.Mode}'.");
        });

        group.MapPut("/modes", async (BulkServiceModeUpdateBody body, IGatewayRuntimeManager manager, IApiResponseFactory factory, HttpContext c) =>
        {
            var requestedAuthMode = body.AuthMode ?? body.GatewayMode;
            if (!string.IsNullOrEmpty(requestedAuthMode) && !await manager.SetAuthModeAsync(requestedAuthMode))
            {
                return ApiResponseFactory.Failure(c, "Invalid auth mode. Must be 'Enforce' or Development-only 'Bypass'.", StatusCodes.Status400BadRequest, errorCode: "INVALID_AUTH_MODE");
            }
            if (body.Services != null && !await manager.SetBulkServiceModesAsync(body.Services))
            {
                return ApiResponseFactory.Failure(c, "One or more service names or modes are invalid.", StatusCodes.Status400BadRequest, errorCode: "INVALID_SERVICE_MODE");
            }
            var state = new GatewayStateResponse(manager.GetAuthMode(c), manager.GetServiceModes());
            return factory.SuccessResult(c, state, "Gateway modes updated.");
        });
    }

    // ---------- mock engine management ----------
    private static void MapMockEngine(WebApplication app)
    {
        var group = app.MapGroup("/api/v1/mock-engine")
            .WithTags("Mock Engine")
            .RequireRateLimiting("api")
            .RequireAuthorization(GatewayPolicies.GatewayAdmin)
            .WithMetadata(new DisableMockAuthorizationBypass());

        group.MapGet("/definitions", (IMockEngine engine, IApiResponseFactory factory, HttpContext c) =>
        {
            var defs = engine.GetDefinitions().Select(d => new
            {
                d.Id,
                d.Service,
                d.Method,
                d.Path,
                d.Description,
                d.Policy,
                d.StatusCode,
                d.ResponseType,
                hasRequest = d.Request.HasValue,
                hasResponse = d.Response.HasValue
            });
            return factory.SuccessResult(c, defs, "Mock endpoint definitions retrieved.");
        });

        group.MapPost("/reload", (IMockEngine engine, IApiResponseFactory factory, HttpContext c) =>
        {
            engine.Reload();
            var count = engine.GetDefinitions().Count;
            return factory.SuccessResult(c, new { loadedCount = count }, $"Reloaded {count} mock definitions successfully.");
        });
    }

    // ---------- auction ----------
    private static void MapAuction(WebApplication app)
    {
        var group = app.MapGroup("/api/v1/auctions").WithTags("Auction").WithMetadata(new BackendTag("auction")).RequireRateLimiting("api");

        group.MapGet("", (HttpContext c, BackendDispatcher d, AuctionApi.AuctionApiClient client, int page = 1, int pageSize = 20, string? status = null, string? sellerId = null) =>
        {
            if (!PageOk(page, pageSize)) return Bad(c, PageMessage, field: "Pagination");
            return d.ExecutePaged(c, "auction", "auctions", page, pageSize, o => client.ListAuctionsAsync(new AuctionQuery
            {
                Page = page,
                PageSize = pageSize,
                Status = status ?? "",
                SellerId = sellerId ?? ""
            }, o), AuctionPage.Parser);
        }).Produces<ApiResponse<PagedData<Auction>>>().AllowAnonymous();

        group.MapGet("/{id:guid}", (Guid id, HttpContext c, BackendDispatcher d, AuctionApi.AuctionApiClient client) =>
            d.Execute(c, "auction", "auction", o => client.GetAuctionAsync(new IdRequest { Id = id.ToString() }, o), Auction.Parser, successMessage: "Auction retrieved successfully."))
            .Produces<ApiResponse<Auction>>().AllowAnonymous();

        group.MapPost("", (CreateAuctionBody body, HttpContext c, BackendDispatcher d, AuctionApi.AuctionApiClient client) =>
        {
            if (!Guid.TryParse(body.ProductId, out _)) return Bad(c, "productId phải là UUID", field: "ProductId");
            if (string.IsNullOrWhiteSpace(body.Title)) return Bad(c, "title là bắt buộc", field: "Title");
            if (!decimal.TryParse(body.StartingPrice, NumberStyles.Number, CultureInfo.InvariantCulture, out var sp) || sp <= 0)
                return Bad(c, "startingPrice phải là số > 0", field: "StartingPrice");
            if (!decimal.TryParse(body.StepPrice, NumberStyles.Number, CultureInfo.InvariantCulture, out var stp) || stp <= 0)
                return Bad(c, "stepPrice phải là số > 0", field: "StepPrice");

            return d.Execute(c, "auction", "auction-create", o => client.CreateAuctionAsync(new CreateAuctionRequest
            {
                ProductId = body.ProductId,
                SellerId = UserOf(c).UserId,
                StartingPrice = sp.ToString("0.00", CultureInfo.InvariantCulture),
                StepPrice = stp.ToString("0.00", CultureInfo.InvariantCulture),
                StartTime = body.StartTime ?? "",
                EndTime = body.EndTime ?? "",
                Title = body.Title
            }, o), Auction.Parser, successMessage: "Auction created successfully.", successStatusCode: StatusCodes.Status201Created);
        }).Produces<ApiResponse<Auction>>().RequireAuthorization("seller");

        group.MapPost("/{id:guid}/bids", (Guid id, PlaceBidBody body, HttpContext c, BackendDispatcher d, AuctionApi.AuctionApiClient client) =>
        {
            if (!decimal.TryParse(body.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
                return Bad(c, "amount phải là số > 0", field: "Amount");

            return d.Execute(c, "auction", "bid-place", o => client.PlaceBidAsync(new PlaceBidRequest
            {
                AuctionId = id.ToString(),
                UserId = UserOf(c).UserId,
                Amount = amount.ToString("0.00", CultureInfo.InvariantCulture)
            }, o), Bid.Parser, successMessage: "Bid placed successfully.", successStatusCode: StatusCodes.Status201Created);
        }).Produces<ApiResponse<Bid>>().RequireAuthorization("authenticated");

        group.MapGet("/{id:guid}/bids", (Guid id, HttpContext c, BackendDispatcher d, AuctionApi.AuctionApiClient client) =>
            d.Execute(c, "auction", "bids", o => client.ListBidsAsync(new IdRequest { Id = id.ToString() }, o), BidPage.Parser, successMessage: "Bids retrieved successfully."))
            .Produces<ApiResponse<BidPage>>().AllowAnonymous();
    }

    // ---------- fulfillment ----------
    private static void MapFulfillment(WebApplication app)
    {
        var group = app.MapGroup("/api/v1/fulfillment").WithTags("Fulfillment").WithMetadata(new BackendTag("fulfillment")).RequireRateLimiting("api");

        group.MapGet("/inventory/{skuId}", (string skuId, HttpContext c, BackendDispatcher d, FulfillmentApi.FulfillmentApiClient client) =>
        {
            if (string.IsNullOrWhiteSpace(skuId)) return Bad(c, "skuId là bắt buộc", field: "SkuId");
            return d.Execute(c, "fulfillment", "inventory", o => client.GetInventoryAsync(new IdRequest { Id = skuId }, o), FulfillmentInventory.Parser, successMessage: "Inventory retrieved successfully.");
        }).Produces<ApiResponse<FulfillmentInventory>>().AllowAnonymous();

        group.MapPost("/shipping/quote", (ShippingQuoteBody body, HttpContext c, BackendDispatcher d, FulfillmentApi.FulfillmentApiClient client) =>
        {
            if (string.IsNullOrWhiteSpace(body.SellerAddress)) return Bad(c, "sellerAddress là bắt buộc", field: "SellerAddress");
            if (string.IsNullOrWhiteSpace(body.BuyerAddress)) return Bad(c, "buyerAddress là bắt buộc", field: "BuyerAddress");
            if (body.WeightGrams <= 0) return Bad(c, "weightGrams phải > 0", field: "WeightGrams");

            return d.Execute(c, "fulfillment", "shipping-quote", o => client.CalculateShippingFeeAsync(new ShippingFeeRequest
            {
                SellerAddress = body.SellerAddress,
                BuyerAddress = body.BuyerAddress,
                WeightGrams = body.WeightGrams
            }, o), ShippingFeeQuote.Parser, successMessage: "Shipping quote calculated.");
        }).Produces<ApiResponse<ShippingFeeQuote>>().AllowAnonymous();

        group.MapGet("/shipments/{trackingCode}", (string trackingCode, HttpContext c, BackendDispatcher d, FulfillmentApi.FulfillmentApiClient client) =>
        {
            if (string.IsNullOrWhiteSpace(trackingCode)) return Bad(c, "trackingCode là bắt buộc", field: "TrackingCode");
            return d.Execute(c, "fulfillment", "shipment-tracking", o => client.GetShipmentTrackingAsync(new IdRequest { Id = trackingCode }, o), ShipmentTracking.Parser, successMessage: "Shipment tracking retrieved.");
        }).Produces<ApiResponse<ShipmentTracking>>().AllowAnonymous();
    }

    // ---------- notification ----------
    private static void MapNotification(WebApplication app)
    {
        var group = app.MapGroup("/api/v1/notifications").WithTags("Notification").WithMetadata(new BackendTag("notification"))
            .RequireRateLimiting("api").RequireAuthorization("authenticated");

        group.MapGet("", (HttpContext c, BackendDispatcher d, NotificationApi.NotificationApiClient client) =>
            d.Execute(c, "notification", "notifications", o => client.ListNotificationsAsync(UserOf(c), o), NotificationPage.Parser, successMessage: "Notifications retrieved."))
            .Produces<ApiResponse<NotificationPage>>();

        group.MapPut("/{id:guid}/read", (Guid id, HttpContext c, BackendDispatcher d, NotificationApi.NotificationApiClient client) =>
            d.Execute(c, "notification", "notification-read", o => client.MarkNotificationReadAsync(new IdRequest { Id = id.ToString() }, o), Notification.Parser, successMessage: "Notification marked as read."))
            .Produces<ApiResponse<Notification>>();

        group.MapGet("/preferences", (HttpContext c, BackendDispatcher d, NotificationApi.NotificationApiClient client) =>
            d.Execute(c, "notification", "preferences", o => client.GetPreferencesAsync(UserOf(c), o), NotificationPreferences.Parser, successMessage: "Preferences retrieved."))
            .Produces<ApiResponse<NotificationPreferences>>();

        group.MapPut("/preferences", (UpdatePreferencesBody body, HttpContext c, BackendDispatcher d, NotificationApi.NotificationApiClient client) =>
            d.Execute(c, "notification", "preferences-update", o => client.UpdatePreferencesAsync(new UpdateNotificationPreferencesRequest
            {
                UserId = UserOf(c).UserId,
                EmailEnabled = body.EmailEnabled,
                PushEnabled = body.PushEnabled,
                OrderUpdates = body.OrderUpdates,
                AuctionAlerts = body.AuctionAlerts
            }, o), NotificationPreferences.Parser, successMessage: "Preferences updated."))
            .Produces<ApiResponse<NotificationPreferences>>();
    }

    // ---------- wallet (e-wallet / payment balance) ----------
    private static void MapWallet(WebApplication app)
    {
        var group = app.MapGroup("/api/v1/wallet").WithTags("Wallet").WithMetadata(new BackendTag("wallet"))
            .RequireRateLimiting("api").RequireAuthorization("authenticated");

        // 1. Lấy thông tin ví và số dư hiện tại
        group.MapGet("/balance", (HttpContext c, BackendDispatcher d, WalletApi.WalletApiClient client) =>
            d.Execute(c, "wallet", "wallet-balance", o => client.GetWalletAsync(UserOf(c), o), Wallet.Parser, successMessage: "Wallet retrieved successfully."))
            .Produces<ApiResponse<Wallet>>();

        // 2. Nạp tiền vào ví
        group.MapPost("/deposit", (WalletDepositBody body, HttpContext c, BackendDispatcher d, WalletApi.WalletApiClient client) =>
        {
            if (!decimal.TryParse(body.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amt) || amt <= 0)
                return Bad(c, "amount phải là số dương hợp lệ", field: "Amount");

            return d.Execute(c, "wallet", "wallet-deposit", o => client.DepositAsync(new WalletDepositRequest
            {
                UserId = UserOf(c).UserId,
                Amount = amt.ToString("0.00", CultureInfo.InvariantCulture),
                Currency = string.IsNullOrWhiteSpace(body.Currency) ? "VND" : body.Currency.Trim().ToUpperInvariant(),
                PaymentMethod = string.IsNullOrWhiteSpace(body.PaymentMethod) ? "BANK_TRANSFER" : body.PaymentMethod.Trim()
            }, o), WalletTransaction.Parser, successMessage: "Deposit processed successfully.");
        }).Produces<ApiResponse<WalletTransaction>>();

        // 3. Rút tiền từ ví
        group.MapPost("/withdraw", (WalletWithdrawBody body, HttpContext c, BackendDispatcher d, WalletApi.WalletApiClient client) =>
        {
            if (!decimal.TryParse(body.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amt) || amt <= 0)
                return Bad(c, "amount phải là số dương hợp lệ", field: "Amount");
            if (string.IsNullOrWhiteSpace(body.BankAccount))
                return Bad(c, "bankAccount là bắt buộc", field: "BankAccount");

            return d.Execute(c, "wallet", "wallet-withdraw", o => client.WithdrawAsync(new WalletWithdrawRequest
            {
                UserId = UserOf(c).UserId,
                Amount = amt.ToString("0.00", CultureInfo.InvariantCulture),
                Currency = string.IsNullOrWhiteSpace(body.Currency) ? "VND" : body.Currency.Trim().ToUpperInvariant(),
                BankAccount = body.BankAccount.Trim()
            }, o), WalletTransaction.Parser, successMessage: "Withdrawal processed successfully.");
        }).Produces<ApiResponse<WalletTransaction>>();

        // 4. Chuyển tiền giữa các ví người dùng
        group.MapPost("/transfer", (WalletTransferBody body, HttpContext c, BackendDispatcher d, WalletApi.WalletApiClient client) =>
        {
            if (!GuidOk(body.RecipientUserId) || string.IsNullOrWhiteSpace(body.RecipientUserId))
                return Bad(c, "recipientUserId phải là UUID hợp lệ", field: "RecipientUserId");
            if (!decimal.TryParse(body.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amt) || amt <= 0)
                return Bad(c, "amount phải là số dương hợp lệ", field: "Amount");

            var sender = UserOf(c);
            if (string.Equals(sender.UserId, body.RecipientUserId, StringComparison.OrdinalIgnoreCase))
                return Bad(c, "Không thể tự chuyển tiền cho chính mình", field: "RecipientUserId");

            return d.Execute(c, "wallet", "wallet-transfer", o => client.TransferAsync(new WalletTransferRequest
            {
                SenderUserId = sender.UserId,
                RecipientUserId = body.RecipientUserId,
                Amount = amt.ToString("0.00", CultureInfo.InvariantCulture),
                Note = body.Note ?? ""
            }, o), WalletTransaction.Parser, successMessage: "Transfer processed successfully.");
        }).Produces<ApiResponse<WalletTransaction>>();

        // 5. Lịch sử giao dịch ví (phân trang)
        group.MapGet("/transactions", (HttpContext c, BackendDispatcher d, WalletApi.WalletApiClient client, int page = 1, int pageSize = 20, string? type = null) =>
        {
            if (!PageOk(page, pageSize)) return Bad(c, PageMessage, field: "Pagination");

            return d.ExecutePaged(c, "wallet", "wallet-transactions", page, pageSize, o => client.ListTransactionsAsync(new WalletTransactionsRequest
            {
                UserId = UserOf(c).UserId,
                Page = page,
                PageSize = pageSize,
                Type = type ?? ""
            }, o), WalletTransactionPage.Parser);
        }).Produces<ApiResponse<PagedData<WalletTransaction>>>();
    }

    // ---------- helpers ----------
    private static bool PageOk(int page, int size) => page > 0 && size is >= 1 and <= 100;
    private static bool GuidOk(string? value) => string.IsNullOrEmpty(value) || Guid.TryParse(value, out _);

    private static Task<IResult> Bad(HttpContext context, string message, string field = "General", string errorCode = "VAL_INVALID_INPUT")
    {
        var errors = new Dictionary<string, string[]>
        {
            [field] = [message]
        };
        return Task.FromResult(ApiResponseFactory.Failure(
            context,
            title: "One or more validation errors occurred.",
            status: StatusCodes.Status400BadRequest,
            detail: message,
            errorCode: errorCode,
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            errors: errors
        ));
    }

    private static UserRequest UserOf(HttpContext context) => new() { UserId = context.User.FindFirst("sub")?.Value ?? "" };

    private static string[] RolesOf(HttpContext context) => context.User.FindAll("role").Select(x => x.Value).ToArray();

    private static string RoleOf(HttpContext context)
    {
        var roles = RolesOf(context);
        foreach (var role in new[] { "admin", "staff", "seller" })
        {
            if (roles.Contains(role)) return role;
        }
        return roles.FirstOrDefault() ?? "buyer";
    }

    private static bool ProductValid(ProductBody body, out string price, out string error)
    {
        price = ""; error = "";
        if (string.IsNullOrWhiteSpace(body.Name)) { error = "name là bắt buộc"; return false; }
        if (!decimal.TryParse(body.Price, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value <= 0)
        { error = "price phải là số thập phân > 0 (ví dụ \"1250000.00\")"; return false; }
        if (body.Available < 0) { error = "available phải >= 0"; return false; }
        if (!GuidOk(body.CategoryId)) { error = "categoryId phải là UUID"; return false; }
        price = value.ToString("0.00", CultureInfo.InvariantCulture);
        return true;
    }
}
