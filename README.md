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
   - CDR dibuka tanpa CorelDRAW, lewat alat libcdr yang ikut di folder Printech. Warnanya menjadi RGB dan desainnya ditempel sebagai gambar, jadi periksa pratinjaunya sebelum cetak. Bila libcdr gagal, Printech memakai CorelDRAW yang terpasang. Jendela Printech tidak macet selama menunggu.
   - AI yang disimpan tanpa PDF-compatible ditolak dengan pesan.
   - Bila aplikasi dibuka tanpa file, muncul halaman sambutan: tarik file ke kotaknya atau klik **Buka Folder**.
2. **Atur desain** di panel **Desain**.
   - Setiap desain punya lebar artwork (mm) dan jumlah (pcs). Tombol **Kembalikan** mengembalikan jumlah ke 1 dan menyusun ulang.
   - **Lainnya** (ikon titik tiga) berisi Rapikan gambar dan Edit garis potong.
   - PDF/AI/CDR memakai ukuran aslinya. PNG/JPG/SVG memakai "Lebar awal" (bawaan 50 mm).
   - Angka dpi merah berarti resolusinya di bawah 150 dpi, dan hasil cetaknya akan pecah.
3. **Pilih kertas** di panel **Kertas**.
   - Pilihan: A3+ 320 × 480 mm (bawaan, kertas Develop), A0–A6, SRA3, SRA4, atau Custom (ukuran bebas). Ada juga tombol putar kertas.
   - Margin tepi (bawaan 10 mm) adalah area kosong untuk tanda registrasi dari Cutting Master 5 dan untuk printer. Margin yang pas untuk format tanda registrasi operator belum dipastikan.
4. **Atur garis potong** di panel **Garis potong**.
   - Kiss-cut: bawaan aktif, offset 2 mm, Halus 1 mm.
   - Die-cut: bawaan mati, offset 4 mm, Halus 2 mm.
   - Jarak antar stiker: 2 mm. Bleed gambar PNG/JPG: 0 = mati.
   - Perubahan lebar, jumlah, dan garis potong baru diterapkan setelah klik **Susun otomatis** (Ctrl+L). Mengganti atau memutar kertas langsung menyusun ulang. Lebar boleh memakai koma atau titik (62,5 atau 62.5).
