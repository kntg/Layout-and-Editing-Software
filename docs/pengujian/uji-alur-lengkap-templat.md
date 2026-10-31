# Uji alur lengkap Printech (dari file masuk sampai hasil potong)

<!-- uji-alur-lengkap.md dan lembar-hasil-uji-alur-lengkap.csv dibuat oleh buat-uji-alur-lengkap.py dari uji-alur-lengkap-templat.md. Ubah templat ini atau kasus uji di buat-uji-alur-lengkap.py, lalu jalankan: python -X utf8 docs/pengujian/buat-uji-alur-lengkap.py -->

Dokumen ini dulu bernama "validasi end-to-end" (e2e). Isinya: semua kasus uji dari file customer masuk ke Printech sampai PDF dicetak di Develop dan dipotong di Graphtec, beserta status dan buktinya. Draf 2026-10-19, disusun dari pemeriksaan kode, bukan hanya dari dokumentasi.

**Versi kode yang diuji:** versi 2026-10-19 (kode commit `0cc6446`), yaitu versi sebelumnya (kode commit `7a414c8`) ditambah perubahan 2026-10-18 dan 2026-10-19:

- [kode mesin](../../StickerLayout/Core.cs): penjaga jarak dibulatkan ke atas, die minimal 1 piksel di luar kiss, penanda PDF yang ditempel sebagai gambar, fungsi Compact, salinan PDF/AI saat dimuat (D2), dan simpanan hasil konversi CDR per versi file (D1);
- [kode jendela utama](../../StickerLayout/MainWindow.xaml.cs) dan [teks aplikasi](../../StickerLayout/Strings.cs): peringatan bila PDF ditempel sebagai gambar; Susun memuat ulang dari PDF yang sama;
- [kode export](../../StickerLayout/Export.cs) dan [pemeriksaan otomatis](../../StickerLayout/SelfCheck.cs): baris Print Ticket dan pemeriksaan tambahan;
- folder `Printech-TestData/`.

Bagian 4 berisi hasil pemeriksaan sebelum perbaikan D1/D2, hasil sesudahnya, dan uji tambahan 2026-10-19 sore (kasus khusus, alur edit lewat jendela aplikasi, Print Ticket panjang, profil memori).

## 1. Jenis pengujian, status, dan tingkat dampak

| Jenis | Isi | Cara menjalankan | Sudah dijalankan |
| --- | --- | --- | --- |
| Pemeriksaan otomatis | Pemeriksaan bawaan aplikasi ([SelfCheck.cs](../../StickerLayout/SelfCheck.cs)): skenario S1–S9, S11–S16, P1–P5, dan pemeriksaan tambahan | `StickerLayout.exe --selfcheck <folder>`. Exit code 0 dan baris terakhir `OK` berarti lulus | Ya |
| Validasi data uji | Mesin Printech dijalankan pada data uji (`Printech-TestData`), lalu hasilnya dinilai dengan bentuk file asli (`validasi.ps1` + `validasi.py`), ditambah pengecekan bahwa file asli tidak berubah (`cek-original.ps1`) | Lihat [panduan data uji](../../Printech-TestData/README.md) | Ya |
| Uji tampilan | Pengujian lewat jendela aplikasi di PC pengembang. Sebagian sudah dijalankan oleh skrip yang mengoperasikan aplikasi sendiri lewat mouse, keyboard, dan kontrol jendela: uji semua file input, uji file khusus, uji tata letak, uji urungkan, dan uji editor garis potong (skrip `ui-*.ps1` di `Printech-TestData`, dijalankan dengan Windows PowerShell 5.1) | Langkah per kasus uji, atau skrip | Ya (2026-10-19) |
| Operator dan mesin | CorelDRAW, Cutting Master 5, printer Develop, Graphtec | PC operator di AP 55 | Tidak (BLOCKED, di luar cakupan prototipe) |

Repo ini tidak punya proyek uji unit (xUnit/NUnit) dan tidak punya pemeriksaan otomatis di server (CI). Satu-satunya pemeriksaan otomatis di dalam aplikasi adalah pemeriksaan bawaan (`--selfcheck`). Validasi (`validasi.py`) butuh Python dengan PyMuPDF, Pillow, NumPy, dan SciPy.

**Status:**

- **PASS:** sudah dijalankan, hasilnya sesuai harapan, dan buktinya tersimpan.
- **FAIL:** sudah dijalankan, hasilnya tidak sesuai harapan.
- **BLOCKED:** tidak bisa dijalankan di lingkungan ini, karena perangkat, file uji, atau keputusan belum ada.
- **NOT RUN:** bisa dijalankan, tetapi belum dijalankan.

**Tingkat dampak (severity; definisi disetujui 2026-10-31).** Di lembar hasil (CSV), kolom Tingkat dampak berisi usulan dampak bila uji itu gagal; label tiap kasus tetap usulan dan diperiksa saat kasusnya gagal.

- **Blocker:** bisa membuang bahan atau mencetak/memotong hal yang salah tanpa disadari operator. Rilis ditahan.
- **Mayor:** hasil salah tetapi terlihat atau ada peringatan, atau ada cara kerja yang aman. Rilis boleh dengan catatan.
- **Minor:** kenyamanan atau tampilan.

## 2. Alur dari file masuk sampai produksi fisik

### Di Printech

1. **File masuk** lewat tombol Tambah, Ctrl+I/Ctrl+O, tarik-dan-lepas, atau argumen perintah ([kode jendela utama](../../StickerLayout/MainWindow.xaml.cs)).
2. **CDR diubah ke PDF** oleh proses pembantu (`--cdr2pdf`) yang mengendalikan CorelDRAW ([kode aplikasi](../../StickerLayout/App.xaml.cs), [kode mesin](../../StickerLayout/Core.cs)).
   - Batas waktu 30 detik.
   - Hasil konversi disimpan per lokasi dan versi file (waktu ubah + ukuran), jadi CDR yang diedit dikonversi lagi.
