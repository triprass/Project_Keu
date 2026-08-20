namespace Project_Keu.Infrastructure.Notifications;

/// <summary>
/// Mengubah nomor telepon pegawai menjadi <c>chatId</c> yang dimengerti WAHA, yaitu
/// nomor internasional tanpa tanda "+" lalu diakhiri "@c.us".
///
/// Nomor di data kepegawaian ditulis bermacam-macam: "0812-3456-7890", "+62 812 3456 7890",
/// atau "812345678". Tanpa penyeragaman ini, sebagian pesan akan terkirim ke nomor yang
/// keliru atau ditolak WAHA.
/// </summary>
public static class WhatsAppChatId
{
    private const string Suffix = "@c.us";

    /// <summary>Panjang wajar nomor internasional; di luar rentang ini hampir pasti data rusak.</summary>
    private const int MinDigits = 8;
    private const int MaxDigits = 15;

    /// <summary>
    /// Mengembalikan chatId, atau null bila nomornya kosong atau tidak masuk akal.
    /// Nomor yang diawali "+" dianggap sudah internasional dan tidak diberi kode negara.
    /// </summary>
    public static string? FromPhoneNumber(string? rawPhoneNumber, string defaultCountryCode)
    {
        if (string.IsNullOrWhiteSpace(rawPhoneNumber))
        {
            return null;
        }

        var trimmed = rawPhoneNumber.Trim();

        // Diperiksa sebelum tanda baca dibuang, karena "+" itulah penanda bahwa kode
        // negaranya sudah ikut ditulis.
        var isInternational = trimmed.StartsWith('+') || trimmed.StartsWith("00", StringComparison.Ordinal);

        var digits = new string(trimmed.Where(char.IsAsciiDigit).ToArray());

        if (digits.Length == 0)
        {
            return null;
        }

        var countryCode = new string(defaultCountryCode.Where(char.IsAsciiDigit).ToArray());

        if (isInternational)
        {
            // "0062..." ditulis sebagian orang sebagai ganti "+62...".
            digits = digits.TrimStart('0');
        }
        else if (countryCode.Length > 0)
        {
            digits = digits.StartsWith('0')
                ? countryCode + digits.TrimStart('0')
                : digits.StartsWith(countryCode, StringComparison.Ordinal)
                    ? digits
                    : countryCode + digits;
        }

        return digits.Length is >= MinDigits and <= MaxDigits
            ? digits + Suffix
            : null;
    }
}
