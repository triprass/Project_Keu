using System.Text.RegularExpressions;

namespace Project_Keu.Infrastructure;

/// <summary>Makna sebuah status bagi tampilan: sudah ditangani, belum, atau tak tergolong.</summary>
public enum QuestionStatusKind
{
    Neutral,
    Pending,
    Answered
}

/// <summary>
/// Menentukan warna lencana status pertanyaan dari data, bukan dari tebakan longgar.
///
/// Aturan sebelumnya memeriksa apakah nama status mengandung kata "jawab" — dan itu
/// benar untuk "Telah Dijawab" maupun "Belum Dijawab", sehingga keduanya tampil
/// dengan warna yang sama. Di sini penanda "belum ditangani" diperiksa lebih dulu,
/// baru penanda "sudah ditangani"; keduanya dicocokkan sebagai kata utuh.
/// </summary>
public static partial class QuestionStatusStyle
{
    private static readonly string[] PendingHints =
        ["PENDING", "BELUM", "OPEN", "BARU", "MENUNGGU", "DRAFT", "PROSES", "ANTRE"];

    private static readonly string[] AnsweredHints =
        ["ANSWERED", "TERJAWAB", "DIJAWAB", "TELAH", "SUDAH", "SELESAI", "CLOSED", "DONE"];

    /// <summary>Hanya kode heksadesimal atau nama warna CSS yang aman dipakai pada atribut style.</summary>
    [GeneratedRegex("^(#[0-9a-fA-F]{3,8}|[a-zA-Z]{3,20})$")]
    private static partial Regex ColorPattern();

    private const string FallbackColor = "#64748b";

    /// <summary>
    /// Golongan status berdasarkan kode dan namanya. Kode diperiksa lebih dulu karena
    /// itulah nilai tetap yang dipakai kode program; nama bisa diubah pengelola.
    /// </summary>
    public static QuestionStatusKind Classify(string? code, string? name)
    {
        if (Mentions(code, PendingHints) || Mentions(name, PendingHints))
        {
            return QuestionStatusKind.Pending;
        }

        if (Mentions(code, AnsweredHints) || Mentions(name, AnsweredHints))
        {
            return QuestionStatusKind.Answered;
        }

        return QuestionStatusKind.Neutral;
    }

    /// <summary>Nama golongan untuk dikirim ke peramban: "answered", "pending", atau "neutral".</summary>
    public static string ClassifyToken(string? code, string? name) =>
        Classify(code, name) switch
        {
            QuestionStatusKind.Answered => "answered",
            QuestionStatusKind.Pending => "pending",
            _ => "neutral"
        };

    /// <summary>
    /// Warna yang aman ditulis ke atribut style. Nilai yang tidak dikenali diganti
    /// warna netral, sehingga isi kolom tidak bisa menyelipkan properti CSS lain.
    /// </summary>
    public static string SafeColor(string? value) =>
        !string.IsNullOrWhiteSpace(value) && ColorPattern().IsMatch(value.Trim())
            ? value.Trim()
            : FallbackColor;

    /// <summary>Benar bila kolom warna berisi nilai yang memang bisa dipakai.</summary>
    public static bool HasUsableColor(string? value) =>
        !string.IsNullOrWhiteSpace(value) && ColorPattern().IsMatch(value.Trim());

    private static bool Mentions(string? value, string[] hints) =>
        !string.IsNullOrWhiteSpace(value) &&
        hints.Any(hint => value.Contains(hint, StringComparison.OrdinalIgnoreCase));
}
