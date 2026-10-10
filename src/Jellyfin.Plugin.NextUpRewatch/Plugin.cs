using System;
using System.Collections.Generic;
using Jellyfin.Plugin.NextUpRewatch.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.NextUpRewatch;

/// <summary>
/// The main plugin class for NextUp Rewatch.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="xmlSerializer">The XML serializer.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "NextUp Rewatch";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("9b84a277-2f3b-48cf-9a99-b1d5c7f897e1");

    /// <inheritdoc />
    public override string Description =>
        "Filters stalled completed series and dead-end specials from Next Up when rewatching is active, while seamlessly preserving active rewatches.";

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "NextUpRewatch",
                EmbeddedResourcePath = string.Format("{0}.Configuration.Web.config.html", GetType().Namespace),
                EnableInMainMenu = true
            },
            new PluginPageInfo
            {
                Name = "NextUpRewatch.js",
                EmbeddedResourcePath = string.Format("{0}.Configuration.Web.config.js", GetType().Namespace)
            }
        };
    }
}
