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

Total 100 kasus: PASS 79, FAIL 0, BLOCKED 21, NOT RUN 0. Status per 2026-10-19; IMP-10, IMP-11, IMP-17, IMP-18, IMP-23, CUT-07, CUT-14, NEST-10, EDIT-12, EDIT-13, EDIT-14, HW-15, dan PERF-01 per 2026-10-21; EDIT-16 dan HW-16 per 2026-10-22; IMP-19, CUT-10, CUT-14, CUT-15, CUT-16, CUT-17, EDIT-13, EDIT-14, EDIT-15, EDIT-17, EDIT-18, EDIT-19, CUT-18, HW-15, HW-17, dan HW-18 per 2026-10-23; IMP-19, CUT-19, NEST-11, EDIT-12, EDIT-14, EDIT-16, EDIT-18, dan EDIT-20 per 2026-10-26; CUT-03, CUT-05, CUT-10, CUT-18, CUT-19, EDIT-14, EDIT-16, EDIT-18, dan EDIT-20 per 2026-10-27; CUT-13, IMP-04, IMP-21, dan NEST-11 per 2026-10-28.

Kolom Sumber menunjukkan asal syaratnya:

- **Dok:** sudah tertulis di kebutuhan aplikasi, dokumen uji, atau spesifikasi leader.
- **Kode:** perilaku yang ada di kode tetapi belum tertulis di dokumen.
- **Kriteria B / Permintaan user:** berasal dari rencana validasi yang dikirim user, belum masuk dokumen resmi.
- **Usul:** syarat baru yang masih perlu disetujui.

Lembar kerja yang sama, untuk diisi saat menguji: [lembar hasil uji (CSV)](lembar-hasil-uji-alur-lengkap.csv).

### Impor file

