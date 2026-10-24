namespace Nexus.Gateway.Common;

using System.Globalization;
using Nexus.Gateway.Dtos.Auction;

/// <summary>
/// Stateless input checks for the Auction HTTP contract. Business rules that depend on data or
/// configuration (30-day window, minimum duration, bid increments, ownership...) stay in AuctionService.
/// </summary>
public static class AuctionValidation
{
    private static readonly string[] Statuses =
        ["DRAFT", "SCHEDULED", "RUNNING", "ENDED", "SETTLED", "FAILED", "CANCELLED"];

    public static void EnsureAuctionId(string? value, string field = "auctionId")
    {
        if (!Guid.TryParse(value, out _))
            throw new GatewayInputException("Auction ID must be a valid UUID.", field);
    }

    public static void EnsureGuid(string? value, string field, string label)
    {
        if (!Guid.TryParse(value, out _))
            throw new GatewayInputException($"{label} must be a valid UUID.", field);
    }

    public static void EnsureStatus(string? status)
    {
        if (status is null) return;
        if (!Statuses.Contains(status, StringComparer.Ordinal))
            throw new GatewayInputException(
                "Status must be one of DRAFT, SCHEDULED, RUNNING, ENDED, SETTLED, FAILED or CANCELLED.", "status");
    }

    public static void EnsureCreate(CreateAuctionRequestDto body)
    {
        EnsureGuid(body.ProductId, "productId", "Product ID");
        EnsureGuid(body.SkuId, "skuId", "SKU ID");
        EnsureStartPrice(body.StartPriceAmount, required: true);
        EnsureBidStep(body.BidStepAmount);
        EnsureSchedule(body.ScheduledStartAt, body.ScheduledEndAt, required: true);
    }

    public static void EnsureUpdate(UpdateAuctionRequestDto body)
    {
        if (body.StartPriceAmount is null && body.BidStepAmount is null &&
            body.ScheduledStartAt is null && body.ScheduledEndAt is null)
            throw new GatewayInputException("At least one field must be provided.", "body");

        EnsureStartPrice(body.StartPriceAmount, required: false);
        EnsureBidStep(body.BidStepAmount);
        EnsureSchedule(body.ScheduledStartAt, body.ScheduledEndAt, required: false);
    }

    public static void EnsureRecreate(RecreateAuctionRequestDto body)
    {
        EnsureStartPrice(body.StartPriceAmount, required: true);
        EnsureBidStep(body.BidStepAmount);
        EnsureSchedule(body.ScheduledStartAt, body.ScheduledEndAt, required: true);
    }

    public static void EnsureOptionalReason(string? reason)
    {
        // Reason is optional, but when it is sent it must not be blank.
        if (reason is not null && string.IsNullOrWhiteSpace(reason))
            throw new GatewayInputException("Reason must not be empty.", "reason");
    }

    public static void EnsureRequiredReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new GatewayInputException("Reason is required.", "reason");
    }

    public static void EnsureBidAmount(decimal? amount)
    {
        if (amount is null || amount <= 0m || amount != decimal.Round(amount.Value, 2))
            throw new GatewayInputException(
                "Amount must be greater than zero and have at most two decimal places.", "amount");
    }

    public static void EnsureBidHeaders(string? idempotencyKey, string? ifMatch)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new GatewayInputException("Idempotency-Key header is required.", "Idempotency-Key");
        if (string.IsNullOrWhiteSpace(ifMatch))
            throw new GatewayInputException("If-Match header is required.", "If-Match");
    }

    public static void EnsureConfig(string? key, System.Text.Json.JsonElement? value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new GatewayInputException("Config key is required.", "key");
        if (value is null || value.Value.ValueKind is System.Text.Json.JsonValueKind.Undefined or System.Text.Json.JsonValueKind.Null)
            throw new GatewayInputException("Config value is required.", "configValue");
    }

    public static string ToIso(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static void EnsureStartPrice(decimal? value, bool required)
    {
        if (value is null)
        {
            if (required) throw new GatewayInputException("Start price is required.", "startPriceAmount");
            return;
        }

        if (value < 0m || value != decimal.Round(value.Value, 2))
            throw new GatewayInputException(
                "Start price must be zero or greater and have at most two decimal places.", "startPriceAmount");
    }

    private static void EnsureBidStep(decimal? value)
    {
        if (value is null) return;
        if (value <= 0m || value != decimal.Round(value.Value, 2))
            throw new GatewayInputException(
                "Bid step must be greater than zero and have at most two decimal places.", "bidStepAmount");
    }

    private static void EnsureSchedule(DateTimeOffset? start, DateTimeOffset? end, bool required)
    {
        if (required)
        {
            if (start is null) throw new GatewayInputException("Scheduled start time is required.", "scheduledStartAt");
            if (end is null) throw new GatewayInputException("Scheduled end time is required.", "scheduledEndAt");
        }

        if (start is not null && start <= DateTimeOffset.UtcNow)
            throw new GatewayInputException("Scheduled start time must be in the future.", "scheduledStartAt");

        if (start is not null && end is not null && end <= start)
            throw new GatewayInputException("Scheduled end time must be after the scheduled start time.", "scheduledEndAt");
    }
}
