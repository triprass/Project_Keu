# TODO - Debug Reverse Proxy /Pertanyaan Error

- [x] Read remaining relevant files:
  - [x] Pages/FormPertanyaan.cshtml
  - [x] Pages/FormPertanyaan.cshtml.cs
  - [x] Controllers/StartController.cs
  - [x] Pages/_ViewStart.cshtml
  - [x] Pages/_ViewImports.cshtml
- [x] Patch Program.cs for forwarded headers and middleware order
- [x] Add proxy-related config in appsettings.json
- [x] Update TODO progress after each completed step
- [ ] Produce root-cause analysis with before/after code
- [ ] Provide Traefik + Docker best-practice recommendations

## Konfigurasi wajib saat deploy

Connection string tidak lagi disimpan di `appsettings.json`. Aplikasi berhenti
saat startup dengan pesan yang jelas kalau nilai berikut belum diisi.

| Environment variable | Wajib | Keterangan |
| --- | --- | --- |
| `ConnectionStrings__DefaultConnection` | ya | Koneksi PostgreSQL. |
| `Security__AdminApiKey` | ya di Production | Kunci untuk endpoint API administratif (header `X-Api-Key`). Tanpa ini endpoint tersebut membalas 503. |
| `App__TimeZone` | tidak (default `Asia/Jayapura`) | Zona waktu untuk tampilan dan filter tanggal. |

## Akun administrator (halaman `/Login`)

Kredensial disimpan di database, bukan di konfigurasi.

1. Jalankan `Database/001_admin_rbac.sql` satu kali pada database.
2. Buat hash kata sandi: `dotnet Project_Keu.dll --hash-password "KataSandiAnda"`.
3. Tempel hash tersebut pada blok INSERT di bagian bawah skrip, lalu jalankan.

Selama belum ada akun aktif, halaman login menampilkan pesan penyiapan dan
formulirnya tidak dirender sama sekali.

### Hak akses

| Tabel | Isi |
| --- | --- |
| `tb_m_admin_user` | akun admin, hash kata sandi, status aktif, penguncian |
| `tb_m_role` | peran: `SUPERADMIN`, `ADMIN_KEU`, `VIEWER` |
| `tb_m_permission` | izin bergaya `grup.aksi`, mis. `questions.export` |
| `tb_r_admin_user_role` | akun ↔ peran |
| `tb_r_role_permission` | peran ↔ izin |

Membatasi halaman atau endpoint cukup dengan atribut, tanpa perubahan lain:

```csharp
[Authorize]                                 // wajib login saja
[Authorize(Roles = "SUPERADMIN")]           // berdasarkan peran
[Authorize(Policy = "questions.export")]    // berdasarkan izin
```

Policy untuk sebuah izin dibentuk otomatis dari kodenya, jadi izin baru cukup
ditambahkan di `tb_m_permission` lalu dipasang ke peran. `SUPERADMIN` selalu
lolos seluruh pemeriksaan izin.
| `ReverseProxy__Enabled` | tidak (default `true` di Production) | Bila `true`: `UseForwardedHeaders` aktif dan redirect HTTPS di dalam container dilewati karena TLS diterminasi di Traefik. |
| `ReverseProxy__ForwardLimit` | tidak (default `1`) | Jumlah proxy yang dipercaya di depan aplikasi. |

Untuk pengembangan lokal, connection string disimpan di user-secrets:

```
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Port=...;Database=...;Username=...;Password=..."
```

## Endpoint operasional

- `GET /health` - liveness, tidak menyentuh database.
- `GET /health/ready` - readiness, mengecek koneksi PostgreSQL.
