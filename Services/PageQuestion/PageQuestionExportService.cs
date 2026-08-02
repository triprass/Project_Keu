using ClosedXML.Excel;
using Project_Keu.Infrastructure;

namespace Project_Keu.Services.PageQuestion;

/// <summary>
/// Menghasilkan berkas Excel (.xlsx) asli, bukan CSV, supaya hasil unduhan bisa
/// membawa garis tabel, filter kolom, dan penomoran baris.
/// </summary>
public sealed class PageQuestionExportService
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private const string SheetName = "Daftar Pertanyaan";

    private static readonly string[] Headers =
    [
        "No",
        "No. Pertanyaan",
        "Tgl Dibuat",
        "Judul",
        "Pertanyaan",
        "Kategori",
        "Pegawai",
        "Status"
    ];

    private readonly AppTimeZone _appTimeZone;

    public PageQuestionExportService(AppTimeZone appTimeZone)
    {
        _appTimeZone = appTimeZone;
    }

    public byte[] BuildWorkbook(IReadOnlyCollection<PageQuestionQueryService.QuestionResponse> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SheetName);

        WriteHeader(sheet);
        WriteRows(sheet, rows);

        var lastRow = 1 + rows.Count;
        var tableRange = sheet.Range(1, 1, Math.Max(lastRow, 2), Headers.Length);

        ApplyBorders(tableRange);

        // Filter dropdown pada baris header.
        tableRange.SetAutoFilter();

        // Header tetap terlihat saat digulir.
        sheet.SheetView.FreezeRows(1);

        AdjustLayout(sheet, lastRow);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteHeader(IXLWorksheet sheet)
    {
        for (var i = 0; i < Headers.Length; i++)
        {
            sheet.Cell(1, i + 1).SetValue(Headers[i]);
        }

        var headerRow = sheet.Range(1, 1, 1, Headers.Length);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Font.FontColor = XLColor.White;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#12284C");
        headerRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRow.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Row(1).Height = 22;
    }

    private void WriteRows(IXLWorksheet sheet, IReadOnlyCollection<PageQuestionQueryService.QuestionResponse> rows)
    {
        var rowNumber = 0;

        foreach (var row in rows)
        {
            rowNumber++;
            var r = rowNumber + 1;

            sheet.Cell(r, 1).SetValue(rowNumber);
            SetText(sheet.Cell(r, 2), row.QuestionNo);

            // Ditulis sebagai tanggal asli (bukan teks) agar bisa diurutkan dan
            // difilter sebagai tanggal di Excel. Nilainya waktu lokal, sama dengan
            // yang tampil di tabel aplikasi.
            var createdLocal = _appTimeZone.ToLocal(row.CreatedAt);
            sheet.Cell(r, 3).SetValue(createdLocal);
            sheet.Cell(r, 3).Style.DateFormat.Format = "dd-mm-yyyy hh:mm";

            SetText(sheet.Cell(r, 4), row.Title);
            SetText(sheet.Cell(r, 5), row.QuestionText);
            SetText(sheet.Cell(r, 6), row.CategoryName);
            SetText(sheet.Cell(r, 7), row.EmployeeName);
            SetText(sheet.Cell(r, 8), row.StatusName);

            sheet.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Cell(r, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
    }

    /// <summary>
    /// Menulis nilai sebagai teks. Berbeda dengan CSV, xlsx menyimpan tipe sel secara
    /// eksplisit: string tetap jadi string dan rumus hanya lahir dari elemen formula,
    /// sehingga isi pertanyaan yang diawali "=" atau "+" tidak dieksekusi Excel.
    /// </summary>
    private static void SetText(IXLCell cell, string? value)
    {
        cell.SetValue(value ?? string.Empty);
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
    }

    private static void ApplyBorders(IXLRange range)
    {
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml("#12284C");
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorderColor = XLColor.FromHtml("#9DB2CC");
    }

    private static void AdjustLayout(IXLWorksheet sheet, int lastRow)
    {
        sheet.Columns(1, Headers.Length).AdjustToContents();

        // AdjustToContents bisa membuat kolom pertanyaan sangat lebar; dibatasi
        // supaya berkas tetap enak dibaca dan muat dicetak.
        sheet.Column(1).Width = 6;
        ClampWidth(sheet.Column(2), 14, 22);
        ClampWidth(sheet.Column(3), 16, 20);
        ClampWidth(sheet.Column(4), 18, 34);
        ClampWidth(sheet.Column(5), 30, 70);
        ClampWidth(sheet.Column(6), 18, 30);
        ClampWidth(sheet.Column(7), 18, 30);
        ClampWidth(sheet.Column(8), 14, 20);

        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.SetRowsToRepeatAtTop(1, 1);

        if (lastRow > 1)
        {
            sheet.Range(2, 1, lastRow, Headers.Length).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        }
    }

    private static void ClampWidth(IXLColumn column, double min, double max)
    {
        column.Width = Math.Clamp(column.Width, min, max);
    }
}
