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
          .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
          .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
          .Where(file => !IsInsideNestedGitRepository(path, file));
      })
      .Distinct(StringComparer.Ordinal);
  }

  private static bool IsInsideNestedGitRepository(string rootPath, string filePath)
  {
    var rootDirectory = new DirectoryInfo(Path.GetFullPath(rootPath));
    var currentDirectory = new FileInfo(filePath).Directory;
    while (currentDirectory is not null && !PathsEqual(currentDirectory.FullName, rootDirectory.FullName))
    {
      var gitPath = Path.Combine(currentDirectory.FullName, ".git");
      if (File.Exists(gitPath) || Directory.Exists(gitPath))
      {
        return true;
      }

      currentDirectory = currentDirectory.Parent;
    }

    return false;
  }

  private static bool PathsEqual(string left, string right)
  {
    return string.Equals(
      Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
      Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
      OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal
    );
  }
}
