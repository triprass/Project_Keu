# TODO - Buat halaman Admin Dashboard (Daftar Pertanyaan)

- [x] Buat page baru `Pages/AdminDashboard.cshtml` sesuai referensi
- [x] Tambahkan tabel daftar pertanyaan + tombol aksi/status
- [x] Tambahkan styling dashboard di `wwwroot/css/site.css`
- [x] Gunakan class reusable `badge-hexagon` untuk badge kiri bawah
- [x] Verifikasi struktur tampilan desktop

# TODO - Buat halaman Admin Dashboard V2 (sesuai screenshot terbaru)

- [x] Update struktur `Pages/AdminDashboardV2.cshtml` (judul + kolom No, Tanggal, Pertanyaan, Status)
- [x] Sesuaikan konten baris data dan status (Telah Dijawab / Belum Dijawab)
- [x] Tambahkan styling khusus V2 di `wwwroot/css/site.css` (warna biru, border, spacing, badge kiri bawah)

# TODO - Tambah footer global di semua halaman

- [x] Tambah markup footer global di `Pages/Shared/_Layout.cshtml`
- [x] Tambah styling footer di `wwwroot/css/site.css`
- [x] Finalisasi TODO setelah implementasi selesai

# TODO - Polish footer mobile agar lebih rapi & modern

- [ ] Refine style footer untuk mobile (`max-width: 768px`)
- [ ] Tingkatkan hierarchy tipografi dan spacing footer mobile
- [ ] Finalisasi TODO polish footer mobile

# TODO - Backend PostgreSQL tb_m_employee

- [ ] Tambah package EF Core PostgreSQL
- [ ] Tambah connection string PostgreSQL di appsettings.json
- [ ] Buat entity Employee + mapping tabel tb_m_employee
- [ ] Buat AppDbContext dan registrasi di Program.cs
- [ ] Buat endpoint API employee (GET list, GET by id)
- [ ] Finalisasi TODO backend PostgreSQL
