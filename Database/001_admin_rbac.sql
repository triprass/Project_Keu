-- ============================================================================
--  Pilar Keuangan - Tabel Akun Administrator, Peran, dan Izin (RBAC)
--
--  Jalankan sekali pada database KEU_JayaPura, mis:
--      psql -h <host> -p <port> -U <user> -d KEU_JayaPura -f 001_admin_rbac.sql
--
--  Skrip ini aman dijalankan ulang: seluruh pembuatan objek memakai
--  IF NOT EXISTS dan seluruh penyisipan data memakai ON CONFLICT DO NOTHING.
--
--  Kolom waktu memakai timestamptz karena aplikasi menulis DateTime.UtcNow.
--
--  Prasyarat: gen_random_uuid() tersedia bawaan sejak PostgreSQL 13. Untuk versi
--  yang lebih lama, jalankan lebih dulu:  CREATE EXTENSION IF NOT EXISTS pgcrypto;
-- ============================================================================

BEGIN;

-- ---------------------------------------------------------------------------
-- 1. Tabel
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS tb_m_admin_user (
    id                  uuid         PRIMARY KEY,
    username            varchar(100) NOT NULL,
    password_hash       varchar(255) NOT NULL,
    full_name           varchar(150) NOT NULL,
    email               varchar(150),
    employee_id         uuid         REFERENCES tb_m_employee (id),
    is_active           boolean      NOT NULL DEFAULT true,
    last_login_at       timestamptz,
    failed_login_count  integer      NOT NULL DEFAULT 0,
    locked_until        timestamptz,
    created_by          varchar(100),
    created_at          timestamptz  NOT NULL DEFAULT now(),
    updated_by          varchar(100),
    updated_at          timestamptz
);

-- Nama pengguna dibandingkan tanpa membedakan huruf besar-kecil oleh aplikasi,
-- jadi keunikannya pun harus case-insensitive.
CREATE UNIQUE INDEX IF NOT EXISTS ux_admin_user_username
    ON tb_m_admin_user (lower(username));

