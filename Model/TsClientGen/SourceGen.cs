public class SourceGen
{
    /// <summary>
    /// Returns the directory containing the solution file. Should only be used when the code is run from the source tree, i.e. in during development or CI/CD.
    /// </summary>
    /// <returns></returns>
    public static string GetSourceRoot()
    {
        var path = new FileInfo(typeof(SourceGen).Assembly.Location).Directory;
        while (Directory.GetFiles(path!.FullName, "*.sln").Length == 0)
        {
            path = path.Parent;
        }

        return path.FullName;
    }

    /// <summary>
    /// Returns the directory containing the solution file. Should only be used when the code is run from the source tree, i.e. in during development or CI/CD.
    /// </summary>
    /// <returns></returns>
    public static string GetSourceRoot(params string[] append)
    {
        var root = GetSourceRoot();
        return Path.Combine(new[] { root }.Concat(append).ToArray());
    }

}