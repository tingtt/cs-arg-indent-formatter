using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CsArgIndentFormatter;

internal static class CSharpArgIndentFormatter
{
  public static string Format(string source, EditorConfigIndentationOptions indentationOptions)
  {
    return ApplyListIndentFormatting(source, DetectNewLine(source), indentationOptions);
  }

  private static string ApplyListIndentFormatting(
    string source,
    string newLine,
    EditorConfigIndentationOptions indentationOptions
  )
  {
    var syntaxTree = CSharpSyntaxTree.ParseText(source);
    var root = syntaxTree.GetRoot();
    var sourceText = syntaxTree.GetText();

    var replacements = new List<TextReplacement>();

    replacements.AddRange(root.DescendantNodes()
      .OfType<ArgumentListSyntax>()
      .Where(argumentList => IsSingleMultilineLambdaArgument(argumentList, sourceText))
      .Where(argumentList => !ContainsMultilineStringContent(argumentList, sourceText))
      .Select(argumentList => new TextReplacement(
        argumentList.Span,
        FormatSingleLambdaArgumentList(argumentList, sourceText, newLine, indentationOptions)
      )));

    replacements.AddRange(root.DescendantNodes()
      .OfType<ArgumentListSyntax>()
      .Where(argumentList => SpansMultipleLines(sourceText, argumentList.Span))
      .Where(argumentList => !IsSingleMultilineLambdaArgument(argumentList, sourceText))
      .Where(argumentList => !ContainsMultilineStringContent(argumentList, sourceText))
      .Where(argumentList => StartsOnNextLine(sourceText, argumentList.OpenParenToken.Span.End, argumentList.CloseParenToken.SpanStart))
      .Select(argumentList => new TextReplacement(
        argumentList.Span,
        FormatParenthesizedContent(
          sourceText,
          argumentList.OpenParenToken.Span.End,
          argumentList.CloseParenToken.SpanStart,
          GetLineIndent(sourceText, argumentList.OpenParenToken.SpanStart),
          newLine,
          indentationOptions
        )
      )));

    return ApplyReplacements(source, replacements);
  }

  private static string FormatSingleLambdaArgumentList(
    ArgumentListSyntax argumentList,
    SourceText sourceText,
    string newLine,
    EditorConfigIndentationOptions indentationOptions
  )
  {
    var lambda = (ParenthesizedLambdaExpressionSyntax)argumentList.Arguments[0].Expression;
    var block = lambda.Block!;
    var lineIndent = GetLineIndent(sourceText, argumentList.OpenParenToken.SpanStart);
    var braceIndent = lineIndent + indentationOptions.IndentUnit;
    var bodyIndent = braceIndent + indentationOptions.IndentUnit;
    var lambdaHeader = sourceText.ToString(TextSpan.FromBounds(lambda.SpanStart, block.OpenBraceToken.SpanStart)).TrimEnd();

    var blockInnerText = sourceText.ToString(TextSpan.FromBounds(block.OpenBraceToken.Span.End, block.CloseBraceToken.SpanStart));
    var formattedBlockInnerText = ReindentSnippet(blockInnerText, bodyIndent, newLine);

    if (string.IsNullOrEmpty(formattedBlockInnerText))
    {
      return $"({lambdaHeader}{newLine}{braceIndent}{{{newLine}{braceIndent}}}{newLine}{lineIndent})";
    }

    return $"({lambdaHeader}{newLine}{braceIndent}{{{newLine}{formattedBlockInnerText}{newLine}{braceIndent}}}{newLine}{lineIndent})";
  }

  private static string FormatParenthesizedContent(
    SourceText sourceText,
    int contentStart,
    int contentEnd,
    string baseIndent,
    string newLine,
    EditorConfigIndentationOptions indentationOptions
  )
  {
    var innerText = sourceText.ToString(TextSpan.FromBounds(contentStart, contentEnd));
    var formattedInnerText = ReindentSnippet(innerText, baseIndent + indentationOptions.IndentUnit, newLine);
    return $"({newLine}{formattedInnerText}{newLine}{baseIndent})";
  }

