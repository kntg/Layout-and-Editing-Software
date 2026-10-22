# Smart select di editor garis potong

Sumber: pertanyaan user 2026-10-22 ("tengah donat tidak dapat titik"; "smart select harus bisa membaca setiap bentuk
dan sudut") dan jawaban user di sesi yang sama. Status: cara pakai, mesin, dan pengujian disetujui user per bagian;
menunggu review dokumen ini.

## Latar belakang

Diukur pada file uji user `pngtree-cartoon-donut-sticker-clipart-vector-png-image_12267653.png` (1200 × 1200 px,
RGBA, lebar 50 mm). File ini milik pngtree dan tidak masuk repo.

- Lubang tengah donat lebarnya 4,16 mm, tetapi jari-jari ruang terbesarnya hanya 1,29 mm karena bayangan ikut
  mempersempit. Offset kiss 2 mm + Halus 1 mm menutup lubang itu, jadi garis otomatis hanya punya kontur luar.
  Dicek di mesin Printech: kiss 2/1 menghasilkan 1 kontur; 1/0,5 menghasilkan garis dalam 1,05 mm; 0,5/0
  menghasilkan garis dalam 3,04 mm.
- Watermark "pngtree" semi-transparan (alpha paling tinggi 38) terbaca sebagai artwork, karena artwork = alpha > 16.
  Garis luar melebar sampai 49,5 mm, padahal badan donat ±42 mm. Pada offset kecil, potongan watermark menjadi
  kontur sendiri.
- Editor garis potong (2026-10-21) hanya bisa menggeser, menambah, dan menghapus titik pada kontur yang sudah ada.
  Kontur baru belum bisa dibuat.

## Keputusan

| # | Pertanyaan | Keputusan |
| --- | --- | --- |
| 1 | Jenis smart select | Klik objek tanpa AI. Objek di foto berlatar ramai di luar cakupan |
| 2 | Bentuk garis | Mengikuti bentuk objek dengan jarak offset. Lekukan diikuti tanpa Halus; sudut luar membulat sebesar offset |
| 3 | Lubang sempit | Garis otomatis tidak berubah. Lubang yang lebih sempit dari offset ditambah atau dihapus dengan klik di dalam lubang (Smart select), dengan jarak yang dikecilkan supaya muat. Ini juga keputusan akhir poin 4 spesifikasi 2026-10-21 (stiker donat) |
| 4 | Pendekatan | Pilih area objek dari transparansi atau warna, lalu buat garis berjarak offset. Bukan deteksi tepi, bukan penyaringan garis otomatis |

Alasan keputusan 3: lubang huruf berukuran mirip lubang donat, jadi mesin tidak bisa membedakannya. Diukur pada teks
"PRINTECH" (Arial Bold, seperti selfcheck S3): pada tinggi huruf 8,8 mm, lubang P dan R berjari-jari ruang
1,04–1,14 mm, sedangkan lubang donat 1,29 mm. Bila semua lubang dipotong otomatis, setiap huruf berlubang mendapat
lingkaran potong ±1 mm yang sulit dibuang (weeding) dan berisiko menyobek vinyl.

## Desain

### 1. Cara pakai di editor

- Tombol **Smart select** (ikon tongkat) di panel kiri editor garis potong. Selama aktif, klik di gambar memilih
  objek; saat tidak aktif, klik mengedit titik seperti sekarang.
- **Klik di objek:** garis kiss dan die yang aktif diganti garis yang mengikuti objek itu, masing-masing dengan offset
  sendiri: kontur luar ditambah garis dalam untuk lubang objek yang muat (lihat 2e). Satu klik mengatur kedua garis,
  sehingga die tetap membungkus kiss.
- **Shift+klik di objek:** objek lain ditambahkan, untuk desain dengan beberapa bagian terpisah.
- **Klik di dalam lubang objek:** menambah garis dalam untuk lubang itu, walaupun lubangnya lebih sempit dari offset
  (lihat 2f). Klik lagi di lubang yang sama menghapusnya. Bila objek di sekeliling lubang belum dipilih, objek itu ikut
  dipilih.
- **Slider Toleransi** (0–255): makin tinggi, makin banyak piksel tipis yang diabaikan (watermark, bayangan pudar).
  Untuk gambar tanpa transparansi (JPG), Toleransi menentukan seberapa mirip warna dengan latar yang dianggap latar.
  Nilai awal 64 untuk gambar bertransparansi dan 32 (sama dengan Rapikan) untuk gambar tanpa transparansi. Mengubah
  slider menghitung ulang pilihan (objek dan lubang yang sudah diklik); editan titik sesudahnya ikut terganti dan bisa
  diurungkan.
- **Klik di area kosong:** pesan "Tidak ada objek di titik ini" di bar bawah; garis tidak berubah. Lubang yang
  terlalu kecil: pesan "Lubang terlalu kecil untuk dipotong".
- Hasilnya polygon biasa: titik bisa digeser, ditambah, dan dihapus; Simplify, Urungkan/Ulangi, dan Reset otomatis
  berlaku seperti sekarang. Setelah Simpan, desain bertanda "garis manual".

### 2. Mesin

a. **Sumber piksel:** `Sticker.Preview` (artwork yang sudah di-crop) diskala ke 6 px/mm, resolusi yang sama dengan
   mask garis otomatis.

b. **Piksel objek:** bila gambar punya transparansi (ada piksel dengan alpha < 255), objek = alpha > Toleransi. Bila
   tidak, objek = piksel yang warnanya berbeda dari warna tepi gambar lebih dari Toleransi. Warna tepi dan ukuran beda
   warna sama dengan Rapikan (`CleanImage.BorderColor`; selisih terbesar di antara kanal R, G, B).

c. **Area objek:** piksel objek yang tersambung (4 arah) dengan titik klik. Area dari Shift+klik digabungkan.

d. **Garis luar:** area objek dilebarkan sejauh offset dengan cakram, tanpa Close dan Smooth (Halus). Akibatnya sudut
   luar membulat dan lekukan diikuti; lekukan yang lebih sempit dari 2 × offset tertutup karena memang tidak ada ruang
   untuk garis berjarak offset. Setelah itu Potrace (parameter sama dengan garis otomatis) dan polygon dengan
   Simplify aktif.

e. **Lubang saat klik objek:** lubang asli objek (latar yang dikelilingi objek) dengan jari-jari ruang R ≥ offset +
   0,5 mm mendapat garis dalam berjarak offset. Lubang yang lebih sempit ditutup.

f. **Klik di dalam lubang:** garis dalam berjarak d = min(offset, R − 0,5 mm), dibuat hanya bila d ≥ 0,3 mm
   (lingkaran potong terkecil ±1 mm). Untuk donat uji: R 1,29 mm, jadi garis dalam berjarak 0,79 mm dari tepi
   lubang. Celah di antara sprinkle (R ≤ 0,18 mm) tidak dipotong.

g. **Kiss dan die:** area die digabung dengan area kiss yang dilebarkan 1 px, aturan yang sama dengan garis otomatis.
   Garis dalam die harus berada di dalam garis dalam kiss; bila tidak tersisa lingkaran potong ±1 mm, lubang itu
   hanya di-kiss-cut.

h. **Angka kalibrasi:** Toleransi awal 64 (bertransparansi) dan 32 (tanpa transparansi); ruang tersisa minimal
   0,5 mm; jarak minimal garis dalam ke tepi lubang 0,3 mm. Semuanya titik awal dan dikalibrasi di mesin operator.

i. **Data:** hasil disimpan sebagai polygon manual yang sudah ada (`ManualKiss`/`ManualDie`). Skala lebar, kunci
   offset, Simpan, dan undo/redo editor maupun global berlaku sama. Tidak ada format baru.

j. **Garis otomatis tidak berubah:** `Loader.Load` tanpa polygon manual memberi hasil yang sama seperti sebelumnya,
   termasuk ambang alpha > 16 dan aturan lubang 2026-10-21.

### 3. Pengujian dan dokumen

- **Selfcheck S13**, tanpa mouse seperti S12. Gambar uji: cincin dengan lubang sempit, watermark alpha 38, satu pulau
  terpisah, dan satu takik. Yang dicek:
  - klik di cincin: watermark tidak ikut, takik diikuti, lubang sempit tertutup;
  - klik di lubang: garis dalam muncul dengan jarak 0,3 mm sampai offset dari tepi lubang, die tetap membungkus kiss;
  - klik lagi di lubang: garis dalam hilang;
  - Shift+klik di pulau: 2 kontur luar;
  - Toleransi 16: watermark ikut terpilih;
  - Urungkan dan Ulangi;
  - gambar tanpa transparansi berlatar putih: yang terpilih objeknya, bukan latarnya.
- **Uji lama:** S1–S12 dan validasi garis potong (`validasi.ps1` + `validasi.py`) harus identik, karena garis otomatis
  tidak berubah.
- **Uji UI tanpa mouse:** tombol Smart select dan slider Toleransi ada dan bisa diatur lewat UI Automation. Klik di
  kanvas butuh mouse sungguhan: NOT RUN, kecuali user mengizinkan uji mouse singkat.
- **File donat user:** diuji di PC pengembang; bukti hanya berupa teks, file tidak masuk repo.
- **Dokumen:** PRD (F24, keputusan poin 4 donat, batasan), UJI-GARIS-POTONG S13, kasus e2e, dan status dokumen ini.

## Di luar cakupan

- AI dan objek di foto berlatar ramai.
- Mengubah ambang alpha garis otomatis (tetap > 16).
- Mengubah aturan lubang garis otomatis.

## Urutan kerja

1. Mesin: pemilihan objek, garis offset, tambah/hapus lubang, dan selfcheck S13.
2. Editor: tombol, slider, klik dan Shift+klik, pesan.
3. Verifikasi: build Release/Debug, selfcheck, validasi identik, UI tanpa mouse, file donat lokal.
4. Dokumen dan bukti di `04-evidence`.

Commit dan push hanya atas perintah user.
