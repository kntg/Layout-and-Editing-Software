# Rencana validasi end-to-end Printech

Status: draf 2026-10-19. Disusun dari audit kode, bukan hanya dari dokumentasi. Rencana ini mencakup alur dari file customer masuk ke Printech sampai PDF dicetak di Develop dan dipotong di Graphtec.

**Versi kode yang diuji:** commit `0cc6446` (2026-10-19). Berisi commit `7a414c8` ditambah perubahan 2026-10-18 dan 2026-10-19:
- [Core.cs](../../StickerLayout/Core.cs): penjaga jarak dibulatkan ke atas, die minimal 1 piksel di luar kiss, `Sticker.PdfAsImage`, `Nest.Compact`, salinan PDF/AI saat dimuat (D2), dan cache CDR dengan versi file (D1);
- [MainWindow.xaml.cs](../../StickerLayout/MainWindow.xaml.cs) dan [Strings.cs](../../StickerLayout/Strings.cs): peringatan bila PDF ditempel sebagai gambar; Susun memuat ulang dari PDF yang sama;
- [Export.cs](../../StickerLayout/Export.cs) dan [SelfCheck.cs](../../StickerLayout/SelfCheck.cs): baris Print Ticket dan tes tambahan;
- folder `Printech-TestData/`.

Bagian 4 berisi hasil pemeriksaan sebelum perbaikan D1/D2, hasil sesudahnya, dan uji tambahan 2026-10-19 sore (kasus khusus, alur edit lewat UI, Print Ticket panjang, profil RAM).

## 1. Tingkat pengujian, status, dan severity

| Tingkat | Isi | Cara menjalankan | Dijalankan di audit ini |
|---|---|---|---|
| Otomatis | Self-check bawaan aplikasi ([SelfCheck.cs](../../StickerLayout/SelfCheck.cs)): skenario S1–S9, S11, P1–P5, dan pemeriksaan tambahan | `StickerLayout.exe --selfcheck <folder>`. Exit code 0 dan baris terakhir `OK` berarti lulus | Ya |
| Integrasi | Mesin Printech dijalankan pada dataset `Printech-TestData`, lalu dinilai dengan geometri file asli: `validasi.ps1` + `validasi.py`, ditambah `cek-original.ps1` | Lihat [Printech-TestData/README.md](../../Printech-TestData/README.md) | Ya |
| Manual UI | Pengujian lewat aplikasi di PC pengembang. Sebagian sudah otomatis dengan UI Automation (mouse, keyboard, kontrol asli): `ui-semua-input.ps1`, `ui-file-khusus.ps1`, `ui-edit-layout.ps1` di `Printech-TestData`, dijalankan dengan Windows PowerShell 5.1 | Langkah per kasus uji, atau skrip | Ya (2026-10-19) |
| Operator/hardware | CorelDRAW, Cutting Master 5, printer Develop, Graphtec | PC operator di AP 55 | Tidak (BLOCKED) |

Repo ini tidak punya project unit test (xUnit/NUnit) dan tidak punya CI. Satu-satunya gerbang otomatis di dalam aplikasi adalah `--selfcheck`. `validasi.py` butuh Python dengan PyMuPDF, Pillow, NumPy, dan SciPy.

**Status:**
- **PASS:** sudah dijalankan, hasilnya sesuai harapan, dan buktinya tersimpan.
- **FAIL:** sudah dijalankan, hasilnya tidak sesuai harapan.
- **BLOCKED:** tidak bisa dijalankan di lingkungan ini, karena perangkat, file uji, atau keputusan belum ada.
- **NOT RUN:** bisa dijalankan, tetapi belum dijalankan.

**Severity (usulan, perlu disetujui).** Di CSV, kolom Severity berisi usulan dampak bila tes itu gagal.
- **Blocker:** bisa membuang bahan atau mencetak/memotong hal yang salah tanpa disadari operator. Rilis ditahan.
- **Mayor:** hasil salah tetapi terlihat atau ada peringatan, atau ada cara kerja yang aman. Rilis boleh dengan catatan.
- **Minor:** kenyamanan atau tampilan.

## 2. Alur sistem dari import sampai produksi fisik

