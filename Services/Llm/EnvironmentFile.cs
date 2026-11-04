namespace lionheart.Services.Llm;

/// <summary>Loads the <c>.env</c> file into the process environment, under what is already set.</summary>
/// <remarks>
/// A variable already on the process wins over the file. That is what makes
/// <c>Llm__ChatProvider=Relay dotnet run</c> mean what it says: the file in the main checkout names
/// the owner's paid provider, and a worktree that inherits it would otherwise bill him for a test it
/// asked to run locally. DotNetEnv overwrites by default, hence <c>NoClobber</c>.
/// <para>
/// The search walks up from the working directory and stops at the first <c>.env</c>, so a
/// worktree's own file shadows the main checkout's entirely.
/// </para>
/// </remarks>
public static class EnvironmentFile
{
    /// <param name="path">Where the search starts; the working directory's <c>.env</c> when absent.</param>
    public static void Load(string? path = null) =>
        DotNetEnv.Env.NoClobber().TraversePath().Load(path);
}
