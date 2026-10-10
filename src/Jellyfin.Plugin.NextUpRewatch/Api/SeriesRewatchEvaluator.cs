using System;
using Jellyfin.Data.Entities;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.NextUpRewatch.Api;

/// <summary>
/// Default implementation of <see cref="ISeriesRewatchEvaluator"/> querying Jellyfin's LibraryManager and UserDataManager.
/// </summary>
public class SeriesRewatchEvaluator : ISeriesRewatchEvaluator
{
    private readonly ILibraryManager _libraryManager;
    private readonly IUserDataManager _userDataManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeriesRewatchEvaluator"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="userDataManager">The user data manager.</param>
    public SeriesRewatchEvaluator(ILibraryManager libraryManager, IUserDataManager userDataManager)
    {
        _libraryManager = libraryManager;
        _userDataManager = userDataManager;
    }

    /// <inheritdoc />
    public DateTime? GetSeriesMaxLastPlayedDate(Guid seriesId, User user)
    {
        var series = _libraryManager.GetItemById(seriesId) as Series;
        if (series == null)
        {
            return null;
        }

        DateTime? maxDate = null;
        var episodes = series.GetEpisodes(user, new DtoOptions(), false);

        foreach (var ep in episodes)
        {
            var userData = _userDataManager.GetUserData(user, ep);
            if (userData?.LastPlayedDate != null)
            {
                var playedDateUtc = userData.LastPlayedDate.Value.ToUniversalTime();
                if (!maxDate.HasValue || playedDateUtc > maxDate.Value)
                {
                    maxDate = playedDateUtc;
                }
            }
        }

        return maxDate;
    }
}