5. **Periksa layout.**
   - **Seret stiker:** tahan Shift supaya stiker hanya bergeser mendatar atau tegak. Stiker pindah ke posisi kosong terdekat, di lembar lain bila dilepas di atasnya. Bila tidak ada tempat, stiker kembali ke posisi awal. Beberapa stiker yang terpilih bergerak bersama; bila satu saja tidak punya tempat, semuanya kembali. Tahan Alt saat melepas untuk menyalin: stiker asal tetap di tempatnya, dan salinannya ditaruh di titik lepas. Esc membatalkan tarikan. R memutar 90°, Delete menghapus, panah menggeser 1 mm (dengan Shift: 10 mm).
   - **Pilih banyak stiker:** Ctrl+klik menambah atau melepas stiker dari pilihan. Seret dari area kosong lembar membuat kotak pilih. Stiker terpilih bergerak, berputar, tersalin, dan terhapus bersama, juga bila berada di lembar berbeda. Titik pojok hanya muncul saat satu stiker terpilih.
   - **Stiker bertanda merah:** tumpang tindih dengan stiker lain, lebih dekat dari Jarak antar stiker, atau keluar margin. Selama luas stiker melebihi lembar karena bertumpuk, angka bahan tampil 100% dan merah; susun ulang untuk melihat angka sebenarnya.
   - **Atur ukuran di kanvas:** klik stiker, lalu tarik salah satu dari 4 titik di pojoknya. Lebar × tinggi artwork tampil di dekat kursor. Proporsi tetap selama **Kunci rasio** di panel Pilihan tercentang; lepas centangnya untuk mengubah lebar dan tinggi sendiri-sendiri (rasio bebas). Saat mouse dilepas, hanya stiker itu yang berubah, dan garis potong dihitung ulang. Bila desainnya juga dipakai stiker lain, stiker itu mendapat baris sendiri di daftar desain (nama sama, lebar baru), dan stiker lain tidak berubah. Kotak lebar dan tinggi di panel Pilihan bekerja sama untuk stiker yang terpilih. Garis potong, bleed, dan border selalu dihitung ulang dengan jarak mm yang sama. Stiker yang diubah diam di tempatnya; stiker yang ditimpanya pindah ke tempat kosong di lembar yang sama, atau ke lembar baru di sebelahnya bila lembar itu penuh, dengan ukurannya sendiri.
   - **Hemat atau Rapi:** pilihan di toolbar. Hemat (bawaan) memadatkan stiker dan boleh memutarnya, jadi bahannya paling hemat. Rapi menyusun stiker per baris dan kolom dengan tepi kiri rata dan desain yang sama berkumpul, tetapi bahan bisa lebih boros. Susun otomatis, tambah file, dan ganti kertas mengikuti pilihan ini.
   - **Penuhi lembar:** mengisi ruang kosong di semua lembar dengan salinan desain bergiliran, dan jumlah per desain ikut bertambah. Bila ada stiker terpilih, hanya desain stiker itu yang ditambah. Saat Rapi dipilih, semua stiker disusun ulang, jadi geseran manual hilang (bisa diurungkan).
   - **Duplikat kanvas:** tombol **Duplikat** di judul lembar menyalin lembar itu utuh, termasuk hasil edit, ke lembar baru di sampingnya. Lembar berjajar ke kanan dan turun ke bawah bila baris di layar penuh. Bahan bertambah 1 lembar, dan jumlah (pcs) tiap desain ikut bertambah. Cara keyboard: klik area kosong di kanvas, Ctrl+C, lalu Ctrl+V.
   - **Salin-tempel stiker:** klik stiker, Ctrl+C, lalu Ctrl+V. Salinan berada tepat di kanan stiker asli; bila baris penuh, turun ke baris berikutnya. Beberapa stiker terpilih tersalin bersama; bila satu saja tidak punya tempat, tidak ada yang ditempel. Susun otomatis tetap menyusun ulang semua stiker dari jumlah pcs, jadi hasil duplikat dan tempel hilang (bisa diurungkan).
   - **Urungkan / Ulangi:** Ctrl+Z / Ctrl+Y, sampai 30 langkah.
6. **Export.**
   - Tombol **Export…** (Ctrl+E, atau menu File) membuka dialog Export. Pilih format, isi file, lembar, dan tanda registrasi, lalu klik Export dan pilih lokasi simpan. Ctrl+Shift+E membuka dialog dengan JPG terpilih.
   - **PDF:** satu halaman per lembar. Artwork vektor tetap vektor. Garis kiss magenta C0 M100 Y0 K0, die cyan C100 M0 Y0 K0.
   - **SVG:** satu file per lembar dengan layer Artwork, Kiss Cut, Die Cut, dan tanda registrasi, untuk dibuka di CorelDRAW. Artwork di dalamnya berupa gambar dan warnanya RGB; export PDF tetap menjaga vektor dan CMYK.
   - **JPG preview:** untuk konfirmasi ke customer lewat WhatsApp. Bingkai merah tidak ikut di JPG.
   - **Print Ticket:** ringkasan satu halaman A4 untuk lampiran SPK, selalu untuk semua lembar.
   - **Isi file** (PDF dan SVG): Print + cutting, Print saja (untuk printer, tanpa garis potong), atau Cutting saja (garis potong saja).
   - **Lembar:** centang lembar yang diexport. File per lembar memakai nomor lembar aslinya, misalnya `nama-3.svg`.
   - **Tanda registrasi:** ikut ke file hanya bila dicentang (bawaan mati).
   - Export langsung ke CDR atau AI tidak ada: buka PDF atau SVG hasil Printech di CorelDRAW atau Illustrator, lalu simpan dari sana.
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

