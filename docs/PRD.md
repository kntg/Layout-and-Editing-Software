# PRD — Printech (Layout Stiker Print & Cut)

Status: Draf 4 · Tanggal: 2026-10-17 · Nama sementara: "Printech" (dikonfirmasi 2026-10-13).

## 1. Tujuan

Ini alat internal toko. Operator mengubah file dari customer menjadi layout stiker yang siap dicetak dan dipotong. Layout harus hemat bahan dan warnanya akurat.

Tahap sekarang: prototipe → cek kualitas → uji oleh operator. Status "production" tercapai bila tidak ada error lagi.

**Cakupan prototipe** (2026-10-17): automasi layout saja, yaitu import file, garis potong otomatis, susun otomatis, dan export PDF/preview JPG. Estimasi pengerjaan 2 minggu. Dokumentasi disimpan di repo GitHub ini.

**Tujuan utama: mempercepat proses** (konfirmasi 2026-10-14). Saat ini Photoshop dipakai hanya untuk merapikan gambar, bukan untuk layout, dan pekerjaan itu memakan banyak waktu. Printech tidak bertujuan mengurangi pemakaian Adobe.

**Cakupan:** stiker saja (kiss-cut dan die-cut). Nametag, kartu, dan dokumen tidak termasuk.

## 2. Pengguna

- Ada dua peran: operator layout dan operator mesin. Di kantor pusat keduanya dipegang orang berbeda; di cabang bisa dipegang orang yang sama.
- Software dipakai internal perusahaan. Ini terhitung pemakaian komersial, tetapi software tidak didistribusikan ke luar.

## 3. Observasi AP 55 (PPT leader proyek, 2026-10-14)

- Cabang AP 55 (Angkasa Putra). Tiga operator memegang semua mesin. Mesin cetak stiker: printer laser **Develop**; mesin potong: **Graphtec**.
- Alur stiker: file masuk lewat WhatsApp, email, atau flashdisk → operator mengecek file → layout garis potong dan posisi cetak di CorelDRAW → cetak di Develop → kiss-cut di Graphtec.
- Operator mengirim tampilan file ke customer untuk dikonfirmasi ("apakah sesuai?") sebelum bon dicetak.
- Photoshop dipakai untuk merapikan gambar. File AI diubah dulu ke PDF, lalu dilanjutkan di CorelDRAW.
- Produk teratas: stiker vinyl kiss-cut (offline) dan stiker dalam jumlah besar (online).
- Kertas stiker di Develop: A3+ 32×48 cm.

## 4. Alur di Printech

1. Customer menyerahkan file: 80% CorelDRAW & PDF, 20% AI & PNG/JPG. SVG jarang. Barang fisik belum diterima untuk cutting.
2. Operator menambahkan file. Aplikasi membuat garis kiss-cut dan die-cut secara otomatis.
3. Operator mengatur lebar dan jumlah per desain, ukuran kertas, dan margin tepi, lalu menjalankan "Susun otomatis". Jika satu lembar penuh, lembar berikutnya dibuat otomatis.
4. Operator mengedit hasil susunan bila perlu, lalu export ke PDF.
5. PDF dibuka di CorelDRAW atau Illustrator. Reg mark dan pengiriman ke mesin potong dilakukan lewat Cutting Master 5 (Graphtec), dengan garis potong dipilih berdasarkan warna.

## 5. Kebutuhan fungsional