| Kode | Jenis | Input | Langkah | Hasil benar | Bukti | Sumber | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| IMP-01 | Validasi data uji + pemeriksaan otomatis | pdf-single-page.pdf | Tambah file; baca panel Desain | Vektor; artwork 60,0 × 30,0 mm (±0,2); garis kiss 64 × 34 mm (±0,4) | verifikasi-loader.txt; pemeriksaan otomatis logo.pdf | Dok: kebutuhan aplikasi F1, F2. Toleransi: usul | PASS |
| IMP-02 | Validasi data uji + pemeriksaan otomatis | pdf-multi-page.pdf, pdf-irregular-shapes.pdf | Tambah file | 3 dan 4 desain bernama "… p1" sampai "… pN"; ukuran per halaman sesuai hasil-yang-diharapkan.md | verifikasi-loader.txt; pemeriksaan otomatis multi.pdf | Dok: kebutuhan aplikasi F1 | PASS |
| IMP-03 | Validasi data uji + pemeriksaan otomatis | ai-compatible.ai | Tambah file | Vektor 50,0 × 25,0 mm | verifikasi-loader.txt; pemeriksaan otomatis logo.ai | Dok: kebutuhan aplikasi F1 | PASS |
| IMP-04 | Uji tampilan (skrip) | 01-input/khusus/ai-postscript.ai | Uji file khusus (ui-file-khusus.ps1) | Dialog "Gagal memuat" dengan pesan; aplikasi tidak tertutup sendiri (crash); file lain tetap masuk | 04-evidence/2026-10-28-ui-file-khusus.txt | Usul | PASS — Sejak 2026-10-28 (D9): "File AI ini tidak bisa dibaca: biasanya karena disimpan tanpa opsi PDF-compatible…" |
| IMP-05 | Operator dan mesin | cdr-normal.cdr | Tambah file | Sama dengan pdf-single-page.pdf: vektor 60,0 × 30,0 mm | Tangkapan layar panel Desain; waktu konversi (detik) | Dok: kebutuhan aplikasi F1; skenario uji stabilitas P7 | BLOCKED — CorelDRAW tidak ada di PC pengembangan |
| IMP-06 | Pemeriksaan otomatis + uji tampilan | invalid-file.cdr | Tambah bersama file lain | Dialog "Gagal memuat" berisi pesan CorelDRAW tidak terpasang; file lain tetap masuk; tidak crash | pemeriksaan otomatis P1; ui-semua-input.txt | Dok: kebutuhan aplikasi F1; skenario uji stabilitas P1 | PASS |
| IMP-07 | Pemeriksaan otomatis | Proses pembantu CDR dengan batas waktu 1 ms | Pemeriksaan otomatis P2 | Proses pembantu dihentikan dalam waktu kurang dari 2 detik; tidak ada proses yang tersisa | selfcheck.txt P2 | Dok: kebutuhan aplikasi F1; skenario uji stabilitas P2 | PASS — Hanya jalur kode batas waktu, bukan CorelDRAW asli (lihat IMP-08) |
| IMP-08 | Operator dan mesin | CDR apa pun | Tambah CDR (skenario uji stabilitas P9) | Printech tetap responsif; setelah 30 detik muncul pesan "tidak merespons dalam 30 detik"; setelah dialog ditutup, menambah ulang berhasil | Video atau tangkapan layar + catatan waktu | Dok: skenario uji stabilitas P9 | BLOCKED |
| IMP-09 | Operator dan mesin | CDR yang diedit lalu ditambah ulang | Tambah CDR; ubah dan simpan CDR di CorelDRAW; tambah lagi | Desain kedua memakai isi terbaru | Tangkapan layar kedua desain | Usul | BLOCKED — Kode diperbaiki 2026-10-19 (D1): simpanan hasil konversi memakai versi file; belum diuji dengan CorelDRAW |
| IMP-10 | Validasi data uji | png-transparent.png | Tambah file | Dipangkas sampai isinya, 50,0 × 25,0 mm; 406 dpi; lubang Ø 10 mm mendapat garis kiss dalam, die menutup lubang (aturan donat 2026-10-21) | verifikasi-loader.txt; validasi A5 | Dok: kebutuhan aplikasi F2 | PASS |
| IMP-11 | Validasi data uji | jpg-low-resolution.jpg | Tambah file | 50,0 × 50,0 mm (±0,5); seluruh gambar dipakai, latar putih tidak dibuang (masukan leader 2026-10-21) | verifikasi-loader.txt; validasi B-ui (2026-10-21) | Dok: kebutuhan aplikasi F2 | PASS — ±122 dpi sejak gambar dipakai utuh (sebelumnya ±102) |
| IMP-12 | Validasi data uji + pemeriksaan otomatis | svg-vector.svg | Tambah file | 50,0 × 25,0 mm; ±305 dpi; ditempel sebagai gambar (batasan di kebutuhan aplikasi) | verifikasi-loader.txt; pemeriksaan otomatis badge.svg | Dok: kebutuhan aplikasi F1 + batasan SVG (kebutuhan aplikasi §8) | PASS |
| IMP-13 | Validasi data uji + pemeriksaan otomatis + uji tampilan | jpg-low-resolution.jpg, png-147dpi.png, png-152dpi.png | Tambah file | ±102 dan 147 dpi merah, 152 dpi tidak; peringatan "2 desain resolusi rendah" | verifikasi-loader.txt; ui-semua-input.txt; pemeriksaan otomatis | Dok: kebutuhan aplikasi F12 (ambang 150 dpi masih tebakan, kebutuhan aplikasi §8) | PASS — Temuan D6: 149,5–149,99 dpi tampil "150 dpi" berwarna merah |
| IMP-14 | Validasi data uji + pemeriksaan otomatis | pdf-large-a3.pdf, jpg-large-24mp.jpg | Tambah file | Dimuat dalam waktu kurang dari 5 detik tanpa crash; A3 280 × 420 mm (±0,3) | verifikasi-loader.txt; pemeriksaan otomatis S9 | Dok: skenario uji garis potong S9. ±0,3: usul | PASS — Halaman di atas 400 mm diubah menjadi gambar paling besar 4000 piksel, ukuran tampil +0,2 mm |
| IMP-15 | Validasi data uji + uji tampilan | pdf-oversize.pdf | Tambah file | Tidak dipasang; peringatan "1 lebih besar dari kertas"; aplikasi tetap jalan | ui-semua-input.txt; validasi B1 | Usul (perilaku kode, belum tertulis di kebutuhan aplikasi) | PASS |
| IMP-16 | Validasi data uji + uji tampilan | pdf-rotated.pdf | Tambah file | Ditempel sebagai gambar ±254 dpi, 30 × 60 mm; peringatan "1 PDF ditempel sebagai gambar…" | validasi X3; ui-semua-input.txt | Dok: kebutuhan aplikasi §8 (batasan + peringatan) | PASS — Peringatan ditambahkan 2026-10-18, belum di-commit |
| IMP-17 | Validasi data uji + uji tampilan | 01-input/khusus/pdf-encrypted.pdf | Dimuat lalu diexport lewat mesin Printech; tampilan: aplikasi dibuka dengan file sebagai argumen; validasi X3 | Dimuat (vektor bila pustaka PDFsharp bisa menempelnya; bila tidak, gambar + peringatan); export tetap jadi tanpa error | 04-evidence/2026-10-21-pdf-terenkripsi.txt, -ui.png | Dok: kebutuhan aplikasi §8 | PASS — D8 diperbaiki 2026-10-21: dimuat sebagai gambar 60 × 30 mm + peringatan, export jadi. Sebelumnya FAIL: dimuat sebagai vektor, lalu export berhenti dengan error |
| IMP-18 | Uji sekali jalan | PDF berisi kotak putih + logo (kotak memenuhi halaman / tidak) | Dimuat lewat mesin pemuat Printech | Kotak putih dipertahankan, garis mengelilingi kotak (keputusan 2026-10-21: latar tidak dibuang otomatis) | 04-evidence/2026-10-21-d7-kotak-putih.txt | Masukan leader 2026-10-21; kebutuhan aplikasi §10 butir 9 | PASS — Halaman putih penuh 100 × 100 mm, kotak 84 × 84 mm: keduanya kotak. Sebelumnya halaman putih penuh mengikuti logo (D7) |
| IMP-19 | Uji tampilan | Semua file 01-input sekaligus | Jalankan aplikasi dengan semua file sebagai argumen | 19 desain; "18 stiker di 2 lembar."; 3 peringatan; "54% · sisa 46%"; judul lembar 4 pcs 90% dan 14 pcs 17% (tanda registrasi aktif sejak 2026-10-26; sebelumnya 92% dan 16%) | ui-semua-input.txt; 04-evidence/2026-10-26-validasi-garis-potong-nesting.txt | Usul (angka dari validasi B-ui) | PASS — Diulang 2026-10-26 (uji semua file input, ui-semua-input.ps1): status, peringatan, dan 54% sama; lembar 1 4 pcs 90% dan lembar 2 14 pcs 17%, sama dengan validasi B-ui, karena area 6 mm di sekitar tanda registrasi dikosongkan; memori 81 MB |
| IMP-20 | Validasi data uji | 01-input dan 01-input/customer | powershell -ExecutionPolicy Bypass -File cek-original.ps1 | Semua baris "sama"; exit code 0 | Hasil cek file asli | Permintaan user: file asli tidak berubah | PASS — 15/15 |
| IMP-21 | Uji tampilan (skrip) | 01-input/khusus/invalid-file.png, .jpg, .svg | Uji file khusus (ui-file-khusus.ps1) | Dialog "Gagal memuat"; aplikasi tidak crash; file lain tetap masuk | 04-evidence/2026-10-28-ui-file-khusus.txt | Usul | PASS — Sejak 2026-10-28 (D9): "Gambar rusak atau bukan file PNG/JPG yang valid." dan "SVG rusak atau bukan file SVG yang valid." |
| IMP-22 | Validasi data uji | File pengganti: data uji hasil riset di 01-input (PDF, AI, PNG, JPG, SVG, PDF banyak halaman, PDF besar) | validasi.ps1 lalu validasi.py | SEMUA LULUS | 04-evidence/ | Usul | PASS — Pengganti sementara (keputusan user 2026-10-31). File customer asli belum diuji; ganti lagi bila tersedia. |
| IMP-23 | Validasi data uji + uji tampilan | 01-input/latar/png-latar-abu-muda.png, png-latar-pink-pastel.png, png-latar-putih.png | Dimuat dan diexport lewat mesin Printech, warna latar diukur dari gambar render MuPDF; tampilan: aplikasi dibuka dengan ketiga file sebagai argumen | Semua latar utuh di layar dan PDF, termasuk putih (sejak 2026-10-21); garis kiss mengelilingi kotak gambar | 04-evidence/2026-10-21-png-latar.txt, -sebelum.png, -sesudah.png, -ui.png | Temuan tim 2026-10-21 (D10) | PASS — Sebelum perbaikan FAIL: latar tinggal 5 % dan logo ±4× lebih besar. Diperbaiki 2026-10-21 (fungsi Loader.WhiteBorder) |

### Garis potong

