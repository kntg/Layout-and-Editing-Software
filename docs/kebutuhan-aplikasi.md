# Kebutuhan aplikasi Printech (layout stiker print & cut)

Status: Draf 4 · Tanggal: 2026-10-17 · Nama sementara: "Printech" (dikonfirmasi 2026-10-13). Dokumen ini dulu bernama PRD (Product Requirements Document).

## 1. Tujuan

Ini alat internal toko. Operator mengubah file dari customer menjadi layout stiker yang siap dicetak dan dipotong. Layout harus hemat bahan dan warnanya akurat.

Tahap sekarang: prototipe, lalu cek kualitas, lalu uji oleh operator. Status siap pakai (production) tercapai bila tidak ada error lagi.

**Cakupan prototipe** (2026-10-17): automasi layout saja, yaitu memasukkan file, garis potong otomatis, susun otomatis, dan export PDF/preview JPG. Estimasi pengerjaan 2 minggu. Dokumentasi disimpan di repo GitHub ini.

**Tujuan utama: mempercepat proses** (konfirmasi 2026-10-14). Saat ini Photoshop dipakai hanya untuk merapikan gambar, bukan untuk layout, dan pekerjaan itu memakan banyak waktu. Sejak 2026-11-06 (keputusan user, disetujui leader), Printech juga ditujukan untuk persiapan produksi tanpa CorelDRAW dan Illustrator: membuka file customer, layout, garis potong, dan export. Membuat atau mengedit artwork tetap di aplikasi desain, dan pengiriman pekerjaan ke printer dan mesin potong tetap di luar cakupan.

**Cakupan:** stiker saja (kiss-cut dan die-cut). Nametag, kartu, dan dokumen tidak termasuk.

## 2. Pengguna

- Ada dua peran: operator layout dan operator mesin. Di kantor pusat keduanya dipegang orang berbeda; di cabang bisa dipegang orang yang sama.
- Aplikasi dipakai di dalam perusahaan. Ini terhitung pemakaian komersial, tetapi aplikasi tidak dibagikan ke luar perusahaan.

## 3. Pengamatan di AP 55 (presentasi leader proyek, 2026-10-14)

- Cabang AP 55 (Angkasa Putra). Tiga operator memegang semua mesin. Mesin cetak stiker: printer laser **Develop**; mesin potong: **Graphtec**.
- Alur stiker: file masuk lewat WhatsApp, email, atau flashdisk; operator mengecek file; layout garis potong dan posisi cetak dibuat di CorelDRAW; dicetak di Develop; lalu di-kiss-cut di Graphtec.
- Operator mengirim tampilan file ke customer untuk dikonfirmasi ("apakah sesuai?") sebelum bon dicetak.
- Photoshop dipakai untuk merapikan gambar. File AI diubah dulu ke PDF, lalu dilanjutkan di CorelDRAW.
- Produk teratas: stiker vinyl kiss-cut (offline) dan stiker dalam jumlah besar (online).
- Kertas stiker di Develop: A3+ 32×48 cm.

## 4. Alur di Printech

1. Customer menyerahkan file: 80% CorelDRAW dan PDF, 20% AI dan PNG/JPG. SVG jarang. Barang fisik belum diterima untuk dipotong.
2. Operator menambahkan file. Aplikasi membuat garis kiss-cut dan die-cut secara otomatis.
3. Operator mengatur lebar dan jumlah per desain, ukuran kertas, dan margin tepi, lalu menjalankan "Susun otomatis". Bila satu lembar penuh, lembar berikutnya dibuat otomatis.
4. Operator mengedit hasil susunan bila perlu, lalu export ke PDF.
5. PDF dibuka di CorelDRAW atau Illustrator. Tanda registrasi (reg mark) dan pengiriman ke mesin potong dilakukan lewat Cutting Master 5 (Graphtec), dengan garis potong dipilih berdasarkan warna.

## 5. Kebutuhan fungsional

