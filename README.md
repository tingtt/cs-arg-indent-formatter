# cs-arg-indent-formatter

## Overview

`cs-arg-indent-formatter` is a Roslyn-based C# formatter for **multiline function-call argument indentation**.

It does **not** aim to be a general-purpose C# formatter. Its scope is limited to argument indentation patterns that are hard to express with the default `dotnet format` behavior.

## Scope

The first version targets these multiline layouts:

- indentation for argument lists that start on the next line after `(`
- block-body parenthesized lambdas used as invocation arguments

It preserves the input file newline style, reads `indent_style` and `indent_size` from the `.editorconfig` in the current working directory, skips files inside nested git repositories such as submodules, and skips target nodes that contain multiline string literals or multiline interpolated strings.

## Quickstart

```bash
cd <your-project>
git clone https://github.com/tingtt/cs-arg-indent-formatter.git Tools/cs-arg-indent-formatter
dotnet run --project Tools/cs-arg-indent-formatter -- <path>
```

Check mode:

```bash
dotnet run --project Tools/cs-arg-indent-formatter -- --check <path>
```

## Examples

Constructor calls and other multiline argument lists are rewritten like this:

```csharp
var formatter = new Formatter(
    parser,
    writer,
    settings
);
```

becomes:

```csharp
var formatter = new Formatter(
  parser,
  writer,
  settings
);
```

Statement lambdas used as invocation arguments are rewritten like this:

```csharp
button.OnClick.AddListener(() =>
{
  logger.Log("clicked");
  tracker.Track("clicked");
});
```

becomes:

```csharp
button.OnClick.AddListener(() =>
  {
    logger.Log("clicked");
    tracker.Track("clicked");
  }
);
```

The same indentation rule also applies when a constructor call appears as an argument:

```bash
dotnet run --project Tools/cs-arg-indent-formatter -- src
```

```csharp
Register(
    new Formatter(
        parser,
        writer,
        settings
    ),
    "default"
);
```

becomes:

```csharp
Register(
  new Formatter(
    parser,
    writer,
    settings
  ),
  "default"
);
```
