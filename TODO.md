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

## Pemberitahuan WhatsApp (WAHA)

Pegawai yang mengirim pertanyaan memicu pesan ke pengelola, dan jawaban yang
disimpan memicu pesan balik ke penanya. Pengirimannya lewat
[WAHA](https://waha.devlike.pro): `POST /api/sendText` dengan header `X-Api-Key`.

Dimatikan secara bawaan. Nyalakan lewat environment variable:

| Environment variable | Wajib | Keterangan |
| --- | --- | --- |
| `Notifications__Enabled` | ya, untuk mengaktifkan | `true` untuk menyalakan. Bawaan `false`. |
| `Notifications__Waha__BaseUrl` | ya bila aktif | Alamat peladen WAHA, mis. `http://waha:3000`. |
| `Notifications__Waha__ApiKey` | ya bila WAHA memakai kunci | Nilai header `X-Api-Key`. |
| `Notifications__Waha__Session` | tidak (default `default`) | Nama sesi WhatsApp di WAHA. |
| `Notifications__Waha__TimeoutSeconds` | tidak (default `20`) | Dijepit ke rentang 5-120 detik. |
| `Notifications__DefaultCountryCode` | tidak (default `62`) | Untuk nomor bergaya lokal `08xx`. Nomor berawalan `+` dibiarkan apa adanya. |
| `Notifications__AdminRecipients__0` | tidak | Nomor tambahan, mis. nomor piket. Digabung dengan nomor pengelola, bukan menggantikan. |
| `Notifications__PortalUrl` | tidak | Tautan yang disisipkan di akhir pesan. |

Penerima pemberitahuan pertanyaan baru dihitung dari data, bukan dari daftar
tetap: akun admin aktif yang punya izin `questions.answer` lewat peran aktif
(atau berperan `SUPERADMIN`), yang tertaut ke pegawai aktif **dan nomor telepon
pegawainya terisi**. Nomor penanya diambil dari `tb_m_employee.phone_number`.

Pengiriman berjalan di antrean latar belakang. WAHA yang lambat atau mati tidak
menahan request dan tidak membatalkan pertanyaan atau jawaban yang sudah
tersimpan; kegagalannya tercatat di log.

### Menyiapkan WAHA di mesin pengembang

```
docker compose -f docker-compose.waha.yml up -d
python tools/qr_server.py
```

Buka `http://localhost:8808`, lalu pindai kodenya dari WhatsApp
(**Perangkat Tertaut** -> **Tautkan Perangkat**). Halamannya menarik QR baru
tiap 3 detik - QR dari WhatsApp sendiri berganti tiap ~20 detik, jadi tanpa itu
kodenya kerap sudah kedaluwarsa saat dipindai. Begitu tertaut, halaman berganti
menjadi panel info berisi nomor dan akun yang dipakai.

Tautannya tersimpan di volume `waha_sessions`, jadi cukup dipindai sekali;
container yang di-restart tidak meminta pemindaian ulang.

Setelah itu arahkan aplikasi ke WAHA tersebut:

```
dotnet user-secrets set "Notifications:Enabled" "true"
dotnet user-secrets set "Notifications:Waha:BaseUrl" "http://127.0.0.1:3000"
dotnet user-secrets set "Notifications:Waha:ApiKey" "waha-dev-local"
```

Kunci bawaan `waha-dev-local` hanya untuk mesin pengembang. Ganti lewat berkas
`.env` (`WAHA_API_KEY=...`) di lingkungan mana pun yang bisa dijangkau orang
lain: siapa saja yang memegang kunci itu bisa mengirim WhatsApp atas nama nomor
yang tertaut.

### Menyiapkan WAHA di server

Satu WAHA, satu nomor resmi, dipindai sekali. Semua aplikasi mengarah ke sana,
sehingga nomor pengirim yang dilihat pegawai selalu sama. Kalau tiap orang
menjalankan WAHA sendiri, pegawai menerima pesan dari nomor pribadi siapa pun
yang kebetulan menjalankan aplikasi.

**1. Salin berkasnya ke server.** Direktori compose di VPS bukan salinan
repositori ini, jadi kedua berkas berikut perlu dikirim ke sana:

```
scp docker-compose.waha.prod.yml .env.waha.example pengguna@server:/docker/project_keu/
```

**2. Siapkan rahasianya** di direktori itu:

```
cd /docker/project_keu
cp .env.waha.example .env
openssl rand -hex 32        # isikan sebagai WAHA_API_KEY
nano .env
```

`.env` tidak boleh ikut ter-commit; `.gitignore` sudah menolaknya.

**3. Hidupkan WAHA** berdampingan dengan aplikasi:

```
docker compose -f docker-compose.yml -f docker-compose.waha.prod.yml up -d
```

`.env.waha.example` memuat baris `COMPOSE_FILE=...` yang membuat perintah
`docker compose` polos ikut membaca kedua berkas. Itu diperlukan karena skrip
deploy di `.github/workflows/docker-image.yml` menjalankan `docker compose pull`
dan `up -d` tanpa flag `-f`; tanpa baris tersebut, WAHA tidak ikut terkelola
oleh deploy otomatis.

Port WAHA sengaja hanya terbuka ke `127.0.0.1`. Aplikasi menghubunginya lewat
jaringan compose, bukan lewat port itu.

**4. Tambahkan environment berikut pada layanan aplikasi** di compose yang sudah
ada di VPS:

```yaml
    environment:
      Notifications__Enabled: "true"
      Notifications__Waha__BaseUrl: "http://waha:3000"
      Notifications__Waha__ApiKey: ${WAHA_API_KEY:?WAHA_API_KEY belum diisi di .env}
      Notifications__Waha__Session: ${WAHA_SESSION:-default}
      Notifications__PortalUrl: ${PORTAL_URL:-}
```

`http://waha:3000` adalah nama layanan di jaringan compose, bukan alamat publik.
Aplikasi tidak perlu menunggu WAHA siap: pemberitahuan berjalan di antrean, jadi
WAHA yang mati tidak menghalangi aplikasi hidup.

**5. Pindai QR sekali** dari komputer Anda, lewat terowongan SSH - tidak ada port
yang perlu dibuka ke internet:

```
ssh -L 3000:127.0.0.1:3000 pengguna@server        # biarkan terbuka
```

Lalu di komputer Anda, pada salinan repositori ini:

```
WAHA_BASE_URL=http://127.0.0.1:3000 WAHA_API_KEY=<isi WAHA_API_KEY>   python tools/qr_server.py
```

Buka `http://localhost:8808` dan pindai dari ponsel bernomor resmi. Begitu
halaman menampilkan panel "Info Tersambung", terowongan SSH boleh ditutup.

Tautannya tersimpan di volume `waha_sessions`, jadi penarikan image baru maupun
restart server tidak meminta pemindaian ulang.

**6. Isi nomor telepon pengelola** lewat menu Master -> Pegawai. Tanpa itu,
pemberitahuan pertanyaan baru tidak punya penerima.

### Memeriksa keadaan WAHA di server

```
docker compose ps waha
docker compose logs -f waha
curl -H "X-Api-Key: $WAHA_API_KEY" http://127.0.0.1:3000/api/sessions
```

Sesi yang sehat berstatus `WORKING`. `SCAN_QR_CODE` berarti tautannya lepas dan
perlu dipindai ulang lewat langkah 5.

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
