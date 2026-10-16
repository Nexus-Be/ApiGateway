namespace Nexus.Gateway.Common;

// HTTP query contract only. Keep the existing per-service gRPC request types and tag numbers.
public sealed class PaginationRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public bool IsValid => ValidationHelpers.PageOk(Page, PageSize);

    public void EnsureValid()
    {
        if (!IsValid)
            throw new GatewayInputException(ValidationHelpers.PageMessage, "Pagination");
    }
}
