/* ===========================================================================
   Perilaku bersama panel administrasi: menu sisi, dialog, konfirmasi hapus,
   dan pencarian yang menerapkan diri sendiri.

   Semua halaman /Admin dirender di server; berkas ini hanya menambah
   kenyamanan. Tanpa JavaScript, tautan dan formulir tetap berfungsi.
   =========================================================================== */
(function () {
    'use strict';

    /* ------------------------------------------------------------ Menu sisi */

    var shell = document.getElementById('admShell');
    var burger = document.getElementById('admBurger');
    var scrim = document.getElementById('admScrim');
    var closeBtn = document.getElementById('admSidebarClose');

    var SIDEBAR_KEY = 'pilarkeuangan.admin.sidebar';

    // Satu tombol menangani dua bentuk menu. Di layar lebar sidebar menciut
    // menjadi rel ikon; di layar sempit sidebar memang berupa laci yang muncul
    // di atas isi halaman, sehingga yang dilakukan adalah membuka dan menutupnya.
    var wideScreen = window.matchMedia('(min-width: 961px)');

    function readStored() {
        // Penyimpanan lokal bisa ditolak peramban (mis. mode privat); kalau begitu
        // sidebar cukup memakai keadaan bawaannya.
        try { return window.localStorage.getItem(SIDEBAR_KEY); } catch (e) { return null; }
    }

    function writeStored(value) {
        try { window.localStorage.setItem(SIDEBAR_KEY, value); } catch (e) { /* diabaikan */ }
    }

    function modalIsOpen() {
        return document.querySelector('.adm-modal:not([hidden])') !== null;
    }

    function syncBurger() {
        if (!burger || !shell) return;

        var isHidden = wideScreen.matches
            ? shell.classList.contains('is-collapsed')
            : !shell.classList.contains('is-menu-open');

        var label = isHidden ? 'Tampilkan menu' : 'Sembunyikan menu';

        burger.setAttribute('aria-expanded', String(!isHidden));
        burger.setAttribute('aria-label', label);
        burger.setAttribute('title', label);
    }

    /** Laci menu pada layar sempit. */
    function setMenu(open) {
        if (!shell) return;

        shell.classList.toggle('is-menu-open', open);
        if (scrim) scrim.hidden = !open;

        // Halaman dikunci selama laci terbuka, tetapi tidak dibuka kembali kalau
        // masih ada dialog yang terbuka dan juga membutuhkan penguncian itu.
        document.body.style.overflow = (open || modalIsOpen()) ? 'hidden' : '';

        syncBurger();
    }

    /** Rel ikon pada layar lebar. Pilihannya diingat antar halaman. */
    function setCollapsed(collapsed) {
        if (!shell) return;

        shell.classList.toggle('is-collapsed', collapsed);
        writeStored(collapsed ? 'collapsed' : 'expanded');
        syncBurger();
    }

    if (shell && readStored() === 'collapsed') {
        shell.classList.add('is-collapsed');
    }

    if (burger) {
        burger.addEventListener('click', function () {
            if (wideScreen.matches) {
                setCollapsed(!shell.classList.contains('is-collapsed'));
            } else {
                setMenu(!shell.classList.contains('is-menu-open'));
            }
        });
    }

    if (scrim) scrim.addEventListener('click', function () { setMenu(false); });
    if (closeBtn) closeBtn.addEventListener('click', function () { setMenu(false); });

    // Saat lebar layar melewati ambang, laci selalu ditutup agar keadaan kedua
    // bentuk menu tidak tercampur. Pilihan ciut/bentang sendiri tetap disimpan.
    function onViewportChange() { setMenu(false); }

    if (typeof wideScreen.addEventListener === 'function') {
        wideScreen.addEventListener('change', onViewportChange);
    } else if (typeof wideScreen.addListener === 'function') {
        wideScreen.addListener(onViewportChange);
    }

    syncBurger();

    /* --------------------------------------------------------------- Dialog */

    var lastFocused = null;

    function openModal(modal) {
        if (!modal) return;
        lastFocused = document.activeElement;
        modal.hidden = false;
        document.body.style.overflow = 'hidden';

        // Fokus ke kolom pertama yang bisa diisi, bukan ke tombol tutup.
        var first = modal.querySelector('[data-autofocus], input:not([type=hidden]):not([disabled]), select, textarea');
        if (first) {
            first.focus();
            if (typeof first.select === 'function' && first.tagName === 'INPUT' && first.type === 'text') {
                first.select();
            }
        }
    }

    function closeModal(modal) {
        if (!modal || modal.hidden) return;
        modal.hidden = true;

        // Laci menu yang masih terbuka tetap membutuhkan halaman dalam keadaan terkunci.
        var drawerOpen = shell !== null && shell.classList.contains('is-menu-open');
        document.body.style.overflow = drawerOpen ? 'hidden' : '';

        if (lastFocused && typeof lastFocused.focus === 'function') lastFocused.focus();
    }

    function closeAllModals() {
        document.querySelectorAll('.adm-modal:not([hidden])').forEach(closeModal);
    }

    document.addEventListener('click', function (ev) {
        var opener = ev.target.closest('[data-modal-open]');

        if (opener) {
            ev.preventDefault();
            var modal = document.getElementById(opener.getAttribute('data-modal-open'));
            if (!modal) return;

            // Tombol "ubah" membawa nilai baris lewat atribut data-field-*, sehingga
            // satu dialog dipakai bersama untuk tambah maupun ubah.
            applyFields(modal, opener);
            openModal(modal);
            return;
        }

        if (ev.target.closest('[data-modal-close]')) {
            ev.preventDefault();
            closeModal(ev.target.closest('.adm-modal'));
            return;
        }

        // Klik pada latar gelap menutup dialog, klik di dalam kotaknya tidak.
        if (ev.target.classList && ev.target.classList.contains('adm-modal')) {
            closeModal(ev.target);
        }
    });

    document.addEventListener('keydown', function (ev) {
        if (ev.key === 'Escape') {
            closeAllModals();
            setMenu(false);
        }
    });

    /**
     * Menyalin nilai dari atribut data-field-<nama> pada tombol pemicu ke kontrol
     * bernama <nama> di dalam dialog. Nilai kosong tetap disalin supaya sisa data
     * dari baris yang dibuka sebelumnya tidak tertinggal.
     */
    function applyFields(modal, trigger) {
        var mode = trigger.getAttribute('data-mode');
        if (mode) {
            modal.querySelectorAll('[data-when]').forEach(function (el) {
                el.hidden = el.getAttribute('data-when') !== mode;
            });
        }

        var title = trigger.getAttribute('data-title');
        var titleEl = modal.querySelector('[data-modal-title]');
        if (title && titleEl) titleEl.textContent = title;

        var form = modal.querySelector('form');
        if (!form) return;

        var action = trigger.getAttribute('data-action');
        if (action) form.setAttribute('action', action);

        Array.prototype.forEach.call(trigger.attributes, function (attr) {
            if (attr.name.indexOf('data-field-') !== 0) return;

            var name = attr.name.slice('data-field-'.length);
            var control = form.querySelector('[data-name="' + name + '"]');
            if (!control) return;

            if (control.type === 'checkbox') {
                control.checked = attr.value === 'true' || attr.value === 'True';
            } else if (control.tagName === 'SELECT' || control.tagName === 'TEXTAREA' || control.tagName === 'INPUT') {
                control.value = attr.value;
            }
        });

        // Daftar centang banyak nilai (mis. peran atau izin) dikirim sebagai satu
        // atribut berisi nilai yang dipisah koma.
        var multi = trigger.getAttribute('data-checked');
        if (multi !== null) {
            var selected = multi.split(',').map(function (v) { return v.trim(); }).filter(Boolean);
            form.querySelectorAll('[data-multi]').forEach(function (box) {
                box.checked = selected.indexOf(box.value) !== -1;
            });
        }
    }

    /* --------------------------------------------- Dialog yang dibuka server */

    // Bila validasi gagal, server menandai dialog mana yang harus terbuka kembali
    // beserta isian yang sudah diketik pengguna.
    var autoOpen = document.querySelector('.adm-modal[data-open-on-load="true"]');
    if (autoOpen) openModal(autoOpen);

    /* ---------------------------------------------------- Konfirmasi tindakan */

    document.addEventListener('submit', function (ev) {
        var form = ev.target;
        var message = form.getAttribute('data-confirm');
        if (!message) return;

        if (!window.confirm(message)) {
            ev.preventDefault();
            return;
        }

        // Cegah klik ganda pada aksi yang mengubah data.
        var submit = form.querySelector('[type=submit]');
        if (submit) setTimeout(function () { submit.disabled = true; }, 0);
    });

    /* ------------------------------------------------------------ Pencarian */

    // Kotak pencarian dan dropdown filter mengirim formulirnya sendiri: kotak teks
    // setelah pengetikan berhenti, dropdown seketika. Tombol "Cari" tetap ada
    // sebagai jalur cadangan bila JavaScript tidak aktif.
    document.querySelectorAll('form[data-auto-submit]').forEach(function (form) {
        var timer = null;

        form.querySelectorAll('[data-auto="text"]').forEach(function (input) {
            input.addEventListener('input', function () {
                window.clearTimeout(timer);
                timer = window.setTimeout(function () { form.requestSubmit(); }, 450);
            });
        });

        form.querySelectorAll('[data-auto="instant"]').forEach(function (control) {
            control.addEventListener('change', function () { form.requestSubmit(); });
        });
    });
})();
