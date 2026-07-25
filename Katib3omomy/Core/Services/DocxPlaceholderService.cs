using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Katib3omomy.Core.Services;

public partial class DocxPlaceholderService : IDocxPlaceholderService
{
    private static readonly Regex PlaceholderRegex = MyPlaceholderRegex();

    [GeneratedRegex(@"\*([^*]+)\*")]
    private static partial Regex MyPlaceholderRegex();

    public Task<List<string>> ExtractPlaceholdersAsync(string filePath)
    {
        return Task.Run(() => ExtractPlaceholders(filePath));
    }

    public Task<string> ExtractPlainTextAsync(string filePath)
    {
        return Task.Run(() => ExtractPlainText(filePath));
    }

    public Task<string> GenerateDocumentAsync(string templatePath, Dictionary<string, string> values, string outputDir, string baseFileName)
    {
        return Task.Run(() => GenerateDocument(templatePath, values, outputDir, baseFileName));
    }

    public Task<string> GenerateDocumentFromPlainTextAsync(string content, Dictionary<string, string> values, string outputDir, string baseFileName)
    {
        return Task.Run(() => GenerateFromPlainText(content, values, outputDir, baseFileName));
    }

    public bool TemplateHasTables(string filePath)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var doc = WordprocessingDocument.Open(fs, false);
            return doc.MainDocumentPart?.Document?.Body?.Descendants<Table>().Any() == true;
        }
        catch
        {
            return false;
        }
    }

    public bool IsValidDocx(string filePath)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var doc = WordprocessingDocument.Open(fs, false);
            return doc.MainDocumentPart?.Document?.Body is not null;
        }
        catch
        {
            return false;
        }
    }

    private List<string> ExtractPlaceholders(string filePath)
    {
        var placeholders = new HashSet<string>();

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var doc = WordprocessingDocument.Open(fs, false);

        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is not null)
            CollectPlaceholders(body, placeholders);

        foreach (var headerPart in doc.MainDocumentPart!.HeaderParts)
        {
            if (headerPart.RootElement is not null)
                CollectPlaceholders(headerPart.RootElement, placeholders);
        }

        foreach (var footerPart in doc.MainDocumentPart.FooterParts)
        {
            if (footerPart.RootElement is not null)
                CollectPlaceholders(footerPart.RootElement, placeholders);
        }

        return placeholders.ToList();
    }

    private static void CollectPlaceholders(OpenXmlElement parent, HashSet<string> placeholders)
    {
        foreach (var p in parent.Descendants<Paragraph>())
        {
            var text = string.Concat(p.Descendants<Text>().Select(t => t.Text));
            foreach (Match match in PlaceholderRegex.Matches(text))
                placeholders.Add(match.Groups[1].Value.Trim());
        }
    }

    private string ExtractPlainText(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var doc = WordprocessingDocument.Open(fs, false);

        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null)
            return "\u0644\u0627 \u064A\u0648\u062C\u062F \u0646\u0635 \u0642\u0627\u0628\u0644 \u0644\u0644\u0627\u0633\u062A\u062E\u0631\u0627\u062C \u0641\u064A \u0647\u0630\u0627 \u0627\u0644\u0642\u0627\u0644\u0628";

        var sb = new StringBuilder();

        foreach (var element in body.Elements())
        {
            if (element is Paragraph p)
            {
                var text = ExtractParagraphText(p);
                if (!string.IsNullOrEmpty(text))
                    sb.AppendLine(text);
            }
            else if (element is Table table)
            {
                ExtractTableText(table, sb);
                sb.AppendLine();
            }
        }

        var result = sb.ToString().Trim();
        return string.IsNullOrEmpty(result)
            ? "\u0644\u0627 \u064A\u0648\u062C\u062F \u0646\u0635 \u0642\u0627\u0628\u0644 \u0644\u0644\u0627\u0633\u062A\u062E\u0631\u0627\u062C \u0641\u064A \u0647\u0630\u0627 \u0627\u0644\u0642\u0627\u0644\u0628"
            : result;
    }

    private static string ExtractParagraphText(Paragraph p)
    {
        var runs = p.Descendants<Run>().ToList();
        if (runs.Count == 0) return string.Empty;

        var text = string.Concat(runs.Select(r => r.InnerText)).Trim();

        if (string.IsNullOrEmpty(text)) return string.Empty;

        var hasBiDi = p.GetFirstChild<ParagraphProperties>()?.GetFirstChild<BiDi>() is not null;
        var hasArabic = text.Any(c => c >= '\u0600' && c <= '\u06FF' ||
                                      c >= '\u0750' && c <= '\u077F' ||
                                      c >= '\u08A0' && c <= '\u08FF' ||
                                      c >= '\uFB50' && c <= '\uFDFF' ||
                                      c >= '\uFE70' && c <= '\uFEFF');

        if (hasBiDi || hasArabic)
            text = "\u200F\u202B" + text + "\u202C";

        return text;
    }

    private static void ExtractTableText(Table table, StringBuilder sb)
    {
        foreach (var row in table.Elements<TableRow>())
        {
            var cells = new List<string>();
            foreach (var cell in row.Elements<TableCell>())
            {
                var cellText = string.Join(" ", cell.Descendants<Paragraph>()
                    .Select(ExtractParagraphText));
                cells.Add(cellText.Trim());
            }
            sb.AppendLine(string.Join(" \u2022 ", cells.Where(c => !string.IsNullOrEmpty(c))));
        }
    }

    private string GenerateDocument(string templatePath, Dictionary<string, string> values, string outputDir, string baseFileName)
    {
        var ms = new MemoryStream();
        using (var fs = new FileStream(templatePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            fs.CopyTo(ms);
        }
        ms.Position = 0;

        using (var doc = WordprocessingDocument.Open(ms, true))
        {
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body is not null)
                ReplacePlaceholdersInContainer(body, values);

            foreach (var headerPart in doc.MainDocumentPart!.HeaderParts)
            {
                if (headerPart.RootElement is not null)
                    ReplacePlaceholdersInContainer(headerPart.RootElement, values);
            }

            foreach (var footerPart in doc.MainDocumentPart.FooterParts)
            {
                if (footerPart.RootElement is not null)
                    ReplacePlaceholdersInContainer(footerPart.RootElement, values);
            }

            doc.Save();
        }

        var sanitizedName = SanitizeFileName(baseFileName);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        var outputFileName = $"{sanitizedName}_{timestamp}.docx";
        var outputPath = Path.Combine(outputDir, outputFileName);

        Directory.CreateDirectory(outputDir);
        File.WriteAllBytes(outputPath, ms.ToArray());

        return outputPath;
    }

    private void ReplacePlaceholdersInContainer(OpenXmlElement container, Dictionary<string, string> values)
    {
        foreach (var p in container.Descendants<Paragraph>().ToList())
        {
            MergeSplitRuns(p, values);
            ReplacePlaceholdersInParagraph(p, values);
        }
    }

    private void MergeSplitRuns(Paragraph p, Dictionary<string, string> values)
    {
        var runs = p.Elements<Run>().ToList();
        if (runs.Count < 2) return;

        bool changed;
        do
        {
            changed = false;
            var runTexts = runs.Select(r => r.InnerText).ToList();
            var fullText = string.Concat(runTexts);

            foreach (Match match in PlaceholderRegex.Matches(fullText))
            {
                int start = match.Index;
                int end = start + match.Length;

                int startRun = -1, endRun = -1;
                int offset = 0;
                for (int i = 0; i < runTexts.Count; i++)
                {
                    if (startRun == -1 && offset + runTexts[i].Length > start)
                        startRun = i;
                    offset += runTexts[i].Length;
                    if (startRun != -1 && offset >= end)
                    {
                        endRun = i;
                        break;
                    }
                }

                if (endRun > startRun)
                {
                    MergePlaceholderAcrossRuns(runs, startRun, endRun, start, end, match.Value, runTexts);
                    changed = true;
                    break;
                }
            }
        }
        while (changed);
    }

    private static void MergePlaceholderAcrossRuns(List<Run> runs, int startRunIdx, int endRunIdx, int startPos, int endPos, string placeholderText, List<string> runTexts)
    {
        int offset = 0;
        var runOffsets = new List<(int start, int end)>();
        foreach (var rt in runTexts)
        {
            runOffsets.Add((offset, offset + rt.Length));
            offset += rt.Length;
        }

        for (int i = startRunIdx; i <= endRunIdx; i++)
        {
            var (rStart, rEnd) = runOffsets[i];
            var run = runs[i];
            var textNodes = run.Elements<Text>().ToList();
            if (textNodes.Count == 0) continue;

            int segStart = Math.Max(rStart, startPos) - rStart;
            int segEnd = Math.Min(rEnd, endPos) - rStart;

            if (i == startRunIdx)
            {
                string before = runTexts[i][..segStart];
                string after = runTexts[i][segEnd..];
                textNodes[0].Text = before + placeholderText + after;
                textNodes[0].Space = SpaceProcessingModeValues.Preserve;
                for (int t = 1; t < textNodes.Count; t++)
                    textNodes[t].Text = "";
            }
            else
            {
                string before = runTexts[i][..segStart];
                string after = runTexts[i][segEnd..];
                textNodes[0].Text = before + after;
                textNodes[0].Space = SpaceProcessingModeValues.Preserve;
                for (int t = 1; t < textNodes.Count; t++)
                    textNodes[t].Text = "";
            }
        }
    }

    private void ReplacePlaceholdersInParagraph(Paragraph p, Dictionary<string, string> values)
    {
        bool hasReplacement = false;

        foreach (var text in p.Descendants<Text>().ToList())
        {
            foreach (var kvp in values)
            {
                var placeholder = $"*{kvp.Key}*";
                if (text.Text.Contains(placeholder))
                {
                    text.Text = text.Text.Replace(placeholder, $"\u200F\u202B{kvp.Value}\u202C");
                    text.Space = SpaceProcessingModeValues.Preserve;
                    hasReplacement = true;
                }
            }
        }

        if (!hasReplacement) return;

        EnsureParagraphIsBidi(p);
        foreach (var run in p.Elements<Run>())
        {
            foreach (var text in run.Elements<Text>())
            {
                if (text.Text.Contains('\u200F'))
                {
                    EnsureRunIsRtl(run);
                    break;
                }
            }
        }
    }

    private static void EnsureParagraphIsBidi(Paragraph p)
    {
        var pPr = p.GetFirstChild<ParagraphProperties>();
        if (pPr is null)
        {
            pPr = new ParagraphProperties();
            p.InsertAt(pPr, 0);
        }
        if (pPr.GetFirstChild<BiDi>() is null)
            pPr.Append(new BiDi());
    }

    private static void EnsureRunIsRtl(Run r)
    {
        var rPr = r.GetFirstChild<RunProperties>();
        if (rPr is null)
        {
            rPr = new RunProperties();
            r.InsertAt(rPr, 0);
        }
        if (rPr.GetFirstChild<RightToLeftText>() is null)
            rPr.Append(new RightToLeftText());
    }

    private string GenerateFromPlainText(string content, Dictionary<string, string> values, string outputDir, string baseFileName)
    {
        string result = content;
        foreach (var kvp in values)
            result = result.Replace($"*{kvp.Key}*", $"\u200F\u202B{kvp.Value}\u202C");

        var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = new Body();

            var lines = result.Split('\n');
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var paragraph = new Paragraph();
                var pPr = new ParagraphProperties();
                pPr.Append(new BiDi());
                paragraph.Append(pPr);

                var run = new Run();
                var rPr = new RunProperties();
                rPr.Append(new RunFonts { Ascii = "Arial", HighAnsi = "Arial", ComplexScript = "Arial" });
                rPr.Append(new FontSize { Val = "28" });
                rPr.Append(new FontSizeComplexScript { Val = "28" });
                rPr.Append(new RightToLeftText());
                run.Append(rPr);
                run.Append(new Text(trimmed) { Space = SpaceProcessingModeValues.Preserve });
                paragraph.Append(run);
                body.Append(paragraph);
            }

            var sectPr = new SectionProperties();
            sectPr.Append(new PageSize { Width = 11906, Height = 16838 });
            sectPr.Append(new PageMargin
            {
                Top = 1134,
                Right = (UInt32Value)1134U,
                Bottom = 1134,
                Left = (UInt32Value)1134U,
                Header = 0U,
                Footer = 0U
            });
            body.Append(sectPr);

            mainPart.Document.Append(body);
            mainPart.Document.Save();
        }

        ms.Position = 0;
        var sanitizedName = SanitizeFileName(baseFileName);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        var outputFileName = $"{sanitizedName}_{timestamp}.docx";
        var outputPath = Path.Combine(outputDir, outputFileName);

        Directory.CreateDirectory(outputDir);
        File.WriteAllBytes(outputPath, ms.ToArray());

        return outputPath;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Concat(name.Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) ? '_' : c));
        return string.IsNullOrWhiteSpace(sanitized) ? "document" : sanitized.Trim('_');
    }
}
