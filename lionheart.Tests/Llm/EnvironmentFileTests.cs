using lionheart.Services.Llm;

namespace lionheart.Tests.Llm;

/// <summary>
/// The <c>.env</c> file fills in what the process does not set, and never overrides it. This is
/// what keeps <c>Llm__ChatProvider=Relay dotnet run</c> from quietly running on the paid provider
/// the file names.
/// </summary>
public sealed class EnvironmentFileTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("lionheart-env-").FullName;
    private readonly string _set = $"LH_ENV_TEST_SET_{Guid.NewGuid():N}";
    private readonly string _unset = $"LH_ENV_TEST_UNSET_{Guid.NewGuid():N}";

    public EnvironmentFileTests() =>
        File.WriteAllText(Path.Combine(_directory, ".env"), $"{_set}=Anthropic\n{_unset}=from-file\n");

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(_set, null);
        Environment.SetEnvironmentVariable(_unset, null);
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void A_variable_set_on_the_process_wins_over_the_file()
    {
        Environment.SetEnvironmentVariable(_set, "Relay");

        EnvironmentFile.Load(Path.Combine(_directory, ".env"));

        Assert.Equal("Relay", Environment.GetEnvironmentVariable(_set));
    }

    [Fact]
    public void A_variable_the_process_does_not_set_comes_from_the_file()
    {
        EnvironmentFile.Load(Path.Combine(_directory, ".env"));

        Assert.Equal("from-file", Environment.GetEnvironmentVariable(_unset));
    }

    [Fact]
    public void The_search_starts_below_and_finds_the_nearest_file_above()
    {
        var below = Directory.CreateDirectory(Path.Combine(_directory, "worktree", "deeper")).FullName;

        EnvironmentFile.Load(Path.Combine(below, ".env"));

        Assert.Equal("from-file", Environment.GetEnvironmentVariable(_unset));
    }
}