| ID | Kebutuhan | Status v2 |
| ---- | ----------- | ----------- |
| F1 | Input PDF, AI (PDF-compatible), CDR (lewat CorelDRAW yang terpasang, di proses worker terpisah yang dihentikan setelah 30 detik bila CorelDRAW tidak merespons), PNG, JPG, SVG. PDF multi-halaman = satu desain per halaman | Ada. Jalur CDR belum diuji |
| F2 | Crop otomatis ke batas artwork (margin transparan). Latar gambar opak tidak dibuang otomatis. PDF memakai ukuran asli. Lebar dan jumlah bisa diatur per desain | Ada |
| F3 | Garis kiss-cut dan die-cut, masing-masing dengan offset dan penghalus (smooth) sendiri. Lubang asli artwork yang tetap terbuka setelah offset (stiker donat) mendapat garis dalam kiss dan die (2026-10-21) | Ada |
| F4 | Kertas: preset A0–A6, **A3+ 320×480 (default)**, SRA3, SRA4, custom, dan tombol putar orientasi | Ada |
| F5 | Margin tepi kertas bisa diatur (area kosong untuk reg mark/printer) | Ada |
| F6 | Susun otomatis ke banyak lembar; lembar baru dibuat otomatis | Ada |
| F7 | Edit hasil susunan: seret, putar 90°, hapus, tombol Delete/R/panah. Muncul peringatan bila tumpang tindih atau keluar margin | Ada |
| F8 | Zoom ala Word: Ctrl+roda (titik di bawah kursor tetap), persen, tombol "Pas layar". Alat Hand/Zoom, tahan Spasi atau tombol tengah mouse untuk geser tampilan | Ada |
| F9 | Export PDF: artwork tetap vektor dengan warna asli. Kiss-cut = magenta 100%, die-cut = cyan 100%, DeviceCMYK murni (tanpa RGB/sRGB atau profil ICC; dicek otomatis di S8) | Ada |
| F10 | Bahasa UI: Indonesia dan English | Ada |
| F11 | UI gaya Photoshop: tema gelap, menu, options bar, batang alat, panel lipat (Desain dengan thumbnail, Pilihan, Kertas, Garis potong), status bar dengan meter bahan dan peringatan, drag & drop file, file langsung disusun saat ditambahkan | Ada |
| F12 | Resolusi cetak (dpi) tiap desain yang ditempel sebagai gambar. Merah dan masuk peringatan bila di bawah 150 dpi (target 300 dpi). Gunanya menangkap foto terkompres dari WhatsApp sebelum dicetak | Ada |
| F13 | Export preview JPG (Ctrl+Shift+E): satu JPG per lembar, sisi panjang ±2000 px, untuk konfirmasi ke customer lewat WhatsApp | Ada |
| F14 | Shortcut ala CorelDRAW: Ctrl+I = tambah file, F4 / Shift+F4 = pas layar (selain Ctrl+O dan Ctrl+0) | Ada |
| F15 | Rapikan gambar (pengganti langkah Photoshop), untuk desain yang ditempel sebagai gambar: hapus latar otomatis, penghapus klik, toleransi warna, kikis tepi 0–3 px, urungkan dan kembalikan. Dibuka lewat ikon penghapus atau dobel-klik thumbnail. Garis potong dihitung ulang dari hasilnya | Ada. Di luar cakupan prototipe: tidak dikembangkan |
| F16 | Garis potong dengan node sedikit dan kurva mulus untuk pisau Graphtec: tanpa sudut tajam; slider "Halus" sekarang juga menghaluskan bentuk sehingga node berkurang (lihat Keputusan) | Ada |
| F17 | Bleed untuk gambar PNG/JPG (opsional, 0 = mati): warna tepi diperluas sampai bleed mm di luar garis kiss, supaya pergeseran pisau tidak menyisakan tepi putih. Maksimal sama dengan jarak antar stiker | Ada |
| F18 | Indikator bahan: "Bahan terpakai" dihitung terhadap area kertas di dalam margin, berwarna hijau/kuning/merah menurut target bentuk reguler (70–85%, hijau ≥ 75%) atau irregular (55–70%, hijau ≥ 60%). Yield per lembar ("24 pcs / lembar A3+") dan sisa bahan | Ada |
| F19 | Snap magnet: stiker yang digeser atau diputar pindah ke posisi kosong terdekat (tidak bertabrakan, jarak antar stiker tetap). Preset bahan dihapus 2026-10-23 (tidak diperlukan, masukan user/leader) | Ada |
| F20 | Print Ticket: PDF A4 1 halaman berisi total lembar, total pcs, efisiensi bahan, rincian per lembar dan per desain, serta pengaturan potong, untuk lampiran nota kasir/SPK | Ada |
| F22 | Undo/redo global: Ctrl+Z, Ctrl+Y, Ctrl+Shift+Z, dan menu Edit. Mencakup tambah/hapus desain, susun, penuhi lembar, geser, putar, hapus stiker, Rapikan, editor garis potong, ganti kertas. Maksimal 30 langkah. Di kotak teks, Ctrl+Z tetap undo teks | Ada (2026-10-21) |
| F21 | Penuhi lembar (tombol Compact): ruang kosong di semua lembar diisi salinan desain bergiliran sampai penuh, karena customer membayar per lembar | Ada (2026-10-21) |
| F23 | Editor garis potong (ikon pensil di daftar desain): garis kiss dan die menjadi node yang bisa digeser, ditambah (klik di garis atau kurva, bentuk tidak berubah), dan dihapus (Delete). Sejak 2026-10-23 seperti CorelDRAW: segmen lurus atau kurva Bezier, node terpilih menampilkan handle untuk mengatur lengkung, tombol Jadikan kurva/Jadikan garis dan Node halus/Node sudut. Slider Simplify (toleransi 0,05–0,5 mm, Kompleks–Sederhana), urungkan/ulangi di editor, dan Reset otomatis. Berlaku untuk semua salinan desain; daftar desain menandai "garis manual" | Ada (2026-10-21, kurva 2026-10-23) |
| F24 | Smart select di editor garis potong (kotak centang di panel kiri): klik objek, garis kiss dan die mengikuti bentuknya dengan jarak offset (lekukan diikuti, sudut luar bulat); Shift+klik menambah objek; klik di dalam lubang menambah atau menghapus garis dalam, termasuk lubang yang lebih sempit dari offset; slider Toleransi mengabaikan piksel tipis seperti watermark. Tanpa AI | Ada (2026-10-22) |
| F25 | Desain kotak otomatis: foto, JPG/PNG opak, PNG kotak, dan PDF berlatar kotak putih mendapat garis kotak bersudut tajam (4 node lurus) sejauh offset, tanpa operator memilih apa pun | Ada (2026-10-23) |
| F26 | Preset bentuk garis potong di editor: kotak, kotak sudut bulat, lingkaran, oval, segitiga, bintang, hati, segi enam. Bentuk mengelilingi artwork sejauh offset (artwork tidak terpotong), die memakai bentuk yang sama, dan bentuk ikut menyesuaikan bila offset atau lebar diubah. Daftar desain menampilkan nama bentuk | Ada (2026-10-23) |
| F27 | Warna border per desain (editor garis potong): area antara artwork dan garis kiss dicetak dengan warna CMYK pilihan (merah, hitam, biru, kuning, hijau, atau CMYK kustom), diteruskan sejauh bleed (bila bleed 0: 1 mm) di luar garis. Tampil di layar, preview JPG, dan PDF | Ada (2026-10-23) |

