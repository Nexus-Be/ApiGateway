namespace Nexus.Gateway.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Gateway.Common;
using Nexus.Gateway.Dispatching;
using Nexus.Gateway.Dtos.Notification;
using Nexus.Gateway.Responses;
using Nexus.Gateway.Security;
using ShoppingCart.Contracts.V1;

[ApiController]
[Route("api/v1/notifications")]
[BackendTag("notification")]
[EnableRateLimiting("api")]
[Authorize("authenticated")]
public sealed class NotificationController : ControllerBase
{
    private readonly IGatewayDispatcher _dispatcher;
    private readonly NotificationApi.NotificationApiClient _client;

    public NotificationController(IGatewayDispatcher dispatcher, NotificationApi.NotificationApiClient client)
    {
        _dispatcher = dispatcher;
        _client = client;
    }

    [HttpGet("")]
    [ProducesResponseType(typeof(ApiResponse<NotificationPage>), StatusCodes.Status200OK)]
    public Task<IResult> ListNotifications()
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "notification",
            "notifications",
            o => _client.ListNotificationsAsync(HttpContext.ToUserRequest(), o),
            NotificationPage.Parser,
            successMessage: "Notifications retrieved."
        );
    }

    [HttpPut("{id:guid}/read")]
    [ProducesResponseType(typeof(ApiResponse<Notification>), StatusCodes.Status200OK)]
    public Task<IResult> MarkNotificationRead([FromRoute] Guid id)
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "notification",
            "notification-read",
            o => _client.MarkNotificationReadAsync(new IdRequest { Id = id.ToString() }, o),
            Notification.Parser,
            successMessage: "Notification marked as read."
        );
    }

    [HttpGet("preferences")]
    [ProducesResponseType(typeof(ApiResponse<NotificationPreferences>), StatusCodes.Status200OK)]
    public Task<IResult> GetPreferences()
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "notification",
            "preferences",
            o => _client.GetPreferencesAsync(HttpContext.ToUserRequest(), o),
            NotificationPreferences.Parser,
            successMessage: "Preferences retrieved."
        );
    }

    [HttpPut("preferences")]
    [ProducesResponseType(typeof(ApiResponse<NotificationPreferences>), StatusCodes.Status200OK)]
    public Task<IResult> UpdatePreferences([FromBody] UpdatePreferencesRequestDto body)
    {
        return _dispatcher.DispatchAsync(
            HttpContext,
            "notification",
            "preferences-update",
            o => _client.UpdatePreferencesAsync(new UpdateNotificationPreferencesRequest
            {
                UserId = HttpContext.ToUserRequest().UserId,
                EmailEnabled = body.EmailEnabled,
                PushEnabled = body.PushEnabled,
                OrderUpdates = body.OrderUpdates,
                AuctionAlerts = body.AuctionAlerts
            }, o),
            NotificationPreferences.Parser,
            successMessage: "Preferences updated."
        );
    }
}
