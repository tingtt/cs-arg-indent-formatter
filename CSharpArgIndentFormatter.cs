using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CsArgIndentFormatter;

internal static class CSharpArgIndentFormatter
{
  public static string Format(string source)
  {
    return ApplyListIndentFormatting(source);
  }

  private static string ApplyListIndentFormatting(string source)
  {
    var syntaxTree = CSharpSyntaxTree.ParseText(source);
    var root = syntaxTree.GetRoot();
    var sourceText = syntaxTree.GetText();

    var replacements = new List<TextReplacement>();

    replacements.AddRange(root.DescendantNodes()
      .OfType<ArgumentListSyntax>()
      .Where(argumentList => IsSingleMultilineLambdaArgument(argumentList, sourceText))
      .Select(argumentList => new TextReplacement(
        argumentList.Span,
        FormatSingleLambdaArgumentList(argumentList, sourceText)
      )));

    replacements.AddRange(root.DescendantNodes()
      .OfType<ArgumentListSyntax>()
      .Where(argumentList => SpansMultipleLines(sourceText, argumentList.Span))
      .Where(argumentList => !IsSingleMultilineLambdaArgument(argumentList, sourceText))
      .Where(argumentList => StartsOnNextLine(sourceText, argumentList.OpenParenToken.Span.End, argumentList.CloseParenToken.SpanStart))
      .Select(argumentList => new TextReplacement(
        argumentList.Span,
        FormatParenthesizedContent(
          sourceText,
          argumentList.OpenParenToken.Span.End,
          argumentList.CloseParenToken.SpanStart,
          GetLineIndent(sourceText, argumentList.OpenParenToken.SpanStart)
        )
      )));

    replacements.AddRange(root.DescendantNodes()
      .OfType<BaseMethodDeclarationSyntax>()
      .Select(method => method.ParameterList)
      .Where(parameterList => SpansMultipleLines(sourceText, parameterList.Span))
      .Where(parameterList => StartsOnNextLine(sourceText, parameterList.OpenParenToken.Span.End, parameterList.CloseParenToken.SpanStart))
      .Select(parameterList => new TextReplacement(
        parameterList.Span,
        FormatParenthesizedContent(
          sourceText,
          parameterList.OpenParenToken.Span.End,
          parameterList.CloseParenToken.SpanStart,
          GetLineIndent(sourceText, parameterList.OpenParenToken.SpanStart)
        )
      )));

    return ApplyReplacements(source, replacements);
  }

  private static string FormatSingleLambdaArgumentList(ArgumentListSyntax argumentList, SourceText sourceText)
  {
    var lambda = (ParenthesizedLambdaExpressionSyntax)argumentList.Arguments[0].Expression;
    var block = lambda.Block!;
    var lineIndent = GetLineIndent(sourceText, argumentList.OpenParenToken.SpanStart);
    var lambdaHeader = string.IsNullOrEmpty(lambda.AsyncKeyword.Text)
      ? $"{lambda.ParameterList} =>"
      : $"{lambda.AsyncKeyword.Text} {lambda.ParameterList} =>";

    var blockInnerText = sourceText.ToString(TextSpan.FromBounds(block.OpenBraceToken.Span.End, block.CloseBraceToken.SpanStart));
    var formattedBlockInnerText = ReindentSnippet(blockInnerText, lineIndent + 4);

    if (string.IsNullOrEmpty(formattedBlockInnerText))
    {
      return $"({lambdaHeader}\n{Indent(lineIndent + 2)}{{\n{Indent(lineIndent + 2)}}}\n{Indent(lineIndent)})";
    }

    return $"({lambdaHeader}\n{Indent(lineIndent + 2)}{{\n{formattedBlockInnerText}\n{Indent(lineIndent + 2)}}}\n{Indent(lineIndent)})";
  }

  private static string FormatParenthesizedContent(
    SourceText sourceText,
    int contentStart,
    int contentEnd,
    int baseIndent
  )
  {
    var innerText = sourceText.ToString(TextSpan.FromBounds(contentStart, contentEnd));
    var formattedInnerText = ReindentSnippet(innerText, baseIndent + 2);
    return $"(\n{formattedInnerText}\n{Indent(baseIndent)})";
  }

  private static string ReindentSnippet(string rawText, int targetIndent)
  {
    var normalized = rawText.Replace("\r\n", "\n");
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
      "\n",
      lines.Select(line =>
      {
        if (string.IsNullOrWhiteSpace(line))
        {
          return string.Empty;
        }

        var trimmedLine = line.Length >= minimumIndent ? line[minimumIndent..] : line.TrimStart();
        return $"{Indent(targetIndent)}{trimmedLine}";
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

  private static int GetLineIndent(SourceText sourceText, int position)
  {
    var line = sourceText.Lines.GetLineFromPosition(position);
    return GetLeadingWhitespaceLength(line.ToString());
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

  private static string Indent(int size)
  {
    return new string(' ', size);
  }

  private sealed record TextReplacement(TextSpan Span, string Content);
}