  private static string ReindentSnippet(string rawText, string targetIndent, string newLine)
  {
    var normalized = rawText.Replace("\r\n", "\n").Replace("\r", "\n");
    var lines = normalized.Split('\n').ToList();

    TrimOuterEmptyLines(lines);
    if (lines.Count == 0)
    {
      return string.Empty;
    }

    var minimumIndent = lines
      .Where(line => !string.IsNullOrWhiteSpace(line))
      .Select(GetLeadingWhitespaceLength)
      .DefaultIfEmpty(0)
      .Min();

    return string.Join(
      newLine,
      lines.Select(line =>
      {
        if (string.IsNullOrWhiteSpace(line))
        {
          return string.Empty;
        }

        var trimmedLine = line.Length >= minimumIndent ? line[minimumIndent..] : line.TrimStart();
        return $"{targetIndent}{trimmedLine}";
      })
    );
  }

  private static bool SpansMultipleLines(SourceText sourceText, TextSpan span)
  {
    var lineSpan = sourceText.Lines.GetLinePositionSpan(span);
    return lineSpan.Start.Line != lineSpan.End.Line;
  }

  private static bool StartsOnNextLine(SourceText sourceText, int contentStart, int contentEnd)
  {
    if (contentStart >= contentEnd)
    {
      return false;
    }

    for (var i = contentStart; i < contentEnd; i++)
    {
      var currentCharacter = sourceText[i];
      if (currentCharacter == '\r')
      {
        continue;
      }

      if (currentCharacter == '\n')
      {
        return true;
      }

      if (!char.IsWhiteSpace(currentCharacter))
      {
        return false;
      }
    }

    return false;
  }

  private static bool IsSingleMultilineLambdaArgument(ArgumentListSyntax argumentList, SourceText sourceText)
  {
    if (argumentList.Arguments.Count != 1)
    {
      return false;
    }

    if (argumentList.Arguments[0].Expression is not ParenthesizedLambdaExpressionSyntax lambda || lambda.Block is null)
    {
      return false;
    }

    return SpansMultipleLines(sourceText, lambda.Span);
  }

  private static string DetectNewLine(string source)
  {
    return source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
  }

  private static bool ContainsMultilineStringContent(SyntaxNode node, SourceText sourceText)
  {
    return node.DescendantNodesAndSelf().Any(descendant =>
      descendant switch
      {
        LiteralExpressionSyntax literal
          when literal.IsKind(SyntaxKind.StringLiteralExpression) =>
            SpansMultipleLines(sourceText, literal.Span),
        InterpolatedStringExpressionSyntax interpolatedString =>
          SpansMultipleLines(sourceText, interpolatedString.Span),
        _ => false,
      }
    );
  }

  private static string GetLineIndent(SourceText sourceText, int position)
  {
    var line = sourceText.Lines.GetLineFromPosition(position);
    var lineText = line.ToString();
    return lineText[..GetLeadingWhitespaceLength(lineText)];
  }

  private static int GetLeadingWhitespaceLength(string line)
  {
    var count = 0;
    while (count < line.Length && char.IsWhiteSpace(line[count]) && line[count] != '\r' && line[count] != '\n')
    {
      count++;
    }

    return count;
  }

  private static void TrimOuterEmptyLines(List<string> lines)
  {
    while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
    {
      lines.RemoveAt(0);
    }

    while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
    {
      lines.RemoveAt(lines.Count - 1);
    }
  }

  private static string ApplyReplacements(string source, IEnumerable<TextReplacement> replacements)
  {
    var orderedReplacements = replacements
      .OrderByDescending(replacement => replacement.Span.Start)
      .ToList();

    if (orderedReplacements.Count == 0)
    {
      return source;
    }

    var builder = new StringBuilder(source);
    foreach (var replacement in orderedReplacements)
    {
      builder.Remove(replacement.Span.Start, replacement.Span.Length);
      builder.Insert(replacement.Span.Start, replacement.Content);
    }

    return builder.ToString();
  }
  private sealed record TextReplacement(TextSpan Span, string Content);
}
