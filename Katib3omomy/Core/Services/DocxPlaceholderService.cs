using System.IO;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Katib3omomy.Core.Services;

public partial class DocxPlaceholderService : IDocxPlaceholderService
{
    private static readonly Regex PlaceholderRegex = PlaceholderPattern();

    [GeneratedRegex(@"\*([^*]+)\*")]
    private static partial Regex PlaceholderPattern();

    public Task<List<string>> ExtractPlaceholdersAsync(string filePath)
    {
        return Task.Run(() =>
        {
            var placeholders = new HashSet<string>();

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var doc = WordprocessingDocument.Open(stream, false);

            if (doc.MainDocumentPart?.Document.Body is not null)
                ExtractFromBody(doc.MainDocumentPart.Document.Body, placeholders);

            foreach (var headerPart in doc.MainDocumentPart.HeaderParts)
                ExtractFromBody(headerPart.Header, placeholders);

            foreach (var footerPart in doc.MainDocumentPart.FooterParts)
                ExtractFromBody(footerPart.Footer, placeholders);

            return placeholders.OrderBy(p => p).ToList();
        });
    }

    private static void ExtractFromBody(OpenXmlElement body, HashSet<string> placeholders)
    {
        foreach (var para in body.Descendants<Paragraph>())
        {
            var fullText = string.Concat(para.Descendants<Text>().Select(t => t.Text));
            var matches = PlaceholderRegex.Matches(fullText);
            foreach (Match match in matches)
                placeholders.Add(match.Groups[1].Value.Trim());
        }
    }

