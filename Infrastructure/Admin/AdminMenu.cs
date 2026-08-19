namespace Project_Keu.Infrastructure.Admin;

/// <summary>
/// Satu butir menu pada bilah navigasi admin.
/// </summary>
/// <param name="Title">Label yang tampil pada menu.</param>
/// <param name="PageName">Nama halaman Razor, mis. "/Admin/Master/Categories".</param>
/// <param name="IconPath">Isi elemen &lt;svg&gt; (viewBox 0 0 24 24, stroke currentColor).</param>
/// <param name="Policy">Izin yang dibutuhkan. Menu disembunyikan bila pengguna tidak memilikinya.</param>
/// <param name="Description">Keterangan singkat, dipakai pada kartu pintasan di dasbor.</param>
public sealed record AdminMenuItem(
    string Title,
    string PageName,
    string IconPath,
    string? Policy = null,
    string? Description = null);

public sealed record AdminMenuSection(string Title, IReadOnlyList<AdminMenuItem> Items);

/// <summary>
/// Sumber tunggal susunan menu admin. Sidebar, dasbor, dan judul halaman semuanya
/// membacanya dari sini, sehingga menambah satu master cukup dilakukan di satu tempat.
/// </summary>
public static class AdminMenu
{
    // Kode izin ini sama persis dengan isi kolom code pada tb_m_permission.
    public const string PolicyQuestionsView = "questions.view";
    public const string PolicyAnswer = "questions.answer";
    public const string PolicyCategories = "categories.manage";
    public const string PolicyStatuses = "statuses.manage";
    public const string PolicyEmployees = "employees.view";
    public const string PolicyAdminUsers = "admin.users.manage";
    public const string PolicyRoles = "admin.roles.manage";

    private const string IconHome =
        """<path d="M3 10.5 12 3l9 7.5" /><path d="M5 9.8V20a1 1 0 0 0 1 1h4v-6h4v6h4a1 1 0 0 0 1-1V9.8" />""";

    private const string IconInbox =
        """<path d="M4 13h4l2 3h4l2-3h4" /><path d="M6.4 4h11.2a2 2 0 0 1 1.9 1.4L21 13v5a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-5l1.5-7.6A2 2 0 0 1 6.4 4z" />""";

    private const string IconLayers =
        """<polygon points="12 3 21 8 12 13 3 8 12 3" /><polyline points="3 13 12 18 21 13" /><polyline points="3 17.5 12 22.5 21 17.5" />""";

    private const string IconTag =
        """<path d="M20.6 13.4 12.8 21a2 2 0 0 1-2.8 0l-7-7A2 2 0 0 1 2.4 12.6V5a2 2 0 0 1 2-2H12a2 2 0 0 1 1.4.6l7.2 7.2a2 2 0 0 1 0 2.6z" /><line x1="7.5" y1="7.5" x2="7.51" y2="7.5" />""";

    private const string IconFlag =
        """<path d="M5 21V4" /><path d="M5 4.8h11.5l-1.8 3.6 1.8 3.6H5" />""";

    private const string IconUsers =
        """<path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" /><circle cx="9" cy="7" r="4" /><path d="M22 21v-2a4 4 0 0 0-3-3.9" /><path d="M16 3.1a4 4 0 0 1 0 7.8" />""";

    private const string IconShield =
        """<path d="M12 3 5 6v5.5c0 4.3 2.9 8.3 7 9.5 4.1-1.2 7-5.2 7-9.5V6l-7-3z" /><polyline points="9 12 11.2 14.2 15.5 9.9" />""";

    private const string IconKey =
        """<circle cx="7.5" cy="15.5" r="4.5" /><line x1="10.7" y1="12.3" x2="21" y2="2" /><line x1="18" y1="5" x2="20.5" y2="7.5" /><line x1="15.5" y1="7.5" x2="18" y2="10" />""";

    private const string IconIdCard =
        """<rect x="2.5" y="5" width="19" height="14" rx="2" /><circle cx="8.5" cy="11" r="2.2" /><path d="M5 16.4a3.6 3.6 0 0 1 7 0" /><line x1="15" y1="10" x2="19" y2="10" /><line x1="15" y1="13.5" x2="19" y2="13.5" />""";

    private const string IconCalendarSync =
        """<path d="M11 10v4h4"/><path d="m11 14 1.535-1.605a5 5 0 018 1.5"/><path d="M16 2v3"/><path d="m21 18-1.535 1.605a5 5 0 01-8-1.5"/><path d="M21 22v-4h-4"/><path d="M21 8.517V5a2 2 0 00-2-2H5a2 2 0 00-2 2v14a2 2 0 002 2h3.517"/><path d="M3 9h4"/><path d="M8 2v3"/>""";

    public static IReadOnlyList<AdminMenuSection> Sections { get; } =
    [
        new("Utama",
        [
            new AdminMenuItem("Beranda", "/Admin/Index", IconHome,
                Description: "Ringkasan layanan konsultasi keuangan."),
            new AdminMenuItem("Jawab Pertanyaan", "/Admin/Master/Employees", IconCalendarSync, PolicyEmployees,
                Description: "Data seluruh pertanyaan dan jawab pertanyaan."),
        ]),

        new("Master Data",
        [
            //new AdminMenuItem("Kategori", "/Admin/Master/Categories", IconLayers, PolicyCategories,
            //    Description: "Kelompok besar topik pada halaman utama."),
            new AdminMenuItem("Kategori Pertanyaan", "/Admin/Master/QuestionCategories", IconTag, PolicyCategories,
                Description: "Kategori yang dipilih pegawai saat bertanya."),
            //new AdminMenuItem("Status Pertanyaan", "/Admin/Master/QuestionStatuses", IconFlag, PolicyStatuses,
            //    Description: "Tahapan penanganan beserta warna lencananya."),
            new AdminMenuItem("Pegawai", "/Admin/Master/Employees", IconIdCard, PolicyEmployees,
                Description: "Data pegawai yang berhak mengajukan pertanyaan."),
            new AdminMenuItem("Akun Admin", "/Admin/Master/AdminUsers", IconUsers, PolicyAdminUsers,
                Description: "Akun pengelola aplikasi beserta perannya."),
            new AdminMenuItem("Peran", "/Admin/Master/Roles", IconShield, PolicyRoles,
                Description: "Kumpulan izin yang diberikan bersama-sama."),
            //new AdminMenuItem("Izin", "/Admin/Master/Permissions", IconKey, PolicyRoles,
            //    Description: "Satuan hak akses yang dipakai kode program."),
        ]),
    ];

    public static IEnumerable<AdminMenuItem> AllItems => Sections.SelectMany(section => section.Items);

    /// <summary>Butir menu yang cocok dengan halaman yang sedang dibuka, untuk penanda aktif dan judul.</summary>
    public static AdminMenuItem? Find(string? pageName) =>
        pageName is null
            ? null
            : AllItems.FirstOrDefault(item => string.Equals(item.PageName, pageName, StringComparison.OrdinalIgnoreCase));
}