3. **Desain dimuat** ([kode mesin](../../StickerLayout/Core.cs)):
   - PDF/AI disalin dulu ke folder sementara; desain dan export memakai salinan itu. Susun memuat ulang dari salinan yang sama.
   - PDF/AI digambar dengan PDFium dan dicek apakah bisa ditempel sebagai vektor.
   - SVG digambar dengan SharpVectors.
   - PNG/JPG dibuka.
   - Isinya dipangkas. Artwork = piksel yang tidak transparan (alpha di atas 16). Latar tanpa bagian transparan, termasuk putih, tidak dibuang otomatis sejak masukan leader 2026-10-21.
4. **Garis potong dibuat:**
   - gambar bantu (mask) 6 piksel per mm, lalu celah ditutup (offset + halus), dikaburkan dengan penjaga jarak, rongga diisi kecuali lubang asli yang tetap terbuka (donat), lalu dijadikan kurva oleh Potrace;
   - die = die digabung dengan (kiss + 1 piksel).
   - Garis manual dari editor garis potong (titik lurus dan kurva, sejak 2026-10-23) dan preset bentuk dipakai apa adanya; grid susun dan luas dihitung dari garis itu ([titik kurva](../../StickerLayout/Curves.cs), [bentuk](../../StickerLayout/Shapes.cs)). Desain kotak otomatis mendapat garis kotak bersudut tajam.
5. **Bleed** (pilihan, hanya untuk gambar).
6. **Susun otomatis:**
   - stiker ditaruh di tempat kosong pertama (greedy) pada grid 1 mm, dengan 4 rotasi dan 3 urutan percobaan yang dihitung bersamaan;
   - margin dan tumpang tindih diperiksa.
7. **Edit tata letak:** seret + tempel otomatis, putar, panah, hapus, dan penuhi lembar.
   - Urungkan/ulangi untuk seluruh aplikasi: keadaan dokumen disimpan setelah setiap aksi.
   - Editor garis potong per desain ([kode editor](../../StickerLayout/CutEditWindow.xaml.cs)).
8. **Indikator bahan dan peringatan** ([kode jendela utama](../../StickerLayout/MainWindow.xaml.cs)).
9. **Export PDF** ([kode export](../../StickerLayout/Export.cs)):
   - Artwork PDF/AI ditempel sebagai objek vektor (Form XObject) dari salinan yang dibuat saat dimuat.
   - Gambar diambil dari memori.
   - Garis potong berwarna CMYK murni (DeviceCMYK), tebal 0,1 mm.
10. **Hasil pendukung:** preview JPG dan Print Ticket.

**Di luar Printech (operator, [kebutuhan aplikasi §10](../kebutuhan-aplikasi.md))**
11. Buka PDF di CorelDRAW, lalu pasang tanda registrasi dengan cara yang biasa dipakai operator.
12. Cetak di Develop A3+. Belum dipastikan: bagaimana garis potong disembunyikan saat dicetak.
13. Kirim ke Cutting Master 5, pilih garis per warna (magenta = kiss, cyan = die), lalu potong di Graphtec.

## 3. Bagian kode dan uji yang sudah ada

| Fitur | Kode | Uji yang sudah ada | Yang belum diuji |
| --- | --- | --- | --- |
| Impor PDF/AI | [kode mesin](../../StickerLayout/Core.cs) | pemeriksaan otomatis (`logo.pdf`, `logo.ai`, `multi.pdf`); validasi E1, X1, X3 (termasuk PDF terenkripsi); verifikasi data uji | kotak putih (D7) |
| Impor CDR | [kode mesin](../../StickerLayout/Core.cs), [kode aplikasi](../../StickerLayout/App.xaml.cs), [kode jendela utama](../../StickerLayout/MainWindow.xaml.cs) | pemeriksaan otomatis P1–P2 (tanpa CorelDRAW) | Konversi asli, CorelDRAW macet, dan bukti D1 dengan CorelDRAW: butuh operator |
| Impor PNG/JPG/SVG | [kode mesin](../../StickerLayout/Core.cs) | pemeriksaan otomatis (`circle.png`, `rough.jpg`, `star.jpg`, `badge.svg`); validasi | File rusak |
| dpi dan peringatan | [kode mesin](../../StickerLayout/Core.cs) | pemeriksaan otomatis; verifikasi data uji | Pembulatan tampilan (D6) |
| Garis potong | [kode mesin](../../StickerLayout/Core.cs); editor: [titik kurva](../../StickerLayout/Curves.cs) dan [jendela editor](../../StickerLayout/CutEditWindow.xaml.cs); [bentuk](../../StickerLayout/Shapes.cs); [smart select](../../StickerLayout/SmartSelect.cs) | S1–S9, S11–S15; validasi A1–A7 | Potong fisik (S10) |
| Bleed | [kode mesin](../../StickerLayout/Core.cs) | pemeriksaan otomatis; validasi B4 | Cetak dan potong fisik |
| Susun otomatis dan pemeriksaan bentuk | [kode mesin](../../StickerLayout/Core.cs) | pemeriksaan otomatis, P3, P5; validasi B1–B7 | — |
| Edit tata letak (jendela aplikasi) | [kode jendela utama](../../StickerLayout/MainWindow.xaml.cs) | Fungsi tempel otomatis, pemeriksaan, dan penuhi lembar, serta logika editor garis potong lewat pemeriksaan otomatis; alur di jendela lewat uji tata letak (`ui-edit-layout.ps1`: EDIT-01..08, EDIT-11, CUT-12), uji urungkan (`ui-undo.ps1`: EDIT-12), uji editor tanpa mouse (`ui-editor-garis.ps1`: EDIT-13), dan uji editor dengan mouse (`ui-editor-mouse.ps1`: EDIT-14, EDIT-16, EDIT-18, EDIT-20) | Uji tampilan tidak termasuk pemeriksaan otomatis: dijalankan terpisah, dan butuh PC yang tidak sedang dipakai |
| Export PDF | [kode export](../../StickerLayout/Export.cs) | pemeriksaan otomatis S8 dan salinan PDF; validasi E1, X1–X3 | CorelDRAW |
| Preview JPG dan Print Ticket | [kode export](../../StickerLayout/Export.cs) | pemeriksaan otomatis; validasi X4–X5 | Print Ticket dengan lebih dari 10 lembar / lebih dari 20 desain |
| Rapikan gambar (dibekukan) | [kode Rapikan](../../StickerLayout/Cleanup.cs), [jendela Rapikan](../../StickerLayout/CleanupWindow.xaml.cs) | pemeriksaan otomatis, P4, P5 | Jendela Rapikan |
| Kinerja dan memori | [kode jendela utama](../../StickerLayout/MainWindow.xaml.cs), [kode mesin](../../StickerLayout/Core.cs) | P5 otomatis; P6 manual; profil memori (`ram-profil.ps1`) | Tidak ada pemeriksaan otomatis untuk memori |

