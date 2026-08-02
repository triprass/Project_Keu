using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Infrastructure.Admin;
using Project_Keu.Models;

namespace Project_Keu.Pages.Admin.Master;

[Authorize(Policy = AdminMenu.PolicyEmployees)]
public class EmployeesModel : AdminPageModelBase
{
    private readonly AppDbContext _context;

    public EmployeesModel(AppDbContext context)
    {
        _context = context;
    }

    public sealed record Row(
        Guid Id,
        string EmployeeNo,
        string? Nip,
        string FullName,
        string? NickName,
        string? Email,
        string? PhoneNumber,
        string? Gender,
        DateOnly? BirthDate,
        string? Company,
        string? Branch,
        string? Location,
        DateOnly? HireDate,
        DateOnly? ResignDate,
        string? EmploymentStatus,
        bool IsActive,
        int QuestionCount);

    public IReadOnlyList<Row> Items { get; private set; } = [];

    /// <summary>Saringan status: kosong = semua, "1" = aktif, "0" = nonaktif.</summary>
    [BindProperty(SupportsGet = true, Name = "s")]
    public string? StatusFilter { get; set; }

    [BindProperty]
    public EmployeeInput Input { get; set; } = new();

    public override Dictionary<string, object?> ListState() =>
        new() { ["q"] = Search, ["s"] = StatusFilter };

    public sealed class EmployeeInput
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Nomor pegawai wajib diisi.")]
        [StringLength(20, ErrorMessage = "Nomor pegawai maksimal 20 karakter.")]
        public string EmployeeNo { get; set; } = string.Empty;

        [StringLength(30, ErrorMessage = "NIP maksimal 30 karakter.")]
        public string? Nip { get; set; }

        [Required(ErrorMessage = "Nama lengkap wajib diisi.")]
        [StringLength(150, ErrorMessage = "Nama lengkap maksimal 150 karakter.")]
        public string FullName { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Nama panggilan maksimal 100 karakter.")]
        public string? NickName { get; set; }

        [EmailAddress(ErrorMessage = "Format surel tidak sesuai.")]
        [StringLength(150, ErrorMessage = "Surel maksimal 150 karakter.")]
        public string? Email { get; set; }

        [StringLength(30, ErrorMessage = "Nomor telepon maksimal 30 karakter.")]
        public string? PhoneNumber { get; set; }

        public string? Gender { get; set; }

        public DateOnly? BirthDate { get; set; }

        [StringLength(100)] public string? Company { get; set; }
        [StringLength(100)] public string? Branch { get; set; }
        [StringLength(100)] public string? Location { get; set; }

        public DateOnly? HireDate { get; set; }
        public DateOnly? ResignDate { get; set; }

        [StringLength(30, ErrorMessage = "Status kepegawaian maksimal 30 karakter.")]
        public string? EmploymentStatus { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public static IReadOnlyList<SelectListItem> GenderOptions { get; } =
    [
        new("—", ""),
        new("Laki-laki", "L"),
        new("Perempuan", "P")
    ];

    public static IReadOnlyList<string> EmploymentStatusSuggestions { get; } =
        ["PNS", "PPPK", "Kontrak", "Honorer", "Magang"];

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        Input.EmployeeNo = Input.EmployeeNo?.Trim() ?? string.Empty;
        Input.FullName = Input.FullName?.Trim() ?? string.Empty;
        Input.Nip = Blank(Input.Nip);
        Input.NickName = Blank(Input.NickName);
        Input.Email = Blank(Input.Email);
        Input.PhoneNumber = Blank(Input.PhoneNumber);
        Input.Company = Blank(Input.Company);
        Input.Branch = Blank(Input.Branch);
        Input.Location = Blank(Input.Location);
        Input.EmploymentStatus = Blank(Input.EmploymentStatus);
        Input.Gender = Input.Gender is "L" or "P" ? Input.Gender : null;

        var currentId = Input.Id ?? Guid.Empty;

        var duplicateNo = await _context.Employees
            .AsNoTracking()
            .AnyAsync(x => x.DeletedAt == null
                        && x.Id != currentId
                        && x.EmployeeNo.ToLower() == Input.EmployeeNo.ToLower(), cancellationToken);

        if (duplicateNo)
        {
            ModelState.AddModelError("Input.EmployeeNo", "Nomor pegawai ini sudah terdaftar.");
        }

        if (Input.Nip is not null)
        {
            // NIP dipakai halaman publik untuk mengenali pengaju pertanyaan, jadi
            // tidak boleh dimiliki dua pegawai sekaligus.
            var duplicateNip = await _context.Employees
                .AsNoTracking()
                .AnyAsync(x => x.DeletedAt == null
                            && x.Id != currentId
                            && x.Nip != null
                            && x.Nip.ToLower() == Input.Nip.ToLower(), cancellationToken);

            if (duplicateNip)
            {
                ModelState.AddModelError("Input.Nip", "NIP ini sudah dipakai pegawai lain.");
            }
        }

        if (Input.ResignDate is not null && Input.HireDate is not null && Input.ResignDate < Input.HireDate)
        {
            ModelState.AddModelError("Input.ResignDate", "Tanggal berhenti tidak boleh mendahului tanggal masuk.");
        }

        if (!ModelState.IsValid)
        {
            ReopenDialog = true;
            await LoadAsync(cancellationToken);
            return Page();
        }

        var now = DateTime.UtcNow;

        if (Input.Id is null || Input.Id == Guid.Empty)
        {
            var entity = new Employee
            {
                Id = Guid.NewGuid(),
                CreatedBy = CurrentUserName,
                CreatedAt = now
            };

            Apply(entity);
            _context.Employees.Add(entity);

            await _context.SaveChangesAsync(cancellationToken);
            Notify($"Pegawai \"{Input.FullName}\" berhasil ditambahkan.");
        }
        else
        {
            var entity = await _context.Employees
                .FirstOrDefaultAsync(x => x.Id == Input.Id && x.DeletedAt == null, cancellationToken);

            if (entity is null)
            {
                NotifyError("Data pegawai tidak ditemukan atau sudah dihapus.");
                return RedirectToList();
            }

            Apply(entity);
            entity.UpdatedBy = CurrentUserName;
            entity.UpdatedAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            Notify($"Data pegawai \"{Input.FullName}\" berhasil diperbarui.");
        }

        return RedirectToList();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _context.Employees
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);

        if (entity is null)
        {
            NotifyError("Data pegawai tidak ditemukan atau sudah dihapus.");
            return RedirectToList();
        }

        // Pertanyaan dan jawaban merujuk pegawai lewat kunci asing, jadi barisnya
        // dipertahankan dan hanya ditandai terhapus. Riwayat lama tetap terbaca.
        entity.DeletedAt = DateTime.UtcNow;
        entity.DeletedBy = CurrentUserName;
        entity.IsActive = false;

        await _context.SaveChangesAsync(cancellationToken);
        Notify($"Pegawai \"{entity.FullName}\" berhasil dihapus.");

        return RedirectToList();
    }

