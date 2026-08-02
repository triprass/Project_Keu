using System.Text;
using Project_Keu.Services.AdminDashboardV2;

namespace Project_Keu.Services.AdminDashboardV2;

public class AdminDashboardV2ExportService
{
    public byte[] BuildExcelCompatibleCsv(
        IReadOnlyCollection<AdminDashboardV2QueryService.QuestionResponse> rows)
    {
        var sb = new StringBuilder();

        // UTF-8 BOM agar Excel membaca karakter dengan benar
        sb.Append('\uFEFF');

        sb.AppendLine("Question No,Title,Question,Category,Status,Employee,Created At");

        foreach (var row in rows)
        {
            sb.Append(EscapeCsv(row.QuestionNo)).Append(',')
              .Append(EscapeCsv(row.Title)).Append(',')
              .Append(EscapeCsv(row.QuestionText)).Append(',')
              .Append(EscapeCsv(row.CategoryName)).Append(',')
              .Append(EscapeCsv(row.StatusName)).Append(',')
              .Append(EscapeCsv(row.EmployeeName)).Append(',')
              .Append(EscapeCsv(row.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")))
              .AppendLine();
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string EscapeCsv(string? input)
    {
        var value = input ?? string.Empty;

        if (value.Contains('"'))
            value = value.Replace("\"", "\"\"");

        var mustQuote = value.Contains(',') || value.Contains('\n') || value.Contains('\r') || value.Contains('"');
        return mustQuote ? $"\"{value}\"" : value;
    }
}
