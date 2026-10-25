using Google.Protobuf;
using ShoppingCart.Contracts.V1;
using Xunit;

namespace Nexus.Gateway.Tests;

/// <summary>
/// Every auction operation resolves its response from <c>Seeds/auction/{operation}.json</c>.
/// The paged path parses the seed against the protobuf contract and fails the request when the shape
/// is wrong, while the single-object path silently falls back to the raw JSON
/// (MockDataMapper.MapMockAsync) — so a drifted seed would ship without failing anything at runtime.
/// These tests pin every seed to its contract message.
/// </summary>
public sealed class AuctionSeedConformanceTests
{
    public static IEnumerable<object[]> Seeds() =>
    [
        ["auctions", "list"],
        ["seller-auctions", "list"],
        ["auction", "detail"],
        ["auction-bids", "bids"],
        ["me-bids", "my-bids"],
        ["auction-bid-place", "bid-place"],
        ["auction-result", "result"],
        ["auction-create", "created"],
        ["auction-update", "updated"],
        ["auction-cancel", "cancel"],
        ["auction-recreate", "recreated"],
        ["auction-status-history", "status-history"],
        ["auction-force-cancel", "force-cancel"],
        ["auction-saga", "saga"],
        ["auction-configs", "config-list"],
        ["auction-config-update", "config"]
    ];

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Seed_parses_with_its_contract_message(string operation, string shape)
    {
        var path = SeedsDirectory() is { } dir
            ? Path.Combine(dir, $"{operation}.json")
            : throw new DirectoryNotFoundException("Seeds/auction was not found from the test output directory.");

        Assert.True(File.Exists(path), $"Missing seed file: {path}");

        var json = File.ReadAllText(path);
        var message = Parse(shape, json);

        var canonical = JsonFormatter.Default.Format(message);
        Assert.False(string.IsNullOrWhiteSpace(canonical));
    }

    private static IMessage Parse(string shape, string json) => shape switch
    {
        "list" => AuctionListResponse.Parser.ParseJson(json),
        "detail" => AuctionDetailResponse.Parser.ParseJson(json),
        "bids" => BidListResponse.Parser.ParseJson(json),
        "my-bids" => MyBidListResponse.Parser.ParseJson(json),
        "bid-place" => PlaceBidResponse.Parser.ParseJson(json),
        "result" => AuctionResultResponse.Parser.ParseJson(json),
        "created" => AuctionCreatedResponse.Parser.ParseJson(json),
        "updated" => AuctionUpdatedResponse.Parser.ParseJson(json),
        "cancel" => AuctionCancelResponse.Parser.ParseJson(json),
        "recreated" => AuctionRecreatedResponse.Parser.ParseJson(json),
        "status-history" => AuctionStatusHistoryResponse.Parser.ParseJson(json),
        "force-cancel" => AuctionForceCancelResponse.Parser.ParseJson(json),
        "saga" => AuctionSagaResponse.Parser.ParseJson(json),
        "config-list" => AuctionConfigListResponse.Parser.ParseJson(json),
        "config" => AuctionConfigResponse.Parser.ParseJson(json),
        _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unknown seed shape.")
    };

    /// <summary>
    /// Seeds are copied to the content root at runtime; walk up from the test binaries to the source folder
    /// so the guard also covers the files a reviewer edits.
    /// </summary>
    private static string? SeedsDirectory()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(start);
            while (directory != null)
            {
                foreach (var candidate in new[]
                         {
                             Path.Combine(directory.FullName, "Nexus.Gateway", "Seeds", "auction"),
                             Path.Combine(directory.FullName, "Seeds", "auction")
                         })
                {
                    if (Directory.Exists(candidate)) return candidate;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }
}