CREATE TABLE IF NOT EXISTS tb_m_role (
    id          uuid         PRIMARY KEY,
    code        varchar(50)  NOT NULL,
    name        varchar(100) NOT NULL,
    description text,
    is_active   boolean      NOT NULL DEFAULT true,
    created_at  timestamptz  NOT NULL DEFAULT now(),
    updated_at  timestamptz
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_role_code ON tb_m_role (code);

CREATE TABLE IF NOT EXISTS tb_m_permission (
    id          uuid         PRIMARY KEY,
    code        varchar(100) NOT NULL,
    name        varchar(150) NOT NULL,
    group_name  varchar(50),
    description text,
    is_active   boolean      NOT NULL DEFAULT true,
    created_at  timestamptz  NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_permission_code ON tb_m_permission (code);

CREATE TABLE IF NOT EXISTS tb_r_admin_user_role (
    admin_user_id uuid        NOT NULL REFERENCES tb_m_admin_user (id) ON DELETE CASCADE,
    role_id       uuid        NOT NULL REFERENCES tb_m_role (id)       ON DELETE CASCADE,
    created_at    timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (admin_user_id, role_id)
);

CREATE TABLE IF NOT EXISTS tb_r_role_permission (
    role_id       uuid        NOT NULL REFERENCES tb_m_role (id)       ON DELETE CASCADE,
    permission_id uuid        NOT NULL REFERENCES tb_m_permission (id) ON DELETE CASCADE,
    created_at    timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (role_id, permission_id)
);

CREATE INDEX IF NOT EXISTS ix_admin_user_role_role       ON tb_r_admin_user_role (role_id);
CREATE INDEX IF NOT EXISTS ix_role_permission_permission ON tb_r_role_permission (permission_id);

-- ---------------------------------------------------------------------------
-- 2. Daftar izin
--    Kode "grup.aksi" inilah yang dipakai pada [Authorize(Policy = "...")].
-- ---------------------------------------------------------------------------

INSERT INTO tb_m_permission (id, code, name, group_name, description) VALUES
    (gen_random_uuid(), 'questions.view',     'Lihat daftar pertanyaan',        'Pertanyaan', 'Membuka halaman daftar pertanyaan.'),
    (gen_random_uuid(), 'questions.export',   'Unduh rekap pertanyaan',         'Pertanyaan', 'Mengunduh daftar pertanyaan ke Excel.'),
    (gen_random_uuid(), 'questions.answer',   'Jawab pertanyaan',               'Pertanyaan', 'Mengisi dan mengubah jawaban.'),
    (gen_random_uuid(), 'questions.delete',   'Hapus pertanyaan',               'Pertanyaan', 'Menghapus pertanyaan.'),
    (gen_random_uuid(), 'categories.manage',  'Kelola kategori pertanyaan',     'Master',     'Menambah, mengubah, menghapus kategori.'),
    (gen_random_uuid(), 'statuses.manage',    'Kelola status pertanyaan',       'Master',     'Menambah, mengubah, menghapus status.'),
    (gen_random_uuid(), 'employees.view',     'Lihat data pegawai',             'Master',     'Membuka data pegawai.'),
    (gen_random_uuid(), 'admin.users.manage', 'Kelola akun administrator',      'Sistem',     'Menambah dan menonaktifkan akun admin.'),
    (gen_random_uuid(), 'admin.roles.manage', 'Kelola peran dan hak akses',     'Sistem',     'Mengatur peran beserta izinnya.')
ON CONFLICT (code) DO NOTHING;

-- ---------------------------------------------------------------------------
-- 3. Peran
-- ---------------------------------------------------------------------------

INSERT INTO tb_m_role (id, code, name, description) VALUES
    (gen_random_uuid(), 'SUPERADMIN', 'Super Administrator', 'Akses penuh. Otomatis memiliki seluruh izin, termasuk izin yang ditambahkan kemudian.'),
    (gen_random_uuid(), 'ADMIN_KEU',  'Admin Keuangan',      'Mengelola dan menjawab pertanyaan serta data master.'),
    (gen_random_uuid(), 'VIEWER',     'Peninjau',            'Hanya melihat dan mengunduh rekap.')
ON CONFLICT (code) DO NOTHING;

-- ---------------------------------------------------------------------------
-- 4. Pemetaan peran -> izin
--    SUPERADMIN tidak perlu dipetakan: aplikasi meloloskannya untuk semua izin.
-- ---------------------------------------------------------------------------

INSERT INTO tb_r_role_permission (role_id, permission_id)
SELECT r.id, p.id
FROM tb_m_role r
JOIN tb_m_permission p ON p.code IN (
    'questions.view', 'questions.export', 'questions.answer',
    'categories.manage', 'statuses.manage', 'employees.view'
)
WHERE r.code = 'ADMIN_KEU'
ON CONFLICT DO NOTHING;

INSERT INTO tb_r_role_permission (role_id, permission_id)
SELECT r.id, p.id
FROM tb_m_role r
JOIN tb_m_permission p ON p.code IN ('questions.view', 'questions.export')
WHERE r.code = 'VIEWER'
ON CONFLICT DO NOTHING;

COMMIT;

-- ============================================================================
--  5. AKUN ADMIN PERTAMA  -  jalankan setelah blok di atas berhasil
--
--  a. Buat hash kata sandi (kata sandi asli tidak pernah masuk ke database):
--
--         dotnet Project_Keu.dll --hash-password "KataSandiAnda"
--
--     Keluarannya berbentuk:
--         PBKDF2-SHA512$210000$<salt-base64>$<hash-base64>
--
--  b. Salin hash tersebut ke perintah di bawah, ganti juga username dan nama,
--     lalu jalankan. Hapus komentarnya terlebih dahulu.
-- ============================================================================

-- BEGIN;
--
-- INSERT INTO tb_m_admin_user (id, username, password_hash, full_name, is_active, created_by)
-- VALUES (
--     gen_random_uuid(),
--     'adminkeu',
--     'TEMPEL_HASH_DI_SINI',
--     'Administrator Pilar Keuangan',
--     true,
--     'setup'
-- )
-- ON CONFLICT DO NOTHING;
--
-- INSERT INTO tb_r_admin_user_role (admin_user_id, role_id)
-- SELECT u.id, r.id
-- FROM tb_m_admin_user u
-- JOIN tb_m_role r ON r.code = 'SUPERADMIN'
-- WHERE lower(u.username) = lower('adminkeu')
-- ON CONFLICT DO NOTHING;
--
-- COMMIT;

-- ============================================================================
--  Catatan pemeliharaan
--
--  Menambah izin baru:
--      INSERT INTO tb_m_permission (id, code, name, group_name)
--      VALUES (gen_random_uuid(), 'reports.view', 'Lihat laporan', 'Laporan');
--    lalu pasang ke peran yang membutuhkan lewat tb_r_role_permission.
--    Aplikasi tidak perlu diubah: [Authorize(Policy = "reports.view")]
--    langsung berlaku begitu izinnya ada dan terpasang pada peran.
--
--  Membuka kunci akun yang terkunci sementara:
--      UPDATE tb_m_admin_user
--      SET locked_until = NULL, failed_login_count = 0
--      WHERE lower(username) = lower('adminkeu');
--
--  Menonaktifkan akun:
--      UPDATE tb_m_admin_user SET is_active = false WHERE lower(username) = lower('adminkeu');
-- ============================================================================