| Kode | Kebutuhan | Status (versi 2) |
| ---- | ----------- | ----------- |
| F1 | File yang bisa dimasukkan: PDF, AI (yang disimpan PDF-compatible), CDR, PNG, JPG, dan SVG. PDF beberapa halaman = satu desain per halaman. CDR dibuka tanpa CorelDRAW lewat alat libcdr yang ikut di folder Printech (sejak 2026-11-06): satu desain per halaman, ukuran asli, ditempel sebagai gambar RGB ±300 dpi. Bila libcdr gagal, CDR dibuka lewat CorelDRAW yang terpasang, di proses pembantu terpisah yang dihentikan setelah 30 detik bila CorelDRAW tidak merespons. AI yang disimpan tanpa PDF-compatible ditolak dengan pesan untuk menyimpan ulang (2026-11-06) | Ada. CDR lewat libcdr diuji dengan contoh dari uji LibreOffice |
| F2 | Gambar otomatis dipangkas sampai batas artwork (pinggiran transparan dibuang). Latar gambar tanpa bagian transparan tidak dibuang otomatis. PDF memakai ukuran aslinya. Lebar dan jumlah bisa diatur per desain | Ada |
| F3 | Garis kiss-cut dan die-cut, masing-masing dengan offset dan Halus (penghalus) sendiri. Lubang asli artwork yang tetap terbuka setelah offset (stiker donat) mendapat garis dalam pada kiss dan die (2026-10-21) | Ada |
| F4 | Kertas: pilihan A0–A6, **A3+ 320×480 (bawaan)**, SRA3, SRA4, ukuran bebas (custom), dan tombol putar orientasi | Ada |
| F5 | Margin tepi kertas bisa diatur (area kosong untuk tanda registrasi dan printer) | Ada |
| F6 | Susun otomatis ke banyak lembar; lembar baru dibuat otomatis | Ada |
| F7 | Edit hasil susunan: seret, putar 90°, hapus, tombol Delete/R/panah. Muncul peringatan bila stiker tumpang tindih, lebih dekat dari jarak antar stiker, atau keluar margin. Menghapus stiker menurunkan jumlah pcs desainnya, jadi Susun otomatis tidak mengembalikannya; bila semua salinan dihapus, jumlahnya 0 dan desain tetap di daftar (2026-11-05). Tahan Shift saat menyeret: stiker hanya bergeser mendatar atau tegak, mengikuti arah gerak yang lebih besar (2026-11-08). Saat dibawa ke lembar lain, lembar tujuan dan posisi lepasnya mengikuti stiker yang terlihat, bukan kursor (perbaikan 2026-11-09) | Ada |
| F8 | Perbesar/perkecil tampilan (zoom) seperti di Word: Ctrl+roda mouse (titik di bawah kursor tetap di tempat), persen, tombol "Pas layar". Alat Hand/Zoom, tahan Spasi, atau tombol tengah mouse untuk menggeser tampilan | Ada |
| F9 | Export PDF: artwork tetap vektor dengan warna asli. Kiss-cut = magenta 100%, die-cut = cyan 100%, warna CMYK murni (DeviceCMYK; tanpa RGB/sRGB atau profil warna ICC; dicek otomatis di skenario uji S8) | Ada |
| F10 | Bahasa tampilan: Indonesia dan English | Ada |
| F11 | Tampilan gaya Photoshop: tema gelap, menu, bilah pilihan (options bar), batang alat, panel lipat (Desain dengan gambar kecil, Pilihan, Kertas, Garis potong), baris status dengan meter bahan dan peringatan, tarik-dan-lepas file (drag & drop), file langsung disusun saat ditambahkan | Ada |
| F12 | Resolusi cetak (dpi) tiap desain yang ditempel sebagai gambar. Merah dan masuk peringatan bila di bawah 150 dpi (target 300 dpi). Gunanya menangkap foto terkompres dari WhatsApp sebelum dicetak | Ada |
| F13 | Export preview JPG (dialog Export, Ctrl+Shift+E memilih JPG): satu JPG per lembar, sisi panjang ±2000 piksel, untuk konfirmasi ke customer lewat WhatsApp. Bingkai merah tidak ikut, dan bila ada stiker bertanda merah Printech bertanya dulu (2026-11-05) | Ada |
| F14 | Shortcut seperti CorelDRAW: Ctrl+I = tambah file, F4 / Shift+F4 = pas layar (selain Ctrl+O dan Ctrl+0) | Ada |
| F15 | Rapikan gambar (pengganti langkah Photoshop), untuk desain yang ditempel sebagai gambar: hapus latar otomatis, penghapus klik, toleransi warna, kikis tepi 0–3 piksel, urungkan dan kembalikan. Dibuka lewat menu Lainnya di baris desain atau klik dua kali pada gambar kecil desain. Garis potong dihitung ulang dari hasilnya. Crop kotak sejak 2026-11-08 (permintaan user): centang Crop kotak, tarik kotak di gambar, dan bagian di luarnya dihapus, bisa diurungkan. Zoom Ctrl + roda mouse 1×–8× dan geser dengan tombol kanan (dibuat oleh Jonathan, 2026-11-08) | Ada. Di luar cakupan prototipe, kecuali crop kotak dan zoom (2026-11-08) |
| F16 | Garis potong dengan sedikit titik (node) dan kurva mulus untuk pisau Graphtec: tanpa sudut tajam, kecuali desain kotak (F25) dan preset bentuk (F26); isian "Halus" sekarang juga menghaluskan bentuk sehingga titiknya berkurang (lihat Keputusan) | Ada |
| F17 | Bleed untuk gambar PNG/JPG (pilihan, 0 = mati): warna tepi diperluas sampai sejauh nilai bleed (mm) di luar garis kiss, supaya pergeseran pisau tidak menyisakan tepi putih. Paling besar sama dengan jarak antar stiker. Bagian artwork yang semi-transparan (bayangan, tepi foto yang memudar) tetap tembus, karena warna perluasan hanya diambil dari piksel yang tidak transparan. Warna tepi diperluas sebagai rata-rata halus dari beberapa skala, jadi tepi bertekstur tidak membentuk garis (2026-10-28) | Ada |
| F18 | Indikator bahan: "Bahan terpakai" dihitung terhadap area kertas di dalam margin, berwarna hijau/kuning/merah menurut target bentuk teratur (70–85%, hijau ≥ 75%) atau bentuk tidak teratur (55–70%, hijau ≥ 60%). Jumlah per lembar ("24 pcs / lembar A3+") dan sisa bahan | Ada |
| F19 | Tempel otomatis (snap magnet): stiker yang digeser atau diputar pindah ke posisi kosong terdekat (tidak bertabrakan, jarak antar stiker tetap). Bila tidak ada tempat, stiker yang digeser kembali ke posisi awal, putaran dibatalkan, dan muncul pesan. Pilihan bahan siap pakai (preset bahan) dihapus 2026-10-23 (tidak diperlukan, masukan user/leader) | Ada |
| F20 | Print Ticket: PDF A4 1 halaman berisi total lembar, total pcs, efisiensi bahan, rincian per lembar dan per desain, serta pengaturan potong, untuk lampiran nota kasir atau SPK | Ada |
| F22 | Urungkan/ulangi (undo/redo) untuk seluruh aplikasi: Ctrl+Z, Ctrl+Y, Ctrl+Shift+Z, dan menu Edit. Mencakup tambah/hapus desain, susun, penuhi lembar, geser, putar, hapus stiker, Rapikan, editor garis potong, ganti kertas, dan tanda registrasi. Paling banyak 30 langkah. Di kotak teks, Ctrl+Z tetap mengurungkan ketikan. Sesudah Urungkan atau Ulangi tidak ada stiker yang terpilih, karena stiker dibuat ulang dari catatan langkah (dicek 2026-11-06). Susun otomatis menimpa edit manual tanpa konfirmasi, karena bisa diurungkan (keputusan user 2026-11-02) | Ada (2026-10-21) |
| F21 | Penuhi lembar (tombol yang dulu bernama Compact): ruang kosong di semua lembar diisi salinan desain bergiliran sampai penuh, karena customer membayar per lembar. Bila ada stiker terpilih, hanya desain stiker itu yang ditambah, di Hemat maupun Rapi (keputusan user 2026-11-08); tanpa pilihan, semua desain bergiliran | Ada (2026-10-21) |
| F23 | Editor garis potong (menu Lainnya di daftar desain): garis kiss dan die menjadi titik yang bisa digeser, ditambah (klik di garis atau kurva, bentuk tidak berubah), dan dihapus (Delete). Sejak 2026-10-23 seperti CorelDRAW: ruas lurus atau kurva Bezier; titik yang dipilih menampilkan pegangan (handle) untuk mengatur lengkung; tombol Jadikan kurva/Jadikan garis dan Node halus/Node sudut. Slider Simplify (toleransi 0,05–0,5 mm, Kompleks–Sederhana), urungkan/ulangi di editor, dan Reset otomatis. Berlaku untuk semua salinan desain; daftar desain menandai "garis manual". Sejak 2026-10-26 (temuan leader): klik titik hanya memilih (titik tidak melompat ke kursor; seret baru dimulai setelah mouse bergeser ±3,5 piksel, dan menggeser titik sejauh gerakan mouse); Shift+klik memilih beberapa titik, lalu seret, Delete, dan tombol titik berlaku untuk semua titik yang dipilih. Tahan Shift saat menyeret titik: garis ke titik tetangga lurus mendatar, tegak, atau 45 derajat, dari tetangga yang arahnya paling dekat ke kursor (2026-11-08). Zoom Ctrl + roda mouse 1×–8× dan geser dengan tombol kanan (dibuat oleh Jonathan, 2026-11-08) | Ada (2026-10-21, kurva 2026-10-23, pilih banyak 2026-10-26) |
| F24 | Smart select di editor garis potong (kotak centang di panel kiri): klik objek, lalu garis kiss dan die mengikuti bentuknya dengan jarak offset (lekukan diikuti, sudut luar membulat); Shift+klik menambah objek; klik di dalam lubang menambah atau menghapus garis dalam, termasuk lubang yang lebih sempit dari offset; slider Toleransi mengabaikan piksel tipis seperti watermark. Tanpa AI. Sejak 2026-11-08 (keputusan user) garis hasil smart select ikut offset dan Halus yang baru: klik, toleransi, dan Simplify disimpan bersama desain, lalu garisnya dihitung ulang saat garis potong dimuat ulang, selama titiknya tidak diedit sesudah smart select. Membuka editor lagi membawa klik itu | Ada (2026-10-22) |
| F25 | Desain kotak otomatis: foto, JPG/PNG tanpa bagian transparan, PNG kotak, dan PDF berlatar kotak putih mendapat garis kotak bersudut tajam (4 titik dengan garis lurus) sejauh offset, tanpa operator memilih apa pun | Ada (2026-10-23) |
| F26 | Preset bentuk garis potong di editor: kotak, kotak sudut bulat, lingkaran, oval, segitiga, bintang, hati, segi enam, dan sejak 2026-11-08 kotak bergelombang dan lingkaran bergelombang (§6). Bentuk mengelilingi artwork sejauh offset (artwork tidak terpotong), die memakai bentuk yang sama, dan bentuk ikut menyesuaikan bila offset atau lebar diubah. Daftar desain menampilkan nama bentuk. Sejak 2026-10-26 (temuan leader) bentuk dipilih per garis lewat "Garis yang diedit": kiss dan die bisa berbeda, misalnya kiss bintang dan die kotak ("Bintang · die Kotak"). Die Otomatis mengikuti bentuk kiss, kecuali di bawah kiss bergelombang: die-nya polos; die dengan bentuk lain membungkus garis kiss sejauh selisih offset (bila kiss otomatis: sejauh offset die dari artwork) | Ada (2026-10-23, per garis 2026-10-26, bergelombang 2026-11-08) |
| F27 | Warna border per desain (editor garis potong): area antara artwork dan garis kiss dicetak dengan warna CMYK pilihan (merah, hitam, biru, kuning, hijau, CMYK pilihan sendiri, dialog warna Windows lewat tombol Warna lain…, atau pipet dari artwork sejak 2026-11-08), diteruskan sejauh bleed (bila bleed 0: 1 mm) di luar garis. Tampil di layar, preview JPG, dan PDF | Ada (2026-10-23) |
| F28 | Tanda registrasi (menu Tampilan, atau tombol di batang alat kiri sejak 2026-11-08; di file export hanya bila dicentang di dialog Export, bawaan mati sejak 2026-11-08; dibuat oleh Jonathan; bawaan mati sejak 2026-11-03, perubahan dari Yansen, sebelumnya aktif): siku L hitam (K 100%) di 4 pojok, 1 mm di luar area cetak, tampil di layar, preview JPG, dan PDF. Panjang 10 mm. Tebal 1 mm sejak 2026-10-28 (perubahan dari leader; sebelumnya 0,5 mm), dan kedua garis siku tersambung di sudutnya. Susun otomatis, Penuhi lembar, dan tempel otomatis tidak menaruh stiker dalam 6 mm dari garis tanda; stiker yang masuk area itu ditandai merah (temuan leader 2026-10-26: stiker kena tanda). Nilai dari Graphtec CE7000 User's Manual bab 5: tebal garis 0,3–1,0 mm, ukuran 5–20 mm, area pindai a = 6 mm tidak boleh dicetak | Ada (2026-10-26; tebal 1 mm sejak 2026-10-28) |
| F29 | Export SVG (dialog Export, dibuat oleh Yansen): satu file per lembar dengan layer Artwork, Kiss Cut, Die Cut, dan Registration Marks (bila tanda registrasi aktif). Artwork ditempel sebagai gambar di posisi dan ukuran yang sama dengan export PDF, dibatasi garis potong, dan warna border ikut. Desain PDF/AI memakai gambar tampilan layar | Ada (2026-10-29). Diperbaiki 2026-10-31: artwork PDF/AI tidak lagi kosong, gambar bermargin transparan tidak lagi mengecil, JPG ditandai JPEG, artwork dibatasi garis potong, dan warna border ikut |
| F30 | Pilihan susun Hemat dan Rapi di toolbar (permintaan user). Hemat adalah bawaan dan memakai susun padat; bila tanda registrasi aktif, ada satu percobaan tambahan dengan baris pertama turun melewati area tanda, yang dipakai hanya bila lebih hemat (2026-11-03). Rapi: stiker tersusun per baris dan kolom dari kiri atas, desain sama berkumpul, desain tertinggi dulu, dan tidak diputar kecuali hanya muat bila diputar. Desain yang cukup untuk satu baris penuh mulai di baris baru; yang sedikit mengisi sisa baris dan rata tengah secara vertikal. Margin, bleed, dan area tanda registrasi tetap dipatuhi. Baris mulai dari tepi kiri dan turun bila pojoknya kena area tanda; bila cara ini menambah lembar, baris boleh mulai setelah area tanda, satu kotak penuh dilewati supaya kolom tetap lurus (2026-11-03). Tiap stiker memakai kotak penuh, jadi bahan lebih boros pada bentuk tidak beraturan. Saat Rapi dipilih, Penuhi lembar menambah salinan bergiliran lalu menyusun ulang selama jumlah lembar tidak bertambah, jadi geseran manual hilang (bisa diurungkan) | Ada (2026-11-02, dua pilihan 2026-11-03) |
| F31 | Duplikat kanvas (tombol Duplikat di judul lembar, atau klik area kosong kanvas lalu Ctrl+C dan Ctrl+V; permintaan user): lembar disalin utuh, termasuk posisi, rotasi, dan hasil edit, ke tepat setelah lembar asal. Lembar di layar berjajar ke kanan dan turun ke bawah bila baris penuh. Jumlah pcs tiap desain ikut bertambah, dan bahan bertambah 1 lembar. Bisa diurungkan. Susun otomatis tetap menyusun ulang semua stiker dari jumlah pcs, jadi lembar hasil duplikat ikut hilang | Ada (2026-11-02) |
| F32 | Salin-tempel stiker (klik stiker, lalu Ctrl+C dan Ctrl+V atau menu Edit; permintaan user): salinan berada tepat di kanan stiker asli pada baris yang sama, dengan rotasi sama dan jarak antar stiker yang berlaku. Baris penuh turun ke baris berikutnya, mulai dari kolom paling kiri yang segaris. Posisi yang terisi atau kena area tanda registrasi dilewati satu kotak penuh. Bila tidak ada tempat di kanan atau bawah, tidak ada yang ditambahkan dan muncul pesan, supaya bahan tidak bertambah tanpa disadari. Beberapa stiker terpilih tersalin bersama: semua ditempel, atau tidak sama sekali bila satu saja tidak punya tempat. Berlaku di dalam Printech saja, bukan lewat clipboard Windows | Ada (2026-11-02) |
| F33 | Atur ukuran di kanvas (permintaan user): stiker terpilih punya 4 titik di pojoknya. Saat titik ditarik, pojok seberangnya diam, dan lebar × tinggi artwork tampil di dekat kursor. Proporsi tetap selama Kunci rasio di panel Pilihan tercentang (bawaan); bila dilepas, sisi mendatar dan tegak mengikuti tarikannya sendiri-sendiri (rasio bebas, sejak 2026-11-07, lihat §6 "Rasio bebas"). Saat mouse dilepas, ukuran hanya berlaku untuk stiker itu (sejak 2026-11-07, lihat §6 "Ukuran per stiker"), dan garis potong dihitung ulang dengan offset, Halus, dan bleed yang sama. Bila desainnya masih dipakai stiker lain, stiker itu menjadi baris baru di panel Desain. Kotak lebar dan tinggi di panel Pilihan bekerja sama untuk stiker yang terpilih, juga beberapa sekaligus, dengan pojok kiri-atas tetap. Bisa diurungkan. Sisi artwork paling kecil 5 mm, kecuali memang sudah lebih kecil; kotak stiker paling besar area cetak (dengan rasio bebas: per sisi). Stiker yang diubah pindah ke posisi kosong terdekat bila menabrak. Bila tidak ada posisi kosong, ukuran baru tetap berlaku, stiker dijaga di dalam area cetak dan ditandai merah, dan muncul pesan untuk memakai Susun otomatis | Ada (2026-11-03; per stiker dan rasio bebas 2026-11-07) |
| F34 | Pindah stiker antar-lembar (permintaan user): selama diseret, stiker tetap terlihat di luar lembarnya dan di atas lembar lain. Dilepas di atas lembar lain: stiker pindah ke posisi kosong terdekat di lembar itu dengan rotasi yang sama. Lembar tujuan tidak ada tempat: stiker kembali ke posisi awal dan muncul pesan. Lembar asal yang jadi kosong hilang, seperti saat menghapus stiker terakhirnya. Dilepas di luar semua lembar: posisi kosong terdekat di lembar asal. Satu Ctrl+Z membatalkan semuanya. Selama mouse ditahan, shortcut keyboard diabaikan; bila tarikan terputus (misalnya Alt+Tab), tarikan batal dan stiker kembali ke posisi awal. Beberapa stiker terpilih bergerak bersama: bila satu saja tidak punya tempat, semuanya kembali ke posisi awal. Posisi lepas dihitung dari kursor di lembar tujuan pada semua zoom dan posisi gulir, termasuk bila tampilan digulir selagi menarik (2026-11-05) | Ada (2026-11-03) |
| F35 | Pilih banyak stiker (dibuat oleh Jonathan): Ctrl+klik menambah atau melepas stiker dari pilihan, dan seret dari area kosong membuat kotak pilih pada lembar itu. Stiker terpilih bergerak, berputar, tersalin, dan terhapus bersama, juga bila berada di lembar berbeda, dan tiap stiker diperiksa di lembarnya sendiri. Titik pojok ukuran hanya muncul saat satu stiker terpilih | Ada (2026-11-03) |
| F36 | Halaman sambutan (dibuat oleh Yansen): tampil bila aplikasi dibuka tanpa file. File bisa ditarik ke kotaknya atau dipilih lewat Buka Folder. Panel kirinya (File Baru, File Terbaru, Pengaturan, dan Bantuan) disembunyikan sampai tombolnya berfungsi, karena baru menampilkan pesan sementara (keputusan user 2026-11-04) | Sebagian (2026-11-03) |
| F37 | Tombol Kembalikan dan menu Lainnya di baris desain (dibuat oleh Yansen): Kembalikan mengembalikan jumlah ke 1 lalu menyusun ulang; Lainnya berisi Rapikan gambar dan Edit garis potong. Rapikan gambar mati untuk desain vektor (PDF/AI/CDR), dan tooltipnya menjelaskan alasannya | Ada (2026-11-04) |
| F38 | Salin dengan Alt + seret dan batal dengan Esc (permintaan user): stiker yang dilepas sambil menahan Alt tetap di tempatnya, dan salinannya ditaruh di posisi kosong terdekat dari titik lepas, juga di lembar lain. Dilepas di celah antar-lembar: salinan masuk lembar asal. Beberapa stiker: semua atau tidak sama sekali. Jumlah (pcs) ikut naik, salinannya terpilih, dan satu tarikan adalah satu langkah urungkan. Esc selama menarik, mengatur ukuran, atau membuat kotak pilih membatalkannya tanpa langkah urungkan. Selama menarik, baris status menyebut mode dan lembar tujuan. Isi lembar dan posisi stiker baru berubah saat dilepas. Alt berarti salin hanya selagi stiker ditarik dengan alat Pindah; dengan alat Zoom, Alt + klik tetap memperkecil tampilan. Bila Alt dilepas di jendela lain (Alt+Tab), Alt berikutnya di Printech tetap membuka menu seperti biasa (2026-11-06) | Ada (2026-11-05) |
| F39 | Duplikat stiker dengan Ctrl+D (dibuat oleh Jonathan): salinan stiker terpilih berada di kanan atau bawahnya, dengan aturan yang sama dengan tempel. Stiker asal tidak berubah, salinannya terpilih, dan satu Ctrl+D adalah satu langkah urungkan. Beberapa stiker terpilih ikut semua; bila satu saja tidak punya tempat, tidak ada yang ditambahkan dan muncul pesan | Ada (2026-11-05). Beberapa stiker belum diuji dengan mouse |
| F40 | Dialog Export (daftar tugas leader, keputusan user 2026-11-08): satu tombol Export (toolbar, Ctrl+E, dan satu-satunya export di menu File) membuka dialog; tidak ada file yang langsung dibuat. Dialog memilih format (PDF, SVG per layer, JPG preview, Print Ticket), isi file untuk PDF dan SVG (Print + cutting, Print saja tanpa garis potong, Cutting saja tanpa artwork), lembar yang diexport (kotak centang per lembar dengan jumlah pcs), dan tanda registrasi (bawaan mati). File per lembar memakai nomor lembar aslinya, misalnya nama-3.svg. Peringatan stiker merah hanya untuk lembar yang diexport. Print Ticket selalu meringkas semua lembar. Ctrl+Shift+E membuka dialog dengan JPG terpilih | Ada (2026-11-08) |