## 6. Keputusan

- **Platform:** Windows, C#/.NET 10, WPF.
- **Reg mark:** tidak dibuat oleh aplikasi. Reg mark dibuat oleh Cutting Master 5 di CorelDRAW/Illustrator, yang mendukung FCX2000 dan CE7000. Aplikasi hanya menyisakan margin tepi kertas (default 10 mm, Unverified).
- **Batas aplikasi** (keputusan leader proyek, 2026-10-14): aplikasi hanya membuat layout. Pengaturan dan urusan mesin tetap dikerjakan operator; profil mesin dihapus dari aplikasi.
- **Cutting Master 5:** bisa mengatur kondisi potong, urutan, dan potong/tidak per warna atau per layer.
- **Export:** PDF menggantikan SVG dari v1. Alasannya: vektor dan CMYK tetap terjaga, dan satu file bisa berisi banyak halaman.
- **Nesting:** greedy first-fit di grid 1 mm. Margin dicek terhadap garis potong terluar, bukan kotak stiker. Diukur 2026-10-17, dibanding grid 2 mm dan margin per kotak:
  - job 130 stiker A4: 7 lembar (sebelumnya 8), pemakaian bahan 51,6% (sebelumnya 45,2%), susun ±85 ms (Release);
  - jarak nyata ke stiker terdekat (diminta 2 mm): median 3,2 mm, maksimal 4,4 mm (sebelumnya 4,1 dan 5,5 mm), tetap 0 tabrakan;
  - jarak garis die ke tepi kertas (margin 10 mm): minimal 10,5 mm (sebelumnya 12,5 mm).
- **Ringan:** render tampilan lewat CPU, bukan GPU. Diukur 2026-10-17:
  - RAM menurut Task Manager (private working set): 35 MB tanpa file, 52–57 MB dengan 4 desain. Target spesifikasi ±120 MB tercapai.
  - Total working set 104–153 MB. Angka ini ikut menghitung DLL Windows yang dipakai bersama, termasuk driver grafis Intel ±70 MB yang tetap dimuat WPF walau render lewat CPU. Laporan sebelumnya (136–153 MB, "di atas target") memakai angka total ini.
  - CPU saat diam 0%.
  - Build operator dikompilasi ReadyToRun, sehingga waktu CPU saat start ±setengahnya.
  - Ganti zoom 0,1–0,4 detik CPU. Layar memakai salinan gambar maksimal 1600 px (export tetap resolusi asli): dengan satu gambar 4000×3000 px RAM 186 MB, jendela Rapikan 310 MB, kembali ke 267 MB setelah ditutup. Hapus latar 4000×3000 px: 0,5 detik.
  - File besar (temuan D4, diperbaiki 2026-10-21): salinan layar selalu salinan piksel mandiri, dan setiap selesai menyusun aplikasi menjalankan GC penuh (±40 ms) supaya bitmap resolusi penuh hasil muat langsung dilepas. RAM Task Manager: JPG 24 MP 226 → 41 MB, dua PDF besar 326 → 53 MB, semua 19 file uji 279 → 84 MB. Puncak sesaat saat memuat file besar tetap tinggi (JPG 24 MP ±200 MB), karena gambar didecode resolusi penuh.
