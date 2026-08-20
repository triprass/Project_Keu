#!/usr/bin/env bash
#
# Menyiapkan WAHA di server. Dijalankan DI SERVER, di direktori compose aplikasi
# (biasanya /docker/project_keu).
#
#   scp docker-compose.waha.prod.yml tools/setup-waha-server.sh pengguna@server:/docker/project_keu/
#   ssh pengguna@server
#   cd /docker/project_keu && bash setup-waha-server.sh
#
# Aman diulang: nilai yang sudah ada di .env tidak pernah ditimpa, dan WAHA yang
# sudah tertaut tidak diminta memindai ulang.

set -euo pipefail

COMPOSE_WAHA="docker-compose.waha.prod.yml"
COMPOSE_APP="docker-compose.yml"
ENV_FILE=".env"

say()  { printf '\n\033[1m==> %s\033[0m\n' "$*"; }
warn() { printf '\033[33m    %s\033[0m\n' "$*"; }
die()  { printf '\n\033[31mGAGAL: %s\033[0m\n\n' "$*" >&2; exit 1; }

# ---------------------------------------------------------------- prasyarat

say "Memeriksa prasyarat"

command -v docker >/dev/null 2>&1 || die "docker tidak ditemukan."
docker compose version >/dev/null 2>&1 || die "'docker compose' tidak tersedia (butuh Compose v2)."
docker info >/dev/null 2>&1 || die "daemon docker tidak berjalan, atau pengguna ini tidak berhak memakainya."

[ -f "$COMPOSE_WAHA" ] || die "$COMPOSE_WAHA tidak ada di direktori ini.
    Kirim dulu dari komputer Anda:
      scp $COMPOSE_WAHA pengguna@server:$(pwd)/"

echo "    docker      : $(docker --version | cut -d' ' -f3 | tr -d ,)"
echo "    compose     : $(docker compose version --short 2>/dev/null || echo '?')"
echo "    direktori   : $(pwd)"

if [ -f "$COMPOSE_APP" ]; then
    echo "    aplikasi    : $COMPOSE_APP ditemukan"
    COMPOSE_CHAIN="$COMPOSE_APP:$COMPOSE_WAHA"
else
    warn "$COMPOSE_APP tidak ada di sini. WAHA tetap bisa dijalankan, tetapi"
    warn "aplikasi TIDAK akan bisa memanggilnya lewat nama 'waha' karena berbeda"
    warn "jaringan compose. Pastikan Anda berada di direktori compose aplikasi."
    COMPOSE_CHAIN="$COMPOSE_WAHA"
fi

# ------------------------------------------------------------------- .env

say "Menyiapkan $ENV_FILE"

[ -f "$ENV_FILE" ] || { : > "$ENV_FILE"; echo "    dibuat baru"; }

# Menambahkan kunci hanya bila belum ada. Nilai yang sudah diisi orang tidak
# boleh tersentuh - menimpanya berarti memutus tautan WhatsApp yang sedang hidup.
ensure_var() {
    local key="$1" value="$2" note="$3"

    if grep -qE "^[[:space:]]*${key}=" "$ENV_FILE"; then
        local current
        current="$(grep -E "^[[:space:]]*${key}=" "$ENV_FILE" | head -1 | cut -d= -f2-)"

        if [ -n "$current" ]; then
            echo "    $key sudah ada, dibiarkan"
            return
        fi

        # Ada barisnya tetapi kosong; isi di tempat.
        local tmp
        tmp="$(mktemp)"
        sed "s|^[[:space:]]*${key}=.*|${key}=${value}|" "$ENV_FILE" > "$tmp"
        mv "$tmp" "$ENV_FILE"
        echo "    $key diisi ($note)"
        return
    fi

    printf '%s=%s\n' "$key" "$value" >> "$ENV_FILE"
    echo "    $key ditambahkan ($note)"
}

random_secret() {
    if command -v openssl >/dev/null 2>&1; then
        openssl rand -hex 32
    else
        head -c 32 /dev/urandom | od -An -tx1 | tr -d ' \n'
    fi
}