**Di Printech**
1. **File masuk** lewat tombol Tambah, Ctrl+I/Ctrl+O, drag & drop, atau argumen: `MainWindow.AddFiles` ([MainWindow.xaml.cs:160](../../StickerLayout/MainWindow.xaml.cs#L160)).
2. **CDR dikonversi ke PDF** oleh proses worker `--cdr2pdf` ([App.xaml.cs:19](../../StickerLayout/App.xaml.cs#L19)) lewat CorelDRAW COM: `Corel.ToPdf` dan `Corel.Worker` ([Core.cs:516](../../StickerLayout/Core.cs#L516), [Core.cs:542](../../StickerLayout/Core.cs#L542)).
   - Batas waktu 30 detik.
   - Hasil konversi disimpan di cache per path dan versi file (waktu ubah + ukuran), jadi CDR yang diedit dikonversi lagi ([Core.cs:518-519](../../StickerLayout/Core.cs#L518-L519)).
3. **Desain dimuat:** `Loader.LoadAll` dan `Loader.Load` ([Core.cs:89](../../StickerLayout/Core.cs#L89), [Core.cs:128](../../StickerLayout/Core.cs#L128)).
   - PDF/AI disalin dulu ke folder temp; desain dan export memakai salinan itu ([Core.cs:110-122](../../StickerLayout/Core.cs#L110-L122)). Susun memuat ulang dari salinan yang sama ([MainWindow.xaml.cs:247](../../StickerLayout/MainWindow.xaml.cs#L247)).
   - PDF/AI dirender dengan PDFium dan dicek apakah bisa ditempel vektor ([Core.cs:284](../../StickerLayout/Core.cs#L284)).
   - SVG dirender dengan SharpVectors ([Core.cs:311](../../StickerLayout/Core.cs#L311)).
   - PNG/JPG didekode.
   - Konten di-crop ([Core.cs:363](../../StickerLayout/Core.cs#L363)). Latar ditentukan oleh transparansi: bila ada transparansi, latar = piksel transparan; bila tidak, latar = putih ([Core.cs:154-155](../../StickerLayout/Core.cs#L154-L155), [Core.cs:360-361](../../StickerLayout/Core.cs#L360-L361)).
4. **Garis potong dibuat:**
   - mask 6 px/mm → closing (offset + halus) → blur + penjaga jarak → isi lubang → Potrace ([Core.cs:383-501](../../StickerLayout/Core.cs#L383-L501));
   - die = die ∪ (kiss + 1 piksel) ([Core.cs:186](../../StickerLayout/Core.cs#L186)).
5. **Bleed** (opsional, hanya raster): [Core.cs:231](../../StickerLayout/Core.cs#L231).
6. **Nesting:** `Nest.Run` ([Core.cs:675](../../StickerLayout/Core.cs#L675)).
   - Greedy di grid 1 mm, 4 rotasi, 3 urutan percobaan dihitung paralel.
   - `Nest.Check` memeriksa margin dan tumpang tindih ([Core.cs:778](../../StickerLayout/Core.cs#L778)).
7. **Edit layout:** seret + snap ([MainWindow.xaml.cs:587-597](../../StickerLayout/MainWindow.xaml.cs#L587-L597), [Core.cs:743](../../StickerLayout/Core.cs#L743)), putar, panah, hapus ([MainWindow.xaml.cs:281-292](../../StickerLayout/MainWindow.xaml.cs#L281-L292), [MainWindow.xaml.cs:536-539](../../StickerLayout/MainWindow.xaml.cs#L536-L539)), Compact lembar terakhir ([Core.cs:767](../../StickerLayout/Core.cs#L767)).
8. **Indikator bahan dan peringatan:** [MainWindow.xaml.cs:95-111](../../StickerLayout/MainWindow.xaml.cs#L95-L111).
9. **Export PDF:** `Export.Pdf` ([Export.cs:75](../../StickerLayout/Export.cs#L75)).
   - Artwork PDF/AI ditempel sebagai Form XObject dari salinan yang dibuat saat dimuat ([Export.cs:131](../../StickerLayout/Export.cs#L131)).
   - Raster diambil dari memori.
   - Garis potong DeviceCMYK, tebal 0,1 mm ([Export.cs:16](../../StickerLayout/Export.cs#L16), [Export.cs:119-120](../../StickerLayout/Export.cs#L119-L120)).
10. **Output pendukung:** preview JPG ([Export.cs:144](../../StickerLayout/Export.cs#L144)) dan Print Ticket ([Export.cs:21](../../StickerLayout/Export.cs#L21)).

**Di luar Printech (operator, [PRD §10](../PRD.md))**
11. Buka PDF di CorelDRAW, lalu pasang reg mark dengan cara yang biasa dipakai operator.
12. Cetak di Develop A3+. Unverified: bagaimana garis potong disembunyikan saat cetak.
13. Kirim ke Cutting Master 5, pilih garis per warna (magenta = kiss, cyan = die), lalu potong di Graphtec.

## 3. Peta implementasi dan cakupan tes

| Fitur | Implementasi | Tes yang sudah ada | Celah |
|---|---|---|---|
| Import PDF/AI | [Core.cs:89-159](../../StickerLayout/Core.cs#L89-L159), [Core.cs:284-309](../../StickerLayout/Core.cs#L284-L309) | selfcheck `logo.pdf`/`logo.ai`/`multi.pdf`; validasi E1, X1; verifikasi dataset | PDF terenkripsi gagal di export (D8), kotak putih (D7) |
| Import CDR | [Core.cs:516-555](../../StickerLayout/Core.cs#L516-L555), [App.xaml.cs:19](../../StickerLayout/App.xaml.cs#L19), [MainWindow.xaml.cs:170-176](../../StickerLayout/MainWindow.xaml.cs#L170-L176) | selfcheck P1–P2 (tanpa CorelDRAW) | Konversi asli, CorelDRAW macet, dan bukti D1 dengan CorelDRAW: butuh operator |
| Import PNG/JPG/SVG | [Core.cs:140-148](../../StickerLayout/Core.cs#L140-L148), [Core.cs:311-327](../../StickerLayout/Core.cs#L311-L327) | selfcheck `circle.png`/`rough.jpg`/`star.jpg`/`badge.svg`; validasi | File rusak |
| dpi dan peringatan | [Core.cs:46-50](../../StickerLayout/Core.cs#L46-L50) | selfcheck (SelfCheck.cs:97-100); verifikasi dataset | Pembulatan tampilan (D6) |
| Garis potong | [Core.cs:383-501](../../StickerLayout/Core.cs#L383-L501) | S1–S9, S11; validasi A1–A6 | Potong fisik (S10) |
| Bleed | [Core.cs:209-281](../../StickerLayout/Core.cs#L209-L281) | selfcheck (SelfCheck.cs:255-269); validasi B4 | Cetak/potong fisik |
| Nesting dan pemeriksaan geometri | [Core.cs:610-800](../../StickerLayout/Core.cs#L610-L800) | selfcheck (SelfCheck.cs:274-298), P3, P5; validasi B1–B7 | — |
| Edit layout (UI) | [MainWindow.xaml.cs:264-597](../../StickerLayout/MainWindow.xaml.cs#L264-L597) | Fungsi `Snap`/`Check`/`Compact` lewat selfcheck; alur UI lewat `ui-edit-layout.ps1` (EDIT-01..09, EDIT-11, CUT-12) | Uji UI tidak masuk self-check: dijalankan terpisah, butuh desktop tanpa gangguan mouse |
| Export PDF | [Export.cs:75-141](../../StickerLayout/Export.cs#L75-L141) | selfcheck S8 (SelfCheck.cs:319-350), salinan PDF (SelfCheck.cs:375-390); validasi E1, X1–X3 | CorelDRAW |
| Preview JPG dan Print Ticket | [Export.cs:21-71](../../StickerLayout/Export.cs#L21-L71), [Export.cs:144-176](../../StickerLayout/Export.cs#L144-L176) | selfcheck (SelfCheck.cs:312, 354-358, 387-391); validasi X4–X5 | Ticket dengan > 10 lembar / > 20 desain |
| Rapikan gambar (dibekukan) | [Cleanup.cs](../../StickerLayout/Cleanup.cs), [CleanupWindow.xaml.cs](../../StickerLayout/CleanupWindow.xaml.cs) | selfcheck (SelfCheck.cs:71-91), P4, P5 | Dialog UI |
| Kinerja dan RAM | — | P5 otomatis; P6 manual; `ram-profil.ps1` | Tidak ada gerbang otomatis untuk RAM; file besar melewati target (D4) |

## 4. Hasil pemeriksaan otomatis (2026-10-19)

| Perintah | Exit code | Hasil | Artefak |
|---|---|---|---|
| `dotnet build StickerLayout -c Release` | 0 | 0 warning, 0 error | `StickerLayout/bin/Release/net10.0-windows/` |
| `dotnet build StickerLayout -c Debug` | 0 | 0 warning, 0 error | `StickerLayout/bin/Debug/net10.0-windows/` |
| `dotnet publish StickerLayout -c Release -r win-x64 --self-contained -o <folder sementara>` | 0 | 146 MB | Folder sementara. `publish\Printech` sengaja tidak ditimpa. |
| `…\Release\…\StickerLayout.exe --selfcheck <folder baru>` | 0 | 15/15 skenario lulus, baris terakhir `OK` | `<folder>\selfcheck.txt` |
| `…\Debug\…\StickerLayout.exe --selfcheck samples` (perintah di PRD §11) | 0 | 15/15 skenario lulus | `samples/selfcheck.txt` |
| Uji negatif: `logo.pdf` diganti PDF berukuran lain | 1 (sesuai harapan) | "ukuran asli PDF salah: 70,60x70,60" | — |
| Uji negatif: folder tidak bisa dibuat | 1 (sesuai harapan) | — | — |
| exe hasil publish `--selfcheck <folder baru>` | 0 | 15/15 skenario lulus | — |
| `pwsh Printech-TestData/validasi.ps1` | 0 | 17 skenario dijalankan | `Printech-TestData/03-exports/validasi/` |
| `python Printech-TestData/validasi.py` | **1** | 18 dari 21 kriteria lulus. Gagal: A2b, A4, dan A5 (A5 gagal karena A4) | `Printech-TestData/04-evidence/2026-10-19-validasi-*` |
| `cek-original.ps1` | 0 | 15/15 file original tidak berubah | — |

**Setelah perbaikan D1/D2 (2026-10-19)**

| Perintah | Exit code | Hasil |
|---|---|---|
| `dotnet build StickerLayout -c Release` dan `-c Debug` | 0 | 0 warning, 0 error |
| Self-check Release dan Debug (folder baru, `samples`, 2 uji negatif) | 0 / 0 / 1 / 1 sesuai harapan | 15/15 skenario lulus. Cek baru: PDF diganti lalu dihapus setelah dimuat, muat ulang dan export tetap dari salinan (49,5 mm), tambah ulang memakai isi baru (28,3 mm) |
| `validasi.ps1` + `validasi.py` | 0 / 1 | Semua angka dan vonis sama persis dengan sebelum perbaikan (gagal tetap A2b, A4, A5) |
| Uji UI, semua file sekaligus | — | Sama dengan sebelum perbaikan: "18 stiker di 2 lembar.", 3 peringatan, "54% · sisa 46%" |
| `cek-original.ps1` | 0 | 15/15 file original tidak berubah |

**Uji tambahan 2026-10-19 (tanpa perubahan kode)**

| Uji | Exit code | Hasil |
|---|---|---|
| `ui-file-khusus.ps1` (5 file `01-input/khusus`) | 0 | 4 dialog "Gagal memuat" ditutup satu per satu; 1 desain masuk; aplikasi responsif. Pesan error masih teknis (D9). |
| PDF terenkripsi lewat mesin Printech | — | Dimuat sebagai vektor, lalu `Export.Pdf` melempar exception sehingga seluruh export gagal (D8). |
| `ui-edit-layout.ps1`, 3 kali berturut-turut | 0 | 11/11 langkah lulus di setiap jalan (EDIT-01..09, EDIT-11, CUT-12). |
| Print Ticket 13 lembar, 21 desain | — | Tetap 1 halaman A4; ada baris "… dan 3 lainnya" (lembar) dan "… dan 1 lainnya" (desain). |
| `ram-profil.ps1` | 0 | Kosong 35 MB, 4 desain 67, 16 desain 99, JPG 24 MP 216, dua PDF besar 347, semua 19 desain 351 MB (D4). |
| `cek-original.ps1` (termasuk `01-input/khusus`) | 0 | 20/20 file original tidak berubah. |

**Diulang 2026-10-21 setelah perbaikan D10:** build Release dan Debug 0 warning; selfcheck Release dan Debug 4/4 sesuai harapan (15/15 skenario, ditambah cek latar abu 242, pink pastel, dan hampir putih 252); `validasi.ps1` menghasilkan 1144 nilai garis, ukuran, dan posisi yang identik dengan 2026-10-19; laporan `validasi.py` identik (A2b, A4, A5 tetap gagal seperti sebelumnya); `cek-original.ps1` 23/23 sama (3 file baru `01-input/latar`); tiga PNG latar dimuat di UI tanpa peringatan.

## 5. Matriks format

| Aspek | PDF | AI (PDF-compatible) | CDR | PNG | JPG | SVG |
|---|---|---|---|---|---|---|
| Dimuat, ukuran benar | IMP-01, IMP-02: PASS | IMP-03: PASS | IMP-05: BLOCKED | IMP-10: PASS | IMP-11: PASS | IMP-12: PASS |
| Multi-halaman jadi desain terpisah | IMP-02: PASS | NOT RUN | NOT RUN | — | — | — |
| Di PDF export | Vektor, warna CMYK utuh (EXP-04: PASS). Halaman /Rotate jadi gambar + peringatan (IMP-16: PASS) | Vektor (EXP-04: PASS) | Vektor bila konversi berhasil (BLOCKED) | Gambar | Gambar | Gambar ±300 dpi (batasan PRD) |
| dpi dan peringatan resolusi | Tidak tampil (vektor) | Tidak tampil | Tidak tampil | IMP-13: PASS | IMP-13: PASS | ±305 dpi |
| File rusak atau gagal | IMP-17: FAIL (PDF terenkripsi gagal di export, D8) | IMP-04: PASS | IMP-06: PASS; IMP-08: BLOCKED | IMP-21: PASS | IMP-21: PASS | IMP-21: PASS |
| Latar warna muda (abu 242, pink pastel) | — | — | — | IMP-23: PASS (sebelumnya hilang, D10) | Aturan sama, belum ada file uji | — |
| Latar putih | IMP-18: BLOCKED (D7) | Sama dengan PDF | — | Ada transparansi: putih dihitung artwork | Putih dibuang (IMP-11) | — |

## 6. Kasus uji

Total 81 kasus: PASS 57, FAIL 5, BLOCKED 18, NOT RUN 1. Status per 2026-10-19; IMP-23 per 2026-10-21.

Kolom Sumber menunjukkan asal kriterianya:
- **Dok:** sudah tertulis di PRD, dokumen uji, atau spesifikasi leader.
- **Kode:** perilaku yang ada di kode tetapi belum tertulis di dokumen.
- **Kriteria B / Permintaan user:** berasal dari rencana validasi yang dikirim user, belum masuk dokumen resmi.
- **Usul:** kriteria baru yang masih perlu disetujui.

Lembar kerja yang sama, untuk diisi saat menguji: [e2e-test-results-template.csv](e2e-test-results-template.csv).

### Import

| ID | Tingkat | Input | Langkah | Hasil benar | Bukti | Sumber | Status |
|---|---|---|---|---|---|---|---|
| IMP-01 | Integrasi + Otomatis | pdf-single-page.pdf | Tambah file; baca panel Desain | Vektor; artwork 60,0 × 30,0 mm (±0,2); garis kiss 64 × 34 mm (±0,4) | verifikasi-loader.txt; selfcheck logo.pdf (SelfCheck.cs:66-68) | Dok: PRD F1, F2. Toleransi: Usul | PASS |
| IMP-02 | Integrasi + Otomatis | pdf-multi-page.pdf, pdf-irregular-shapes.pdf | Tambah file | 3 dan 4 desain bernama "… p1" sampai "… pN"; ukuran per halaman sesuai expected-results.md | verifikasi-loader.txt; selfcheck multi.pdf (SelfCheck.cs:369) | Dok: PRD F1 | PASS |
| IMP-03 | Integrasi + Otomatis | ai-compatible.ai | Tambah file | Vektor 50,0 × 25,0 mm | verifikasi-loader.txt; selfcheck logo.ai (SelfCheck.cs:69) | Dok: PRD F1 | PASS |
| IMP-04 | Manual UI (otomatis UIA) | 01-input/khusus/ai-postscript.ai | ui-file-khusus.ps1 | Dialog "Gagal memuat" dengan pesan; aplikasi tidak crash; file lain tetap masuk | 04-evidence/2026-10-19-ui-file-khusus.txt | Usul | PASS — Pesan masih teknis: "PDF tidak bisa dibuka (PDFium error 3)." (D9) |
| IMP-05 | Operator/hardware | cdr-normal.cdr | Tambah file | Sama dengan pdf-single-page.pdf: vektor 60,0 × 30,0 mm | Tangkapan layar panel Desain; waktu konversi (detik) | Dok: PRD F1; UJI-STABILITAS P7 | BLOCKED — CorelDRAW tidak ada di PC pengembangan |
| IMP-06 | Otomatis + Manual UI | invalid-file.cdr | Tambah bersama file lain | Dialog "Gagal memuat" berisi pesan CorelDRAW tidak terpasang; file lain tetap masuk; tidak crash | selfcheck P1; ui-semua-input.txt | Dok: PRD F1; UJI-STABILITAS P1 | PASS |
| IMP-07 | Otomatis | Worker CDR dengan batas waktu 1 ms | selfcheck P2 | Worker dihentikan < 2 detik; tidak ada worker tersisa | selfcheck.txt P2 | Dok: PRD F1; UJI-STABILITAS P2 | PASS — Hanya jalur kode batas waktu, bukan CorelDRAW asli (lihat IMP-08) |
| IMP-08 | Operator/hardware | CDR apa pun | Tambah CDR (UJI-STABILITAS P9) | Printech tetap responsif; setelah 30 detik muncul pesan "tidak merespons dalam 30 detik"; setelah dialog ditutup, tambah ulang berhasil | Video atau tangkapan layar + catatan waktu | Dok: UJI-STABILITAS P9 | BLOCKED |
| IMP-09 | Operator/hardware | CDR yang diedit lalu ditambah ulang | Tambah CDR; ubah dan simpan CDR di CorelDRAW; tambah lagi | Desain kedua memakai isi terbaru | Tangkapan layar kedua desain | Usul | BLOCKED — Kode diperbaiki 2026-10-19 (D1): cache konversi memakai versi file (Core.cs:518-519); belum diuji dengan CorelDRAW |
| IMP-10 | Integrasi | png-transparent.png | Tambah file | Crop ke konten 50,0 × 25,0 mm; 406 dpi; lubang transparan tidak dipotong | verifikasi-loader.txt; validasi A5 | Dok: PRD F2 | PASS |
| IMP-11 | Integrasi | jpg-low-resolution.jpg | Tambah file | 50,0 × 50,0 mm (±0,5); latar putih dibuang | verifikasi-loader.txt | Dok: PRD F2 | PASS |
| IMP-12 | Integrasi + Otomatis | svg-vector.svg | Tambah file | 50,0 × 25,0 mm; ±305 dpi; ditempel sebagai gambar (batasan PRD) | verifikasi-loader.txt; selfcheck badge.svg | Dok: PRD F1 + batasan SVG (PRD §8) | PASS |
| IMP-13 | Integrasi + Otomatis + Manual UI | jpg-low-resolution.jpg, png-147dpi.png, png-152dpi.png | Tambah file | ±102 dan 147 dpi merah, 152 dpi tidak; peringatan "2 desain resolusi rendah" | verifikasi-loader.txt; ui-semua-input.txt; SelfCheck.cs:97-100 | Dok: PRD F12 (ambang 150 dpi masih tebakan, PRD §8) | PASS — Temuan D6: 149,5–149,99 dpi tampil "150 dpi" berwarna merah (Core.cs:49-50) |
| IMP-14 | Integrasi + Otomatis | pdf-large-a3.pdf, jpg-large-24mp.jpg | Tambah file | Dimuat < 5 detik tanpa crash; A3 280 × 420 mm (±0,3) | verifikasi-loader.txt; selfcheck S9 | Dok: UJI-GARIS-POTONG S9. ±0,3: Usul | PASS — Halaman > 400 mm dirender maksimal 4000 px (Core.cs:284-291), ukuran tampil +0,2 mm |
| IMP-15 | Integrasi + Manual UI | pdf-oversize.pdf | Tambah file | Tidak dipasang; peringatan "1 lebih besar dari kertas"; aplikasi tetap jalan | ui-semua-input.txt; validasi B1 | Usul (perilaku kode, belum di PRD) | PASS |
| IMP-16 | Integrasi + Manual UI | pdf-rotated.pdf | Tambah file | Ditempel sebagai gambar ±254 dpi, 30 × 60 mm; peringatan "1 PDF ditempel sebagai gambar…" | validasi X3; ui-semua-input.txt | Dok: PRD §8 (batasan + peringatan) | PASS — Peringatan ditambahkan 2026-10-18, belum di-commit |
| IMP-17 | Integrasi + Manual UI | 01-input/khusus/pdf-encrypted.pdf | Tambah lewat UI (ui-file-khusus.ps1), lalu Export.Pdf | Dimuat (vektor bila PDFsharp bisa membaca; bila tidak, gambar + peringatan); export tetap jadi tanpa exception | 04-evidence/2026-10-19-pdf-terenkripsi.txt | Dok: PRD §8 | FAIL — D8: dimuat sebagai vektor tanpa peringatan, lalu Export.Pdf melempar exception sehingga seluruh export gagal (Core.cs:297-306, Export.cs:95-99) |
| IMP-18 | Integrasi (probe) | PDF berisi kotak putih yang tidak memenuhi halaman + logo | Tambah file | PERLU KEPUTUSAN: garis mengikuti logo atau kotak putih. Sekarang: kotak 84 × 84 mm. Bila latar putih memenuhi halaman, garis mengikuti logo (44 mm). | probe 2026-10-19 (scratchpad) | Dok: PRD §10 butir 9 (pertanyaan terbuka) | BLOCKED — Temuan D7: hasil bergantung pada transparansi halaman (Core.cs:154-155, 360-361) |
| IMP-19 | Manual UI | Semua file 01-input sekaligus | Jalankan exe dengan semua file sebagai argumen | 19 desain; "18 stiker di 2 lembar."; 3 peringatan; "54% · sisa 46%"; judul lembar 91% dan 16% | ui-semua-input.txt; tangkapan layar | Usul (angka dari validasi B-ui) | PASS — Dijalankan 2026-10-18 dengan skrip UIA di luar repo |
| IMP-20 | Integrasi | 01-input dan 01-input/customer | powershell -ExecutionPolicy Bypass -File cek-original.ps1 | Semua baris "sama"; exit code 0 | Output cek-original | Permintaan user: file original tidak berubah | PASS — 15/15 |
| IMP-21 | Manual UI (otomatis UIA) | 01-input/khusus/invalid-file.png, .jpg, .svg | ui-file-khusus.ps1 | Dialog "Gagal memuat"; aplikasi tidak crash; file lain tetap masuk | 04-evidence/2026-10-19-ui-file-khusus.txt | Usul | PASS — Pesan teknis berbahasa Inggris, misalnya "No imaging component suitable to complete this operation was found." (D9) |
| IMP-22 | Manual UI | File customer di 01-input/customer | Tambah file; bandingkan dengan hasil-benar.md | Sesuai hasil-benar.md | 04-evidence/customer/ | Usul | NOT RUN — Belum ada file customer |
| IMP-23 | Integrasi + Manual UI | 01-input/latar/png-latar-abu-muda.png, png-latar-pink-pastel.png, png-latar-putih.png | Loader + Export.Pdf, warna latar diukur di render MuPDF; UI: exe dengan ketiga file sebagai argumen | Latar abu muda dan pink pastel utuh di layar dan PDF, garis kiss mengelilingi kotak gambar; latar putih dibuang | 04-evidence/2026-10-21-png-latar.txt, -sebelum.png, -sesudah.png, -ui.png | Temuan tim 2026-10-21 (D10) | PASS — Sebelum perbaikan FAIL: latar tinggal 5 % dan logo ±4× lebih besar. Diperbaiki 2026-10-21 (Loader.WhiteBorder) |

### Garis potong

| ID | Tingkat | Input | Langkah | Hasil benar | Bukti | Sumber | Status |
|---|---|---|---|---|---|---|---|
| CUT-01 | Integrasi | Lingkaran, persegi, AI, SVG, PNG, JPG | validasi.py A1 | Median jarak garis ke artwork = offset ±0,25 mm; jarak terjauh ≤ offset + 0,5 mm | Laporan validasi A1 | Dok: PRD F3. Toleransi: Usul | PASS — Median − offset −0,19 s.d. +0,02 |
| CUT-02 | Integrasi + Otomatis | png-rough-edge.png, bintang, rough.jpg | validasi.py A2a; selfcheck S5 | Panjang garis selalu turun seiring Halus naik; node pada Halus 1 dan 2 < Halus 0 | Laporan A2a; selfcheck S5; validasi-halus.png | Dok: PRD F16; UJI-GARIS-POTONG S5 | PASS |
| CUT-03 | Integrasi + Otomatis | rough.jpg (S1), png-rough-edge.png (A2b) | selfcheck S1; validasi.py A2b | Node turun ≥ 60% dari Halus 0 ke Halus 1 | selfcheck S1; laporan A2b | Dok: PRD §8 (target 60–80%); S1 | FAIL — S1 lulus (74%); A2b 54%. Perlu keputusan target (D5) |
| CUT-04 | Integrasi + Otomatis | Semua desain uji | validasi.py A3; selfcheck S6; SelfCheck.cs:55 | 0 titik garis kiss di luar garis die | Laporan A3; selfcheck S6 | Dok: PRD §6 (die membungkus kiss) | PASS |
| CUT-05 | Integrasi | Semua desain uji | validasi.py A4 | Jarak terdekat garis ke artwork ≥ offset − 0,3 mm | Laporan A4 | Dok: PRD §6 (penjaga jarak) | FAIL — 63/158 garis sedikit di bawah batas, terburuk 0,13 mm; garis tetap di luar artwork (D3) |
| CUT-06 | Otomatis | Bintang, offset 0,5 dan 0 mm | selfcheck S4 | Luas artwork di luar garis < 0,5 mm² (offset 0,5) dan < 3 mm² (offset 0) | selfcheck S4 | Dok: UJI-GARIS-POTONG S4 | PASS |
| CUT-07 | Integrasi + Otomatis | png-transparent.png, cincin C, titik melingkar | selfcheck S11; validasi.py A5 | Lubang tertutup tidak dipotong; 1 kontur per desain | selfcheck S11; laporan A5 | Dok: PRD §8 (hanya kontur luar) | PASS |
| CUT-08 | Integrasi | Bintang, segitiga, logo kasar, garis tipis 0,2 mm | verify-testdata + validasi.py | Ukuran garis dalam toleransi sudut lancip; garis tipis tetap dikelilingi (lebar kiss ≥ 48,5 mm) | verifikasi-loader.txt; validasi-garis-potong.png | Usul (toleransi) | PASS |
| CUT-09 | Otomatis + Integrasi | Celah 3–3,9 mm (S7); tiga lingkaran celah 4 dan 15 mm | selfcheck S7; validasi.py A5 | Celah < 2 × (offset + halus) menyatu; celah 15 mm punya garis sendiri | selfcheck S7; laporan A5 | Dok: S7. Aturan celah: Usul | PASS |
| CUT-10 | Integrasi + Otomatis | Semua | validasi.py A5; selfcheck S2, S8 | 0 sudut tajam atau garis lurus; lingkaran ≤ 4 node; garis bentuk sederhana di pita [offset − 0,3; offset + 0,5] | Laporan A5; selfcheck S2/S8 | Dok: PRD F16 | FAIL — Yang gagal hanya batas bawah pita, yaitu CUT-05 |
| CUT-11 | Integrasi | pdf-multi-page.pdf, png-rough-edge.png | validasi.py A6 | Magenta saja / cyan saja / keduanya / pesan "Aktifkan minimal satu garis potong." | Laporan A6 | Dok: PRD F3, F9 | PASS |
| CUT-12 | Manual UI (otomatis UIA) | Desain apa pun | ui-edit-layout.ps1 | Dialog NeedCut / DieInside / BleedGap; layout tidak dibuat | 04-evidence/2026-10-19-ui-edit-layout.txt | Dok: PRD F3, F17 (MainWindow.xaml.cs:129-133) | PASS — 3 dari 3 jalan lulus |
| CUT-13 | Otomatis + Integrasi | PNG/JPG dengan bleed 1–2 mm | selfcheck bleed; validasi.py B4 | Warna meluas sampai bleed di luar kiss; vektor tanpa bleed; area bleed di dalam margin | selfcheck (SelfCheck.cs:255-269); laporan B4 | Dok: PRD F17 | PASS |

### Nesting

| ID | Tingkat | Input | Langkah | Hasil benar | Bukti | Sumber | Status |
|---|---|---|---|---|---|---|---|
| NEST-01 | Integrasi + Otomatis | Skenario B-sedikit sampai B-ui | validasi.py B1; SelfCheck.cs:276 | Terpasang + tidak muat = diminta; per desain = jumlah salinan | Laporan B1 | Kriteria B (rencana validasi user) | PASS |
| NEST-02 | Integrasi + Otomatis | Sama | validasi.py B2; cek tumpang tindih SelfCheck.cs:280-287 | 0 pasangan garis terluar yang bersinggungan atau saling memuat | Laporan B2 | Dok: PRD F7 | PASS — 0 dari 997 pasangan |
| NEST-03 | Integrasi | Sama | validasi.py B3 | Jarak terkecil antar garis terluar stiker berbeda ≥ 2,0 mm | Laporan B3 | Dok: spesifikasi jarak 2 mm | PASS — Terkecil 2,50 mm |
| NEST-04 | Integrasi | Sama, termasuk bleed 1 mm | validasi.py B4 | Garis terluar ≥ 10 mm dari tepi; garis + bleed ≥ 10 mm | Laporan B4 | Dok: PRD F5 | PASS — Terkecil 10,44 mm |
| NEST-05 | Integrasi | B-rotasi + susun otomatis | validasi.py B5 | Jarak garis–artwork di PDF sama di 4 rotasi (selisih ≤ 0,02 mm) | Laporan B5 | Kriteria B (rencana validasi user) | PASS |
| NEST-06 | Integrasi + Otomatis | B-campuran, B-banyak-lembar | validasi.py B6; SelfCheck.cs:275 | Lembar ≥ batas bawah luas; tidak ada lembar kosong; stiker lembar terakhir tidak muat di lembar sebelumnya | Laporan B6 | Dok: PRD F6 | PASS |
| NEST-07 | Integrasi + Manual UI | B-ui | validasi.py B7; uji UI | Pemakaian bahan Printech = hitung ulang dari garis (±0,5 poin); angka layar = data | Laporan B7; ui-semua-input.txt | Dok: PRD F18. ±0,5: Usul | PASS |
| NEST-08 | Otomatis | 130 stiker | selfcheck P3 | 5 kali susun = 1 hasil | selfcheck P3 | Dok: UJI-STABILITAS P3 | PASS |
| NEST-09 | Otomatis | 130 stiker | selfcheck P5 | Susun < 1000 ms | selfcheck P5 | Dok: UJI-STABILITAS P5 | PASS |
| NEST-10 | Otomatis + Integrasi | Lembar terakhir | Compact; validasi.py X1 | Tinggi turun; stiker tetap di dalam margin; tinggi halaman PDF = tinggi lembar | selfcheck (SelfCheck.cs:392+); laporan X1 | Dok: tombol Compact (MainWindow.xaml.cs:264-271) | PASS |

### Edit layout

| ID | Tingkat | Input | Langkah | Hasil benar | Bukti | Sumber | Status |
|---|---|---|---|---|---|---|---|
| EDIT-01 | Manual UI (otomatis UIA) | Layout apa pun | ui-edit-layout.ps1 (langkah EDIT-01) | Pindah ke posisi kosong terdekat; tidak merah | 04-evidence/2026-10-19-ui-edit-layout.txt | Dok: PRD F7, F19 | PASS — Fungsi Nest.Snap lulus di selfcheck (SelfCheck.cs:288-298) |
| EDIT-02 | Manual UI (otomatis UIA) | Layout apa pun | ui-edit-layout.ps1 (langkah EDIT-02) | Putar 90° + snap; garis dan artwork ikut berputar | 04-evidence/2026-10-19-ui-edit-layout.txt | Dok: PRD F7, F19 | PASS — Snap putar lulus di selfcheck |
| EDIT-03 | Manual UI (otomatis UIA) | Layout apa pun | ui-edit-layout.ps1 (langkah EDIT-03) | Bergerak tanpa snap; menabrak atau keluar margin = merah + peringatan | 04-evidence/2026-10-19-ui-edit-layout.txt | Dok: PRD F7 (MainWindow.xaml.cs:536-539) | PASS |
| EDIT-04 | Manual UI (otomatis UIA) | Layout apa pun | ui-edit-layout.ps1 (langkah EDIT-04) | Stiker hilang; status dan bahan diperbarui; lembar kosong dihapus | 04-evidence/2026-10-19-ui-edit-layout.txt | Dok: PRD F7 (MainWindow.xaml.cs:281-289) | PASS |
| EDIT-05 | Manual UI (otomatis UIA) | Desain gambar | ui-edit-layout.ps1 (langkah EDIT-05) | Desain dimuat ulang dengan lebar baru; jumlah stiker = total salinan | 04-evidence/2026-10-19-ui-edit-layout.txt | Dok: PRD F2 (MainWindow.xaml.cs:241-251) | PASS |
| EDIT-06 | Manual UI (otomatis UIA) | Layout dengan edit manual | ui-edit-layout.ps1 (langkah EDIT-06) | Layout dibuat ulang; edit manual hilang (belum ada undo) | 04-evidence/2026-10-19-ui-edit-layout.txt | Dok: PRD §8 (belum ada undo) | PASS — Usul: konfirmasi sebelum menimpa edit manual |
| EDIT-07 | Manual UI (otomatis UIA) | Layout dengan stiker merah | ui-edit-layout.ps1 (langkah EDIT-07) | Dialog konfirmasi muncul; pilih Tidak = export batal | 04-evidence/2026-10-19-ui-edit-layout.txt | Kode (MainWindow.xaml.cs:329-330) | PASS |
| EDIT-08 | Manual UI (otomatis UIA) | Layout apa pun | ui-edit-layout.ps1 (langkah EDIT-08) | Layout dikosongkan lalu langsung disusun ulang di kertas baru; judul lembar menyebut A4 | 04-evidence/2026-10-19-ui-edit-layout.txt | Dok: PRD F4 | PASS — Harapan dikoreksi sesuai kode (MainWindow.xaml.cs:359-364); 3 dari 3 jalan lulus |
| EDIT-09 | Manual UI (otomatis UIA) | Desain apa pun | ui-edit-layout.ps1 (langkah EDIT-09) | Offset, halus, margin, dan jarak terisi sesuai RESEARCH.md §13, lalu layout disusun ulang | 04-evidence/2026-10-19-ui-edit-layout.txt | Dok: PRD F19 | PASS — Validitas nilai preset lulus di selfcheck (SelfCheck.cs:307) |
| EDIT-10 | Otomatis | Foto berlatar polos | Rapikan: hapus latar, penghapus klik, kikis tepi, urungkan | Latar hilang; urungkan mengembalikan; export memakai hasil Rapikan | selfcheck (SelfCheck.cs:71-91), P4, P5 | Dok: PRD F15 (fitur dibekukan) | PASS — Dialog UI tidak diuji ulang pada audit ini |
| EDIT-11 | Manual UI (otomatis UIA) | - | ui-edit-layout.ps1 (langkah EDIT-11) | Semua teks UI dan Print Ticket berbahasa Inggris | 04-evidence/2026-10-19-ui-edit-layout.txt | Dok: PRD F10 | PASS |

### Export

| ID | Tingkat | Input | Langkah | Hasil benar | Bukti | Sumber | Status |
|---|---|---|---|---|---|---|---|
| EXP-01 | Integrasi + Otomatis | Semua skenario | validasi.py X1, E1; SelfCheck.cs:319-332 | Halaman = lembar; ukuran halaman ±0,01 mm; posisi artwork ±0,05 mm; ukuran desain = file asli ±0,05 mm | Laporan X1/E1 | Dok: spesifikasi export. Toleransi: Usul | PASS |
| EXP-02 | Integrasi + Otomatis | Semua | validasi.py X2 (preflight); selfcheck S8 | Stroke hanya `0 1 0 0 K` (kiss) dan `1 0 0 0 K` (die); 0 RGB/ICC/Separation; 0 OutputIntent | Laporan X2; selfcheck S8 | Dok: PRD F9 | PASS |
| EXP-03 | Integrasi | Semua | validasi.py X2 (separasi MuPDF CMYK, tanpa antialias) | Kiss tepat C0 M100 Y0 K0; die tepat C100 M0 Y0 K0 | Laporan X2; validasi-separasi-cmyk.png | Dok: spesifikasi tahap 4 | PASS — Separasi di CorelDRAW: HW-02 |
| EXP-04 | Integrasi + Otomatis | PDF/AI | validasi.py X3; SelfCheck.cs:338 | PDF/AI = Form XObject; warna isi CMYK sama dengan file input; gambar hanya PNG/JPG/SVG dan PDF diputar | Laporan X3 | Dok: PRD F9 | PASS |
| EXP-05 | Otomatis | logo.pdf, lalu isi file diganti multi.pdf dan file dihapus | selfcheck: muat PDF; ganti isi file; muat ulang seperti Susun; tambah ulang; hapus file; export (SelfCheck.cs:375-390) | Muat ulang dan export memakai isi saat dimuat (49,5 mm, tanpa peringatan); tambah ulang memakai isi baru (28,3 mm) | selfcheck.txt baris "PDF diganti lalu dihapus setelah dimuat" | Usul | PASS — Diperbaiki 2026-10-19 (D2): PDF/AI disalin ke folder temp saat dimuat (Core.cs:110-122) |
| EXP-06 | Integrasi + Otomatis | Skenario B | validasi.py X4; SelfCheck.cs:354-358 | 1 JPEG per lembar; sisi panjang 2000 px; isi di posisi stiker sama dengan PDF (≥ 95%) | Laporan X4 | Dok: PRD F13 | PASS |
| EXP-07 | Integrasi + Otomatis | Skenario B | validasi.py X5; SelfCheck.cs:312, 404-408 | 1 halaman A4; total lembar, total pcs, efisiensi, per lembar, per desain, pengaturan = layout | Laporan X5 | Dok: PRD F20 | PASS |
| EXP-08 | Integrasi (probe) | 22 desain dari 01-input, A3 × 12 (13 lembar, 21 desain terpasang) | Export.Ticket | Tetap 1 halaman; ada baris "… dan N lainnya" | 04-evidence/2026-10-19-print-ticket-panjang.txt; 03-exports/2026-10-19-print-ticket-13-lembar.pdf | Kode (Export.cs:57-66) | PASS — Baris "… dan 3 lainnya" (lembar) dan "… dan 1 lainnya" (desain); blok terbawah 270 mm |

### Operator/hardware

| ID | Tingkat | Input | Langkah | Hasil benar | Bukti | Sumber | Status |
|---|---|---|---|---|---|---|---|
| HW-01 | Operator/hardware | PDF export (B-campuran.pdf + export dari UI) | Import atau buka PDF | Halaman 320 × 480 mm; artwork PDF tetap kurva (bukan bitmap); warna sama dengan file asli | Tangkapan layar Object Properties | Dok: PRD §10 butir 6 | BLOCKED |
| HW-02 | Operator/hardware | Sama | Klik garis kiss dan die; buka pratinjau separasi | Kiss C0 M100 Y0 K0; die C100 M0 Y0 K0; kiss hanya di pelat M, die hanya di pelat C | Tangkapan layar | Dok: PRD §10 butir 10; spesifikasi tahap 4 | BLOCKED |
| HW-03 | Operator/hardware | circle.png, rough.jpg (40 mm) | Hitung node | circle: kiss 3, die 4; rough.jpg: kiss 17, die 16 (±1) | Tangkapan layar | Dok: UJI-GARIS-POTONG S8 (bagian manual) | BLOCKED — Angka rough.jpg berubah sejak 2026-10-18 |
| HW-04 | Operator/hardware | Sama | Pilih garis per warna | Magenta → kiss, cyan → die; tidak ada garis lain yang ikut | Tangkapan layar dialog CM5 | Dok: PRD §10 butir 7 | BLOCKED |
| HW-05 | Operator/hardware | Lembar A3+ | Cetak di Develop | Artwork tajam; warna sesuai file asli; reg mark terbaca Graphtec | Foto hasil cetak | Dok: PRD §10 butir 6 dan 8 | BLOCKED — Unverified: cara operator menyembunyikan garis potong saat cetak |
| HW-06 | Operator/hardware | 3–5 desain (bulat, teks, logo kasar, berlubang, berdekatan), 4 salinan | Cetak lalu potong kiss (UJI-GARIS-POTONG S10) | Pisau mulus; garis tidak mengenai artwork; border rata; selisih potong ≤ toleransi (usul ±0,5 mm, belum disetujui) | Foto + ukuran selisih (mm) per desain | Dok: S10; PRD §10 butir 8. Toleransi: Usul | BLOCKED |
| HW-07 | Operator/hardware | Sama, die-cut | Potong die | Tembus; stiker lepas utuh; die tidak memotong stiker kiss | Foto | Dok: S10 | BLOCKED |
| HW-08 | Operator/hardware | PNG dengan bleed 2 mm, offset kiss 0,5 mm | Cetak lalu potong | Tepi stiker tetap berwarna, tanpa garis putih | Foto | Dok: PRD §10 butir 10 | BLOCKED |
| HW-09 | Operator/hardware | CDR asli | UJI-STABILITAS P7 | Desain masuk; waktu dicatat untuk kalibrasi batas 30 detik | Catatan waktu | Dok: UJI-STABILITAS P7 | BLOCKED |
| HW-10 | Operator/hardware | CDR | UJI-STABILITAS P8 | Dokumen operator utuh; CorelDRAW tidak tertutup | Tangkapan layar | Dok: UJI-STABILITAS P8 | BLOCKED |
| HW-11 | Operator/hardware | 3 file CDR | UJI-STABILITAS P10 | Ketiganya masuk berurutan; UI responsif; Susun tidak membuka CorelDRAW lagi | Video atau catatan | Dok: UJI-STABILITAS P10 | BLOCKED |
| HW-12 | Operator/hardware | Zip operator | Unblock zip, jalankan, lalu --selfcheck | Aplikasi terbuka; selfcheck exit 0 di PC operator | Output selfcheck | Dok: PRD §11 | BLOCKED — Zip dibangun ulang dari commit 0cc6446 (2026-10-19); selfcheck exe paket di PC pengembang: exit 0, 15/15 |
| HW-13 | Operator/hardware | Preview JPG | Kirim ke customer | Cukup jelas untuk konfirmasi | Tangkapan layar WhatsApp | Dok: PRD §10 butir 3 | BLOCKED |
| HW-14 | Operator/hardware | Print Ticket | Lampirkan ke SPK | Isinya cukup untuk kasir | Catatan operator | Dok: PRD §10 butir 11 | BLOCKED |

### Kinerja

| ID | Tingkat | Input | Langkah | Hasil benar | Bukti | Sumber | Status |
|---|---|---|---|---|---|---|---|
| PERF-01 | Manual UI | 19 desain sekaligus (01-input) | ram-profil.ps1 | ±120 MB (spesifikasi bagian 2); beban acuan belum ditetapkan | 04-evidence/2026-10-19-ram-profil.txt | Dok: spesifikasi leader bagian 2. Beban acuan: Usul | FAIL — Kosong 35, 4 desain 67, 16 desain 99, JPG 24 MP 216, dua PDF besar 347, semua 19 desain 351 MB. Setelah GC paksa tetap ±300 MB (memori native, D4) |
| PERF-02 | Manual UI | Semua file 01-input | Catat waktu sampai status muncul | Semua dimuat dan disusun < 5 detik; UI responsif | ui-semua-input.txt | Usul (S9 hanya per file) | PASS — 2,5–4,0 detik; file non-CDR dimuat di UI thread (MainWindow.xaml.cs:176) |

## 7. Bukti yang harus dikumpulkan
- **Otomatis:** `selfcheck.txt` dan exit code. Simpan salinannya di `Printech-TestData/04-evidence/`.
- **Integrasi:** laporan `04-evidence/<tanggal>-validasi-garis-potong-nesting.txt`, gambar bukti `04-evidence/<tanggal>-validasi-*.png`, dan export di `03-exports/validasi/`.
- **Manual UI:** tangkapan layar bernama `TANGGAL-ID-keterangan.png` di `Printech-TestData/04-evidence/`.
- **Operator/hardware:** catat bahan, versi CorelDRAW dan Cutting Master 5, serta model dan firmware Graphtec.
  - Foto hasil cetak dan potong dengan penggaris atau lup.
  - Ukuran selisih potong (mm) per desain.
- **Isi CSV:** kolom Actual Result, Status, Evidence (path bukti), dan Environment (PC, Windows, build/commit). Kolom Evidence sudah berisi jenis bukti yang diminta; ganti dengan path bukti yang sebenarnya.

## 8. Persyaratan yang sudah terdokumentasi dan kriteria baru

**Sudah terdokumentasi**
- [PRD](../PRD.md) F1–F20: format input, crop, garis kiss/die, kertas, margin, susun banyak lembar, edit, export DeviceCMYK, bahasa, dpi 150, preview 2000 px, Rapikan, garis dengan node sedikit, bleed ≤ jarak, indikator bahan dan target, snap/preset, Print Ticket.
- PRD §6 (keputusan): penjaga jarak offset − 0,3 mm, die membungkus kiss, grid 1 mm, worker CDR 30 detik.
- PRD §8 (batasan): hanya kontur luar dipotong; SVG dan PDF yang diputar jadi gambar dengan peringatan; belum ada undo.
- [UJI-GARIS-POTONG.md](../UJI-GARIS-POTONG.md) S1–S11, [UJI-STABILITAS.md](../UJI-STABILITAS.md) P1–P10, dan checklist operator PRD §10 butir 1–12.
- Spesifikasi leader:
  - bagian 2: RAM ±120 MB, UI tetap responsif;
  - bagian 3: DeviceCMYK, bleed;
  - bagian 4: indikator bahan, snap, preset, Print Ticket;
  - tahap export (Printech-Deskripsi.pdf).

**Kriteria baru (usulan, perlu disetujui)**
1. Checklist rilis E2E 5 butir (bagian 9).
2. Angka toleransi validasi:
   - A1: median ±0,25 mm, jarak terjauh offset + 0,5 mm;
   - E1/X1: 0,05 mm;
   - B7: 0,5 poin persen;
   - X4: kecocokan 95%;
   - X5: ±1 poin;
   - ukuran artwork ±0,2/±0,3/±0,5 mm;
   - garis potong ±0,4 mm.
3. Toleransi selisih potong fisik di Graphtec. Usulan ±0,5 mm. RESEARCH.md menyebut akurasi mesin ±0,3 mm, tetapi itu Unverified.
4. Beban acuan untuk target RAM ±120 MB, misalnya 20 desain termasuk satu foto 24 MP.
5. Perilaku yang belum ditetapkan:
   - PDF dengan kotak putih (IMP-18);
   - CDR diedit lalu ditambah ulang (IMP-09);
   - PDF sumber berubah sebelum export (EXP-05);
   - AI tanpa PDF-compatible (IMP-04);
   - konfirmasi sebelum Susun menimpa edit manual (EDIT-06).
6. Target reduksi node 60%: berlaku untuk jenis logo apa (CUT-03).
7. Definisi severity (bagian 1), dan daftar pemeriksaan otomatis yang wajib untuk rilis.

## 9. Checklist rilis E2E (usulan) dan status 2026-10-19

| Butir | Kasus uji | Status |
|---|---|---|
| 1. Build dan pengujian otomatis | Bagian 4 | Belum. `validasi.py` exit 1 (A2b, A4, A5). Build, self-check, dan uji regresi lulus setelah perbaikan D1/D2. |
| 2. Input dan geometri | IMP-\*, CUT-\*, NEST-\* | Belum. CUT-03, CUT-05, CUT-10, IMP-17 FAIL. IMP-05, IMP-08, IMP-09, IMP-18 BLOCKED. IMP-22 (file customer) NOT RUN. |
| 3. Export dan kompatibilitas | EXP-\*, HW-01 sampai HW-04 | Belum. Semua EXP lulus, tetapi PDF terenkripsi menggagalkan export (D8). HW-01 sampai HW-04 BLOCKED. |
| 4. Produksi fisik | HW-05 sampai HW-08 | Belum. BLOCKED. |
| 5. Dokumentasi dan defect | Bagian 7 dan 10 | Belum. Bukti belum di-commit; defect D1–D7 masih terbuka. |

## 10. Temuan dan defect terbuka

| ID | Temuan | Bukti | Lokasi | Usul severity |
|---|---|---|---|---|
| D1 | **Diperbaiki 2026-10-19:** cache sekarang memakai versi file ([Core.cs:518-519](../../StickerLayout/Core.cs#L518-L519)); bukti dengan CorelDRAW menunggu IMP-09. Sebelumnya: cache konversi CDR hanya memakai path file sebagai kunci. Bila CDR diedit lalu ditambah ulang di sesi yang sama, hasil konversi lama yang dipakai. | Kode | [Core.cs:519](../../StickerLayout/Core.cs#L519) | Blocker |
| D2 | **Diperbaiki 2026-10-19:** PDF/AI disalin saat dimuat ([Core.cs:110-122](../../StickerLayout/Core.cs#L110-L122)); dicek selfcheck (SelfCheck.cs:375-390). Sebelumnya: file PDF dibaca ulang saat export, sedangkan garis potong dihitung saat dimuat. Bila file diubah di antaranya, artwork dan garis bisa tidak cocok. Bila file dipindah, export memakai render layar RGB beresolusi rendah dan hanya memberi peringatan. | Kode | [Export.cs:131](../../StickerLayout/Export.cs#L131), [Export.cs:107-111](../../StickerLayout/Export.cs#L107-L111) | Mayor |
| D3 | Garis bisa sampai 0,13 mm lebih dekat dari batas offset − 0,3 mm, di sudut lancip dan pada offset kecil. Garis tetap di luar artwork. | validasi A4 | [Core.cs:407-426](../../StickerLayout/Core.cs#L407-L426) (penjaga jarak), [Core.cs:480-501](../../StickerLayout/Core.cs#L480-L501) (Potrace) | Mayor |
| D4 | RAM melewati target ±120 MB bila memuat file besar: JPG 24 MP 216 MB, dua PDF besar 347 MB, semua 19 desain 351 MB (4 desain kecil 67 MB). Setelah GC paksa tetap ±300 MB: memori tertahan di native (bitmap render/decode resolusi penuh), bukan sampah .NET. Muat ulang tidak menambah memori (bukan kebocoran per muat). | `ram-profil.ps1`, probe | [Core.cs:284-309](../../StickerLayout/Core.cs#L284-L309) (render PDF), [Core.cs:140-148](../../StickerLayout/Core.cs#L140-L148) (decode gambar) | Mayor |
| D5 | Node pada logo bertepi bergelombang hanya turun 54% (target 60%). | validasi A2b | — | Minor |
| D6 | Nilai 149,5–149,99 dpi tampil sebagai "150 dpi" tetapi berwarna merah. | Kode | [Core.cs:49-50](../../StickerLayout/Core.cs#L49-L50) | Minor |
| D7 | PDF dengan kotak putih yang tidak memenuhi halaman menghasilkan garis potong berbentuk kotak (84 × 84 mm), bukan mengikuti logo. Bila latar putih memenuhi halaman, garis mengikuti logo (44 × 44 mm). | Probe 2026-10-19 | [Core.cs:154-155](../../StickerLayout/Core.cs#L154-L155), [Core.cs:360-361](../../StickerLayout/Core.cs#L360-L361) | Mayor (perilaku perlu diputuskan) |
| D8 | PDF terenkripsi (hanya kata sandi pemilik) lolos cek vektor saat dimuat. Lalu `Export.Pdf` melempar exception "The archive entry was compressed using an unsupported compression method." sehingga **seluruh export layout gagal**. PRD: PDF yang tidak terbaca PDFsharp seharusnya ditempel sebagai gambar dengan peringatan. | Probe 2026-10-19 | [Core.cs:297-306](../../StickerLayout/Core.cs#L297-L306), [Export.cs:95-99](../../StickerLayout/Export.cs#L95-L99) | Mayor |
| D9 | Pesan error untuk file rusak masih teknis, sebagian berbahasa Inggris: "PDF tidak bisa dibuka (PDFium error 3)." untuk AI tanpa PDF-compatible; "No imaging component suitable…" untuk PNG/JPG; "Data at the root level is invalid…" untuk SVG. | `ui-file-khusus.ps1` | [Pdfium.cs:43](../../StickerLayout/Pdfium.cs#L43), [MainWindow.xaml.cs:178](../../StickerLayout/MainWindow.xaml.cs#L178) | Minor |
| D10 | **Diperbaiki 2026-10-21:** latar PNG/JPG opak hanya dibuang bila minimal separuh tepi gambar putih (R, G, B ≥ 250) ([Core.cs:158](../../StickerLayout/Core.cs#L158), [Core.cs:367-374](../../StickerLayout/Core.cs#L367-L374)); dicek selfcheck. Sebelumnya (laporan tim): latar warna muda dengan R, G, B ≥ 240, misalnya abu 242 atau pink pastel 255,240,245, dianggap putih. Latar hilang dari layar dan PDF, tinggal cincin selebar offset kiss, dan lebar stiker dipakai untuk logo saja (logo ±4× lebih besar). | [png-latar](../../Printech-TestData/04-evidence/2026-10-21-png-latar.txt) | [Core.cs:363-374](../../StickerLayout/Core.cs#L363-L374) | Mayor |

**Batasan yang sudah diketahui** (tercatat di PRD, bukan defect baru):
- file selain CDR dimuat di UI thread ([MainWindow.xaml.cs:176](../../StickerLayout/MainWindow.xaml.cs#L176));
- Susun otomatis menimpa edit manual dan belum ada undo ([MainWindow.xaml.cs:232-262](../../StickerLayout/MainWindow.xaml.cs#L232-L262));
- geser dengan panah tidak di-snap ([MainWindow.xaml.cs:536-539](../../StickerLayout/MainWindow.xaml.cs#L536-L539));
- SVG selalu ditempel sebagai gambar;
- halaman PDF > 400 mm dirender maksimal 4000 px ([Core.cs:291](../../StickerLayout/Core.cs#L291)).

**Sudah diperbaiki (di-commit bersama dokumen ini):**
- penjaga jarak dibulatkan ke atas ([Core.cs:422](../../StickerLayout/Core.cs#L422));
- die minimal 1 piksel di luar kiss ([Core.cs:186](../../StickerLayout/Core.cs#L186));
- D1: cache konversi CDR dengan versi file ([Core.cs:518-519](../../StickerLayout/Core.cs#L518-L519));
- D2: salinan PDF/AI saat dimuat ([Core.cs:110-122](../../StickerLayout/Core.cs#L110-L122), [MainWindow.xaml.cs:247](../../StickerLayout/MainWindow.xaml.cs#L247));
- D10: latar PNG/JPG warna muda tidak lagi dibuang ([Core.cs:158](../../StickerLayout/Core.cs#L158)), 2026-10-21;
- peringatan PDF yang ditempel sebagai gambar ([Core.cs:32](../../StickerLayout/Core.cs#L32), [MainWindow.xaml.cs:109-110](../../StickerLayout/MainWindow.xaml.cs#L109-L110)).

## 11. Urutan perbaikan yang disarankan (berdasarkan risiko produksi)
D1 dan D2 sudah diperbaiki 2026-10-19. Berikutnya:
1. **D8:** satu PDF terenkripsi menggagalkan seluruh export. Perbaikan kecil: bila PDF tidak bisa ditempel vektor saat export, pakai gambar dengan peringatan. Juga cek enkripsi saat dimuat.
2. **D7:** putuskan perilakunya dulu. Saat ini garis bisa berbentuk kotak, bukan mengikuti logo.
3. **D4:** RAM file besar. Usulan: buat mask langsung di 6 px/mm (bukan render 10 px/mm penuh), dan decode gambar besar dengan ukuran kecil untuk mask dan layar. File asli tetap dipakai untuk export. Perlu diukur ulang dengan `ram-profil.ps1`.
4. **Zip operator di PC operator (HW-12), lalu HW-01 sampai HW-08 dan IMP-09:** satu-satunya bukti produksi fisik dan bukti D1 dengan CorelDRAW asli.
5. **Keputusan toleransi D3 dan D5; pesan error D9; tampilan dpi D6.**

## 12. Keputusan yang dibutuhkan
1. **D8:** izin memperbaiki (gambar + peringatan bila PDF tidak bisa ditempel vektor saat export).
2. **D4:** izin mengubah cara render/decode untuk mask dan layar, supaya RAM file besar turun.
3. **D7:** garis potong mengikuti logo atau mengikuti kotak putih?
4. **A4/D3:** terima toleransi 0,15 mm dan tulis di aturan (saran), atau uji mask 10 px/mm (lebih berat, belum diuji)?
5. **A2b/D5:** target node ≥ 60% berlaku hanya untuk `rough.jpg` (S1)?
6. **Pemeriksaan otomatis yang wajib untuk rilis:** self-check saja, atau ditambah `validasi.py` exit 0 dan `ui-edit-layout.ps1` 11/11?
7. **Severity:** apakah usulan severity D1–D10 dan definisinya (bagian 1) disetujui?
8. **File customer:** di mana lokasi file customer yang sudah diizinkan untuk pengujian?
