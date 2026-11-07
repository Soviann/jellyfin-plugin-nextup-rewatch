using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.NextUpRewatch.Configuration;

/// <summary>
/// Plugin configuration for NextUp Rewatch.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        EnableFilter = true;
        EnableDebugLogging = false;
        ForceEnableRewatching = true;
        PromoteActiveRewatches = true;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the rewatch filter is enabled.
    /// </summary>
    public bool EnableFilter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether debug logging is enabled.
    /// </summary>
    public bool EnableDebugLogging { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to always force enableRewatching=true server-side on /Shows/NextUp requests.
    /// </summary>
    public bool ForceEnableRewatching { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to promote active rewatch episodes by updating their LastPlayedDate to the series latest watch date.
    /// </summary>
    public bool PromoteActiveRewatches { get; set; }
}
