using System.Text.Json;
using Nexus.Gateway.Responses;
using Xunit;

namespace Nexus.Gateway.Tests;

public class ApiResponseFactoryTests
{
    [Fact]
    public void CreateSuccess_Produces_Valid_Structure()
    {
        var factory = new ApiResponseFactory();
        var data = new { id = "usr_9981", fullName = "Nguyen Van A", email = "vana@example.com", status = "Active" };
        var response = factory.CreateSuccess(data, "User retrieved successfully.", 200, "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

        Assert.True(response.Success);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("User retrieved successfully.", response.Message);
        Assert.NotNull(response.Data);
        Assert.Equal("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01", response.TraceId);

        var json = JsonSerializer.Serialize(response);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(200, root.GetProperty("statusCode").GetInt32());
        Assert.Equal("User retrieved successfully.", root.GetProperty("message").GetString());
        Assert.Equal("usr_9981", root.GetProperty("data").GetProperty("id").GetString());
        Assert.Equal("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01", root.GetProperty("traceId").GetString());
    }

    [Fact]
    public void CreatePaged_Produces_Valid_Pagination_Structure()
    {
        var factory = new ApiResponseFactory();
        var items = new[]
        {
            new { id = 101, orderCode = "ORD-001", amount = 250000 },
            new { id = 102, orderCode = "ORD-002", amount = 500000 }
        };

        var response = factory.CreatePaged(items, pageIndex: 1, pageSize: 20, totalCount: 245, statusCode: 200, traceId: "test-trace-id");

        Assert.True(response.Success);
        Assert.Equal(200, response.StatusCode);
        Assert.Null(response.Message);
        Assert.Equal(2, response.Data!.Items.Count);
        Assert.Equal(1, response.Data.Pagination.PageIndex);
        Assert.Equal(20, response.Data.Pagination.PageSize);
        Assert.Equal(245, response.Data.Pagination.TotalCount);
        Assert.Equal(13, response.Data.Pagination.TotalPages);
        Assert.False(response.Data.Pagination.HasPreviousPage);
        Assert.True(response.Data.Pagination.HasNextPage);

        var json = JsonSerializer.Serialize(response);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Verify message is omitted when null
        Assert.False(root.TryGetProperty("message", out _));

        var pagination = root.GetProperty("data").GetProperty("pagination");
        Assert.Equal(1, pagination.GetProperty("pageIndex").GetInt32());
        Assert.Equal(20, pagination.GetProperty("pageSize").GetInt32());
        Assert.Equal(245, pagination.GetProperty("totalCount").GetInt32());
        Assert.Equal(13, pagination.GetProperty("totalPages").GetInt32());
        Assert.False(pagination.GetProperty("hasPreviousPage").GetBoolean());
        Assert.True(pagination.GetProperty("hasNextPage").GetBoolean());
    }

    [Fact]
    public void CreateFailure_Produces_Valid_ProblemDetails_Structure()
    {
        var factory = new ApiResponseFactory();
        var errors = new Dictionary<string, string[]>
        {
            ["Email"] = ["The Email field is not a valid e-mail address."],
            ["Password"] = [
                "The Password must be at least 8 characters long.",
                "The Password must contain at least one uppercase letter."
            ]
        };

        var error = factory.CreateFailure(
            title: "One or more validation errors occurred.",
            status: 400,
            detail: "Please refer to the errors property for additional details.",
            errorCode: "VAL_INVALID_INPUT",
            type: "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            errors: errors,
            traceId: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
        );

        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.1", error.Type);
        Assert.Equal("One or more validation errors occurred.", error.Title);
        Assert.Equal(400, error.Status);
        Assert.Equal("VAL_INVALID_INPUT", error.ErrorCode);
        Assert.Equal("Please refer to the errors property for additional details.", error.Detail);
        Assert.NotNull(error.Errors);
        Assert.Equal("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01", error.TraceId);

        var json = JsonSerializer.Serialize(error);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.1", root.GetProperty("type").GetString());
        Assert.Equal("One or more validation errors occurred.", root.GetProperty("title").GetString());
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal("VAL_INVALID_INPUT", root.GetProperty("errorCode").GetString());
        Assert.Equal(1, root.GetProperty("errors").GetProperty("Email").GetArrayLength());
        Assert.Equal(2, root.GetProperty("errors").GetProperty("Password").GetArrayLength());
    }
}