## 6. Keputusan

- **Platform:** Windows; dibuat dengan C# dan .NET 10 (WPF).
- **Tanda registrasi (reg mark):** awalnya tidak dibuat oleh aplikasi; tanda dibuat oleh Cutting Master 5 di CorelDRAW/Illustrator, yang mendukung mesin FCX2000 dan CE7000. Sejak 2026-10-26 aplikasi bisa menggambar tanda registrasi sendiri (F28, menu Tanda registrasi; pilih "Tidak ada" bila tanda tetap dibuat lewat Cutting Master 5). Margin tepi kertas (bawaan 10 mm, belum dipastikan) tetap berlaku. Jarak tanda ke tepi kertas belum disesuaikan dengan mesin: manual CE7000 untuk kertas lembar menyebut minimal 15 mm (sisi belakang 35 mm), tergantung model dan arah masuk kertas.
- **Batas aplikasi** (keputusan leader proyek, 2026-10-14): aplikasi hanya membuat layout. Pengaturan dan urusan mesin tetap dikerjakan operator; profil mesin dihapus dari aplikasi.
- **Cutting Master 5:** bisa mengatur kondisi potong, urutan, dan potong/tidak per warna atau per layer.
- **Export:** PDF menggantikan SVG dari versi 1 sebagai export utama. Alasannya: vektor dan CMYK tetap terjaga, dan satu file bisa berisi banyak halaman. Sejak 2026-10-29 export SVG ditambahkan lagi oleh leader (F29) dengan layer terpisah; artwork di dalamnya berupa gambar RGB (lihat bagian 8).
- **Susun otomatis (nesting):** stiker ditaruh satu per satu di tempat kosong pertama yang muat (greedy first-fit), pada kotak bantu (grid) 1 mm. Margin dicek terhadap garis potong terluar, bukan kotak stiker. Diukur 2026-10-17, dibanding grid 2 mm dan margin per kotak:
  - pekerjaan 130 stiker A4: 7 lembar (sebelumnya 8), pemakaian bahan 51,6% (sebelumnya 45,2%), susun ±85 ms (versi rilis);
  - jarak nyata ke stiker terdekat (diminta 2 mm): median 3,2 mm, paling jauh 4,4 mm (sebelumnya 4,1 dan 5,5 mm), tetap 0 tabrakan;
  - jarak garis die ke tepi kertas (margin 10 mm): minimal 10,5 mm (sebelumnya 12,5 mm).
- **Ringan:** tampilan digambar oleh prosesor (CPU), bukan kartu grafis (GPU). Diukur 2026-10-17:
  - Memori menurut Task Manager (private working set): 35 MB tanpa file, 52–57 MB dengan 4 desain. Target spesifikasi ±120 MB tercapai.
  - Total working set 104–153 MB. Angka ini ikut menghitung file sistem Windows (DLL) yang dipakai bersama, termasuk driver grafis Intel ±70 MB yang tetap dimuat walau tampilan digambar lewat CPU. Laporan sebelumnya (136–153 MB, "di atas target") memakai angka total ini.
  - Pemakaian prosesor saat diam 0%.
  - Versi untuk operator dikompilasi dengan ReadyToRun, sehingga kerja prosesor saat aplikasi dibuka tinggal ±setengahnya.
  - Ganti zoom memakan 0,1–0,4 detik kerja prosesor. Layar memakai salinan gambar paling besar 1600 piksel (export tetap memakai resolusi asli): dengan satu gambar 4000×3000 piksel, memori 186 MB; saat jendela Rapikan dibuka 310 MB, kembali ke 267 MB setelah ditutup. Hapus latar 4000×3000 piksel: 0,5 detik.
  - File besar (temuan D4, diperbaiki 2026-10-21): salinan untuk layar selalu berdiri sendiri, dan setiap selesai menyusun, aplikasi membersihkan memori sepenuhnya (GC penuh, ±40 ms) supaya gambar resolusi penuh hasil muat langsung dilepas. Memori menurut Task Manager: JPG 24 MP 226 → 41 MB, dua PDF besar 326 → 53 MB, semua 19 file uji 279 → 84 MB. Puncak sesaat saat memuat file besar tetap tinggi (JPG 24 MP ±200 MB), karena gambar dibuka dalam resolusi penuh.
- **Aman:** tanpa koneksi jaringan. File asli customer hanya dibaca. Yang ditulis hanya file export (PDF, Print Ticket, preview JPG, dan SVG; lokasinya dipilih operator) dan folder sementara milik aplikasi (salinan PDF/AI dan hasil konversi CDR), yang dihapus saat aplikasi ditutup. Bila aplikasi dihentikan paksa, folder itu tertinggal di `%TEMP%\Printech`. Error yang tidak terduga tampil sebagai pesan, dan aplikasi tetap jalan. Pembaca PDF yang dipakai untuk file customer: PDFium versi terbaru (build 8100, 2026).
- **Kertas bawaan A3+ 320×480 mm** (kertas stiker Develop di AP 55). F4/folio tidak ditambahkan karena cakupannya hanya stiker.
- **Ambang resolusi 150 dpi** masih tebakan awal (target riset 300 ppi); disesuaikan setelah melihat hasil cetak Develop.
- **Latar gambar tidak dibuang otomatis** (masukan leader 2026-10-21, menggantikan aturan latar putih D10). Artwork = piksel yang tidak transparan (alpha di atas 16). Gambar tanpa bagian transparan (JPG, PNG tanpa transparansi), termasuk yang berlatar putih, dipakai utuh, dan garis potong mengelilingi kotak gambar. Pemangkasan otomatis tetap membuang pinggiran transparan. Untuk garis yang mengikuti bentuk objek, operator membuang latar lewat Rapikan gambar atau mengatur titik garis potong. Ini juga menjawab pertanyaan kotak putih di PDF (D7): kotaknya dipertahankan.
- **Garis dalam untuk lubang asli (stiker donat)** (masukan leader 2026-10-21). Spesifikasi awal leader hanya memotong garis terluar; untuk stiker donat, garis kiss dan die juga dibuat di dalam lubang, digeser ke tengah lubang sejauh offset. Hanya rongga yang seluruhnya berada di lubang asli artwork (latar transparan yang tidak terhubung ke tepi gambar) yang dipotong (fungsi `Loader.KeepHoles`); rongga dari celah yang tertutup offset tetap diisi. Lubang die ikut tertutup bila lubang kiss tertutup, karena die harus membungkus kiss. Grid susun otomatis tetap memakai bentuk padat, jadi stiker lain tidak ditaruh di dalam lubang. Dikonfirmasi user 2026-10-22: aturan ini tetap untuk garis otomatis; lubang yang lebih sempit dari offset dipotong lewat klik smart select (F24).
- **Tombol Compact menjadi Penuhi lembar** (masukan leader 2026-10-21). Customer membayar per lembar, jadi tombol ini mengisi ruang kosong di semua lembar dengan salinan desain secara bergiliran sampai tidak ada yang muat (fungsi `Nest.Fill`; grid dan aturan jarak sama dengan susun otomatis). Jumlah per desain mengikuti hasilnya; desain yang sama sekali tidak muat tetap pada jumlahnya. Fungsi lama (memangkas tinggi lembar terakhir untuk bahan gulungan) dihapus karena AP 55 memakai kertas lembaran.
- **Editor garis potong: titik lurus dan kurva** (masukan leader 2026-10-21, contohnya Roboflow Annotate; titik kurva seperti CorelDRAW sejak 2026-10-23, [spesifikasi](superpowers/specs/2026-10-23-bentuk-garis-potong-design.md)). Garis otomatis tetap berupa kurva Bezier dari Potrace.
  - **Titik dari kurva otomatis:** kurva dipecah setiap ±0,05 mm, lalu dicocokkan dengan kurva Bezier kubik (metode Schneider), dengan selisih paling besar sebesar toleransi slider Simplify. Sudut garis (belokan lebih dari 45°, misalnya sudut kotak) tetap persis menjadi titik sudut. Bagian yang lurus dalam batas toleransi menjadi garis lurus.
  - **Hasil pemeriksaan otomatis S12:** simpangan 0,185 mm pada toleransi 0,2 mm. Titik logo kasar 21/17/10 pada toleransi 0,05/0,2/0,5 mm. Kiss png-transparent.png turun dari 31 titik poligon (versi 2026-10-21) menjadi 14 titik kurva.
  - **Data:** hanya desain yang diedit yang menyimpan titik (`Sticker.ManualKiss`/`ManualDie`, dalam mm relatif terhadap artwork). Tiap titik menyimpan posisinya, pegangan masuk, pegangan keluar, dan tanda halus.
  - **Export:** ruas lurus ditulis sebagai garis (`l`) dan ruas kurva sebagai Bezier (`c`), dengan warna CMYK yang sama.

  Aturan:
  - Klik di garis atau kurva menambah titik dengan membelah ruas itu (metode de Casteljau), sehingga bentuk tidak berubah. Menghapus titik menyambung kedua tetangganya memakai pegangan luar mereka. Titik halus menjaga kedua pegangannya tetap segaris saat salah satunya diseret.
  - Titik dan pegangan tidak bisa keluar dari kotak kerja. Kotak kerja adalah kotak desain otomatis, atau lebih besar bila garis/bentuk keluar darinya. Grid susun otomatis dan luas dihitung ulang dari garis; kotak desain membesar mengikuti titik dan pegangan terjauh.
  - Garis manual tidak berubah bila offset atau Halus diubah, kecuali garis hasil smart select yang titiknya belum diedit (sejak 2026-11-08, lihat F24). Menggeser Simplify di editor membuat ulang garis yang sedang diedit dari garis otomatis, jadi edit titiknya hilang (Urungkan mengembalikannya; perilaku ini dipertahankan, keputusan user 2026-11-02). Bila lebar diubah, garis diskala bersama artwork. Rapikan gambar mengembalikan garis otomatis, karena artwork-nya berubah.
  - Simpan dinonaktifkan selama garis kiss keluar dari die. Editor hanya menampilkan garis yang aktif; garis yang sedang mati kembali otomatis. Bila die manual sedangkan kiss otomatis (kiss diaktifkan belakangan), kiss dipangkas ke dalam die.
  - Simpan tanpa mengedit mempertahankan garis yang ada (otomatis, bentuk, atau manual). Reset otomatis lalu Simpan mengembalikan desain ke garis otomatis.