    public Task<string> ExtractPlainTextAsync(string filePath)
    {
        return Task.Run(() =>
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var doc = WordprocessingDocument.Open(stream, false);

            var sb = new StringBuilder();
            if (doc.MainDocumentPart?.Document.Body is not null)
                ExtractPlainTextFromBody(doc.MainDocumentPart.Document.Body, sb);

            var result = sb.ToString().Trim();
            return string.IsNullOrEmpty(result) ? "لا يوجد نص قابل للاستخراج في هذا القالب" : result;
        });
    }

    private static void ExtractPlainTextFromBody(OpenXmlElement parent, StringBuilder sb)
    {
        foreach (var child in parent.ChildElements)
        {
            if (child is Paragraph para)
            {
                var text = string.Concat(para.Descendants<Text>().Select(t => t.Text)).Trim();
                if (!string.IsNullOrEmpty(text))
                    sb.AppendLine(text);
            }
            else if (child is Table table)
            {
                ExtractTableText(table, sb);
            }
        }
    }

    private static void ExtractTableText(Table table, StringBuilder sb)
    {
        sb.AppendLine();
        foreach (var row in table.Descendants<TableRow>())
        {
            var cells = new List<string>();
            foreach (var cell in row.Descendants<TableCell>())
            {
                var cellText = string.Join(" ", cell.Descendants<Paragraph>()
                    .Select(p => string.Concat(p.Descendants<Text>().Select(t => t.Text))));
                cells.Add(cellText.Trim());
            }
            sb.AppendLine(string.Join(" • ", cells));
        }
        sb.AppendLine();
    }

    public Task<string> GenerateDocumentAsync(string templatePath, Dictionary<string, string> values, string outputDir, string baseFileName)
    {
        return Task.Run(() =>
        {
            Directory.CreateDirectory(outputDir);

            byte[] templateBytes = File.ReadAllBytes(templatePath);
            using var memStream = new MemoryStream();
            memStream.Write(templateBytes, 0, templateBytes.Length);
            memStream.Position = 0;

            using var doc = WordprocessingDocument.Open(memStream, true);

            var body = doc.MainDocumentPart!.Document.Body!;
            ReplacePlaceholdersInBody(body, values);

            foreach (var headerPart in doc.MainDocumentPart.HeaderParts)
                ReplacePlaceholdersInBody(headerPart.Header, values);

            foreach (var footerPart in doc.MainDocumentPart.FooterParts)
                ReplacePlaceholdersInBody(footerPart.Footer, values);

            doc.Save();

            var safeName = SanitizeFileName(baseFileName);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
            var outputFileName = $"{safeName}_{timestamp}.docx";
            var outputPath = Path.Combine(outputDir, outputFileName);

            File.WriteAllBytes(outputPath, memStream.ToArray());

            return outputPath;
        });
    }

    private void ReplacePlaceholdersInBody(OpenXmlElement body, Dictionary<string, string> values)
    {
        foreach (var para in body.Descendants<Paragraph>().ToList())
        {
            MergeSplitRuns(para);

            foreach (var text in para.Descendants<Text>().ToList())
            {
                string original = text.Text;
                string result = PlaceholderRegex.Replace(original, match =>
                {
                    string key = match.Groups[1].Value.Trim();
                    if (values.TryGetValue(key, out string val))
                        return $"\u200F\u202B{val}\u202C";
                    return match.Value;
                });

                if (result != original)
                {
                    text.Text = result;
                    text.Space = SpaceProcessingModeValues.Preserve;

                    var run = text.Ancestors<Run>().FirstOrDefault();
                    if (run != null)
                    {
                        EnsureRunIsRtl(run);
                        var p = run.Ancestors<Paragraph>().FirstOrDefault();
                        if (p != null)
                            EnsureParagraphIsBidi(p);
                    }
                }
            }
        }
    }

    private void MergeSplitRuns(Paragraph paragraph)
    {
        bool merged;
        do
        {
            merged = false;
            var runs = paragraph.Descendants<Run>().ToList();
            if (runs.Count < 2) break;

            var runTexts = runs.Select(r => r.InnerText).ToList();
            var offsets = new List<int> { 0 };
            foreach (var t in runTexts)
                offsets.Add(offsets.Last() + t.Length);
            var fullText = string.Concat(runTexts);

            foreach (Match match in PlaceholderRegex.Matches(fullText))
            {
                int startPos = match.Index;
                int endPos = match.Index + match.Length - 1;

                int startRun = FindRunIndex(offsets, startPos);
                int endRun = FindRunIndex(offsets, endPos);

                if (startRun >= endRun) continue;

                merged = true;

                string fullPlaceholder = match.Value;
                int placeholderEnd = match.Index + match.Length;

                int hostStart = startRun;

                // Phase 1a: Host run — embed the full placeholder
                Run hostRun = runs[hostStart];
                int localStart = startPos - offsets[hostStart];
                string hostOldText = runTexts[hostStart];
                int hostOverlapLen = Math.Min(placeholderEnd, offsets[hostStart + 1]) - startPos;
                string hostPrefix = hostOldText[..localStart];
                string hostAfterOverlap = hostOldText[(localStart + hostOverlapLen)..];
                string hostNewText = hostPrefix + fullPlaceholder + hostAfterOverlap;
                SetRunInnerText(hostRun, hostNewText);

                // Phase 1b: Middle runs — remove their placeholder fragment
                for (int i = hostStart + 1; i < endRun; i++)
                {
                    Run midRun = runs[i];
                    string midOldText = runTexts[i];
                    int midFragmentStart = Math.Max(startPos, offsets[i]);
                    int midFragmentEnd = Math.Min(placeholderEnd, offsets[i + 1]);
                    int midLocalStart = midFragmentStart - offsets[i];
                    int midLocalEnd = midFragmentEnd - offsets[i];
                    string midPrefix = midOldText[..midLocalStart];
                    string midSuffix = midOldText[midLocalEnd..];
                    SetRunInnerText(midRun, midPrefix + midSuffix);
                }

                // Phase 1c: End run — remove its placeholder fragment
                if (endRun > hostStart)
                {
                    Run endRunObj = runs[endRun];
                    string endOldText = runTexts[endRun];
                    int endFragmentStart = Math.Max(startPos, offsets[endRun]);
                    int endLocalStart = endFragmentStart - offsets[endRun];
                    int endOverlapLen = Math.Min(placeholderEnd, offsets[endRun + 1]) - endFragmentStart;
                    string endPrefix = endOldText[..endLocalStart];
                    string endSuffix = endOldText[(endLocalStart + endOverlapLen)..];
                    SetRunInnerText(endRunObj, endPrefix + endSuffix);
                }

                break;
            }
        } while (merged);
    }

    private static int FindRunIndex(List<int> offsets, int position)
    {
        for (int i = 0; i < offsets.Count - 1; i++)
            if (position >= offsets[i] && position < offsets[i + 1])
                return i;
        return offsets.Count - 2;
    }

    private static void SetRunInnerText(Run run, string text)
    {
        var textElements = run.Descendants<Text>().ToList();
        if (textElements.Count == 0)
        {
            var newText = new Text(text);
            newText.Space = SpaceProcessingModeValues.Preserve;
            run.Append(newText);
        }
        else
        {
            textElements[0].Text = text;
            textElements[0].Space = SpaceProcessingModeValues.Preserve;
            for (int i = 1; i < textElements.Count; i++)
                textElements[i].Text = string.Empty;
        }
    }

    private static void EnsureParagraphIsBidi(Paragraph p)
    {
        if (p.ParagraphProperties == null)
            p.ParagraphProperties = new ParagraphProperties();
        if (p.ParagraphProperties.BiDi == null)
            p.ParagraphProperties.BiDi = new BiDi();
    }

    private static void EnsureRunIsRtl(Run r)
    {
        if (r.RunProperties == null)
            r.RunProperties = new RunProperties();
        if (r.RunProperties.RightToLeftText == null)
            r.RunProperties.RightToLeftText = new RightToLeftText
            {
                Val = OnOffValue.FromBoolean(true)
            };
    }

    public Task<string> GenerateDocumentFromPlainTextAsync(string content, Dictionary<string, string> values, string outputDir, string baseFileName)
    {
        return Task.Run(() =>
        {
            Directory.CreateDirectory(outputDir);

            string processed = content;
            foreach (var kvp in values)
            {
                if (!string.IsNullOrEmpty(kvp.Key))
                    processed = processed.Replace($"*{kvp.Key}*", $"\u200F\u202B{kvp.Value}\u202C");
            }

            using var memStream = new MemoryStream();
            using var doc = WordprocessingDocument.Create(memStream, WordprocessingDocumentType.Document);

            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = new Body();
            mainPart.Document.Append(body);

            var lines = processed.Split('\n');
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var para = new Paragraph();
                var paraProps = new ParagraphProperties();
                paraProps.Append(new BiDi());
                para.Append(paraProps);

                var run = new Run();
                var runProps = new RunProperties();
                runProps.Append(new RunFonts
                {
                    Ascii = "Arial",
                    HighAnsi = "Arial",
                    ComplexScript = "Arial"
                });
                runProps.Append(new FontSize { Val = "28" });
                runProps.Append(new FontSizeComplexScript { Val = "28" });
                runProps.Append(new RightToLeftText
                {
                    Val = OnOffValue.FromBoolean(true)
                });
                run.Append(runProps);

                var text = new Text(trimmed) { Space = SpaceProcessingModeValues.Preserve };
                run.Append(text);
                para.Append(run);
                body.Append(para);
            }

            var sectionProps = new SectionProperties();
            sectionProps.Append(new PageSize
            {
                Width = 11906,
                Height = 16838
            });
            sectionProps.Append(new PageMargin
            {
                Top = 1134,
                Bottom = 1134,
                Left = 1134,
                Right = 1134
            });
            body.Append(sectionProps);

            doc.Save();

            var safeName = SanitizeFileName(baseFileName);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
            var outputFileName = $"{safeName}_{timestamp}.docx";
            var outputPath = Path.Combine(outputDir, outputFileName);

            File.WriteAllBytes(outputPath, memStream.ToArray());

            return outputPath;
        });
    }

    public bool IsValidDocx(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var doc = WordprocessingDocument.Open(stream, false);
            return doc.MainDocumentPart?.Document?.Body != null;
        }
        catch
        {
            return false;
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Concat(name.Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) ? '_' : c));
        return string.IsNullOrWhiteSpace(sanitized) ? "document" : sanitized.Trim('_');
    }
}
