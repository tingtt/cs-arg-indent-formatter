# cs-arg-indent-formatter

## Overview

`cs-arg-indent-formatter` is a Roslyn-based C# formatter for repository-specific indentation rules that are not covered by the default `dotnet format` behavior.

## Scope

The first version targets these multiline layouts:

- indentation for argument lists that start on the next line after `(`
- indentation for method parameter lists that start on the next line after `(`
- block-body parenthesized lambdas used as invocation arguments

It preserves the input file newline style and skips files inside nested git repositories such as submodules.

## Quickstart

```bash
dotnet run --project Tools/cs-arg-indent-formatter -- <path>
```

Check mode:

```bash
dotnet run --project Tools/cs-arg-indent-formatter -- --check <path>
```

Recommended target paths are source directories in the main repository, for example:

```bash
dotnet run --project Tools/cs-arg-indent-formatter -- Assets/App/Menu
```