- **Aman:** tanpa koneksi jaringan. File asli customer hanya dibaca. Yang ditulis hanya PDF export (lokasi dipilih operator) dan folder temp milik aplikasi (salinan PDF/AI dan hasil konversi CDR), yang dihapus saat aplikasi ditutup. Bila aplikasi dihentikan paksa, folder itu tertinggal di `%TEMP%\Printech`. Error tak terduga tampil sebagai pesan, aplikasi tetap jalan. PDFium versi terbaru (build 8100, 2026) dipakai untuk membaca PDF customer.
- **Kertas default A3+ 320×480 mm** (kertas stiker Develop di AP 55). F4/folio tidak ditambahkan karena cakupan hanya stiker.
- **Ambang resolusi 150 dpi** masih tebakan awal (target riset 300 ppi); disesuaikan setelah melihat hasil cetak Develop.
- **Latar gambar tidak dibuang otomatis** (masukan leader 2026-10-21, menggantikan aturan putih D10). Artwork = piksel tidak transparan (alpha > 16). Gambar opak (JPG, PNG tanpa transparansi), termasuk yang berlatar putih, dipakai utuh dan garis potong mengelilingi kotak gambar. Auto-crop tetap membuang margin transparan. Untuk garis yang mengikuti bentuk objek, operator membuang latar lewat Rapikan gambar atau mengatur titik garis potong. Ini juga menjawab pertanyaan kotak putih di PDF (D7): kotaknya dipertahankan.
- **Garis dalam untuk lubang asli (stiker donat)** (masukan leader 2026-10-21). Spesifikasi awal leader hanya memotong kontur terluar; untuk stiker donat garis kiss dan die juga dibuat di dalam lubang, digeser ke tengah lubang sejauh offset. Hanya rongga yang seluruhnya berada di lubang asli artwork (latar transparan yang tidak terhubung ke tepi gambar) yang dipotong (`Loader.KeepHoles`); rongga dari celah yang tertutup offset tetap diisi. Lubang die ikut tertutup bila lubang kiss tertutup, karena die harus membungkus kiss. Grid nesting tetap memakai bentuk padat, jadi stiker lain tidak ditaruh di dalam lubang. Dikonfirmasi user 2026-10-22: aturan ini tetap untuk garis otomatis; lubang yang lebih sempit dari offset dipotong lewat klik smart select (F24).
- **Compact = penuhi lembar** (masukan leader 2026-10-21). Customer membayar per lembar, jadi tombol Compact mengisi ruang kosong di semua lembar dengan salinan desain secara bergiliran sampai tidak ada yang muat (`Nest.Fill`, grid dan aturan jarak sama dengan susun otomatis). Jumlah per desain ikut hasil; desain yang sama sekali tidak muat tetap pada jumlahnya. Fungsi lama (memangkas tinggi lembar terakhir untuk bahan roll) dihapus karena AP 55 memakai kertas lembaran.
- **Editor garis potong: node lurus dan kurva** (masukan leader 2026-10-21, contoh Roboflow Annotate; node kurva seperti CorelDRAW sejak 2026-10-23, [spesifikasi](superpowers/specs/2026-10-23-bentuk-garis-potong-design.md)). Garis otomatis tetap kurva Bezier dari Potrace.
  - **Node dari kurva otomatis:** kurva dipecah tiap ±0,05 mm, lalu di-fit cubic Bezier (metode Schneider) dengan galat maksimal sebesar toleransi slider Simplify. Titik sudut garis (belokan > 45°, misalnya sudut kotak) tetap persis menjadi node sudut. Bagian yang lurus dalam toleransi menjadi segmen lurus.
  - **Hasil selfcheck S12:** simpangan 0,185 mm pada toleransi 0,2 mm. Node logo kasar 21/17/10 pada toleransi 0,05/0,2/0,5 mm. Kiss png-transparent.png turun dari 31 titik polygon (versi 2026-10-21) menjadi 14 node kurva.
  - **Data:** hanya desain yang diedit yang menyimpan node (`Sticker.ManualKiss`/`ManualDie`, mm relatif terhadap artwork). Tiap node menyimpan titik, handle masuk, handle keluar, dan tanda halus.
  - **Export:** segmen lurus ditulis sebagai garis (`l`) dan segmen kurva sebagai Bezier (`c`), dengan warna CMYK yang sama.

  Aturan:
  - Klik di garis atau kurva menambah node dengan membelah segmen (de Casteljau), sehingga bentuk tidak berubah. Menghapus node menyambung kedua tetangganya memakai handle luar mereka. Node halus menjaga kedua handle tetap segaris saat salah satunya diseret.
  - Node dan handle tidak bisa keluar dari kotak kerja. Kotak kerja adalah kotak desain otomatis, atau lebih besar bila garis/bentuk keluar darinya. Grid nesting dan luas dihitung ulang dari garis; kotak desain membesar mengikuti titik dan handle terjauh.
  - Garis manual tidak berubah bila offset, Halus, atau Simplify diubah. Bila lebar diubah, garis diskala bersama artwork. Rapikan gambar mengembalikan garis otomatis, karena artwork-nya berubah.
  - Simpan dinonaktifkan selama garis kiss keluar dari die. Editor hanya menampilkan garis yang aktif; garis yang sedang mati kembali otomatis. Bila die manual sedangkan kiss otomatis (kiss diaktifkan belakangan), kiss dipangkas ke dalam die.
  - Simpan tanpa mengedit, atau Reset otomatis lalu Simpan, mengembalikan desain ke garis otomatis.
- **Smart select tanpa AI** (2026-10-22, [spesifikasi](superpowers/specs/2026-10-22-smart-select-design.md)). Klik objek di editor garis potong: objek = piksel dengan alpha > Toleransi (awal 64) pada gambar bertransparansi, atau piksel yang beda warnanya dengan warna tepi gambar > Toleransi (awal 32, ukuran sama dengan Rapikan) pada gambar opak, yang tersambung dengan titik klik. Garis = objek dilebarkan sejauh offset tanpa Halus, jadi lekukan diikuti dan sudut luar membulat. Lubang objek yang lebar (jari-jari ruang ≥ offset + 0,5 mm) langsung dapat garis dalam; lubang yang lebih sempit dipotong lewat klik di dalam lubang dengan jarak min(offset, jari-jari ruang − 0,5 mm), minimal min(offset, 0,3 mm). Die harus berada di dalam garis dalam kiss; bila tidak muat, lubang hanya di-kiss-cut. Hasilnya garis manual biasa (node kurva sejak 2026-10-23). Garis otomatis sengaja tidak berubah: lubang huruf berukuran mirip lubang donat (P dan R pada teks setinggi 8,8 mm: jari-jari ruang 1,04–1,14 mm; donat uji 1,29 mm), jadi memotong semua lubang otomatis akan membuat lingkaran potong ±1 mm di setiap huruf. Angka 0,5 dan 0,3 mm dikalibrasi di Graphtec operator.
- **Desain kotak otomatis** (2026-10-23). Silhouette dianggap kotak bila semua piksel tepi mask (6 px/mm) terisi dan isinya ≥ 99,5 %.
  - Garis kiss = kotak artwork ± offset kiss. Garis die = kotak ± offset die, minimal kiss + 1 piksel. Halus tidak dipakai.
  - Kotak bersudut bulat (sudutnya transparan) tetap memakai garis otomatis.
  - Validasi: 4 desain kotak di data uji (jpg-large-24mp, jpg-low-resolution, pdf-multi-page p2, pdf-single-page) dideteksi sama oleh validasi independen. Garisnya = kotak ± offset, selisih maksimal 0,10 mm (A7).
  - Kriteria A1 dan A5 disesuaikan untuk desain kotak: jarak terjauh di sudut kotak = offset × √2, dan garis lurus memang aturannya.
  - Akibat sampingan: jarak garis ke artwork pada desain kotak kini tepat sama dengan offset. Garis yang di bawah batas A4 turun dari 65 ke 43.