ensure_var COMPOSE_FILE "$COMPOSE_CHAIN" "agar deploy otomatis ikut mengelola WAHA"
ensure_var WAHA_API_KEY "$(random_secret)" "dibuat acak"
ensure_var WAHA_DASHBOARD_USER "admin" "bawaan"
ensure_var WAHA_DASHBOARD_PASSWORD "$(random_secret)" "dibuat acak"
ensure_var WAHA_SESSION "default" "bawaan"
ensure_var WAHA_PORT "3000" "bawaan"
ensure_var TZ "Asia/Jayapura" "bawaan"

chmod 600 "$ENV_FILE"

# shellcheck disable=SC1090
set -a; . "./$ENV_FILE"; set +a

[ -n "${WAHA_API_KEY:-}" ] || die "WAHA_API_KEY kosong di $ENV_FILE."

# ------------------------------------------------------------------ jalankan

say "Menghidupkan WAHA"

compose_args=()
[ -f "$COMPOSE_APP" ] && compose_args+=(-f "$COMPOSE_APP")
compose_args+=(-f "$COMPOSE_WAHA")

docker compose "${compose_args[@]}" up -d waha

say "Menunggu sesi siap"

status=""
for attempt in $(seq 1 30); do
    status="$(curl -fsS -H "X-Api-Key: ${WAHA_API_KEY}" \
        "http://127.0.0.1:${WAHA_PORT}/api/sessions/${WAHA_SESSION}" 2>/dev/null \
        | sed -n 's/.*"status"[[:space:]]*:[[:space:]]*"\([A-Z_]*\)".*/\1/p' || true)"

    case "$status" in
        WORKING|SCAN_QR_CODE|FAILED) break ;;
    esac

    printf '    percobaan %2d: %s\n' "$attempt" "${status:-belum menjawab}"
    sleep 5
done

# ---------------------------------------------------------------- kesimpulan

echo
case "$status" in
    WORKING)
        say "Selesai - WhatsApp sudah tertaut"
        echo "    Tidak perlu memindai apa pun. Sesi lama dipulihkan dari volume."
        ;;
    SCAN_QR_CODE)
        say "WAHA siap, tinggal dipindai"

        # Alamat server ditebak dari antarmuka jaringan. Kalau tidak ketemu,
        # lebih baik menampilkan penanda yang jelas daripada "pengguna@" yang
        # terlihat seperti perintah utuh padahal tidak bisa dijalankan.
        server_user="$(whoami 2>/dev/null || echo pengguna)"
        server_host="$(hostname -I 2>/dev/null | awk '{print $1}')"
        [ -n "$server_host" ] || server_host="<alamat-server>"

        cat <<INSTRUKSI

    Dari komputer yang ada ponsel bernomor resmi di dekatnya, jalankan:

      ssh -L 3000:127.0.0.1:${WAHA_PORT} ${server_user}@${server_host}

    lalu di terminal lain, pada salinan repositori:

      WAHA_BASE_URL=http://127.0.0.1:3000 \\
      WAHA_API_KEY=${WAHA_API_KEY} \\
        python tools/qr_server.py

    Buka http://localhost:8808 dan pindai dari WhatsApp:
    Perangkat Tertaut -> Tautkan Perangkat

INSTRUKSI
        warn "Kunci API di atas adalah rahasia. Jangan kirim lewat kanal terbuka."
        ;;
    FAILED)
        die "sesi gagal dimulai. Periksa: docker compose logs --tail=50 waha"
        ;;
    *)
        die "WAHA tidak menjawab setelah ~2,5 menit.
    Periksa: docker compose logs --tail=50 waha"
        ;;
esac

say "Langkah berikutnya"
cat <<LANJUT

    1. Tambahkan pada layanan aplikasi di $COMPOSE_APP:

         Notifications__Enabled: "true"
         Notifications__Waha__BaseUrl: "http://waha:3000"
         Notifications__Waha__ApiKey: \${WAHA_API_KEY:?WAHA_API_KEY belum diisi di .env}

       lalu: docker compose up -d

    2. Isi nomor telepon pengelola lewat menu Master -> Pegawai.
       Tanpa itu, pemberitahuan pertanyaan baru tidak punya penerima.

LANJUT
