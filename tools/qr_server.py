"""
Halaman QR WAHA yang menyegarkan dirinya sendiri, lalu berganti menjadi
panel info begitu WhatsApp tertaut.

Endpoint QR di WAHA butuh header X-Api-Key, sehingga <img src="..."> tidak bisa
menembaknya langsung dari peramban. Skrip ini menyajikan halamannya sekaligus
meneruskan permintaan QR ke WAHA dengan kunci yang benar, jadi kunci itu tidak
pernah sampai ke sisi peramban.

Alat bantu penyiapan, bukan bagian aplikasi. Hanya butuh Python bawaan.

    docker compose -f docker-compose.waha.yml up -d
    python tools/qr_server.py
    # buka http://localhost:8808

Nilai bawaannya mengikuti docker-compose.waha.yml. Kalau WAHA dijalankan dengan
kunci lain, timpa lewat environment variable atau argumen baris perintah:

    WAHA_API_KEY=... python tools/qr_server.py
    python tools/qr_server.py 8808 http://127.0.0.1:3000 kunci-anda default
"""

import json
import os
import sys
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer


def setting(position, env_name, fallback):
    """Argumen baris perintah menang atas environment variable, lalu nilai bawaan."""
    if len(sys.argv) > position:
        return sys.argv[position]

    return os.environ.get(env_name) or fallback


PORT = int(setting(1, "WAHA_QR_PORT", "8808"))
WAHA = setting(2, "WAHA_BASE_URL", "http://127.0.0.1:3000").rstrip("/")
API_KEY = setting(3, "WAHA_API_KEY", "waha-dev-local")
SESSION = setting(4, "WAHA_SESSION", "default")