- **Preset bentuk** (2026-10-23). Bentuk mengelilingi artwork, dihitung dari hull cembung silhouette 6 px/mm yang diperluas sejauh offset:
  - kotak = bbox ± offset;
  - lingkaran = lingkaran terkecil (Welzl) + offset;
  - oval = elips dengan rasio bbox;
  - segitiga sama sisi dan segi enam beraturan = irisan setengah bidang;
  - bintang (5 sudut, lekuk dalam 0,5 × jari-jari luar) dan hati = skala seragam terkecil (pencarian biner, uji titik di dalam bentuk).

  Aturan:
  - Die memakai bentuk dan pusat yang sama dengan offset die.
  - Hull memakai sudut piksel, sengaja ke arah luar supaya artwork tidak terpotong. Akibatnya bentuk bulat atau miring bisa sampai ±0,6 mm lebih besar dari ukuran minimal (selfcheck S15).
  - Bentuk disimpan per desain (`Sticker.Shape`) dan dihitung ulang setiap kali desain dimuat ulang (offset, lebar, Susun, Rapikan). Begitu node-nya diedit, hasilnya disimpan sebagai garis manual. Simplify tidak berlaku untuk bentuk.
  - Kotak sudut bulat memakai radius 3 mm dan bintang memakai rasio 0,5 sebagai titik awal; nilai ini disesuaikan dengan pilihan operator.
- **Warna border** (masukan 2026-10-23). Disimpan per desain (`Sticker.Border`, CMYK) dan ikut saat desain dimuat ulang.
  - Area = garis kiss (tanpa kiss: die) diperlebar sejauh bleed, atau 1 mm bila bleed 0, supaya pergeseran pisau tidak menyisakan tepi putih. Lubang ikut diperlebar ke dalam; tengah lubang yang dipotong tetap putih. Bagian transparan artwork di dalam garis memperlihatkan warna border.
  - PDF menulis warnanya sebagai CMYK murni (operator `k`), digambar di bawah artwork.
  - Bila warna border dipakai, bleed tidak lagi memperluas warna tepi gambar (perluasan itu akan menutupi border).
  - Tanpa bleed, jarak antar stiker minimal 1 mm supaya perpanjangan warna tidak masuk stiker tetangga.
- **Rapikan gambar hanya untuk raster** (PNG, JPG, SVG, PDF yang gagal dibaca vektor). File vektor (PDF/AI/CDR) tetap dirapikan di CorelDRAW supaya warna CMYK dan vektornya utuh. Rapikan hanya mengubah transparansi, tidak menggambar ulang piksel.
- **Reduksi node garis potong (2026-10-16).** RDP literal tidak dipakai untuk garis otomatis (editor garis potong memakai fit Bezier sejak 2026-10-23, lihat di atas). Potrace sudah menghasilkan kurva Bezier yang dioptimasi; RDP hanya bekerja pada garis lurus, jadi kurva harus dipecah dulu. Hasil ukur: teks butuh 117 titik garis lurus untuk akurasi 0,1 mm, sedangkan Bezier cukup 72 node. Yang dipakai:
  - Tuning Potrace: `AlphaMax` 1,3334 (tidak ada sudut tajam, pisau tidak berbelok mendadak) dan `OptTolerance` 1 px = 0,17 mm (lebih banyak segmen kurva digabung).
  - Penghalusan bentuk sebelum trace: blur dengan sigma = nilai Halus (3 lintasan box blur, mendekati Gaussian), lalu threshold. Lekukan kecil hilang. Halus 1–2 mm paling efektif; di atas 2 mm node bisa naik lagi.
  - Penjaga jarak: di mask, penghalusan tidak boleh membawa garis lebih dekat dari offset - 0,3 mm ke artwork (jari-jari dibulatkan ke atas sejak 2026-10-18). Garis die selalu membungkus garis kiss, minimal 1 piksel (0,17 mm) di luarnya.
  - Diukur 2026-10-18 per titik garis terhadap geometri asli file ([validasi-garis-potong-nesting.md](../Printech-TestData/02-expected/validasi-garis-potong-nesting.md)):
    - median jarak = offset −0,19 s.d. +0,02 mm;
    - setelah kurva Potrace, garis bisa sampai 0,13 mm lebih dekat dari offset - 0,3 mm. Ini terjadi di sudut lancip dan pada offset kecil (galat mask 6 px/mm).
  - Hasil di app (kiss 2/1 mm, die 4/2 mm), node kiss/die sebelum -> sesudah: rough.jpg 64/43 -> 16/13 (-73%), lingkaran 11/10 -> 3/4 (-67%), logo.pdf 21/15 -> 8/7 (-58%), bintang 29/27 -> 15/17 (-43%), badge 9/8 -> 8/8. Sudut tajam 2 -> 0. Sejak 2026-10-18 (penjaga dibulatkan ke atas, die minimal 1 px di luar kiss) rough.jpg 17/16. Muat per desain 20-70 ms, stiker 300 mm ±1 detik (Release).
  - Skenario uji S1–S16 beserta hasilnya: [UJI-GARIS-POTONG.md](UJI-GARIS-POTONG.md).