- **Smart select tanpa AI** (2026-10-22, [spesifikasi](superpowers/specs/2026-10-22-smart-select-design.md)). Klik objek di editor garis potong. Yang dianggap objek adalah piksel yang tersambung dengan titik klik, dan: pada gambar bertransparansi, tingkat tidak-transparannya (alpha) di atas Toleransi (awal 64); pada gambar tanpa bagian transparan, warnanya berbeda dari warna tepi gambar lebih dari Toleransi (awal 32, ukuran yang sama dengan Rapikan). Garis = objek dilebarkan sejauh offset tanpa Halus, jadi lekukan diikuti dan sudut luar membulat. Lubang objek yang lebar (jari-jari ruangnya minimal offset + 0,5 mm) langsung mendapat garis dalam; lubang yang lebih sempit dipotong lewat klik di dalam lubang, dengan jarak min(offset, jari-jari ruang − 0,5 mm), minimal min(offset, 0,3 mm). Die harus berada di dalam garis dalam kiss; bila tidak muat, lubang hanya diberi kiss-cut. Hasilnya garis manual biasa (titik kurva sejak 2026-10-23). Garis otomatis sengaja tidak berubah: lubang huruf berukuran mirip lubang donat (P dan R pada teks setinggi 8,8 mm: jari-jari ruang 1,04–1,14 mm; donat uji 1,29 mm), jadi memotong semua lubang secara otomatis akan membuat lingkaran potong ±1 mm di setiap huruf. Angka 0,5 dan 0,3 mm dikalibrasi di Graphtec operator.
- **Desain kotak otomatis** (2026-10-23). Bentuk artwork (siluet) dianggap kotak bila semua piksel tepi gambar bantu (mask, 6 piksel per mm) terisi dan isinya minimal 99,5 %.
  - Garis kiss = kotak artwork ± offset kiss. Garis die = kotak ± offset die, minimal kiss + 1 piksel. Halus tidak dipakai.
  - Kotak bersudut bulat (sudutnya transparan) tetap memakai garis otomatis.
  - Validasi: 4 desain kotak di data uji (jpg-large-24mp, jpg-low-resolution, pdf-multi-page p2, pdf-single-page) terdeteksi sama oleh validasi yang terpisah dari aplikasi. Garisnya = kotak ± offset, selisih paling banyak 0,10 mm (syarat A7).
  - Syarat A1 dan A5 disesuaikan untuk desain kotak: jarak terjauh di sudut kotak = offset × √2, dan garis lurus memang aturannya.
  - Akibat sampingan: jarak garis ke artwork pada desain kotak kini tepat sama dengan offset. Garis yang di bawah batas A4 turun dari 65 ke 43.
- **Preset bentuk** (2026-10-23). Bentuk mengelilingi artwork, dihitung dari kerangka luar cembung (convex hull) siluet artwork pada 6 piksel per mm, lalu diperluas sejauh offset:
  - kotak = kotak batas artwork (bbox) ± offset;
  - lingkaran = lingkaran terkecil yang memuat artwork (metode Welzl) + offset;
  - oval = elips dengan perbandingan sisi kotak batas;
  - segitiga sama sisi dan segi enam beraturan = dihitung dari irisan setengah bidang;
  - bintang (5 sudut, lekuk dalam 0,5 × jari-jari luar) dan hati = dibesarkan sama rata sampai ukuran terkecil yang memuat artwork (pencarian biner, uji titik di dalam bentuk);
  - kotak bergelombang dan lingkaran bergelombang = kotak sudut bulat atau lingkaran di atas, lalu tepinya didorong keluar berbentuk gelombang (2026-11-08, lihat §6).

  Aturan:
  - Die Otomatis memakai bentuk dan pusat yang sama dengan offset die, kecuali di bawah kiss bergelombang (die polos, lihat §6); die dengan bentuk lain membungkus garis kiss sejauh selisih offset (F26).
  - Kerangka luar memakai sudut piksel, sengaja ke arah luar supaya artwork tidak terpotong. Akibatnya bentuk bulat atau miring bisa sampai ±0,6 mm lebih besar dari ukuran minimal (pemeriksaan otomatis S15).
  - Bentuk disimpan per desain (`Sticker.Shape`) dan dihitung ulang setiap kali desain dimuat ulang (offset, lebar, Susun, Rapikan). Begitu titiknya diedit, hasilnya disimpan sebagai garis manual. Simplify tidak berlaku untuk bentuk.
  - Kotak sudut bulat memakai radius 3 mm dan bintang memakai rasio 0,5 sebagai nilai awal; nilai ini disesuaikan dengan pilihan operator.
- **Warna border** (masukan 2026-10-23). Disimpan per desain (`Sticker.Border`, CMYK) dan ikut saat desain dimuat ulang.
  - Area = garis kiss (tanpa kiss: die) diperlebar sejauh bleed, atau 1 mm bila bleed 0, supaya pergeseran pisau tidak menyisakan tepi putih. Lubang ikut diperlebar ke dalam; tengah lubang yang dipotong tetap putih. Bagian transparan artwork di dalam garis memperlihatkan warna border.
  - PDF menulis warnanya sebagai CMYK murni (perintah `k`), digambar di bawah artwork.
  - Bila warna border dipakai, bleed tidak lagi memperluas warna tepi gambar (perluasan itu akan menutupi border).
  - Tanpa bleed, jarak antar stiker minimal 1 mm supaya perpanjangan warna tidak masuk ke stiker tetangga.
- **Rapikan gambar hanya untuk gambar (raster)** (PNG, JPG, SVG, CDR yang dibuka tanpa CorelDRAW, dan PDF yang gagal dibaca sebagai vektor). File vektor (PDF/AI, dan CDR yang dibuka lewat CorelDRAW) tetap dirapikan di CorelDRAW supaya warna CMYK dan vektornya utuh. Rapikan hanya mengubah transparansi, tidak menggambar ulang piksel.
- **Pengurangan titik garis potong (2026-10-16).** Cara RDP tidak dipakai untuk garis otomatis (editor garis potong memakai pencocokan kurva Bezier sejak 2026-10-23, lihat di atas). Potrace sudah menghasilkan kurva Bezier yang dioptimalkan; RDP hanya bekerja pada garis lurus, jadi kurva harus dipecah dulu. Hasil ukur: teks butuh 117 titik garis lurus untuk ketelitian 0,1 mm, sedangkan Bezier cukup 72 titik. Yang dipakai:
  - Pengaturan Potrace: `AlphaMax` 1,3334 (tidak ada sudut tajam, pisau tidak berbelok mendadak) dan `OptTolerance` 1 piksel = 0,17 mm (lebih banyak potongan kurva digabung).
  - Penghalusan bentuk sebelum dijadikan kurva: gambar dikaburkan sebesar nilai Halus (sigma; 3 kali box blur, mendekati Gaussian), lalu dipisah lagi menjadi isi dan bukan isi (threshold). Lekukan kecil hilang. Halus 1–2 mm paling efektif; di atas 2 mm jumlah titik bisa naik lagi.
  - Penjaga jarak: di gambar bantu (mask), penghalusan tidak boleh membawa garis lebih dekat dari offset − 0,3 mm ke artwork (jari-jari dibulatkan ke atas sejak 2026-10-18). Garis die selalu membungkus garis kiss, minimal 1 piksel (0,17 mm) di luarnya.
  - Diukur 2026-10-18 di setiap titik garis terhadap bentuk asli file ([validasi garis potong dan susunan](../Printech-TestData/02-expected/validasi-garis-potong-susunan.md)):
    - nilai tengah (median) jarak = offset −0,19 s.d. +0,02 mm;
    - setelah menjadi kurva Potrace, garis bisa sampai 0,13 mm lebih dekat dari offset − 0,3 mm. Ini terjadi di sudut lancip dan pada offset kecil (selisih dari gambar bantu 6 piksel per mm). Selisih sampai 0,15 mm diterima (keputusan D3, 2026-10-27), jadi validasi memakai batas offset − 0,45 mm.
  - Hasil di aplikasi (kiss 2/1 mm, die 4/2 mm), titik kiss/die sebelum → sesudah: rough.jpg 64/43 → 16/13 (−73%), lingkaran 11/10 → 3/4 (−67%), logo.pdf 21/15 → 8/7 (−58%), bintang 29/27 → 15/17 (−43%), badge 9/8 → 8/8. Sudut tajam 2 → 0. Sejak 2026-10-18 (penjaga dibulatkan ke atas, die minimal 1 piksel di luar kiss) rough.jpg 17/16. Muat per desain 20–70 ms, stiker 300 mm ±1 detik (versi rilis).
  - Skenario uji S1–S23 beserta hasilnya: [skenario uji garis potong](UJI-GARIS-POTONG.md).
