using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace Nabrh.WordExporter;

internal static class Program
{
    private const int WdAlertsNone = 0;
    private const int WdFormatDocumentDefault = 16;
    private const int WdAlignParagraphRight = 2;
    private const int WdReadingOrderRtl = 0;

    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        try
        {
            if (args.Length == 1 && args[0].Equals("--capability", StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(WordCapabilityDetector.Detect());
                return 0;
            }

            if (args.Length == 2 && args[0].Equals("--request", StringComparison.OrdinalIgnoreCase))
            {
                var request = JsonSerializer.Deserialize<WordExportRequest>(
                    File.ReadAllText(args[1], Encoding.UTF8), JsonOptions)
                    ?? throw new InvalidDataException("The Word export request is empty or invalid.");

                var response = Export(request);
                WriteJson(response);
                return response.Success ? 0 : 2;
            }

            WriteJson(new WordExportResponse(false, null,
                "Usage: Nabrh.WordExporter --capability | --request <request.json>"));
            return 64;
        }
        catch (Exception ex)
        {
            WriteJson(new WordExportResponse(false, null, ex.Message));
            return 1;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private static void WriteJson<T>(T value) => Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions));

    private static WordExportResponse Export(WordExportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OutputPath))
            return new(false, null, "An output path is required.");

        var capability = WordCapabilityDetector.Detect();
        if (!capability.IsAvailable)
            return new(false, null, capability.Message);

        string outputPath = Path.GetFullPath(request.OutputPath);
        if (!outputPath.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            outputPath += ".docx";

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        object? word = null;
        object? document = null;

        try
        {
            var wordType = Type.GetTypeFromProgID("Word.Application", throwOnError: false)
                ?? throw new InvalidOperationException("Microsoft Word COM registration was not found.");

            word = Activator.CreateInstance(wordType)
                ?? throw new InvalidOperationException("Microsoft Word could not be started.");

            dynamic app = word;
            app.Visible = false;
            app.DisplayAlerts = WdAlertsNone;

            document = app.Documents.Add();
            dynamic doc = document;
            dynamic selection = app.Selection;

            ConfigurePage(doc);
            InsertTitle(selection, string.IsNullOrWhiteSpace(request.MeetingTitle) ? "تقرير اجتماع" : request.MeetingTitle);

            if (!string.IsNullOrWhiteSpace(request.MeetingDate))
                InsertMetadata(selection, $"التاريخ: {request.MeetingDate}");

            if (!string.IsNullOrWhiteSpace(request.SummaryMarkdown))
            {
                InsertHeading(selection, "ملخص الاجتماع", 18);
                InsertMarkdown(selection, request.SummaryMarkdown);
            }

            if (request.Transcript.Count > 0)
            {
                InsertHeading(selection, "التفريغ النصي", 18);
                foreach (var segment in request.Transcript)
                {
                    var prefix = BuildTranscriptPrefix(segment);
                    InsertTranscriptSegment(selection, prefix, segment.Text);
                }
            }

            doc.SaveAs2(outputPath, WdFormatDocumentDefault);
            doc.Close(false);
            document = null;
            app.Quit(false);
            word = null;

            if (request.OpenAfterExport)
            {
                Process.Start(new ProcessStartInfo(outputPath) { UseShellExecute = true });
            }

            return new(true, outputPath, null);
        }
        catch (COMException ex)
        {
            return new(false, null, $"Microsoft Word failed to export the document (0x{ex.HResult:X8}): {ex.Message}");
        }
        finally
        {
            if (document is not null)
            {
                try { ((dynamic)document).Close(false); } catch { }
                ReleaseComObject(document);
            }

            if (word is not null)
            {
                try { ((dynamic)word).Quit(false); } catch { }
                ReleaseComObject(word);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    private static void ConfigurePage(dynamic document)
    {
        document.PageSetup.TopMargin = 56.7f;
        document.PageSetup.BottomMargin = 56.7f;
        document.PageSetup.LeftMargin = 56.7f;
        document.PageSetup.RightMargin = 56.7f;
    }

    private static void ConfigureRtl(dynamic selection, float size = 12, bool bold = false)
    {
        selection.ParagraphFormat.Alignment = WdAlignParagraphRight;
        selection.ParagraphFormat.ReadingOrder = WdReadingOrderRtl;
        selection.Font.Name = "ITF Qomra Arabic";
        selection.Font.NameBi = "ITF Qomra Arabic";
        selection.Font.Size = size;
        selection.Font.Bold = bold ? 1 : 0;
    }

    private static void InsertTitle(dynamic selection, string title)
    {
        ConfigureRtl(selection, 24, true);
        selection.TypeText(title.Trim());
        selection.TypeParagraph();
        selection.TypeParagraph();
    }

    private static void InsertMetadata(dynamic selection, string text)
    {
        ConfigureRtl(selection, 11, false);
        selection.Font.Color = 0x666666;
        selection.TypeText(text);
        selection.TypeParagraph();
        selection.Font.Color = 0x000000;
        selection.TypeParagraph();
    }

    private static void InsertHeading(dynamic selection, string text, float size)
    {
        ConfigureRtl(selection, size, true);
        selection.Font.Color = 0x233315;
        selection.TypeText(text);
        selection.TypeParagraph();
        selection.Font.Color = 0x000000;
    }

    private static void InsertMarkdown(dynamic selection, string markdown)
    {
        foreach (var rawLine in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            string line = rawLine.TrimEnd();
            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                InsertHeading(selection, line[4..], 14);
            }
            else if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                InsertHeading(selection, line[3..], 16);
            }
            else if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                InsertHeading(selection, line[2..], 18);
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                ConfigureRtl(selection);
                selection.TypeText($"• {StripMarkdown(line[2..])}");
                selection.TypeParagraph();
            }
            else
            {
                ConfigureRtl(selection);
                selection.TypeText(StripMarkdown(line));
                selection.TypeParagraph();
            }
        }

        selection.TypeParagraph();
    }

    private static void InsertTranscriptSegment(dynamic selection, string prefix, string text)
    {
        ConfigureRtl(selection, 10, true);
        selection.Font.Color = 0x795F2D;
        selection.TypeText(prefix);
        selection.Font.Color = 0x000000;
        selection.Font.Bold = 0;
        selection.Font.Size = 12;
        selection.TypeText(text.Trim());
        selection.TypeParagraph();
    }

    private static string BuildTranscriptPrefix(TranscriptSegment segment)
    {
        var fields = new List<string>();
        if (!string.IsNullOrWhiteSpace(segment.Timestamp)) fields.Add(segment.Timestamp.Trim());
        if (!string.IsNullOrWhiteSpace(segment.Speaker)) fields.Add(segment.Speaker.Trim());
        return fields.Count == 0 ? string.Empty : $"[{string.Join(" — ", fields)}] ";
    }

    private static string StripMarkdown(string value) => value
        .Replace("**", string.Empty, StringComparison.Ordinal)
        .Replace("__", string.Empty, StringComparison.Ordinal)
        .Replace("`", string.Empty, StringComparison.Ordinal)
        .Trim();

    private static void ReleaseComObject(object value)
    {
        try
        {
            if (Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
        }
        catch { }
    }
}