## 4. Hasil pemeriksaan otomatis (2026-10-19)

| Perintah | Exit code | Hasil | Hasil tersimpan di |
| --- | --- | --- | --- |
| `dotnet build StickerLayout -c Release` | 0 | 0 warning, 0 error | `StickerLayout/bin/Release/net10.0-windows/` |
| `dotnet build StickerLayout -c Debug` | 0 | 0 warning, 0 error | `StickerLayout/bin/Debug/net10.0-windows/` |
| `dotnet publish StickerLayout -c Release -r win-x64 --self-contained -o <folder sementara>` | 0 | 146 MB | Folder sementara. `publish\Printech` sengaja tidak ditimpa. |
| `…\Release\…\StickerLayout.exe --selfcheck <folder baru>` | 0 | 15/15 skenario lulus, baris terakhir `OK` | `<folder>\selfcheck.txt` |
| `…\Debug\…\StickerLayout.exe --selfcheck samples` (perintah di kebutuhan aplikasi §11) | 0 | 15/15 skenario lulus | `samples/selfcheck.txt` |
| Uji negatif: `logo.pdf` diganti PDF berukuran lain | 1 (sesuai harapan) | "ukuran asli PDF salah: 70,60x70,60" | — |
| Uji negatif: folder tidak bisa dibuat | 1 (sesuai harapan) | — | — |
| Aplikasi hasil publish, `--selfcheck <folder baru>` | 0 | 15/15 skenario lulus | — |
| `pwsh Printech-TestData/validasi.ps1` | 0 | 17 skenario dijalankan | `Printech-TestData/03-exports/validasi/` |
| `python Printech-TestData/validasi.py` | **1** | 18 dari 21 syarat lulus. Gagal: A2b, A4, dan A5 (A5 gagal karena A4) | `Printech-TestData/04-evidence/2026-10-19-validasi-*` |
| Cek file asli (`cek-original.ps1`) | 0 | 15/15 file asli tidak berubah | — |

### Setelah perbaikan D1/D2 (2026-10-19)

| Perintah | Exit code | Hasil |
| --- | --- | --- |
| `dotnet build StickerLayout -c Release` dan `-c Debug` | 0 | 0 warning, 0 error |
| Pemeriksaan otomatis versi rilis dan pengembang (folder baru, `samples`, 2 uji negatif) | 0 / 0 / 1 / 1 sesuai harapan | 15/15 skenario lulus. Cek baru: PDF diganti lalu dihapus setelah dimuat; muat ulang dan export tetap dari salinan (49,5 mm); menambah ulang memakai isi baru (28,3 mm) |
| `validasi.ps1` + `validasi.py` | 0 / 1 | Semua angka dan hasil penilaian sama persis dengan sebelum perbaikan (gagal tetap A2b, A4, A5) |
| Uji tampilan, semua file sekaligus | — | Sama dengan sebelum perbaikan: "18 stiker di 2 lembar.", 3 peringatan, "54% · sisa 46%" |
| Cek file asli (`cek-original.ps1`) | 0 | 15/15 file asli tidak berubah |

### Uji tambahan 2026-10-19 (tanpa perubahan kode)

| Uji | Exit code | Hasil |
| --- | --- | --- |
| Uji file khusus (`ui-file-khusus.ps1`, 5 file `01-input/khusus`) | 0 | 4 dialog "Gagal memuat" ditutup satu per satu; 1 desain masuk; aplikasi responsif. Pesan error masih teknis (D9). |
| PDF terenkripsi lewat mesin Printech | — | Dimuat sebagai vektor, lalu export PDF berhenti dengan error, sehingga seluruh export gagal (D8). |
| Uji tata letak (`ui-edit-layout.ps1`), 3 kali berturut-turut | 0 | 11/11 langkah lulus di setiap jalan (EDIT-01..09, EDIT-11, CUT-12). |
| Print Ticket 13 lembar, 21 desain | — | Tetap 1 halaman A4; ada baris "… dan 3 lainnya" (lembar) dan "… dan 1 lainnya" (desain). |
| Profil memori (`ram-profil.ps1`) | 0 | Kosong 35 MB, 4 desain 67, 16 desain 99, JPG 24 MP 216, dua PDF besar 347, semua 19 desain 351 MB (D4). |
| Cek file asli (`cek-original.ps1`, termasuk `01-input/khusus`) | 0 | 20/20 file asli tidak berubah. |

**Diulang 2026-10-21 setelah perbaikan D10:** build versi rilis dan pengembang 0 warning; pemeriksaan otomatis versi rilis dan pengembang 4/4 sesuai harapan (15/15 skenario, ditambah cek latar abu 242, pink pastel, dan hampir putih 252); validasi menghasilkan 1144 nilai garis, ukuran, dan posisi yang sama persis dengan 2026-10-19; laporan penilaiannya juga sama (A2b, A4, A5 tetap gagal seperti sebelumnya); cek file asli 23/23 sama (3 file baru di `01-input/latar`); tiga PNG latar dimuat di aplikasi tanpa peringatan.

**Diulang 2026-10-21 setelah perbaikan D8 dan D4:** build versi rilis dan pengembang 0 warning; pemeriksaan otomatis versi rilis dan pengembang 4/4 (15/15); validasi: semua 1584 nilai garis, ukuran, posisi, dan bleed sama persis dengan sebelum D4, dan skenario X sekarang berisi PDF terenkripsi; penilaian: X3 lulus termasuk PDF terenkripsi, A2b, A4, A5 tetap gagal seperti sebelumnya; profil memori: semua 19 desain 84 MB; zip operator dibuat ulang, dan pemeriksaan otomatis pada aplikasi hasil publish dan aplikasi dari zip selesai dengan exit code 0. Bukti: [catatan PDF terenkripsi](../../Printech-TestData/04-evidence/2026-10-21-pdf-terenkripsi.txt), [catatan profil memori](../../Printech-TestData/04-evidence/2026-10-21-ram-profil.txt), [catatan validasi](../../Printech-TestData/04-evidence/2026-10-21-validasi-garis-potong-nesting.txt).

