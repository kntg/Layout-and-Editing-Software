# Printech

Alat internal untuk membuat layout stiker print & cut. Operator menambahkan file dari customer. Printech lalu membuat garis kiss-cut dan die-cut secara otomatis, menyusun stiker di kertas A3+, dan meng-export PDF untuk CorelDRAW dan Cutting Master 5 (Graphtec).

**Status:** prototipe untuk presentasi. Cakupannya automasi layout saja, dengan estimasi 2 minggu (dicatat 2026-10-17). Prototipe dijalankan dari laptop tim dan belum dipasang di PC toko.

Isi README ini:

- [Cara pakai](#cara-pakai)
- [Shortcut](#shortcut)
- [Arti pesan dan peringatan](#arti-pesan-dan-peringatan)
- [Daftar istilah](#daftar-istilah)
- [Memasang di PC operator (nanti)](#memasang-di-pc-operator-nanti)
- [Dokumentasi](#dokumentasi)
- [Untuk pengembang](#untuk-pengembang)

## Cara pakai

### Alur singkat

1. **Tambah file.** Ada tiga cara: tarik file ke jendela, klik **Tambah file…** (Ctrl+O atau Ctrl+I), atau buka file lewat "Open with". File langsung disusun di lembar.
   - Format: PDF, AI (yang disimpan PDF-compatible), CDR, PNG, JPG, SVG. PDF dengan banyak halaman menjadi satu desain per halaman.
   - CDR dibuka lewat CorelDRAW yang terpasang. Jendela Printech tidak macet selama menunggu.
2. **Atur desain** di panel **Desain**.
   - Setiap desain punya lebar artwork (mm) dan jumlah (pcs).
   - PDF/AI/CDR memakai ukuran aslinya. PNG/JPG/SVG memakai "Lebar awal" (bawaan 50 mm).
   - Angka dpi merah berarti resolusinya di bawah 150 dpi, dan hasil cetaknya akan pecah.
3. **Pilih kertas** di panel **Kertas**.
   - Pilihan: A3+ 320 × 480 mm (bawaan, kertas Develop), A0–A6, SRA3, SRA4, atau Custom (ukuran bebas). Ada juga tombol putar kertas.
   - Margin tepi (bawaan 10 mm) adalah area kosong untuk tanda registrasi dari Cutting Master 5 dan untuk printer. Margin yang pas untuk format tanda registrasi operator belum dipastikan.
4. **Atur garis potong** di panel **Garis potong**.
   - Kiss-cut: bawaan aktif, offset 2 mm, Halus 1 mm.
   - Die-cut: bawaan mati, offset 4 mm, Halus 2 mm.
   - Jarak antar stiker: 2 mm. Bleed gambar PNG/JPG: 0 = mati.
   - Perubahan lebar, jumlah, dan garis potong baru diterapkan setelah klik **Susun otomatis** (Ctrl+L). Mengganti atau memutar kertas langsung menyusun ulang.
5. **Periksa layout.**
   - **Seret stiker:** stiker pindah ke posisi kosong terdekat. R memutar 90°, Delete menghapus, panah menggeser 1 mm (dengan Shift: 10 mm).
   - **Stiker bertanda merah:** tumpang tindih dengan stiker lain atau keluar margin.
   - **Penuhi lembar:** mengisi ruang kosong di semua lembar dengan salinan desain bergiliran, dan jumlah per desain ikut bertambah.
   - **Urungkan / Ulangi:** Ctrl+Z / Ctrl+Y, sampai 30 langkah.
6. **Export.**
   - **Export PDF…** (Ctrl+E): satu halaman per lembar. Artwork vektor tetap vektor. Garis kiss magenta C0 M100 Y0 K0, die cyan C100 M0 Y0 K0.
   - **Export preview JPG…** (Ctrl+Shift+E): untuk konfirmasi ke customer lewat WhatsApp.
   - **Export Print Ticket** (menu File): ringkasan satu halaman A4 untuk lampiran SPK.
   - **Export SVG** (menu File): satu file SVG per lembar dengan layer Artwork, Kiss Cut, Die Cut, dan tanda registrasi. Artwork di dalamnya berupa gambar dan warnanya RGB; export PDF tetap menjaga vektor dan CMYK.
7. **Cetak dan potong.**
   - Buka PDF di CorelDRAW, lalu pasang tanda registrasi lewat Cutting Master 5.
   - Cetak di Develop.
   - Potong di Graphtec dengan memilih garis per warna: magenta = kiss-cut, cyan = die-cut.

### Garis potong otomatis

- **Bentuk garis:** mengikuti bentuk artwork (bagian yang tidak transparan) sejauh offset. Halus menghaluskan lekukan kecil; disarankan 1–2 mm, 0 = mati.
- **Latar gambar tanpa bagian transparan tidak dibuang otomatis.** Contohnya JPG, atau PNG tanpa transparansi. Gambar seperti ini dipakai utuh, dan desain berbentuk kotak mendapat garis kotak bersudut tajam.
- **Garis yang mengikuti objek:** buang latar dulu lewat Rapikan gambar, atau pakai smart select di editor garis potong.
- **Lubang:** lubang asli artwork yang masih terbuka setelah offset (misalnya stiker donat) mendapat garis dalam.
- **Kiss dan die:** garis die-cut selalu membungkus garis kiss-cut.

### Editor garis potong

Buka lewat ikon pensil di baris desain. Hasilnya berlaku untuk semua salinan desain itu.

- **Bentuk.**
  - Pilihan: Otomatis, kotak, kotak sudut bulat, lingkaran, oval, segitiga, bintang, hati, segi enam.
  - Bentuk berlaku untuk garis yang dipilih di **Garis yang diedit**, jadi kiss dan die bisa berbeda. Contoh: pilih Kiss-cut lalu Bintang, kemudian Die-cut lalu Kotak.
  - Die Otomatis mengikuti bentuk kiss. Die dengan bentuk lain membungkus garis kiss sejauh selisih offset.
  - Bentuk mengelilingi artwork sejauh offset, dan ikut menyesuaikan bila offset atau lebar diubah, selama titiknya tidak diedit.
  - Nama bentuk tampil di daftar desain, misalnya "Bintang · die Kotak".
- **Warna border.**
  - Pilihan: tanpa warna, merah, hitam, biru, kuning, hijau, atau CMYK pilihan sendiri (empat angka persen, misalnya `0 100 100 0`, lalu Enter).
  - Area antara artwork dan garis kiss (tanpa kiss: garis die) dicetak dengan warna itu, diteruskan sejauh bleed di luar garis (bila bleed 0: 1 mm).
- **Garis yang diedit:** kiss-cut atau die-cut.
- **Simplify** (hanya untuk bentuk Otomatis): makin ke kanan (Sederhana), titik garisnya makin sedikit. Garis dibuat ulang dari garis otomatis.
- **Smart select.**
  - Centang, lalu klik objek: garis hanya mengikuti objek itu. Piksel tipis seperti watermark diabaikan; atur lewat slider Toleransi.
  - Shift+klik menambah objek. Klik di dalam lubang menambah atau menghapus garis dalamnya.
- **Titik garis (node)**, diatur dengan mouse di kanvas.
  - Klik titik untuk memilihnya; klik saja tidak menggeser titik. Shift+klik menambah atau melepas titik dari pilihan.
  - Seret titik yang dipilih untuk menggeser semuanya sejauh gerakan mouse.
  - Klik di garis atau kurva untuk menambah titik. Delete menghapus semua titik yang dipilih.
  - Bila tepat satu titik dipilih, pegangan kurva (handle, berupa titik bulat) tampil untuk mengatur lengkung.
  - Tombol **Jadikan kurva / Jadikan garis** dan **Node halus / Node sudut** berlaku untuk semua titik yang dipilih.
- **Urungkan / Ulangi** (Ctrl+Z / Ctrl+Y) dan **Reset otomatis**. Ganti warna border belum ikut urungkan di editor; Urungkan di jendela utama mencakupnya.
- **Simpan** nonaktif selama garis kiss keluar dari garis die.

### Rapikan gambar

- **Untuk:** desain berupa gambar, yaitu PNG, JPG, SVG, dan PDF yang ditempel sebagai gambar.
- **Cara membuka:** ikon penghapus di baris desain, atau klik dua kali gambar kecil desainnya.
- **Isi:** hapus latar otomatis (warna latar diambil dari tepi gambar), penghapus klik, toleransi warna, kikis tepi 0–3 piksel, lalu **Pakai**.
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
| R | Putar stiker yang dipilih 90° |
| Delete | Hapus stiker yang dipilih (di editor: hapus titik yang dipilih) |
| Panah, Shift+panah | Geser stiker yang dipilih 1 mm / 10 mm |

## Arti pesan dan peringatan

| Pesan | Arti dan yang perlu dilakukan |
| --- | --- |
| "… lebih besar dari kertas" | Desain tidak muat di area cetak. Perkecil lebarnya, atau pilih kertas yang lebih besar. |
| "… desain resolusi rendah" | Di bawah 150 dpi pada lebar itu. Minta file asli ke customer, atau perkecil desainnya. |
| "… PDF ditempel sebagai gambar (halaman diputar atau tidak terbaca)" | PDF dicetak sebagai gambar, bukan vektor. Periksa ketajaman dan warnanya sebelum cetak. |
| "… tumpang tindih/keluar margin" | Ada stiker bertanda merah. Geser stikernya atau klik Susun otomatis. Export masih bisa setelah konfirmasi. |
| "CorelDRAW tidak terpasang…" | Export CDR ke PDF dulu di PC yang ada CorelDRAW-nya, lalu tambahkan PDF-nya. |
| "CorelDRAW tidak merespons dalam 30 detik…" | Cek jendela CorelDRAW, mungkin ada dialog yang terbuka. Atau export CDR ke PDF secara manual. |
| "File AI ini tidak bisa dibaca…" | AI disimpan tanpa opsi PDF-compatible. Simpan ulang dari Illustrator dengan "Create PDF Compatible File", atau export ke PDF. |
| "PDF tidak bisa dibuka: file rusak atau dilindungi password." | Minta file baru ke customer, atau buka password PDF-nya dulu. |
| "Gambar rusak…" atau "SVG rusak…" | File rusak atau ekstensinya salah. Minta file baru ke customer. |
| "Aktifkan minimal satu garis potong." | Centang kiss-cut atau die-cut. |
| "Offset die-cut harus lebih besar dari offset kiss-cut." | Naikkan offset die-cut. |
| "Bleed tidak boleh lebih besar dari jarak antar stiker." | Kecilkan bleed atau besarkan jarak antar stiker. |
| "Jarak antar stiker minimal 1 mm bila warna border dipakai tanpa bleed." | Besarkan jarak antar stiker, atau isi bleed. |

## Daftar istilah

| Istilah | Arti |
| --- | --- |
| Artwork | Gambar atau desain yang dicetak. |
| Kiss-cut | Potongan yang hanya memotong lapisan stiker; alasnya tetap utuh. Garisnya magenta di PDF. |
| Die-cut | Potongan tembus sampai alas, sehingga stiker lepas satu per satu. Garisnya cyan di PDF. |
| Offset | Jarak garis potong dari tepi artwork. |
| Halus | Pengaturan yang menghaluskan lekukan kecil pada garis potong. |
| Bleed | Warna tepi artwork yang diperluas sedikit melewati garis potong, supaya tidak ada tepi putih bila pisau sedikit bergeser. |
| Border | Area antara artwork dan garis kiss. |
| Susun otomatis (nesting) | Menata stiker di lembar supaya bahan terpakai sehemat mungkin. |
| Titik garis (node) | Titik pada garis potong yang bisa digeser di editor. Makin sedikit titiknya, makin mulus gerakan pisau. |
| Tanda registrasi (reg mark) | Siku hitam di pojok lembar yang dibaca mesin Graphtec, supaya posisi potong sama dengan hasil cetak. |
| dpi | Jumlah titik gambar per inci. Makin tinggi, makin tajam hasil cetak. |
| CMYK | Empat warna tinta cetak: cyan, magenta, kuning (yellow), dan hitam (K). |
| Vektor | Gambar dari garis dan bentuk; tetap tajam di ukuran berapa pun. |
| Gambar (raster) | Gambar dari piksel, misalnya PNG dan JPG; bisa pecah bila diperbesar. |
| Pemeriksaan otomatis (self-check) | Uji bawaan aplikasi yang memeriksa garis potong, susunan, dan export tanpa membuka jendela. |
| Uji alur lengkap (e2e) | Uji dari file masuk sampai hasil potong. Dulu disebut "end-to-end" atau "e2e". |
| Skrip uji (berakhiran .ps1) | File berisi perintah Windows PowerShell yang menjalankan uji secara otomatis. |
| Branch, commit, push | Istilah git: cabang kerja, simpanan perubahan, dan pengiriman perubahan ke GitHub. |

## Memasang di PC operator (nanti)

Belum dipakai: untuk presentasi, prototipe dijalankan dari laptop tim (lihat [Untuk pengembang](#untuk-pengembang)). Bagian ini untuk nanti, bila Printech dipasang di PC tanpa .NET SDK, misalnya PC toko.

Operator tidak perlu memasang .NET. Yang dibutuhkan:

- Windows 10/11 64-bit.
- CorelDRAW terpasang, hanya bila ingin menambahkan file CDR.

Langkah:

1. Minta `Printech-win-x64.zip` ke pengembang (cara membuatnya ada di [Membuat zip untuk operator](#membuat-zip-untuk-operator)).
2. Klik kanan zip, pilih **Properties**, centang **Unblock**, lalu **OK**. Aplikasinya belum ditandatangani, jadi tanpa langkah ini Windows SmartScreen bisa memblokirnya.
3. Ekstrak, lalu jalankan `Printech\StickerLayout.exe`. Bila SmartScreen tetap muncul, pilih **More info**, lalu **Run anyway**.

## Dokumentasi

- [Kebutuhan aplikasi](docs/kebutuhan-aplikasi.md) (dulu bernama PRD): tujuan, fitur, keputusan, batasan, daftar uji untuk operator, dan cara menjalankan (§11).
- [Catatan riset](docs/RESEARCH.md): riset pendukung keputusan.
- [Skenario uji garis potong](docs/UJI-GARIS-POTONG.md): skenario S1–S17 dan hasilnya.
- [Skenario uji stabilitas](docs/UJI-STABILITAS.md): skenario P1–P10 (proses pembantu CDR, kerja paralel, memori) dan hasilnya.
- [Uji alur lengkap](docs/pengujian/uji-alur-lengkap.md): semua kasus uji dari file masuk sampai potong di Graphtec, dengan status PASS/FAIL/BLOCKED/NOT RUN dan buktinya. Lembar isian: [lembar hasil uji (CSV)](docs/pengujian/lembar-hasil-uji-alur-lengkap.csv).
- [Data uji](Printech-TestData/README.md): file uji dengan hasil benar yang sudah diketahui, skrip validasi, dan skrip uji tampilan.
- [Simulasi produksi](docs/simulasi/index.html): tampilan 3D satu lembar hasil validasi. Buka file ini di browser.
- [Spesifikasi](docs/superpowers/specs/) dan [rencana pembuatan](docs/superpowers/plans/) fitur baru (editor garis potong, smart select, bentuk garis potong, warna border).

## Untuk pengembang

### Kebutuhan

| Untuk | Yang dipasang |
| --- | --- |
| Build dan menjalankan aplikasi | Windows 10/11 64-bit, [Git](https://git-scm.com), [.NET 10 SDK](https://dotnet.microsoft.com/download) |
| Pemeriksaan otomatis dan validasi | PowerShell 7 (`pwsh`) |
| Uji tampilan (skrip `Printech-TestData/ui-*.ps1`) | Windows PowerShell 5.1 (bawaan Windows) |
| Validasi, pembuat dokumen uji alur lengkap, uji negatif pemeriksaan otomatis | Python 3 dengan PyMuPDF, Pillow, NumPy, dan SciPy: `pip install pymupdf pillow numpy scipy` |
| Pemeriksaan format dokumen Markdown | Node.js (`npx`) |
| Menguji file CDR | CorelDRAW terpasang |

Editor kode bebas; semua build lewat perintah `dotnet`. Pustaka yang dipakai (PDFium, PDFsharp, BitmapToVector/Potrace, SharpVectors) diunduh otomatis saat build pertama.

### Ambil kode dan jalankan

Unduh kode (clone) ke folder beralamat pendek, misalnya `C:\src`. Windows membatasi panjang alamat file 260 karakter, dan alamat file terpanjang di repo (termasuk hasil build) sekitar 120 karakter. Bila alamat foldernya terlalu panjang, Git gagal dengan pesan `Filename too long`.

```powershell
git clone https://github.com/kntg/Layout-and-Editing-Software.git
cd Layout-and-Editing-Software
dotnet build StickerLayout
dotnet run --project StickerLayout
```

File bisa langsung dibuka dengan menuliskannya di akhir perintah: `dotnet run --project StickerLayout -- samples\circle.png samples\logo.pdf`. Folder `samples/` berisi file contoh kecil.

### Cek sebelum push

Wajib sebelum push ke repo tim atau sebelum presentasi (keputusan 2026-10-28):

1. Build versi rilis: harus 0 warning dan 0 error.
2. Pemeriksaan otomatis aplikasi: harus `RINGKAS: 4/4 hasil sesuai harapan`.
3. Validasi otomatis: data uji di-export, lalu hasilnya dinilai; harus `RINGKAS: SEMUA LULUS` (exit code 0).

```powershell
dotnet build StickerLayout -c Release            # harus 0 warning, 0 error
pwsh -File Printech-TestData/run-selfcheck.ps1   # harus "RINGKAS: 4/4 hasil sesuai harapan"
pwsh Printech-TestData/validasi.ps1              # data uji diexport ke 03-exports/validasi
python Printech-TestData/validasi.py             # harus "RINGKAS: SEMUA LULUS" (exit 0)
```

Tambahan sesuai bagian yang diubah:

- **Tampilan, editor, atau alur di jendela aplikasi:** jalankan juga uji tata letak (`ui-edit-layout.ps1`, 10/10 langkah lulus), yang memakai mouse dan keyboard sungguhan, ditambah uji tampilan lain yang terkait. Jalankan dengan Windows PowerShell 5.1, dan jangan pakai PC selama uji. Daftarnya ada di [panduan data uji](Printech-TestData/README.md).
- **Dokumen:** periksa formatnya dengan markdownlint. Bila kasus uji alur lengkap berubah, ubah `docs/pengujian/buat-uji-alur-lengkap.py` atau templatnya, lalu jalankan skrip itu.

### Alat pengembang

| Perintah | Fungsi |
| --- | --- |
| `pwsh -File Printech-TestData/run-selfcheck.ps1 [-Config Debug]` | Pemeriksaan otomatis 4 kali: folder baru, folder `samples`, dan 2 uji negatif yang harus gagal. Exit code 1 bila ada hasil yang tidak sesuai harapan. |
| `pwsh Printech-TestData/validasi.ps1`, lalu `python Printech-TestData/validasi.py` | Validasi garis potong, susunan, dan export pada data uji. Rinciannya di [panduan data uji](Printech-TestData/README.md). |
| `python -X utf8 docs/pengujian/buat-uji-alur-lengkap.py` | Membuat ulang dokumen uji alur lengkap dan lembar hasilnya (CSV) dari templat `uji-alur-lengkap-templat.md` dan daftar kasus uji di skrip itu. |
| `npx --yes markdownlint-cli2@0.23.2 "**/*.md"` | Memeriksa format semua file Markdown dengan aturan di `.markdownlint-cli2.jsonc`. |
| `StickerLayout\bin\Debug\net10.0-windows\StickerLayout.exe --selfcheck samples` | Pemeriksaan otomatis satu kali di folder `samples`. Hasilnya di `samples/selfcheck.txt`; exit code 0 = lulus. |

### Membuat zip untuk operator

```powershell
dotnet publish StickerLayout -c Release -r win-x64 --self-contained -o publish\Printech
Compress-Archive -Path publish\Printech -DestinationPath publish\Printech-win-x64.zip -Force
```

Hasilnya `publish\Printech-win-x64.zip`, berisi folder `Printech`. Folder `publish/` tidak ikut git, jadi zip tidak ikut ter-commit. Bagikan zip hanya di dalam perusahaan. BitmapToVector (Potrace) berlisensi GPL-3.0, jadi zip tidak boleh diunggah ke GitHub Release publik atau dibagikan ke luar.

### Struktur folder

| Lokasi | Isi |
| --- | --- |
| `StickerLayout/Core.cs` | Mesin utama: memuat desain, garis potong otomatis (gambar bantu, penghalusan, Potrace), susun otomatis, dan proses pembantu CorelDRAW |
| `StickerLayout/Curves.cs`, `Shapes.cs`, `SmartSelect.cs` | Titik garis potong (lurus/kurva), preset bentuk dan desain kotak, smart select |
| `StickerLayout/Export.cs`, `ExportSvg.cs`, `Pdfium.cs` | Export PDF, preview JPG, Print Ticket, export SVG; membuka PDF/AI |
| `StickerLayout/Cleanup.cs` | Rapikan gambar (hapus latar, penghapus klik, kikis tepi) |
| `StickerLayout/MainWindow.*`, `CutEditWindow.*`, `CleanupWindow.*`, `RulerControl.cs`, `Theme.xaml` | Tampilan: jendela utama, editor garis potong, jendela Rapikan, penggaris, tema |
| `StickerLayout/Strings.cs` | Semua teks tampilan dalam bahasa Indonesia dan English |
| `StickerLayout/SelfCheck.cs` | Pemeriksaan otomatis bawaan (`StickerLayout.exe --selfcheck <folder>`) |
| `samples/` | File contoh. Pemeriksaan otomatis juga menulis hasilnya di sini; hasil itu tidak ikut git |
| `Printech-TestData/` | Data uji dengan hasil benar, skrip validasi dan uji tampilan, serta bukti uji |
| `docs/` | Kebutuhan aplikasi, riset, skenario uji, uji alur lengkap (`docs/pengujian/`), dan spesifikasi fitur |

### Alur kerja tim

- **Repo tim:** [kntg/Layout-and-Editing-Software](https://github.com/kntg/Layout-and-Editing-Software). Repo ini **publik**. `main` harus selalu bisa di-build dan lulus pemeriksaan otomatis.
- **Branch:** kerjakan di branch sendiri (`feature/<nama>`, `fix/<nama>`).
- **Masuk ke `main`:** setelah pembuatnya mengaudit atau menguji perubahan, branch boleh langsung digabung ke `main` lalu di-push. Pakai pull request (permintaan penggabungan di GitHub) bila perubahan ingin diperiksa leader dulu.
- **Sebelum push:** jalankan `git fetch`, gabungkan `main` terbaru ke branch Anda (merge), lalu jalankan [cek sebelum push](#cek-sebelum-push).
- **Jangan `git push --force`** ke `main`, karena commit rekan lain bisa hilang.
- **Jangan commit:** file customer, data internal toko, atau kata sandi. File customer untuk uji disimpan di `Printech-TestData/01-input/customer/`; folder itu tidak ikut git.
- **Fitur yang mengubah perilaku:** perbarui juga [kebutuhan aplikasi](docs/kebutuhan-aplikasi.md), skenario uji, dan kasus uji alur lengkap.
- **Pesan commit:** bahasa Indonesia, satu baris ringkas, lalu penjelasan alasannya.