| Kode | Jenis | Input | Langkah | Hasil benar | Bukti | Sumber | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| CUT-01 | Validasi data uji | Lingkaran, persegi, AI, SVG, PNG, JPG | validasi A1 | Median jarak garis ke artwork = offset ±0,25 mm; jarak terjauh paling banyak offset + 0,5 mm | Laporan validasi A1 | Dok: kebutuhan aplikasi F3. Toleransi: usul | PASS — Median − offset −0,19 s.d. +0,02 |
| CUT-02 | Validasi data uji + pemeriksaan otomatis | png-rough-edge.png, bintang, rough.jpg | validasi A2a; pemeriksaan otomatis S5 | Panjang garis selalu turun seiring Halus naik; titik pada Halus 1 dan 2 lebih sedikit daripada Halus 0 | Laporan A2a; pemeriksaan otomatis S5; validasi-halus.png | Dok: kebutuhan aplikasi F16; skenario uji garis potong S5 | PASS |
| CUT-03 | Validasi data uji + pemeriksaan otomatis | rough.jpg (S1), png-rough-edge.png (A2b, info) | pemeriksaan otomatis S1; validasi A2b | rough.jpg: titik berkurang minimal 60% dari Halus 0 ke Halus 1; png-rough-edge.png hanya dicatat | pemeriksaan otomatis S1; 04-evidence/2026-10-27-validasi-keputusan-d3-d5.txt | Dok: kebutuhan aplikasi §8 (target 60–80%); S1; keputusan D5 2026-10-27 | PASS — S1 lulus (74%). png-rough-edge.png berkurang 54%, dicatat sebagai info sesuai keputusan D5 |
| CUT-04 | Validasi data uji + pemeriksaan otomatis | Semua desain uji | validasi A3; pemeriksaan otomatis S6 | 0 titik garis kiss di luar garis die | Laporan A3; pemeriksaan otomatis S6 | Dok: kebutuhan aplikasi §6 (die membungkus kiss) | PASS |
| CUT-05 | Validasi data uji | Semua desain uji | validasi A4 | Jarak terdekat garis ke artwork minimal offset − 0,3 mm, dengan toleransi galat kurva 0,15 mm | 04-evidence/2026-10-27-validasi-keputusan-d3-d5.txt | Dok: kebutuhan aplikasi §6 (penjaga jarak); keputusan D3 2026-10-27 | PASS — Validasi 2026-10-27: 158/158 garis di atas batas dengan toleransi; 43 garis di bawah offset − 0,3 mm, terdalam 0,13 mm; garis tetap di luar artwork |
| CUT-06 | Pemeriksaan otomatis | Bintang, offset 0,5 dan 0 mm | pemeriksaan otomatis S4 | Luas artwork di luar garis kurang dari 0,5 mm² (offset 0,5) dan kurang dari 3 mm² (offset 0) | pemeriksaan otomatis S4 | Dok: skenario uji garis potong S4 | PASS |
| CUT-07 | Validasi data uji + pemeriksaan otomatis | png-transparent.png, cincin C, bulatan kecil melingkar, donat besar dan kecil | pemeriksaan otomatis S11; validasi A5 | Rongga dari celah yang tertutup dan lubang kecil tidak dipotong; lubang asli yang tetap terbuka mendapat garis dalam kiss dan die; die membungkus kiss | pemeriksaan otomatis S11; laporan A5 | Masukan leader 2026-10-21 (stiker donat); kebutuhan aplikasi §8 | PASS — S11 2026-10-21: C 1/1, bulatan 1/1, donat besar 2/2, donat kecil 1/1. A5: jumlah garis tertutup dan lubang 100% benar |
| CUT-08 | Validasi data uji | Bintang, segitiga, logo kasar, garis tipis 0,2 mm | Verifikasi data uji + validasi | Ukuran garis dalam toleransi sudut lancip; garis tipis tetap dikelilingi (lebar kiss minimal 48,5 mm) | verifikasi-loader.txt; validasi-garis-potong.png | Usul (toleransi) | PASS |
| CUT-09 | Pemeriksaan otomatis + validasi data uji | Celah 3–3,9 mm (S7); tiga lingkaran celah 4 dan 15 mm | pemeriksaan otomatis S7; validasi A5 | Celah kurang dari 2 × (offset + halus) menyatu; celah 15 mm punya garis sendiri | pemeriksaan otomatis S7; laporan A5 | Dok: S7. Aturan celah: usul | PASS |
| CUT-10 | Validasi data uji + pemeriksaan otomatis | Semua | validasi A5; pemeriksaan otomatis S2, S8 | 0 sudut tajam atau garis lurus pada desain bukan kotak; lingkaran paling banyak 4 titik; garis bentuk sederhana di rentang [offset − 0,3; offset + 0,5], batas bawah dengan toleransi 0,15 mm seperti CUT-05 (desain kotak: batas atas offset × √2 + 0,5) | Laporan A5; pemeriksaan otomatis S2/S8; 04-evidence/2026-10-27-validasi-keputusan-d3-d5.txt | Dok: kebutuhan aplikasi F16, F25; keputusan D3 2026-10-27 | PASS — Validasi 2026-10-27: 0 garis keluar rentang (sebelumnya 5, semuanya batas bawah = CUT-05); 0 garis lurus pada 136 garis bukan kotak |
| CUT-11 | Validasi data uji | pdf-multi-page.pdf, png-rough-edge.png | validasi A6 | Magenta saja / cyan saja / keduanya / pesan "Aktifkan minimal satu garis potong." | Laporan A6 | Dok: kebutuhan aplikasi F3, F9 | PASS |
| CUT-12 | Uji tampilan (skrip) | Desain apa pun | Uji tata letak (ui-edit-layout.ps1) | Dialog peringatan garis potong (NeedCut / DieInside / BleedGap); layout tidak dibuat | 04-evidence/2026-10-19-ui-edit-layout.txt; diulang 2026-10-26-ui-edit-layout.txt (10/10) | Dok: kebutuhan aplikasi F3, F17 | PASS — 3 dari 3 jalan lulus |
| CUT-13 | Pemeriksaan otomatis + validasi data uji | PNG/JPG dengan bleed 1–2 mm | pemeriksaan otomatis bleed; validasi B4 | Warna meluas sampai bleed di luar kiss; bagian artwork yang semi-transparan tetap tembus; vektor tanpa bleed; area bleed di dalam margin | pemeriksaan otomatis (bleed, bayangan semi-transparan, tepi bertekstur); laporan B4; 04-evidence/2026-10-28-bukti-bleed-alpha.txt, 2026-10-28-bukti-bleed-pull-push.txt | Dok: kebutuhan aplikasi F17 | PASS — 2026-10-28: sebelumnya bayangan dan tepi foto yang memudar menjadi pekat saat bleed aktif (bayangan alpha 39 menjadi hitam). Sekarang warna perluasan hanya diambil dari piksel yang tidak transparan, dan artwork ditumpuk di atasnya: pemeriksaan otomatis bayangan #D77600, zona bleed oranye. Tepi bertekstur tidak lagi menjadi garis (perluasan rata-rata dari beberapa skala): 120 juring hitam/putih, kecerahan zona bleed 124-130, sebelumnya 0-255 |
| CUT-14 | Pemeriksaan otomatis | circle.png 40 mm, logo kasar, donat lubang Ø 21,5 mm | pemeriksaan otomatis S12: garis manual lurus dan kurva dimuat, titik digeser, lebar 40 → 80 mm, offset kiss 2 → 1 mm, die manual rapat + kiss otomatis, export PDF, pencocokan kurva Bezier untuk Simplify, membelah kurva, Jadikan kurva/garis, titik halus, editor tanpa mouse (tambah titik di kurva, pegangan, tombol titik) | Simpangan kurva ke garis titik paling banyak sebesar toleransi Simplify; titik berkurang bila toleransi naik; garis lurus = perintah garis lurus (M/L/Z) dan garis lurus di PDF; garis kurva = kurva di PDF; membelah kurva kurang dari 0,01 mm; grid berubah saat titik digeser; skala ikut lebar; offset tidak mengubah garis manual; kiss di dalam die; Simpan nonaktif saat kiss keluar die | pemeriksaan otomatis S12 | Masukan leader 2026-10-21 dan 2026-10-23 (titik pada garis dan kurva); kebutuhan aplikasi F23 | PASS — 2026-10-23: simpangan 0,185 mm (toleransi 0,2); titik logo kasar 21/17/10; PDF lurus 93 garis lurus, PDF kurva 11 kurva; membelah kurva 0,0066 mm; pegangan titik halus segaris; donat 2 → 1 garis kiss. Sebelumnya poligon: simpangan 0,125 mm, titik 64/32/16 |
| CUT-15 | Pemeriksaan otomatis | Cincin berlubang sempit Ø 2,6 mm, lubang jarum Ø 1,2 mm, takik 5 mm, watermark alpha 38, pulau; gambar tanpa bagian transparan berlatar putih | pemeriksaan otomatis S13: smart select langsung lewat mesin dan lewat editor tanpa mouse | Watermark tidak ikut pada Toleransi 64; takik diikuti; lubang sempit tertutup sampai diklik; klik kedua menghapus; lubang jarum ditolak; die membungkus kiss; Shift+klik menambah pulau; gambar tanpa bagian transparan memilih objek | pemeriksaan otomatis S13 | Spesifikasi smart select 2026-10-22; kebutuhan aplikasi F24 | PASS — Kiss kiri 8,16 mm (Toleransi 64) / -1,87 mm (16); garis 1, +lubang 2 (garis dalam 0,88 mm, die 1, kiss di luar die 0,00 mm2), klik lubang 2x 1, +pulau 2; lubang jarum ditolak; takik diikuti (garis otomatis menutupnya); gambar tanpa bagian transparan Toleransi 32, kiri 8,03 mm; editor 1/2/1/2/3 garis. Diulang 2026-10-23 dengan garis kurva: tepi kiri 8,20 mm, garis dalam 1,00 mm |
| CUT-16 | Pemeriksaan otomatis + validasi data uji | Gambar tanpa bagian transparan, PNG kotak dengan pinggiran transparan, JPG tanpa bagian transparan, PDF kotak (01-input: 4 desain) | pemeriksaan otomatis S14; validasi A7 (deteksi kotak sendiri dari file asli) | Garis kiss dan die = kotak artwork ± offset, 4 garis lurus, Halus tidak dipakai, die minimal kiss + 1 piksel; kotak bersudut bulat tetap kurva | pemeriksaan otomatis S14; laporan A7 | Masukan 2026-10-23 (bentuk kotak belum otomatis); kebutuhan aplikasi F25 | PASS — S14: selisih kotak 0,001 mm; A7: 4 desain kotak, 22 garis, selisih paling banyak 0,10 mm |
| CUT-17 | Pemeriksaan otomatis | circle.png 40 mm, teks PRINTECH 60 mm | pemeriksaan otomatis S15: 8 preset bentuk, export, editor tanpa mouse | Artwork + (offset − 0,3) di dalam kiss; die membungkus kiss; titik 4/8/4/4/3/10/4/6; ukuran bentuk terkecil −0,1 s.d. +0,7 mm; bentuk ikut offset dan lebar; titik yang diedit = garis manual | pemeriksaan otomatis S15; 04-evidence/2026-10-23-bentuk-garis-potong.txt, 2026-10-23-bentuk-preset.png | Masukan 2026-10-23 (preset bentuk); kebutuhan aplikasi F26 | PASS — Lingkaran 44,5 mm (terkecil 44), segitiga 76,7 × 66,4, segi enam 51,3 × 44,4, bintang 84,3; artwork keluar 0,00 mm²; offset 3 = 46,45 mm; lebar 60 = 64,58 mm |
| CUT-18 | Pemeriksaan otomatis | circle.png 40 mm, donat lubang Ø 21,5 mm, bentuk Bintang dan Segitiga | pemeriksaan otomatis S16: PDF export diubah menjadi gambar 10 piksel per mm, preview JPG, editor tanpa mouse | Pita offset berwarna; diteruskan 1 mm (bleed 0) atau sejauh bleed di luar garis kiss, juga di ujung tajam (tanpa lonjakan); artwork sama; tengah lubang putih; PDF `0 1 1 0 k`; preview sama dengan PDF | pemeriksaan otomatis S16; 04-evidence/2026-10-23-warna-border.txt, 2026-10-27-selfcheck.txt | Masukan 2026-10-23 (offset berwarna); kebutuhan aplikasi F27 | PASS — Merah #EE1D23 di PDF, #FA0105 di preview; 1,5 mm di luar garis: putih (bleed 0), merah (bleed 2). Ujung tajam (2026-10-27): menjorok 1,00 mm di ujung Bintang dan Segitiga, 2,00 mm untuk Bintang bleed 2; sebelum perbaikan 2,26, 2,00, dan 4,52 mm |
| CUT-19 | Pemeriksaan otomatis | circle.png 40 mm | pemeriksaan otomatis S15: editor kiss Bintang, lalu Garis yang diedit = Die-cut, lalu Kotak; muat ulang (Bintang, Kotak) dan (Otomatis, Kotak) | Kiss 10 titik, die 4 titik; tombol Kotak tertandai saat die dipilih; kiss bintang + 1,9 mm di dalam die; kiss otomatis di dalam die kotak 48 × 48 mm; tanda daftar desain "Bintang · die Kotak" | pemeriksaan otomatis S15 | Temuan leader 2026-10-26 (kiss dan die jangan digabung); kebutuhan aplikasi F26 | PASS — Versi lama gagal: die ikut bentuk kiss. Lewat tampilan (uji editor dengan mouse, ui-editor-mouse.ps1 CUT-19/CUT-19b, 2026-10-26): Info 'Kiss 10 titik · die 4 titik · Bintang · die Kotak', tombol bentuk mengikuti garis yang dipilih, Simpan memberi tanda 'Bintang · die Kotak' |

