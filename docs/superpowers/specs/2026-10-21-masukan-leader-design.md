# Masukan leader 2026-10-21: editor garis potong, undo/redo, latar, donat, penuhi lembar

Sumber: pesan leader (Yansen) via user, contoh Roboflow Annotate (polygon + titik + slider Simplify), dan jawaban user
2026-10-21. Status: disetujui user ("Kerjakan yang ini"); poin 4 dikonfirmasi 2026-10-22. Implementasi: tahap 1–4 selesai
2026-10-21. Hasil uji: PRD F21–F23, UJI-GARIS-POTONG S11–S12, dan e2e-validation (EDIT-12–14, CUT-07, CUT-14).

## Kebutuhan dan keputusan

| # | Kebutuhan | Keputusan |
| --- | --- | --- |
| 1 | Garis potong berbentuk polygon dengan titik yang bisa diatur operator | Editor per desain (jendela sendiri). Kiss dan die. Titik digeser, ditambah (klik di garis), dihapus (Delete). Slider Simplify terpisah dari Halus. Undo/redo di editor. Berlaku untuk semua salinan desain |
| 2 | Undo/redo | Global di jendela utama: Ctrl+Z, Ctrl+Y, Ctrl+Shift+Z |
| 3 | Latar jangan dihapus otomatis saat file dimasukkan | Termasuk putih. Gambar opak dipakai utuh; latar dibuang manual (Rapikan atau titik polygon) |
| 4 | Stiker donat: garis di luar dan di dalam | Lubang yang tetap terbuka setelah offset dan Halus otomatis mendapat garis dalam (kiss dan die). Dikonfirmasi user 2026-10-22: aturan otomatis ini tetap; lubang yang lebih sempit ditambah lewat klik smart select ([spesifikasi 2026-10-22](2026-10-22-smart-select-design.md), belum diimplementasi) |
| 5 | Compact: ruang kosong diisi stiker tambahan (customer bayar per lembar) | Tombol Compact diganti "Penuhi lembar": semua lembar diisi salinan desain bergiliran sampai tidak ada yang muat. Pangkas tinggi untuk roll dihapus |

## Desain

### 3. Latar tidak dihapus otomatis

- Foreground = alpha > 16 untuk semua gambar. Gambar opak (alpha 255 semua) = seluruh gambar, garis potong mengelilingi kotak gambar.
- Aturan putih (`Fg` < 240, `WhiteBorder`) dihapus. Auto-crop margin transparan tetap (spesifikasi leader).
- Dampak: perbaikan D10 tidak dibutuhkan lagi (semua latar dipertahankan); D7 terjawab (kotak putih di PDF dipertahankan).
- Tes: selfcheck memuat `star.jpg` dan `rough.jpg` setelah "Hapus latar" (alur operator baru), validasi memakai aturan
  yang sama, `expected-results` untuk JPG dan `png-latar-putih` diperbarui.

### 5. Penuhi lembar (pengganti Compact)

- `Nest.Fill`: untuk tiap lembar, grid hunian dibangun dari stiker yang sudah ada, lalu salinan desain (jumlah > 0)
  ditambahkan bergiliran lewat `TryPlace` yang sama dengan susun otomatis. Desain yang tidak muat lagi dikeluarkan dari
  giliran. Jumlah per desain di daftar diperbarui sesuai hasil.
- `Nest.Compact` dan teks "Compact lembar terakhir" dihapus.
- Tes: selfcheck (0 tabrakan, jumlah bertambah, jumlah lembar tetap, semua desain kebagian), validasi B-campuran memakai
  penuhi lembar sebagai pengganti `-compact`.

### 2. Undo/redo global

- Satu langkah = snapshot dokumen setelah aksi operator: daftar desain (beserta lebar dan jumlah), isi lembar (posisi,
  rotasi), tinggi lembar, dan pengaturan (kertas, margin, jarak, kiss/die, bleed, lebar awal).
- Aksi: tambah file, hapus desain, susun otomatis, penuhi lembar, geser, putar, panah, hapus stiker, Rapikan, ganti
  kertas atau preset, simpan editor garis potong.
- Riwayat maksimal 30 langkah (snapshot memegang Sticker lama; batas menjaga RAM).
- Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z di jendela utama; saat mengetik di kotak teks, Ctrl+Z tetap undo teks bawaan.
- Memulihkan pengaturan tidak boleh memicu susun ulang (event kertas/preset ditahan selama pemulihan).

### 4. Garis dalam (donat)

- Lubang yang masih terbuka setelah Close dan Smooth tidak diisi lagi: Potrace ikut membuat kontur dalam.
- Grid nesting tetap memakai bentuk padat (tidak ada stiker di dalam lubang).
- Die harus membungkus kiss: gabungan die dengan kiss memakai mask kiss sebelum lubang diisi.
- Lubang kecil yang tertutup offset (lubang huruf) tetap tidak dipotong.
- Tes: selfcheck S11 diperbarui (lubang tertutup: tanpa kontur dalam; donat: ada kontur dalam kiss dan die, die membungkus kiss).

### 1. Editor garis potong

- Ikon "Edit garis potong" di tiap baris desain membuka jendela editor (pola sama dengan Rapikan).
- Polygon dibuat dari kurva Potrace: kurva dipecah rapat lalu disederhanakan (RDP). Simplify = toleransi 0,05–0,5 mm
  (Complex–Simple). Titik polygon selalu di kurva, jadi garis paling dekat ke artwork bergeser maksimal sebesar toleransi.
- Edit: seret titik, klik di garis menambah titik, Delete menghapus titik terpilih (minimal 3), Ctrl+Z / Ctrl+Y.
  Titik tidak bisa keluar dari kotak desain (grid nesting tetap benar).
- Simpan dinonaktifkan selama garis kiss keluar dari die. Reset otomatis mengembalikan kurva otomatis.
- Data: `Sticker` menyimpan polygon manual kiss/die (koordinat relatif artwork, mm). Grid nesting dan luas dihitung ulang
  dari polygon. Lebar berubah: polygon diskala. Offset/Halus/Simplify berubah: desain manual tidak berubah.
  Daftar desain menandai "garis manual".
- Export: polygon ditulis sebagai garis lurus (M/L/Z) dengan warna CMYK yang sama.

## Urutan kerja dan verifikasi

1. #3 dan #5: build Release/Debug 0 warning, selfcheck 4/4, validasi (perubahan hanya yang diharapkan), UI semua input.
2. #2: selfcheck tidak berubah; uji UI undo/redo.
3. #4: selfcheck S11 baru, validasi.
4. #1: selfcheck polygon (toleransi, skala, kunci, grid), export M/L/Z, uji UI editor.

Setiap tahap: dokumen (PRD, UJI-GARIS-POTONG, expected-results, e2e-validation) dan bukti di `04-evidence`.
Commit dan push hanya atas perintah user.

## Di luar cakupan

- Deteksi objek di foto dengan AI (Smart Select/SAM di Roboflow), crop foto, simpan proyek.
