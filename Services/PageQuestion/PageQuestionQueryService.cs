using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Infrastructure;
using Project_Keu.Models;

namespace Project_Keu.Services.PageQuestion;

public sealed class PageQuestionQueryService
{
    /// <summary>Batas atas ukuran halaman untuk permintaan biasa.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Batas keras jumlah baris export, supaya satu permintaan tidak menghabiskan memori server.</summary>
    public const int MaxExportRows = 20_000;

    /// <summary>Batas panjang kata kunci; di atas ini pencarian hanya membebani database.</summary>
    private const int MaxKeywordLength = 200;

    private const string LikeEscape = "\\";

    private readonly AppDbContext _context;
    private readonly AppTimeZone _appTimeZone;

    public PageQuestionQueryService(AppDbContext context, AppTimeZone appTimeZone)
    {
        _context = context;
        _appTimeZone = appTimeZone;
    }

    public sealed class QueryRequest
    {
        public string? Q { get; set; }
        public string? EmployeeKeyword { get; set; }
        public string? CategoryKeyword { get; set; }
        public string? StatusKeyword { get; set; }

        /// <summary>Kata kunci untuk kolom "Pertanyaan": mencakup isi, judul, dan nomor.</summary>
        public string? QuestionKeyword { get; set; }

        /// <summary>Saringan khusus kolom "No. Pertanyaan"; hanya dipakai tampilan admin.</summary>
        public string? QuestionNoKeyword { get; set; }

        /// <summary>Saringan khusus kolom "Judul"; hanya dipakai tampilan admin.</summary>
        public string? TitleKeyword { get; set; }

        /// <summary>
        /// Saringan kolom "Jawaban". Mencocokkan isi jawaban mana pun milik satu
        /// pertanyaan, bukan hanya jawaban terakhir yang kebetulan ditampilkan.
        /// </summary>
        public string? AnswerKeyword { get; set; }
        public DateTime? CreatedDate { get; set; }
        public Guid? CategoryId { get; set; }
        public Guid? StatusId { get; set; }
        public Guid? CreatedByEmployee { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public sealed class QuestionResponse
    {
        public Guid Id { get; set; }
        public string QuestionNo { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string QuestionText { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;

        /// <summary>Kode status; dipakai menentukan warna lencana, bukan untuk ditampilkan.</summary>
        public string StatusCode { get; set; } = string.Empty;

        /// <summary>Warna dari master status, bila pengelola mengisinya.</summary>
        public string? StatusColor { get; set; }

        /// <summary>
        /// Golongan status siap pakai: "answered", "pending", atau "neutral". Ditentukan
        /// server supaya semua tampilan memakai aturan yang sama persis.
        /// </summary>
        public string StatusKind { get; set; } = "neutral";
        public string EmployeeName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Isi jawaban terakhir, atau string kosong bila pertanyaan belum dijawab.
        /// Yang diambil hanya satu jawaban terbaru: kolom pada tabel memang hanya
        /// menampilkan ringkasan, dan riwayat lengkapnya ada di halaman detail.
        /// </summary>
        public string AnswerText { get; set; } = string.Empty;

        /// <summary>Benar bila pertanyaan sudah punya jawaban tersimpan.</summary>
        public bool HasAnswer => !string.IsNullOrWhiteSpace(AnswerText);

        /// <summary>
        /// Tanggal siap tampil dalam zona waktu aplikasi. Dikirim dari server supaya
        /// yang dilihat pengguna sama persis dengan yang dipakai saat memfilter, tidak
        /// bergantung pada zona waktu browser masing-masing.
        /// </summary>
        public string CreatedAtDisplay { get; set; } = string.Empty;
    }

    public sealed class QueryResult
    {
        public List<QuestionResponse> Questions { get; set; } = new();
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }

        /// <summary>
        /// Jumlah baris dalam lingkup halaman tanpa filter pengguna, dipakai untuk
        /// membedakan "belum ada data" dari "tidak ada yang cocok". Hanya dihitung
        /// terpisah ketika <see cref="TotalItems"/> nol; selain itu nilainya sama.
        /// </summary>
        public int TotalUnfiltered { get; set; }
    }

    public async Task<QueryResult> GetQuestionsAsync(QueryRequest request, CancellationToken cancellationToken = default)
    {
        Normalize(request);

        var query = BuildFilteredQuery(request);

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)request.PageSize));

