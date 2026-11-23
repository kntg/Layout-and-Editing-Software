# Data uji Printech

Folder ini berisi file untuk menguji Printech. Hasil yang benar untuk setiap file sudah dicatat sebelum file dicoba di aplikasi, jadi hasil Printech bisa langsung dibandingkan.

| Folder | Isi |
| --- | --- |
| `01-input/` | File uji buatan sendiri (bukan file customer). Daftar file dan apa yang diuji dengan masing-masing ada di [hasil yang diharapkan](02-expected/hasil-yang-diharapkan.md). `cdr-normal.cdr` dibuat operator di CorelDRAW; caranya ada di `cdr-normal.BACA.txt`. `CHECKSUMS.sha256` mencatat sidik digital (hash) setiap file asli, untuk memastikan file tidak berubah. |
| `01-input/khusus/` | Kasus khusus yang diuji terpisah: file AI yang disimpan tanpa opsi PDF-compatible, PDF terenkripsi, serta file PNG, JPG, dan SVG yang rusak. Hasil benarnya ada di [hasil yang diharapkan](02-expected/hasil-yang-diharapkan.md). |
| `01-input/latar/` | Gambar PNG berlatar penuh: abu muda, pink pastel, dan putih (temuan D10). Latar tidak pernah dibuang otomatis, termasuk latar putih (masukan leader 2026-10-21). Hasil benarnya ada di [hasil yang diharapkan](02-expected/hasil-yang-diharapkan.md). |
| `01-input/customer/` | File customer yang boleh dipakai untuk uji. Hanya ada di PC lokal dan tidak ikut git. Lihat [panduannya](01-input/customer/README.md). |
| `02-expected/` | [Hasil yang diharapkan](02-expected/hasil-yang-diharapkan.md): hasil benar per file, batas selisih yang diterima, dan cara mengeceknya. [Validasi garis potong dan susunan](02-expected/validasi-garis-potong-susunan.md): kriteria dan hasil validasi otomatis untuk garis potong dan susun otomatis. |
| `03-exports/` | Hasil export Printech saat diuji. Subfolder `validasi/` berisi hasil export dan data mentah dari validasi otomatis; subfolder ini dibuat ulang setiap kali validasi dijalankan dan tidak ikut git. |
| `04-evidence/` | Bukti pengujian: laporan, tangkapan layar, catatan hasil, dan foto hasil potong. |

## Menguji dengan tangan

1. Buka Printech dengan pengaturan bawaan.
2. Tambahkan file dari `01-input/`, satu per satu atau sekaligus.
3. Bandingkan hasilnya dengan [hasil yang diharapkan](02-expected/hasil-yang-diharapkan.md).
4. Simpan hasil export ke `03-exports/` dan bukti ke `04-evidence/`. Format nama file: `TANGGAL-nama-file-jenis`, contoh `2026-10-18-pdf-single-page-export.pdf`.
5. Pastikan file asli tidak berubah. Jalankan perintah ini di PowerShell, dari folder ini:

   ```powershell
   powershell -ExecutionPolicy Bypass -File cek-original.ps1
   ```

   Hasil benar: semua baris bertulisan `sama`, dan perintah selesai tanpa galat (exit code 0).

## Validasi otomatis (di PC pengembang)

Validasi otomatis menjalankan mesin Printech pada semua data uji, lalu membandingkan garis potong, susunan, dan PDF hasilnya dengan bentuk file asli.

1. Build aplikasi versi rilis: `dotnet build -c Release` di folder `StickerLayout`.
2. Jalankan Printech untuk semua skenario: `pwsh validasi.ps1`.
3. Nilai hasilnya: `python validasi.py`. Butuh Python dengan PyMuPDF, Pillow, NumPy, dan SciPy. Hasil benar: semua lulus (exit code 0).

**Pemeriksaan otomatis bawaan aplikasi (self-check).** Aplikasi punya pemeriksaan bawaan yang menguji garis potong, susunan, dan export tanpa membuka jendela. Skrip `run-selfcheck.ps1` menjalankannya 4 kali: dengan folder baru, folder `samples`, PDF berukuran salah, dan folder yang tidak bisa dibuat. Dua yang terakhir harus gagal dengan sengaja (exit code 1), untuk memastikan kegagalan memang terdeteksi. Folder kerjanya ada di folder sementara Windows. Hasil benar: `RINGKAS: 4/4` (exit code 0).

