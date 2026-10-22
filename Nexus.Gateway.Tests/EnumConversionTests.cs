namespace Nexus.Gateway.Tests;

using System;
using System.Text.Json;
using Nexus.Gateway.Common.Converters;
using Xunit;

public sealed class EnumConversionTests
{
    public enum TestOrderStatus
    {
        PendingPayment = 0,
        Paid = 1,
        Confirmed = 2,
        Shipping = 3,
        Delivered = 4,
        Completed = 5,
        Cancelled = 6
    }

    public sealed class TestOrderDto
    {
        public string OrderId { get; set; } = string.Empty;
        public TestOrderStatus Status { get; set; }
        public TestOrderStatus? SecondaryStatus { get; set; }
    }

    private readonly JsonSerializerOptions _options;

    public EnumConversionTests()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        _options.Converters.Add(new PascalCaseJsonStringEnumConverter(allowIntegerValues: false));
    }

    [Fact]
    public void Serializes_enum_to_exact_pascal_case_string()
    {
        var model = new TestOrderDto
        {
            OrderId = "ORD-001",
            Status = TestOrderStatus.PendingPayment,
            SecondaryStatus = TestOrderStatus.Confirmed
        };

        var json = JsonSerializer.Serialize(model, _options);

        // Đảm bảo enum được chuyển thành chuỗi PascalCase
        Assert.Contains("\"status\":\"PendingPayment\"", json);
        Assert.Contains("\"secondaryStatus\":\"Confirmed\"", json);
    }

    [Theory]
    [InlineData("PendingPayment", TestOrderStatus.PendingPayment)]
    [InlineData("pendingPayment", TestOrderStatus.PendingPayment)] // camelCase
    [InlineData("PAID", TestOrderStatus.Paid)] // UPPERCASE
    [InlineData("Confirmed", TestOrderStatus.Confirmed)]
    [InlineData("shipping", TestOrderStatus.Shipping)]
    public void Deserializes_various_casing_to_correct_enum_value(string inputString, TestOrderStatus expected)
    {
        var json = $"{{\"orderId\":\"ORD-002\",\"status\":\"{inputString}\"}}";
        var model = JsonSerializer.Deserialize<TestOrderDto>(json, _options);

        Assert.NotNull(model);
        Assert.Equal(expected, model.Status);
    }

    [Fact]
    public void Handles_null_nullable_enum()
    {
        var json = "{\"orderId\":\"ORD-003\",\"status\":\"Paid\",\"secondaryStatus\":null}";
        var model = JsonSerializer.Deserialize<TestOrderDto>(json, _options);

        Assert.NotNull(model);
        Assert.Equal(TestOrderStatus.Paid, model.Status);
        Assert.Null(model.SecondaryStatus);

        var serialized = JsonSerializer.Serialize(model, _options);
        Assert.Contains("\"secondaryStatus\":null", serialized);
    }

    [Fact]
    public void Throws_descriptive_JsonException_when_invalid_string_provided()
    {
        var json = "{\"orderId\":\"ORD-004\",\"status\":\"NonExistentStatus\"}";

        var ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TestOrderDto>(json, _options));

        Assert.Contains("Invalid value 'NonExistentStatus' for enum 'TestOrderStatus'", ex.Message);
        Assert.Contains("Valid values are: PendingPayment, Paid, Confirmed, Shipping, Delivered, Completed, Cancelled", ex.Message);
    }

    [Fact]
    public void Rejects_raw_numbers_when_allow_integers_is_false()
    {
        var json = "{\"orderId\":\"ORD-005\",\"status\":1}";

        var ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TestOrderDto>(json, _options));
        Assert.Contains("Expected string value for enum 'TestOrderStatus', but got Number", ex.Message);
    }
}