        if (request.Page > totalPages) request.Page = totalPages;

        var questions = await Project(query)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        ApplyDisplayFormatting(questions);

        // Query tambahan hanya dijalankan saat hasil kosong, untuk memilih pesan
        // yang tepat. Pada kasus normal tidak ada biaya tambahan.
        var totalUnfiltered = totalItems;

        if (totalItems == 0)
        {
            totalUnfiltered = await BaseQuery(request.CategoryId).CountAsync(cancellationToken);
        }

        return new QueryResult
        {
            Questions = questions,
            TotalItems = totalItems,
            TotalPages = totalPages,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalUnfiltered = totalUnfiltered
        };
    }

    /// <summary>
    /// Mengambil seluruh baris yang cocok untuk keperluan export. Dipisahkan dari
    /// <see cref="GetQuestionsAsync"/> karena batas <see cref="MaxPageSize"/> di sana
    /// akan memotong hasil export menjadi satu halaman saja.
    /// </summary>
    public async Task<List<QuestionResponse>> GetQuestionsForExportAsync(QueryRequest request, CancellationToken cancellationToken = default)
    {
        Normalize(request);

        var rows = await Project(BuildFilteredQuery(request))
            .Take(MaxExportRows)
            .ToListAsync(cancellationToken);

        ApplyDisplayFormatting(rows);

        return rows;
    }

    /// <summary>
    /// Rapikan input sebelum dipakai: spasi berlebih dibuang, nilai di luar batas
    /// dijepit, dan rentang tanggal terbalik ditukar supaya tetap memberi hasil.
    /// </summary>
    private static void Normalize(QueryRequest request)
    {
        request.Q = NormalizeKeyword(request.Q);
        request.EmployeeKeyword = NormalizeKeyword(request.EmployeeKeyword);
        request.CategoryKeyword = NormalizeKeyword(request.CategoryKeyword);
        request.StatusKeyword = NormalizeKeyword(request.StatusKeyword);
        request.QuestionKeyword = NormalizeKeyword(request.QuestionKeyword);
        request.QuestionNoKeyword = NormalizeKeyword(request.QuestionNoKeyword);
        request.TitleKeyword = NormalizeKeyword(request.TitleKeyword);
        request.AnswerKeyword = NormalizeKeyword(request.AnswerKeyword);

        if (request.DateFrom.HasValue && request.DateTo.HasValue && request.DateFrom > request.DateTo)
        {
            (request.DateFrom, request.DateTo) = (request.DateTo, request.DateFrom);
        }

        if (request.Page < 1) request.Page = 1;
        if (request.PageSize <= 0) request.PageSize = 10;
        if (request.PageSize > MaxPageSize) request.PageSize = MaxPageSize;
    }

    private static string? NormalizeKeyword(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > MaxKeywordLength
            ? trimmed[..MaxKeywordLength]
            : trimmed;
    }

    private IQueryable<Question> BaseQuery(Guid? categoryId)
    {
        var query = _context.Questions.AsNoTracking();

        // CategoryId adalah konteks halaman (berasal dari kartu kategori), bukan
        // filter yang diisi pengguna, jadi ikut dipakai saat menghitung baseline.
        return categoryId.HasValue
            ? query.Where(x => x.CategoryId == categoryId.Value)
            : query;
    }

