using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Jellyfin.Data.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NextUpRewatch.Api;

/// <summary>
/// Middleware that intercepts /Shows/NextUp responses and filters out stalled completed series items.
/// </summary>
public class NextUpRewatchMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<NextUpRewatchMiddleware> _logger;
    private readonly ISeriesRewatchEvaluator _evaluator;
    private readonly IUserManager _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="NextUpRewatchMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in pipeline.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="evaluator">The series rewatch evaluator.</param>
    /// <param name="userManager">The user manager.</param>
    public NextUpRewatchMiddleware(
        RequestDelegate next,
        ILogger<NextUpRewatchMiddleware> logger,
        ISeriesRewatchEvaluator evaluator,
        IUserManager userManager)
    {
        _next = next;
        _logger = logger;
        _evaluator = evaluator;
        _userManager = userManager;
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing asynchronous execution.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var config = Plugin.Instance?.Configuration;
        if (config != null && !config.EnableFilter)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.EndsWith("/Shows/NextUp", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Only filter when enableRewatching is requested
        var enableRewatchingQuery = context.Request.Query["enableRewatching"].ToString();
        var isRewatchingEnabled = string.Equals(enableRewatchingQuery, "true", StringComparison.OrdinalIgnoreCase);

        if (!isRewatchingEnabled)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Determine user
        var userIdStr = context.Request.Query["userId"].ToString();
        User? user = null;
        if (Guid.TryParse(userIdStr, out var userGuid))
        {
            user = _userManager.GetUserById(userGuid);
        }

        if (user == null)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var originalBodyStream = context.Response.Body;
        await using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        try
        {
            await _next(context).ConfigureAwait(false);

            if (context.Response.StatusCode == StatusCodes.Status200OK
                && (context.Response.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                responseBodyStream.Seek(0, SeekOrigin.Begin);
                var filteredJsonBytes = await FilterNextUpResponseAsync(responseBodyStream, user, config?.EnableDebugLogging ?? false).ConfigureAwait(false);

                context.Response.Body = originalBodyStream;
                context.Response.ContentLength = filteredJsonBytes.Length;
                await originalBodyStream.WriteAsync(filteredJsonBytes).ConfigureAwait(false);
            }
            else
            {
                responseBodyStream.Seek(0, SeekOrigin.Begin);
                context.Response.Body = originalBodyStream;
                await responseBodyStream.CopyToAsync(originalBodyStream).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while processing /Shows/NextUp in NextUpRewatchMiddleware");
            responseBodyStream.Seek(0, SeekOrigin.Begin);
            context.Response.Body = originalBodyStream;
            await responseBodyStream.CopyToAsync(originalBodyStream).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Filters the NextUp response JSON stream to exclude stalled completed items.
    /// </summary>
    /// <param name="responseStream">The raw response stream.</param>
    /// <param name="user">The requesting user.</param>
    /// <param name="debugLogging">Whether to emit debug log entries.</param>
    /// <returns>The filtered JSON byte array.</returns>
    public async Task<byte[]> FilterNextUpResponseAsync(Stream responseStream, User user, bool debugLogging)
    {
        var jsonNode = await JsonNode.ParseAsync(responseStream).ConfigureAwait(false);
        if (jsonNode is not JsonObject rootObj || !rootObj.TryGetPropertyValue("Items", out var itemsNode) || itemsNode is not JsonArray itemsArray)
        {
            responseStream.Seek(0, SeekOrigin.Begin);
            using var ms = new MemoryStream();
            await responseStream.CopyToAsync(ms).ConfigureAwait(false);
            return ms.ToArray();
        }

        var filteredItems = new JsonArray();
        int removedCount = 0;

        foreach (var itemNode in itemsArray)
        {
            if (itemNode is not JsonObject itemObj)
            {
                if (itemNode != null)
                {
                    filteredItems.Add(itemNode.DeepClone());
                }
                continue;
            }

            var isPlayed = false;
            DateTime? epLastPlayedDate = null;

            if (itemObj.TryGetPropertyValue("UserData", out var userDataNode) && userDataNode is JsonObject userDataObj)
            {
                if (userDataObj.TryGetPropertyValue("Played", out var playedVal))
                {
                    isPlayed = playedVal?.GetValue<bool>() ?? false;
                }

                if (userDataObj.TryGetPropertyValue("LastPlayedDate", out var lastPlayedVal) && lastPlayedVal != null)
                {
                    if (DateTime.TryParse(lastPlayedVal.ToString(), out var parsedDate))
                    {
                        epLastPlayedDate = parsedDate.ToUniversalTime();
                    }
                }
            }

            // Always keep unplayed episodes
            if (!isPlayed)
            {
                filteredItems.Add(itemObj.DeepClone());
                continue;
            }

            // For played episodes, check if there is an active rewatch in progress
            var seriesIdStr = itemObj["SeriesId"]?.ToString();
            var seriesName = itemObj["SeriesName"]?.ToString() ?? "Unknown";
            var episodeName = itemObj["Name"]?.ToString() ?? "Unknown";

            if (!string.IsNullOrEmpty(seriesIdStr) && Guid.TryParse(seriesIdStr, out var seriesGuid))
            {
                var seriesMaxLastPlayed = _evaluator.GetSeriesMaxLastPlayedDate(seriesGuid, user);

                // If this episode is the most recently played episode in the series (EpLastPlayed >= SeriesMax),
                // it is the finished episode with no subsequent episode to follow -> FILTER OUT!
                if (epLastPlayedDate.HasValue && seriesMaxLastPlayed.HasValue && epLastPlayedDate.Value >= seriesMaxLastPlayed.Value)
                {
                    removedCount++;
                    if (debugLogging)
                    {
                        _logger.LogInformation(
                            "[NextUpRewatch] Filtered stalled episode '{Episode}' of series '{Series}' (EpPlayed: {EpDate}, SeriesMax: {MaxDate})",
                            episodeName,
                            seriesName,
                            epLastPlayedDate.Value,
                            seriesMaxLastPlayed.Value);
                    }
                    continue;
                }

                // Otherwise (EpLastPlayed < SeriesMax), an episode was played MORE recently than this one,
                // meaning this is legitimately the NEXT episode in an active rewatch sequence -> KEEP!
                if (debugLogging)
                {
                    _logger.LogInformation(
                        "[NextUpRewatch] Kept active rewatch episode '{Episode}' of series '{Series}' (EpPlayed: {EpDate} < SeriesMax: {MaxDate})",
                        episodeName,
                        seriesName,
                        epLastPlayedDate,
                        seriesMaxLastPlayed);
                }
            }

            filteredItems.Add(itemObj.DeepClone());
        }

        rootObj["Items"] = filteredItems;
        if (rootObj.TryGetPropertyValue("TotalRecordCount", out var totalRecordCountNode) && totalRecordCountNode != null)
        {
            var currentTotal = totalRecordCountNode.GetValue<int>();
            rootObj["TotalRecordCount"] = Math.Max(0, currentTotal - removedCount);
        }

        return JsonSerializer.SerializeToUtf8Bytes(rootObj, new JsonSerializerOptions { WriteIndented = false });
    }
}