    private void Apply(Employee entity)
    {
        entity.EmployeeNo = Input.EmployeeNo;
        entity.Nip = Input.Nip;
        entity.FullName = Input.FullName;
        entity.NickName = Input.NickName;
        entity.Email = Input.Email;
        entity.PhoneNumber = Input.PhoneNumber;
        entity.Gender = Input.Gender;
        entity.BirthDate = Input.BirthDate;
        entity.Company = Input.Company;
        entity.Branch = Input.Branch;
        entity.Location = Input.Location;
        entity.HireDate = Input.HireDate;
        entity.ResignDate = Input.ResignDate;
        entity.EmploymentStatus = Input.EmploymentStatus;
        entity.IsActive = Input.IsActive;
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var query = _context.Employees.AsNoTracking().Where(x => x.DeletedAt == null);

        TotalUnfiltered = await query.CountAsync(cancellationToken);

        if (StatusFilter == "1")
        {
            query = query.Where(x => x.IsActive);
        }
        else if (StatusFilter == "0")
        {
            query = query.Where(x => !x.IsActive);
        }
        else
        {
            StatusFilter = null;
        }

        var keyword = NormalizedSearch();

        if (keyword is not null)
        {
            var pattern = ContainsPattern(keyword);

            query = query.Where(x =>
                EF.Functions.ILike(x.EmployeeNo, pattern, LikeEscape) ||
                EF.Functions.ILike(x.FullName, pattern, LikeEscape) ||
                (x.Nip != null && EF.Functions.ILike(x.Nip, pattern, LikeEscape)) ||
                (x.Email != null && EF.Functions.ILike(x.Email, pattern, LikeEscape)) ||
                (x.Location != null && EF.Functions.ILike(x.Location, pattern, LikeEscape)));
        }

        TotalItems = await query.CountAsync(cancellationToken);
        ClampPage();

        var page = await query
            .OrderBy(x => x.FullName)
            .ThenBy(x => x.Id)
            .Skip(RowOffset)
            .Take(PageSize)
            .Select(x => new
            {
                x.Id,
                x.EmployeeNo,
                x.Nip,
                x.FullName,
                x.NickName,
                x.Email,
                x.PhoneNumber,
                x.Gender,
                x.BirthDate,
                x.Company,
                x.Branch,
                x.Location,
                x.HireDate,
                x.ResignDate,
                x.EmploymentStatus,
                x.IsActive
            })
            .ToListAsync(cancellationToken);

        // Jumlah pertanyaan diambil sekali untuk seluruh baris di halaman ini, bukan
        // satu kueri per baris.
        var ids = page.Select(x => x.Id).ToList();

        var questionCounts = await _context.Questions
            .AsNoTracking()
            .Where(q => ids.Contains(q.CreatedByEmployee))
            .GroupBy(q => q.CreatedByEmployee)
            .Select(g => new { EmployeeId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.EmployeeId, x => x.Total, cancellationToken);

        Items = page
            .Select(x => new Row(
                x.Id, x.EmployeeNo, x.Nip, x.FullName, x.NickName, x.Email, x.PhoneNumber,
                x.Gender, x.BirthDate, x.Company, x.Branch, x.Location, x.HireDate,
                x.ResignDate, x.EmploymentStatus, x.IsActive,
                questionCounts.TryGetValue(x.Id, out var total) ? total : 0))
            .ToList();
    }
}