**Masukan leader tahap 1 (2026-10-21):** latar gambar tanpa bagian transparan tidak dibuang otomatis, dan Compact diganti "Penuhi lembar" ([spesifikasi](../superpowers/specs/2026-10-21-masukan-leader-design.md)). Pemeriksaan otomatis versi rilis dan pengembang 4/4 (15/15); JPG di pemeriksaan otomatis dirapikan dulu seperti alur operator. Validasi: hanya A2b, A4, A5 yang gagal, seperti sebelumnya; B-campuran dipenuhi menjadi 198 stiker di 4 lembar tanpa tabrakan. Uji tampilan semua file: "18 stiker di 2 lembar.", 54 %, judul lembar 92 % dan 16 %, memori 87 MB ([bukti](../../Printech-TestData/04-evidence/2026-10-21-ui-semua-input.txt)). Uji sekali jalan D7: kotak putih dipertahankan.

**Masukan leader tahap 2–4 (2026-10-21):** urungkan/ulangi untuk seluruh aplikasi (uji urungkan `ui-undo.ps1`: EDIT-12 lulus), garis dalam untuk lubang asli (stiker donat, pemeriksaan otomatis S11, CUT-07), dan editor garis potong berbasis poligon (pemeriksaan otomatis S12: CUT-14; uji editor tanpa mouse `ui-editor-garis.ps1`: EDIT-13 lulus 5/5, [bukti](../../Printech-TestData/04-evidence/2026-10-21-ui-editor-garis.txt)). Build versi rilis dan pengembang 0 warning. Pemeriksaan otomatis versi rilis 4/4 (16/16 skenario). Versi pengembang: S9 (stiker 300 mm kurang dari 5 detik di versi pengembang) 3,9–5,8 detik, gagal 3 dari 6 jalan saat prosesor PC 42–62 % dipakai aplikasi lain; versi kode sebelum perubahan hari itu, diukur bergantian dalam kondisi yang sama, 2,1–7,4 detik, jadi bukan kemunduran akibat perubahan, tetapi syarat versi pengembang belum terbukti lulus hari itu. Validasi: hanya A2b, A4, A5 yang gagal, seperti sebelumnya. Uji tampilan semua file: angka sama, memori 81 MB. Menyeret, menambah, dan menghapus titik dengan mouse sungguhan: NOT RUN (EDIT-14).

**Smart select (2026-10-22):** klik objek tanpa AI di editor garis potong ([spesifikasi](../superpowers/specs/2026-10-22-smart-select-design.md)). Pemeriksaan otomatis S13 lulus (CUT-15); kotak centang dan Toleransi lewat skrip uji tampilan lulus, 6/6 langkah (EDIT-15); klik kanvas dengan mouse NOT RUN (EDIT-16). Garis otomatis tidak berubah: `validasi.json` sama persis dengan sebelum perubahan, pemeriksaan otomatis versi rilis dan pengembang 4/4 (17/17 skenario). File donat milik user (dari pngtree, tidak masuk repo): garis luar 49,51 mm dengan watermark menjadi 44,03 mm tanpa watermark, dan lubang tengah mendapat garis dalam 1,36 mm dengan 1 klik ([bukti](../../Printech-TestData/04-evidence/2026-10-22-smart-select.txt)).

**Masukan 2026-10-23 (kotak otomatis, preset bentuk, titik kurva, preset bahan dihapus):** [spesifikasi](../superpowers/specs/2026-10-23-bentuk-garis-potong-design.md).

- **Pemeriksaan otomatis:** S12 diperluas untuk kurva (CUT-14), S14 desain kotak (CUT-16), dan S15 preset bentuk (CUT-17). Versi rilis dan pengembang 4/4 (19/19 skenario).
- **Validasi:** syarat baru A7 lulus (4 desain kotak, selisih paling banyak 0,10 mm). A1 dan A5 disesuaikan untuk desain kotak. A2b, A4, dan A5 tetap gagal seperti sebelumnya, dengan lebih sedikit garis: A4 43 garis (sebelumnya 65), rentang A5 5 garis (sebelumnya 27).
- **Skrip uji tampilan:** uji editor tanpa mouse (`ui-editor-garis.ps1`) 7/7, termasuk tombol bentuk (EDIT-17). Uji semua file input (`ui-semua-input.ps1`) sama, kecuali lembar 1 92% (IMP-19).
- **Belum diuji:** menyeret titik dan pegangan kurva dengan mouse (EDIT-18, NOT RUN) dan potong di Graphtec (HW-17, BLOCKED).
- **Preset bahan dihapus:** langkah EDIT-09 di uji tata letak (`ui-edit-layout.ps1`) dan kasus EDIT-09 dihapus.
- **Bukti:** [catatan bentuk garis potong](../../Printech-TestData/04-evidence/2026-10-23-bentuk-garis-potong.txt).

**Warna border (2026-10-23):** pemeriksaan otomatis S16 lulus (CUT-18); tombol warna lewat skrip uji tampilan lulus, uji editor tanpa mouse (`ui-editor-garis.ps1`) 8/8 (EDIT-19); cetak dan potong di Develop/Graphtec BLOCKED (HW-18). [Bukti](../../Printech-TestData/04-evidence/2026-10-23-warna-border.txt).

**Temuan leader (2026-10-26):**

