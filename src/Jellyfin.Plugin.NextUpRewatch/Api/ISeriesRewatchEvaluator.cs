using System;
using Jellyfin.Data.Entities;

namespace Jellyfin.Plugin.NextUpRewatch.Api;

/// <summary>
/// Evaluates series playback status for rewatch filtering.
/// </summary>
public interface ISeriesRewatchEvaluator
{
    /// <summary>
    /// Gets the maximum last played date across all episodes in the specified series.
    /// </summary>
    /// <param name="seriesId">The series unique identifier.</param>
    /// <param name="user">The user.</param>
    /// <returns>The maximum last played date, or null if no episodes have been played.</returns>
    DateTime? GetSeriesMaxLastPlayedDate(Guid seriesId, User user);
}