### Susun otomatis

| Kode | Jenis | Input | Langkah | Hasil benar | Bukti | Sumber | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| NEST-01 | Validasi data uji + pemeriksaan otomatis | Skenario B-sedikit sampai B-ui | validasi B1; pemeriksaan otomatis | Terpasang + tidak muat = yang diminta; per desain = jumlah salinan | Laporan B1 | Kriteria B (rencana validasi user) | PASS |
| NEST-02 | Validasi data uji + pemeriksaan otomatis | Sama | validasi B2; cek tumpang tindih di pemeriksaan otomatis | 0 pasangan garis terluar yang bersinggungan atau saling memuat | Laporan B2 | Dok: kebutuhan aplikasi F7 | PASS — 0 dari 997 pasangan |
| NEST-03 | Validasi data uji | Sama | validasi B3 | Jarak terkecil antar garis terluar stiker yang berbeda minimal 2,0 mm | Laporan B3 | Dok: spesifikasi jarak 2 mm | PASS — Terkecil 2,50 mm |
| NEST-04 | Validasi data uji | Sama, termasuk bleed 1 mm | validasi B4 | Garis terluar minimal 10 mm dari tepi; garis + bleed minimal 10 mm | Laporan B4 | Dok: kebutuhan aplikasi F5 | PASS — Terkecil 10,44 mm |
| NEST-05 | Validasi data uji | B-rotasi + susun otomatis | validasi B5 | Jarak garis–artwork di PDF sama di 4 rotasi (selisih paling banyak 0,02 mm) | Laporan B5 | Kriteria B (rencana validasi user) | PASS |
| NEST-06 | Validasi data uji + pemeriksaan otomatis | B-campuran, B-banyak-lembar | validasi B6; pemeriksaan otomatis | Jumlah lembar minimal sebanyak batas bawah dari luas; tidak ada lembar kosong; stiker lembar terakhir tidak muat di lembar sebelumnya | Laporan B6 | Dok: kebutuhan aplikasi F6 | PASS |
| NEST-07 | Validasi data uji + uji tampilan | B-ui | validasi B7; uji tampilan | Pemakaian bahan Printech = hitung ulang dari garis (±0,5 poin); angka di layar = data | Laporan B7; ui-semua-input.txt | Dok: kebutuhan aplikasi F18. ±0,5: usul | PASS |
| NEST-08 | Pemeriksaan otomatis | 130 stiker | pemeriksaan otomatis P3 | 5 kali susun = 1 hasil | pemeriksaan otomatis P3 | Dok: skenario uji stabilitas P3 | PASS |
| NEST-09 | Pemeriksaan otomatis | 130 stiker | pemeriksaan otomatis P5 | Susun kurang dari 1000 ms | pemeriksaan otomatis P5 | Dok: skenario uji stabilitas P5 | PASS |
| NEST-10 | Pemeriksaan otomatis + validasi data uji | Semua lembar setelah susun | Penuhi lembar (dulu Compact); pemeriksaan otomatis; validasi B-campuran | Ruang kosong diisi salinan desain bergiliran; jumlah lembar tetap; 0 tabrakan; jarak dan margin tetap; mengisi ulang menambah 0 | pemeriksaan otomatis (penuhi lembar); laporan B1–B4 2026-10-21 | Masukan leader 2026-10-21 (customer bayar per lembar) | PASS — Pemeriksaan otomatis (kertas A4): 26 → 36 stiker, 5 desain bertambah. B-campuran: 160 → 198 stiker di 4 lembar. Pangkas tinggi lembar terakhir (untuk bahan gulungan) dihapus |
| NEST-11 | Pemeriksaan otomatis + validasi data uji | Semua desain uji (A4) dan B-ui (A3+) | pemeriksaan otomatis S17: susun, Penuhi lembar, tempel otomatis, cek pojok; validasi B-ui dengan tanda aktif | Garis terluar stiker minimal 6 mm + setengah tebal tanda dari garis tanda (area pindai Graphtec a = 6 mm): 6,25 mm saat tebal 0,5 mm, 6,50 mm sejak tebal 1 mm; stiker di pojok ditandai merah hanya saat tanda aktif; tebal tanda 0,3–1,0 mm, panjang 5–20 mm | pemeriksaan otomatis S17; 04-evidence/2026-10-26-validasi-garis-potong-nesting.txt | Temuan leader 2026-10-26 (stiker kena tanda registrasi); Graphtec CE7000 User's Manual bab 5; kebutuhan aplikasi F28 | PASS — Susun/Penuhi lembar 9,78 mm, tempel otomatis 7,50 mm; sebelum perbaikan 2,16 mm dan tebal 0,25 mm. Sejak perubahan dari leader 2026-10-28 (tebal 1 mm): lulus 2026-10-28 dengan jarak yang sama (batas 6,50 mm) |

