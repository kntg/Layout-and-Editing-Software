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
| Otomatis | Self-check bawaan aplikasi ([SelfCheck.cs](../../StickerLayout/SelfCheck.cs)): skenario S1–S9, S11–S16, P1–P5, dan pemeriksaan tambahan | `StickerLayout.exe --selfcheck <folder>`. Exit code 0 dan baris terakhir `OK` berarti lulus | Ya |
| Integrasi | Mesin Printech dijalankan pada dataset `Printech-TestData`, lalu dinilai dengan geometri file asli: `validasi.ps1` + `validasi.py`, ditambah `cek-original.ps1` | Lihat [Printech-TestData/README.md](../../Printech-TestData/README.md) | Ya |
| Manual UI | Pengujian lewat aplikasi di PC pengembang. Sebagian sudah otomatis dengan UI Automation (mouse, keyboard, kontrol asli): `ui-semua-input.ps1`, `ui-file-khusus.ps1`, `ui-edit-layout.ps1`, `ui-undo.ps1`, `ui-editor-garis.ps1` di `Printech-TestData`, dijalankan dengan Windows PowerShell 5.1 | Langkah per kasus uji, atau skrip | Ya (2026-10-19) |
| Operator/hardware | CorelDRAW, Cutting Master 5, printer Develop, Graphtec | PC operator di AP 55 | Tidak (BLOCKED, di luar cakupan prototipe) |

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