- **Stabilitas dan performa** (2026-10-17, spesifikasi leader bagian 2):
  - CorelDRAW COM berjalan di proses worker (exe yang sama dengan argumen `--cdr2pdf`), bukan di proses utama. Selama menunggu, UI tetap jalan. Lewat 30 detik, worker dihentikan dan operator mendapat pesan; Printech tetap jalan. CorelDRAW sendiri tidak dimatikan supaya pekerjaan operator yang belum disimpan aman.
  - 3 percobaan nesting dan hapus latar dihitung paralel dengan `Parallel.For`. Spesifikasi menyebut `Parallel.ForEachAsync`, tetapi API itu untuk kerja async (I/O), sedangkan kerja ini murni CPU. Hasilnya identik bit per bit dengan versi berurutan.
  - Hasil ukur (Release, CPU 12 thread): nesting 130 stiker 57 → 43 ms. Gambar 4000×3000: hapus latar 119 → 97 ms (flood fill tetap berurutan), kikis tepi 103 → 31 ms, tampilan 62 → 20 ms. RAM saat diam tidak berubah (total working set versi lama 148–152 MB, versi baru 148–153 MB, 4 kali ukur).
  - Hapus latar dan penghapus klik di jendela Rapikan dihitung di background, jadi jendela tetap merespons. Hanya pembaruan tampilan (±20–70 ms) yang jalan di UI thread.
  - Sejak 2026-10-19, file PDF/AI disalin ke folder temp saat dimuat. Export dan Susun memakai salinan itu, jadi artwork tetap sama dengan dasar garis potong walau file asli diubah, dipindah, atau dihapus. Untuk memakai isi baru, tambahkan file itu lagi.
  - Sejak 2026-10-19, hasil konversi CDR disimpan per versi file (waktu ubah + ukuran). CDR yang diedit lalu ditambah ulang dikonversi lagi.
  - Skenario uji P1–P10: [UJI-STABILITAS.md](UJI-STABILITAS.md).
- **Prepress** (2026-10-17, spesifikasi leader bagian 3):
  - Garis potong ditulis DeviceCMYK murni lewat operator `K`: kiss `0 1 0 0`, die `1 0 0 0`. Tidak ada operator RGB, ruang warna ICC, atau OutputIntent, jadi tidak ada konversi ke sRGB. Operator lain yang ada hanya `0 g`/`0 G` (DeviceGray), yang ditulis PDFsharp sebelum artwork vektor ditempel; itu bukan warna garis potong.
  - Bleed: gambar bleed dibuat saat desain dimuat (maksimal 300 dpi), lalu dipakai untuk layar, preview JPG, dan PDF. Area yang dicetak = garis kiss (tanpa kiss: garis die) diperlebar sebesar bleed. File vektor tidak diberi bleed.
  - Bleed ikut mengisi border putih antara artwork dan garis kiss. Pakai bersama offset kecil (0–0,5 mm) untuk stiker tanpa border.
- **Indikator dan UX operator** (2026-10-17, spesifikasi leader bagian 4):
  - Bentuk reguler atau irregular ditentukan otomatis: bila rata-rata isi bentuk terhadap kotaknya (ditimbang luas) ≥ 75%, dihitung reguler (persegi ±1, lingkaran ±0,79, bintang ±0,57). Batas hijau irregular (60%) tidak disebut di spesifikasi; dipakai aturan yang sama dengan reguler, yaitu batas bawah + 5 poin.
  - Snap memakai grid nesting 1 mm (posisi kosong terdekat), bukan NFP poligon. Geser dengan panah keyboard tetap bebas. Bila tidak ada tempat kosong, stiker tetap di posisinya dan ditandai merah.
  - Preset bahan dihapus 2026-10-23: menurut user/leader tidak diperlukan, karena offset, Halus, margin, dan jarak diatur langsung. Nilai awalnya tetap dicatat di [RESEARCH.md](RESEARCH.md) bagian 13 sebagai riwayat riset.
- **Ditunda sampai ada kebutuhan nyata:** hapus latar foto dengan AI dan memperbesar gambar pecah. Untuk gambar pecah, cara tercepat tetap meminta file asli ke customer; angka dpi merah memberi tahu kapan perlu.
- **Tidak dibangun:** hide copy, simpan proyek, reg mark buatan aplikasi, dan mode nametag/kartu.

## 7. Dependensi dan lisensi

| Paket | Fungsi | Lisensi |
| --- | --- | --- |
| SharpVectors.Wpf 1.8.6 | Membaca SVG | BSD-3 |
| BitmapToVector 1.16.3 | Potrace, untuk garis potong | GPL-3.0. Aman untuk pemakaian internal; jangan didistribusikan ke luar perusahaan |
| bblanchon.PDFium.Win32 157.0.8100 | Render PDF/AI (PDFium, hanya binary Windows) | Apache-2.0 |
| PDFsharp 6.2.4 | Menulis PDF | MIT |

## 8. Batasan yang diketahui

