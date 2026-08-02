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
| `Admin__Username` | ya, untuk halaman `/Login` | Nama pengguna administrator. |
| `Admin__PasswordHash` | ya, untuk halaman `/Login` | Hash kata sandi, dibuat dengan `dotnet Project_Keu.dll --hash-password "<kata-sandi>"`. Kata sandi asli tidak pernah disimpan. Tanpa nilai ini halaman login menampilkan pesan "belum disiapkan" dan tidak bisa dipakai masuk. |
| `App__TimeZone` | tidak (default `Asia/Jayapura`) | Zona waktu untuk tampilan dan filter tanggal. |
| `ReverseProxy__Enabled` | tidak (default `true` di Production) | Bila `true`: `UseForwardedHeaders` aktif dan redirect HTTPS di dalam container dilewati karena TLS diterminasi di Traefik. |
| `ReverseProxy__ForwardLimit` | tidak (default `1`) | Jumlah proxy yang dipercaya di depan aplikasi. |

Untuk pengembangan lokal, connection string disimpan di user-secrets:

```
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Port=...;Database=...;Username=...;Password=..."
```

## Endpoint operasional

- `GET /health` - liveness, tidak menyentuh database.
- `GET /health/ready` - readiness, mengecek koneksi PostgreSQL.
