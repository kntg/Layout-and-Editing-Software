# Printech

Alat internal untuk membuat layout stiker print & cut. Operator menambahkan file dari customer. Printech lalu membuat garis kiss-cut dan die-cut secara otomatis, menyusun stiker di kertas A3+, dan meng-export PDF untuk CorelDRAW dan Cutting Master 5 (Graphtec).

**Status:** prototipe. Cakupannya automasi layout saja, dengan estimasi 2 minggu (dicatat 2026-10-17).

## Dokumentasi

- [PRD](docs/PRD.md): tujuan, fitur, keputusan, batasan, checklist uji operator, dan cara menjalankan (§11).
- [Riset](docs/RESEARCH.md): riset pendukung keputusan.
- [Uji garis potong](docs/UJI-GARIS-POTONG.md): skenario uji S1–S16 dan hasilnya.
- [Uji stabilitas](docs/UJI-STABILITAS.md): skenario uji P1–P10 (worker CDR, paralel, RAM) dan hasilnya.
- [Validasi end-to-end](docs/testing/e2e-validation.md): semua kasus uji dari import sampai potong di Graphtec, dengan status PASS/FAIL/BLOCKED/NOT RUN dan buktinya. Lembar isian: [CSV](docs/testing/e2e-test-results-template.csv).
- [Data uji](Printech-TestData/README.md): file uji dengan hasil benar yang sudah diketahui, skrip validasi, dan skrip uji UI.
- [Simulasi produksi](docs/simulasi/index.html): tampilan 3D satu lembar hasil validasi. Buka file ini di browser.
- [Spesifikasi](docs/superpowers/specs/) dan [rencana implementasi](docs/superpowers/plans/) fitur baru (editor garis potong, smart select, bentuk garis potong, warna border).

## Mulai cepat

Butuh Windows dan .NET 10 SDK.

- Jalankan aplikasi: `dotnet run --project StickerLayout`
- Cek otomatis: `StickerLayout\bin\Debug\net10.0-windows\StickerLayout.exe --selfcheck samples`

## Alat pengembang

| Perintah | Fungsi |
| --- | --- |
| `pwsh -File Printech-TestData/run-selfcheck.ps1 [-Config Debug]` | Self-check 4 kali: folder baru, folder `samples`, dan 2 uji negatif yang harus gagal. Exit code 1 bila ada hasil yang tidak sesuai harapan. |
| `pwsh Printech-TestData/validasi.ps1`, lalu `python Printech-TestData/validasi.py` | Validasi garis potong, nesting, dan export pada data uji. Rinciannya di [README data uji](Printech-TestData/README.md). |
| `python -I docs/testing/gen_e2e.py` | Membuat ulang `e2e-validation.md` dan CSV-nya dari `e2e-validation.template.md` dan daftar kasus uji di `gen_e2e.py`. |
| `npx --yes markdownlint-cli2@0.23.2 "**/*.md"` | Memeriksa semua file Markdown dengan aturan di `.markdownlint-cli2.jsonc`. |