- **Stabilitas dan performa** (2026-10-17, spesifikasi leader bagian 2):
  - CDR dibaca lebih dulu oleh alat libcdr (`cdr2xhtml.exe` dari MSYS2, di folder `libcdr`), juga di proses terpisah dengan batas 30 detik (sejak 2026-11-06). Kotak pesan Windows untuk DLL yang hilang atau alat yang crash dimatikan, jadi kegagalannya langsung diteruskan ke CorelDRAW atau ke pesan untuk operator.
  - Bila libcdr gagal, CorelDRAW dikendalikan (lewat COM) dari proses pembantu (worker; aplikasi yang sama dengan argumen `--cdr2pdf`), bukan dari proses utama. Selama menunggu, tampilan tetap bisa dipakai. Lewat 30 detik, proses pembantu dihentikan dan operator mendapat pesan; Printech tetap jalan. CorelDRAW sendiri tidak dimatikan, supaya pekerjaan operator yang belum disimpan aman.
  - 3 percobaan susun otomatis dan hapus latar dihitung bersamaan (paralel, dengan `Parallel.For`). Spesifikasi menyebut `Parallel.ForEachAsync`, tetapi cara itu untuk pekerjaan yang banyak menunggu (baca/tulis file atau jaringan), sedangkan pekerjaan ini murni hitungan prosesor. Hasilnya sama persis sampai tiap bit dengan versi berurutan.
  - Hasil ukur (versi rilis, prosesor 12 thread): susun 130 stiker 57 → 43 ms. Gambar 4000×3000: hapus latar 119 → 97 ms (pengisian area atau flood fill tetap berurutan), kikis tepi 103 → 31 ms, tampilan 62 → 20 ms. Memori saat diam tidak berubah (total working set versi lama 148–152 MB, versi baru 148–153 MB, 4 kali ukur).
  - Hapus latar dan penghapus klik di jendela Rapikan dihitung di latar belakang, jadi jendela tetap merespons. Hanya pembaruan tampilan (±20–70 ms) yang berjalan di jalur utama tampilan.
  - Sejak 2026-10-19, file PDF/AI disalin ke folder sementara saat dimuat. Export dan Susun memakai salinan itu, jadi artwork tetap sama dengan dasar garis potong walau file asli diubah, dipindah, atau dihapus. Untuk memakai isi baru, tambahkan file itu lagi.
  - Sejak 2026-11-05, gambar PNG dan JPG juga tidak dibaca ulang dari file asli saat lebar atau garis potong berubah. Muat ulang memakai gambar yang sudah ada di memori, jadi file asli boleh diubah, dipindah, atau dihapus. SVG tetap digambar ulang supaya tajam selama filenya masih sama, dan dibaca dari folder aslinya karena gambar yang ditautkan SVG hanya terbaca dari sana (dicoba: dari folder lain gambar tautannya hilang). Bila filenya berubah atau hilang, SVG memakai gambar terakhir.
  - Foto bertanda orientasi (foto HP) ditegakkan saat dimuat, dan gambar tegak itu yang dipakai di layar, Rapikan, PDF, dan SVG (2026-11-05).
  - Sejak 2026-10-19, hasil konversi CDR disimpan per versi file (waktu ubah + ukuran); sejak 2026-11-06 termasuk hasil libcdr, juga yang gagal, supaya tidak dicoba ulang. CDR yang diedit lalu ditambah ulang dikonversi lagi.
  - Skenario uji P1–P10: [skenario uji stabilitas](UJI-STABILITAS.md).
- **Persiapan cetak (prepress)** (2026-10-17, spesifikasi leader bagian 3):
  - Garis potong ditulis dengan warna CMYK murni (DeviceCMYK) lewat perintah `K`: kiss `0 1 0 0`, die `1 0 0 0`. Tidak ada perintah warna RGB, profil warna (ICC), atau profil output (OutputIntent), jadi tidak ada konversi ke sRGB. Perintah warna lain yang ada hanya `0 g`/`0 G` (abu-abu, DeviceGray), yang ditulis pustaka PDFsharp sebelum artwork vektor ditempel; itu bukan warna garis potong.
  - Bleed: gambar bleed dibuat saat desain dimuat (paling tinggi 300 dpi), lalu dipakai untuk layar, preview JPG, dan PDF. Area yang dicetak = garis kiss (tanpa kiss: garis die) diperlebar sebesar bleed. File vektor tidak diberi bleed.
  - Bleed ikut mengisi border putih antara artwork dan garis kiss. Pakai bersama offset kecil (0–0,5 mm) untuk stiker tanpa border.
- **Indikator dan kemudahan bagi operator** (2026-10-17, spesifikasi leader bagian 4):
  - Bentuk teratur (reguler) atau tidak teratur (irregular) ditentukan otomatis: bila rata-rata isi bentuk terhadap kotaknya (ditimbang menurut luas) minimal 75%, dihitung teratur (persegi ±1, lingkaran ±0,79, bintang ±0,57). Batas hijau bentuk tidak teratur (60%) tidak disebut di spesifikasi; dipakai aturan yang sama dengan bentuk teratur, yaitu batas bawah + 5 poin.
  - Tempel otomatis (snap) memakai grid susun 1 mm (posisi kosong terdekat), bukan perhitungan bentuk poligon (NFP). Geser dengan panah keyboard tetap bebas. Bila tidak ada tempat kosong, stiker kembali ke posisi awal dan muncul pesan (keputusan user 2026-11-03). Sebelumnya stiker tertinggal di posisi lepas dan ditandai merah, bisa di luar lembar sehingga tidak terlihat.
  - Stiker yang lebih dekat dari setelan Jarak antar stiker ditandai merah seperti tumpang tindih (keputusan user 2026-11-03), dan Export bertanya dulu. Sel dan jaraknya sama dengan grid susun, jadi hasil susun otomatis dan tempel otomatis tidak pernah ditandai. Mengubah angka Jarak atau margin langsung memeriksa ulang semua lembar.
  - Kotak lebar desain menerima koma dan titik sebagai tanda desimal, seperti kotak di panel. Teks yang tidak terbaca atau angka bukan positif membuat kotaknya merah dan lebar tetap. Angka panel yang salah (kertas, margin, jarak, offset, Halus, bleed, lebar bawaan) tidak lagi diam-diam diganti nilai bawaan: Susun, tambah file, dan export berhenti dengan pesan, dan kursor pindah ke kotak itu (2026-11-05).
  - Angka bahan dibatasi paling tinggi 100%, dan merah bila luas stiker melebihi area lembar (keputusan user 2026-11-04). Itu hanya terjadi bila stiker bertumpuk, jadi angka sebenarnya baru terbaca setelah disusun ulang. Print Ticket mengikuti aturan yang sama.
  - Zoom in tidak mengurangi jumlah lembar per baris, supaya titik di bawah kursor tetap di tempat, dan lembar yang melebihi tampilan digeser (keputusan user 2026-11-04). Zoom out menambah jumlah lembar per baris bila muat, sehingga lembar bisa berpindah baris. Pas layar, Susun otomatis, dan perubahan lebar jendela menghitungnya dari awal.
  - Pilihan bahan siap pakai (preset bahan) dihapus 2026-10-23: menurut user/leader tidak diperlukan, karena offset, Halus, margin, dan jarak diatur langsung. Nilai awalnya tetap dicatat di [catatan riset](RESEARCH.md) bagian 13 sebagai riwayat riset.
- **CDR dan AI tanpa CorelDRAW atau Illustrator** (keputusan user 2026-11-06, disetujui leader):
  - CDR dibaca lebih dulu oleh libcdr; CorelDRAW hanya cadangan bila libcdr gagal. libcdr mengubah semua warna (CMYK, Pantone, spot) menjadi RGB, dan artworknya ditempel sebagai gambar ±300 dpi. Ini diterima supaya CDR bisa dibuka di PC tanpa CorelDRAW.
  - Hanya pustaka gratis, dan tidak ada program yang dipasang terpisah di PC operator: alat libcdr beserta DLL-nya (±42 MB) ikut di folder Printech dan masuk git. Asal, versi, sidik SHA256, dan lisensinya ada di `StickerLayout/libcdr/SUMBER.txt`.
  - AI yang disimpan tanpa PDF-compatible hanya berisi halaman pengganti; isi aslinya data khusus Illustrator tanpa spesifikasi publik dan tanpa pembaca gratis yang andal. Printech menolaknya dengan pesan untuk menyimpan ulang dengan "Create PDF Compatible File" atau export ke PDF.
  - Pustaka berbayar (misalnya Aspose.Imaging) tidak dipakai.
- **Ukuran per stiker** (keputusan user 2026-11-07):
  - Titik pojok di kanvas dan kotak lebar di panel Pilihan hanya mengubah stiker yang terpilih. Stiker lain yang memakai desain sama tidak berubah.
  - Bila desainnya masih dipakai stiker lain, stiker yang diubah mendapat desain sendiri: baris baru tepat di bawah desain lama di daftar desain, dengan nama sama, lebar baru, dan jumlah sebanyak stiker yang diubah. Jumlah desain lama berkurang sebanyak itu. Bila semua stiker desain itu yang diubah, desainnya sendiri yang berubah dan tidak ada baris baru.
  - Cara ini dipilih daripada skala per stiker. Garis potong tetap dihitung ulang dengan offset mm yang sama, sedangkan skala akan ikut memperbesar jarak garis potong, bleed, dan border. Susun otomatis, export, urungkan, Ctrl+D, dan salin-tempel tetap memakai desain milik tiap stiker, jadi tidak perlu diubah.
  - Kotak lebar di daftar desain tetap mengubah lebar desain itu untuk semua stikernya, dan berlaku saat Susun otomatis.
- **Rasio bebas** (keputusan user 2026-11-07):
  - Lebar dan tinggi artwork bisa diatur sendiri-sendiri. Panel Pilihan punya kotak Lebar dan Tinggi serta kotak centang **Kunci rasio** yang tercentang dari awal. Bila kunci menyala, sisi lainnya ikut sebanding, seperti sebelumnya. Bila dilepas, sisi lainnya tetap, dan titik pojok di kanvas mengubah sisi mendatar dan tegak sendiri-sendiri. Tidak ada tombol tersembunyi seperti Shift.
  - Kunci rasio bukan bagian dokumen: tidak ikut urungkan, dan berlaku untuk tarikan dan isian berikutnya.
  - Lebar dan Tinggi di panel memakai sumbu artwork, juga untuk stiker yang diputar 90°, sama seperti daftar desain dan Print Ticket.
  - Garis kiss dan die, bleed, dan border dihitung ulang pada ukuran baru dengan jarak mm yang sama. Garis manual dari editor ikut diregangkan. Bentuk preset Lingkaran, Lingkaran bergelombang, Bintang, Hati, Segitiga, dan Segi enam tetap beraturan dan membesar mengelilingi artwork; Oval dan Kotak mengikuti bentuk artwork.
  - Regangan disimpan di desain (`StretchY`), jadi ikut cara pisah desain, urungkan, Susun otomatis, Rapikan, editor garis potong, Ctrl+D, salin-tempel, dan export. Kotak lebar di daftar desain menjaga rasio desain itu.
  - Dpi yang ditampilkan memakai arah yang paling rendah, jadi gambar yang ditinggikan bisa menjadi merah.
  - Tombol untuk kembali ke proporsi file asli tidak dibuat; Ctrl+Z sudah mengembalikan ukuran sebelumnya.
- **Export** (keputusan user 2026-11-08, daftar tugas leader):
  - Semua export lewat satu dialog: format, isi file (print, cutting, atau keduanya), lembar, dan tanda registrasi dipilih dulu, baru lokasi simpan.
  - Export CDR dan AI tidak dibuat. Menulis CDR hanya bisa lewat CorelDRAW, yang tidak terpasang di PC pengembangan; operator membuka PDF atau SVG hasil Printech di CorelDRAW atau Illustrator, lalu menyimpannya sendiri.
  - Tanda registrasi di file export berupa kotak centang di dialog, bawaan mati (leader: "regmark cocok untuk visual saja"). Di layar, tanda dan area kosong di sekitarnya tetap mengikuti menu Tampilan.
- **Warna border dari roda warna dan pipet** (keputusan user 2026-11-08, daftar tugas leader):
  - Tombol **Warna lain…** membuka dialog warna bawaan Windows (spektrum warna dan warna sendiri), tanpa pustaka baru. Warna sendiri di dialog disimpan selama Printech terbuka.
  - Kotak centang **Pipet**: klik di gambar stiker, warna titik itu menjadi warna border. Klik di area transparan tidak mengubah apa pun.
  - Warna layar (RGB) diubah ke CMYK dengan kebalikan rumus tampilan layar, dibulatkan ke persen utuh.
- **Daftar tugas leader, lanjutan** (keputusan user 2026-11-08):
  - Shift berlaku di dua tempat: di editor garis potong garis ke titik tetangga dikunci mendatar, tegak, atau 45 derajat; di kanvas stiker hanya bergeser mendatar atau tegak. Shift+klik di editor tetap memilih beberapa titik.
  - Rapikan gambar mendapat crop kotak, bukan potong bebas (lasso).
  - Penuhi lembar mengikuti stiker terpilih, tanpa jendela pilihan baru.
  - Garis smart select ikut offset dan Halus (lihat F24).