- Jalur CDR lewat COM CorelDRAW belum diuji, karena CorelDRAW tidak terpasang di PC pengembangan.
- PDF yang diputar (/Rotate) atau tidak bisa dibaca PDFsharp akan ditempel sebagai gambar (±254 dpi, RGB). Termasuk PDF terenkripsi yang bisa dibuka tetapi gagal ditempel: saat dimuat, halaman dicoba ditempel seperti saat export (temuan D8, 2026-10-21). Aplikasi memberi peringatan di status bar sejak desain dimuat ("… PDF ditempel sebagai gambar"; sebelum 2026-10-18 hanya saat impor PDFsharp gagal ketika export).
- SVG di-export sebagai gambar ±300 dpi.
- Yang dipotong: kontur luar, ditambah lubang asli artwork yang tetap terbuka setelah offset dan Halus (stiker donat). Rongga yang terbentuk saat offset menutup celah artwork (misalnya huruf C besar) dan lubang yang tertutup offset tidak dipotong. Garis dalam tidak bisa dimatikan per desain; hapus lewat editor garis potong bila tidak diinginkan. Lubang yang lebih sempit dari offset bisa ditambahkan lewat smart select (klik di dalam lubang).
- Nesting belum optimal (greedy).
- Stiker belum bisa dipindah antar-lembar. Rotasi hanya kelipatan 90°.
- Warna di layar hanya preview. Akurasi warna dijaga di file export.
- Rapikan gambar: latar foto yang ramai (bukan warna polos) belum bisa dihapus otomatis; gambar di atas 4000 px diperkecil ke 4000 px saat dirapikan.
- Reduksi node: target 60-80% tercapai pada logo kasar dan bentuk bulat. Teks dan bintang turun 33-59% karena sudutnya adalah detail asli. Logo dengan gelombang tepi yang nyata (Printech-TestData `png-rough-edge.png`) hanya turun 54%. Di area cekung garis bisa bergeser keluar sampai ±0,5 mm (kiss) atau ±0,7 mm (die): border sedikit lebih lebar. Halus 0 mematikan penghalusan bentuk.
- CDR: batas 30 detik belum dikalibrasi di PC operator. Bila CorelDRAW menggantung karena dialog yang tidak terlihat, tutup CorelDRAW lalu tambahkan file lagi.
- Muat ulang desain dan nesting masih berjalan di UI thread (nesting 130 stiker ±85 ms; desain sangat besar bisa ±1 detik).
- Jarak nyata antar stiker masih lebih lebar dari yang diminta (median +1,2 mm), karena jejak stiker dibulatkan ke grid 1 mm.
- Editor garis potong:
  - Menggeser node dan handle, menambah, dan menghapus node dengan mouse belum diuji otomatis (uji UI tanpa mouse); logikanya diuji selfcheck S12 dan S15.
  - Print Ticket tetap menulis offset dan Halus dari pengaturan, juga untuk desain bergaris manual atau berbentuk preset; nama bentuk tidak ditulis.
- Preset bentuk:
  - Bentuk bisa sampai ±0,6 mm lebih besar dari ukuran minimal, karena hull dihitung dari sudut piksel.
  - Ukuran bentuk belum bisa diketik, dan bentuk belum bisa diputar.
  - Bentuk selalu mengelilingi artwork. Memotong foto menjadi bentuk (crop) belum ada.
  - Dengan bleed aktif, ruang antara artwork dan bentuk ikut diisi warna tepi artwork, sama seperti garis manual yang jauh dari artwork. Pakai warna border bila ruang itu harus berwarna rata.
- Warna border: urungkan/ulangi di dalam editor belum mencakup ganti warna (urungkan global di jendela utama mencakupnya). Print Ticket belum menulis warna border. Warna di layar hanya perkiraan dari CMYK.
- Smart select membaca transparansi atau beda warna dengan latar; objek di foto berlatar ramai belum bisa dipilih. Klik di kanvas belum diuji dengan mouse sungguhan (logikanya diuji selfcheck S13).
- Bleed: tepi JPG yang sangat kasar (kompresi tinggi) bisa menyisakan garis pucat tipis di area bleed; rapikan gambarnya dulu untuk hasil terbaik. Stiker raster sangat besar (±300 mm) dengan bleed dimuat ±1–2 detik lebih lama.

## 9. Pertanyaan terbuka

| Pertanyaan | Ditanyakan ke | Default sampai dijawab |
| --- | --- | --- |
| Format reg mark milik operator | operator (file menyusul) | margin 10 mm |
| Offset, jarak antar-stiker, dan bleed standar toko | operator | kiss 2/1 mm, die 4/2 mm, jarak 2 mm |
| Spesifikasi PC operator | operator | Windows 10/11 64-bit |
| Ambang dpi yang masih bisa diterima | operator, dari hasil cetak | 150 dpi |
| Seberapa sering latar foto atau gambar pecah muncul | operator, setelah uji | hapus latar dengan AI ditunda |
| Garis potong di Cutting Master 5 dikenali lewat warna atau layer | operator | warna (magenta/cyan) |

## 10. Checklist uji operator