internal static class WordCapabilityDetector
{
    public static WordCapability Detect()
    {
        bool progIdRegistered = Type.GetTypeFromProgID("Word.Application", throwOnError: false) is not null;
        string? executablePath = FindExecutablePath();
        string? officeVersion = ReadClickToRunValue("VersionToReport");
        string? officeProducts = ReadClickToRunValue("ProductReleaseIds");
        string? platform = ReadClickToRunValue("Platform");
        bool available = progIdRegistered && !string.IsNullOrWhiteSpace(executablePath);

        string message = available
            ? "Microsoft Word Desktop and its COM engine are available."
            : progIdRegistered
                ? "Microsoft Word is registered, but WINWORD.EXE could not be located. Repair Office before exporting."
                : "Microsoft Word Desktop is not installed. Word export will be unavailable.";

        return new(available, progIdRegistered, executablePath, officeVersion, officeProducts, platform, message);
    }

    private static string? FindExecutablePath()
    {
        const string appPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\WINWORD.EXE";
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var key = baseKey.OpenSubKey(appPath);
                if (key?.GetValue(null) is string path && File.Exists(path)) return path;
            }
            catch { }
        }

        return null;
    }

    private static string? ReadClickToRunValue(string name)
    {
        const string path = @"SOFTWARE\Microsoft\Office\ClickToRun\Configuration";
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = baseKey.OpenSubKey(path);
                if (key?.GetValue(name) is string value && !string.IsNullOrWhiteSpace(value)) return value;
            }
            catch { }
        }

        return null;
    }
}

internal sealed record WordCapability(
    bool IsAvailable,
    bool ProgIdRegistered,
    string? ExecutablePath,
    string? OfficeVersion,
    string? OfficeProducts,
    string? Platform,
    string Message);

internal sealed record WordExportResponse(bool Success, string? OutputPath, string? Error);

internal sealed class WordExportRequest
{
    public string OutputPath { get; init; } = string.Empty;
    public string MeetingTitle { get; init; } = string.Empty;
    public string? MeetingDate { get; init; }
    public string? SummaryMarkdown { get; init; }
    public List<TranscriptSegment> Transcript { get; init; } = [];
    public bool OpenAfterExport { get; init; }
}

internal sealed class TranscriptSegment
{
    public string? Timestamp { get; init; }
    public string? Speaker { get; init; }
    public string Text { get; init; } = string.Empty;
}
