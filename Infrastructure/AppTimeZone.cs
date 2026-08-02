namespace Project_Keu.Infrastructure;

/// <summary>
/// Zona waktu tampilan aplikasi. Kolom timestamp disimpan dalam UTC, sedangkan
/// pengguna membaca dan memfilter memakai tanggal lokal. Tanpa konversi ini,
/// pertanyaan yang dibuat 2 Agustus 07:00 WIT (= 1 Agustus 22:00 UTC) tampil
/// sebagai 2 Agustus tetapi tidak ikut terjaring saat difilter 2 Agustus.
/// </summary>
public sealed class AppTimeZone
{
    private const string ConfigurationKey = "App:TimeZone";
    private const string DefaultTimeZoneId = "Asia/Jayapura";

    public AppTimeZone(IConfiguration configuration, ILogger<AppTimeZone> logger)
    {
        var timeZoneId = configuration[ConfigurationKey];

        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            timeZoneId = DefaultTimeZoneId;
        }

        try
        {
            TimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            logger.LogError(ex, "Zona waktu '{TimeZoneId}' tidak dikenal, memakai UTC sebagai pengganti.", timeZoneId);
            TimeZone = TimeZoneInfo.Utc;
        }
    }

    public TimeZoneInfo TimeZone { get; }

    /// <summary>Awal hari lokal (00:00) untuk tanggal tertentu, dinyatakan dalam UTC.</summary>
    public DateTime StartOfLocalDayUtc(DateTime localDate)
    {
        var unspecified = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, TimeZone);
    }

    /// <summary>Awal hari lokal berikutnya dalam UTC, dipakai sebagai batas atas eksklusif.</summary>
    public DateTime StartOfNextLocalDayUtc(DateTime localDate)
    {
        return StartOfLocalDayUtc(localDate.Date.AddDays(1));
    }

    /// <summary>Ubah timestamp UTC dari database menjadi waktu lokal untuk ditampilkan.</summary>
    public DateTime ToLocal(DateTime utcValue)
    {
        var utc = utcValue.Kind == DateTimeKind.Utc
            ? utcValue
            : DateTime.SpecifyKind(utcValue, DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZone);
    }
}