- Kiss dan die bisa berbeda bentuk: pemeriksaan otomatis S15 lulus (CUT-19).
- Klik titik tanpa menggeser, seret relatif, dan Shift+klik untuk beberapa titik: logikanya lulus di pemeriksaan otomatis S12, dan uji dengan mouse dan keyboard sungguhan (`ui-editor-mouse.ps1`) lulus (EDIT-20, CUT-19). [Bukti](../../Printech-TestData/04-evidence/2026-10-26-ui-editor-mouse.txt).
- Uji mouse dan keyboard yang lama diulang di versi ini: uji tata letak (`ui-edit-layout.ps1`) 10/10 dan uji urungkan (`ui-undo.ps1`) 3/3.
- Kasus editor yang sebelumnya NOT RUN kini diuji dengan mouse sungguhan dan lulus: EDIT-14 (operasi titik), EDIT-16 (smart select dengan klik di kanvas), dan EDIT-18 (pegangan kurva). Total uji editor dengan mouse (`ui-editor-mouse.ps1`) 8/8. Skrip berhenti sendiri bila jendela editor tidak aktif, supaya klik tidak masuk ke aplikasi lain.
- Stiker tidak lagi kena tanda registrasi: pemeriksaan otomatis S17 lulus (NEST-11).
- Validasi B-ui kini menyalakan tanda registrasi seperti bawaan aplikasi, jadi lembar 1 menjadi 4 pcs 90%, sama dengan layar (IMP-19). [Bukti](../../Printech-TestData/04-evidence/2026-10-26-validasi-garis-potong-nesting.txt).

**Ujung tajam warna border dan bleed (2026-10-27):** sebelumnya warna border dan bleed menjorok lebih jauh di ujung tajam: ujung Bintang 2,26 mm dan Segitiga 2,00 mm untuk border 1 mm, serta 4,52 mm untuk bleed 2 mm di ujung Bintang. Akibatnya warna bisa tercetak di stiker tetangga. Sekarang perluasan garis (`Sticker.Grow`) memakai sambungan bulat, jadi jaraknya tepat 1,00, 1,00, dan 2,00 mm. Pemeriksaan otomatis S16 lulus (CUT-18). Hasil validasi sama persis dengan 2026-10-26. [Bukti pemeriksaan otomatis](../../Printech-TestData/04-evidence/2026-10-27-selfcheck.txt), [bukti validasi](../../Printech-TestData/04-evidence/2026-10-27-validasi-garis-potong-nesting.txt).

**Uji mouse EDIT-16 mengikuti pengaturan aplikasi (2026-10-27):** aplikasi tidak menyimpan pengaturan antar-jalan, jadi uji editor dengan mouse (`ui-editor-mouse.ps1`) membaca lebar dan offset kiss dari aplikasi, lalu menghitung ukuran lubang donat uji. Pilihan `-Offset` mencoba offset lain. Setelah perbaikan ujung tajam, uji lulus 8/8 pada offset 2 mm dan 8/8 pada offset 3 mm. Skrip juga berhenti bila mouse disentuh selama uji. [Bukti](../../Printech-TestData/04-evidence/2026-10-27-ui-editor-mouse.txt), [bukti offset 3](../../Printech-TestData/04-evidence/2026-10-27-ui-editor-mouse-offset-3.txt).

**Keputusan D3 dan D5 (2026-10-27):** user menyetujui saran. Jarak garis ke artwork (A4 dan batas bawah rentang A5) memakai toleransi galat kurva 0,15 mm, dan target pengurangan titik minimal 60% hanya berlaku untuk `rough.jpg`. Validasi (`validasi.py`) kini selesai dengan exit code 0, jadi CUT-03, CUT-05, dan CUT-10 PASS. A4 tetap melaporkan 43 garis di bawah offset − 0,3 mm (terdalam 0,13 mm). [Bukti](../../Printech-TestData/04-evidence/2026-10-27-validasi-keputusan-d3-d5.txt).

**Bleed, D6, dan D9 (2026-10-28):** bleed sebelumnya membuat bagian artwork yang semi-transparan menjadi pekat. Bayangan tipis menjadi hitam, dan tepi foto yang memudar memperlihatkan latar asli yang tersembunyi (temuan dari tangkapan layar user). Sekarang warna perluasan hanya diambil dari piksel yang tidak transparan, lalu artwork ditumpuk di atasnya, jadi bayangan tetap tembus (CUT-13). Angka dpi dan warna merahnya kini memakai pembulatan yang sama (D6), dan file rusak memberi pesan untuk operator (D9: IMP-04, IMP-21). Pemeriksaan otomatis 4/4 (21/21), validasi exit code 0 dengan hasil penilaian sama persis dengan 2026-10-27. Perluasan warna tepi kini memakai rata-rata dari beberapa skala (cara pull-push), bukan perluasan piksel ke 8 arah (dilasi), jadi tepi bertekstur tidak lagi membentuk garis: pada gambar uji 120 juring hitam-putih, kecerahan zona bleed 124–130, sebelumnya 0–255. Stiker 300 mm dengan bleed dimuat 594 ms (tanpa bleed 211 ms). [Bukti bleed](../../Printech-TestData/04-evidence/2026-10-28-bukti-bleed-alpha.txt), [bukti bleed tanpa garis](../../Printech-TestData/04-evidence/2026-10-28-bukti-bleed-pull-push.txt), [bukti file rusak](../../Printech-TestData/04-evidence/2026-10-28-ui-file-khusus.txt).

**Tanda registrasi 1 mm (2026-10-28):** perubahan dari leader: tebal tanda registrasi 1 mm (sebelumnya 0,5 mm), dan kedua garis siku tersambung di sudutnya. Pemeriksaan otomatis 4/4; S17 lulus dengan batas jarak 6,50 mm, dan jaraknya sama seperti sebelumnya (susun 9,78 mm, tempel otomatis 7,50 mm) (NEST-11). Laporan validasi sama persis dengan sebelum perubahan.

## 5. Matriks format