### Edit tata letak

| Kode | Jenis | Input | Langkah | Hasil benar | Bukti | Sumber | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| EDIT-01 | Uji tampilan (skrip) | Layout apa pun | Uji tata letak (ui-edit-layout.ps1, langkah EDIT-01) | Pindah ke posisi kosong terdekat; tidak merah | 04-evidence/2026-10-19-ui-edit-layout.txt; diulang 2026-10-26-ui-edit-layout.txt (10/10) | Dok: kebutuhan aplikasi F7, F19 | PASS — Fungsi tempel otomatis (Nest.Snap) lulus di pemeriksaan otomatis |
| EDIT-02 | Uji tampilan (skrip) | Layout apa pun | Uji tata letak (ui-edit-layout.ps1, langkah EDIT-02) | Putar 90° + tempel otomatis; garis dan artwork ikut berputar | 04-evidence/2026-10-19-ui-edit-layout.txt; diulang 2026-10-26-ui-edit-layout.txt (10/10) | Dok: kebutuhan aplikasi F7, F19 | PASS — Tempel otomatis setelah putar lulus di pemeriksaan otomatis |
| EDIT-03 | Uji tampilan (skrip) | Layout apa pun | Uji tata letak (ui-edit-layout.ps1, langkah EDIT-03) | Bergerak tanpa tempel otomatis; menabrak atau keluar margin = merah + peringatan | 04-evidence/2026-10-19-ui-edit-layout.txt; diulang 2026-10-26-ui-edit-layout.txt (10/10) | Dok: kebutuhan aplikasi F7 | PASS |
| EDIT-04 | Uji tampilan (skrip) | Layout apa pun | Uji tata letak (ui-edit-layout.ps1, langkah EDIT-04) | Stiker hilang; status dan bahan diperbarui; lembar kosong dihapus | 04-evidence/2026-10-19-ui-edit-layout.txt; diulang 2026-10-26-ui-edit-layout.txt (10/10) | Dok: kebutuhan aplikasi F7 | PASS |
| EDIT-05 | Uji tampilan (skrip) | Desain gambar | Uji tata letak (ui-edit-layout.ps1, langkah EDIT-05) | Desain dimuat ulang dengan lebar baru; jumlah stiker = total salinan | 04-evidence/2026-10-19-ui-edit-layout.txt; diulang 2026-10-26-ui-edit-layout.txt (10/10) | Dok: kebutuhan aplikasi F2 | PASS |
| EDIT-06 | Uji tampilan (skrip) | Layout dengan edit manual | Uji tata letak (ui-edit-layout.ps1, langkah EDIT-06) | Layout dibuat ulang; edit manual hilang (belum ada urungkan) | 04-evidence/2026-10-19-ui-edit-layout.txt; diulang 2026-10-26-ui-edit-layout.txt (10/10) | Dok: kebutuhan aplikasi §8 (belum ada urungkan) | PASS — Usul: konfirmasi sebelum menimpa edit manual |
| EDIT-07 | Uji tampilan (skrip) | Layout dengan stiker merah | Uji tata letak (ui-edit-layout.ps1, langkah EDIT-07) | Dialog konfirmasi muncul; pilih Tidak = export batal | 04-evidence/2026-10-19-ui-edit-layout.txt; diulang 2026-10-26-ui-edit-layout.txt (10/10) | Kode (jendela utama) | PASS |
| EDIT-08 | Uji tampilan (skrip) | Layout apa pun | Uji tata letak (ui-edit-layout.ps1, langkah EDIT-08) | Layout dikosongkan lalu langsung disusun ulang di kertas baru; judul lembar menyebut A4 | 04-evidence/2026-10-19-ui-edit-layout.txt; diulang 2026-10-26-ui-edit-layout.txt (10/10) | Dok: kebutuhan aplikasi F4 | PASS — Harapan dikoreksi sesuai kode; 3 dari 3 jalan lulus |
| EDIT-10 | Pemeriksaan otomatis | Foto berlatar polos | Rapikan: hapus latar, penghapus klik, kikis tepi, urungkan | Latar hilang; urungkan mengembalikan; export memakai hasil Rapikan | pemeriksaan otomatis (Rapikan), P4, P5 | Dok: kebutuhan aplikasi F15 (fitur dibekukan) | PASS — Jendela Rapikan tidak diuji ulang pada pemeriksaan ini |
| EDIT-12 | Uji tampilan (skrip) | 4 desain dari 01-input | Uji urungkan (ui-undo.ps1): Penuhi lembar, menu Urungkan, Ctrl+Y, Ctrl+Z, Ctrl+Shift+Z | Jumlah stiker kembali dan maju sesuai langkah; Ctrl+Shift+Z tidak memilih alat Zoom | 04-evidence/2026-10-21-ui-undo.txt; diulang 2026-10-26-ui-undo.txt (3/3) | Masukan leader 2026-10-21 | PASS — 4 → 58 → 4 → 58 → 4 → 58 stiker |
| EDIT-11 | Uji tampilan (skrip) | - | Uji tata letak (ui-edit-layout.ps1, langkah EDIT-11) | Semua teks tampilan dan Print Ticket berbahasa Inggris | 04-evidence/2026-10-19-ui-edit-layout.txt; diulang 2026-10-26-ui-edit-layout.txt (10/10) | Dok: kebutuhan aplikasi F10 | PASS |
| EDIT-13 | Uji tampilan (skrip) | png-transparent.png | Uji editor tanpa mouse (ui-editor-garis.ps1): buka Edit garis potong, Simplify 0,5 mm, tombol Urungkan/Ulangi editor, Simpan, menu Edit > Urungkan | Editor menampilkan kiss dan die; Simplify mengurangi titik garis yang diedit saja; Simpan memberi tanda "garis manual"; Urungkan di jendela utama mengembalikan garis otomatis | 04-evidence/2026-10-23-bentuk-garis-potong.txt, 2026-10-21-editor-garis-potong.png, 2026-10-21-editor-garis-manual.png | Masukan leader 2026-10-21; kebutuhan aplikasi F23 | PASS — 2026-10-23: 8/8 langkah lulus; titik kurva: kiss 14 → 11 titik, die 10 tetap (poligon 2026-10-21: 31 → 22 titik) |
| EDIT-14 | Uji tampilan | Desain apa pun | Seret titik, klik di garis atau kurva untuk menambah titik, Delete, Ctrl+Z/Ctrl+Y di editor, seret titik keluar kotak | Titik mengikuti mouse dan berhenti di batas kotak; titik baru di posisi klik tanpa mengubah bentuk; garis tertutup dengan 3 titik terhapus bila ada garis lain; Simpan nonaktif saat kiss keluar die | 04-evidence/2026-10-26-ui-editor-mouse.txt, 2026-10-26-editor-mouse-batas-kotak.png | Masukan leader 2026-10-21; kebutuhan aplikasi F23 | PASS — Mouse dan keyboard sungguhan (uji editor dengan mouse, ui-editor-mouse.ps1, png-transparent.png): titik bintang diseret jauh ke kanan berhenti di batas kanan kotak, dan Simpan nonaktif; Ctrl+Z dan Ctrl+Y di editor; klik di tengah sisi bintang menambah 1 titik di posisi klik tanpa mengubah garis, Delete menghapusnya; garis dalam otomatis (2 titik) hilang seluruhnya setelah 1 Delete, garis luar tetap. Logikanya juga lulus di pemeriksaan otomatis S12 (CUT-14) |
| EDIT-15 | Uji tampilan (skrip) | png-transparent.png | Uji editor tanpa mouse (ui-editor-garis.ps1): centang Smart select, baca Toleransi awal | Kotak centang bisa dinyalakan; Toleransi awal 64 untuk PNG transparan | 04-evidence/2026-10-22-smart-select.txt | Spesifikasi smart select 2026-10-22 | PASS — Langkah EDIT-04 di skrip; 7/7 langkah lulus 2026-10-23 |
| EDIT-16 | Uji tampilan | Stiker donat berwatermark | Klik donat, klik lubang tengah, klik lagi, Shift+klik objek lain, geser Toleransi | Garis mengikuti donat tanpa watermark; garis dalam muncul lalu hilang; objek kedua ikut; Toleransi menghitung ulang | 04-evidence/2026-10-27-ui-editor-mouse.txt, 2026-10-27-ui-editor-mouse-offset-3.txt, 2026-10-27-editor-mouse-smart-offset-3.png, 2026-10-26-editor-mouse-smart-toleransi-16.png | Spesifikasi smart select 2026-10-22 | PASS — Mouse sungguhan pada gambar uji buatan skrip uji editor dengan mouse (ui-editor-mouse.ps1): donat dengan lubang sempit, watermark alpha 38 yang menempel, dan kotak terpisah. Jari-jari lubang dihitung dari lebar dan offset kiss di aplikasi, R = (offset + 1,3) / 2 mm. Lulus pada offset 2 mm (R 1,65 mm) dan offset 3 mm (R 2,15 mm; garis bergeser keluar ±1 mm). Klik donat: garis luar tanpa watermark; klik lubang: kiss 3 → 5 titik (garis dalam muncul), klik lagi: 3 titik (hilang); Shift+klik kotak: 10 titik; Toleransi 16: garis ikut membungkus watermark. Logikanya juga lulus di pemeriksaan otomatis S13 (CUT-15) dan pada file donat user lewat mesin |
| EDIT-17 | Uji tampilan (skrip) | png-transparent.png | Uji editor tanpa mouse (ui-editor-garis.ps1) langkah EDIT-07: tombol Lingkaran, Simpan, offset kiss 3, Susun, buka editor lagi | Kiss 4 titik bertanda Lingkaran; daftar desain menampilkan tanda "Lingkaran"; setelah offset diubah dan disusun ulang, bentuk tetap lingkaran | 04-evidence/2026-10-23-bentuk-garis-potong.txt, 2026-10-23-editor-bentuk-lingkaran.png | Masukan 2026-10-23 (preset bentuk); kebutuhan aplikasi F26 | PASS — 8/8 langkah skrip lulus (2026-10-23) |
| EDIT-18 | Uji tampilan | Desain apa pun | Klik titik, seret pegangan, Jadikan kurva/Jadikan garis, Node halus/Node sudut, pilih bentuk lain lalu edit titik | Pegangan tampil di titik yang dipilih; lengkung mengikuti pegangan; titik halus menjaga pegangan segaris; bentuk yang diedit menjadi garis manual | 04-evidence/2026-10-26-ui-editor-mouse.txt, 2026-10-26-editor-mouse-handle-halus.png | Masukan 2026-10-23; kebutuhan aplikasi F23, F26 | PASS — Mouse sungguhan: kiss Lingkaran (4 titik), klik titik atas: titik terpilih dan 2 pegangan tampil; pegangan kanan diseret 20 piksel ke atas: pegangan kiri ikut segaris dengan panjang tetap (titik halus), lengkung mengikuti pegangan (tangkapan layar), Info menjadi garis manual; Node sudut: pegangan kanan diseret, pegangan kiri tetap; Jadikan garis: pegangan hilang; Jadikan kurva: pegangan di 1/3 tali busur. Logikanya juga lulus di pemeriksaan otomatis S12 dan S15 |
| EDIT-20 | Uji tampilan (mouse sungguhan) | png-transparent.png, bentuk Bintang | Uji editor dengan mouse (ui-editor-mouse.ps1): klik 3 piksel dari titik tanpa menggeser; tekan 4 piksel dari titik lalu seret +30/+15 piksel; Ctrl+Z; klik titik kiri, Shift+klik titik kanan, seret 25 piksel, Delete | Klik saja tidak memindah titik dan tidak membuat garis manual; seret menggeser titik sejauh gerakan mouse (tidak melompat ke kursor); Shift+klik memilih 2 titik, seret dan Delete berlaku untuk keduanya | 04-evidence/2026-10-26-ui-editor-mouse.txt, 2026-10-26-editor-mouse-shift-dua-node.png | Temuan leader 2026-10-26; kebutuhan aplikasi F23 | PASS — Mouse dan keyboard sungguhan, titik dicari dari tangkapan layar: EDIT-20a/b/c lulus (klik memilih titik tanpa menggesernya, bergeser +30/+15 piksel bukan ke kursor, 2 titik bergeser lalu terhapus: kiss 10 → 8 titik). Logikanya juga lulus di pemeriksaan otomatis S12 |
| EDIT-19 | Uji tampilan (skrip) | png-transparent.png | Uji editor tanpa mouse (ui-editor-garis.ps1) langkah EDIT-08: tombol Merah, Simpan, buka editor lagi | Kotak CMYK berisi 0 100 100 0; warna tetap setelah disimpan; lembar menampilkan border merah | 04-evidence/2026-10-23-warna-border.txt, 2026-10-23-lembar-warna-border.png | Masukan 2026-10-23; kebutuhan aplikasi F27 | PASS — 8/8 langkah skrip lulus |

