using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Jellyfin.Plugin.NextUpRewatch.Api;

/// <summary>
/// Registers the NextUpRewatchMiddleware into the ASP.NET Core pipeline.
/// </summary>
public class StartupFilter : IStartupFilter
{
    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseMiddleware<NextUpRewatchMiddleware>();
            next(app);
        };
    }
}
