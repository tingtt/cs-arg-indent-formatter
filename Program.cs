using CsArgIndentFormatter;

var options = CommandLineOptions.Parse(args);
var indentationOptions = EditorConfigIndentationOptions.LoadFromCurrentDirectory();

if (options.ShowHelp)
{
  Console.WriteLine(CommandLineOptions.HelpText);
  return 0;
}

if (options.Paths.Count == 0)
{
  Console.Error.WriteLine("At least one file or directory path is required.");
  Console.Error.WriteLine(CommandLineOptions.HelpText);
  return 1;
}

var changedFiles = new List<string>();
foreach (var filePath in CommandLineOptions.EnumerateTargetFiles(options.Paths))
{
  var source = File.ReadAllText(filePath);
  var formatted = CSharpArgIndentFormatter.Format(source, indentationOptions);
  if (formatted == source)
  {
    continue;
  }

  changedFiles.Add(filePath);
  if (!options.CheckOnly)
  {
    File.WriteAllText(filePath, formatted);
  }
}

if (changedFiles.Count == 0)
{
  Console.WriteLine(options.CheckOnly ? "No files require formatting." : "No files changed.");
  return 0;
}

foreach (var changedFile in changedFiles)
{
  Console.WriteLine(changedFile);
}

return options.CheckOnly ? 1 : 0;
