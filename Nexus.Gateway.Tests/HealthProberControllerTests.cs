namespace Nexus.Gateway.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Gateway.HealthProber;
using Xunit;

public sealed class HealthProberControllerTests
{
    [Fact]
    public async Task Get_prober_status_requires_admin_and_returns_all_8_services()
    {
        using var app = new Factory();
        using var client = app.CreateClient();

        // 1. Không có token -> 401 Unauthorized
        var unauthResp = await client.GetAsync("/api/v1/gateway/prober/status");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthResp.StatusCode);

        // 2. Token buyer -> 403 Forbidden
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Factory.TokenFor("buyer1", "buyer"));
        var forbiddenResp = await client.GetAsync("/api/v1/gateway/prober/status");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResp.StatusCode);

        // 3. Token admin -> 200 OK
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Factory.TokenFor("admin1", "admin"));
        var okResp = await client.GetAsync("/api/v1/gateway/prober/status");
        Assert.Equal(HttpStatusCode.OK, okResp.StatusCode);

        var content = await okResp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());

        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(8, data.GetArrayLength());
    }

    [Fact]
    public async Task Toggle_and_bulk_toggle_prober_daemons()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Factory.TokenFor("admin1", "admin"));

        // 1. Bật catalog prober
        var enableResp = await client.PutAsJsonAsync("/api/v1/gateway/prober/catalog", new { enabled = true, intervalSeconds = 30 });
        Assert.Equal(HttpStatusCode.OK, enableResp.StatusCode);

        // 2. Kiểm tra status của catalog
        var checkResp = await client.GetAsync("/api/v1/gateway/prober/status/catalog");
        Assert.Equal(HttpStatusCode.OK, checkResp.StatusCode);
        var checkContent = await checkResp.Content.ReadAsStringAsync();
        using var checkDoc = JsonDocument.Parse(checkContent);
        Assert.True(checkDoc.RootElement.GetProperty("data").GetProperty("isEnabled").GetBoolean());

        // 3. Tắt catalog prober
        var disableResp = await client.PutAsJsonAsync("/api/v1/gateway/prober/catalog", new { enabled = false });
        Assert.Equal(HttpStatusCode.OK, disableResp.StatusCode);

        // 4. Bulk enable all
        var bulkResp = await client.PutAsJsonAsync("/api/v1/gateway/prober/bulk", new { enableAll = true });
        Assert.Equal(HttpStatusCode.OK, bulkResp.StatusCode);

        var allResp = await client.GetAsync("/api/v1/gateway/prober/status");
        var allContent = await allResp.Content.ReadAsStringAsync();
        using var allDoc = JsonDocument.Parse(allContent);
        foreach (var svc in allDoc.RootElement.GetProperty("data").EnumerateArray())
        {
            Assert.True(svc.GetProperty("isEnabled").GetBoolean());
        }
    }

    [Fact]
    public async Task Manual_health_check_endpoint_executes_probe()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Factory.TokenFor("admin1", "admin"));

        // Gọi API check thủ công trên service catalog
        var resp = await client.PostAsync("/api/v1/gateway/prober/catalog/check", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var content = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        var data = doc.RootElement.GetProperty("data");
        Assert.True(data.TryGetProperty("isSuccess", out _));
        Assert.True(data.TryGetProperty("latencyMs", out _));
    }

    [Fact]
    public async Task Circuit_breaker_fast_fails_when_service_is_unhealthy()
    {
        using var app = new Factory();
        var proberManager = app.Services.GetRequiredService<IHealthProberManager>();

        // Giả lập service users bị đánh dấu Unhealthy
        var userState = proberManager.GetState("users")!;
        userState.Status = ProberRunStatus.Unhealthy;

        using var client = app.CreateClient();
        using var realReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { username = "testuser", password = "Password123!" })
        };
        realReq.Headers.Add("X-Mock-Mode", "Real");

        var response = await client.SendAsync(realReq);

        // Circuit breaker ngắt ngay lập tức với HTTP 503 và errorCode: DOWNSTREAM_UNHEALTHY
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("DOWNSTREAM_UNHEALTHY", doc.RootElement.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Database_prober_status_toggle_and_manual_check()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Factory.TokenFor("admin1", "admin"));

        // 1. Get all databases status
        var allResp = await client.GetAsync("/api/v1/gateway/prober/databases/status");
        Assert.Equal(HttpStatusCode.OK, allResp.StatusCode);
        var allContent = await allResp.Content.ReadAsStringAsync();
        using var allDoc = JsonDocument.Parse(allContent);
        Assert.True(allDoc.RootElement.GetProperty("success").GetBoolean());
        var allData = allDoc.RootElement.GetProperty("data");
        Assert.Equal(8, allData.GetArrayLength());

        // 2. Toggle database for catalog
        var toggleResp = await client.PutAsJsonAsync("/api/v1/gateway/prober/databases/catalog", new
        {
            enabled = true,
            connectionString = "Host=ep-cool-fog.ap-southeast-1.aws.neon.tech;Database=catalog_db;"
        });
        Assert.Equal(HttpStatusCode.OK, toggleResp.StatusCode);

        // 3. Get single database status
        var singleResp = await client.GetAsync("/api/v1/gateway/prober/databases/status/catalog");
        Assert.Equal(HttpStatusCode.OK, singleResp.StatusCode);
        var singleContent = await singleResp.Content.ReadAsStringAsync();
        using var singleDoc = JsonDocument.Parse(singleContent);
        Assert.True(singleDoc.RootElement.GetProperty("data").GetProperty("isEnabled").GetBoolean());

        // 4. Manual database check
        var checkResp = await client.PostAsync("/api/v1/gateway/prober/databases/catalog/check", null);
        Assert.Equal(HttpStatusCode.OK, checkResp.StatusCode);
        var checkContent = await checkResp.Content.ReadAsStringAsync();
        using var checkDoc = JsonDocument.Parse(checkContent);
        Assert.True(checkDoc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("healthy", checkDoc.RootElement.GetProperty("data").GetProperty("status").GetString());
    }

    [Fact]
    public async Task CheckHealService_self_probe_endpoints()
    {
        using var app = new Factory();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Factory.TokenFor("admin1", "admin"));

        // 1. CheckHealService operational status
        var statusResp = await client.GetAsync("/api/v1/gateway/prober/healthcheck-service/status");
        Assert.Equal(HttpStatusCode.OK, statusResp.StatusCode);
        var statusContent = await statusResp.Content.ReadAsStringAsync();
        using var statusDoc = JsonDocument.Parse(statusContent);
        Assert.True(statusDoc.RootElement.GetProperty("success").GetBoolean());
        Assert.True(statusDoc.RootElement.GetProperty("data").GetProperty("isHealthy").GetBoolean());

        // 2. Active probe to CheckHealService
        var probeResp = await client.PostAsync("/api/v1/gateway/prober/healthcheck-service/check", null);
        Assert.Equal(HttpStatusCode.OK, probeResp.StatusCode);
        var probeContent = await probeResp.Content.ReadAsStringAsync();
        using var probeDoc = JsonDocument.Parse(probeContent);
        Assert.True(probeDoc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("healthy", probeDoc.RootElement.GetProperty("data").GetProperty("status").GetString());
    }
}
