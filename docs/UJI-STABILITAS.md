# Skenario uji: stabilitas dan performa (spesifikasi bagian 2)

Fitur yang diuji:

- konversi CDR di proses pembantu yang terpisah (worker), dengan batas waktu 30 detik;
- tampilan aplikasi tetap bisa dipakai selama menunggu CorelDRAW;
- 3 percobaan susun otomatis yang dijalankan bersamaan (paralel);
- hapus latar yang dijalankan bersamaan (paralel).

Lihat [kebutuhan aplikasi](kebutuhan-aplikasi.md), bagian Keputusan, butir "Stabilitas dan performa".

Cara menjalankan:

- **Otomatis:** pemeriksaan otomatis bawaan aplikasi (`StickerLayout.exe --selfcheck <folder>`). Hasilnya tertulis di `selfcheck.txt` sebagai baris `P1 LULUS ...` sampai `P5`. Bila satu skenario gagal, pemeriksaan selesai dengan kode 1.
- **Pengembang:** P6 diukur dengan Task Manager. P1 juga dicek lewat tampilan aplikasi.
- **Operator:** P7–P10 butuh PC dengan CorelDRAW terpasang.

| Kode | Skenario | Syarat lulus | Cara | Hasil 2026-10-17 |
| --- | --- | --- | --- | --- |
| P1 | Tambah file CDR di PC tanpa CorelDRAW | Muncul pesan "CorelDRAW tidak terpasang"; aplikasi tidak tertutup sendiri (crash) | Otomatis + tampilan aplikasi | Lulus. Pemeriksaan otomatis: pesan muncul dalam 0,3 detik. Lewat tampilan aplikasi: pesan muncul 1,1 detik setelah file ditambahkan, dan aplikasi tetap responsif |
| P2 | Proses pembantu melewati batas waktu | Proses dihentikan, muncul pesan batas waktu, dan tidak ada proses pembantu yang tersisa | Otomatis (batas diset 1 ms) | Lulus: dihentikan dalam 20–36 ms, 0 proses tersisa |
| P3 | Susun otomatis paralel dijalankan 5 kali | Hasilnya sama persis setiap kali | Otomatis | Lulus: 5 kali, 1 hasil. Saat cara paralel diterapkan (grid 2 mm), jumlah lembar dan pemakaian bahan sama persis dengan versi berurutan |
| P4 | Hapus latar paralel dibandingkan dengan versi berurutan | Kikis tepi 1–3 piksel dan gambar tampilan sama persis sampai ke tiap bit | Otomatis | Lulus |
| P5 | Lama kerja di jalur utama tampilan (UI thread) | Susun 130 stiker kurang dari 1000 ms; satu langkah Rapikan pada gambar 4000×3000 kurang dari 2000 ms (waktu terbaik dari 3 kali) | Otomatis | Lulus. Versi rilis (grid 1 mm): 84–93 ms dan 173–194 ms. Versi pengembang (Debug): 581–799 ms dan 452 ms. Angkanya naik bila aplikasi lain sedang memakai prosesor |
| P6 | Pemakaian memori saat diam, dengan 4 file contoh | Target spesifikasi ±120 MB, diukur seperti di Task Manager (private working set) | Pengembang | Lulus: 52–57 MB (tanpa file 35 MB). Total working set 131–153 MB ikut menghitung file sistem Windows (DLL) yang dipakai bersama, termasuk driver grafis Intel ±70 MB |
| P7 | CDR asli, saat CorelDRAW tertutup dan saat terbuka | Desain masuk; lama proses dicatat untuk menyetel batas 30 detik | Operator | Belum diuji |
| P8 | CorelDRAW sedang dipakai dan ada dokumen yang belum disimpan | Dokumen operator tetap utuh, dan CorelDRAW tidak tertutup | Operator | Belum diuji |
| P9 | CorelDRAW sedang menampilkan dialog | Printech tetap bisa dipakai selama menunggu; bila CorelDRAW tidak merespons, pesan muncul setelah 30 detik; setelah dialog ditutup, menambah ulang berhasil | Operator | Belum diuji |
| P10 | Tiga file CDR sekaligus, lalu ubah offset dan klik Susun | Ketiganya masuk berurutan dan tampilan tetap responsif; saat Susun, CorelDRAW tidak dibuka lagi | Operator | Belum diuji |

## Langkah operator (P7–P10)

1. **P7:** Tutup CorelDRAW. Tambahkan satu file CDR yang biasa dipakai, lalu catat berapa detik dari klik sampai desain muncul. Ulangi dengan CorelDRAW dalam keadaan terbuka.
2. **P8:** Buka CorelDRAW, buat dokumen baru, gambar sesuatu, dan jangan disimpan. Tambahkan file CDR di Printech. Setelah selesai, pastikan dokumen di CorelDRAW masih ada.
3. **P9:**
   - Di CorelDRAW, buka sebuah dialog (misalnya Options, Ctrl+J) dan biarkan terbuka.
   - Tambahkan file CDR di Printech. Selama menunggu, coba geser dan perbesar lembar di Printech.
   - Bila CorelDRAW tidak merespons, setelah ±30 detik harus muncul pesan "CorelDRAW tidak merespons".
   - Tutup dialog di CorelDRAW, lalu tambahkan file itu lagi.
4. **P10:**
   - Pilih tiga file CDR sekaligus. Selama proses, baris status menampilkan "Membuka … di CorelDRAW".
   - Setelah semua masuk, ubah offset kiss lalu klik Susun. Teks "Membuka … di CorelDRAW" tidak boleh muncul lagi.
5. Catat hasil setiap skenario: lulus atau gagal, waktu, dan catatan.

## Catatan

1. **"60 FPS" (60 gambar per detik) tidak diukur langsung.** Yang diukur adalah lama kerja di jalur utama tampilan (P5):
   - susun 130 stiker sekitar 85 ms (grid 1 mm), atau ±5 gambar;
   - langkah Rapikan dihitung di latar belakang, jadi jendela tetap merespons; hanya pembaruan tampilan (±20–70 ms) yang berjalan di jalur utama;
   - konversi CDR tidak menghentikan tampilan.
2. **Cara paralel: `Parallel.For`, bukan `Parallel.ForEachAsync`.** Spesifikasi menyebut `Parallel.ForEachAsync`, tetapi pekerjaan di sini murni hitungan prosesor (lihat kebutuhan aplikasi).
3. **Pembanding P4.** Hapus latar paralel dibandingkan dengan salinan cara lama di `SelfCheck.ErodeRef`.
