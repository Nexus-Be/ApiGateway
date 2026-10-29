namespace Nexus.Gateway.Controllers;

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.User;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;

[ApiController]
[Route("api/v1")]
[BackendTag("users")]
[EnableRateLimiting("api")]
public sealed class UsersController : GatewayBaseController
{
    private const string Service = "users";

    public UsersController(IGatewayDispatcher dispatcher) : base(dispatcher)
    {
    }

    [HttpGet("me")]
    [Authorize(GatewayPolicies.Authenticated)]
    [EnableRateLimiting("user-frequent")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetMe()
    {
        return ForwardAsync(
            Service,
            "/api/v1/me",
            HttpMethod.Get,
            mockSeed: "profile",
            successMessage: "User profile retrieved successfully."
        );
    }

    [HttpGet("me/sessions")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<SessionListResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> ListMySessions()
    {
        return ForwardAsync(
            Service,
            "/api/v1/me/sessions",
            HttpMethod.Get,
            mockSeed: "user-sessions",
            successMessage: "Active sessions retrieved successfully."
        );
    }

    [HttpDelete("me/sessions/{sessionId}")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IResult> RevokeSession([FromRoute] string sessionId)
    {
        var id = UserValidation.EnsureRequired(sessionId, "SessionId");

        return ForwardNoContentAsync(
            Service,
            $"/api/v1/me/sessions/{id}",
            HttpMethod.Delete,
            mockSeed: "user-session-revoke"
        );
    }

    [HttpPatch("me")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<MyProfileUpdatedResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateMe([FromBody] UpdateMyProfileDto body)
    {
        return ForwardAsync(
            Service,
            "/api/v1/me",
            HttpMethod.Patch,
            body: body,
            mockSeed: "user-profile-update",
            successMessage: "Profile updated successfully."
        );
    }

    [HttpGet("me/avatar")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<AvatarResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetMyAvatar()
    {
        return ForwardAsync(
            Service,
            "/api/v1/me/avatar",
            HttpMethod.Get,
            mockSeed: "user-my-avatar",
            successMessage: "Avatar retrieved successfully."
        );
    }

    [HttpPost("me/avatar")]
    [Authorize(GatewayPolicies.Authenticated)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<AvatarUploadedResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> UploadMyAvatar([FromForm] UploadSingleFileForm form)
    {
        var fileName = form.File?.FileName ?? "avatar.png";

        return ForwardAsync(
            Service,
            "/api/v1/me/avatar",
            HttpMethod.Post,
            body: new { fileName },
            mockSeed: "user-my-avatar-upload",
            successMessage: "Avatar uploaded successfully."
        );
    }

    [HttpGet("me/reputation")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<MyReputationResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetMyReputation()
    {
        return ForwardAsync(
            Service,
            "/api/v1/me/reputation",
            HttpMethod.Get,
            mockSeed: "user-my-reputation",
            successMessage: "Reputation score retrieved successfully."
        );
    }

    [HttpGet("me/penalties")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<PenaltyListResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> ListMyPenalties([FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        UserValidation.EnsurePaged(page, limit);

        return ForwardPagedAsync(
            Service,
            "/api/v1/me/penalties",
            page,
            limit,
            HttpMethod.Get,
            mockSeed: "user-my-penalties",
            queryParams: new Dictionary<string, string?>
            {
                ["page"] = page.ToString(),
                ["limit"] = limit.ToString()
            }
        );
    }

    [HttpGet("user/{userId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PublicUserProfileResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetUserPublicProfile([FromRoute] string userId)
    {
        UserValidation.EnsureUuid(userId, "UserId");

        return ForwardAsync(
            Service,
            $"/api/v1/user/{userId}",
            HttpMethod.Get,
            mockSeed: "user-public-profile",
            successMessage: "User profile retrieved successfully."
        );
    }

    [HttpGet("user/{userId}/avatar")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AvatarResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetUserAvatar([FromRoute] string userId)
    {
        UserValidation.EnsureUuid(userId, "UserId");

        return ForwardAsync(
            Service,
            $"/api/v1/user/{userId}/avatar",
            HttpMethod.Get,
            mockSeed: "user-avatar",
            successMessage: "Avatar retrieved successfully."
        );
    }

    [HttpGet("seller/{sellerId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<SellerPublicProfileResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetSellerPublicProfile([FromRoute] string sellerId)
    {
        UserValidation.EnsureUuid(sellerId, "SellerId");

        return ForwardAsync(
            Service,
            $"/api/v1/seller/{sellerId}",
            HttpMethod.Get,
            mockSeed: "seller-public-profile",
            successMessage: "Seller profile retrieved successfully."
        );
    }

    [HttpGet("user/{userId}/reputation")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<UserReputationResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetUserReputation([FromRoute] string userId)
    {
        UserValidation.EnsureUuid(userId, "UserId");

        return ForwardAsync(
            Service,
            $"/api/v1/user/{userId}/reputation",
            HttpMethod.Get,
            mockSeed: "user-reputation",
            successMessage: "User reputation retrieved successfully."
        );
    }

    [HttpGet("user/{userId}/reputation/history")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<ReputationHistoryResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetUserReputationHistory([FromRoute] string userId, [FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        UserValidation.EnsureUuid(userId, "UserId");
        UserValidation.EnsurePaged(page, limit);

        return ForwardPagedAsync(
            Service,
            $"/api/v1/user/{userId}/reputation/history",
            page,
            limit,
            HttpMethod.Get,
            mockSeed: "user-reputation-history",
            queryParams: new Dictionary<string, string?>
            {
                ["page"] = page.ToString(),
                ["limit"] = limit.ToString()
            }
        );
    }

    [HttpGet("user/{userId}/reviews")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<UserReviewListResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetUserReviews(
        [FromRoute] string userId,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] int filterRating = 0)
    {
        UserValidation.EnsureUuid(userId, "UserId");
        UserValidation.EnsurePaged(page, limit);

        return ForwardAsync(
            Service,
            $"/api/v1/user/{userId}/reviews",
            HttpMethod.Get,
            mockSeed: "user-reviews",
            successMessage: "User reviews retrieved successfully.",
            queryParams: new Dictionary<string, string?>
            {
                ["page"] = page.ToString(),
                ["limit"] = limit.ToString(),
                ["filterRating"] = filterRating.ToString()
            }
        );
    }

    [HttpPost("review/files/upload")]
    [Authorize(GatewayPolicies.Authenticated)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<UploadFilesResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> UploadReviewFiles([FromForm] UploadMultipleFilesForm form)
    {
        var fileNames = form.Files?.Select(f => f.FileName).ToList() ?? ["review_file_1.jpg"];

        return ForwardAsync(
            Service,
            "/api/v1/review/files/upload",
            HttpMethod.Post,
            body: new { fileNames },
            mockSeed: "review-files-upload",
            successMessage: "Files uploaded successfully."
        );
    }

    [HttpPost("reviews")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<ReviewCreatedResponseDto>), StatusCodes.Status201Created)]
    public Task<IResult> CreateReview([FromBody] CreateReviewDto body)
    {
        UserValidation.EnsureRequired(body.TransactionType, "TransactionType");
        UserValidation.EnsureRequired(body.TransactionId, "TransactionId");
        UserValidation.EnsureUuid(body.TargetUserId, "TargetUserId");
        UserValidation.EnsureRating(body.Rating);
        UserValidation.EnsureRequired(body.Comment, "Comment");

        return ForwardAsync(
            Service,
            "/api/v1/reviews",
            HttpMethod.Post,
            body: body,
            mockSeed: "review-create",
            successStatusCode: StatusCodes.Status201Created,
            successMessage: "Review submitted successfully."
        );
    }

    [HttpPost("report/files/upload")]
    [Authorize(GatewayPolicies.Authenticated)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<UploadFilesResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> UploadReportFiles([FromForm] UploadMultipleFilesForm form)
    {
        var fileNames = form.Files?.Select(f => f.FileName).ToList() ?? ["report_evidence_1.jpg"];

        return ForwardAsync(
            Service,
            "/api/v1/report/files/upload",
            HttpMethod.Post,
            body: new { fileNames },
            mockSeed: "report-files-upload",
            successMessage: "Report evidence files uploaded successfully."
        );
    }

    [HttpPost("user/reports")]
    [HttpPost("users/reports")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<UserReportCreatedResponseDto>), StatusCodes.Status201Created)]
    public Task<IResult> CreateUserReport([FromBody] CreateUserReportDto body)
    {
        UserValidation.EnsureUuid(body.ReportedUserId, "ReportedUserId");
        UserValidation.EnsureRequired(body.ViolationType, "ViolationType");
        UserValidation.EnsureRequired(body.Description, "Description");

        return ForwardAsync(
            Service,
            "/api/v1/users/reports",
            HttpMethod.Post,
            body: body,
            mockSeed: "user-report-create",
            successStatusCode: StatusCodes.Status201Created,
            successMessage: "Report submitted successfully."
        );
    }

    [HttpGet("me/reports")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<MyReportListResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> ListMyReports(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string status = "")
    {
        UserValidation.EnsurePaged(page, limit);

        return ForwardPagedAsync(
            Service,
            "/api/v1/me/reports",
            page,
            limit,
            HttpMethod.Get,
            mockSeed: "user-my-reports",
            queryParams: new Dictionary<string, string?>
            {
                ["page"] = page.ToString(),
                ["limit"] = limit.ToString(),
                ["status"] = status
            }
        );
    }

    [HttpPost("seller/registration")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<SellerRegistrationCreatedResponseDto>), StatusCodes.Status201Created)]
    public Task<IResult> CreateSellerRegistration([FromBody] CreateSellerRegistrationDto body)
    {
        UserValidation.EnsureRequired(body.ShopName, "ShopName", "Shop name");
        UserValidation.EnsureEmail(body.BusinessEmail, "BusinessEmail");
        UserValidation.EnsureRequired(body.Hotline, "Hotline");
        UserValidation.EnsureRequired(body.IdentityCardNumber, "IdentityCardNumber", "Identity card number");

        return ForwardAsync(
            Service,
            "/api/v1/seller/registration",
            HttpMethod.Post,
            body: body,
            mockSeed: "seller-registration-create",
            successStatusCode: StatusCodes.Status201Created,
            successMessage: "Seller registration submitted successfully."
        );
    }

    [HttpPost("seller/registration/otp/resend")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<SellerRegistrationOtpResendResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> ResendSellerRegistrationOtp([FromBody] ResendSellerRegistrationOtpDto body)
    {
        UserValidation.EnsureRequired(body.RegistrationId, "RegistrationId");
        UserValidation.EnsureEmail(body.BusinessEmail, "BusinessEmail");

        return ForwardAsync(
            Service,
            "/api/v1/seller/registration/otp/resend",
            HttpMethod.Post,
            body: body,
            mockSeed: "seller-registration-otp-resend",
            successMessage: "Verification OTP has been resent to business email."
        );
    }

    [HttpPost("seller/registration/documents")]
    [Authorize(GatewayPolicies.Authenticated)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<UploadFilesResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> UploadSellerDocuments([FromForm] UploadSellerDocumentsForm form)
    {
        var registrationId = UserValidation.EnsureRequired(form.RegistrationId, "RegistrationId");
        var fileNames = form.Files?.Select(f => f.FileName).ToList() ?? ["id_front.jpg", "id_back.jpg"];

        return ForwardAsync(
            Service,
            "/api/v1/seller/registration/documents",
            HttpMethod.Post,
            body: new { registrationId, fileNames },
            mockSeed: "seller-registration-documents",
            successMessage: "Seller registration documents uploaded successfully."
        );
    }

    [HttpPost("seller/otp/verify")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<VerifySellerOtpResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> VerifySellerOtp([FromBody] VerifySellerOtpDto body)
    {
        UserValidation.EnsureRequired(body.RegistrationId, "RegistrationId");
        UserValidation.EnsureOtp(body.OtpCode);

        return ForwardAsync(
            Service,
            "/api/v1/seller/otp/verify",
            HttpMethod.Post,
            body: body,
            mockSeed: "seller-otp-verify",
            successMessage: "Seller registration email verified successfully."
        );
    }

    [HttpGet("seller/registration")]
    [Authorize(GatewayPolicies.Authenticated)]
    [ProducesResponseType(typeof(ApiResponse<SellerRegistrationStatusResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetSellerRegistrationStatus()
    {
        return ForwardAsync(
            Service,
            "/api/v1/seller/registration",
            HttpMethod.Get,
            mockSeed: "seller-registration-status",
            successMessage: "Seller registration status retrieved successfully."
        );
    }

    [HttpGet("seller/profile")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<SellerOwnProfileResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> GetSellerOwnProfile()
    {
        return ForwardAsync(
            Service,
            "/api/v1/seller/profile",
            HttpMethod.Get,
            mockSeed: "seller-own-profile",
            successMessage: "Seller profile retrieved successfully."
        );
    }

    [HttpPatch("seller/profile")]
    [Authorize(GatewayPolicies.Seller)]
    [ProducesResponseType(typeof(ApiResponse<SellerOwnProfileUpdatedResponseDto>), StatusCodes.Status200OK)]
    public Task<IResult> UpdateSellerOwnProfile([FromBody] UpdateSellerOwnProfileDto body)
    {
        return ForwardAsync(
            Service,
            "/api/v1/seller/profile",
            HttpMethod.Patch,
            body: body,
            mockSeed: "seller-own-profile-update",
            successMessage: "Seller profile updated successfully."
        );
    }
}
