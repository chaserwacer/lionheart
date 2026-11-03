using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace lionheart.Data;

/// <summary>
/// Where this application's database lives, and how to register it.
/// </summary>
/// <remarks>
/// Stated once because two entry points need it: the web host, and the archive import command.
/// Spelling the connection string in both is how they come to disagree.
/// </remarks>
public static class LionheartDatabase
{
    public const string ConnectionString = "Data Source=./Data/lionheart.db";

    /// <summary>Registers <see cref="ModelContext"/> against the development database file.</summary>
    public static THostBuilder AddLionheartDatabase<THostBuilder>(this THostBuilder builder)
        where THostBuilder : IHostApplicationBuilder
    {
        builder.Services.AddDbContext<ModelContext>(options => options.UseSqlite(ConnectionString));
        return builder;
    }
}
