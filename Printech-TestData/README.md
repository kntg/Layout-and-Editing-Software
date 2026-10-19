# Printech-TestData

Kumpulan file uji untuk Printech. Hasil benar setiap file sudah diketahui sebelum file dimasukkan ke aplikasi, jadi hasil Printech bisa dibandingkan langsung.

| Folder | Isi |
|---|---|
| `01-input/` | File uji sintetis. Daftar file dan skenario yang diuji masing-masing: [expected-results.md](02-expected/expected-results.md). `cdr-normal.cdr` dibuat operator di CorelDRAW (lihat `cdr-normal.BACA.txt`). `CHECKSUMS.sha256` = hash file original. |
| `01-input/customer/` | File customer yang diizinkan untuk pengujian. Hanya ada di PC lokal, tidak masuk git. Lihat [README](01-input/customer/README.md). |
| `02-expected/` | [expected-results.md](02-expected/expected-results.md): hasil benar per file, toleransi, cara mengecek. [validasi-garis-potong-nesting.md](02-expected/validasi-garis-potong-nesting.md): kriteria dan hasil validasi mesin garis potong dan nesting. |
| `03-exports/` | Hasil export Printech saat menguji. `validasi/` berisi export dan data mentah dari `validasi.ps1`; folder ini dibuat ulang setiap kali validasi dijalankan dan tidak masuk git. |
| `04-evidence/` | Bukti pengujian: laporan, tangkapan layar, log, foto hasil potong. |

## Cara menguji manual
1. Buka Printech dengan pengaturan bawaan.
2. Tambahkan file dari `01-input/`, satu per satu atau sekaligus.
3. Bandingkan hasilnya dengan `02-expected/expected-results.md`.
4. Simpan export ke `03-exports/` dan bukti ke `04-evidence/`. Format nama: `TANGGAL-nama-file-jenis`, contoh `2026-10-18-pdf-single-page-export.pdf`.
5. Cek file original tidak berubah:
   ```
   powershell -ExecutionPolicy Bypass -File cek-original.ps1
   ```
   Hasil benar: semua `sama`, exit code 0.

## Validasi otomatis (PC pengembangan)
1. `dotnet build -c Release` di folder `StickerLayout`.
2. `pwsh validasi.ps1`: menjalankan Printech untuk semua skenario.
3. `python validasi.py`: menilai hasilnya. Butuh PyMuPDF, Pillow, NumPy, dan SciPy. Exit code 0 berarti semua lulus.

## Membuat ulang file uji
`python make-testdata.py` (butuh PyMuPDF dan Pillow). Ukuran setiap bentuk tertulis di skrip itu. File yang sudah ada tidak ditimpa, karena PyMuPDF tidak menghasilkan byte yang sama setiap kali. File baru dicatat dengan `cek-original.ps1 -Catat`.