Buka lewat **Lainnya** (ikon titik tiga) di baris desain, lalu **Edit garis potong**. Hasilnya berlaku untuk semua salinan desain itu, tetapi tidak untuk baris ukuran lain dari file yang sama.

- **Bentuk.**
  - Pilihan: Otomatis, kotak, kotak sudut bulat, lingkaran, oval, segitiga, bintang, hati, segi enam, kotak bergelombang, lingkaran bergelombang.
  - Bentuk berlaku untuk garis yang dipilih di **Garis yang diedit**, jadi kiss dan die bisa berbeda. Contoh: pilih Kiss-cut lalu Bintang, kemudian Die-cut lalu Kotak.
  - Die Otomatis mengikuti bentuk kiss, kecuali di bawah kiss bergelombang: die-nya polos dan membungkus kiss. Die dengan bentuk lain membungkus garis kiss sejauh selisih offset.
  - Tepi bergelombang: lembah gelombang tetap sejauh offset dari artwork, puncaknya di tiap sudut, panjang gelombang 4–8 mm, dan tanpa lekukan lancip.
  - Bentuk mengelilingi artwork sejauh offset, dan ikut menyesuaikan bila offset atau lebar diubah, selama titiknya tidak diedit.
  - Nama bentuk tampil di daftar desain, misalnya "Bintang · die Kotak".
- **Warna border.**
  - Pilihan: tanpa warna, merah, hitam, biru, kuning, hijau, atau CMYK pilihan sendiri (empat angka persen, misalnya `0 100 100 0`, lalu Enter).
  - **Warna lain…** membuka dialog warna Windows. **Pipet**: centang, lalu klik di gambar stiker untuk memakai warnanya. Warna dari keduanya diubah ke CMYK, jadi hasil cetak bisa sedikit beda dari layar.
  - Area antara artwork dan garis kiss (tanpa kiss: garis die) dicetak dengan warna itu, diteruskan sejauh bleed di luar garis (bila bleed 0: 1 mm).
- **Garis yang diedit:** kiss-cut atau die-cut.
- **Simplify** (hanya untuk bentuk Otomatis): makin ke kanan (Sederhana), titik garisnya makin sedikit. Garis dibuat ulang dari garis otomatis.
- **Smart select.**
  - Centang, lalu klik objek: garis hanya mengikuti objek itu. Piksel tipis seperti watermark diabaikan; atur lewat slider Toleransi.
  - Shift+klik menambah objek. Klik di dalam lubang menambah atau menghapus garis dalamnya.
  - Garis smart select ikut offset dan Halus yang baru, selama titiknya tidak diedit sesudahnya.
- **Titik garis (node)**, diatur dengan mouse di kanvas.
  - Klik titik untuk memilihnya; klik saja tidak menggeser titik. Shift+klik menambah atau melepas titik dari pilihan.
  - Tahan Shift saat menyeret titik: garis ke titik tetangga lurus mendatar, tegak, atau 45 derajat.
  - Zoom: Ctrl + roda mouse (1×–8×); geser tampilan dengan tombol kanan mouse.
  - Seret titik yang dipilih untuk menggeser semuanya sejauh gerakan mouse.
  - Klik di garis atau kurva untuk menambah titik. Delete menghapus semua titik yang dipilih.
  - Bila tepat satu titik dipilih, pegangan kurva (handle, berupa titik bulat) tampil untuk mengatur lengkung.
  - Tombol **Jadikan kurva / Jadikan garis** dan **Node halus / Node sudut** berlaku untuk semua titik yang dipilih.
- **Urungkan / Ulangi** (Ctrl+Z / Ctrl+Y) dan **Reset otomatis**. Ganti warna border belum ikut urungkan di editor; Urungkan di jendela utama mencakupnya.
- **Simpan** nonaktif selama garis kiss keluar dari garis die.

### Rapikan gambar

