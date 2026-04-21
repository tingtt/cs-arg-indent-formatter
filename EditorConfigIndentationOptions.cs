namespace CsArgIndentFormatter;

internal sealed class EditorConfigIndentationOptions
{
  private EditorConfigIndentationOptions(string indentStyle, int indentSize)
  {
    IndentStyle = indentStyle;
    IndentSize = indentSize;
  }

  public string IndentStyle { get; }
  public int IndentSize { get; }

  public string IndentUnit => IndentStyle == "tab"
    ? "\t"
    : new string(' ', IndentSize);

  public static EditorConfigIndentationOptions LoadFromCurrentDirectory()
  {
    var editorConfigPath = Path.Combine(Directory.GetCurrentDirectory(), ".editorconfig");
    if (!File.Exists(editorConfigPath))
    {
      return new EditorConfigIndentationOptions(indentStyle: "space", indentSize: 2);
    }

    string? indentStyle = null;
    int? indentSize = null;
    var appliesToCSharp = true;

    foreach (var rawLine in File.ReadLines(editorConfigPath))
    {
      var line = rawLine.Trim();
      if (string.IsNullOrEmpty(line) || line.StartsWith('#') || line.StartsWith(';'))
      {
        continue;
      }

      if (line.StartsWith('[') && line.EndsWith(']'))
      {
        appliesToCSharp = SectionAppliesToCSharp(line[1..^1]);
        continue;
      }

      if (!appliesToCSharp)
      {
        continue;
      }

      var separatorIndex = line.IndexOf('=');
      if (separatorIndex < 0)
      {
        continue;
      }

      var key = line[..separatorIndex].Trim();
      var value = line[(separatorIndex + 1)..].Trim();
      switch (key)
      {
        case "indent_style" when value is "space" or "tab":
          indentStyle = value;
          break;
        case "indent_size" when int.TryParse(value, out var parsedIndentSize) && parsedIndentSize > 0:
          indentSize = parsedIndentSize;
          break;
      }
    }

    return new EditorConfigIndentationOptions(
      indentStyle ?? "space",
      indentSize ?? 2
    );
  }

  private static bool SectionAppliesToCSharp(string section)
  {
    if (section == "*")
    {
      return true;
    }

    var normalized = section.Replace("{", ",").Replace("}", ",");
    return normalized
      .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
      .Any(token => token.Equals("*.cs", StringComparison.OrdinalIgnoreCase)
        || token.Equals(".cs", StringComparison.OrdinalIgnoreCase)
        || token.Equals("**/*.cs", StringComparison.OrdinalIgnoreCase));
  }
}
