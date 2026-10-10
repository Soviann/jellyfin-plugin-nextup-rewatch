using Jellyfin.Plugin.NextUpRewatch.Api;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.NextUpRewatch;

/// <summary>
/// Registers plugin services and middleware into the Jellyfin dependency injection container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddTransient<ISeriesRewatchEvaluator, SeriesRewatchEvaluator>();
        serviceCollection.AddTransient<IStartupFilter, StartupFilter>();
    }
}