- **Untuk:** desain berupa gambar, yaitu PNG, JPG, SVG, dan PDF yang ditempel sebagai gambar.
- **Cara membuka:** **Lainnya** (ikon titik tiga) di baris desain, lalu **Rapikan gambar**; atau klik dua kali gambar kecil desainnya.
- **Isi:** hapus latar otomatis (warna latar diambil dari tepi gambar), penghapus klik, crop kotak (centang, lalu tarik kotak: di luar kotak terhapus), toleransi warna, kikis tepi 0–3 piksel, lalu **Pakai**. Zoom dengan Ctrl + roda mouse.
- **Catatan:** file vektor (PDF/AI, dan CDR yang dibuka lewat CorelDRAW) dirapikan di CorelDRAW supaya warnanya tetap utuh. Selain crop kotak dan zoom (2026-11-08), fitur ini tidak dikembangkan lagi.

### Tampilan dan bahasa

- **Zoom:** Ctrl + roda mouse, Ctrl++ / Ctrl+-. Pas layar: Ctrl+0 atau F4. Saat diperbesar, jumlah lembar per baris tetap dan titik di bawah kursor tidak berpindah; lembar yang melebihi layar digeser. Saat diperkecil, jumlah lembar per baris bertambah bila muat. Pas layar, Susun otomatis, dan mengubah lebar jendela menata ulang.
- **Geser tampilan:** alat Hand (H), tahan Spasi, atau tombol tengah mouse.
- **Alat Zoom (Z):** klik untuk memperbesar, Alt+klik untuk memperkecil.
- **Penggaris (mm):** tampil di atas dan kiri lembar. Bisa disembunyikan lewat tombol penggaris di batang alat kiri.
- **Bahasa:** menu **Bahasa**, pilih Indonesia atau English.
- **Tanda registrasi:** tombol siku di batang alat kiri (di bawah penggaris), atau menu **Tampilan**, centang **Tanda registrasi** (bawaan: mati).
  - Pilihan itu menggambar siku L hitam di 4 pojok lembar di layar. Ke file export, siku hanya ikut bila **Tanda registrasi** dicentang di dialog Export.
  - Stiker tidak ditaruh dalam 6 mm dari tanda (area pindai menurut manual Graphtec). Stiker yang masuk area itu ditandai merah; klik Susun otomatis untuk memindahkannya.

## Shortcut

| Tombol | Fungsi |
| --- | --- |
| Ctrl+O, Ctrl+I | Tambah file |
| Ctrl+L | Susun otomatis |
| Ctrl+E | Dialog Export |
| Ctrl+Shift+E | Dialog Export dengan JPG terpilih |
| Ctrl+Z | Urungkan |
| Ctrl+Y, Ctrl+Shift+Z | Ulangi |
| Ctrl+0, F4 | Pas layar |
| Ctrl++, Ctrl+-, Ctrl + roda mouse | Zoom |
| V / H / Z | Alat Pilih / Geser tampilan / Zoom |
| Spasi (tahan), tombol tengah mouse | Geser tampilan sementara |
| R | Putar stiker yang dipilih 90° |
| Delete | Hapus stiker yang dipilih; jumlah pcs desainnya ikut turun (di editor: hapus titik yang dipilih) |
| Panah, Shift+panah | Geser stiker yang dipilih 1 mm / 10 mm |
| Ctrl+C, lalu Ctrl+V | Salin dan tempel semua stiker yang dipilih. Bila tidak ada stiker yang dipilih: duplikat kanvas yang terakhir diklik |
| Ctrl+D | Duplikat semua stiker yang dipilih di kanan atau bawahnya. Bila satu saja tidak punya tempat, tidak ada yang ditambahkan |
| Ctrl+klik | Tambah atau lepas stiker dari pilihan |
| Alt + seret stiker | Salin stiker ke titik lepas, juga ke lembar lain |
| Shift + seret stiker | Geser lurus mendatar atau tegak |
| Esc saat menarik | Batalkan tarikan, atur ukuran, atau kotak pilih |

## Arti pesan dan peringatan

