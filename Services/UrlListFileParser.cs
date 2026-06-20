using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace NetURLScanner.Services;

public static class UrlListFileParser
{
    private static readonly Regex UrlRegex = new(
        @"https?://[^\s<>""']+|www\.[^\s<>""']+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static async Task<List<string>> ParseAsync(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        return ext switch
        {
            ".csv" or ".txt" => await ParseCsvAsync(file),
            ".xlsx" or ".xls" => ParseExcel(file),
            ".pdf" => ParsePdf(file),
            _ => new List<string>()
        };
    }

    public static async Task<List<string>> ParseCsvAsync(IFormFile file)
    {
        var urls = new List<string>();
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;
            var firstCol = line.Split(',')[0].Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(firstCol) && !firstCol.Equals("url", StringComparison.OrdinalIgnoreCase))
                urls.Add(NormalizeUrl(firstCol));
        }
        return urls;
    }

    public static List<string> ParseExcel(IFormFile file)
    {
        var urls = new List<string>();
        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();
        foreach (var row in sheet.RowsUsed())
        {
            var cell = row.Cell(1).GetString().Trim();
            if (string.IsNullOrWhiteSpace(cell) || cell.Equals("url", StringComparison.OrdinalIgnoreCase))
                continue;
            urls.Add(NormalizeUrl(cell));
        }
        return urls;
    }

    public static List<string> ParsePdf(IFormFile file)
    {
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var stream = file.OpenReadStream();
        using var pdf = new PdfDocument(new PdfReader(stream));
        for (int i = 1; i <= pdf.GetNumberOfPages(); i++)
        {
            var text = PdfTextExtractor.GetTextFromPage(pdf.GetPage(i), new LocationTextExtractionStrategy());
            foreach (Match m in UrlRegex.Matches(text))
                urls.Add(NormalizeUrl(m.Value));
        }
        return urls.ToList();
    }

    private static string NormalizeUrl(string raw)
    {
        raw = raw.Trim().TrimEnd('.', ',', ';');
        if (raw.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            return "https://" + raw;
        return raw;
    }
}
