using Nexus.Gateway.Common;
using Xunit;

namespace Nexus.Gateway.Tests;

public sealed class PaginationRequestBindingTests
{
    [Fact]
    public void Pagination_request_has_correct_defaults()
    {
        var request = new PaginationRequest();
        Assert.Equal(1, request.Page);
        Assert.Equal(20, request.PageSize);
        Assert.True(request.IsValid);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 50)]
    [InlineData(10, 100)]
    public void Pagination_request_accepts_valid_bounds(int page, int pageSize)
    {
        var request = new PaginationRequest { Page = page, PageSize = pageSize };
        Assert.True(request.IsValid);
        request.EnsureValid(); // Should not throw
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(1, -5)]
    public void Pagination_request_rejects_invalid_bounds(int page, int pageSize)
    {
        var request = new PaginationRequest { Page = page, PageSize = pageSize };
        Assert.False(request.IsValid);
        var ex = Assert.Throws<GatewayInputException>(() => request.EnsureValid());
        Assert.Equal("VAL_INVALID_INPUT", ex.ErrorCode);
    }
}
