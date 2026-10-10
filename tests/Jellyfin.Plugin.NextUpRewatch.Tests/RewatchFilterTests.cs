using System;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Jellyfin.Data.Entities;
using Jellyfin.Plugin.NextUpRewatch.Api;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.NextUpRewatch.Tests;

public class RewatchFilterTests
{
    private readonly Mock<ISeriesRewatchEvaluator> _evaluatorMock = new();
    private readonly Mock<IUserManager> _userManagerMock = new();
    private readonly Mock<ILogger<NextUpRewatchMiddleware>> _loggerMock = new();

    private NextUpRewatchMiddleware CreateMiddleware()
    {
        RequestDelegate next = (HttpContext ctx) => Task.CompletedTask;
        return new NextUpRewatchMiddleware(
            next,
            _loggerMock.Object,
            _evaluatorMock.Object,
            _userManagerMock.Object);
    }

    private User CreateTestUser()
    {
        return new User("TestUser", "password", "salt");
    }

    [Fact]
    public async Task FilterNextUpResponse_KeepsUnplayedEpisodes_Nominal()
    {
        // Nominal case: An episode that has never been played (Played == false) is kept intact
        var middleware = CreateMiddleware();
        var user = CreateTestUser();

        var json = """
        {
            "Items": [
                {
                    "Name": "Episode 1",
                    "SeriesName": "New Show",
                    "SeriesId": "36e2c359-c8f2-06df-ccb2-2fab2b79cabd",
                    "UserData": {
                        "Played": false,
                        "LastPlayedDate": null
                    }
                }
            ],
            "TotalRecordCount": 1
        }
        """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var resultBytes = await middleware.FilterNextUpResponseAsync(stream, user, debugLogging: false);
        var resultJson = Encoding.UTF8.GetString(resultBytes);

        var node = JsonNode.Parse(resultJson);
        Assert.NotNull(node);
        var items = node["Items"]?.AsArray();
        Assert.NotNull(items);
        Assert.Single(items);
        Assert.Equal("Episode 1", items[0]?["Name"]?.ToString());
        Assert.Equal(1, node["TotalRecordCount"]?.GetValue<int>());
    }

    [Fact]
    public async Task FilterNextUpResponse_FiltersStalledCompletedEpisode_WhenEpPlayedEqualsSeriesMax()
    {
        // Stalled case: EpLastPlayed == SeriesMaxLastPlayed (e.g. Overlord or Columbo S00)
        var middleware = CreateMiddleware();
        var user = CreateTestUser();
        var seriesId = Guid.NewGuid();

        var now = DateTime.UtcNow;
        _evaluatorMock.Setup(e => e.GetSeriesMaxLastPlayedDate(seriesId, user)).Returns(now);

        var json = $$"""
        {
            "Items": [
                {
                    "Name": "Special 61",
                    "SeriesName": "Overlord",
                    "SeriesId": "{{seriesId}}",
                    "UserData": {
                        "Played": true,
                        "LastPlayedDate": "{{now:O}}"
                    }
                }
            ],
            "TotalRecordCount": 1
        }
        """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var resultBytes = await middleware.FilterNextUpResponseAsync(stream, user, debugLogging: true);
        var resultJson = Encoding.UTF8.GetString(resultBytes);

        var node = JsonNode.Parse(resultJson);
        Assert.NotNull(node);
        var items = node["Items"]?.AsArray();
        Assert.NotNull(items);
        Assert.Empty(items);
        Assert.Equal(0, node["TotalRecordCount"]?.GetValue<int>());
    }

    [Fact]
    public async Task FilterNextUpResponse_KeepsActiveRewatchEpisode_WhenEpPlayedIsOlderThanSeriesMax()
    {
        // Active rewatch case: EpLastPlayed < SeriesMaxLastPlayed (e.g. Sherlock Holmes S02E01)
        var middleware = CreateMiddleware();
        var user = CreateTestUser();
        var seriesId = Guid.NewGuid();

        var seriesMaxDate = DateTime.UtcNow; // Watched Ep 1 yesterday
        var ep2WatchedDate = DateTime.UtcNow.AddMonths(-3); // Ep 2 was watched 3 months ago during initial run

        _evaluatorMock.Setup(e => e.GetSeriesMaxLastPlayedDate(seriesId, user)).Returns(seriesMaxDate);

        var json = $$"""
        {
            "Items": [
                {
                    "Name": "Episode 2",
                    "SeriesName": "Sherlock Holmes",
                    "SeriesId": "{{seriesId}}",
                    "UserData": {
                        "Played": true,
                        "LastPlayedDate": "{{ep2WatchedDate:O}}"
                    }
                }
            ],
            "TotalRecordCount": 1
        }
        """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var resultBytes = await middleware.FilterNextUpResponseAsync(stream, user, debugLogging: true);
        var resultJson = Encoding.UTF8.GetString(resultBytes);

        var node = JsonNode.Parse(resultJson);
        Assert.NotNull(node);
        var items = node["Items"]?.AsArray();
        Assert.NotNull(items);
        Assert.Single(items);
        Assert.Equal("Episode 2", items[0]?["Name"]?.ToString());
        Assert.Equal(1, node["TotalRecordCount"]?.GetValue<int>());
    }