| Aspek | PDF | AI (PDF-compatible) | CDR | PNG | JPG | SVG |
| --- | --- | --- | --- | --- | --- | --- |
| Dimuat, ukuran benar | IMP-01, IMP-02: PASS | IMP-03: PASS | IMP-05: BLOCKED | IMP-10: PASS | IMP-11: PASS | IMP-12: PASS |
| Banyak halaman jadi desain terpisah | IMP-02: PASS | NOT RUN | NOT RUN | — | — | — |
| Di PDF export | Vektor, warna CMYK utuh (EXP-04: PASS). Halaman yang diputar (/Rotate) jadi gambar + peringatan (IMP-16: PASS) | Vektor (EXP-04: PASS) | Vektor bila konversi berhasil (BLOCKED) | Gambar | Gambar | Gambar ±300 dpi (batasan di kebutuhan aplikasi) |
| dpi dan peringatan resolusi | Tidak tampil (vektor) | Tidak tampil | Tidak tampil | IMP-13: PASS | IMP-13: PASS | ±305 dpi |
| File rusak atau gagal | IMP-17: PASS (PDF terenkripsi jadi gambar + peringatan, D8 diperbaiki) | IMP-04: PASS | IMP-06: PASS; IMP-08: BLOCKED | IMP-21: PASS | IMP-21: PASS | IMP-21: PASS |
| Latar warna muda (abu 242, pink pastel) | — | — | — | IMP-23: PASS (sebelumnya hilang, D10) | Aturan sama, belum ada file uji | — |
| Latar putih | IMP-18: PASS (kotak dipertahankan) | Sama dengan PDF | — | Ada transparansi: putih dihitung artwork | Dipakai utuh, tidak dibuang (IMP-11) | — |

## 6. Kasus uji

Total {{TOTAL}} kasus: PASS {{PASS}}, FAIL {{FAIL}}, BLOCKED {{BLOCKED}}, NOT RUN {{NOT_RUN}}. Status per 2026-10-19; IMP-10, IMP-11, IMP-17, IMP-18, IMP-23, CUT-07, CUT-14, NEST-10, EDIT-12, EDIT-13, EDIT-14, HW-15, dan PERF-01 per 2026-10-21; EDIT-16 dan HW-16 per 2026-10-22; IMP-19, CUT-10, CUT-14, CUT-15, CUT-16, CUT-17, EDIT-13, EDIT-14, EDIT-15, EDIT-17, EDIT-18, EDIT-19, CUT-18, HW-15, HW-17, dan HW-18 per 2026-10-23; IMP-19, CUT-19, NEST-11, EDIT-12, EDIT-14, EDIT-16, EDIT-18, dan EDIT-20 per 2026-10-26; CUT-03, CUT-05, CUT-10, CUT-18, CUT-19, EDIT-14, EDIT-16, EDIT-18, dan EDIT-20 per 2026-10-27; CUT-13, IMP-04, IMP-21, dan NEST-11 per 2026-10-28.

Kolom Sumber menunjukkan asal syaratnya:

- **Dok:** sudah tertulis di kebutuhan aplikasi, dokumen uji, atau spesifikasi leader.
- **Kode:** perilaku yang ada di kode tetapi belum tertulis di dokumen.
- **Kriteria B / Permintaan user:** berasal dari rencana validasi yang dikirim user, belum masuk dokumen resmi.
- **Usul:** syarat baru yang masih perlu disetujui.

Lembar kerja yang sama, untuk diisi saat menguji: [lembar hasil uji (CSV)](lembar-hasil-uji-alur-lengkap.csv).

{{TESTCASES}}

## 7. Bukti yang harus dikumpulkan

- **Pemeriksaan otomatis:** `selfcheck.txt` dan exit code. Simpan salinannya di `Printech-TestData/04-evidence/`.
- **Validasi data uji:** laporan `04-evidence/<tanggal>-validasi-garis-potong-nesting.txt`, gambar bukti `04-evidence/<tanggal>-validasi-*.png`, dan hasil export di `03-exports/validasi/`.
- **Uji tampilan:** tangkapan layar bernama `TANGGAL-KODE-keterangan.png` di `Printech-TestData/04-evidence/`.
- **Operator dan mesin:** catat bahan, versi CorelDRAW dan Cutting Master 5, serta model dan firmware Graphtec.
  - Foto hasil cetak dan potong dengan penggaris atau kaca pembesar.
  - Ukuran selisih potong (mm) per desain.
- **Isi lembar hasil (CSV):** kolom Hasil sebenarnya, Status, Bukti (lokasi file bukti), dan Lingkungan (PC, Windows, versi build/commit). Kolom Bukti sudah berisi jenis bukti yang diminta; ganti dengan lokasi file bukti yang sebenarnya.

## 8. Persyaratan yang sudah tertulis dan syarat baru

### Sudah tertulis

- [Kebutuhan aplikasi](../kebutuhan-aplikasi.md) F1–F20: format input, pemangkasan, garis kiss/die, kertas, margin, susun banyak lembar, edit, export CMYK murni, bahasa, dpi 150, preview 2000 piksel, Rapikan, garis dengan sedikit titik, bleed paling besar sama dengan jarak, indikator dan target bahan, tempel otomatis dan preset bahan, Print Ticket.
- Kebutuhan aplikasi §6 (keputusan): penjaga jarak offset − 0,3 mm, die membungkus kiss, grid 1 mm, proses pembantu CDR 30 detik.
- Kebutuhan aplikasi §8 (batasan): hanya garis luar dan lubang asli yang tetap terbuka (donat) yang dipotong; SVG dan PDF yang diputar menjadi gambar dengan peringatan.
- [Skenario uji garis potong](../UJI-GARIS-POTONG.md) S1–S16, [skenario uji stabilitas](../UJI-STABILITAS.md) P1–P10, dan daftar uji operator di kebutuhan aplikasi §10 butir 1–18.
- Spesifikasi leader:
  - bagian 2: memori ±120 MB, tampilan tetap responsif;
  - bagian 3: CMYK murni (DeviceCMYK), bleed;
  - bagian 4: indikator bahan, tempel otomatis, preset bahan (dihapus 2026-10-23), Print Ticket;
  - tahap export (Printech-Deskripsi.pdf).

### Syarat baru (usulan, perlu disetujui)

1. Daftar periksa rilis 5 butir (bagian 9).
2. Angka toleransi validasi:
   - A1: median ±0,25 mm, jarak terjauh offset + 0,5 mm;
   - E1/X1: 0,05 mm;
   - B7: 0,5 poin persen;
   - X4: kecocokan 95%;
   - X5: ±1 poin;
   - ukuran artwork ±0,2/±0,3/±0,5 mm;
   - garis potong ±0,4 mm.
