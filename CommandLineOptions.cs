namespace CsArgIndentFormatter;

internal sealed class CommandLineOptions
{
  private CommandLineOptions(bool checkOnly, bool showHelp, IReadOnlyList<string> paths)
  {
    CheckOnly = checkOnly;
    ShowHelp = showHelp;
    Paths = paths;
  }

  public bool CheckOnly { get; }
  public bool ShowHelp { get; }
  public IReadOnlyList<string> Paths { get; }

  public static string HelpText =>
    """
    Usage:
      cs-arg-indent-formatter [--check] <path> [<path>...]

    Options:
      --check   Exit with code 1 when any file would change.
      --help    Show this help.
    """;

  public static CommandLineOptions Parse(string[] args)
  {
    var checkOnly = false;
    var showHelp = false;
    var paths = new List<string>();

    foreach (var arg in args)
    {
      switch (arg)
      {
        case "--check":
          checkOnly = true;
          break;
        case "--help":
        case "-h":
          showHelp = true;
          break;
        default:
          paths.Add(arg);
          break;
      }
    }

    return new CommandLineOptions(checkOnly, showHelp, paths);
  }

  public static IEnumerable<string> EnumerateTargetFiles(IEnumerable<string> paths)
  {
    return paths
      .Select(Path.GetFullPath)
      .SelectMany(path =>
      {
        if (File.Exists(path))
        {
          return new[] { path };
        }

        if (!Directory.Exists(path))
        {
          throw new DirectoryNotFoundException($"Path not found: {path}");
        }

        return Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories)
          .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
          .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
          .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
      })
      .Distinct(StringComparer.Ordinal);
  }
}