    [Theory]
    [InlineData(0, true)]   // Ep played date == Series max date -> Stalled -> Filter out (removed: true)
    [InlineData(10, true)]  // Ep played date > Series max date (clock skew / edge) -> Stalled -> Filter out (removed: true)
    [InlineData(-10, false)] // Ep played date < Series max date -> Active rewatch -> Keep (removed: false)
    public async Task FilterNextUpResponse_MatrixTest_RelativeDates(int offsetMinutes, bool expectRemoved)
    {
        var middleware = CreateMiddleware();
        var user = CreateTestUser();
        var seriesId = Guid.NewGuid();

        var baseTime = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
        _evaluatorMock.Setup(e => e.GetSeriesMaxLastPlayedDate(seriesId, user)).Returns(baseTime);

        var epTime = baseTime.AddMinutes(offsetMinutes);

        var json = $$"""
        {
            "Items": [
                {
                    "Name": "Matrix Episode",
                    "SeriesName": "Matrix Series",
                    "SeriesId": "{{seriesId}}",
                    "UserData": {
                        "Played": true,
                        "LastPlayedDate": "{{epTime:O}}"
                    }
                }
            ],
            "TotalRecordCount": 1
        }
        """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var resultBytes = await middleware.FilterNextUpResponseAsync(stream, user, debugLogging: false);
        var resultJson = Encoding.UTF8.GetString(resultBytes);

        var node = JsonNode.Parse(resultJson);
        var items = node?["Items"]?.AsArray();
        Assert.NotNull(items);

        if (expectRemoved)
        {
            Assert.Empty(items);
            Assert.Equal(0, node?["TotalRecordCount"]?.GetValue<int>());
        }
        else
        {
            Assert.Single(items);
            Assert.Equal(1, node?["TotalRecordCount"]?.GetValue<int>());
        }
    }

    [Fact]
    public async Task FilterNextUpResponse_Boundary_EmptyItemsArray()
    {
        var middleware = CreateMiddleware();
        var user = CreateTestUser();

        var json = """
        {
            "Items": [],
            "TotalRecordCount": 0
        }
        """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var resultBytes = await middleware.FilterNextUpResponseAsync(stream, user, debugLogging: false);
        var resultJson = Encoding.UTF8.GetString(resultBytes);

        var node = JsonNode.Parse(resultJson);
        var items = node?["Items"]?.AsArray();
        Assert.NotNull(items);
        Assert.Empty(items);
        Assert.Equal(0, node?["TotalRecordCount"]?.GetValue<int>());
    }

    [Fact]
    public async Task FilterNextUpResponse_Error_MalformedJson_ThrowsException()
    {
        var middleware = CreateMiddleware();
        var user = CreateTestUser();

        var malformed = "This is not valid json <error>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(malformed));

        await Assert.ThrowsAnyAsync<Exception>(() => middleware.FilterNextUpResponseAsync(stream, user, debugLogging: false));
    }

    [Theory]
    [InlineData("gzip")]
    [InlineData("br")]
    [InlineData("deflate")]
    public async Task InvokeAsync_HandlesCompressedResponses_Correctly(string encoding)
    {
        var seriesId = Guid.NewGuid();
        var userGuid = Guid.NewGuid();
        var user = CreateTestUser();

        _userManagerMock.Setup(u => u.GetUserById(userGuid)).Returns(user);
        // Series max played is same as episode -> should be filtered
        _evaluatorMock.Setup(e => e.GetSeriesMaxLastPlayedDate(seriesId, user))
            .Returns(DateTime.UtcNow);

        var rawJson = $$"""
        {
            "Items": [
                {
                    "Name": "Finished Ep",
                    "SeriesName": "Columbo",
                    "SeriesId": "{{seriesId}}",
                    "UserData": {
                        "Played": true,
                        "LastPlayedDate": "{{DateTime.UtcNow:O}}"
                    }
                }
            ],
            "TotalRecordCount": 1
        }
        """;

        var rawBytes = Encoding.UTF8.GetBytes(rawJson);

        // Next middleware writes compressed JSON into Response.Body
        RequestDelegate next = async (HttpContext ctx) =>
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.Headers.ContentEncoding = encoding;

            if (encoding == "gzip")
            {
                await using var gz = new System.IO.Compression.GZipStream(ctx.Response.Body, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true);
                await gz.WriteAsync(rawBytes);
            }
            else if (encoding == "br")
            {
                await using var br = new System.IO.Compression.BrotliStream(ctx.Response.Body, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true);
                await br.WriteAsync(rawBytes);
            }
            else if (encoding == "deflate")
            {
                await using var def = new System.IO.Compression.DeflateStream(ctx.Response.Body, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true);
                await def.WriteAsync(rawBytes);
            }
        };

        var middleware = new NextUpRewatchMiddleware(
            next,
            _loggerMock.Object,
            _evaluatorMock.Object,
            _userManagerMock.Object);

        var context = new DefaultHttpContext();
        context.Request.Path = "/Shows/NextUp";
        context.Request.QueryString = new QueryString($"?userId={userGuid}&enableRewatching=true");

        using var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        await middleware.InvokeAsync(context);

        // Verify output is compressed and can be decompressed
        responseStream.Seek(0, SeekOrigin.Begin);
        Stream decompressed;
        if (encoding == "gzip")
        {
            decompressed = new System.IO.Compression.GZipStream(responseStream, System.IO.Compression.CompressionMode.Decompress);
        }
        else if (encoding == "br")
        {
            decompressed = new System.IO.Compression.BrotliStream(responseStream, System.IO.Compression.CompressionMode.Decompress);
        }
        else
        {
            decompressed = new System.IO.Compression.DeflateStream(responseStream, System.IO.Compression.CompressionMode.Decompress);
        }

        using var reader = new StreamReader(decompressed, Encoding.UTF8);
        var resultJson = await reader.ReadToEndAsync();
        var node = JsonNode.Parse(resultJson);

        Assert.NotNull(node);
        var items = node?["Items"]?.AsArray();
        Assert.NotNull(items);
        Assert.Empty(items); // Item was filtered out!
        Assert.Equal(0, node?["TotalRecordCount"]?.GetValue<int>());
    }
}