3. Toleransi selisih potong fisik di Graphtec. Usulan ±0,5 mm. [Catatan riset](../RESEARCH.md) menyebut ketelitian mesin ±0,3 mm, tetapi itu belum dipastikan.
4. Beban acuan untuk target memori ±120 MB, misalnya 20 desain termasuk satu foto 24 MP.
5. Perilaku yang belum ditetapkan:
   - PDF dengan kotak putih (IMP-18);
   - CDR diedit lalu ditambah ulang (IMP-09);
   - PDF sumber berubah sebelum export (EXP-05);
   - AI tanpa PDF-compatible (IMP-04);
   - konfirmasi sebelum Susun menimpa edit manual (EDIT-06).
6. **Diputuskan 2026-10-27:** target pengurangan titik 60% berlaku untuk logo kasar seperti `rough.jpg` (S1); logo bergelombang hanya dicatat (CUT-03, D5).
7. Definisi tingkat dampak (bagian 1), dan daftar pemeriksaan otomatis yang wajib untuk rilis.

## 9. Daftar periksa rilis (usulan) dan status 2026-10-19

| Butir | Kasus uji | Status |
| --- | --- | --- |
| 1. Build dan pengujian otomatis | Bagian 4 | Lulus 2026-10-27: build 0 warning, pemeriksaan otomatis 4/4, validasi exit code 0 setelah keputusan D3 dan D5. |
| 2. Input dan bentuk garis | IMP-\*, CUT-\*, NEST-\* | Belum. CUT-03, CUT-05, CUT-10 PASS sejak keputusan D3 dan D5 (2026-10-27); IMP-22 PASS dengan data uji pengganti (2026-10-31). IMP-05, IMP-08, IMP-09, IMP-18 BLOCKED. |
| 3. Export dan kecocokan dengan aplikasi lain | EXP-\*, HW-01 sampai HW-04 | Belum. Semua EXP lulus; PDF terenkripsi tidak lagi menggagalkan export (D8 diperbaiki 2026-10-21). HW-01 sampai HW-04 BLOCKED. |
| 4. Produksi fisik | HW-05 sampai HW-08 | Belum. BLOCKED. |
| 5. Dokumentasi dan masalah | Bagian 7 dan 10 | Belum. Tidak ada masalah yang masih terbuka (D1, D2, D4, D6, D8, D9, D10 sudah diperbaiki; D3, D5, D7 sudah diputuskan). |

## 10. Temuan dan masalah yang masih terbuka

| Kode | Temuan | Bukti | Lokasi di kode | Usulan tingkat dampak |
| --- | --- | --- | --- | --- |
| D1 | **Diperbaiki 2026-10-19:** simpanan hasil konversi CDR sekarang memakai versi file; bukti dengan CorelDRAW menunggu IMP-09. Sebelumnya: simpanan itu hanya memakai lokasi file sebagai kunci. Bila CDR diedit lalu ditambah ulang di sesi yang sama, hasil konversi lama yang dipakai. | Kode | [kode mesin](../../StickerLayout/Core.cs) | Blocker |
| D2 | **Diperbaiki 2026-10-19:** PDF/AI disalin saat dimuat; dicek pemeriksaan otomatis. Sebelumnya: file PDF dibaca ulang saat export, sedangkan garis potong dihitung saat dimuat. Bila file diubah di antaranya, artwork dan garis bisa tidak cocok. Bila file dipindah, export memakai gambar layar RGB beresolusi rendah dan hanya memberi peringatan. | Kode | [kode export](../../StickerLayout/Export.cs) | Mayor |
| D3 | **Diputuskan 2026-10-27:** toleransi galat kurva 0,15 mm diterima; validasi A4 dan batas bawah rentang A5 memakai offset − 0,3 − 0,15 mm (CUT-05 dan CUT-10 PASS). Sebelumnya: garis bisa sampai 0,13 mm lebih dekat dari batas offset − 0,3 mm, di sudut lancip dan pada offset kecil. Garis tetap di luar artwork. | validasi A4 | [kode mesin](../../StickerLayout/Core.cs) (penjaga jarak dan Potrace) | Mayor |
| D4 | **Diperbaiki 2026-10-21:** gambar resolusi penuh hasil muat langsung dilepas setelah menyusun, dan salinan layar tidak lagi memegang gambar sumber. Memori: semua 19 desain 279 → 84 MB, JPG 24 MP 226 → 41 MB, dua PDF besar 326 → 53 MB. Sebelumnya: pembersihan memori (GC) tidak jalan setelah muat, sehingga gambar hasil buka/gambar ulang resolusi penuh (JPG 24 MP: 96 MB di luar .NET + 96 MB di dalam .NET) tetap terhitung; logo kecil di halaman besar menahan gambar halaman penuh (±43 MB) selama desainnya ada. Puncak sesaat saat memuat masih tinggi. | [catatan profil memori](../../Printech-TestData/04-evidence/2026-10-21-ram-profil.txt) | [kode mesin](../../StickerLayout/Core.cs), [kode jendela utama](../../StickerLayout/MainWindow.xaml.cs) | Mayor |
| D5 | **Diputuskan 2026-10-27:** target pengurangan titik minimal 60% hanya untuk logo kasar seperti `rough.jpg` (S1); logo bergelombang dicatat sebagai info A2b (CUT-03 PASS). Sebelumnya: titik pada logo bertepi bergelombang hanya berkurang 54% (target 60%). | validasi A2b | — | Minor |
| D6 | **Diperbaiki 2026-10-28:** warna merah memakai pembulatan yang sama dengan angka dpi yang tampil (dicek pemeriksaan otomatis). Sebelumnya: nilai 149,5–149,99 dpi tampil sebagai "150 dpi" tetapi berwarna merah. | Kode | [kode mesin](../../StickerLayout/Core.cs) | Minor |
| D7 | **Diputuskan 2026-10-21:** latar tidak dibuang otomatis, jadi kotak putih di PDF selalu dipertahankan dan garis mengelilingi kotak (dicek uji sekali jalan, IMP-18). Sebelumnya: kotak putih yang tidak memenuhi halaman menghasilkan kotak 84 × 84 mm, sedangkan halaman putih penuh mengikuti logo (44 × 44 mm). | [catatan kotak putih](../../Printech-TestData/04-evidence/2026-10-21-d7-kotak-putih.txt) | [kode mesin](../../StickerLayout/Core.cs) | Mayor |
| D8 | **Diperbaiki 2026-10-21:** saat dimuat, halaman PDF dicoba ditempel seperti saat export; bila gagal, PDF ditempel sebagai gambar dengan peringatan. Dicek validasi X3 dan uji tampilan. Sebelumnya: PDF terenkripsi (hanya diberi kata sandi pemilik) lolos cek vektor saat dimuat, lalu export PDF berhenti dengan error "The archive entry was compressed using an unsupported compression method." sehingga seluruh export layout gagal. | [catatan PDF terenkripsi](../../Printech-TestData/04-evidence/2026-10-21-pdf-terenkripsi.txt) | [kode mesin](../../StickerLayout/Core.cs) | Mayor |
| D9 | **Diperbaiki 2026-10-28:** file rusak memberi pesan untuk operator per jenis file (AI, PDF, PNG/JPG, SVG); pesan teknisnya tetap tersimpan di dalam error (InnerException). Dicek pemeriksaan otomatis dan uji file khusus (`ui-file-khusus.ps1`). Sebelumnya: pesan error untuk file rusak masih teknis, sebagian berbahasa Inggris: "PDF tidak bisa dibuka (PDFium error 3)." untuk AI tanpa PDF-compatible; "No imaging component suitable…" untuk PNG/JPG; "Data at the root level is invalid…" untuk SVG. | Uji file khusus (`ui-file-khusus.ps1`) | [kode pembaca PDF](../../StickerLayout/Pdfium.cs), [kode jendela utama](../../StickerLayout/MainWindow.xaml.cs) | Minor |
| D10 | **Diperbaiki 2026-10-21, lalu digantikan masukan leader hari yang sama:** latar gambar tanpa bagian transparan tidak pernah dibuang otomatis, termasuk putih; dicek pemeriksaan otomatis. Laporan tim awal: latar warna muda (abu 242, pink pastel) hilang dari layar dan PDF, tinggal cincin selebar offset kiss, dan lebar stiker dipakai untuk logo saja. | [catatan latar PNG](../../Printech-TestData/04-evidence/2026-10-21-png-latar.txt) | [kode mesin](../../StickerLayout/Core.cs) | Mayor |

