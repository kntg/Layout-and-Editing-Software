# Rencana validasi end-to-end Printech

<!-- e2e-validation.md dan e2e-test-results-template.csv dibuat oleh gen_e2e.py dari e2e-validation.template.md. Ubah template atau kasus uji di gen_e2e.py, lalu jalankan: python -I docs/testing/gen_e2e.py -->

Status: draf 2026-10-19. Disusun dari audit kode, bukan hanya dari dokumentasi. Rencana ini mencakup alur dari file customer masuk ke Printech sampai PDF dicetak di Develop dan dipotong di Graphtec.

**Versi kode yang diuji:** commit `0cc6446` (2026-10-19). Berisi commit `7a414c8` ditambah perubahan 2026-10-18 dan 2026-10-19:

- [Core.cs](../../StickerLayout/Core.cs): penjaga jarak dibulatkan ke atas, die minimal 1 piksel di luar kiss, `Sticker.PdfAsImage`, `Nest.Compact`, salinan PDF/AI saat dimuat (D2), dan cache CDR dengan versi file (D1);
- [MainWindow.xaml.cs](../../StickerLayout/MainWindow.xaml.cs) dan [Strings.cs](../../StickerLayout/Strings.cs): peringatan bila PDF ditempel sebagai gambar; Susun memuat ulang dari PDF yang sama;
- [Export.cs](../../StickerLayout/Export.cs) dan [SelfCheck.cs](../../StickerLayout/SelfCheck.cs): baris Print Ticket dan tes tambahan;
- folder `Printech-TestData/`.

Bagian 4 berisi hasil pemeriksaan sebelum perbaikan D1/D2, hasil sesudahnya, dan uji tambahan 2026-10-19 sore (kasus khusus, alur edit lewat UI, Print Ticket panjang, profil RAM).

## 1. Tingkat pengujian, status, dan severity

| Tingkat | Isi | Cara menjalankan | Dijalankan di audit ini |
| --- | --- | --- | --- |
| Otomatis | Self-check bawaan aplikasi ([SelfCheck.cs](../../StickerLayout/SelfCheck.cs)): skenario S1–S9, S11–S13, P1–P5, dan pemeriksaan tambahan | `StickerLayout.exe --selfcheck <folder>`. Exit code 0 dan baris terakhir `OK` berarti lulus | Ya |
| Integrasi | Mesin Printech dijalankan pada dataset `Printech-TestData`, lalu dinilai dengan geometri file asli: `validasi.ps1` + `validasi.py`, ditambah `cek-original.ps1` | Lihat [Printech-TestData/README.md](../../Printech-TestData/README.md) | Ya |
| Manual UI | Pengujian lewat aplikasi di PC pengembang. Sebagian sudah otomatis dengan UI Automation (mouse, keyboard, kontrol asli): `ui-semua-input.ps1`, `ui-file-khusus.ps1`, `ui-edit-layout.ps1`, `ui-undo.ps1`, `ui-editor-garis.ps1` di `Printech-TestData`, dijalankan dengan Windows PowerShell 5.1 | Langkah per kasus uji, atau skrip | Ya (2026-10-19) |
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

### Di Printech