### Export

| Kode | Jenis | Input | Langkah | Hasil benar | Bukti | Sumber | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| EXP-01 | Validasi data uji + pemeriksaan otomatis | Semua skenario | validasi X1, E1; pemeriksaan otomatis | Halaman = lembar; ukuran halaman ±0,01 mm; posisi artwork ±0,05 mm; ukuran desain = file asli ±0,05 mm | Laporan X1/E1 | Dok: spesifikasi export. Toleransi: usul | PASS |
| EXP-02 | Validasi data uji + pemeriksaan otomatis | Semua | validasi X2 (pemeriksaan isi PDF); pemeriksaan otomatis S8 | Garis hanya `0 1 0 0 K` (kiss) dan `1 0 0 0 K` (die); 0 warna RGB, profil warna (ICC), atau warna khusus (Separation); 0 profil output (OutputIntent) | Laporan X2; pemeriksaan otomatis S8 | Dok: kebutuhan aplikasi F9 | PASS |
| EXP-03 | Validasi data uji | Semua | validasi X2 (pisah warna CMYK oleh MuPDF, tanpa penghalusan tepi) | Kiss tepat C0 M100 Y0 K0; die tepat C100 M0 Y0 K0 | Laporan X2; validasi-separasi-cmyk.png | Dok: spesifikasi tahap 4 | PASS — Pisah warna di CorelDRAW: HW-02 |
| EXP-04 | Validasi data uji + pemeriksaan otomatis | PDF/AI | validasi X3; pemeriksaan otomatis | PDF/AI = objek vektor (Form XObject); warna isi CMYK sama dengan file input; gambar hanya PNG/JPG/SVG dan PDF yang diputar | Laporan X3 | Dok: kebutuhan aplikasi F9 | PASS |
| EXP-05 | Pemeriksaan otomatis | logo.pdf, lalu isi file diganti multi.pdf dan file dihapus | pemeriksaan otomatis: muat PDF; ganti isi file; muat ulang seperti Susun; tambah ulang; hapus file; export | Muat ulang dan export memakai isi saat dimuat (49,5 mm, tanpa peringatan); menambah ulang memakai isi baru (28,3 mm) | selfcheck.txt baris "PDF diganti lalu dihapus setelah dimuat" | Usul | PASS — Diperbaiki 2026-10-19 (D2): PDF/AI disalin ke folder sementara saat dimuat |
| EXP-06 | Validasi data uji + pemeriksaan otomatis | Skenario B | validasi X4; pemeriksaan otomatis | 1 JPEG per lembar; sisi panjang 2000 piksel; isi di posisi stiker sama dengan PDF (cocok minimal 95%) | Laporan X4 | Dok: kebutuhan aplikasi F13 | PASS |
| EXP-07 | Validasi data uji + pemeriksaan otomatis | Skenario B | validasi X5; pemeriksaan otomatis | 1 halaman A4; total lembar, total pcs, efisiensi, per lembar, per desain, dan pengaturan sesuai layout | Laporan X5 | Dok: kebutuhan aplikasi F20 | PASS |
| EXP-08 | Uji sekali jalan | 22 desain dari 01-input, A3 × 12 (13 lembar, 21 desain terpasang) | Print Ticket dibuat lewat mesin export | Tetap 1 halaman; ada baris "… dan N lainnya" | 04-evidence/2026-10-19-print-ticket-panjang.txt; 03-exports/2026-10-19-print-ticket-13-lembar.pdf | Kode (kode export) | PASS — Baris "… dan 3 lainnya" (lembar) dan "… dan 1 lainnya" (desain); blok terbawah 270 mm |

