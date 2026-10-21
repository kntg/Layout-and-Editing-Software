# File customer untuk pengujian

Folder ini untuk file customer yang **sudah diizinkan** untuk pengujian, misalnya logo, stiker, atau desain CDR/PDF/AI yang biasa dipotong di AP 55. File customer melengkapi file sintetis di `01-input`: hasil benar file sintetis diketahui dari cara file dibuat, sedangkan file customer menunjukkan apakah Printech benar pada pekerjaan nyata.

**File customer tidak masuk git.** Repo GitHub ini publik. `.gitignore` mengecualikan semua isi folder `customer/` (di `01-input`, `03-exports`, dan `04-evidence`), kecuali README ini. Jangan unggah file customer ke tempat lain.

## Cara menambahkan

1. **Pastikan izin.** Hanya file yang customer atau pemiliknya izinkan untuk pengujian. Catat siapa yang mengizinkan dan kapan di `hasil-benar.md` (langkah 4).
2. **Salin file apa adanya.** Pakai copy, bukan "Save As" atau export ulang. Jangan buka lalu simpan di CorelDRAW atau Illustrator, karena itu mengubah isi file.
3. **Catat hash file original:**

   ```powershell
   powershell -ExecutionPolicy Bypass -File ..\..\cek-original.ps1 -Catat
   ```

   Hash disimpan di `customer/CHECKSUMS.sha256`. Hash yang sudah tercatat tidak pernah ditimpa.
4. **Tulis hasil benar sebelum file dimasukkan ke Printech.** Buat `customer/hasil-benar.md`. Per file, ukur di CorelDRAW atau Acrobat, bukan di Printech:
   - jumlah halaman/desain;
   - ukuran artwork W × H (mm);
   - jenis file (vektor atau gambar); untuk gambar, ukuran piksel;
   - ada lubang atau bagian terpisah;
   - garis kiss/die yang benar = artwork + 2 × offset (lihat `02-expected/expected-results.md`).
5. **Uji di Printech** dengan pengaturan di `02-expected/expected-results.md`. Simpan export di `03-exports/customer/` dan bukti di `04-evidence/customer/`. Kedua folder itu juga tidak masuk git.
6. **Setelah pengujian, cek file original tidak berubah:**

   ```powershell
   powershell -ExecutionPolicy Bypass -File ..\..\cek-original.ps1
   ```

   Hasil benar: semua baris `sama` dan exit code 0.