1. **File masuk** lewat tombol Tambah, Ctrl+I/Ctrl+O, drag & drop, atau argumen: `MainWindow.AddFiles` ([MainWindow.xaml.cs:225](../../StickerLayout/MainWindow.xaml.cs#L225)).
2. **CDR dikonversi ke PDF** oleh proses worker `--cdr2pdf` ([App.xaml.cs:19](../../StickerLayout/App.xaml.cs#L19)) lewat CorelDRAW COM: `Corel.ToPdf` dan `Corel.Worker` ([Core.cs:648](../../StickerLayout/Core.cs#L648), [Core.cs:674](../../StickerLayout/Core.cs#L674)).
   - Batas waktu 30 detik.
   - Hasil konversi disimpan di cache per path dan versi file (waktu ubah + ukuran), jadi CDR yang diedit dikonversi lagi ([Core.cs:645-646](../../StickerLayout/Core.cs#L645-L646)).
3. **Desain dimuat:** `Loader.LoadAll` dan `Loader.Load` ([Core.cs:92](../../StickerLayout/Core.cs#L92), [Core.cs:132](../../StickerLayout/Core.cs#L132)).
   - PDF/AI disalin dulu ke folder temp; desain dan export memakai salinan itu ([Core.cs:113-126](../../StickerLayout/Core.cs#L113-L126)). Susun memuat ulang dari salinan yang sama ([MainWindow.xaml.cs:350](../../StickerLayout/MainWindow.xaml.cs#L350)).
   - PDF/AI dirender dengan PDFium dan dicek apakah bisa ditempel vektor ([Core.cs:296](../../StickerLayout/Core.cs#L296)).
   - SVG dirender dengan SharpVectors ([Core.cs:331](../../StickerLayout/Core.cs#L331)).
   - PNG/JPG didekode.
   - Konten di-crop ([Core.cs:387](../../StickerLayout/Core.cs#L387)). Artwork = piksel tidak transparan (alpha > 16). Latar opak, termasuk putih, tidak dibuang otomatis sejak masukan leader 2026-10-21 ([Core.cs:383-385](../../StickerLayout/Core.cs#L383-L385), [Core.cs:158](../../StickerLayout/Core.cs#L158)).
4. **Garis potong dibuat:**
   - mask 6 px/mm → closing (offset + halus) → blur + penjaga jarak → rongga diisi, kecuali lubang asli yang tetap terbuka (donat) → Potrace ([Core.cs:405-554](../../StickerLayout/Core.cs#L405-L554));
   - die = die ∪ (kiss + 1 piksel) ([Core.cs:216](../../StickerLayout/Core.cs#L216)).
   - Garis manual dari editor garis potong (polygon) dipakai apa adanya; grid nesting dan luas dihitung dari polygon ([Core.cs:132](../../StickerLayout/Core.cs#L132), [Core.cs:556-633](../../StickerLayout/Core.cs#L556-L633)).
5. **Bleed** (opsional, hanya raster): [Core.cs:248](../../StickerLayout/Core.cs#L248).
6. **Nesting:** `Nest.Run` ([Core.cs:818](../../StickerLayout/Core.cs#L818)).
   - Greedy di grid 1 mm, 4 rotasi, 3 urutan percobaan dihitung paralel.
   - `Nest.Check` memeriksa margin dan tumpang tindih ([Core.cs:930](../../StickerLayout/Core.cs#L930)).
7. **Edit layout:** seret + snap ([MainWindow.xaml.cs:707-711](../../StickerLayout/MainWindow.xaml.cs#L707-L711), [Core.cs:885](../../StickerLayout/Core.cs#L885)), putar, panah, hapus ([MainWindow.xaml.cs:393-402](../../StickerLayout/MainWindow.xaml.cs#L393-L402), [MainWindow.xaml.cs:654-659](../../StickerLayout/MainWindow.xaml.cs#L654-L659)), penuhi lembar (`Nest.Fill`, [Core.cs:907](../../StickerLayout/Core.cs#L907)).
   - Undo/redo global: snapshot dokumen setelah setiap aksi ([MainWindow.xaml.cs:167-205](../../StickerLayout/MainWindow.xaml.cs#L167-L205)).
   - Editor garis potong per desain ([MainWindow.xaml.cs:294-321](../../StickerLayout/MainWindow.xaml.cs#L294-L321), [CutEditWindow.xaml.cs](../../StickerLayout/CutEditWindow.xaml.cs)).
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
| --- | --- | --- | --- |
| Import PDF/AI | [Core.cs:92-163](../../StickerLayout/Core.cs#L92-L163), [Core.cs:296-328](../../StickerLayout/Core.cs#L296-L328) | selfcheck `logo.pdf`/`logo.ai`/`multi.pdf`; validasi E1, X1, X3 (termasuk PDF terenkripsi); verifikasi dataset | kotak putih (D7) |
| Import CDR | [Core.cs:643-687](../../StickerLayout/Core.cs#L643-L687), [App.xaml.cs:19](../../StickerLayout/App.xaml.cs#L19), [MainWindow.xaml.cs:235-241](../../StickerLayout/MainWindow.xaml.cs#L235-L241) | selfcheck P1–P2 (tanpa CorelDRAW) | Konversi asli, CorelDRAW macet, dan bukti D1 dengan CorelDRAW: butuh operator |
| Import PNG/JPG/SVG | [Core.cs:146-155](../../StickerLayout/Core.cs#L146-L155), [Core.cs:331-347](../../StickerLayout/Core.cs#L331-L347) | selfcheck `circle.png`/`rough.jpg`/`star.jpg`/`badge.svg`; validasi | File rusak |
| dpi dan peringatan | [Core.cs:46-50](../../StickerLayout/Core.cs#L46-L50) | selfcheck (SelfCheck.cs:100-103); verifikasi dataset | Pembulatan tampilan (D6) |
| Garis potong | [Core.cs:405-554](../../StickerLayout/Core.cs#L405-L554), editor [Core.cs:556-633](../../StickerLayout/Core.cs#L556-L633) dan [CutEditWindow.xaml.cs](../../StickerLayout/CutEditWindow.xaml.cs), smart select [SmartSelect.cs](../../StickerLayout/SmartSelect.cs) | S1–S9, S11–S13; validasi A1–A6 | Potong fisik (S10) |
| Bleed | [Core.cs:241-293](../../StickerLayout/Core.cs#L241-L293) | selfcheck (SelfCheck.cs:338-352); validasi B4 | Cetak/potong fisik |
| Nesting dan pemeriksaan geometri | [Core.cs:742-951](../../StickerLayout/Core.cs#L742-L951) | selfcheck (SelfCheck.cs:364-390), P3, P5; validasi B1–B7 | — |
| Edit layout (UI) | [MainWindow.xaml.cs:384-718](../../StickerLayout/MainWindow.xaml.cs#L384-L718) | Fungsi `Snap`/`Check`/`Fill` dan logika editor garis potong lewat selfcheck; alur UI lewat `ui-edit-layout.ps1` (EDIT-01..09, EDIT-11, CUT-12), `ui-undo.ps1` (EDIT-12), dan `ui-editor-garis.ps1` (EDIT-13) | Uji UI tidak masuk self-check: dijalankan terpisah, butuh desktop tanpa gangguan mouse |
| Export PDF | [Export.cs:75-141](../../StickerLayout/Export.cs#L75-L141) | selfcheck S8 (SelfCheck.cs:408-439), salinan PDF (SelfCheck.cs:466-480); validasi E1, X1–X3 | CorelDRAW |
| Preview JPG dan Print Ticket | [Export.cs:21-71](../../StickerLayout/Export.cs#L21-L71), [Export.cs:144-176](../../StickerLayout/Export.cs#L144-L176) | selfcheck (SelfCheck.cs:401-403, 446-450); validasi X4–X5 | Ticket dengan > 10 lembar / > 20 desain |
| Rapikan gambar (dibekukan) | [Cleanup.cs](../../StickerLayout/Cleanup.cs), [CleanupWindow.xaml.cs](../../StickerLayout/CleanupWindow.xaml.cs) | selfcheck (SelfCheck.cs:74-94), P4, P5 | Dialog UI |
| Kinerja dan RAM | [MainWindow.xaml.cs:367-370](../../StickerLayout/MainWindow.xaml.cs#L367-L370), [Core.cs:351-364](../../StickerLayout/Core.cs#L351-L364) | P5 otomatis; P6 manual; `ram-profil.ps1` | Tidak ada gerbang otomatis untuk RAM |

## 4. Hasil pemeriksaan otomatis (2026-10-19)

| Perintah | Exit code | Hasil | Artefak |
| --- | --- | --- | --- |
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

### Setelah perbaikan D1/D2 (2026-10-19)

| Perintah | Exit code | Hasil |
| --- | --- | --- |
| `dotnet build StickerLayout -c Release` dan `-c Debug` | 0 | 0 warning, 0 error |
| Self-check Release dan Debug (folder baru, `samples`, 2 uji negatif) | 0 / 0 / 1 / 1 sesuai harapan | 15/15 skenario lulus. Cek baru: PDF diganti lalu dihapus setelah dimuat, muat ulang dan export tetap dari salinan (49,5 mm), tambah ulang memakai isi baru (28,3 mm) |
| `validasi.ps1` + `validasi.py` | 0 / 1 | Semua angka dan vonis sama persis dengan sebelum perbaikan (gagal tetap A2b, A4, A5) |
| Uji UI, semua file sekaligus | — | Sama dengan sebelum perbaikan: "18 stiker di 2 lembar.", 3 peringatan, "54% · sisa 46%" |
| `cek-original.ps1` | 0 | 15/15 file original tidak berubah |

### Uji tambahan 2026-10-19 (tanpa perubahan kode)

| Uji | Exit code | Hasil |
| --- | --- | --- |
| `ui-file-khusus.ps1` (5 file `01-input/khusus`) | 0 | 4 dialog "Gagal memuat" ditutup satu per satu; 1 desain masuk; aplikasi responsif. Pesan error masih teknis (D9). |
| PDF terenkripsi lewat mesin Printech | — | Dimuat sebagai vektor, lalu `Export.Pdf` melempar exception sehingga seluruh export gagal (D8). |
| `ui-edit-layout.ps1`, 3 kali berturut-turut | 0 | 11/11 langkah lulus di setiap jalan (EDIT-01..09, EDIT-11, CUT-12). |
| Print Ticket 13 lembar, 21 desain | — | Tetap 1 halaman A4; ada baris "… dan 3 lainnya" (lembar) dan "… dan 1 lainnya" (desain). |
| `ram-profil.ps1` | 0 | Kosong 35 MB, 4 desain 67, 16 desain 99, JPG 24 MP 216, dua PDF besar 347, semua 19 desain 351 MB (D4). |
| `cek-original.ps1` (termasuk `01-input/khusus`) | 0 | 20/20 file original tidak berubah. |

**Diulang 2026-10-21 setelah perbaikan D10:** build Release dan Debug 0 warning; selfcheck Release dan Debug 4/4 sesuai harapan (15/15 skenario, ditambah cek latar abu 242, pink pastel, dan hampir putih 252); `validasi.ps1` menghasilkan 1144 nilai garis, ukuran, dan posisi yang identik dengan 2026-10-19; laporan `validasi.py` identik (A2b, A4, A5 tetap gagal seperti sebelumnya); `cek-original.ps1` 23/23 sama (3 file baru `01-input/latar`); tiga PNG latar dimuat di UI tanpa peringatan.

**Diulang 2026-10-21 setelah perbaikan D8 dan D4:** build Release dan Debug 0 warning; selfcheck Release dan Debug 4/4 (15/15); `validasi.ps1`: semua 1584 nilai garis, ukuran, posisi, dan bleed identik dengan sebelum D4, skenario X sekarang berisi PDF terenkripsi; `validasi.py`: X3 lulus termasuk PDF terenkripsi, A2b, A4, A5 tetap gagal seperti sebelumnya; `ram-profil.ps1`: semua 19 desain 84 MB; zip operator dibangun ulang, selfcheck exe hasil publish dan exe dari zip exit 0. Bukti: [pdf-terenkripsi](../../Printech-TestData/04-evidence/2026-10-21-pdf-terenkripsi.txt), [ram-profil](../../Printech-TestData/04-evidence/2026-10-21-ram-profil.txt), [validasi](../../Printech-TestData/04-evidence/2026-10-21-validasi-garis-potong-nesting.txt).

**Masukan leader tahap 1 (2026-10-21):** latar gambar opak tidak dibuang otomatis dan Compact diganti "Penuhi lembar" ([spec](../superpowers/specs/2026-10-21-masukan-leader-design.md)). Selfcheck Release dan Debug 4/4 (15/15); JPG di selfcheck dirapikan dulu seperti alur operator. Validasi: hanya A2b, A4, A5 gagal seperti sebelumnya; B-campuran dipenuhi 198 stiker di 4 lembar tanpa tabrakan. UI semua input: "18 stiker di 2 lembar.", 54 %, judul lembar 92 % dan 16 %, RAM 87 MB ([bukti](../../Printech-TestData/04-evidence/2026-10-21-ui-semua-input.txt)). Probe D7: kotak putih dipertahankan.

**Masukan leader tahap 2–4 (2026-10-21):** undo/redo global (`ui-undo.ps1`: EDIT-12 lulus), garis dalam untuk lubang asli (stiker donat, selfcheck S11, CUT-07), dan editor garis potong polygon (selfcheck S12: CUT-14; `ui-editor-garis.ps1` tanpa mouse: EDIT-13 lulus 5/5, [bukti](../../Printech-TestData/04-evidence/2026-10-21-ui-editor-garis.txt)). Build Release dan Debug 0 warning. Selfcheck Release 4/4 (16/16 skenario). Selfcheck Debug: S9 (stiker 300 mm < 5 detik di Debug) 3,9–5,8 detik, gagal 3 dari 6 jalan saat CPU PC 42–62 % dipakai aplikasi lain; build HEAD sebelum perubahan hari ini, diukur bergantian di kondisi yang sama, 2,1–7,4 detik, jadi bukan regresi, tetapi kriteria Debug belum terbukti lulus hari ini. Validasi: hanya A2b, A4, A5 gagal seperti sebelumnya. UI semua input: angka sama, RAM 81 MB. Seret, tambah, dan hapus titik dengan mouse sungguhan: NOT RUN (EDIT-14).

**Smart select (2026-10-22):** klik objek tanpa AI di editor garis potong ([spesifikasi](../superpowers/specs/2026-10-22-smart-select-design.md)). Selfcheck S13 lulus (CUT-15); kotak centang dan Toleransi lewat UI Automation lulus, 6/6 langkah (EDIT-15); klik kanvas dengan mouse NOT RUN (EDIT-16). Garis otomatis tidak berubah: `validasi.json` identik dengan sebelum perubahan, selfcheck Release dan Debug 4/4 (17/17 skenario). File donat user (pngtree, tidak masuk repo): garis luar 49,51 mm dengan watermark menjadi 44,03 mm tanpa watermark, dan lubang tengah dapat garis dalam 1,36 mm dengan 1 klik ([bukti](../../Printech-TestData/04-evidence/2026-10-22-smart-select.txt)).

## 5. Matriks format

| Aspek | PDF | AI (PDF-compatible) | CDR | PNG | JPG | SVG |
| --- | --- | --- | --- | --- | --- | --- |
| Dimuat, ukuran benar | IMP-01, IMP-02: PASS | IMP-03: PASS | IMP-05: BLOCKED | IMP-10: PASS | IMP-11: PASS | IMP-12: PASS |
| Multi-halaman jadi desain terpisah | IMP-02: PASS | NOT RUN | NOT RUN | — | — | — |
| Di PDF export | Vektor, warna CMYK utuh (EXP-04: PASS). Halaman /Rotate jadi gambar + peringatan (IMP-16: PASS) | Vektor (EXP-04: PASS) | Vektor bila konversi berhasil (BLOCKED) | Gambar | Gambar | Gambar ±300 dpi (batasan PRD) |
| dpi dan peringatan resolusi | Tidak tampil (vektor) | Tidak tampil | Tidak tampil | IMP-13: PASS | IMP-13: PASS | ±305 dpi |
| File rusak atau gagal | IMP-17: PASS (PDF terenkripsi jadi gambar + peringatan, D8 diperbaiki) | IMP-04: PASS | IMP-06: PASS; IMP-08: BLOCKED | IMP-21: PASS | IMP-21: PASS | IMP-21: PASS |
| Latar warna muda (abu 242, pink pastel) | — | — | — | IMP-23: PASS (sebelumnya hilang, D10) | Aturan sama, belum ada file uji | — |
| Latar putih | IMP-18: PASS (kotak dipertahankan) | Sama dengan PDF | — | Ada transparansi: putih dihitung artwork | Dipakai utuh, tidak dibuang (IMP-11) | — |

## 6. Kasus uji

Total {{TOTAL}} kasus: PASS {{PASS}}, FAIL {{FAIL}}, BLOCKED {{BLOCKED}}, NOT RUN {{NOT_RUN}}. Status per 2026-10-19; IMP-10, IMP-11, IMP-17, IMP-18, IMP-23, CUT-07, CUT-14, NEST-10, EDIT-12, EDIT-13, EDIT-14, HW-15, dan PERF-01 per 2026-10-21; CUT-15, EDIT-15, EDIT-16, dan HW-16 per 2026-10-22.

Kolom Sumber menunjukkan asal kriterianya:

- **Dok:** sudah tertulis di PRD, dokumen uji, atau spesifikasi leader.
- **Kode:** perilaku yang ada di kode tetapi belum tertulis di dokumen.
- **Kriteria B / Permintaan user:** berasal dari rencana validasi yang dikirim user, belum masuk dokumen resmi.
- **Usul:** kriteria baru yang masih perlu disetujui.

Lembar kerja yang sama, untuk diisi saat menguji: [e2e-test-results-template.csv](e2e-test-results-template.csv).

{{TESTCASES}}

## 7. Bukti yang harus dikumpulkan

- **Otomatis:** `selfcheck.txt` dan exit code. Simpan salinannya di `Printech-TestData/04-evidence/`.
- **Integrasi:** laporan `04-evidence/<tanggal>-validasi-garis-potong-nesting.txt`, gambar bukti `04-evidence/<tanggal>-validasi-*.png`, dan export di `03-exports/validasi/`.
- **Manual UI:** tangkapan layar bernama `TANGGAL-ID-keterangan.png` di `Printech-TestData/04-evidence/`.
- **Operator/hardware:** catat bahan, versi CorelDRAW dan Cutting Master 5, serta model dan firmware Graphtec.
  - Foto hasil cetak dan potong dengan penggaris atau lup.
  - Ukuran selisih potong (mm) per desain.
- **Isi CSV:** kolom Actual Result, Status, Evidence (path bukti), dan Environment (PC, Windows, build/commit). Kolom Evidence sudah berisi jenis bukti yang diminta; ganti dengan path bukti yang sebenarnya.

## 8. Persyaratan yang sudah terdokumentasi dan kriteria baru

### Sudah terdokumentasi

- [PRD](../PRD.md) F1–F20: format input, crop, garis kiss/die, kertas, margin, susun banyak lembar, edit, export DeviceCMYK, bahasa, dpi 150, preview 2000 px, Rapikan, garis dengan node sedikit, bleed ≤ jarak, indikator bahan dan target, snap/preset, Print Ticket.
- PRD §6 (keputusan): penjaga jarak offset − 0,3 mm, die membungkus kiss, grid 1 mm, worker CDR 30 detik.
- PRD §8 (batasan): hanya kontur luar dan lubang asli yang tetap terbuka (donat) dipotong; SVG dan PDF yang diputar jadi gambar dengan peringatan.
- [UJI-GARIS-POTONG.md](../UJI-GARIS-POTONG.md) S1–S12, [UJI-STABILITAS.md](../UJI-STABILITAS.md) P1–P10, dan checklist operator PRD §10 butir 1–13.
- Spesifikasi leader:
  - bagian 2: RAM ±120 MB, UI tetap responsif;
  - bagian 3: DeviceCMYK, bleed;
  - bagian 4: indikator bahan, snap, preset, Print Ticket;
  - tahap export (Printech-Deskripsi.pdf).

### Kriteria baru (usulan, perlu disetujui)

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
| --- | --- | --- |
| 1. Build dan pengujian otomatis | Bagian 4 | Belum. `validasi.py` exit 1 (A2b, A4, A5). Build, self-check, dan uji regresi lulus setelah perbaikan D1/D2. |
| 2. Input dan geometri | IMP-\*, CUT-\*, NEST-\* | Belum. CUT-03, CUT-05, CUT-10 FAIL. IMP-05, IMP-08, IMP-09, IMP-18 BLOCKED. IMP-22 (file customer) NOT RUN. |
| 3. Export dan kompatibilitas | EXP-\*, HW-01 sampai HW-04 | Belum. Semua EXP lulus; PDF terenkripsi tidak lagi menggagalkan export (D8 diperbaiki 2026-10-21). HW-01 sampai HW-04 BLOCKED. |
| 4. Produksi fisik | HW-05 sampai HW-08 | Belum. BLOCKED. |
| 5. Dokumentasi dan defect | Bagian 7 dan 10 | Belum. Defect terbuka: D3, D5, D6, D7, D9 (D1, D2, D4, D8, D10 sudah diperbaiki). |

## 10. Temuan dan defect terbuka

| ID | Temuan | Bukti | Lokasi | Usul severity |
| --- | --- | --- | --- | --- |
| D1 | **Diperbaiki 2026-10-19:** cache sekarang memakai versi file ([Core.cs:645-646](../../StickerLayout/Core.cs#L645-L646)); bukti dengan CorelDRAW menunggu IMP-09. Sebelumnya: cache konversi CDR hanya memakai path file sebagai kunci. Bila CDR diedit lalu ditambah ulang di sesi yang sama, hasil konversi lama yang dipakai. | Kode | [Core.cs:646](../../StickerLayout/Core.cs#L646) | Blocker |
| D2 | **Diperbaiki 2026-10-19:** PDF/AI disalin saat dimuat ([Core.cs:113-126](../../StickerLayout/Core.cs#L113-L126)); dicek selfcheck (SelfCheck.cs:466-480). Sebelumnya: file PDF dibaca ulang saat export, sedangkan garis potong dihitung saat dimuat. Bila file diubah di antaranya, artwork dan garis bisa tidak cocok. Bila file dipindah, export memakai render layar RGB beresolusi rendah dan hanya memberi peringatan. | Kode | [Export.cs:131](../../StickerLayout/Export.cs#L131), [Export.cs:107-111](../../StickerLayout/Export.cs#L107-L111) | Mayor |
| D3 | Garis bisa sampai 0,13 mm lebih dekat dari batas offset − 0,3 mm, di sudut lancip dan pada offset kecil. Garis tetap di luar artwork. | validasi A4 | [Core.cs:457-478](../../StickerLayout/Core.cs#L457-L478) (penjaga jarak), [Core.cs:533-554](../../StickerLayout/Core.cs#L533-L554) (Potrace) | Mayor |
| D4 | **Diperbaiki 2026-10-21:** bitmap resolusi penuh hasil muat langsung dilepas setelah menyusun ([MainWindow.xaml.cs:367-370](../../StickerLayout/MainWindow.xaml.cs#L367-L370)), dan salinan layar tidak lagi memegang bitmap sumber ([Core.cs:351-364](../../StickerLayout/Core.cs#L351-L364)). RAM: semua 19 desain 279 → 84 MB, JPG 24 MP 226 → 41 MB, dua PDF besar 326 → 53 MB. Sebelumnya: GC tidak jalan setelah muat, sehingga bitmap decode/render penuh (JPG 24 MP: 96 MB native + 96 MB heap) tetap terhitung; logo kecil di halaman besar menahan render halaman penuh (±43 MB) selama desainnya ada. Puncak sesaat saat memuat masih tinggi. | [ram-profil](../../Printech-TestData/04-evidence/2026-10-21-ram-profil.txt) | [Core.cs:351-364](../../StickerLayout/Core.cs#L351-L364) | Mayor |
| D5 | Node pada logo bertepi bergelombang hanya turun 54% (target 60%). | validasi A2b | — | Minor |
| D6 | Nilai 149,5–149,99 dpi tampil sebagai "150 dpi" tetapi berwarna merah. | Kode | [Core.cs:49-50](../../StickerLayout/Core.cs#L49-L50) | Minor |
| D7 | **Diputuskan 2026-10-21:** latar tidak dibuang otomatis, jadi kotak putih di PDF selalu dipertahankan dan garis mengelilingi kotak (dicek probe, IMP-18). Sebelumnya: kotak putih yang tidak memenuhi halaman menghasilkan kotak 84 × 84 mm, sedangkan halaman putih penuh mengikuti logo (44 × 44 mm). | [d7-kotak-putih](../../Printech-TestData/04-evidence/2026-10-21-d7-kotak-putih.txt) | [Core.cs (Fg)](../../StickerLayout/Core.cs) | Mayor |
| D8 | **Diperbaiki 2026-10-21:** saat dimuat, halaman PDF dicoba ditempel seperti saat export ([Core.cs:313-320](../../StickerLayout/Core.cs#L313-L320)); bila gagal, PDF ditempel sebagai gambar dengan peringatan. Dicek validasi X3 dan UI. Sebelumnya: PDF terenkripsi (hanya kata sandi pemilik) lolos cek vektor saat dimuat, lalu `Export.Pdf` melempar exception "The archive entry was compressed using an unsupported compression method." sehingga seluruh export layout gagal. | [pdf-terenkripsi](../../Printech-TestData/04-evidence/2026-10-21-pdf-terenkripsi.txt) | [Core.cs:313-320](../../StickerLayout/Core.cs#L313-L320) | Mayor |
| D9 | Pesan error untuk file rusak masih teknis, sebagian berbahasa Inggris: "PDF tidak bisa dibuka (PDFium error 3)." untuk AI tanpa PDF-compatible; "No imaging component suitable…" untuk PNG/JPG; "Data at the root level is invalid…" untuk SVG. | `ui-file-khusus.ps1` | [Pdfium.cs:43](../../StickerLayout/Pdfium.cs#L43), [MainWindow.xaml.cs:243](../../StickerLayout/MainWindow.xaml.cs#L243) | Minor |
| D10 | **Diperbaiki 2026-10-21, lalu digantikan masukan leader hari yang sama:** latar gambar opak tidak pernah dibuang otomatis, termasuk putih ([Core.cs (Fg)](../../StickerLayout/Core.cs)); dicek selfcheck. Laporan tim awal: latar warna muda (abu 242, pink pastel) hilang dari layar dan PDF, tinggal cincin selebar offset kiss, dan lebar stiker dipakai untuk logo saja. | [png-latar](../../Printech-TestData/04-evidence/2026-10-21-png-latar.txt) | [Core.cs (Fg)](../../StickerLayout/Core.cs) | Mayor |

**Batasan yang sudah diketahui** (tercatat di PRD, bukan defect baru):

- file selain CDR dimuat di UI thread ([MainWindow.xaml.cs:241](../../StickerLayout/MainWindow.xaml.cs#L241));
- Susun otomatis menimpa edit manual; sejak 2026-10-21 bisa dibatalkan dengan undo (Ctrl+Z);
- geser dengan panah tidak di-snap ([MainWindow.xaml.cs:654-659](../../StickerLayout/MainWindow.xaml.cs#L654-L659));
- SVG selalu ditempel sebagai gambar;
- halaman PDF > 400 mm dirender maksimal 4000 px ([Core.cs:303](../../StickerLayout/Core.cs#L303)).

**Sudah diperbaiki (di-commit bersama dokumen ini):**

- penjaga jarak dibulatkan ke atas ([Core.cs:475](../../StickerLayout/Core.cs#L475));
- die minimal 1 piksel di luar kiss ([Core.cs:216](../../StickerLayout/Core.cs#L216));
- D1: cache konversi CDR dengan versi file ([Core.cs:645-646](../../StickerLayout/Core.cs#L645-L646));
- D2: salinan PDF/AI saat dimuat ([Core.cs:113-126](../../StickerLayout/Core.cs#L113-L126), [MainWindow.xaml.cs:350](../../StickerLayout/MainWindow.xaml.cs#L350));
- D8: PDF yang gagal ditempel saat dimuat jadi gambar + peringatan ([Core.cs:313-320](../../StickerLayout/Core.cs#L313-L320)), 2026-10-21;
- D4: bitmap hasil muat dilepas setelah menyusun, salinan layar mandiri ([MainWindow.xaml.cs:367-370](../../StickerLayout/MainWindow.xaml.cs#L367-L370)), 2026-10-21;
- D10: latar PNG/JPG warna muda tidak lagi dibuang (2026-10-21); sejak masukan leader tahap 1 tidak ada latar opak yang dibuang otomatis ([Core.cs:383-385](../../StickerLayout/Core.cs#L383-L385));
- peringatan PDF yang ditempel sebagai gambar ([Core.cs:32](../../StickerLayout/Core.cs#L32), [MainWindow.xaml.cs:109-110](../../StickerLayout/MainWindow.xaml.cs#L109-L110)).

## 11. Urutan perbaikan yang disarankan (berdasarkan risiko produksi)

D1 dan D2 sudah diperbaiki 2026-10-19; D4, D8, dan D10 pada 2026-10-21. Berikutnya:

1. **Zip operator di PC operator (HW-12, zip dibangun ulang 2026-10-21), lalu HW-01 sampai HW-08 dan IMP-09:** satu-satunya bukti produksi fisik dan bukti D1 dengan CorelDRAW asli.
2. **Keputusan toleransi D3 dan D5; pesan error D9; tampilan dpi D6.**

## 12. Keputusan yang dibutuhkan

1. **A4/D3:** terima toleransi 0,15 mm dan tulis di aturan (saran), atau uji mask 10 px/mm (lebih berat, belum diuji)?
2. **A2b/D5:** target node ≥ 60% berlaku hanya untuk `rough.jpg` (S1)?
3. **Pemeriksaan otomatis yang wajib untuk rilis:** self-check saja, atau ditambah `validasi.py` exit 0 dan `ui-edit-layout.ps1` 11/11?
4. **Severity:** apakah usulan severity D1–D10 dan definisinya (bagian 1) disetujui?
5. **File customer:** di mana lokasi file customer yang sudah diizinkan untuk pengujian?
