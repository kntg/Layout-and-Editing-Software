# File customer untuk pengujian

Folder ini untuk file customer yang **sudah diizinkan** untuk pengujian, misalnya logo, stiker, atau desain CDR/PDF/AI yang biasa dipotong di AP 55. File customer melengkapi file uji buatan sendiri di `01-input`: hasil benar file buatan sendiri sudah diketahui dari cara file itu dibuat, sedangkan file customer menunjukkan apakah Printech benar pada pekerjaan nyata.

**File customer tidak ikut git.** Repo GitHub ini publik. Pengaturan git (`.gitignore`) mengecualikan semua isi folder `customer/` (di `01-input`, `03-exports`, dan `04-evidence`), kecuali panduan ini. Jangan unggah file customer ke tempat lain.

## Cara menambahkan

1. **Pastikan izin.** Hanya file yang diizinkan customer atau pemiliknya untuk pengujian. Catat siapa yang mengizinkan dan kapan di `hasil-benar.md` (langkah 4).
2. **Salin file apa adanya.** Pakai copy, bukan "Save As" atau export ulang. Jangan buka lalu simpan di CorelDRAW atau Illustrator, karena itu mengubah isi file.
3. **Catat sidik digital (hash) file asli:**

   ```powershell
   powershell -ExecutionPolicy Bypass -File ..\..\cek-original.ps1 -Catat
   ```

   Sidik disimpan di `customer/CHECKSUMS.sha256`. Sidik yang sudah tercatat tidak pernah ditimpa.
4. **Tulis hasil benar sebelum file dicoba di Printech.** Buat `customer/hasil-benar.md`. Untuk setiap file, ukur di CorelDRAW atau Acrobat, bukan di Printech:
   - jumlah halaman atau desain;
   - ukuran artwork, lebar × tinggi (mm);
   - jenis file (vektor atau gambar); untuk gambar, ukurannya dalam piksel;
   - ada atau tidaknya lubang atau bagian yang terpisah;
   - ukuran garis kiss/die yang benar = artwork + 2 × offset (lihat [hasil yang diharapkan](../../02-expected/hasil-yang-diharapkan.md)).
5. **Uji di Printech** dengan pengaturan yang tertulis di [hasil yang diharapkan](../../02-expected/hasil-yang-diharapkan.md). Simpan hasil export di `03-exports/customer/` dan bukti di `04-evidence/customer/`. Kedua folder itu juga tidak ikut git.
6. **Setelah pengujian, pastikan file asli tidak berubah:**

   ```powershell
   powershell -ExecutionPolicy Bypass -File ..\..\cek-original.ps1
   ```

   Hasil benar: semua baris bertulisan `sama`, dan perintah selesai tanpa galat (exit code 0).