PAGE = """<!doctype html>
<html lang="id">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Tautkan WhatsApp</title>
<style>
  :root {
    --bg: #f2f5f9; --card: #ffffff; --ink: #10243d; --muted: #5c7189;
    --line: #dde5ef; --soft: #f6f9fc;
    --ok: #1b7d46; --ok-bg: #ebfff2; --ok-line: #54b173;
    --warn: #b25f16; --warn-bg: #fff4e8; --warn-line: #f0a868;
    --err: #a32020; --err-bg: #fdeded; --err-line: #e08585;
  }
  @media (prefers-color-scheme: dark) {
    :root {
      --bg: #0e1620; --card: #16212e; --ink: #e6eef7; --muted: #9bb0c6;
      --line: #26364a; --soft: #1b2836;
      --ok: #6ddc9b; --ok-bg: #10301f; --ok-line: #2f7a4f;
      --warn: #f0b276; --warn-bg: #34240f; --warn-line: #8a5f28;
      --err: #f19a9a; --err-bg: #351717; --err-line: #8c3b3b;
    }
  }
  * { box-sizing: border-box; }
  body {
    margin: 0; min-height: 100vh; background: var(--bg); color: var(--ink);
    font-family: "Segoe UI", system-ui, -apple-system, sans-serif;
    display: flex; align-items: center; justify-content: center; padding: 24px;
  }
  .card {
    background: var(--card); border: 1px solid var(--line); border-radius: 14px;
    padding: 28px 30px 24px; width: 100%; max-width: 430px; text-align: center;
    box-shadow: 0 10px 30px rgba(16, 36, 61, .08);
  }
  h1 { margin: 0 0 4px; font-size: 1.18rem; letter-spacing: -.01em; }
  .sub { margin: 0 0 20px; font-size: .82rem; color: var(--muted); line-height: 1.5; }

  .frame {
    position: relative; width: 288px; height: 288px; margin: 0 auto 18px;
    border: 1px solid var(--line); border-radius: 10px; background: #fff;
    display: flex; align-items: center; justify-content: center; overflow: hidden;
  }
  .frame img { width: 264px; height: 264px; image-rendering: pixelated; display: block; }
  .frame .placeholder { font-size: .8rem; color: #7c8ea3; padding: 0 24px; line-height: 1.5; }

  .badge {
    display: inline-flex; align-items: center; gap: 7px; padding: 6px 14px;
    border-radius: 999px; font-size: .78rem; font-weight: 700;
    border: 1px solid var(--warn-line); background: var(--warn-bg); color: var(--warn);
  }
  .badge.ok  { border-color: var(--ok-line);  background: var(--ok-bg);  color: var(--ok); }
  .badge.err { border-color: var(--err-line); background: var(--err-bg); color: var(--err); }
  .dot { width: 7px; height: 7px; border-radius: 50%; background: currentColor; flex: none; }
  .badge.live .dot { animation: pulse 1.4s ease-in-out infinite; }
  @keyframes pulse { 0%, 100% { opacity: 1; } 50% { opacity: .25; } }

  ol { text-align: left; margin: 18px 0 0; padding-left: 20px; font-size: .82rem;
       color: var(--muted); line-height: 1.75; }

  /* ---------------------------------------------------------- info tersambung */
  .linked-mark {
    width: 62px; height: 62px; margin: 4px auto 14px; border-radius: 50%;
    background: var(--ok-bg); border: 1px solid var(--ok-line); color: var(--ok);
    display: flex; align-items: center; justify-content: center;
  }
  .info {
    text-align: left; margin: 18px 0 0; border: 1px solid var(--line);
    border-radius: 10px; overflow: hidden; background: var(--soft);
  }
  .info-head {
    padding: 9px 14px; font-size: .72rem; font-weight: 700; letter-spacing: .04em;
    text-transform: uppercase; color: var(--muted);
    background: var(--card); border-bottom: 1px solid var(--line);
  }
  .info dl { margin: 0; padding: 4px 14px 10px; }
  .info .row {
    display: flex; justify-content: space-between; align-items: baseline;
    gap: 14px; padding: 7px 0; border-bottom: 1px solid var(--line);
  }
  .info .row:last-child { border-bottom: 0; }
  .info dt { font-size: .78rem; color: var(--muted); flex: none; }
  .info dd {
    margin: 0; font-size: .82rem; font-weight: 600; text-align: right;
    word-break: break-word;
  }
  .info dd.mono { font-family: ui-monospace, "Cascadia Mono", Consolas, monospace; font-weight: 700; }

  .foot { margin-top: 18px; padding-top: 14px; border-top: 1px solid var(--line);
          font-size: .72rem; color: var(--muted); }
  code { background: rgba(127,127,127,.14); padding: 1px 5px; border-radius: 4px; font-size: .95em; }
  @media (prefers-reduced-motion: reduce) { .badge.live .dot { animation: none; } }
</style>
</head>
<body>
  <div class="card">
    <h1 id="title">Tautkan WhatsApp</h1>
    <p class="sub" id="subtitle">Kode di bawah menyegar sendiri tiap 3 detik, jadi yang tampil selalu yang terbaru.</p>

    <div id="stage">
      <div class="frame" id="frame">
        <span class="placeholder" id="placeholder">Menunggu kode dari WAHA...</span>
      </div>
    </div>

    <span class="badge live" id="badge"><span class="dot"></span><span id="badgeText">Menghubungi WAHA</span></span>

    <div id="detail">
      <ol id="steps">
        <li>Buka WhatsApp di ponsel yang nomornya dipakai mengirim.</li>
        <li>Masuk ke <strong>Perangkat Tertaut</strong>.</li>
        <li>Ketuk <strong>Tautkan Perangkat</strong>, lalu arahkan ke kode di atas.</li>
      </ol>
    </div>

    <div class="foot">Sesi <code id="sessionName">-</code> pada <code id="wahaUrl">-</code></div>
  </div>

<script>
(function () {
  var QR_POLL_MS = 3000;
  // Setelah tertaut tidak ada lagi yang mendesak; cukup dipantau agar putusnya
  // sambungan tetap terlihat tanpa perlu memuat ulang halaman.
  var LINKED_POLL_MS = 10000;

  var stage = document.getElementById('stage');
  var frame = document.getElementById('frame');
  var placeholder = document.getElementById('placeholder');
  var badge = document.getElementById('badge');
  var badgeText = document.getElementById('badgeText');
  var detail = document.getElementById('detail');
  var title = document.getElementById('title');
  var subtitle = document.getElementById('subtitle');

  var img = null;
  var linkedRendered = false;

  fetch('/meta').then(function (r) { return r.json(); }).then(function (m) {
    document.getElementById('sessionName').textContent = m.session;
    document.getElementById('wahaUrl').textContent = m.waha;
  }).catch(function () {});

  function escapeHtml(value) {
    return String(value == null ? '' : value)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  function setBadge(text, kind, live) {
    badgeText.textContent = text;
    badge.className = 'badge' + (kind ? ' ' + kind : '') + (live ? ' live' : '');
  }

  // Gambar baru dimuat di belakang layar dan baru dipasang setelah selesai.
  // Kalau <img src> langsung ditimpa, kotaknya berkedip kosong tiap 3 detik.
  function swapQr() {
    var next = new Image();
    next.width = 264; next.height = 264;
    next.alt = 'Kode QR WhatsApp';
    next.onload = function () {
      if (linkedRendered) return;
      if (img) { frame.replaceChild(next, img); }
      else { if (placeholder) placeholder.remove(); frame.appendChild(next); }
      img = next;
    };
    next.src = '/qr.png?t=' + Date.now();
  }

  function phoneFromId(id) {
    if (!id) return null;
    var digits = String(id).split('@')[0].replace(/\\D/g, '');
    return digits ? '+' + digits : null;
  }

  function formatMoment(value) {
    if (!value) return null;
    var ms = typeof value === 'number' ? value : Date.parse(value);
    if (!ms || isNaN(ms)) return null;
    if (ms < 1e12) ms = ms * 1000;   // sebagian versi mengirim detik, bukan milidetik
    try {
      return new Date(ms).toLocaleString('id-ID',
        { dateStyle: 'long', timeStyle: 'short' });
    } catch (e) { return new Date(ms).toLocaleString(); }
  }

  function row(label, value, mono) {
    if (!value) return '';
    return '<div class="row"><dt>' + escapeHtml(label) + '</dt>' +
           '<dd' + (mono ? ' class="mono"' : '') + '>' + escapeHtml(value) + '</dd></div>';
  }

  function renderLinked(s) {
    var me = s.me || {};
    var rows =
      row('Nama akun', me.pushName) +
      row('Nomor', phoneFromId(me.id), true) +
      row('Sesi', s.session, true) +
      row('Status', s.status, true) +
      row('Mesin', s.engine) +
      row('Versi WAHA', s.version) +
      row('Tersambung', formatMoment(s.loginAt));

    title.textContent = 'WhatsApp Tersambung';
    subtitle.textContent = 'Pemberitahuan akan dikirim dari nomor ini.';

    stage.innerHTML =
      '<div class="linked-mark">' +
      '<svg viewBox="0 0 24 24" width="30" height="30" fill="none" stroke="currentColor" ' +
      'stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
      '<polyline points="20 6 9 17 4 12" /></svg></div>';

    detail.innerHTML =
      '<section class="info"><div class="info-head">Info Tersambung</div><dl>' +
      (rows || row('Status', s.status, true)) +
      '</dl></section>';

    setBadge('Siap mengirim pesan', 'ok', false);
    linkedRendered = true;
  }

  function tick() {
    fetch('/status?t=' + Date.now())
      .then(function (r) { return r.json(); })
      .then(function (s) {
        if (s.status === 'WORKING') {
          // Dirender ulang tiap putaran supaya isinya tetap mutakhir bila nama
          // atau nomor akun berubah.
          renderLinked(s);
          setTimeout(tick, LINKED_POLL_MS);
          return;
        }

        if (linkedRendered) {
          // Sempat tersambung lalu lepas: kembalikan halaman ke mode pemindaian.
          window.location.reload();
          return;
        }

        if (s.status === 'SCAN_QR_CODE') {
          setBadge('Menunggu dipindai', '', true);
          swapQr();
        } else if (s.status === 'STARTING') {
          setBadge('WAHA sedang menyiapkan sesi', '', true);
        } else if (s.status === 'FAILED') {
          setBadge('Sesi gagal - mulai ulang container', 'err', false);
        } else if (s.error) {
          setBadge('WAHA tidak terjangkau', 'err', false);
        } else {
          setBadge(String(s.status || 'Tidak diketahui'), '', true);
        }

        setTimeout(tick, QR_POLL_MS);
      })
      .catch(function () {
        setBadge('WAHA tidak terjangkau', 'err', false);
        setTimeout(tick, QR_POLL_MS);
      });
  }

  tick();
})();
</script>
</body>
</html>
"""