**Batasan yang sudah diketahui** (tercatat di kebutuhan aplikasi, bukan masalah baru):

- file selain CDR dimuat di jalur utama tampilan;
- Susun otomatis menimpa edit manual; sejak 2026-10-21 bisa dibatalkan dengan urungkan (Ctrl+Z);
- geser dengan panah tidak memakai tempel otomatis;
- SVG selalu ditempel sebagai gambar;
- halaman PDF di atas 400 mm diubah menjadi gambar paling besar 4000 piksel.

**Sudah diperbaiki (di-commit bersama dokumen ini):**

- penjaga jarak dibulatkan ke atas;
- die minimal 1 piksel di luar kiss;
- D1: simpanan hasil konversi CDR dengan versi file;
- D2: salinan PDF/AI saat dimuat;
- D8: PDF yang gagal ditempel saat dimuat menjadi gambar + peringatan, 2026-10-21;
- D4: gambar hasil muat dilepas setelah menyusun, dan salinan layar berdiri sendiri, 2026-10-21;
- D10: latar PNG/JPG warna muda tidak lagi dibuang (2026-10-21); sejak masukan leader tahap 1, tidak ada latar gambar tanpa bagian transparan yang dibuang otomatis;
- peringatan PDF yang ditempel sebagai gambar;
- D6 dan D9 (2026-10-28): warna dpi rendah memakai pembulatan angka yang tampil; pesan file rusak untuk operator;
- bleed tidak lagi membuat bagian artwork yang semi-transparan menjadi pekat (2026-10-28).

Semua perbaikan di atas ada di [kode mesin](../../StickerLayout/Core.cs), [kode jendela utama](../../StickerLayout/MainWindow.xaml.cs), dan [kode pembaca PDF](../../StickerLayout/Pdfium.cs).

## 11. Urutan perbaikan yang disarankan (berdasarkan risiko produksi)

D1 dan D2 sudah diperbaiki 2026-10-19; D4, D8, dan D10 pada 2026-10-21; D6 dan D9 pada 2026-10-28. Kasus jenis Operator dan mesin di luar cakupan prototipe, yang dipresentasikan dulu dari laptop tim. Berikutnya:

1. **Keputusan di bagian 12:** lokasi file customer (pengganti sementara 2026-10-31). Toleransi D3 dan D5 diputuskan 2026-10-27; pemeriksaan wajib untuk rilis diputuskan 2026-10-28; tingkat dampak diputuskan 2026-10-31.

## 12. Keputusan yang dibutuhkan

1. **A4/D3, diputuskan 2026-10-27:** toleransi 0,15 mm diterima dan ditulis di aturan.
2. **A2b/D5, diputuskan 2026-10-27:** target pengurangan titik minimal 60% hanya untuk `rough.jpg` (S1).
3. **Pemeriksaan otomatis yang wajib untuk rilis, diputuskan 2026-10-28:** sebelum push ke repo tim atau sebelum presentasi wajib build 0 warning, pemeriksaan otomatis 4/4, dan validasi (`validasi.py`) exit code 0. Bila tampilan, editor, atau alur di jendela aplikasi berubah, juga uji tata letak (`ui-edit-layout.ps1`) 10/10 dengan mouse dan keyboard sungguhan (PC tidak dipakai selama uji).
4. **Tingkat dampak, diputuskan 2026-10-31:** definisi tingkat dampak disetujui; label tiap kasus uji tetap usulan dan diperiksa saat kasusnya gagal.
5. **File customer, diputuskan 2026-10-31:** diganti sementara dengan data uji hasil riset di 01-input; file customer asli diuji bila sudah tersedia.
