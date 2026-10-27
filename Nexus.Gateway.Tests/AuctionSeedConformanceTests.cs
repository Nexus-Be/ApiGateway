namespace Nexus.Gateway.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;

/// <summary>
/// Every auction operation resolves its response from <c>Seeds/auction/{operation}.json</c>.
/// These tests verify that every seed file exists, is valid JSON, and has valid structural content.
/// </summary>
public sealed class AuctionSeedConformanceTests
{
    public static IEnumerable<object[]> Seeds() =>
    [
        ["auctions"],
        ["seller-auctions"],
        ["auction"],
        ["auction-bids"],
        ["me-bids"],
        ["auction-bid-place"],
        ["auction-result"],
        ["auction-create"],
        ["auction-update"],
        ["auction-cancel"],
        ["auction-recreate"],
        ["auction-status-history"],
        ["auction-force-cancel"],
        ["auction-saga"],
        ["auction-configs"],
        ["auction-config-update"]
    ];

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Seed_is_valid_json_structure(string operation)
    {
        var path = SeedsDirectory() is { } dir
            ? Path.Combine(dir, $"{operation}.json")
            : throw new DirectoryNotFoundException("Seeds/auction was not found from the test output directory.");

        Assert.True(File.Exists(path), $"Missing seed file: {path}");

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.ValueKind == JsonValueKind.Object || doc.RootElement.ValueKind == JsonValueKind.Array);
    }

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