```powershell
pwsh -File run-selfcheck.ps1                  # versi rilis
pwsh -File run-selfcheck.ps1 -Config Debug    # versi pengembang
```

## Uji tampilan dan pemakaian memori (di PC pengembang)

Skrip di bawah membuka Printech lalu mengoperasikannya sendiri, seperti operator. Jalankan dengan Windows PowerShell 5.1 (bawaan Windows): `powershell -ExecutionPolicy Bypass -File <nama skrip> ...`. Pilihan tiap skrip tertulis di baris pertamanya. Selama uji berjalan, jangan sentuh mouse dan keyboard.

- **Uji semua file input** (`ui-semua-input.ps1`): semua file `01-input` dimuat sekaligus; yang dicek status, peringatan, pemakaian bahan, dan pemakaian memori (RAM).
- **Uji file khusus** (`ui-file-khusus.ps1`): file di `01-input/khusus`; harus muncul pesan galat yang jelas, dan aplikasi tidak tertutup sendiri (crash).
- **Uji tata letak** (`ui-edit-layout.ps1`): menggeser stiker dengan mouse sampai menempel ke stiker lain, memutar, memindah dengan tombol panah, menghapus, mengubah jumlah dan lebar, menyusun ulang, export saat ada stiker bertanda merah, mengganti kertas dan bahasa, serta jendela pemeriksaan garis potong.
- **Profil memori** (`ram-profil.ps1`): pemakaian memori per kelompok file, dengan angka yang sama seperti di Task Manager.
- **Uji urungkan dan ulangi** (`ui-undo.ps1`): urungkan dan ulangi untuk seluruh aplikasi lewat menu Edit, Ctrl+Y, Ctrl+Z, dan Ctrl+Shift+Z. Tanpa mouse; uji tombol keyboard dilewati bila jendela Printech tidak bisa dijadikan jendela aktif.
- **Uji editor garis potong tanpa mouse** (`ui-editor-garis.ps1`): mengendalikan editor garis potong langsung lewat kontrol jendelanya, tanpa mouse dan keyboard. Yang diuji: membuka editor, Simplify, Urungkan/Ulangi di editor, kotak Smart select dan slider Toleransi, Simpan, menu Edit > Urungkan, tombol bentuk Lingkaran (tanda bentuk muncul di daftar desain, dan bentuk tetap setelah offset diubah dan disusun ulang), serta warna border Merah (tetap setelah disimpan). Pilihan `-Shots` menyimpan tangkapan layar.
- **Uji editor garis potong dengan mouse** (`ui-editor-mouse.ps1`): memakai mouse dan keyboard sungguhan. Yang diuji:
  - kiss Bintang dengan die Kotak, lewat pilihan "Garis yang diedit";
  - klik titik garis (node) tanpa menggesernya, seret yang mengikuti jarak gerakan mouse, serta Shift+klik dua titik lalu seret dan hapus (temuan leader 2026-10-26);
  - pegangan kurva (handle), Node halus/sudut, dan Jadikan garis/kurva;
  - seret titik sampai batas kotak, Ctrl+Z/Ctrl+Y, klik di garis untuk menambah titik, dan hapus garis bagian dalam;
  - smart select dengan klik di kanvas, pada gambar uji yang dibuat oleh skrip ini. Ukuran lubang donatnya dihitung dari lebar dan offset kiss di aplikasi; pilihan `-Offset <mm>` mencoba offset kiss lain.

  Titik dicari dari tangkapan layar. Uji berhenti sendiri bila jendela editor tidak aktif atau mouse disentuh, supaya klik tidak masuk ke aplikasi lain dan hasil tidak tercampur gerakan pengguna. Pilihan `-Shots` menyimpan tangkapan layar.

## Membuat ulang file uji

Jalankan `python make-testdata.py` (butuh PyMuPDF dan Pillow). Ukuran setiap bentuk tertulis di skrip itu. File yang sudah ada tidak ditimpa, karena PyMuPDF tidak menghasilkan isi file yang persis sama setiap kali. File baru dicatat dengan `cek-original.ps1 -Catat`.