| Pesan | Arti dan yang perlu dilakukan |
| --- | --- |
| "… lebih besar dari kertas" | Desain tidak muat di area cetak. Perkecil lebarnya, atau pilih kertas yang lebih besar. |
| "Tidak ada tempat di kanan atau bawah stiker ini…" | Salinan stiker tidak muat di lembar ini. Hapus stiker lain, atau pakai Duplikat di judul lembar untuk lembar baru. |
| "… tidak bisa dipakai. Isi angka minimal …" | Ada kotak angka yang salah ketik atau di bawah batasnya. Betulkan angkanya; kursor sudah ada di kotak itu. |
| "File tidak bisa disimpan…" | File tujuan sedang dibuka di program lain, disk penuh, atau folder tidak boleh ditulis. Tutup filenya atau pilih tempat lain, lalu coba lagi. |
| "… desain resolusi rendah" | Di bawah 150 dpi pada lebar itu. Minta file asli ke customer, atau perkecil desainnya. |
| "… PDF ditempel sebagai gambar (halaman diputar atau tidak terbaca)" | PDF dicetak sebagai gambar, bukan vektor. Periksa ketajaman dan warnanya sebelum cetak. |
| "… tumpang tindih/terlalu dekat/keluar margin" | Ada stiker bertanda merah. Geser stikernya atau klik Susun otomatis. Export masih bisa setelah konfirmasi. |
| "Tidak ada tempat…" | Stiker tidak muat di tujuan, jadi kembali ke posisi awal, atau putarannya dibatalkan. Hapus stiker lain, atau klik Susun otomatis. |
| "… stiker dipindah ke lembar baru (lembar …)" | Ukuran baru membuat lembar penuh, jadi stiker yang tertimpa pindah ke lembar baru. Bahan bertambah; Ctrl+Z membatalkannya. |
| "… diubah menjadi …, tetapi lebih besar dari area cetak lembar" | Stiker tidak muat di lembar kosong pun, jadi ditandai merah. Perkecil stikernya atau ganti ukuran kertas. |
| "CDR ini tidak bisa dibaca tanpa CorelDRAW…" | libcdr tidak bisa membaca file itu, dan CorelDRAW tidak terpasang. Export CDR ke PDF di PC yang ada CorelDRAW-nya, lalu tambahkan PDF-nya. |
| "… CDR ditempel sebagai gambar RGB…" | CDR dibuka tanpa CorelDRAW. Warna dan efeknya bisa beda dari CorelDRAW; periksa pratinjaunya sebelum cetak. |
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
- CorelDRAW tidak wajib. CDR dibuka lewat alat libcdr yang ikut di folder Printech; CorelDRAW hanya dipakai bila libcdr gagal membaca sebuah CDR.

Langkah:

1. Minta `Printech-win-x64.zip` ke pengembang (cara membuatnya ada di [Membuat zip untuk operator](#membuat-zip-untuk-operator)).
2. Klik kanan zip, pilih **Properties**, centang **Unblock**, lalu **OK**. Aplikasinya belum ditandatangani, jadi tanpa langkah ini Windows SmartScreen bisa memblokirnya.
3. Ekstrak, lalu jalankan `Printech\StickerLayout.exe`. Bila SmartScreen tetap muncul, pilih **More info**, lalu **Run anyway**.

## Dokumentasi

- [Kebutuhan aplikasi](docs/kebutuhan-aplikasi.md) (dulu bernama PRD): tujuan, fitur, keputusan, batasan, daftar uji untuk operator, dan cara menjalankan (§11).
- [Catatan riset](docs/RESEARCH.md): riset pendukung keputusan.
- [Skenario uji garis potong](docs/UJI-GARIS-POTONG.md): skenario S1–S23 dan hasilnya.
- [Skenario uji stabilitas](docs/UJI-STABILITAS.md): skenario P1–P10 (proses pembantu CDR, kerja paralel, memori) dan hasilnya.
- [Uji alur lengkap](docs/pengujian/uji-alur-lengkap.md): semua kasus uji dari file masuk sampai potong di Graphtec, dengan status PASS/FAIL/BLOCKED/NOT RUN dan buktinya. Lembar isian: [lembar hasil uji (CSV)](docs/pengujian/lembar-hasil-uji-alur-lengkap.csv).
- [Data uji](Printech-TestData/README.md): file uji dengan hasil benar yang sudah diketahui, skrip validasi, dan skrip uji tampilan.
- [Simulasi produksi](docs/simulasi/index.html): tampilan 3D satu lembar hasil validasi. Buka file ini di browser.
- [Spesifikasi](docs/superpowers/specs/) fitur baru (editor garis potong, smart select, bentuk garis potong) dan [rencana pembuatan](docs/superpowers/plans/) smart select.

## Untuk pengembang

### Kebutuhan

| Untuk | Yang dipasang |
| --- | --- |
| Build dan menjalankan aplikasi | Windows 10/11 64-bit, [Git](https://git-scm.com), [.NET 10 SDK](https://dotnet.microsoft.com/download) |
| Pemeriksaan otomatis dan validasi | PowerShell 7 (`pwsh`) |
| Uji tampilan (skrip `Printech-TestData/ui-*.ps1`) | Windows PowerShell 5.1 (bawaan Windows) |
| Validasi, pembuat dokumen uji alur lengkap, uji negatif pemeriksaan otomatis | Python 3 dengan PyMuPDF, Pillow, NumPy, dan SciPy: `pip install pymupdf pillow numpy scipy` |
| Pemeriksaan format dokumen Markdown | Node.js (`npx`) |
| Menguji jalur cadangan CDR lewat CorelDRAW | CorelDRAW terpasang |

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

- **Tampilan, editor, atau alur di jendela aplikasi:** cek dengan tangan bagian yang berubah, memakai [daftar cek tampilan](docs/kebutuhan-aplikasi.md#cek-tampilan-setelah-perubahan). Skrip yang memakai mouse dan keyboard sungguhan, misalnya uji tata letak (`ui-edit-layout.ps1`), tidak wajib lagi (keputusan 2026-11-05), karena PC tidak boleh dipakai selama skrip berjalan. Skripnya tetap ada di [panduan data uji](Printech-TestData/README.md).
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
| `StickerLayout/Core.cs` | Mesin utama: memuat desain, garis potong otomatis (gambar bantu, penghalusan, Potrace), susun otomatis, pembaca CDR lewat libcdr, dan proses pembantu CorelDRAW |
| `StickerLayout/libcdr/` | Alat libcdr dari MSYS2 untuk membuka CDR tanpa CorelDRAW, beserta DLL dan teks lisensinya. Asal, versi, dan sidik SHA256-nya ada di `SUMBER.txt` |
| `StickerLayout/Curves.cs`, `Shapes.cs`, `SmartSelect.cs` | Titik garis potong (lurus/kurva), preset bentuk dan desain kotak, smart select |
| `StickerLayout/Export.cs`, `ExportSvg.cs`, `ExportWindow.xaml(.cs)`, `Pdfium.cs` | Export PDF, preview JPG, Print Ticket, export SVG, dialog Export; membuka PDF/AI |
| `StickerLayout/Cleanup.cs` | Rapikan gambar (hapus latar, penghapus klik, kikis tepi) |
| `StickerLayout/MainWindow.*`, `CutEditWindow.*`, `CleanupWindow.*`, `RulerControl.cs`, `Theme.xaml` | Tampilan: jendela utama, editor garis potong, jendela Rapikan, penggaris, tema |
| `StickerLayout/Strings.cs` | Teks tampilan dalam bahasa Indonesia dan English |
| `StickerLayout/SelfCheck.cs` | Pemeriksaan otomatis bawaan (`StickerLayout.exe --selfcheck <folder>`) |
| `samples/` | File contoh, termasuk `cdr-libreoffice-fdo65220.cdr` (contoh uji CDR dari repo LibreOffice, MPL-2.0). Pemeriksaan otomatis juga menulis hasilnya di sini; hasil itu tidak ikut git |
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