_version_cache = {}


def waha_get(path):
    request = urllib.request.Request(WAHA + path, headers={"X-Api-Key": API_KEY})
    with urllib.request.urlopen(request, timeout=15) as response:
        return response.status, response.read(), response.headers.get("Content-Type", "")


def waha_version():
    """Versi WAHA jarang berubah, jadi cukup diambil sekali lalu disimpan."""
    if "value" not in _version_cache:
        try:
            _, raw, _ = waha_get("/api/version")
            _version_cache["value"] = json.loads(raw).get("version")
        except Exception:
            return None

    return _version_cache["value"]


def pick_login_moment(timestamps):
    """Nama kunci waktu masuk berbeda antar versi WAHA; ambil yang pertama ada."""
    if not isinstance(timestamps, dict):
        return None

    for key in ("login", "loggedIn", "connected", "started"):
        if timestamps.get(key):
            return timestamps[key]

    return None


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def _send(self, status, body, content_type):
        if isinstance(body, str):
            body = body.encode("utf-8")

        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        # Tanpa ini peramban menyajikan QR lama dari cache dan penyegaran jadi sia-sia.
        self.send_header("Cache-Control", "no-store, max-age=0")
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        route = self.path.split("?", 1)[0]

        if route == "/":
            self._send(200, PAGE, "text/html; charset=utf-8")
            return

        if route == "/meta":
            self._send(200, json.dumps({"session": SESSION, "waha": WAHA}), "application/json")
            return

        if route == "/status":
            try:
                _, raw, _ = waha_get("/api/sessions/" + SESSION)
                data = json.loads(raw)

                engine = data.get("engine")
                if isinstance(engine, dict):
                    engine = engine.get("engine")

                payload = {
                    "session": data.get("name") or SESSION,
                    "status": data.get("status"),
                    "me": data.get("me"),
                    "engine": engine,
                    "loginAt": pick_login_moment(data.get("timestamps")),
                }

                # Versi hanya menarik saat sudah tersambung, jadi tidak diambil
                # berulang kali selama halaman masih menunggu pemindaian.
                if payload["status"] == "WORKING":
                    payload["version"] = waha_version()
            except Exception as error:
                payload = {"status": None, "error": str(error)}

            self._send(200, json.dumps(payload), "application/json")
            return

        if route == "/qr.png":
            try:
                status, raw, content_type = waha_get(
                    "/api/" + SESSION + "/auth/qr?format=image")
                self._send(status, raw, content_type or "image/png")
            except urllib.error.HTTPError as error:
                # Lazim terjadi: sesi sudah tertaut sehingga QR tidak ada lagi.
                self._send(error.code, b"", "image/png")
            except Exception:
                self._send(502, b"", "image/png")
            return

        self._send(404, "tidak ditemukan", "text/plain; charset=utf-8")

    def log_message(self, *args):
        pass


if __name__ == "__main__":
    print("Halaman QR: http://localhost:%d  (WAHA: %s, sesi: %s)" % (PORT, WAHA, SESSION))
    ThreadingHTTPServer(("127.0.0.1", PORT), Handler).serve_forever()