1. **File masuk** lewat tombol Tambah, Ctrl+I/Ctrl+O, drag & drop, atau argumen: `MainWindow.AddFiles` ([MainWindow.xaml.cs:221](../../StickerLayout/MainWindow.xaml.cs#L221)).
2. **CDR dikonversi ke PDF** oleh proses worker `--cdr2pdf` ([App.xaml.cs:19](../../StickerLayout/App.xaml.cs#L19)) lewat CorelDRAW COM: `Corel.ToPdf` dan `Corel.Worker` ([Core.cs:601](../../StickerLayout/Core.cs#L601), [Core.cs:627](../../StickerLayout/Core.cs#L627)).
   - Batas waktu 30 detik.
   - Hasil konversi disimpan di cache per path dan versi file (waktu ubah + ukuran), jadi CDR yang diedit dikonversi lagi ([Core.cs:598-599](../../StickerLayout/Core.cs#L598-L599)).
3. **Desain dimuat:** `Loader.LoadAll` dan `Loader.Load` ([Core.cs:94](../../StickerLayout/Core.cs#L94), [Core.cs:134](../../StickerLayout/Core.cs#L134)).
   - PDF/AI disalin dulu ke folder temp; desain dan export memakai salinan itu ([Core.cs:115-128](../../StickerLayout/Core.cs#L115-L128)). Susun memuat ulang dari salinan yang sama ([MainWindow.xaml.cs:346](../../StickerLayout/MainWindow.xaml.cs#L346)).
   - PDF/AI dirender dengan PDFium dan dicek apakah bisa ditempel vektor ([Core.cs:304](../../StickerLayout/Core.cs#L304)).
   - SVG dirender dengan SharpVectors ([Core.cs:339](../../StickerLayout/Core.cs#L339)).
   - PNG/JPG didekode.
   - Konten di-crop ([Core.cs:395](../../StickerLayout/Core.cs#L395)). Artwork = piksel tidak transparan (alpha > 16). Latar opak, termasuk putih, tidak dibuang otomatis sejak masukan leader 2026-10-21 ([Core.cs:391-393](../../StickerLayout/Core.cs#L391-L393), [Core.cs:160](../../StickerLayout/Core.cs#L160)).
4. **Garis potong dibuat:**
   - mask 6 px/mm → closing (offset + halus) → blur + penjaga jarak → rongga diisi, kecuali lubang asli yang tetap terbuka (donat) → Potrace ([Core.cs:413-562](../../StickerLayout/Core.cs#L413-L562));
   - die = die ∪ (kiss + 1 piksel) ([Core.cs:224](../../StickerLayout/Core.cs#L224)).
   - Garis manual dari editor garis potong (node lurus dan kurva, sejak 2026-10-23) dan preset bentuk dipakai apa adanya; grid nesting dan luas dihitung dari garis itu ([Core.cs:134](../../StickerLayout/Core.cs#L134), [Curves.cs](../../StickerLayout/Curves.cs), [Shapes.cs](../../StickerLayout/Shapes.cs)). Desain kotak otomatis mendapat garis kotak tajam (`Shapes.IsBox`).
5. **Bleed** (opsional, hanya raster): [Core.cs:256](../../StickerLayout/Core.cs#L256).
6. **Nesting:** `Nest.Run` ([Core.cs:759](../../StickerLayout/Core.cs#L759)).
   - Greedy di grid 1 mm, 4 rotasi, 3 urutan percobaan dihitung paralel.
   - `Nest.Check` memeriksa margin dan tumpang tindih ([Core.cs:871](../../StickerLayout/Core.cs#L871)).
7. **Edit layout:** seret + snap ([MainWindow.xaml.cs:693-697](../../StickerLayout/MainWindow.xaml.cs#L693-L697), [Core.cs:826](../../StickerLayout/Core.cs#L826)), putar, panah, hapus ([MainWindow.xaml.cs:389-398](../../StickerLayout/MainWindow.xaml.cs#L389-L398), [MainWindow.xaml.cs:640-645](../../StickerLayout/MainWindow.xaml.cs#L640-L645)), penuhi lembar (`Nest.Fill`, [Core.cs:848](../../StickerLayout/Core.cs#L848)).
   - Undo/redo global: snapshot dokumen setelah setiap aksi ([MainWindow.xaml.cs:164-201](../../StickerLayout/MainWindow.xaml.cs#L164-L201)).
   - Editor garis potong per desain ([MainWindow.xaml.cs:290-317](../../StickerLayout/MainWindow.xaml.cs#L290-L317), [CutEditWindow.xaml.cs](../../StickerLayout/CutEditWindow.xaml.cs)).
8. **Indikator bahan dan peringatan:** [MainWindow.xaml.cs:93-109](../../StickerLayout/MainWindow.xaml.cs#L93-L109).
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
| Import PDF/AI | [Core.cs:94-165](../../StickerLayout/Core.cs#L94-L165), [Core.cs:304-336](../../StickerLayout/Core.cs#L304-L336) | selfcheck `logo.pdf`/`logo.ai`/`multi.pdf`; validasi E1, X1, X3 (termasuk PDF terenkripsi); verifikasi dataset | kotak putih (D7) |
| Import CDR | [Core.cs:596-640](../../StickerLayout/Core.cs#L596-L640), [App.xaml.cs:19](../../StickerLayout/App.xaml.cs#L19), [MainWindow.xaml.cs:231-237](../../StickerLayout/MainWindow.xaml.cs#L231-L237) | selfcheck P1–P2 (tanpa CorelDRAW) | Konversi asli, CorelDRAW macet, dan bukti D1 dengan CorelDRAW: butuh operator |
| Import PNG/JPG/SVG | [Core.cs:148-157](../../StickerLayout/Core.cs#L148-L157), [Core.cs:339-355](../../StickerLayout/Core.cs#L339-L355) | selfcheck `circle.png`/`rough.jpg`/`star.jpg`/`badge.svg`; validasi | File rusak |
| dpi dan peringatan | [Core.cs:46-50](../../StickerLayout/Core.cs#L46-L50) | selfcheck (SelfCheck.cs:100-103); verifikasi dataset | Pembulatan tampilan (D6) |
| Garis potong | [Core.cs:413-562](../../StickerLayout/Core.cs#L413-L562), editor [Curves.cs](../../StickerLayout/Curves.cs) dan [CutEditWindow.xaml.cs](../../StickerLayout/CutEditWindow.xaml.cs), bentuk [Shapes.cs](../../StickerLayout/Shapes.cs), smart select [SmartSelect.cs](../../StickerLayout/SmartSelect.cs) | S1–S9, S11–S15; validasi A1–A7 | Potong fisik (S10) |
| Bleed | [Core.cs:249-301](../../StickerLayout/Core.cs#L249-L301) | selfcheck (SelfCheck.cs:338-352); validasi B4 | Cetak/potong fisik |
| Nesting dan pemeriksaan geometri | [Core.cs:683-892](../../StickerLayout/Core.cs#L683-L892) | selfcheck (SelfCheck.cs:364-390), P3, P5; validasi B1–B7 | — |
| Edit layout (UI) | [MainWindow.xaml.cs:380-704](../../StickerLayout/MainWindow.xaml.cs#L380-L704) | Fungsi `Snap`/`Check`/`Fill` dan logika editor garis potong lewat selfcheck; alur UI lewat `ui-edit-layout.ps1` (EDIT-01..08, EDIT-11, CUT-12), `ui-undo.ps1` (EDIT-12), `ui-editor-garis.ps1` (EDIT-13), dan `ui-editor-mouse.ps1` (EDIT-14, EDIT-16, EDIT-18, EDIT-20) | Uji UI tidak masuk self-check: dijalankan terpisah, butuh desktop tanpa gangguan mouse |
| Export PDF | [Export.cs:75-141](../../StickerLayout/Export.cs#L75-L141) | selfcheck S8 (SelfCheck.cs:408-439), salinan PDF (SelfCheck.cs:466-480); validasi E1, X1–X3 | CorelDRAW |
| Preview JPG dan Print Ticket | [Export.cs:21-71](../../StickerLayout/Export.cs#L21-L71), [Export.cs:144-176](../../StickerLayout/Export.cs#L144-L176) | selfcheck (SelfCheck.cs:401-403, 446-450); validasi X4–X5 | Ticket dengan > 10 lembar / > 20 desain |
| Rapikan gambar (dibekukan) | [Cleanup.cs](../../StickerLayout/Cleanup.cs), [CleanupWindow.xaml.cs](../../StickerLayout/CleanupWindow.xaml.cs) | selfcheck (SelfCheck.cs:74-94), P4, P5 | Dialog UI |
| Kinerja dan RAM | [MainWindow.xaml.cs:363-366](../../StickerLayout/MainWindow.xaml.cs#L363-L366), [Core.cs:359-372](../../StickerLayout/Core.cs#L359-L372) | P5 otomatis; P6 manual; `ram-profil.ps1` | Tidak ada gerbang otomatis untuk RAM |

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

**Masukan 2026-10-23 (kotak otomatis, preset bentuk, node kurva, preset bahan dihapus):** [spesifikasi](../superpowers/specs/2026-10-23-bentuk-garis-potong-design.md).

- **Selfcheck:** S12 diperluas untuk kurva (CUT-14), S14 desain kotak (CUT-16), dan S15 preset bentuk (CUT-17). Release dan Debug 4/4 (19/19 skenario).
- **Validasi:** kriteria baru A7 lulus (4 desain kotak, selisih ≤ 0,10 mm). A1 dan A5 disesuaikan untuk desain kotak. A2b, A4, dan A5 tetap gagal seperti sebelumnya, dengan lebih sedikit garis: A4 43 garis (sebelumnya 65), pita A5 5 garis (sebelumnya 27).
- **UI Automation:** `ui-editor-garis.ps1` 7/7, termasuk tombol bentuk (EDIT-17). `ui-semua-input.ps1` sama, kecuali lembar 1 92% (IMP-19).
- **Belum diuji:** seret node dan handle dengan mouse (EDIT-18, NOT RUN) dan potong di Graphtec (HW-17, BLOCKED).
- **Preset bahan dihapus:** langkah EDIT-09 di `ui-edit-layout.ps1` dan kasus EDIT-09 dihapus.
- **Bukti:** [2026-10-23-bentuk-garis-potong.txt](../../Printech-TestData/04-evidence/2026-10-23-bentuk-garis-potong.txt).

**Warna border (2026-10-23):** selfcheck S16 lulus (CUT-18); tombol warna lewat UI Automation lulus, `ui-editor-garis.ps1` 8/8 (EDIT-19); cetak dan potong di Develop/Graphtec BLOCKED (HW-18). [Bukti](../../Printech-TestData/04-evidence/2026-10-23-warna-border.txt).

**Temuan leader (2026-10-26):**

- Kiss dan die bisa beda bentuk: selfcheck S15 lulus (CUT-19).
- Klik node tanpa geser, seret relatif, dan Shift+klik untuk beberapa node: logika lulus di selfcheck S12, dan uji dengan mouse dan keyboard sungguhan `ui-editor-mouse.ps1` lulus (EDIT-20, CUT-19). [Bukti](../../Printech-TestData/04-evidence/2026-10-26-ui-editor-mouse.txt).
- Uji mouse dan keyboard yang lama diulang di versi ini: `ui-edit-layout.ps1` 10/10 dan `ui-undo.ps1` 3/3.
- Kasus editor yang sebelumnya NOT RUN kini diuji dengan mouse sungguhan dan lulus: EDIT-14 (operasi node), EDIT-16 (smart select dengan klik di kanvas), dan EDIT-18 (handle kurva). Total `ui-editor-mouse.ps1` 8/8. Skrip berhenti sendiri bila jendela editor tidak aktif, supaya klik tidak masuk ke aplikasi lain.
- Stiker tidak lagi kena tanda registrasi: selfcheck S17 lulus (NEST-11).
- Validasi B-ui kini menyalakan tanda registrasi seperti bawaan aplikasi, jadi lembar 1 menjadi 4 pcs 90%, sama dengan layar (IMP-19). [Bukti](../../Printech-TestData/04-evidence/2026-10-26-validasi-garis-potong-nesting.txt).

**Ujung tajam warna border dan bleed (2026-10-27):** sebelumnya warna border dan bleed menjorok lebih jauh di ujung tajam: ujung Bintang 2,26 mm dan Segitiga 2,00 mm untuk border 1 mm, serta 4,52 mm untuk bleed 2 mm di ujung Bintang. Akibatnya bisa tercetak di stiker tetangga. Sekarang `Sticker.Grow` memakai sambungan bulat, jadi jaraknya tepat 1,00, 1,00, dan 2,00 mm. Selfcheck S16 lulus (CUT-18). Hasil validasi sama persis dengan 2026-10-26. [Bukti selfcheck](../../Printech-TestData/04-evidence/2026-10-27-selfcheck.txt), [bukti validasi](../../Printech-TestData/04-evidence/2026-10-27-validasi-garis-potong-nesting.txt).

**Uji mouse EDIT-16 mengikuti pengaturan aplikasi (2026-10-27):** aplikasi tidak menyimpan pengaturan antar-jalan, jadi `ui-editor-mouse.ps1` membaca lebar dan offset kiss dari aplikasi lalu menghitung ukuran lubang donat uji. Parameter `-Offset` mencoba offset lain. Setelah perbaikan ujung tajam, uji lulus 8/8 pada offset 2 mm dan 8/8 pada offset 3 mm. Skrip juga berhenti bila mouse disentuh selama uji. [Bukti](../../Printech-TestData/04-evidence/2026-10-27-ui-editor-mouse.txt), [bukti offset 3](../../Printech-TestData/04-evidence/2026-10-27-ui-editor-mouse-offset-3.txt).

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

Total 100 kasus: PASS 75, FAIL 3, BLOCKED 21, NOT RUN 1. Status per 2026-10-19; IMP-10, IMP-11, IMP-17, IMP-18, IMP-23, CUT-07, CUT-14, NEST-10, EDIT-12, EDIT-13, EDIT-14, HW-15, dan PERF-01 per 2026-10-21; EDIT-16 dan HW-16 per 2026-10-22; IMP-19, CUT-10, CUT-14, CUT-15, CUT-16, CUT-17, EDIT-13, EDIT-14, EDIT-15, EDIT-17, EDIT-18, EDIT-19, CUT-18, HW-15, HW-17, dan HW-18 per 2026-10-23; IMP-19, CUT-19, NEST-11, EDIT-12, EDIT-14, EDIT-16, EDIT-18, dan EDIT-20 per 2026-10-26; CUT-18, CUT-19, EDIT-14, EDIT-16, EDIT-18, dan EDIT-20 per 2026-10-27.

Kolom Sumber menunjukkan asal kriterianya:

- **Dok:** sudah tertulis di PRD, dokumen uji, atau spesifikasi leader.
- **Kode:** perilaku yang ada di kode tetapi belum tertulis di dokumen.
- **Kriteria B / Permintaan user:** berasal dari rencana validasi yang dikirim user, belum masuk dokumen resmi.
- **Usul:** kriteria baru yang masih perlu disetujui.

Lembar kerja yang sama, untuk diisi saat menguji: [e2e-test-results-template.csv](e2e-test-results-template.csv).

### Import

| ID | Tingkat | Input | Langkah | Hasil benar | Bukti | Sumber | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| IMP-01 | Integrasi + Otomatis | pdf-single-page.pdf | Tambah file; baca panel Desain | Vektor; artwork 60,0 × 30,0 mm (±0,2); garis kiss 64 × 34 mm (±0,4) | verifikasi-loader.txt; selfcheck logo.pdf (SelfCheck.cs:68-71) | Dok: PRD F1, F2. Toleransi: Usul | PASS |
| IMP-02 | Integrasi + Otomatis | pdf-multi-page.pdf, pdf-irregular-shapes.pdf | Tambah file | 3 dan 4 desain bernama "… p1" sampai "… pN"; ukuran per halaman sesuai expected-results.md | verifikasi-loader.txt; selfcheck multi.pdf (SelfCheck.cs:460) | Dok: PRD F1 | PASS |
| IMP-03 | Integrasi + Otomatis | ai-compatible.ai | Tambah file | Vektor 50,0 × 25,0 mm | verifikasi-loader.txt; selfcheck logo.ai (SelfCheck.cs:72) | Dok: PRD F1 | PASS |
| IMP-04 | Manual UI (otomatis UIA) | 01-input/khusus/ai-postscript.ai | ui-file-khusus.ps1 | Dialog "Gagal memuat" dengan pesan; aplikasi tidak crash; file lain tetap masuk | 04-evidence/2026-10-19-ui-file-khusus.txt | Usul | PASS — Pesan masih teknis: "PDF tidak bisa dibuka (PDFium error 3)." (D9) |
| IMP-05 | Operator/hardware | cdr-normal.cdr | Tambah file | Sama dengan pdf-single-page.pdf: vektor 60,0 × 30,0 mm | Tangkapan layar panel Desain; waktu konversi (detik) | Dok: PRD F1; UJI-STABILITAS P7 | BLOCKED — CorelDRAW tidak ada di PC pengembangan |
| IMP-06 | Otomatis + Manual UI | invalid-file.cdr | Tambah bersama file lain | Dialog "Gagal memuat" berisi pesan CorelDRAW tidak terpasang; file lain tetap masuk; tidak crash | selfcheck P1; ui-semua-input.txt | Dok: PRD F1; UJI-STABILITAS P1 | PASS |
| IMP-07 | Otomatis | Worker CDR dengan batas waktu 1 ms | selfcheck P2 | Worker dihentikan < 2 detik; tidak ada worker tersisa | selfcheck.txt P2 | Dok: PRD F1; UJI-STABILITAS P2 | PASS — Hanya jalur kode batas waktu, bukan CorelDRAW asli (lihat IMP-08) |
| IMP-08 | Operator/hardware | CDR apa pun | Tambah CDR (UJI-STABILITAS P9) | Printech tetap responsif; setelah 30 detik muncul pesan "tidak merespons dalam 30 detik"; setelah dialog ditutup, tambah ulang berhasil | Video atau tangkapan layar + catatan waktu | Dok: UJI-STABILITAS P9 | BLOCKED |
| IMP-09 | Operator/hardware | CDR yang diedit lalu ditambah ulang | Tambah CDR; ubah dan simpan CDR di CorelDRAW; tambah lagi | Desain kedua memakai isi terbaru | Tangkapan layar kedua desain | Usul | BLOCKED — Kode diperbaiki 2026-10-19 (D1): cache konversi memakai versi file (Core.cs:598-599); belum diuji dengan CorelDRAW |
| IMP-10 | Integrasi | png-transparent.png | Tambah file | Crop ke konten 50,0 × 25,0 mm; 406 dpi; lubang Ø 10 mm dapat garis kiss dalam, die menutup lubang (aturan donat 2026-10-21) | verifikasi-loader.txt; validasi A5 | Dok: PRD F2 | PASS |
| IMP-11 | Integrasi | jpg-low-resolution.jpg | Tambah file | 50,0 × 50,0 mm (±0,5); seluruh gambar dipakai, latar putih tidak dibuang (masukan leader 2026-10-21) | verifikasi-loader.txt; validasi B-ui (2026-10-21) | Dok: PRD F2 | PASS — ±122 dpi sejak gambar dipakai utuh (sebelumnya ±102) |
| IMP-12 | Integrasi + Otomatis | svg-vector.svg | Tambah file | 50,0 × 25,0 mm; ±305 dpi; ditempel sebagai gambar (batasan PRD) | verifikasi-loader.txt; selfcheck badge.svg | Dok: PRD F1 + batasan SVG (PRD §8) | PASS |
| IMP-13 | Integrasi + Otomatis + Manual UI | jpg-low-resolution.jpg, png-147dpi.png, png-152dpi.png | Tambah file | ±102 dan 147 dpi merah, 152 dpi tidak; peringatan "2 desain resolusi rendah" | verifikasi-loader.txt; ui-semua-input.txt; SelfCheck.cs:100-103 | Dok: PRD F12 (ambang 150 dpi masih tebakan, PRD §8) | PASS — Temuan D6: 149,5–149,99 dpi tampil "150 dpi" berwarna merah (Core.cs:49-50) |
| IMP-14 | Integrasi + Otomatis | pdf-large-a3.pdf, jpg-large-24mp.jpg | Tambah file | Dimuat < 5 detik tanpa crash; A3 280 × 420 mm (±0,3) | verifikasi-loader.txt; selfcheck S9 | Dok: UJI-GARIS-POTONG S9. ±0,3: Usul | PASS — Halaman > 400 mm dirender maksimal 4000 px (Core.cs:304-311), ukuran tampil +0,2 mm |
| IMP-15 | Integrasi + Manual UI | pdf-oversize.pdf | Tambah file | Tidak dipasang; peringatan "1 lebih besar dari kertas"; aplikasi tetap jalan | ui-semua-input.txt; validasi B1 | Usul (perilaku kode, belum di PRD) | PASS |
| IMP-16 | Integrasi + Manual UI | pdf-rotated.pdf | Tambah file | Ditempel sebagai gambar ±254 dpi, 30 × 60 mm; peringatan "1 PDF ditempel sebagai gambar…" | validasi X3; ui-semua-input.txt | Dok: PRD §8 (batasan + peringatan) | PASS — Peringatan ditambahkan 2026-10-18, belum di-commit |
| IMP-17 | Integrasi + Manual UI | 01-input/khusus/pdf-encrypted.pdf | Loader.LoadAll lalu Export.Pdf; UI: exe dengan file sebagai argumen; validasi X3 | Dimuat (vektor bila PDFsharp bisa menempel; bila tidak, gambar + peringatan); export tetap jadi tanpa exception | 04-evidence/2026-10-21-pdf-terenkripsi.txt, -ui.png | Dok: PRD §8 | PASS — D8 diperbaiki 2026-10-21: dimuat sebagai gambar 60 × 30 mm + peringatan, export jadi. Sebelumnya FAIL: dimuat vektor, export melempar exception |
| IMP-18 | Integrasi (probe) | PDF berisi kotak putih + logo (kotak memenuhi halaman / tidak) | Loader.LoadAll | Kotak putih dipertahankan, garis mengelilingi kotak (keputusan 2026-10-21: latar tidak dibuang otomatis) | 04-evidence/2026-10-21-d7-kotak-putih.txt | Masukan leader 2026-10-21; PRD §10 butir 9 | PASS — Halaman putih penuh 100 × 100 mm, kotak 84 × 84 mm: keduanya kotak. Sebelumnya halaman putih penuh mengikuti logo (D7) |
| IMP-19 | Manual UI | Semua file 01-input sekaligus | Jalankan exe dengan semua file sebagai argumen | 19 desain; "18 stiker di 2 lembar."; 3 peringatan; "54% · sisa 46%"; judul lembar 4 pcs 90% dan 14 pcs 17% (tanda registrasi aktif sejak 2026-10-26; sebelumnya 92% dan 16%) | ui-semua-input.txt; 04-evidence/2026-10-26-validasi-garis-potong-nesting.txt | Usul (angka dari validasi B-ui) | PASS — Diulang 2026-10-26 (ui-semua-input.ps1): status, peringatan, dan 54% sama; lembar 1 4 pcs 90% dan lembar 2 14 pcs 17%, sama dengan validasi B-ui, karena area 6 mm di sekitar tanda registrasi dikosongkan; RAM 81 MB |
| IMP-20 | Integrasi | 01-input dan 01-input/customer | powershell -ExecutionPolicy Bypass -File cek-original.ps1 | Semua baris "sama"; exit code 0 | Output cek-original | Permintaan user: file original tidak berubah | PASS — 15/15 |
| IMP-21 | Manual UI (otomatis UIA) | 01-input/khusus/invalid-file.png, .jpg, .svg | ui-file-khusus.ps1 | Dialog "Gagal memuat"; aplikasi tidak crash; file lain tetap masuk | 04-evidence/2026-10-19-ui-file-khusus.txt | Usul | PASS — Pesan teknis berbahasa Inggris, misalnya "No imaging component suitable to complete this operation was found." (D9) |
| IMP-22 | Manual UI | File customer di 01-input/customer | Tambah file; bandingkan dengan hasil-benar.md | Sesuai hasil-benar.md | 04-evidence/customer/ | Usul | NOT RUN — Belum ada file customer |
| IMP-23 | Integrasi + Manual UI | 01-input/latar/png-latar-abu-muda.png, png-latar-pink-pastel.png, png-latar-putih.png | Loader + Export.Pdf, warna latar diukur di render MuPDF; UI: exe dengan ketiga file sebagai argumen | Semua latar utuh di layar dan PDF, termasuk putih (sejak 2026-10-21); garis kiss mengelilingi kotak gambar | 04-evidence/2026-10-21-png-latar.txt, -sebelum.png, -sesudah.png, -ui.png | Temuan tim 2026-10-21 (D10) | PASS — Sebelum perbaikan FAIL: latar tinggal 5 % dan logo ±4× lebih besar. Diperbaiki 2026-10-21 (Loader.WhiteBorder) |

### Garis potong

| ID | Tingkat | Input | Langkah | Hasil benar | Bukti | Sumber | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| CUT-01 | Integrasi | Lingkaran, persegi, AI, SVG, PNG, JPG | validasi.py A1 | Median jarak garis ke artwork = offset ±0,25 mm; jarak terjauh ≤ offset + 0,5 mm | Laporan validasi A1 | Dok: PRD F3. Toleransi: Usul | PASS — Median − offset −0,19 s.d. +0,02 |
| CUT-02 | Integrasi + Otomatis | png-rough-edge.png, bintang, rough.jpg | validasi.py A2a; selfcheck S5 | Panjang garis selalu turun seiring Halus naik; node pada Halus 1 dan 2 < Halus 0 | Laporan A2a; selfcheck S5; validasi-halus.png | Dok: PRD F16; UJI-GARIS-POTONG S5 | PASS |
| CUT-03 | Integrasi + Otomatis | rough.jpg (S1), png-rough-edge.png (A2b) | selfcheck S1; validasi.py A2b | Node turun ≥ 60% dari Halus 0 ke Halus 1 | selfcheck S1; laporan A2b | Dok: PRD §8 (target 60–80%); S1 | FAIL — S1 lulus (74%); A2b 54%. Perlu keputusan target (D5) |
| CUT-04 | Integrasi + Otomatis | Semua desain uji | validasi.py A3; selfcheck S6; SelfCheck.cs:58 | 0 titik garis kiss di luar garis die | Laporan A3; selfcheck S6 | Dok: PRD §6 (die membungkus kiss) | PASS |
| CUT-05 | Integrasi | Semua desain uji | validasi.py A4 | Jarak terdekat garis ke artwork ≥ offset − 0,3 mm | Laporan A4 | Dok: PRD §6 (penjaga jarak) | FAIL — 43/158 garis sedikit di bawah batas (validasi 2026-10-27), terburuk 0,13 mm; garis tetap di luar artwork (D3) |
| CUT-06 | Otomatis | Bintang, offset 0,5 dan 0 mm | selfcheck S4 | Luas artwork di luar garis < 0,5 mm² (offset 0,5) dan < 3 mm² (offset 0) | selfcheck S4 | Dok: UJI-GARIS-POTONG S4 | PASS |
| CUT-07 | Integrasi + Otomatis | png-transparent.png, cincin C, titik melingkar, donat besar dan kecil | selfcheck S11; validasi.py A5 | Rongga dari celah tertutup dan lubang kecil tidak dipotong; lubang asli yang tetap terbuka dapat garis dalam kiss dan die; die membungkus kiss | selfcheck S11; laporan A5 | Masukan leader 2026-10-21 (stiker donat); PRD §8 | PASS — S11 2026-10-21: C 1/1, titik 1/1, donat besar 2/2, donat kecil 1/1. A5: jumlah kontur dan lubang 100% benar |
| CUT-08 | Integrasi | Bintang, segitiga, logo kasar, garis tipis 0,2 mm | verify-testdata + validasi.py | Ukuran garis dalam toleransi sudut lancip; garis tipis tetap dikelilingi (lebar kiss ≥ 48,5 mm) | verifikasi-loader.txt; validasi-garis-potong.png | Usul (toleransi) | PASS |
| CUT-09 | Otomatis + Integrasi | Celah 3–3,9 mm (S7); tiga lingkaran celah 4 dan 15 mm | selfcheck S7; validasi.py A5 | Celah < 2 × (offset + halus) menyatu; celah 15 mm punya garis sendiri | selfcheck S7; laporan A5 | Dok: S7. Aturan celah: Usul | PASS |
| CUT-10 | Integrasi + Otomatis | Semua | validasi.py A5; selfcheck S2, S8 | 0 sudut tajam atau garis lurus pada desain bukan kotak; lingkaran ≤ 4 node; garis bentuk sederhana di pita [offset − 0,3; offset + 0,5] (desain kotak: batas atas offset × √2 + 0,5) | Laporan A5; selfcheck S2/S8 | Dok: PRD F16, F25 | FAIL — Yang gagal hanya batas bawah pita, yaitu CUT-05. 2026-10-23: 5 garis keluar pita (sebelumnya 27) |
| CUT-11 | Integrasi | pdf-multi-page.pdf, png-rough-edge.png | validasi.py A6 | Magenta saja / cyan saja / keduanya / pesan "Aktifkan minimal satu garis potong." | Laporan A6 | Dok: PRD F3, F9 | PASS |
| CUT-12 | Manual UI (otomatis UIA) | Desain apa pun | ui-edit-layout.ps1 | Dialog NeedCut / DieInside / BleedGap; layout tidak dibuat | 04-evidence/2026-10-19-ui-edit-layout.txt; diulang 2026-10-26-ui-edit-layout.txt (10/10) | Dok: PRD F3, F17 (MainWindow.xaml.cs:127-131) | PASS — 3 dari 3 jalan lulus |
| CUT-13 | Otomatis + Integrasi | PNG/JPG dengan bleed 1–2 mm | selfcheck bleed; validasi.py B4 | Warna meluas sampai bleed di luar kiss; vektor tanpa bleed; area bleed di dalam margin | selfcheck (SelfCheck.cs:338-352); laporan B4 | Dok: PRD F17 | PASS |
| CUT-14 | Otomatis | circle.png 40 mm, logo kasar, donat lubang Ø 21,5 mm | selfcheck S12: garis manual lurus dan kurva dimuat, node digeser, lebar 40 → 80 mm, offset kiss 2 → 1 mm, die manual rapat + kiss otomatis, export PDF, fit Bezier Simplify, membelah kurva, jadikan kurva/garis, node halus, editor tanpa mouse (tambah node di kurva, handle, tombol node) | Simpangan kurva ke garis node ≤ toleransi Simplify; node turun bila toleransi naik; garis lurus = M/L/Z dan PDF garis lurus; garis kurva = PDF kurva; membelah kurva < 0,01 mm; grid berubah saat node digeser; skala ikut lebar; offset tidak mengubah garis manual; kiss di dalam die; Simpan nonaktif saat kiss keluar die | selfcheck S12 | Masukan leader 2026-10-21 dan 2026-10-23 (node pada garis dan kurva); PRD F23 | PASS — 2026-10-23: simpangan 0,185 mm (toleransi 0,2); node logo kasar 21/17/10; PDF lurus 93 garis lurus, PDF kurva 11 kurva; membelah kurva 0,0066 mm; handle node halus segaris; donat 2 → 1 kontur kiss. Sebelumnya polygon: simpangan 0,125 mm, titik 64/32/16 |
| CUT-15 | Otomatis | Cincin berlubang sempit Ø 2,6 mm, lubang jarum Ø 1,2 mm, takik 5 mm, watermark alpha 38, pulau; gambar opak berlatar putih | selfcheck S13: SmartSelect/Classify langsung dan lewat editor tanpa mouse | Watermark tidak ikut pada Toleransi 64; takik diikuti; lubang sempit tertutup sampai diklik; klik kedua menghapus; lubang jarum ditolak; die membungkus kiss; Shift+klik menambah pulau; gambar opak memilih objek | selfcheck S13 | Spesifikasi smart select 2026-10-22; PRD F24 | PASS — Kiss kiri 8,16 mm (Toleransi 64) / -1,87 mm (16); kontur 1, +lubang 2 (garis dalam 0,88 mm, die 1, kiss di luar die 0,00 mm2), klik lubang 2x 1, +pulau 2; lubang jarum ditolak; takik diikuti (otomatis menutupnya); opak Toleransi 32, kiri 8,03 mm; editor 1/2/1/2/3 kontur. Ulang 2026-10-23 dengan garis kurva: tepi kiri 8,20 mm, garis dalam 1,00 mm |
| CUT-16 | Otomatis + Integrasi | Gambar opak, PNG kotak bermargin transparan, JPG opak, PDF kotak (01-input: 4 desain) | selfcheck S14; validasi.py A7 (deteksi kotak sendiri dari file asli) | Garis kiss dan die = kotak artwork ± offset, 4 garis lurus, Halus tidak dipakai, die ≥ kiss + 1 px; kotak bersudut bulat tetap kurva | selfcheck S14; laporan A7 | Masukan 2026-10-23 (bentuk kotak belum otomatis); PRD F25 | PASS — S14: selisih kotak 0,001 mm; A7: 4 desain kotak, 22 garis, selisih ≤ 0,10 mm |
| CUT-17 | Otomatis | circle.png 40 mm, teks PRINTECH 60 mm | selfcheck S15: 8 preset bentuk, export, editor tanpa mouse | Artwork + (offset − 0,3) di dalam kiss; die membungkus kiss; node 4/8/4/4/3/10/4/6; ukuran bentuk terkecil −0,1 s.d. +0,7 mm; bentuk ikut offset dan lebar; node diedit = garis manual | selfcheck S15; 04-evidence/2026-10-23-bentuk-garis-potong.txt, 2026-10-23-bentuk-preset.png | Masukan 2026-10-23 (preset bentuk); PRD F26 | PASS — Lingkaran 44,5 mm (terkecil 44), segitiga 76,7 × 66,4, segi enam 51,3 × 44,4, bintang 84,3; artwork keluar 0,00 mm²; offset 3 = 46,45 mm; lebar 60 = 64,58 mm |
| CUT-18 | Otomatis | circle.png 40 mm, donat lubang Ø 21,5 mm, bentuk Bintang dan Segitiga | selfcheck S16: export PDF dirender 10 px/mm, preview JPG, editor tanpa mouse | Pita offset berwarna; diteruskan 1 mm (bleed 0) atau sejauh bleed di luar garis kiss, juga di ujung tajam (tanpa lonjakan); artwork sama; tengah lubang putih; PDF `0 1 1 0 k`; preview sama dengan PDF | selfcheck S16; 04-evidence/2026-10-23-warna-border.txt, 2026-10-27-selfcheck.txt | Masukan 2026-10-23 (offset berwarna); PRD F27 | PASS — Merah #EE1D23 di PDF, #FA0105 di preview; 1,5 mm di luar garis putih (bleed 0), merah (bleed 2). Ujung tajam (2026-10-27): menjorok 1,00 mm di ujung Bintang dan Segitiga, 2,00 mm untuk Bintang bleed 2; sebelum perbaikan 2,26, 2,00, dan 4,52 mm |
| CUT-19 | Otomatis | circle.png 40 mm | selfcheck S15: editor kiss Bintang, lalu Garis yang diedit = Die-cut, lalu Kotak; muat ulang (Bintang, Kotak) dan (Otomatis, Kotak) | Kiss 10 node, die 4 node; tombol Kotak tertandai saat die dipilih; kiss bintang + 1,9 mm di dalam die; kiss otomatis di dalam die kotak 48 × 48 mm; tanda daftar desain "Bintang · die Kotak" | selfcheck S15 | Temuan leader 2026-10-26 (kiss dan die jangan digabung); PRD F26 | PASS — Versi lama gagal: die ikut bentuk kiss. Lewat UI (ui-editor-mouse.ps1 CUT-19/CUT-19b, 2026-10-26): Info 'Kiss 10 titik · die 4 titik · Bintang · die Kotak', tombol bentuk mengikuti garis yang dipilih, Simpan memberi tanda 'Bintang · die Kotak' |

### Nesting

| ID | Tingkat | Input | Langkah | Hasil benar | Bukti | Sumber | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| NEST-01 | Integrasi + Otomatis | Skenario B-sedikit sampai B-ui | validasi.py B1; SelfCheck.cs:367 | Terpasang + tidak muat = diminta; per desain = jumlah salinan | Laporan B1 | Kriteria B (rencana validasi user) | PASS |
| NEST-02 | Integrasi + Otomatis | Sama | validasi.py B2; cek tumpang tindih SelfCheck.cs:371-375 | 0 pasangan garis terluar yang bersinggungan atau saling memuat | Laporan B2 | Dok: PRD F7 | PASS — 0 dari 997 pasangan |
| NEST-03 | Integrasi | Sama | validasi.py B3 | Jarak terkecil antar garis terluar stiker berbeda ≥ 2,0 mm | Laporan B3 | Dok: spesifikasi jarak 2 mm | PASS — Terkecil 2,50 mm |
| NEST-04 | Integrasi | Sama, termasuk bleed 1 mm | validasi.py B4 | Garis terluar ≥ 10 mm dari tepi; garis + bleed ≥ 10 mm | Laporan B4 | Dok: PRD F5 | PASS — Terkecil 10,44 mm |
| NEST-05 | Integrasi | B-rotasi + susun otomatis | validasi.py B5 | Jarak garis–artwork di PDF sama di 4 rotasi (selisih ≤ 0,02 mm) | Laporan B5 | Kriteria B (rencana validasi user) | PASS |
| NEST-06 | Integrasi + Otomatis | B-campuran, B-banyak-lembar | validasi.py B6; SelfCheck.cs:366 | Lembar ≥ batas bawah luas; tidak ada lembar kosong; stiker lembar terakhir tidak muat di lembar sebelumnya | Laporan B6 | Dok: PRD F6 | PASS |
| NEST-07 | Integrasi + Manual UI | B-ui | validasi.py B7; uji UI | Pemakaian bahan Printech = hitung ulang dari garis (±0,5 poin); angka layar = data | Laporan B7; ui-semua-input.txt | Dok: PRD F18. ±0,5: Usul | PASS |
| NEST-08 | Otomatis | 130 stiker | selfcheck P3 | 5 kali susun = 1 hasil | selfcheck P3 | Dok: UJI-STABILITAS P3 | PASS |
| NEST-09 | Otomatis | 130 stiker | selfcheck P5 | Susun < 1000 ms | selfcheck P5 | Dok: UJI-STABILITAS P5 | PASS |
| NEST-10 | Otomatis + Integrasi | Semua lembar setelah susun | Compact (Penuhi lembar); selfcheck; validasi B-campuran | Ruang kosong diisi salinan desain bergiliran; jumlah lembar tetap; 0 tabrakan; jarak dan margin tetap; isi ulang menambah 0 | selfcheck (penuhi lembar); laporan B1–B4 2026-10-21 | Masukan leader 2026-10-21 (customer bayar per lembar) | PASS — Selfcheck A4: 26 → 36 stiker, 5 desain bertambah. B-campuran: 160 → 198 stiker di 4 lembar. Pangkas tinggi lembar terakhir (roll) dihapus |
| NEST-11 | Otomatis + Integrasi | Semua desain uji (A4) dan B-ui (A3+) | selfcheck S17: susun, Penuhi lembar, snap, cek pojok; validasi B-ui dengan tanda aktif | Garis terluar stiker ≥ 6,25 mm dari garis tanda (area pindai Graphtec a = 6 mm); stiker di pojok ditandai merah hanya saat tanda aktif; tebal tanda 0,3–1,0 mm, panjang 5–20 mm | selfcheck S17; 04-evidence/2026-10-26-validasi-garis-potong-nesting.txt | Temuan leader 2026-10-26 (stiker kena tanda registrasi); Graphtec CE7000 User's Manual bab 5; PRD F28 | PASS — Susun/Penuhi lembar 9,78 mm, snap 7,50 mm; sebelum perbaikan 2,16 mm dan tebal 0,25 mm |

### Edit layout

| ID | Tingkat | Input | Langkah | Hasil benar | Bukti | Sumber | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| EDIT-01 | Manual UI (otomatis UIA) | Layout apa pun | ui-edit-layout.ps1 (langkah EDIT-01) | Pindah ke posisi kosong terdekat; tidak merah | 04-evidence/2026-10-19-ui-edit-layout.txt; diulang 2026-10-26-ui-edit-layout.txt (10/10) | Dok: PRD F7, F19 | PASS — Fungsi Nest.Snap lulus di selfcheck (SelfCheck.cs:379-390) |
| EDIT-02 | Manual UI (otomatis UIA) | Layout apa pun | ui-edit-layout.ps1 (langkah EDIT-02) | Putar 90° + snap; garis dan artwork ikut berputar | 04-evidence/2026-10-19-ui-edit-layout.txt; diulang 2026-10-26-ui-edit-layout.txt (10/10) | Dok: PRD F7, F19 | PASS — Snap putar lulus di selfcheck |
| EDIT-03 | Manual UI (otomatis UIA) | Layout apa pun | ui-edit-layout.ps1 (langkah EDIT-03) | Bergerak tanpa snap; menabrak atau keluar margin = merah + peringatan | 04-evidence/2026-10-19-ui-edit-layout.txt; diulang 2026-10-26-ui-edit-layout.txt (10/10) | Dok: PRD F7 (MainWindow.xaml.cs:640-645) | PASS |
| EDIT-04 | Manual UI (otomatis UIA) | Layout apa pun | ui-edit-layout.ps1 (langkah EDIT-04) | Stiker hilang; status dan bahan diperbarui; lembar kosong dihapus | 04-evidence/2026-10-19-ui-edit-layout.txt; diulang 2026-10-26-ui-edit-layout.txt (10/10) | Dok: PRD F7 (MainWindow.xaml.cs:389-398) | PASS |
| EDIT-05 | Manual UI (otomatis UIA) | Desain gambar | ui-edit-layout.ps1 (langkah EDIT-05) | Desain dimuat ulang dengan lebar baru; jumlah stiker = total salinan | 04-evidence/2026-10-19-ui-edit-layout.txt; diulang 2026-10-26-ui-edit-layout.txt (10/10) | Dok: PRD F2 (MainWindow.xaml.cs:338-349) | PASS |
| EDIT-06 | Manual UI (otomatis UIA) | Layout dengan edit manual | ui-edit-layout.ps1 (langkah EDIT-06) | Layout dibuat ulang; edit manual hilang (belum ada undo) | 04-evidence/2026-10-19-ui-edit-layout.txt; diulang 2026-10-26-ui-edit-layout.txt (10/10) | Dok: PRD §8 (belum ada undo) | PASS — Usul: konfirmasi sebelum menimpa edit manual |
| EDIT-07 | Manual UI (otomatis UIA) | Layout dengan stiker merah | ui-edit-layout.ps1 (langkah EDIT-07) | Dialog konfirmasi muncul; pilih Tidak = export batal | 04-evidence/2026-10-19-ui-edit-layout.txt; diulang 2026-10-26-ui-edit-layout.txt (10/10) | Kode (MainWindow.xaml.cs:425-429) | PASS |
| EDIT-08 | Manual UI (otomatis UIA) | Layout apa pun | ui-edit-layout.ps1 (langkah EDIT-08) | Layout dikosongkan lalu langsung disusun ulang di kertas baru; judul lembar menyebut A4 | 04-evidence/2026-10-19-ui-edit-layout.txt; diulang 2026-10-26-ui-edit-layout.txt (10/10) | Dok: PRD F4 | PASS — Harapan dikoreksi sesuai kode (MainWindow.xaml.cs:458-464); 3 dari 3 jalan lulus |
| EDIT-10 | Otomatis | Foto berlatar polos | Rapikan: hapus latar, penghapus klik, kikis tepi, urungkan | Latar hilang; urungkan mengembalikan; export memakai hasil Rapikan | selfcheck (SelfCheck.cs:74-94), P4, P5 | Dok: PRD F15 (fitur dibekukan) | PASS — Dialog UI tidak diuji ulang pada audit ini |
| EDIT-12 | Manual UI (otomatis UIA) | 4 desain dari 01-input | ui-undo.ps1: Penuhi lembar, menu Urungkan, Ctrl+Y, Ctrl+Z, Ctrl+Shift+Z | Jumlah stiker kembali dan maju sesuai langkah; Ctrl+Shift+Z tidak memilih alat Zoom | 04-evidence/2026-10-21-ui-undo.txt; diulang 2026-10-26-ui-undo.txt (3/3) | Masukan leader 2026-10-21 | PASS — 4 → 58 → 4 → 58 → 4 → 58 stiker |
| EDIT-11 | Manual UI (otomatis UIA) | - | ui-edit-layout.ps1 (langkah EDIT-11) | Semua teks UI dan Print Ticket berbahasa Inggris | 04-evidence/2026-10-19-ui-edit-layout.txt; diulang 2026-10-26-ui-edit-layout.txt (10/10) | Dok: PRD F10 | PASS |
| EDIT-13 | Manual UI (otomatis UIA) | png-transparent.png | ui-editor-garis.ps1: buka Edit garis potong, Simplify 0,5 mm, tombol Urungkan/Ulangi editor, Simpan, menu Edit > Urungkan | Editor menampilkan kiss dan die; Simplify mengurangi node garis yang diedit saja; Simpan memberi tanda "garis manual"; Urungkan global mengembalikan garis otomatis | 04-evidence/2026-10-23-bentuk-garis-potong.txt, 2026-10-21-editor-garis-potong.png, 2026-10-21-editor-garis-manual.png | Masukan leader 2026-10-21; PRD F23 | PASS — 2026-10-23: 8/8 langkah lulus; node kurva: kiss 14 → 11 node, die 10 tetap (polygon 2026-10-21: 31 → 22 titik) |
| EDIT-14 | Manual UI | Desain apa pun | Seret node, klik di garis atau kurva untuk menambah node, Delete, Ctrl+Z/Ctrl+Y di editor, seret node keluar kotak | Node mengikuti mouse dan berhenti di batas kotak; node baru di posisi klik tanpa mengubah bentuk; kontur 3 node terhapus bila ada kontur lain; Simpan nonaktif saat kiss keluar die | 04-evidence/2026-10-26-ui-editor-mouse.txt, 2026-10-26-editor-mouse-batas-kotak.png | Masukan leader 2026-10-21; PRD F23 | PASS — Mouse dan keyboard sungguhan (ui-editor-mouse.ps1, png-transparent.png): node bintang diseret jauh ke kanan berhenti di batas kanan kotak dan Simpan nonaktif; Ctrl+Z dan Ctrl+Y di editor; klik di tengah sisi bintang menambah 1 node di titik klik tanpa mengubah garis, Delete menghapusnya; kontur dalam garis otomatis (2 node) hilang seluruhnya setelah 1 Delete, kontur luar tetap. Logika juga lulus di selfcheck S12 (CUT-14) |
| EDIT-15 | Manual UI (otomatis UIA) | png-transparent.png | ui-editor-garis.ps1: centang Smart select, baca Toleransi awal | Kotak centang bisa dinyalakan; Toleransi awal 64 untuk PNG transparan | 04-evidence/2026-10-22-smart-select.txt | Spesifikasi smart select 2026-10-22 | PASS — EDIT-04 di skrip UI; 7/7 langkah lulus 2026-10-23 |
| EDIT-16 | Manual UI | Stiker donat berwatermark | Klik donat, klik lubang tengah, klik lagi, Shift+klik objek lain, geser Toleransi | Garis mengikuti donat tanpa watermark; garis dalam muncul lalu hilang; objek kedua ikut; Toleransi menghitung ulang | 04-evidence/2026-10-27-ui-editor-mouse.txt, 2026-10-27-ui-editor-mouse-offset-3.txt, 2026-10-27-editor-mouse-smart-offset-3.png, 2026-10-26-editor-mouse-smart-toleransi-16.png | Spesifikasi smart select 2026-10-22 | PASS — Mouse sungguhan pada gambar uji buatan ui-editor-mouse.ps1: donat dengan lubang sempit, watermark alpha 38 yang menempel, kotak terpisah. Jari-jari lubang dihitung dari lebar dan offset kiss di aplikasi, R = (offset + 1,3) / 2 mm. Lulus pada offset 2 mm (R 1,65 mm) dan offset 3 mm (R 2,15 mm; garis bergeser keluar ±1 mm). Klik donat: garis luar tanpa watermark; klik lubang: kiss 3 → 5 titik (garis dalam muncul), klik lagi: 3 titik (hilang); Shift+klik kotak: 10 titik; Toleransi 16: garis ikut membungkus watermark. Logika juga lulus di selfcheck S13 (CUT-15) dan pada file donat user lewat mesin |
| EDIT-17 | Manual UI (otomatis UIA) | png-transparent.png | ui-editor-garis.ps1 langkah EDIT-07: tombol Lingkaran, Simpan, offset kiss 3, Susun, buka editor lagi | Kiss 4 node bertanda Lingkaran; daftar desain menampilkan tanda "Lingkaran"; setelah offset diubah dan disusun ulang bentuk tetap lingkaran | 04-evidence/2026-10-23-bentuk-garis-potong.txt, 2026-10-23-editor-bentuk-lingkaran.png | Masukan 2026-10-23 (preset bentuk); PRD F26 | PASS — 8/8 langkah skrip lulus (2026-10-23) |
| EDIT-18 | Manual UI | Desain apa pun | Klik node, seret handle, Jadikan kurva/Jadikan garis, Node halus/Node sudut, pilih bentuk lain lalu edit node | Handle tampil di node terpilih; lengkung mengikuti handle; node halus menjaga handle segaris; bentuk yang diedit jadi garis manual | 04-evidence/2026-10-26-ui-editor-mouse.txt, 2026-10-26-editor-mouse-handle-halus.png | Masukan 2026-10-23; PRD F23, F26 | PASS — Mouse sungguhan: kiss Lingkaran (4 node), klik node atas: node terpilih dan 2 handle tampil; handle kanan diseret 20 px ke atas: handle kiri ikut segaris dengan panjang tetap (node halus), lengkung mengikuti handle (tangkapan layar), Info jadi garis manual; Node sudut: handle kanan diseret, handle kiri tetap; Jadikan garis: handle hilang; Jadikan kurva: handle di 1/3 tali busur. Logika juga lulus di selfcheck S12 dan S15 |
| EDIT-20 | Manual UI (mouse dan keyboard) | png-transparent.png, bentuk Bintang | ui-editor-mouse.ps1: klik 3 px dari node tanpa menggeser; tekan 4 px dari node lalu seret +30/+15 px; Ctrl+Z; klik node kiri, Shift+klik node kanan, seret 25 px, Delete | Klik saja tidak memindah node dan tidak membuat garis manual; seret menggeser node sejauh gerakan mouse (tidak melompat ke kursor); Shift+klik memilih 2 node, seret dan Delete berlaku untuk keduanya | 04-evidence/2026-10-26-ui-editor-mouse.txt, 2026-10-26-editor-mouse-shift-dua-node.png | Temuan leader 2026-10-26; PRD F23 | PASS — Mouse dan keyboard sungguhan, node dicari dari tangkapan layar: EDIT-20a/b/c lulus (klik memilih node tanpa menggesernya, bergeser +30/+15 px bukan ke kursor, 2 node bergeser lalu terhapus: kiss 10 → 8 titik). Logika juga lulus di selfcheck S12 |
| EDIT-19 | Manual UI (otomatis UIA) | png-transparent.png | ui-editor-garis.ps1 langkah EDIT-08: tombol Merah, Simpan, buka editor lagi | Kotak CMYK berisi 0 100 100 0; warna tetap setelah disimpan; lembar menampilkan border merah | 04-evidence/2026-10-23-warna-border.txt, 2026-10-23-lembar-warna-border.png | Masukan 2026-10-23; PRD F27 | PASS — 8/8 langkah skrip lulus |

### Export

| ID | Tingkat | Input | Langkah | Hasil benar | Bukti | Sumber | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| EXP-01 | Integrasi + Otomatis | Semua skenario | validasi.py X1, E1; SelfCheck.cs:408-423 | Halaman = lembar; ukuran halaman ±0,01 mm; posisi artwork ±0,05 mm; ukuran desain = file asli ±0,05 mm | Laporan X1/E1 | Dok: spesifikasi export. Toleransi: Usul | PASS |
| EXP-02 | Integrasi + Otomatis | Semua | validasi.py X2 (preflight); selfcheck S8 | Stroke hanya `0 1 0 0 K` (kiss) dan `1 0 0 0 K` (die); 0 RGB/ICC/Separation; 0 OutputIntent | Laporan X2; selfcheck S8 | Dok: PRD F9 | PASS |
| EXP-03 | Integrasi | Semua | validasi.py X2 (separasi MuPDF CMYK, tanpa antialias) | Kiss tepat C0 M100 Y0 K0; die tepat C100 M0 Y0 K0 | Laporan X2; validasi-separasi-cmyk.png | Dok: spesifikasi tahap 4 | PASS — Separasi di CorelDRAW: HW-02 |
| EXP-04 | Integrasi + Otomatis | PDF/AI | validasi.py X3; SelfCheck.cs:429 | PDF/AI = Form XObject; warna isi CMYK sama dengan file input; gambar hanya PNG/JPG/SVG dan PDF diputar | Laporan X3 | Dok: PRD F9 | PASS |
| EXP-05 | Otomatis | logo.pdf, lalu isi file diganti multi.pdf dan file dihapus | selfcheck: muat PDF; ganti isi file; muat ulang seperti Susun; tambah ulang; hapus file; export (SelfCheck.cs:466-480) | Muat ulang dan export memakai isi saat dimuat (49,5 mm, tanpa peringatan); tambah ulang memakai isi baru (28,3 mm) | selfcheck.txt baris "PDF diganti lalu dihapus setelah dimuat" | Usul | PASS — Diperbaiki 2026-10-19 (D2): PDF/AI disalin ke folder temp saat dimuat (Core.cs:115-128) |
| EXP-06 | Integrasi + Otomatis | Skenario B | validasi.py X4; SelfCheck.cs:446-450 | 1 JPEG per lembar; sisi panjang 2000 px; isi di posisi stiker sama dengan PDF (≥ 95%) | Laporan X4 | Dok: PRD F13 | PASS |
| EXP-07 | Integrasi + Otomatis | Skenario B | validasi.py X5; SelfCheck.cs:401-403 | 1 halaman A4; total lembar, total pcs, efisiensi, per lembar, per desain, pengaturan = layout | Laporan X5 | Dok: PRD F20 | PASS |
| EXP-08 | Integrasi (probe) | 22 desain dari 01-input, A3 × 12 (13 lembar, 21 desain terpasang) | Export.Ticket | Tetap 1 halaman; ada baris "… dan N lainnya" | 04-evidence/2026-10-19-print-ticket-panjang.txt; 03-exports/2026-10-19-print-ticket-13-lembar.pdf | Kode (Export.cs:57-66) | PASS — Baris "… dan 3 lainnya" (lembar) dan "… dan 1 lainnya" (desain); blok terbawah 270 mm |

### Operator/hardware

| ID | Tingkat | Input | Langkah | Hasil benar | Bukti | Sumber | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
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
| HW-12 | Operator/hardware | Zip operator | Unblock zip, jalankan, lalu --selfcheck | Aplikasi terbuka; selfcheck exit 0 di PC operator | Output selfcheck | Dok: PRD §11 | BLOCKED — Ditunda: prototipe dipresentasikan dulu dari laptop tim, belum dipasang di PC toko. Zip dari clone baru 2026-10-23: selfcheck exe hasil ekstrak zip di PC pengembang exit 0, 20/20 |
| HW-13 | Operator/hardware | Preview JPG | Kirim ke customer | Cukup jelas untuk konfirmasi | Tangkapan layar WhatsApp | Dok: PRD §10 butir 3 | BLOCKED |
| HW-14 | Operator/hardware | Print Ticket | Lampirkan ke SPK | Isinya cukup untuk kasir | Catatan operator | Dok: PRD §10 butir 11 | BLOCKED |
| HW-15 | Operator/hardware | Desain dengan garis manual (node lurus dan kurva) | Edit garis potong, export, buka di Cutting Master 5, potong | Potongan mengikuti node yang diatur; pisau mulus di setiap node | Foto hasil potong | Dok: PRD §10 butir 12 | BLOCKED |
| HW-16 | Operator/hardware | Stiker donat dengan lubang sempit hasil smart select | Smart select donat + klik lubang, export, potong | Lubang tengah terpotong rapi; lingkaran potong terkecil masih bisa dibuang | Foto hasil potong | Dok: PRD §10 butir 13 | BLOCKED — Kalibrasi 0,5 mm dan 0,3 mm di sini |
| HW-17 | Operator/hardware | Foto/JPG kotak, preset bentuk, garis kurva hasil handle | Export, buka di CorelDRAW dan Cutting Master 5, potong | Sudut kotak rapi; bentuk mengikuti preset tanpa memotong artwork; node dan kurva di CorelDRAW sama dengan editor; pisau mulus pada kurva | Foto hasil potong | Dok: PRD §10 butir 14–16 | BLOCKED — Radius kotak sudut bulat 3 mm dan rasio bintang 0,5 dikalibrasi di sini |
| HW-18 | Operator/hardware | Desain dengan warna border | Export, cetak, potong | Warna border sesuai; tidak ada tepi putih di garis potong walau pisau sedikit bergeser | Foto hasil cetak dan potong | Dok: PRD §10 butir 17 | BLOCKED — Perpanjangan 1 mm dikalibrasi di sini |

### Kinerja

| ID | Tingkat | Input | Langkah | Hasil benar | Bukti | Sumber | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| PERF-01 | Manual UI | 19 desain sekaligus (01-input) | ram-profil.ps1 | ±120 MB (spesifikasi bagian 2); beban acuan belum ditetapkan | 04-evidence/2026-10-21-ram-profil.txt | Dok: spesifikasi leader bagian 2. Beban acuan: Usul | PASS — D4 diperbaiki 2026-10-21: semua 19 desain 84 MB, JPG 24 MP 41, dua PDF besar 53 (sebelumnya 279, 226, 326). Puncak sesaat saat memuat tetap tinggi |
| PERF-02 | Manual UI | Semua file 01-input | Catat waktu sampai status muncul | Semua dimuat dan disusun < 5 detik; UI responsif | ui-semua-input.txt | Usul (S9 hanya per file) | PASS — 2,5–4,0 detik; file non-CDR dimuat di UI thread (MainWindow.xaml.cs:237) |

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
- [UJI-GARIS-POTONG.md](../UJI-GARIS-POTONG.md) S1–S16, [UJI-STABILITAS.md](../UJI-STABILITAS.md) P1–P10, dan checklist operator PRD §10 butir 1–18.
- Spesifikasi leader:
  - bagian 2: RAM ±120 MB, UI tetap responsif;
  - bagian 3: DeviceCMYK, bleed;
  - bagian 4: indikator bahan, snap, preset (dihapus 2026-10-23), Print Ticket;
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
| D1 | **Diperbaiki 2026-10-19:** cache sekarang memakai versi file ([Core.cs:598-599](../../StickerLayout/Core.cs#L598-L599)); bukti dengan CorelDRAW menunggu IMP-09. Sebelumnya: cache konversi CDR hanya memakai path file sebagai kunci. Bila CDR diedit lalu ditambah ulang di sesi yang sama, hasil konversi lama yang dipakai. | Kode | [Core.cs:599](../../StickerLayout/Core.cs#L599) | Blocker |
| D2 | **Diperbaiki 2026-10-19:** PDF/AI disalin saat dimuat ([Core.cs:115-128](../../StickerLayout/Core.cs#L115-L128)); dicek selfcheck (SelfCheck.cs:466-480). Sebelumnya: file PDF dibaca ulang saat export, sedangkan garis potong dihitung saat dimuat. Bila file diubah di antaranya, artwork dan garis bisa tidak cocok. Bila file dipindah, export memakai render layar RGB beresolusi rendah dan hanya memberi peringatan. | Kode | [Export.cs:131](../../StickerLayout/Export.cs#L131), [Export.cs:107-111](../../StickerLayout/Export.cs#L107-L111) | Mayor |
| D3 | Garis bisa sampai 0,13 mm lebih dekat dari batas offset − 0,3 mm, di sudut lancip dan pada offset kecil. Garis tetap di luar artwork. | validasi A4 | [Core.cs:465-486](../../StickerLayout/Core.cs#L465-L486) (penjaga jarak), [Core.cs:541-562](../../StickerLayout/Core.cs#L541-L562) (Potrace) | Mayor |
| D4 | **Diperbaiki 2026-10-21:** bitmap resolusi penuh hasil muat langsung dilepas setelah menyusun ([MainWindow.xaml.cs:363-366](../../StickerLayout/MainWindow.xaml.cs#L363-L366)), dan salinan layar tidak lagi memegang bitmap sumber ([Core.cs:359-372](../../StickerLayout/Core.cs#L359-L372)). RAM: semua 19 desain 279 → 84 MB, JPG 24 MP 226 → 41 MB, dua PDF besar 326 → 53 MB. Sebelumnya: GC tidak jalan setelah muat, sehingga bitmap decode/render penuh (JPG 24 MP: 96 MB native + 96 MB heap) tetap terhitung; logo kecil di halaman besar menahan render halaman penuh (±43 MB) selama desainnya ada. Puncak sesaat saat memuat masih tinggi. | [ram-profil](../../Printech-TestData/04-evidence/2026-10-21-ram-profil.txt) | [Core.cs:359-372](../../StickerLayout/Core.cs#L359-L372) | Mayor |
| D5 | Node pada logo bertepi bergelombang hanya turun 54% (target 60%). | validasi A2b | — | Minor |
| D6 | Nilai 149,5–149,99 dpi tampil sebagai "150 dpi" tetapi berwarna merah. | Kode | [Core.cs:49-50](../../StickerLayout/Core.cs#L49-L50) | Minor |
| D7 | **Diputuskan 2026-10-21:** latar tidak dibuang otomatis, jadi kotak putih di PDF selalu dipertahankan dan garis mengelilingi kotak (dicek probe, IMP-18). Sebelumnya: kotak putih yang tidak memenuhi halaman menghasilkan kotak 84 × 84 mm, sedangkan halaman putih penuh mengikuti logo (44 × 44 mm). | [d7-kotak-putih](../../Printech-TestData/04-evidence/2026-10-21-d7-kotak-putih.txt) | [Core.cs (Fg)](../../StickerLayout/Core.cs) | Mayor |
| D8 | **Diperbaiki 2026-10-21:** saat dimuat, halaman PDF dicoba ditempel seperti saat export ([Core.cs:321-328](../../StickerLayout/Core.cs#L321-L328)); bila gagal, PDF ditempel sebagai gambar dengan peringatan. Dicek validasi X3 dan UI. Sebelumnya: PDF terenkripsi (hanya kata sandi pemilik) lolos cek vektor saat dimuat, lalu `Export.Pdf` melempar exception "The archive entry was compressed using an unsupported compression method." sehingga seluruh export layout gagal. | [pdf-terenkripsi](../../Printech-TestData/04-evidence/2026-10-21-pdf-terenkripsi.txt) | [Core.cs:321-328](../../StickerLayout/Core.cs#L321-L328) | Mayor |
| D9 | Pesan error untuk file rusak masih teknis, sebagian berbahasa Inggris: "PDF tidak bisa dibuka (PDFium error 3)." untuk AI tanpa PDF-compatible; "No imaging component suitable…" untuk PNG/JPG; "Data at the root level is invalid…" untuk SVG. | `ui-file-khusus.ps1` | [Pdfium.cs:43](../../StickerLayout/Pdfium.cs#L43), [MainWindow.xaml.cs:239](../../StickerLayout/MainWindow.xaml.cs#L239) | Minor |
| D10 | **Diperbaiki 2026-10-21, lalu digantikan masukan leader hari yang sama:** latar gambar opak tidak pernah dibuang otomatis, termasuk putih ([Core.cs (Fg)](../../StickerLayout/Core.cs)); dicek selfcheck. Laporan tim awal: latar warna muda (abu 242, pink pastel) hilang dari layar dan PDF, tinggal cincin selebar offset kiss, dan lebar stiker dipakai untuk logo saja. | [png-latar](../../Printech-TestData/04-evidence/2026-10-21-png-latar.txt) | [Core.cs (Fg)](../../StickerLayout/Core.cs) | Mayor |

**Batasan yang sudah diketahui** (tercatat di PRD, bukan defect baru):

- file selain CDR dimuat di UI thread ([MainWindow.xaml.cs:237](../../StickerLayout/MainWindow.xaml.cs#L237));
- Susun otomatis menimpa edit manual; sejak 2026-10-21 bisa dibatalkan dengan undo (Ctrl+Z);
- geser dengan panah tidak di-snap ([MainWindow.xaml.cs:640-645](../../StickerLayout/MainWindow.xaml.cs#L640-L645));
- SVG selalu ditempel sebagai gambar;
- halaman PDF > 400 mm dirender maksimal 4000 px ([Core.cs:311](../../StickerLayout/Core.cs#L311)).

**Sudah diperbaiki (di-commit bersama dokumen ini):**

- penjaga jarak dibulatkan ke atas ([Core.cs:483](../../StickerLayout/Core.cs#L483));
- die minimal 1 piksel di luar kiss ([Core.cs:224](../../StickerLayout/Core.cs#L224));
- D1: cache konversi CDR dengan versi file ([Core.cs:598-599](../../StickerLayout/Core.cs#L598-L599));
- D2: salinan PDF/AI saat dimuat ([Core.cs:115-128](../../StickerLayout/Core.cs#L115-L128), [MainWindow.xaml.cs:346](../../StickerLayout/MainWindow.xaml.cs#L346));
- D8: PDF yang gagal ditempel saat dimuat jadi gambar + peringatan ([Core.cs:321-328](../../StickerLayout/Core.cs#L321-L328)), 2026-10-21;
- D4: bitmap hasil muat dilepas setelah menyusun, salinan layar mandiri ([MainWindow.xaml.cs:363-366](../../StickerLayout/MainWindow.xaml.cs#L363-L366)), 2026-10-21;
- D10: latar PNG/JPG warna muda tidak lagi dibuang (2026-10-21); sejak masukan leader tahap 1 tidak ada latar opak yang dibuang otomatis ([Core.cs:391-393](../../StickerLayout/Core.cs#L391-L393));
- peringatan PDF yang ditempel sebagai gambar ([Core.cs:32](../../StickerLayout/Core.cs#L32), [MainWindow.xaml.cs:107-108](../../StickerLayout/MainWindow.xaml.cs#L107-L108)).

## 11. Urutan perbaikan yang disarankan (berdasarkan risiko produksi)

D1 dan D2 sudah diperbaiki 2026-10-19; D4, D8, dan D10 pada 2026-10-21. Kasus tingkat Operator/hardware di luar cakupan prototipe, yang dipresentasikan dulu dari laptop tim. Berikutnya:

1. **Keputusan toleransi D3 dan D5; pesan error D9; tampilan dpi D6.**

## 12. Keputusan yang dibutuhkan

1. **A4/D3:** terima toleransi 0,15 mm dan tulis di aturan (saran), atau uji mask 10 px/mm (lebih berat, belum diuji)?
2. **A2b/D5:** target node ≥ 60% berlaku hanya untuk `rough.jpg` (S1)?
3. **Pemeriksaan otomatis yang wajib untuk rilis:** self-check saja, atau ditambah `validasi.py` exit 0 dan `ui-edit-layout.ps1` 11/11?
4. **Severity:** apakah usulan severity D1–D10 dan definisinya (bagian 1) disetujui?
5. **File customer:** di mana lokasi file customer yang sudah diizinkan untuk pengujian?
