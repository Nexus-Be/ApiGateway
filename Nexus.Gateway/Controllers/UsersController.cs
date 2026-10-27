namespace Nexus.Gateway.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.User;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using ShoppingCart.Contracts.V1;

[ApiController]
[Route("api/v1")]
[BackendTag("users")]
[EnableRateLimiting("api")]
public sealed class UsersController : GatewayBaseController
{
    private const string Service = "users";
    private readonly UsersApi.UsersApiClient _client;
    private readonly UserApi.UserApiClient _userClient;

    public UsersController(
        IGatewayDispatcher dispatcher,
        UsersApi.UsersApiClient client,
        UserApi.UserApiClient userClient) : base(dispatcher)
    {
        _client = client;
        _userClient = userClient;
    }

    [HttpGet("users/me")]
    [HttpGet("me")]
    [Authorize(GatewayPolicies.Authenticated)]
    [EnableRateLimiting("user-frequent")]
    [ProducesResponseType(typeof(ApiResponse<UserProfile>), StatusCodes.Status200OK)]
    public Task<IResult> GetMe()
    {
        return Dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "profile",
            o => _client.GetProfileAsync(HttpContext.ToUserRequest(), o),
            UserProfile.Parser,
            successMessage: "User profile retrieved successfully."
        );
    }

    [HttpGet("me/sessions")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<SessionListResponse>), StatusCodes.Status200OK)]
    public Task<IResult> ListMySessions()
    {
        return Dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "user-sessions",
            o => _userClient.ListSessionsAsync(new ListSessionsRequest
            {
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            SessionListResponse.Parser,
            successMessage: "Active sessions retrieved successfully."
        );
    }

    [HttpDelete("me/sessions/{sessionId}")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IResult> RevokeSession([FromRoute] string sessionId)
    {
        var id = UserValidation.EnsureRequired(sessionId, "SessionId");

        return Dispatcher.DispatchNoContentAsync(
            HttpContext,
            Service,
            "user-session-revoke",
            o => _userClient.RevokeSessionAsync(new RevokeSessionRequest
            {
                SessionId = id,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            RevokeSessionResponse.Parser
        );
    }

    [HttpPatch("me")]
    [HttpPatch("users/me")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<MyProfileUpdatedResponse>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateMe([FromBody] UpdateMyProfileDto body)
    {
        return Dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "user-profile-update",
            o => _userClient.UpdateMyProfileAsync(new UpdateMyProfileRequest
            {
                Username = body.Username,
                FirstName = body.FirstName ?? string.Empty,
                LastName = body.LastName ?? string.Empty,
                PhoneNumber = body.PhoneNumber ?? string.Empty,
                Address = UserValidation.ToAddress(body.Address),
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            MyProfileUpdatedResponse.Parser,
            successMessage: "Profile updated successfully."
        );
    }

    [HttpGet("me/avatar")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<AvatarResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetMyAvatar()
    {
        return Dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "user-my-avatar",
            o => _userClient.GetMyAvatarAsync(new GetMyAvatarRequest
            {
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            AvatarResponse.Parser,
            successMessage: "Avatar retrieved successfully."
        );
    }

    [HttpPost("me/avatar")]
    [Authorize(GatewayPolicies.Authenticated)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<AvatarUploadedResponse>), StatusCodes.Status200OK)]
    public Task<IResult> UploadMyAvatar([FromForm] UploadSingleFileForm form)
    {
        var fileName = form.File?.FileName ?? "avatar.png";

        return Dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "user-my-avatar-upload",
            o => _userClient.UploadMyAvatarAsync(new UploadAvatarRequest
            {
                FileName = fileName,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            AvatarUploadedResponse.Parser,
            successMessage: "Avatar uploaded successfully."
        );
    }

    [HttpGet("me/reputation")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<MyReputationResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetMyReputation()
    {
        return Dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "user-my-reputation",
            o => _userClient.GetMyReputationAsync(new GetMyReputationRequest
            {
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            MyReputationResponse.Parser,
            successMessage: "Reputation score retrieved successfully."
        );
    }

    [HttpGet("me/penalties")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<PenaltyListResponse>), StatusCodes.Status200OK)]
    public Task<IResult> ListMyPenalties([FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        UserValidation.EnsurePaged(page, limit);

        return Dispatcher.DispatchPagedAsync(
            HttpContext,
            Service,
            "user-my-penalties",
            page,
            limit,
            o => _userClient.ListMyPenaltiesAsync(new ListMyPenaltiesRequest
            {
                Page = page,
                Limit = limit,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            PenaltyListResponse.Parser
        );
    }

    [HttpGet("user/{userId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PublicUserProfileResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetUserPublicProfile([FromRoute] string userId)
    {
        UserValidation.EnsureUuid(userId, "UserId");

        return Dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "user-public-profile",
            o => _userClient.GetUserPublicProfileAsync(new GetUserPublicProfileRequest
            {
                UserId = userId
            }, o),
            PublicUserProfileResponse.Parser,
            successMessage: "User profile retrieved successfully."
        );
    }

    [HttpGet("user/{userId}/avatar")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AvatarResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetUserAvatar([FromRoute] string userId)
    {
        UserValidation.EnsureUuid(userId, "UserId");

        return Dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "user-avatar",
            o => _userClient.GetUserAvatarAsync(new GetUserAvatarRequest
            {
                UserId = userId
            }, o),
            AvatarResponse.Parser,
            successMessage: "Avatar retrieved successfully."
        );
    }

    [HttpGet("seller/{sellerId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<SellerPublicProfileResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetSellerPublicProfile([FromRoute] string sellerId)
    {
        UserValidation.EnsureUuid(sellerId, "SellerId");

        return Dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "seller-public-profile",
            o => _userClient.GetSellerPublicProfileAsync(new GetSellerPublicProfileRequest
            {
                SellerId = sellerId
            }, o),
            SellerPublicProfileResponse.Parser,
            successMessage: "Seller profile retrieved successfully."
        );
    }

    [HttpGet("user/{userId}/reputation")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<UserReputationResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetUserReputation([FromRoute] string userId)
    {
        UserValidation.EnsureUuid(userId, "UserId");

        return Dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "user-reputation",
            o => _userClient.GetUserReputationAsync(new GetUserReputationRequest
            {
                UserId = userId
            }, o),
            UserReputationResponse.Parser,
            successMessage: "User reputation retrieved successfully."
        );
    }

    [HttpGet("user/{userId}/reputation/history")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<ReputationHistoryResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetUserReputationHistory([FromRoute] string userId, [FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        UserValidation.EnsureUuid(userId, "UserId");
        UserValidation.EnsurePaged(page, limit);

        return Dispatcher.DispatchPagedAsync(
            HttpContext,
            Service,
            "user-reputation-history",
            page,
            limit,
            o => _userClient.GetUserReputationHistoryAsync(new GetUserReputationHistoryRequest
            {
                UserId = userId,
                Page = page,
                Limit = limit,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            ReputationHistoryResponse.Parser
        );
    }

    [HttpGet("user/{userId}/reviews")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<UserReviewListResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetUserReviews(
        [FromRoute] string userId,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] int filterRating = 0)
    {
        UserValidation.EnsureUuid(userId, "UserId");
        UserValidation.EnsurePaged(page, limit);

        return Dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "user-reviews",
            o => _userClient.GetUserReviewsAsync(new GetUserReviewsRequest
            {
                UserId = userId,
                Page = page,
                Limit = limit,
                FilterRating = filterRating
            }, o),
            UserReviewListResponse.Parser,
            successMessage: "User reviews retrieved successfully."
        );
    }

    [HttpPost("review/files/upload")]
    [Authorize(GatewayPolicies.Authenticated)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<UploadFilesResponse>), StatusCodes.Status200OK)]
    public Task<IResult> UploadReviewFiles([FromForm] UploadMultipleFilesForm form)
    {
        var fileNames = form.Files?.Select(f => f.FileName).ToList() ?? ["review_file_1.jpg"];
        var req = new UploadFilesRequest { Caller = UserValidation.Caller(HttpContext) };
        req.FileNames.AddRange(fileNames);

        return Dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "review-files-upload",
            o => _userClient.UploadReviewFilesAsync(req, o),
            UploadFilesResponse.Parser,
            successMessage: "Files uploaded successfully."
        );
    }

    [HttpPost("reviews")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<ReviewCreatedResponse>), StatusCodes.Status201Created)]
    public Task<IResult> CreateReview([FromBody] CreateReviewDto body)
    {
        UserValidation.EnsureRequired(body.TransactionType, "TransactionType");
        UserValidation.EnsureRequired(body.TransactionId, "TransactionId");
        UserValidation.EnsureUuid(body.TargetUserId, "TargetUserId");
        UserValidation.EnsureRating(body.Rating);
        UserValidation.EnsureRequired(body.Comment, "Comment");

        var req = new CreateReviewRequest
        {
            TransactionType = body.TransactionType,
            TransactionId = body.TransactionId,
            TargetUserId = body.TargetUserId,
            Rating = body.Rating ?? 5,
            Comment = body.Comment,
            Caller = UserValidation.Caller(HttpContext)
        };
        if (body.MediaUrls != null) req.MediaUrls.AddRange(body.MediaUrls);

        return Dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "review-create",
            o => _userClient.CreateReviewAsync(req, o),
            ReviewCreatedResponse.Parser,
            successStatusCode: StatusCodes.Status201Created,
            successMessage: "Review submitted successfully."
        );
    }

    [HttpPost("report/files/upload")]
    [Authorize(GatewayPolicies.Authenticated)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<UploadFilesResponse>), StatusCodes.Status200OK)]
    public Task<IResult> UploadReportFiles([FromForm] UploadMultipleFilesForm form)
    {
        var fileNames = form.Files?.Select(f => f.FileName).ToList() ?? ["report_evidence_1.jpg"];
        var req = new UploadFilesRequest { Caller = UserValidation.Caller(HttpContext) };
        req.FileNames.AddRange(fileNames);

        return Dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "report-files-upload",
            o => _userClient.UploadReportFilesAsync(req, o),
            UploadFilesResponse.Parser,
            successMessage: "Report evidence files uploaded successfully."
        );
    }

    [HttpPost("users/reports")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<UserReportCreatedResponse>), StatusCodes.Status201Created)]
    public Task<IResult> CreateUserReport([FromBody] CreateUserReportDto body)
    {
        UserValidation.EnsureUuid(body.ReportedUserId, "ReportedUserId");
        UserValidation.EnsureRequired(body.ViolationType, "ViolationType");
        UserValidation.EnsureRequired(body.Description, "Description");

        var req = new CreateUserReportRequest
        {
            ReportedUserId = body.ReportedUserId,
            ViolationType = body.ViolationType,
            Description = body.Description,
            Caller = UserValidation.Caller(HttpContext)
        };
        if (body.EvidenceUrls != null) req.EvidenceUrls.AddRange(body.EvidenceUrls);

        return Dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "user-report-create",
            o => _userClient.CreateUserReportAsync(req, o),
            UserReportCreatedResponse.Parser,
            successStatusCode: StatusCodes.Status201Created,
            successMessage: "Report submitted successfully."
        );
    }

    [HttpGet("me/reports")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<MyReportListResponse>), StatusCodes.Status200OK)]
    public Task<IResult> ListMyReports(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string status = "")
    {
        UserValidation.EnsurePaged(page, limit);

        return Dispatcher.DispatchPagedAsync(
            HttpContext,
            Service,
            "user-my-reports",
            page,
            limit,
            o => _userClient.ListMyReportsAsync(new ListMyReportsRequest
            {
                Page = page,
                Limit = limit,
                Status = status ?? string.Empty,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            MyReportListResponse.Parser
        );
    }

    [HttpPost("seller/registration")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<SellerRegistrationCreatedResponse>), StatusCodes.Status201Created)]
    public Task<IResult> CreateSellerRegistration([FromBody] CreateSellerRegistrationDto body)
    {
        UserValidation.EnsureRequired(body.ShopName, "ShopName", "Shop name");
        UserValidation.EnsureEmail(body.BusinessEmail, "BusinessEmail");
        UserValidation.EnsureRequired(body.Hotline, "Hotline");
        UserValidation.EnsureRequired(body.IdentityCardNumber, "IdentityCardNumber", "Identity card number");

        return Dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "seller-registration-create",
            o => _userClient.CreateSellerRegistrationAsync(new CreateSellerRegistrationRequest
            {
                ShopName = body.ShopName,
                BusinessEmail = body.BusinessEmail,
                Hotline = body.Hotline,
                WarehouseAddress = UserValidation.ToAddress(body.WarehouseAddress),
                IdentityCardNumber = body.IdentityCardNumber,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            SellerRegistrationCreatedResponse.Parser,
            successStatusCode: StatusCodes.Status201Created,
            successMessage: "Seller registration submitted successfully."
        );
    }

    [HttpPost("seller/registration/otp/resend")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<SellerRegistrationOtpResendResponse>), StatusCodes.Status200OK)]
    public Task<IResult> ResendSellerRegistrationOtp([FromBody] ResendSellerRegistrationOtpDto body)
    {
        UserValidation.EnsureRequired(body.RegistrationId, "RegistrationId");
        UserValidation.EnsureEmail(body.BusinessEmail, "BusinessEmail");

        return Dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "seller-registration-otp-resend",
            o => _userClient.ResendSellerRegistrationOtpAsync(new ResendSellerRegistrationOtpRequest
            {
                RegistrationId = body.RegistrationId,
                BusinessEmail = body.BusinessEmail,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            SellerRegistrationOtpResendResponse.Parser,
            successMessage: "Verification OTP has been resent to business email."
        );
    }

    [HttpPost("seller/registration/documents")]
    [Authorize(GatewayPolicies.Authenticated)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<UploadFilesResponse>), StatusCodes.Status200OK)]
    public Task<IResult> UploadSellerDocuments([FromForm] UploadSellerDocumentsForm form)
    {
        var registrationId = UserValidation.EnsureRequired(form.RegistrationId, "RegistrationId");
        var fileNames = form.Files?.Select(f => f.FileName).ToList() ?? ["id_front.jpg", "id_back.jpg"];

        var req = new UploadSellerDocumentsRequest
        {
            RegistrationId = registrationId,
            Caller = UserValidation.Caller(HttpContext)
        };
        req.FileNames.AddRange(fileNames);

        return Dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "seller-registration-documents",
            o => _userClient.UploadSellerRegistrationDocumentsAsync(req, o),
            UploadFilesResponse.Parser,
            successMessage: "Seller registration documents uploaded successfully."
        );
    }

    [HttpPost("seller/otp/verify")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<VerifySellerOtpResponse>), StatusCodes.Status200OK)]
    public Task<IResult> VerifySellerOtp([FromBody] VerifySellerOtpDto body)
    {
        UserValidation.EnsureRequired(body.RegistrationId, "RegistrationId");
        UserValidation.EnsureOtp(body.OtpCode);

        return Dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "seller-otp-verify",
            o => _userClient.VerifySellerOtpAsync(new VerifySellerOtpRequest
            {
                RegistrationId = body.RegistrationId,
                OtpCode = body.OtpCode,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            VerifySellerOtpResponse.Parser,
            successMessage: "Seller registration email verified successfully."
        );
    }

    [HttpGet("seller/registration")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<SellerRegistrationStatusResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetSellerRegistrationStatus()
    {
        return Dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "seller-registration-status",
            o => _userClient.GetSellerRegistrationStatusAsync(new GetSellerRegistrationStatusRequest
            {
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            SellerRegistrationStatusResponse.Parser,
            successMessage: "Seller registration status retrieved successfully."
        );
    }

    [HttpGet("seller/profile")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<SellerOwnProfileResponse>), StatusCodes.Status200OK)]
    public Task<IResult> GetSellerOwnProfile()
    {
        return Dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "seller-own-profile",
            o => _userClient.GetSellerOwnProfileAsync(new GetSellerOwnProfileRequest
            {
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            SellerOwnProfileResponse.Parser,
            successMessage: "Seller profile retrieved successfully."
        );
    }

    [HttpPatch("seller/profile")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<SellerOwnProfileUpdatedResponse>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateSellerOwnProfile([FromBody] UpdateSellerOwnProfileDto body)
    {
        return Dispatcher.DispatchAsync(
            HttpContext,
            Service,
            "seller-own-profile-update",
            o => _userClient.UpdateSellerOwnProfileAsync(new UpdateSellerOwnProfileRequest
            {
                ShopName = body.ShopName,
                Hotline = body.Hotline,
                PickupAddress = UserValidation.ToAddress(body.PickupAddress),
                Description = body.Description ?? string.Empty,
                Caller = UserValidation.Caller(HttpContext)
            }, o),
            SellerOwnProfileUpdatedResponse.Parser,
            successMessage: "Seller profile updated successfully."
        );
    }
}
