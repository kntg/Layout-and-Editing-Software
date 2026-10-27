# Printech

Alat internal untuk membuat layout stiker print & cut. Operator menambahkan file dari customer. Printech lalu membuat garis kiss-cut dan die-cut secara otomatis, menyusun stiker di kertas A3+, dan meng-export PDF untuk CorelDRAW dan Cutting Master 5 (Graphtec).

**Status:** prototipe untuk presentasi. Cakupannya automasi layout saja, dengan estimasi 2 minggu (dicatat 2026-10-17). Prototipe dijalankan dari laptop tim dan belum dipasang di PC toko.

Isi README ini:

- [Setup untuk operator](#setup-untuk-operator)
- [Setup untuk developer](#setup-untuk-developer)
- [Alur kerja tim](#alur-kerja-tim)
- [Panduan pemakaian](#panduan-pemakaian)
- [Shortcut](#shortcut)
- [Arti pesan dan peringatan](#arti-pesan-dan-peringatan)
- [Dokumentasi](#dokumentasi)
- [Alat pengembang](#alat-pengembang)

## Setup untuk operator

Belum dipakai: untuk presentasi, prototipe dijalankan dari laptop tim (lihat [Setup untuk developer](#setup-untuk-developer)). Bagian ini untuk nanti, bila Printech dipasang di PC tanpa .NET SDK, misalnya PC toko.

Operator tidak perlu memasang .NET. Yang dibutuhkan:

- Windows 10/11 64-bit.
- CorelDRAW terpasang, hanya bila ingin menambahkan file CDR.

Langkah:

1. Minta `Printech-win-x64.zip` ke developer (cara membuatnya ada di [Membuat zip operator](#membuat-zip-operator)).
2. Klik kanan zip → **Properties** → centang **Unblock** → **OK**. Exe belum ditandatangani, jadi tanpa langkah ini Windows SmartScreen bisa memblokirnya.
3. Ekstrak, lalu jalankan `Printech\StickerLayout.exe`. Bila SmartScreen tetap muncul, pilih **More info → Run anyway**.

## Setup untuk developer

### Kebutuhan

| Untuk | Yang dipasang |
| --- | --- |
| Build dan menjalankan aplikasi | Windows 10/11 64-bit, [Git](https://git-scm.com), [.NET 10 SDK](https://dotnet.microsoft.com/download) |
| Runner self-check dan validasi | PowerShell 7 (`pwsh`) |
| Uji UI (`Printech-TestData/ui-*.ps1`) | Windows PowerShell 5.1 (bawaan Windows) |
| Validasi, generator e2e, uji negatif runner | Python 3 dengan PyMuPDF, Pillow, NumPy, dan SciPy: `pip install pymupdf pillow numpy scipy` |
| Lint Markdown | Node.js (`npx`) |
| Menguji file CDR | CorelDRAW terpasang |

Editor kode bebas; semua build lewat `dotnet` CLI. Paket NuGet (PDFium, PDFsharp, BitmapToVector/Potrace, SharpVectors) diunduh otomatis saat build pertama.

### Ambil kode dan jalankan

Clone ke folder berpath pendek, misalnya `C:\src`. Windows membatasi panjang path 260 karakter, dan path file terpanjang di repo (termasuk hasil build) sekitar 120 karakter. Bila path folder terlalu panjang, Git gagal dengan pesan `Filename too long`.

```powershell
git clone https://github.com/kntg/Layout-and-Editing-Software.git
cd Layout-and-Editing-Software
dotnet build StickerLayout
dotnet run --project StickerLayout
```

File bisa langsung dibuka lewat argumen: `dotnet run --project StickerLayout -- samples\circle.png samples\logo.pdf`. Folder `samples/` berisi file contoh kecil.

### Cek sebelum push

```powershell
dotnet build StickerLayout -c Release            # harus 0 warning, 0 error
pwsh -File Printech-TestData/run-selfcheck.ps1   # harus "RINGKAS: 4/4 hasil sesuai harapan"
```

Tambahan sesuai bagian yang diubah:

- **Tampilan atau alur UI:** uji UI di `Printech-TestData`, dijalankan dengan Windows PowerShell 5.1. Jangan sentuh mouse dan keyboard selama uji. Daftarnya ada di [README data uji](Printech-TestData/README.md).
- **Garis potong, nesting, atau export:** validasi (`validasi.ps1` lalu `validasi.py`).
- **Dokumen:** markdownlint. Bila kasus uji e2e berubah, ubah `docs/testing/gen_e2e.py` atau template-nya, lalu jalankan generatornya.

### Membuat zip operator

```powershell
dotnet publish StickerLayout -c Release -r win-x64 --self-contained -o publish\Printech
Compress-Archive -Path publish\Printech -DestinationPath publish\Printech-win-x64.zip -Force
```

Hasilnya `publish\Printech-win-x64.zip`, berisi folder `Printech`. Folder `publish/` tidak masuk git, jadi zip tidak ikut ter-commit. Bagikan zip hanya di dalam perusahaan. BitmapToVector (Potrace) berlisensi GPL-3.0, jadi zip tidak boleh diunggah ke GitHub Release publik atau dibagikan ke luar.

### Struktur folder

| Lokasi | Isi |
| --- | --- |
| `StickerLayout/Core.cs` | Memuat desain, garis potong otomatis (mask, penghalusan, Potrace), susun otomatis (nesting), worker CorelDRAW |
| `StickerLayout/Curves.cs`, `Shapes.cs`, `SmartSelect.cs` | Node garis potong (lurus/kurva), preset bentuk dan desain kotak, smart select |
| `StickerLayout/Export.cs`, `Pdfium.cs` | Export PDF, preview JPG, Print Ticket; render PDF/AI |
| `StickerLayout/Cleanup.cs` | Rapikan gambar (hapus latar, penghapus klik, kikis tepi) |
| `StickerLayout/MainWindow.*`, `CutEditWindow.*`, `CleanupWindow.*`, `RulerControl.cs`, `Theme.xaml` | Tampilan: jendela utama, editor garis potong, jendela Rapikan, penggaris, tema |
| `StickerLayout/Strings.cs` | Semua teks UI dalam bahasa Indonesia dan English |
| `StickerLayout/SelfCheck.cs` | Uji otomatis bawaan (`StickerLayout.exe --selfcheck <folder>`) |
| `samples/` | File contoh. Self-check juga menulis hasilnya di sini; hasil itu diabaikan git |
| `Printech-TestData/` | Data uji dengan hasil benar, skrip validasi dan uji UI, bukti uji |
| `docs/` | PRD, riset, skenario uji, validasi e2e, spesifikasi fitur |

## Alur kerja tim

- **Repo tim:** [kntg/Layout-and-Editing-Software](https://github.com/kntg/Layout-and-Editing-Software). Repo ini **publik**. `main` harus selalu bisa di-build dan lulus self-check.
- **Branch:** kerjakan di branch sendiri (`feature/<nama>`, `fix/<nama>`), lalu gabungkan ke `main` lewat pull request.
- **Sebelum push:** jalankan `git fetch`, gabungkan `main` terbaru ke branch Anda (merge), lalu jalankan [cek sebelum push](#cek-sebelum-push).
- **Jangan `git push --force`** ke `main`, karena commit rekan lain bisa hilang.
- **Jangan commit:** file customer, data internal toko, atau kata sandi. File customer untuk uji disimpan di `Printech-TestData/01-input/customer/`; folder itu diabaikan git.
- **Fitur yang mengubah perilaku:** perbarui juga [PRD](docs/PRD.md), skenario uji, dan kasus e2e.
- **Pesan commit:** bahasa Indonesia, satu baris ringkas, lalu penjelasan alasannya.

## Panduan pemakaian

### Alur singkat

1. **Tambah file.** Ada tiga cara: tarik file ke jendela, klik **Tambah file…** (Ctrl+O atau Ctrl+I), atau buka file lewat "Open with". File langsung disusun di lembar.
   - Format: PDF, AI (PDF-compatible), CDR, PNG, JPG, SVG. PDF banyak halaman menjadi satu desain per halaman.
   - CDR dibuka lewat CorelDRAW yang terpasang. Jendela Printech tidak macet selama menunggu.
2. **Atur desain** di panel **Desain**.
   - Setiap desain punya lebar artwork (mm) dan jumlah (pcs).
   - PDF/AI/CDR memakai ukuran asli. PNG/JPG/SVG memakai "Lebar awal" (bawaan 50 mm).
   - Angka dpi merah berarti resolusi di bawah 150 dpi, dan hasil cetaknya akan pecah.
3. **Pilih kertas** di panel **Kertas**.
   - Pilihan: A3+ 320 × 480 mm (bawaan, kertas Develop), A0–A6, SRA3, SRA4, atau Custom. Ada juga tombol putar kertas.
   - Margin tepi (bawaan 10 mm) adalah area kosong untuk reg mark Cutting Master 5 dan printer. Margin yang pas untuk format reg mark operator belum dipastikan.
4. **Atur garis potong** di panel **Garis potong**.
   - Kiss-cut: bawaan aktif, offset 2 mm, Halus 1 mm.
   - Die-cut: bawaan mati, 4/2 mm.
   - Jarak antar stiker: 2 mm. Bleed gambar PNG/JPG: 0 = mati.
   - Perubahan lebar, jumlah, dan garis potong baru diterapkan setelah klik **Susun otomatis** (Ctrl+L). Ganti atau putar kertas langsung menyusun ulang.
5. **Periksa layout.**
   - **Seret stiker:** stiker pindah ke posisi kosong terdekat. R memutar 90°, Delete menghapus, panah menggeser 1 mm (Shift: 10 mm).
   - **Stiker bertanda merah:** tumpang tindih atau keluar margin.
   - **Penuhi lembar:** mengisi ruang kosong semua lembar dengan salinan desain bergiliran, dan jumlah per desain ikut bertambah.
   - **Urungkan / Ulangi:** Ctrl+Z / Ctrl+Y, sampai 30 langkah.
6. **Export.**
   - **Export PDF…** (Ctrl+E): satu halaman per lembar. Artwork vektor tetap vektor. Garis kiss magenta C0 M100 Y0 K0, die cyan C100 M0 Y0 K0.
   - **Export preview JPG…** (Ctrl+Shift+E): untuk konfirmasi ke customer lewat WhatsApp.
   - **Export Print Ticket** (menu File): ringkasan A4 untuk lampiran SPK.
7. **Cetak dan potong.**
   - Buka PDF di CorelDRAW, lalu pasang reg mark lewat Cutting Master 5.
   - Cetak di Develop.
   - Potong di Graphtec dengan memilih garis per warna: magenta = kiss-cut, cyan = die-cut.

### Garis potong otomatis

- **Bentuk garis:** mengikuti bentuk artwork (bagian yang tidak transparan) sejauh offset. Halus menghaluskan lekukan kecil; disarankan 1–2 mm, 0 = mati.
- **Latar gambar opak tidak dibuang otomatis.** Gambar opak adalah JPG atau PNG tanpa transparansi. Gambar ini dipakai utuh, dan desain kotak mendapat garis kotak bersudut tajam.
- **Garis yang mengikuti objek:** buang latar dulu lewat Rapikan gambar, atau pakai smart select di editor garis potong.
- **Lubang:** lubang asli artwork yang masih terbuka setelah offset (misalnya stiker donat) mendapat garis dalam.
- **Kiss dan die:** garis die-cut selalu membungkus garis kiss-cut.

### Editor garis potong

Buka lewat ikon pensil di baris desain. Hasilnya berlaku untuk semua salinan desain itu.

- **Bentuk.**
  - Pilihan: Otomatis, kotak, kotak sudut bulat, lingkaran, oval, segitiga, bintang, hati, segi enam.
  - Bentuk berlaku untuk garis yang dipilih di **Garis yang diedit**, jadi kiss dan die bisa berbeda. Contoh: pilih Kiss-cut lalu Bintang, kemudian Die-cut lalu Kotak.
  - Die Otomatis mengikuti bentuk kiss. Die dengan bentuk lain membungkus garis kiss sejauh selisih offset.
  - Bentuk mengelilingi artwork sejauh offset, dan ikut menyesuaikan bila offset atau lebar diubah, selama node-nya tidak diedit.
  - Nama bentuk tampil di daftar desain, misalnya "Bintang · die Kotak".
- **Warna border.**
  - Pilihan: tanpa warna, merah, hitam, biru, kuning, hijau, atau CMYK kustom (empat angka persen, misalnya `0 100 100 0`, lalu Enter).
  - Area antara artwork dan garis kiss (tanpa kiss: garis die) dicetak dengan warna itu, diteruskan sejauh bleed di luar garis (bila bleed 0: 1 mm).
- **Garis yang diedit:** kiss-cut atau die-cut.
- **Simplify** (hanya untuk bentuk Otomatis): makin ke kanan (Sederhana), node makin sedikit. Garis dibuat ulang dari garis otomatis.
- **Smart select.**
  - Centang, lalu klik objek: garis hanya mengikuti objek itu. Piksel tipis seperti watermark diabaikan; atur lewat slider Toleransi.
  - Shift+klik menambah objek. Klik di dalam lubang menambah atau menghapus garis dalamnya.
- **Node** (dengan mouse di kanvas).
  - Klik node untuk memilihnya; klik saja tidak menggeser node. Shift+klik menambah atau melepas node dari pilihan.
  - Seret node terpilih untuk menggeser semuanya sejauh gerakan mouse.
  - Klik di garis atau kurva untuk menambah node. Delete menghapus semua node terpilih.
  - Bila tepat satu node terpilih, handle (titik bulat) tampil untuk mengatur lengkung.
  - Tombol **Jadikan kurva / Jadikan garis** dan **Node halus / Node sudut** berlaku untuk semua node terpilih.
- **Urungkan / Ulangi** (Ctrl+Z / Ctrl+Y) dan **Reset otomatis**. Ganti warna border belum ikut urungkan di editor; Urungkan di jendela utama mencakupnya.
- **Simpan** nonaktif selama garis kiss keluar dari garis die.

### Rapikan gambar

- **Untuk:** desain gambar, yaitu PNG, JPG, SVG, dan PDF yang ditempel sebagai gambar.
- **Cara membuka:** ikon penghapus di baris desain, atau dobel-klik thumbnail.
- **Isi:** hapus latar otomatis (warna latar diambil dari tepi gambar), penghapus klik, toleransi warna, kikis tepi 0–3 px, lalu **Pakai**.
- **Catatan:** file vektor (PDF/AI/CDR) dirapikan di CorelDRAW supaya warnanya tetap utuh. Fitur ini dibekukan, tidak dikembangkan lagi.

### Tampilan dan bahasa

- **Zoom:** Ctrl + roda mouse, Ctrl++ / Ctrl+-. Pas layar: Ctrl+0 atau F4.
- **Geser tampilan:** alat Hand (H), tahan Spasi, atau tombol tengah mouse.
- **Alat Zoom (Z):** klik untuk memperbesar, Alt+klik untuk memperkecil.
- **Penggaris (mm):** tampil di atas dan kiri lembar. Bisa disembunyikan lewat tombol penggaris di batang alat kiri.
- **Bahasa:** menu **Bahasa**, pilih Indonesia atau English.
- **Tanda registrasi:** menu **Tanda registrasi**, pilih Regmark 1 (bawaan) atau Tidak ada.
  - Regmark 1 menggambar siku L hitam di 4 pojok lembar, ikut di layar, preview JPG, dan PDF.
  - Stiker tidak ditaruh dalam 6 mm dari tanda (area pindai menurut manual Graphtec). Stiker yang masuk area itu ditandai merah; klik Susun otomatis untuk memindahkannya.

## Shortcut

| Tombol | Fungsi |
| --- | --- |
| Ctrl+O, Ctrl+I | Tambah file |
| Ctrl+L | Susun otomatis |
| Ctrl+E | Export PDF |
| Ctrl+Shift+E | Export preview JPG |
| Ctrl+Z | Urungkan |
| Ctrl+Y, Ctrl+Shift+Z | Ulangi |
| Ctrl+0, F4 | Pas layar |
| Ctrl++, Ctrl+-, Ctrl + roda mouse | Zoom |
| V / H / Z | Alat Pilih / Geser tampilan / Zoom |
| Spasi (tahan), tombol tengah mouse | Geser tampilan sementara |
| R | Putar stiker terpilih 90° |
| Delete | Hapus stiker terpilih (di editor: hapus node terpilih) |
| Panah, Shift+panah | Geser stiker terpilih 1 mm / 10 mm |

## Arti pesan dan peringatan

| Pesan | Arti dan yang perlu dilakukan |
| --- | --- |
| "… lebih besar dari kertas" | Desain tidak muat di area cetak. Perkecil lebarnya, atau pilih kertas yang lebih besar. |
| "… desain resolusi rendah" | Di bawah 150 dpi pada lebar itu. Minta file asli ke customer, atau perkecil desainnya. |
| "… PDF ditempel sebagai gambar (halaman diputar atau tidak terbaca)" | PDF dicetak sebagai gambar, bukan vektor. Periksa ketajaman dan warnanya sebelum cetak. |
| "… tumpang tindih/keluar margin" | Ada stiker bertanda merah. Geser stikernya atau klik Susun otomatis. Export masih bisa setelah konfirmasi. |
| "CorelDRAW tidak terpasang…" | Export CDR ke PDF dulu di PC yang ada CorelDRAW-nya, lalu tambahkan PDF-nya. |
| "CorelDRAW tidak merespons dalam 30 detik…" | Cek jendela CorelDRAW, mungkin ada dialog yang terbuka. Atau export CDR ke PDF secara manual. |
| "Aktifkan minimal satu garis potong." | Centang kiss-cut atau die-cut. |
| "Offset die-cut harus lebih besar dari offset kiss-cut." | Naikkan offset die-cut. |
| "Bleed tidak boleh lebih besar dari jarak antar stiker." | Kecilkan bleed atau besarkan jarak antar stiker. |
| "Jarak antar stiker minimal 1 mm bila warna border dipakai tanpa bleed." | Besarkan jarak antar stiker, atau isi bleed. |

## Dokumentasi

- [PRD](docs/PRD.md): tujuan, fitur, keputusan, batasan, checklist uji operator, dan cara menjalankan (§11).
- [Riset](docs/RESEARCH.md): riset pendukung keputusan.
- [Uji garis potong](docs/UJI-GARIS-POTONG.md): skenario uji S1–S17 dan hasilnya.
- [Uji stabilitas](docs/UJI-STABILITAS.md): skenario uji P1–P10 (worker CDR, paralel, RAM) dan hasilnya.
- [Validasi end-to-end](docs/testing/e2e-validation.md): semua kasus uji dari import sampai potong di Graphtec, dengan status PASS/FAIL/BLOCKED/NOT RUN dan buktinya. Lembar isian: [CSV](docs/testing/e2e-test-results-template.csv).
- [Data uji](Printech-TestData/README.md): file uji dengan hasil benar yang sudah diketahui, skrip validasi, dan skrip uji UI.
- [Simulasi produksi](docs/simulasi/index.html): tampilan 3D satu lembar hasil validasi. Buka file ini di browser.
- [Spesifikasi](docs/superpowers/specs/) dan [rencana implementasi](docs/superpowers/plans/) fitur baru (editor garis potong, smart select, bentuk garis potong, warna border).

## Alat pengembang

| Perintah | Fungsi |
| --- | --- |
| `pwsh -File Printech-TestData/run-selfcheck.ps1 [-Config Debug]` | Self-check 4 kali: folder baru, folder `samples`, dan 2 uji negatif yang harus gagal. Exit code 1 bila ada hasil yang tidak sesuai harapan. |
| `pwsh Printech-TestData/validasi.ps1`, lalu `python Printech-TestData/validasi.py` | Validasi garis potong, nesting, dan export pada data uji. Rinciannya di [README data uji](Printech-TestData/README.md). |
| `python -I docs/testing/gen_e2e.py` | Membuat ulang `e2e-validation.md` dan CSV-nya dari `e2e-validation.template.md` dan daftar kasus uji di `gen_e2e.py`. |
| `npx --yes markdownlint-cli2@0.23.2 "**/*.md"` | Memeriksa semua file Markdown dengan aturan di `.markdownlint-cli2.jsonc`. |
| `StickerLayout\bin\Debug\net10.0-windows\StickerLayout.exe --selfcheck samples` | Self-check satu kali di folder `samples`. Hasilnya di `samples/selfcheck.txt`; exit code 0 = lulus. |