### Operator dan mesin

| Kode | Jenis | Input | Langkah | Hasil benar | Bukti | Sumber | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| HW-01 | Operator dan mesin | PDF export (B-campuran.pdf + export dari aplikasi) | Import atau buka PDF | Halaman 320 × 480 mm; artwork PDF tetap kurva (bukan gambar piksel); warna sama dengan file asli | Tangkapan layar Object Properties | Dok: kebutuhan aplikasi §10 butir 6 | BLOCKED |
| HW-02 | Operator dan mesin | Sama | Klik garis kiss dan die; buka pratinjau pisah warna (separasi) | Kiss C0 M100 Y0 K0; die C100 M0 Y0 K0; kiss hanya di pelat M, die hanya di pelat C | Tangkapan layar | Dok: kebutuhan aplikasi §10 butir 10; spesifikasi tahap 4 | BLOCKED |
| HW-03 | Operator dan mesin | circle.png, rough.jpg (40 mm) | Hitung titik | circle: kiss 3, die 4; rough.jpg: kiss 17, die 16 (±1) | Tangkapan layar | Dok: skenario uji garis potong S8 (bagian manual) | BLOCKED — Angka rough.jpg berubah sejak 2026-10-18 |
| HW-04 | Operator dan mesin | Sama | Pilih garis per warna | Magenta untuk kiss, cyan untuk die; tidak ada garis lain yang ikut | Tangkapan layar dialog Cutting Master 5 | Dok: kebutuhan aplikasi §10 butir 7 | BLOCKED |
| HW-05 | Operator dan mesin | Lembar A3+ | Cetak di Develop | Artwork tajam; warna sesuai file asli; tanda registrasi terbaca Graphtec | Foto hasil cetak | Dok: kebutuhan aplikasi §10 butir 6 dan 8 | BLOCKED — Belum dipastikan: cara operator menyembunyikan garis potong saat cetak |
| HW-06 | Operator dan mesin | 3–5 desain (bulat, teks, logo kasar, berlubang, berdekatan), 4 salinan | Cetak lalu potong kiss (skenario uji garis potong S10) | Pisau mulus; garis tidak mengenai artwork; border rata; selisih potong dalam toleransi (usul ±0,5 mm, belum disetujui) | Foto + ukuran selisih (mm) per desain | Dok: S10; kebutuhan aplikasi §10 butir 8. Toleransi: usul | BLOCKED |
| HW-07 | Operator dan mesin | Sama, die-cut | Potong die | Tembus; stiker lepas utuh; die tidak memotong stiker kiss | Foto | Dok: S10 | BLOCKED |
| HW-08 | Operator dan mesin | PNG dengan bleed 2 mm, offset kiss 0,5 mm | Cetak lalu potong | Tepi stiker tetap berwarna, tanpa garis putih | Foto | Dok: kebutuhan aplikasi §10 butir 10 | BLOCKED |
| HW-09 | Operator dan mesin | CDR asli | Skenario uji stabilitas P7 | Desain masuk; waktu dicatat untuk menyetel batas 30 detik | Catatan waktu | Dok: skenario uji stabilitas P7 | BLOCKED |
| HW-10 | Operator dan mesin | CDR | Skenario uji stabilitas P8 | Dokumen operator utuh; CorelDRAW tidak tertutup | Tangkapan layar | Dok: skenario uji stabilitas P8 | BLOCKED |
| HW-11 | Operator dan mesin | 3 file CDR | Skenario uji stabilitas P10 | Ketiganya masuk berurutan; tampilan responsif; Susun tidak membuka CorelDRAW lagi | Video atau catatan | Dok: skenario uji stabilitas P10 | BLOCKED |
| HW-12 | Operator dan mesin | Zip operator | Unblock zip, jalankan, lalu pemeriksaan otomatis (--selfcheck) | Aplikasi terbuka; pemeriksaan otomatis exit code 0 di PC operator | Hasil pemeriksaan otomatis | Dok: kebutuhan aplikasi §11 | BLOCKED — Ditunda: prototipe dipresentasikan dulu dari laptop tim, belum dipasang di PC toko. Zip dari unduhan kode baru 2026-10-23: pemeriksaan otomatis aplikasi hasil ekstrak zip di PC pengembang exit code 0, 20/20 |
| HW-13 | Operator dan mesin | Preview JPG | Kirim ke customer | Cukup jelas untuk konfirmasi | Tangkapan layar WhatsApp | Dok: kebutuhan aplikasi §10 butir 3 | BLOCKED |
| HW-14 | Operator dan mesin | Print Ticket | Lampirkan ke SPK | Isinya cukup untuk kasir | Catatan operator | Dok: kebutuhan aplikasi §10 butir 11 | BLOCKED |
| HW-15 | Operator dan mesin | Desain dengan garis manual (titik lurus dan kurva) | Edit garis potong, export, buka di Cutting Master 5, potong | Potongan mengikuti titik yang diatur; pisau mulus di setiap titik | Foto hasil potong | Dok: kebutuhan aplikasi §10 butir 12 | BLOCKED |
| HW-16 | Operator dan mesin | Stiker donat dengan lubang sempit hasil smart select | Smart select donat + klik lubang, export, potong | Lubang tengah terpotong rapi; lingkaran potong terkecil masih bisa dibuang | Foto hasil potong | Dok: kebutuhan aplikasi §10 butir 13 | BLOCKED — Kalibrasi 0,5 mm dan 0,3 mm di sini |
| HW-17 | Operator dan mesin | Foto/JPG kotak, preset bentuk, garis kurva hasil pegangan | Export, buka di CorelDRAW dan Cutting Master 5, potong | Sudut kotak rapi; bentuk mengikuti preset tanpa memotong artwork; titik dan kurva di CorelDRAW sama dengan editor; pisau mulus pada kurva | Foto hasil potong | Dok: kebutuhan aplikasi §10 butir 14–16 | BLOCKED — Radius kotak sudut bulat 3 mm dan rasio bintang 0,5 dikalibrasi di sini |
| HW-18 | Operator dan mesin | Desain dengan warna border | Export, cetak, potong | Warna border sesuai; tidak ada tepi putih di garis potong walau pisau sedikit bergeser | Foto hasil cetak dan potong | Dok: kebutuhan aplikasi §10 butir 17 | BLOCKED — Perpanjangan 1 mm dikalibrasi di sini |

### Kinerja

| Kode | Jenis | Input | Langkah | Hasil benar | Bukti | Sumber | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| PERF-01 | Uji tampilan | 19 desain sekaligus (01-input) | Profil memori (ram-profil.ps1) | ±120 MB (spesifikasi bagian 2); beban acuan belum ditetapkan | 04-evidence/2026-10-21-ram-profil.txt | Dok: spesifikasi leader bagian 2. Beban acuan: usul | PASS — D4 diperbaiki 2026-10-21: semua 19 desain 84 MB, JPG 24 MP 41, dua PDF besar 53 (sebelumnya 279, 226, 326). Puncak sesaat saat memuat tetap tinggi |
| PERF-02 | Uji tampilan | Semua file 01-input | Catat waktu sampai status muncul | Semua dimuat dan disusun dalam waktu kurang dari 5 detik; tampilan responsif | ui-semua-input.txt | Usul (S9 hanya per file) | PASS — 2,5–4,0 detik; file selain CDR dimuat di jalur utama tampilan |

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