    private IQueryable<Question> BuildFilteredQuery(QueryRequest request)
    {
        var query = BaseQuery(request.CategoryId);

        // ILIKE, bukan Contains(). Contains() diterjemahkan menjadi LIKE biasa yang
        // di PostgreSQL bersifat case-sensitive, sehingga mencari "perjalanan" tidak
        // menemukan "Perjalanan".
        if (request.Q is not null)
        {
            var pattern = ContainsPattern(request.Q);
            query = query.Where(x =>
                (x.QuestionNo != null && EF.Functions.ILike(x.QuestionNo, pattern, LikeEscape)) ||
                (x.Title != null && EF.Functions.ILike(x.Title, pattern, LikeEscape)) ||
                (x.QuestionText != null && EF.Functions.ILike(x.QuestionText, pattern, LikeEscape)));
        }

        if (request.EmployeeKeyword is not null)
        {
            var pattern = ContainsPattern(request.EmployeeKeyword);
            query = query.Where(x =>
                x.CreatedByEmployeeNavigation != null &&
                (
                    (x.CreatedByEmployeeNavigation.FullName != null &&
                     EF.Functions.ILike(x.CreatedByEmployeeNavigation.FullName, pattern, LikeEscape)) ||
                    (x.CreatedByEmployeeNavigation.Nip != null &&
                     EF.Functions.ILike(x.CreatedByEmployeeNavigation.Nip, pattern, LikeEscape))
                ));
        }

        if (request.CategoryKeyword is not null)
        {
            var pattern = ContainsPattern(request.CategoryKeyword);
            query = query.Where(x =>
                x.Category != null &&
                x.Category.Name != null &&
                EF.Functions.ILike(x.Category.Name, pattern, LikeEscape));
        }

        if (request.StatusKeyword is not null)
        {
            var pattern = ContainsPattern(request.StatusKeyword);
            query = query.Where(x =>
                x.Status != null &&
                x.Status.Name != null &&
                EF.Functions.ILike(x.Status.Name, pattern, LikeEscape));
        }

        if (request.QuestionKeyword is not null)
        {
            var pattern = ContainsPattern(request.QuestionKeyword);
            query = query.Where(x =>
                (x.QuestionText != null && EF.Functions.ILike(x.QuestionText, pattern, LikeEscape)) ||
                (x.Title != null && EF.Functions.ILike(x.Title, pattern, LikeEscape)) ||
                (x.QuestionNo != null && EF.Functions.ILike(x.QuestionNo, pattern, LikeEscape)));
        }

        // Dua saringan berikut menyempit ke satu kolom saja. Tampilan publik hanya
        // punya satu kolom "Pertanyaan" sehingga cukup memakai QuestionKeyword yang
        // mencakup ketiganya; tampilan admin memisah kolomnya, jadi butuh saringan
        // yang sesuai dengan kolom yang dilihatnya.
        if (request.QuestionNoKeyword is not null)
        {
            var pattern = ContainsPattern(request.QuestionNoKeyword);
            query = query.Where(x => x.QuestionNo != null && EF.Functions.ILike(x.QuestionNo, pattern, LikeEscape));
        }

        if (request.TitleKeyword is not null)
        {
            var pattern = ContainsPattern(request.TitleKeyword);
            query = query.Where(x => x.Title != null && EF.Functions.ILike(x.Title, pattern, LikeEscape));
        }

        if (request.AnswerKeyword is not null)
        {
            var pattern = ContainsPattern(request.AnswerKeyword);
            query = query.Where(x => x.Answers.Any(a =>
                a.AnswerText != null && EF.Functions.ILike(a.AnswerText, pattern, LikeEscape)));
        }

        if (request.StatusId.HasValue)
        {
            query = query.Where(x => x.StatusId == request.StatusId.Value);
        }

        if (request.CreatedByEmployee.HasValue)
        {
            query = query.Where(x => x.CreatedByEmployee == request.CreatedByEmployee.Value);
        }

        // Batas hari dihitung pada zona waktu aplikasi lalu diubah ke UTC, bukan
        // dipotong pada tengah malam UTC.
        if (request.CreatedDate.HasValue)
        {
            var from = _appTimeZone.StartOfLocalDayUtc(request.CreatedDate.Value);
            var toExclusive = _appTimeZone.StartOfNextLocalDayUtc(request.CreatedDate.Value);
            query = query.Where(x => x.CreatedAt >= from && x.CreatedAt < toExclusive);
        }

        if (request.DateFrom.HasValue)
        {
            var from = _appTimeZone.StartOfLocalDayUtc(request.DateFrom.Value);
            query = query.Where(x => x.CreatedAt >= from);
        }

        if (request.DateTo.HasValue)
        {
            // Eksklusif pada awal hari berikutnya supaya tanggal akhir ikut terjaring penuh.
            var toExclusive = _appTimeZone.StartOfNextLocalDayUtc(request.DateTo.Value);
            query = query.Where(x => x.CreatedAt < toExclusive);
        }

        return query;
    }

