using System.Globalization;
using System.Text;

namespace AgFlow.Application.Common.Csv;

public static class CsvBuilder
{
    // UTF-8 مع BOM: Excel من غيره بيقرا الملف ANSI، وأسماء العملاء العربي بتظهر رموز
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    public static byte[] Build(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(Escape)));
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", row.Select(Escape)));

        return Utf8WithBom.GetPreamble().Concat(Utf8WithBom.GetBytes(sb.ToString())).ToArray();
    }

    private static string Escape(object? value)
    {
        switch (value)
        {
            case null: return "";
            // InvariantCulture: بدل ما الفاصلة العشرية تتغير بحسب لغة السيرفر (1,5 بدل 1.5) وتبوّظ الملف
            case decimal d: return d.ToString(CultureInfo.InvariantCulture);
            case int i: return i.ToString(CultureInfo.InvariantCulture);
            case DateTime dt: return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            case DateOnly dOnly: return dOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            case bool b: return b ? "true" : "false";
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

        // CSV Injection: اسم عميل مكتوب "=HYPERLINK(...)" أو "=cmd|..." بيتنفّذ كـ Formula لما الـ Owner يفتح الملف في Excel.
        // اسم العميل بيكتبه أي مستخدم، والملف بيتفتح عند Owner/Finance. الحل: ' قبل أي نص بيبدأ برمز Formula
        if (text.Length > 0 && "=+-@\t\r".Contains(text[0]))
            text = "'" + text;

        return text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
            ? "\"" + text.Replace("\"", "\"\"") + "\""
            : text;
    }
}