1. Tambahkan file CDR. Apakah CorelDRAW terbuka dan desainnya ikut masuk? Berapa detik prosesnya, dan apakah Printech tetap bisa dipakai selama menunggu?
2. Tarik file dari Explorer ke jendela Printech. Coba alat Geser tampilan (H atau tahan Spasi), Zoom (Z), dan shortcut Ctrl+I, F4, R, Delete, panah.
3. Export preview JPG, kirim ke WhatsApp. Apakah cukup jelas untuk konfirmasi ke customer?
4. Masukkan foto yang diterima lewat WhatsApp. Apakah angka dpi merah muncul untuk file yang hasil cetaknya memang pecah?
5. Rapikan 3–5 gambar customer yang biasanya dibersihkan di Photoshop. Berapa menit dibanding Photoshop? Apa yang belum bisa dikerjakan?
6. Export PDF, lalu buka di CorelDRAW. Apakah artwork tajam dan warnanya sama dengan file asli?
7. Di Cutting Master 5, pilih per warna: magenta = kiss-cut, cyan = die-cut. Apakah potongannya sesuai?
8. Cetak di Develop (A3+) dan potong fisik di Graphtec. Berapa mm selisih potongannya? Apakah gerakan pisau mulus pada teks dan logo bertepi kasar? Langkah lengkap: [UJI-GARIS-POTONG.md](UJI-GARIS-POTONG.md) S10.
9. PDF dari customer yang punya latar putih penuh: apakah garis potongnya ikut menjadi kotak? **Terjawab 2026-10-21:** ya, latar tidak dibuang otomatis; operator membuangnya lewat Rapikan atau titik garis potong.
10. Buka PDF di CorelDRAW lalu klik garis kiss dan die: warnanya harus C0 M100 Y0 K0 dan C100 M0 Y0 K0 (CMYK, bukan RGB). Cetak satu desain PNG dengan bleed 2 mm dan offset kiss 0,5 mm, lalu potong: apakah tepi stiker tetap berwarna tanpa garis putih?
11. Apakah offset, halus, dan jarak bawaan cocok untuk bahan yang dicetak? Export Print Ticket lalu lampirkan ke SPK: apakah isinya cukup untuk kasir?
12. Buka Edit garis potong (ikon pensil) pada satu desain. Geser node, tambah node dengan klik di garis atau kurva, hapus node dengan Delete, ubah Simplify, lalu Simpan. Apakah garis di lembar, di PDF (CorelDRAW), dan di Cutting Master 5 sama dengan yang diatur? Apakah pisau Graphtec mulus di setiap node?
13. Buka Edit garis potong pada stiker donat berwatermark, centang Smart select, klik donat lalu klik lubang tengahnya. Apakah garis luar mengabaikan watermark, dan lubang tengah terpotong rapi di Graphtec?
14. Masukkan foto atau JPG kotak (tanpa transparansi). Apakah garis kiss dan die langsung berbentuk kotak bersudut tajam sejauh offset? Setelah dipotong di Graphtec, apakah sudut kotak rapi?
15. Di Edit garis potong, pilih bentuk (lingkaran, oval, segitiga, bintang, hati, segi enam, kotak sudut bulat), lalu Simpan. Apakah bentuknya sesuai keinginan customer, artwork tidak terpotong, dan hasil potong Graphtec mengikuti bentuk? Perlukah radius kotak sudut bulat (3 mm) atau bentuk bintang diubah?
16. Di editor, pilih node, lalu seret handle-nya. Coba juga Jadikan kurva/Jadikan garis dan Node halus/Node sudut. Apakah node dan kurvanya sama saat PDF dibuka di CorelDRAW, dan apakah pisau Graphtec mulus pada kurva?
17. Di editor, pilih warna border (misalnya Merah), Simpan, cetak di Develop, lalu potong di Graphtec. Apakah warnanya sesuai, dan tidak ada tepi putih di garis potong walau pisau sedikit bergeser? Perlukah perpanjangan 1 mm diubah?
18. Catat setiap error atau kejanggalan, lalu kirim ke pengembang.

## 11. Menjalankan

- **Untuk presentasi prototipe:** Printech dijalankan dari laptop tim yang terpasang .NET 10 SDK ([README, Setup untuk developer](../README.md#setup-untuk-developer)). Zip operator di bawah baru dipakai bila Printech dipasang di PC toko.
- **Untuk operator (tanpa memasang .NET):**
  1. Salin `Printech-win-x64.zip` (64 MB) ke PC operator.
  2. Klik kanan zip → Properties → centang **Unblock** → OK. Langkah ini mencegah peringatan Windows SmartScreen, karena exe belum ditandatangani.
  3. Ekstrak (142 MB), lalu jalankan `Printech\StickerLayout.exe`. Bila SmartScreen tetap muncul: **More info → Run anyway**.
  4. Butuh Windows 10/11 64-bit (belum diuji di PC operator). Build Release: jendela muncul ±0,7 detik, RAM ±124 MB.
- **Membuat zip untuk operator:** `dotnet publish StickerLayout -c Release -r win-x64 --self-contained -o publish\Printech`, lalu zip folder `publish\Printech`. Folder `publish/` tidak masuk git.
- **Menjalankan aplikasi:** `dotnet run --project StickerLayout` (perlu .NET 10). File juga bisa dibuka sebagai argumen, misalnya lewat "Open with" atau dengan menyeret file ke ikon aplikasi.
- **File contoh:** folder `samples/` (circle.png, star.jpg, badge.svg, logo.pdf, logo.ai, logo-on-blue.jpg untuk mencoba Rapikan, dan lainnya).
- **Cek otomatis:** `StickerLayout\bin\Debug\net10.0-windows\StickerLayout.exe --selfcheck samples`. Hasilnya ditulis ke `samples/selfcheck.txt`. Skenario garis potong (S1–S9, S11–S16) dan stabilitas (P1–P5) ikut dijalankan; satu saja gagal, exit code 1.