- **Tepi bergelombang** (daftar tugas leader; rinciannya diserahkan user ke agent 2026-11-08, dengan syarat riset dulu):
  - Riset:
    - Stiker bertepi bergelombang (scalloped) adalah produk biasa di percetakan stiker. Contoh: [Vistaprint](https://www.vistaprint.ie/labels-stickers/labels/sticker-sheets/scalloped-stickers) menyebut tepinya "soft, rounded edges".
    - Cetakan scalloped circle dari [Spellbinders](https://www.spellbinderspaperarts.com/nestabilities-classic-scalloped-circles-sm-etched-dies/) memakai 22 lengkung per lingkaran di semua ukuran; lengkung terbesar selebar 0,25 inci (±6,4 mm). Angka ini dari ringkasan hasil pencarian; halaman produknya tidak dibuka.
    - Percetakan stiker menyarankan lekukan diberi lengkung kecil, bukan sudut lancip: [StickerGiant](https://support.stickergiant.com/hc/en-us/articles/34162041989524-Can-You-Cut-Sharp-Corners) dan [Sticker Mule](https://stickermule.com/support/how-intricate-can-inner-cuts-be). Pisau standar Graphtec untuk bahan stiker berwarna (CB09UB) berdiameter 0,9 mm ([manual pisau CE8000](https://mygraphtec.jp/site_download/manual/ce8000/CE8000_Cutter%20Blade%20Manual-ENG.pdf)).
  - Mengapa perlu bentuk garis potong baru: garis otomatis mengikuti tepi artwork, dan bentuk yang sudah ada bertepi rata. Membuat tepi bergelombang dengan tangan di editor berarti menggeser puluhan titik satu per satu, tanpa jaminan jarak ke artwork. Bentuk baru memakai jalur yang sama dengan bentuk lain (dipilih di editor, ikut offset dan lebar, ikut export), jadi tidak ada format file atau alur kerja baru.
  - Bentuknya Kotak bergelombang (dasar kotak sudut bulat) dan Lingkaran bergelombang (dasar lingkaran). Tepi bentuk dasar didorong keluar berbentuk gelombang halus (kosinus):
    - lembah gelombang tepat di bentuk dasar, jadi artwork tetap sejauh offset;
    - puncak gelombang ada di diagonal tiap sudut, dan tiap sisi mendapat gelombang utuh, jadi bentuknya simetris;
    - panjang gelombang ±keliling/22, dibatasi 4–8 mm; tinggi gelombang paling banyak 1/4 panjangnya;
    - jari-jari lengkung terkecil paling sedikit 1 mm, jadi tidak ada lekukan lancip; tinggi gelombang dikurangi bila perlu.
  - Die Otomatis di bawah kiss bergelombang memakai bentuk dasarnya yang polos, membungkus kiss sejauh selisih offset. Dua garis bergelombang dengan offset berbeda tidak sejajar dan bisa saling memotong, sedangkan die polos selalu di luar kiss. Die bergelombang tetap bisa dipilih sendiri (membungkus kiss). Tanpa kiss, die Otomatis ikut bergelombang.
  - Bukti dari pemeriksaan otomatis ("tepi bergelombang"): stiker kotak 40 × 30 mm mendapat 22 gelombang dan stiker bulat 40 mm 24 gelombang, tinggi ±1,5 mm; jarak terdekat ke artwork tidak kurang dari offset; jari-jari lengkung terkecil 1,32 mm dan 1,17 mm; garisnya hanya kurva; die polos maupun die bergelombang membungkus kiss.
- **Ditunda sampai ada kebutuhan nyata:** hapus latar foto dengan AI dan memperbesar gambar pecah. Untuk gambar pecah, cara tercepat tetap meminta file asli ke customer; angka dpi merah memberi tahu kapan perlu.
- **Tidak dibangun:** sembunyikan salinan (hide copy), simpan proyek, dan mode nametag/kartu. Tanda registrasi buatan aplikasi awalnya tidak dibangun, lalu ditambahkan 2026-10-26 (F28).

## 7. Komponen pihak ketiga dan lisensinya

| Paket | Fungsi | Lisensi |
| --- | --- | --- |
| SharpVectors.Wpf 1.8.6 | Membaca SVG | BSD-3 |
| BitmapToVector 1.16.3 | Potrace, pembuat kurva garis potong | GPL-3.0. Aman untuk pemakaian internal; jangan dibagikan ke luar perusahaan |
| bblanchon.PDFium.Win32 157.0.8100 | Membuka dan menggambar PDF/AI (PDFium, versi Windows saja) | Apache-2.0 |
| PDFsharp 6.2.4 | Menulis PDF | MIT |
| libcdr 0.1.9 (alat `cdr2xhtml.exe` dari MSYS2) | Membaca CDR tanpa CorelDRAW | MPL-2.0 |
| librevenge 0.0.6 | Antarmuka gambar yang dipakai libcdr | MPL-2.0 atau LGPL-2.1+ |
| ICU 78.3 | Teks Unicode untuk libcdr | Unicode License v3 |
| lcms2 2.20 | Konversi warna di libcdr | MIT |
| zlib 1.3.2 | Membuka data terkompresi di CDR | zlib |
| libgcc dan libstdc++ 16.2, winpthreads | Pustaka bawaan compiler untuk alat libcdr | GPL-3.0 dengan GCC Runtime Library Exception; winpthreads berlisensi bebas (MIT) |

## 8. Batasan yang diketahui

- Jalur CDR lewat CorelDRAW (kini hanya cadangan bila libcdr gagal) belum diuji, karena CorelDRAW tidak terpasang di PC pengembangan.
- CDR yang dibuka tanpa CorelDRAW (libcdr, sejak 2026-11-06):
  - semua warna menjadi RGB, termasuk hitam murni (K saja), jadi teks hitam bisa tercetak sebagai hitam empat warna;
  - artwork ditempel sebagai gambar ±300 dpi, bukan vektor;
  - PowerClip tidak dibaca, jadi isinya bisa tergambar tanpa potongan dan garis potongnya ikut terlalu besar. Lensa hanya transparansi isi, mesh fill hilang, dan huruf yang tidak terpasang diganti huruf lain;
  - CorelDRAW versi baru (di atas 2021) belum terbukti terbaca. File seperti itu bisa gagal, lalu Printech mencoba CorelDRAW, atau terbuka dengan hasil keliru tanpa terdeteksi. Peringatan di baris status mengingatkan operator untuk memeriksa pratinjaunya;
  - di repo baru ada satu contoh CDR asli (dari uji LibreOffice), jadi mutunya pada file sehari-hari belum terbukti.
- Pemeriksaan AI tanpa PDF-compatible hanya mengenali kalimat bahasa Inggris dari Illustrator.
- PDF yang diputar (/Rotate) atau tidak bisa dibaca pustaka PDFsharp akan ditempel sebagai gambar (±254 dpi, RGB). Termasuk PDF terenkripsi yang bisa dibuka tetapi gagal ditempel: saat dimuat, halaman dicoba ditempel seperti saat export (temuan D8, 2026-10-21). Aplikasi memberi peringatan di baris status sejak desain dimuat ("… PDF ditempel sebagai gambar"; sebelum 2026-10-18 hanya saat PDFsharp gagal ketika export).
- SVG di-export sebagai gambar ±300 dpi.
- Export SVG (F29): artwork selalu berupa gambar, bukan vektor. Desain PDF/AI memakai gambar tampilan layar (paling besar 1600 piksel, RGB), jadi stiker besar bisa kurang tajam. Warna border dan garis potong memakai RGB, karena SVG tidak punya CMYK. Export PDF tetap menjaga vektor dan CMYK.
- Yang dipotong: garis luar, ditambah lubang asli artwork yang tetap terbuka setelah offset dan Halus (stiker donat). Rongga yang terbentuk saat offset menutup celah artwork (misalnya huruf C besar) dan lubang yang tertutup offset tidak dipotong. Garis dalam tidak bisa dimatikan per desain; hapus lewat editor garis potong bila tidak diinginkan. Lubang yang lebih sempit dari offset bisa ditambahkan lewat smart select (klik di dalam lubang).
- Susun otomatis belum optimal (stiker ditaruh di tempat kosong pertama, greedy).
- Rotasi hanya kelipatan 90°.
- Selama Alt + seret, stiker asal ikut bergerak di layar bersama kursor, dan baru tampil lagi di tempatnya saat dilepas. Hasilnya tetap benar: stiker asal tidak pindah, dan salinannya di titik lepas.
- Stiker yang diseret masuk ke posisi kosong terdekat pada kotak bantu 1 mm, juga di mode Rapi: tidak diluruskan ke baris atau kolomnya. Pada zoom kecil posisi lepas bisa meleset sekitar 1 mm, karena 1 piksel layar pada zoom 19% sudah ±1,1 mm.
- Belum ada gulir otomatis saat stiker dibawa ke tepi tampilan.
- Warna di layar hanya pratinjau. Akurasi warna dijaga di file export.
- Foto bertanda orientasi disimpan sebagai PNG setelah ditegakkan, jadi PDF-nya lebih besar daripada JPG aslinya.
- SVG yang file aslinya sudah berubah atau hilang diperbesar dari gambar terakhir, jadi bisa kurang tajam. Angka dpi tetap memberi tahu kapan perlu.
- Spasi selalu menggeser tampilan selama kursor tidak di kotak teks, jadi Spasi tidak menekan tombol atau kotak centang yang sedang fokus. Tanpa batas ini, tombol yang baru diklik akan tertekan lagi saat operator menahan Spasi untuk menggeser.
- Rapikan gambar: latar foto yang ramai (bukan warna polos) belum bisa dihapus otomatis; gambar di atas 4000 piksel diperkecil ke 4000 piksel saat dirapikan.
- Pengurangan titik: target 60–80% tercapai pada logo kasar dan bentuk bulat. Teks dan bintang turun 33–59% karena sudutnya adalah detail asli. Logo dengan gelombang tepi yang nyata (`png-rough-edge.png` di data uji) hanya turun 54%. Target 60% hanya berlaku untuk logo kasar seperti `rough.jpg` (pemeriksaan otomatis S1); logo bergelombang hanya dicatat (keputusan D5, 2026-10-27). Di area cekung, garis bisa bergeser keluar sampai ±0,5 mm (kiss) atau ±0,7 mm (die): border sedikit lebih lebar. Halus 0 mematikan penghalusan bentuk.
- CDR: batas 30 detik belum dikalibrasi di PC operator. Bila CorelDRAW menggantung karena dialog yang tidak terlihat, tutup CorelDRAW lalu tambahkan file lagi.
- Muat ulang desain dan susun otomatis masih berjalan di jalur utama tampilan (susun 130 stiker ±85 ms; desain yang sangat besar bisa ±1 detik).
- Jarak nyata antar stiker masih lebih lebar dari yang diminta (median +1,2 mm), karena jejak stiker dibulatkan ke grid 1 mm.
- Editor garis potong:
  - Memilih beberapa titik hanya lewat Shift+klik; memilih dengan menyeret kotak belum ada.
  - Print Ticket tetap menulis offset dan Halus dari pengaturan, juga untuk desain bergaris manual atau berbentuk preset; nama bentuk tidak ditulis.
- Preset bentuk:
  - Bentuk bisa sampai ±0,6 mm lebih besar dari ukuran minimal, karena kerangka luarnya dihitung dari sudut piksel.
  - Ukuran bentuk belum bisa diketik, dan bentuk belum bisa diputar.
  - Bentuk selalu mengelilingi artwork. Memotong foto menjadi bentuk (crop) belum ada.
  - Memilih bentuk (untuk kiss atau die) membuat ulang kedua garis dari dasarnya, jadi edit titik sebelumnya hilang; Urungkan mengembalikannya.
  - Dengan bleed aktif, ruang antara artwork dan bentuk ikut diisi warna tepi artwork, sama seperti garis manual yang jauh dari artwork. Pakai warna border bila ruang itu harus berwarna rata.
- Warna border: urungkan/ulangi di dalam editor belum mencakup ganti warna (urungkan di jendela utama mencakupnya). Warna dari dialog warna dan pipet diubah dari RGB ke CMYK tanpa profil warna, jadi hasil cetak bisa beda dari layar; pipet pada desain PDF vektor mengambil warna tampilan layar, bukan nilai CMYK aslinya. Print Ticket belum menulis warna border. Warna di layar hanya perkiraan dari CMYK.
- Smart select yang ikut offset: begitu satu titik diedit, garisnya menjadi garis manual biasa yang tidak ikut offset. Urungkan di editor yang melompati dua smart select bisa membuat garisnya dianggap manual. Bila klik lama tidak lagi mengenai objek, garis lama dipakai. Rapikan gambar membuang klik ini bersama garis manualnya.
- Crop kotak di Rapikan hanya berbentuk kotak lurus.
- Tepi bergelombang:
  - 22 gelombang per keliling dan lengkung terkecil 1 mm adalah titik awal, belum dipilih bersama operator AP 55. Tinggi dan panjang gelombang belum bisa diatur, dan hanya kotak dan lingkaran yang bisa bergelombang.
  - Die polos di bawah kiss bergelombang berjarak offset die ditambah tinggi gelombang dari artwork, jadi bahan sedikit lebih boros.
  - Pada stiker yang sangat kecil gelombangnya diperpendek, dan hilang bila jari-jari sudut bentuk dasarnya 1 mm atau kurang.
  - Garisnya punya banyak titik (4 per gelombang, ±90 untuk stiker 40 mm). Menggeser satu titik membuatnya menjadi garis manual biasa.
- Rasio bebas (2026-11-07):
  - Garis manual ikut diregangkan, jadi jaraknya ke artwork tidak tetap dalam mm. Bila desain dipendekkan, garis bisa lebih dekat ke artwork daripada offset; periksa di editor, atau pakai Reset otomatis.
  - Lingkaran, Lingkaran bergelombang, Bintang, Hati, Segitiga, dan Segi enam tidak ikut menjadi lonjong, jadi bahan bisa lebih boros pada desain yang diregangkan.
  - Kunci rasio dibaca saat isian lebar atau tinggi diterapkan, yaitu saat kotaknya ditinggalkan. Bila operator mengetik lalu langsung mengklik Kunci rasio, isian itu diterapkan dengan status kunci yang lama.
  - Selama titik pojok ditarik, pratinjau ikut meregangkan jarak garis potong. Hasil akhirnya dihitung ulang dengan benar saat mouse dilepas.
  - Export SVG memakai `preserveAspectRatio="none"` supaya gambar ikut diregangkan. Belum dicoba apakah CorelDRAW atau Inkscape membacanya dengan benar.
- Ukuran per stiker (2026-11-07): Rapikan dan Edit garis potong berlaku per baris desain, jadi tidak ikut ke baris ukuran lain dari file yang sama. Baris yang dikembalikan ke lebar yang sama dengan baris lain tidak digabung lagi.
- Smart select membaca transparansi atau beda warna dengan latar; objek di foto berlatar ramai belum bisa dipilih.
- Bleed: tepi JPG yang sangat kasar (kompresi tinggi) bisa menyisakan garis pucat tipis di area bleed; rapikan gambarnya dulu untuk hasil terbaik. Stiker gambar yang sangat besar (±300 mm) dengan bleed dimuat ±1–2 detik lebih lama. Zona bleed berisi rata-rata halus warna tepi, jadi motif kecil di tepi (misalnya garis kain atau bulu) tidak diteruskan.
- Tanda registrasi: area 6 mm di sekitar tanda dikosongkan, jadi stiker di pojok lembar bergeser (uji semua file 01-input: lembar 1 dari 5 pcs 92% menjadi 4 pcs 90%; total tetap 18 stiker, 54%). Menyalakan atau mematikan tanda tidak menyusun ulang; stiker di area tanda jadi merah sampai Susun otomatis ditekan. Export PDF memuat tanda hitam (K) bila menu aktif.

## 9. Pertanyaan terbuka

| Pertanyaan | Ditanyakan ke | Nilai bawaan sampai dijawab |
| --- | --- | --- |
| Format tanda registrasi (reg mark) milik operator | operator (file menyusul) | margin 10 mm |
| Offset, jarak antar stiker, dan bleed standar toko | operator | kiss 2/1 mm, die 4/2 mm, jarak 2 mm |
| Spesifikasi PC operator | operator | Windows 10/11 64-bit |
| Ambang dpi yang masih bisa diterima | operator, dari hasil cetak | 150 dpi |
| Seberapa sering latar foto atau gambar pecah muncul | operator, setelah uji | hapus latar dengan AI ditunda |
| Garis potong di Cutting Master 5 dikenali lewat warna atau layer | operator | warna (magenta/cyan) |

## 10. Daftar uji untuk operator

1. Tambahkan file CDR. Apakah desainnya masuk tanpa CorelDRAW, dan apakah warna dan bentuknya sama dengan file aslinya? Berapa detik prosesnya, dan apakah Printech tetap bisa dipakai selama menunggu?
2. Tarik file dari Explorer ke jendela Printech. Coba alat Geser tampilan (H atau tahan Spasi), Zoom (Z), dan shortcut Ctrl+I, F4, R, Delete, serta panah.
3. Export preview JPG, kirim ke WhatsApp. Apakah cukup jelas untuk konfirmasi ke customer?
4. Masukkan foto yang diterima lewat WhatsApp. Apakah angka dpi merah muncul untuk file yang hasil cetaknya memang pecah?
5. Rapikan 3–5 gambar customer yang biasanya dibersihkan di Photoshop. Berapa menit dibanding Photoshop? Apa yang belum bisa dikerjakan?
6. Export PDF, lalu buka di CorelDRAW. Apakah artwork tajam dan warnanya sama dengan file asli?
7. Di Cutting Master 5, pilih per warna: magenta = kiss-cut, cyan = die-cut. Apakah potongannya sesuai?
8. Cetak di Develop (A3+) dan potong di Graphtec. Berapa mm selisih potongannya? Apakah gerakan pisau mulus pada teks dan logo bertepi kasar? Langkah lengkap: [skenario uji garis potong](UJI-GARIS-POTONG.md) S10.
9. PDF dari customer yang punya latar putih penuh: apakah garis potongnya ikut menjadi kotak? **Terjawab 2026-10-21:** ya, latar tidak dibuang otomatis; operator membuangnya lewat Rapikan atau titik garis potong.
10. Buka PDF di CorelDRAW lalu klik garis kiss dan die: warnanya harus C0 M100 Y0 K0 dan C100 M0 Y0 K0 (CMYK, bukan RGB). Cetak satu desain PNG dengan bleed 2 mm dan offset kiss 0,5 mm, lalu potong: apakah tepi stiker tetap berwarna tanpa garis putih?
11. Apakah offset, halus, dan jarak bawaan cocok untuk bahan yang dicetak? Export Print Ticket lalu lampirkan ke SPK: apakah isinya cukup untuk kasir?
12. Buka Edit garis potong (menu Lainnya) pada satu desain. Geser titik, tambah titik dengan klik di garis atau kurva, hapus titik dengan Delete, ubah Simplify, lalu Simpan. Apakah garis di lembar, di PDF (CorelDRAW), dan di Cutting Master 5 sama dengan yang diatur? Apakah pisau Graphtec mulus di setiap titik?
13. Buka Edit garis potong pada stiker donat berwatermark, centang Smart select, klik donat, lalu klik lubang tengahnya. Apakah garis luar mengabaikan watermark, dan lubang tengah terpotong rapi di Graphtec?
14. Masukkan foto atau JPG kotak (tanpa transparansi). Apakah garis kiss dan die langsung berbentuk kotak bersudut tajam sejauh offset? Setelah dipotong di Graphtec, apakah sudut kotak rapi?
15. Di Edit garis potong, pilih bentuk (lingkaran, oval, segitiga, bintang, hati, segi enam, kotak sudut bulat), lalu Simpan. Apakah bentuknya sesuai keinginan customer, artwork tidak terpotong, dan hasil potong Graphtec mengikuti bentuk? Perlukah radius kotak sudut bulat (3 mm) atau bentuk bintang diubah?
16. Di editor, pilih titik, lalu seret pegangan kurvanya (handle). Coba juga Jadikan kurva/Jadikan garis dan Node halus/Node sudut. Apakah titik dan kurvanya sama saat PDF dibuka di CorelDRAW, dan apakah pisau Graphtec mulus pada kurva?
17. Di editor, pilih warna border (misalnya Merah), Simpan, cetak di Develop, lalu potong di Graphtec. Apakah warnanya sesuai, dan tidak ada tepi putih di garis potong walau pisau sedikit bergeser? Perlukah perpanjangan 1 mm diubah?
18. Catat setiap error atau kejanggalan, lalu kirim ke pengembang.

### Cek tampilan setelah perubahan

Dicek dengan tangan saat sempat; cukup bagian yang berubah. Skrip yang memakai mouse dan keyboard sungguhan tidak wajib lagi (keputusan user 2026-11-05), karena PC tidak boleh dipakai selama skrip berjalan.

**Menarik stiker:**

- Seret stiker ke lembar lain. Stiker terlihat di atas lembar lain selama ditarik, lalu pindah ke sana. Lembar asal yang kosong hilang, dan Ctrl+Z mengembalikannya.
- Lepas stiker di celah antar-lembar: stiker tetap di lembar asal. Lepas di lembar yang penuh: stiker kembali ke posisi awal dan muncul pesan.
- Tahan Alt saat melepas: stiker asal tetap, salinannya di titik lepas, dan jumlah pcs naik. Satu Ctrl+Z membatalkannya. Sesudah Alt dilepas, tombol panah langsung menggeser stiker.
- Selagi menarik: baris status menyebut mode dan lembar tujuan, Esc membatalkan, serta Ctrl+Z dan Delete diabaikan.
- Zoom 200% atau lebih, gulir, lalu seret stiker ke lembar lain: stiker jatuh di bawah kursor. Putar stikernya, lalu ulangi. **Belum pernah dicoba dengan mouse.**
- Putar roda mouse selagi menarik tanpa menggeser mouse: stiker tetap di bawah kursor, dan saat dilepas jatuh di sana. **Belum pernah dicoba dengan mouse.**

**Memilih beberapa stiker:**

- Ctrl+klik dua stiker, atau seret dari area kosong lembar untuk kotak pilih: panel menulis "2 stiker terpilih". Klik di luar lembar melepas pilihan.
- Seret, putar (R), hapus (Delete), Ctrl+C lalu Ctrl+V, dan Alt + seret: semua stiker terpilih ikut, juga bila berada di lembar berbeda.
- Ctrl+D pada beberapa stiker: semuanya terduplikat. **Belum pernah dicoba.**
- Bahasa English: panel menulis "2 stickers selected".

**Salin dan ukuran:**

- Ctrl+C lalu Ctrl+V: salinan di kanan stiker asal, dan turun ke baris berikutnya bila penuh. Tombol Duplikat di judul lembar: lembar baru di sampingnya.
- Ctrl+D pada satu stiker: salinan di kanan dan terpilih. Delete menghapus salinannya, bukan stiker asal.
- Klik stiker: 4 titik muncul di pojok. Tarik titik: lebar × tinggi tampil di dekat kursor. Saat dilepas, hanya stiker itu yang berubah; salinannya di lembar yang sama dan di lembar lain tetap, dan daftar desain mendapat baris baru dengan nama sama. Klik titik tanpa menarik: ukuran tetap. **Per stiker belum pernah dicoba (sejak 2026-11-07).**
- Panel Pilihan: ketik lebar, lalu Tab atau klik di luar kotak: hanya stiker terpilih yang berubah, juga bila beberapa stiker terpilih. Selama mengetik belum ada yang berubah. Bila kotaknya ditinggalkan dengan klik di kanvas, klik itu hanya menerapkan lebar, tanpa memilih atau menarik stiker. **Belum pernah dicoba.**
- Ctrl+Z sesudah atur ukuran: stiker kembali ke ukuran awal, dan baris baru di daftar desain hilang. Ctrl+Y mengembalikannya. **Belum pernah dicoba.**
- Rasio bebas: lepas centang Kunci rasio di panel Pilihan, lalu tarik titik pojok ke samping saja: hanya lebar yang berubah, tinggi tetap, dan pojok seberang diam. Tarik ke bawah saja: hanya tinggi yang berubah. Lebar × tinggi di dekat kursor sesuai. **Belum pernah dicoba.**
- Rasio bebas: dengan kunci dilepas, ketik tinggi lalu Tab: lebar tetap. Centang lagi Kunci rasio, ketik lebar: tinggi ikut sebanding dengan rasio yang baru. Ctrl+Z tidak mengubah centang Kunci rasio. **Belum pernah dicoba.**
- Rasio bebas: stiker yang diregangkan tetap regang sesudah Susun otomatis, Rapikan, dan Edit garis potong; garis potong di layar dan di export mengikuti bentuk yang diregangkan dengan jarak yang sama; gambar kecil di daftar desain dan panel Pilihan ikut regang. Mengetik tinggi lalu langsung menekan Ctrl+L atau Ctrl+E: tinggi itu ikut dipakai. **Belum pernah dicoba.**
- Export: tombol Export dan Ctrl+E membuka dialog, bukan langsung menyimpan. Ctrl+Shift+E membuka dialog dengan JPG terpilih. Menu File hanya punya satu Export. **Belum pernah dicoba.**
- Export Print saja: PDF tanpa garis magenta dan cyan. Cutting saja: hanya garis potong. Lepas centang Semua lembar lalu pilih satu lembar: hanya lembar itu yang diexport, dan tombol Export mati bila tidak ada lembar yang dicentang. **Belum pernah dicoba.**
- Export dengan Tanda registrasi tidak dicentang: siku tidak ada di file walau tampil di layar; dicentang: siku ada. **Belum pernah dicoba.**
- Editor garis potong, warna border: Warna lain… membuka dialog warna Windows, dan warna yang dipilih menjadi border (isian CMYK ikut berubah). Centang Pipet lalu klik di gambar: warna titik itu menjadi border, lalu centang Pipet lepas sendiri. **Belum pernah dicoba.**
- Editor garis potong, kotak CMYK: ketik angka, tekan Delete: huruf di kotak terhapus, titik garis tetap. Ctrl+Z di kotak mengurungkan ketikan. Ketik `0 100 100 0` lalu Enter: warna border berlaku dan editor tetap terbuka. **Belum pernah dicoba.**
- Tombol tanda registrasi di batang alat kiri (di bawah penggaris): menyalakan dan mematikan tanda di lembar, sama dengan menu Tampilan, dan ikut Ctrl+Z. **Belum pernah dicoba.**
- Tahan Shift saat menyeret stiker: hanya bergerak mendatar atau tegak, juga bila Shift ditekan atau dilepas di tengah tarikan. Bawa ke lembar di sebelahnya dengan kursor agak turun: stiker jatuh di tempat yang terlihat saat ditarik. Di editor garis potong, tahan Shift saat menyeret titik: garisnya lurus ke titik tetangga. **Belum pernah dicoba.**
- Rapikan gambar: centang Crop kotak, tarik kotak di gambar, lepas mouse: di luar kotak terhapus, centangnya lepas, dan Urungkan mengembalikannya. Ctrl + roda mouse memperbesar. **Belum pernah dicoba.**
- Pilih satu stiker lalu klik Penuhi lembar: hanya desain itu yang bertambah dan pesannya menyebut namanya; tanpa pilihan semua desain bergiliran. **Belum pernah dicoba.**
- Smart select di editor, Pakai, lalu besarkan offset dan Susun otomatis: garisnya ikut melebar. Geser satu titiknya lalu ubah offset lagi: garis tidak lagi ikut. **Belum pernah dicoba.**
- Editor garis potong: pilih Kotak bergelombang atau Lingkaran bergelombang. Kiss bergelombang dengan puncak di tiap sudut, die Otomatis polos di luarnya, dan daftar desain menulis nama bentuknya. Ubah offset atau lebar: gelombangnya ikut. **Belum pernah dicoba.**
- Perbesar stiker sampai bertumpuk: stiker merah, dan angka bahan 100% berwarna merah.

**Tata letak dasar:**

- Geser stiker sampai menempel stiker lain, putar, geser dengan panah, dan hapus. Ubah jumlah dan lebar, lalu Susun otomatis.
- Pilih Hemat dan Rapi, lalu bandingkan hasilnya. Coba Penuhi lembar.
- Naikkan angka Jarak antar stiker: stiker yang terlalu dekat jadi merah. Kembalikan angkanya: merahnya hilang.
- Export saat ada stiker merah: muncul pertanyaan dulu. Ganti kertas dan bahasa. Buka pemeriksaan garis potong.

**Perbaikan temuan 2026-11-05 (sudah dibuktikan tanpa mouse dan keyboard, belum dicoba dengan tangan):**

- Ketik 62,5 di kotak lebar lalu Susun otomatis: lebar 62,5 mm. Ketik abc: kotaknya merah dan lebar tetap.
- Ketik lebar baru lalu langsung Ctrl+L tanpa pindah kotak: angka baru dipakai.
- Naikkan Margin: stiker yang keluar margin langsung merah. Ketik abc di Margin lalu Susun otomatis: muncul pesan dan kursor pindah ke kotak itu.
- Buka foto miring dari HP: tegak di layar, preview JPG, dan PDF.
- Hapus atau ganti nama file gambar sesudah dimuat, lalu ubah lebar dan Susun otomatis: tetap berhasil dengan gambar yang sama.
- Buka file dari halaman sambutan: lembar langsung pas layar.
- Delete pada satu stiker: jumlah pcs turun, dan Susun otomatis tidak mengembalikannya.
- Menu Lainnya pada desain PDF: Rapikan gambar mati, dan tooltipnya menjelaskan alasannya. Pada desain JPG: menu tertutup saat jendela Rapikan terbuka.
- Hapus desain yang lebih besar dari kertas: peringatannya hilang.
- Salin stiker dari lembar 3, kosongkan lembar 1, lalu Ctrl+V: salinan masuk ke lembar yang sama dengan stiker asalnya.
- Export ke PDF yang masih terbuka di program lain: pesannya mudah dipahami. Preview JPG saat ada stiker merah: bertanya dulu, dan bingkai merah tidak ikut di JPG.
- Pilih satu stiker, lalu Ctrl+klik area kosong di lembar lain: 4 titik tetap di stiker itu.
- Tahan Spasi atau tombol tengah mouse, Alt+Tab, lalu kembali: tampilan tidak mengikuti kursor. Tekan tombol tengah saat menarik stiker: tarikan batal, tampilan bergeser, dan berhenti saat tombol dilepas.
- Ganti bahasa: penanda garis di daftar desain ikut berubah.
- Zoom 400% pada stiker bergaris potong: gambar pas dengan garisnya, dan tetap tajam.

**CDR dan AI (2026-11-07; logikanya sudah dibuktikan tanpa mouse dan keyboard, belum dicoba dengan tangan):**

- Tambah file CDR: baris status menulis "Membaca …", lalu desainnya muncul tanpa CorelDRAW, dengan peringatan "CDR ditempel sebagai gambar RGB". Bandingkan warna dan bentuknya dengan file aslinya.
- Ubah lebar desain CDR, lalu Susun otomatis: ukurannya berubah dan gambarnya tetap tajam.
- Tambah AI yang disimpan tanpa PDF-compatible: muncul pesan untuk menyimpan ulang dengan "Create PDF Compatible File", dan tidak ada desain bertuliskan kalimat dari Illustrator.

**Salin, pindah, dan urungkan (2026-11-06; logikanya sudah dibuktikan tanpa mouse dan keyboard, belum dicoba dengan tangan):**

- Alt + seret dari lembar 1 ke lembar 2, lalu dari lembar 2 ke lembar 1: stiker asal tetap, salinannya terpilih di lembar tujuan. Ctrl+Z menghapus salinan, Ctrl+Y mengembalikannya.
- Seret stiker terakhir di sebuah lembar ke lembar lain: lembar asal hilang. Ctrl+Z mengembalikan lembar dan stikernya.
- Tekan lalu lepas Alt selagi menarik: baris status berganti antara Salin dan Pindah. Esc membatalkan tanpa meninggalkan salinan.
- Alat Zoom (Z): klik memperbesar, Alt + klik memperkecil. Kembali ke alat Pindah: Alt + seret tetap menyalin.
- Alt + seret, Alt+Tab ke jendela lain, kembali ke Printech, lalu tekan Alt: menu terbuka seperti biasa.
- Ctrl+D enam kali berturut-turut: salinan berjajar ke kanan, lalu turun ke baris bawah. Ctrl+Z enam kali mengembalikan satu stiker.
- Tarik file gambar dari Explorer ke jendela: desainnya bertambah.

**Tampilan:**

- Ctrl + roda di atas stiker pada 6 lembar: saat diperbesar, jumlah lembar per baris tetap dan stiker di bawah kursor tidak melompat.
- Perkecil jendela: jumlah lembar per baris berkurang, tanpa bilah geser mendatar.
- Daftar 8 desain atau lebih: kata pcs utuh.
- Buka aplikasi tanpa file: halaman sambutan tanpa panel kiri. Tarik file dan Buka Folder berfungsi.
- Menu Tampilan, Tanda registrasi (bawaan mati): centang, lalu Ctrl+Z mengembalikannya.

## 11. Menjalankan

- **Untuk presentasi prototipe:** Printech dijalankan dari laptop tim yang terpasang .NET 10 SDK ([README, bagian Untuk pengembang](../README.md#untuk-pengembang)). Zip untuk operator di bawah baru dipakai bila Printech dipasang di PC toko.
- **Untuk operator (tanpa memasang .NET):**
  1. Salin `Printech-win-x64.zip` (±80 MB, termasuk alat libcdr) ke PC operator.
  2. Klik kanan zip, pilih Properties, centang **Unblock**, lalu OK. Langkah ini mencegah peringatan Windows SmartScreen, karena aplikasinya belum ditandatangani.
  3. Ekstrak (±189 MB), lalu jalankan `Printech\StickerLayout.exe`. Bila SmartScreen tetap muncul: **More info**, lalu **Run anyway**.
  4. Butuh Windows 10/11 64-bit (belum diuji di PC operator). Versi rilis: jendela muncul ±0,7 detik, memori ±124 MB.
- **Membuat zip untuk operator:** `dotnet publish StickerLayout -c Release -r win-x64 --self-contained -o publish\Printech`, lalu zip folder `publish\Printech`. Folder `publish/` tidak ikut git.
- **Menjalankan aplikasi:** `dotnet run --project StickerLayout` (perlu .NET 10). File juga bisa dibuka sebagai argumen, misalnya lewat "Open with" atau dengan menyeret file ke ikon aplikasi.
- **File contoh:** folder `samples/` (circle.png, star.jpg, badge.svg, logo.pdf, logo.ai, logo-on-blue.jpg untuk mencoba Rapikan, dan lainnya).
- **Pemeriksaan otomatis:** `StickerLayout\bin\Debug\net10.0-windows\StickerLayout.exe --selfcheck samples`. Hasilnya ditulis ke `samples/selfcheck.txt`. Skenario garis potong (S1–S9, S11–S23) dan stabilitas (P1–P5) ikut dijalankan; bila satu saja gagal, hasilnya exit code 1.