    private static IQueryable<QuestionResponse> Project(IQueryable<Question> query)
    {
        return query
            .OrderByDescending(x => x.CreatedAt)
            // Pemecah seri agar urutan stabil; tanpa ini baris dengan CreatedAt sama
            // bisa berpindah halaman antar permintaan dan tampil ganda/hilang.
            .ThenByDescending(x => x.Id)
            .Select(x => new QuestionResponse
            {
                Id = x.Id,
                QuestionNo = x.QuestionNo ?? string.Empty,
                Title = x.Title ?? string.Empty,
                QuestionText = x.QuestionText ?? string.Empty,
                CategoryName = x.Category != null ? (x.Category.Name ?? string.Empty) : string.Empty,
                StatusName = x.Status != null ? (x.Status.Name ?? string.Empty) : string.Empty,
                StatusCode = x.Status != null ? (x.Status.Code ?? string.Empty) : string.Empty,
                StatusColor = x.Status != null ? x.Status.Color : null,
                EmployeeName = x.CreatedByEmployeeNavigation != null ? (x.CreatedByEmployeeNavigation.FullName ?? string.Empty) : string.Empty,
                CreatedAt = x.CreatedAt,

                // Urutan sama dengan halaman detail dan halaman menjawab, supaya
                // jawaban yang terbaca di tabel adalah jawaban yang sama dengan
                // yang dibuka saat barisnya diklik. Jawaban tanpa tanggal ditaruh
                // paling belakang tanpa nilai pengganti, karena DateTime.MinValue
                // ditolak kolom timestamptz.
                AnswerText = x.Answers
                    .OrderByDescending(a => a.AnsweredAt.HasValue)
                    .ThenByDescending(a => a.AnsweredAt)
                    .Select(a => a.AnswerText)
                    .FirstOrDefault() ?? string.Empty
            });
    }

    private void ApplyDisplayFormatting(List<QuestionResponse> rows)
    {
        foreach (var row in rows)
        {
            row.CreatedAtDisplay = _appTimeZone.ToLocal(row.CreatedAt).ToString("dd-MM-yyyy");

            // Penggolongan dikerjakan di sini, bukan di peramban, supaya daftar
            // pertanyaan dan beranda administrasi tidak bisa berbeda aturannya.
            row.StatusKind = QuestionStatusStyle.ClassifyToken(row.StatusCode, row.StatusName);

            if (!QuestionStatusStyle.HasUsableColor(row.StatusColor))
            {
                row.StatusColor = null;
            }
        }
    }

    /// <summary>
    /// Bentuk pola <c>%kata%</c> dengan wildcard milik pengguna dinetralkan, supaya
    /// mengetik "100%" mencari teks "100%" dan bukan cocok ke semua baris.
    /// </summary>
    private static string ContainsPattern(string value)
    {
        var escaped = value
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");

        return $"%{escaped}%";
    }
}
