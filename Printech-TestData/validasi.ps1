param(
  [string]$Bin = "$PSScriptRoot\..\StickerLayout\bin\Release\net10.0-windows",
  [string]$Out = "$PSScriptRoot\03-exports\validasi"
)
# Menjalankan mesin Printech (Loader, Nest, Export dari build Release) pada file 01-input untuk skenario
# validasi garis potong (A-*) dan nesting (B-*). Hasil mentah: validasi.json + satu PDF export per skenario di $Out.
# Penilaian dilakukan terpisah oleh validasi.py dengan geometri asli file input, bukan dengan kode Printech.
# Kriteria: 02-expected/validasi-garis-potong-nesting.md.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
[System.Runtime.InteropServices.NativeLibrary]::Load("$Bin\runtimes\win-x64\native\pdfium.dll") | Out-Null
foreach ($d in 'BitmapToVector', 'PdfSharp', 'SharpVectors.Rendering.Wpf', 'StickerLayout') { [Reflection.Assembly]::LoadFrom("$Bin\$d.dll") | Out-Null }
New-Item -ItemType Directory -Force $Out | Out-Null
$in = "$PSScriptRoot\01-input"
$A3 = 320, 480

function Cut([double]$off, [double]$sm) { if ($off -lt 0) { [StickerLayout.Cut]::new($false, 0, 0) } else { [StickerLayout.Cut]::new($true, $off, $sm) } }
$off = Cut -1 0

# Muat file (lebar awal gambar 50 mm). $copies: nama desain -> jumlah salinan (default 1).
function Load([string[]]$files, $kiss, $die, [double]$bleed = 0, [hashtable]$copies = @{}) {
  $list = [System.Collections.Generic.List[StickerLayout.Sticker]]::new()
  foreach ($f in $files) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $got = [StickerLayout.Loader]::LoadAll("$in\$f", 50, $kiss, $die, $bleed)
    foreach ($s in $got) { $s.Copies = if ($copies.ContainsKey($s.Name)) { $copies[$s.Name] } else { 1 }; $list.Add($s); $script:ms[$s] = $sw.ElapsedMilliseconds / $got.Count }
  }
  , $list
}

$ms = @{}
$scenarios = [System.Collections.Generic.List[object]]::new()

# Lembar terakhir benar-benar dibutuhkan? Coba pindahkan stikernya ke lembar sebelumnya dengan Nest.Snap (grid
# hunian yang sama dengan susun otomatis), 4 arah rotasi. Bila semua bisa pindah, lembar terakhir tidak perlu dibuat.
function LastSheet($sheets, [double]$cw, [double]$ch, [double]$margin, [double]$gap) {
  if ($sheets.Count -lt 2) { return $null }
  $before = @(foreach ($ps in $sheets | Select-Object -SkipLast 1) { , [System.Collections.Generic.List[StickerLayout.Placed]]::new($ps) })
  $moved = 0
  foreach ($p in $sheets[$sheets.Count - 1]) {
    $ok = $false
    foreach ($ps in $before) {
      foreach ($r in 0, 90, 180, 270) {
        $q = [StickerLayout.Placed]@{ S = $p.S; Rot = $r; X = $margin; Y = $margin }
        $ps.Add($q)
        if ([StickerLayout.Nest]::Snap($ps, $q, $cw, $ch, $margin, $gap)) { $ok = $true; break }
        $ps.Remove($q) | Out-Null
      }
      if ($ok) { break }
    }
    if ($ok) { $moved++ }
  }
  [ordered]@{ stickers = $sheets[$sheets.Count - 1].Count; movable = $moved }
}
# $sheets: null = susun otomatis dengan Nest.Run; atau daftar lembar berisi Placed buatan sendiri.
# -fill: ruang kosong diisi salinan desain bergiliran seperti tombol "Penuhi lembar"; jumlah per desain ikut hasil
# (MainWindow.Compact_Click). Skenario B-* juga membuat preview JPG dan Print Ticket dengan isi pengaturan yang sama
# seperti MainWindow.Ticket_Click.
function Run([string]$name, $designs, [double]$cw, [double]$ch, [double]$margin = 10, [double]$gap = 2, $sheets = $null, [switch]$fill) {
  $tb = 0
  if (-not $sheets) { $sheets = [StickerLayout.Nest]::Run($designs, $cw, $ch, $margin, $gap, [ref]$tb) }
  if ($fill -and $sheets.Count) {
    [StickerLayout.Nest]::Fill($sheets, $designs, $cw, $ch, $margin, $gap) | Out-Null
    foreach ($d in $designs) { $n = @($sheets | ForEach-Object { $_ } | Where-Object { $_.S -eq $d }).Count; if ($n -gt 0) { $d.Copies = $n } }
  }
  $h = [System.Collections.Generic.List[double]]::new(); foreach ($s in $sheets) { $h.Add($ch) }
  $warn = [StickerLayout.Export]::Pdf("$Out\$name.pdf", $sheets, $cw, $h)
  $prev = @(); $ticket = $null; $tlines = @()
  if ($name -like 'B-*') {
    $prev = @([StickerLayout.Export]::Preview("$Out\$name-preview.jpg", $sheets, $cw, $h, $margin) | ForEach-Object { Split-Path $_ -Leaf })
    $st = $sheets[0][0].S
    if ($st.LoadedKiss.On) { $tlines += [StickerLayout.Lang]::T('TicketCut', [StickerLayout.Lang]::T('Kiss'), $st.LoadedKiss.Off, $st.LoadedKiss.Smooth) }
    if ($st.LoadedDie.On) { $tlines += [StickerLayout.Lang]::T('TicketCut', [StickerLayout.Lang]::T('Die'), $st.LoadedDie.Off, $st.LoadedDie.Smooth) }
    $tlines += [StickerLayout.Lang]::T('TicketGap', $gap, $st.LoadedBleed)
    $ticket = "$name-ticket.pdf"
    [StickerLayout.Export]::Ticket("$Out\$ticket", 'A3+', $sheets, $cw, $h, $margin, [string[]]$tlines)
  }
  $ids = @{}; foreach ($s in $designs) { $ids[$s] = $ids.Count }
  $script:scenarios.Add([ordered]@{
    name = $name; cw = $cw; ch = $ch; margin = $margin; gap = $gap; tooBig = $tb; pdf = "$name.pdf"; exportWarnings = @($warn)
    lastSheet = LastSheet $sheets $cw $ch $margin $gap
    previews = $prev; ticket = $ticket; ticketLines = @($tlines)
    designs = @($designs | ForEach-Object { [ordered]@{
      id = $ids[$_]; name = $_.Name; source = [IO.Path]::GetRelativePath($in, $_.Source); page = $_.Page; copies = $_.Copies; vector = $null -ne $_.Pdf
      artW = $_.ArtW; artH = $_.ArtH; off = $_.Off; W = $_.W; H = $_.H; full = @($_.Full.X, $_.Full.Y, $_.Full.Width, $_.Full.Height)
      kissCut = @($_.LoadedKiss.On, $_.LoadedKiss.Off, $_.LoadedKiss.Smooth); dieCut = @($_.LoadedDie.On, $_.LoadedDie.Off, $_.LoadedDie.Smooth)
      kiss = $_.Kiss; die = $_.Die; areaMm2 = $_.AreaMm2; dpi = $_.Dpi; lowDpi = $_.LowDpi; loadMs = $script:ms[$_]
      bleed = $_.LoadedBleed; bleedImage = $null -ne $_.BleedPng; bleedRect = @($_.BleedRect.X, $_.BleedRect.Y, $_.BleedRect.Width, $_.BleedRect.Height)
      pdfAsImage = $_.PdfAsImage
    } })
    sheets = @(for ($i = 0; $i -lt $sheets.Count; $i++) { $ps = $sheets[$i]; [ordered]@{
      h = $h[$i]; bad = [StickerLayout.Nest]::Check($ps, $cw, $h[$i], $margin); use = [StickerLayout.Yield]::Use($ps, $cw, $h[$i], $margin)
      placements = @($ps | ForEach-Object { [ordered]@{ d = $ids[$_.S]; x = $_.X; y = $_.Y; rot = $_.Rot } })
    } })
  })
  "{0}: {1} desain, {2} stiker di {3} lembar, tidak muat {4}" -f $name, $designs.Count, ($sheets | ForEach-Object { $_.Count } | Measure-Object -Sum).Sum, $sheets.Count, $tb
}

# A. Garis potong. Kertas 1000 x 1000 mm supaya semua desain (termasuk yang lebih besar dari A3+) ikut di-export.
$simple = 'pdf-single-page.pdf', 'pdf-multi-page.pdf', 'ai-compatible.ai', 'svg-vector.svg', 'png-152dpi.png', 'jpg-low-resolution.jpg'
$special = 'pdf-irregular-shapes.pdf', 'png-rough-edge.png', 'png-transparent.png'
$large = 'pdf-large-a3.pdf', 'pdf-oversize.pdf', 'jpg-large-24mp.jpg'
Run 'A-standar' (Load ($simple + $special + $large) (Cut 2 1) (Cut 4 2)) 1000 1000
Run 'A-offset-kecil' (Load ($simple + $special) (Cut 1 1) (Cut 3 1)) 1000 1000
Run 'A-offset-besar' (Load ($simple + $special) (Cut 3 1) (Cut 6 2)) 1000 1000
foreach ($h in 0, 0.5, 1, 2) { Run "A-halus-$h" (Load $special (Cut 2 $h) (Cut 4 $h)) 1000 1000 }
Run 'A-tak-biasa' (Load $special (Cut 2 3) (Cut 4 0)) 1000 1000
Run 'A-hanya-kiss' (Load 'pdf-multi-page.pdf', 'png-rough-edge.png' (Cut 2 1) $off) 1000 1000
Run 'A-hanya-die' (Load 'pdf-multi-page.pdf', 'png-rough-edge.png' $off (Cut 4 2)) 1000 1000
try { [StickerLayout.Loader]::LoadAll("$in\pdf-single-page.pdf", 50, $off, $off) | Out-Null; $none = 'tanpa error' } catch { $none = $_.Exception.InnerException.Message ?? $_.Exception.Message }
"A-tanpa-garis: $none"
$scenarios.Add([ordered]@{ name = 'A-tanpa-garis'; error = $none })

# B. Nesting di A3+ (margin 10 mm, jarak 2 mm).
Run 'B-sedikit' (Load 'pdf-single-page.pdf', 'png-152dpi.png' (Cut 2 1) $off 0 @{ 'pdf-single-page.pdf' = 2 }) @A3
Run 'B-campuran' (Load ($simple + $special + 'png-147dpi.png', 'jpg-large-24mp.jpg') (Cut 2 1) (Cut 4 2) 0 @{
  'pdf-single-page.pdf' = 10; 'pdf-multi-page.pdf p1' = 20; 'pdf-multi-page.pdf p2' = 10; 'pdf-multi-page.pdf p3' = 15; 'ai-compatible.ai' = 10
  'svg-vector.svg' = 10; 'png-152dpi.png' = 5; 'png-147dpi.png' = 5; 'jpg-low-resolution.jpg' = 8; 'pdf-irregular-shapes.pdf p1' = 15
  'pdf-irregular-shapes.pdf p2' = 10; 'pdf-irregular-shapes.pdf p3' = 10; 'pdf-irregular-shapes.pdf p4' = 6; 'png-rough-edge.png' = 10
  'png-transparent.png' = 10; 'jpg-large-24mp.jpg' = 6 }) @A3 -fill
Run 'B-tidak-beraturan' (Load 'pdf-irregular-shapes.pdf', 'png-rough-edge.png', 'pdf-multi-page.pdf' (Cut 2 1) $off 1 @{
  'pdf-irregular-shapes.pdf p1' = 40; 'pdf-irregular-shapes.pdf p2' = 25; 'pdf-irregular-shapes.pdf p3' = 25; 'pdf-irregular-shapes.pdf p4' = 15
  'png-rough-edge.png' = 25; 'pdf-multi-page.pdf p1' = 0; 'pdf-multi-page.pdf p2' = 0; 'pdf-multi-page.pdf p3' = 20 }) @A3
Run 'B-banyak-lembar' (Load 'pdf-large-a3.pdf', 'pdf-multi-page.pdf', 'pdf-oversize.pdf' (Cut 2 1) $off 0 @{
  'pdf-large-a3.pdf' = 2; 'pdf-multi-page.pdf p1' = 150; 'pdf-multi-page.pdf p2' = 0; 'pdf-multi-page.pdf p3' = 0 }) @A3

# Rotasi: desain tidak simetris di 4 arah, diletakkan sendiri (bukan Nest.Run), lalu dicek dan di-export.
$rd = Load 'pdf-irregular-shapes.pdf', 'png-rough-edge.png', 'pdf-multi-page.pdf' (Cut 2 1) (Cut 4 2) 1
$rotated = 'pdf-irregular-shapes.pdf p3', 'png-rough-edge.png', 'pdf-multi-page.pdf p3'
foreach ($s in $rd) { $s.Copies = if ($s.Name -in $rotated) { 4 } else { 0 } }
$sheet = [System.Collections.Generic.List[StickerLayout.Placed]]::new()
$y = 12
foreach ($s in $rd | Where-Object { $_.Name -in $rotated }) {
  $x = 12
  foreach ($r in 0, 90, 180, 270) { $sheet.Add([StickerLayout.Placed]@{ S = $s; Rot = $r; X = $x; Y = $y }); $x += 75 }
  $y += 75
}
$sheets = [System.Collections.Generic.List[System.Collections.Generic.List[StickerLayout.Placed]]]::new(); $sheets.Add($sheet)
Run 'B-rotasi' $rd @A3 -sheets $sheets

# Vektor vs gambar: PDF ber-/Rotate dan PDF terenkripsi yang gagal ditempel PDFsharp (D8) ditempel sebagai gambar
# (dengan peringatan), PDF biasa tetap vektor. Export harus tetap jadi.
Run 'X-pdf-diputar' (Load 'pdf-rotated.pdf', 'pdf-single-page.pdf', 'khusus\pdf-encrypted.pdf' (Cut 2 1) (Cut 4 2)) @A3

# Sama dengan uji UI: semua file 01-input sekaligus (urutan nama), pengaturan bawaan aplikasi (kiss 2/1, die mati).
# invalid-file.cdr tidak ikut karena gagal dimuat.
$all = Get-ChildItem $in -File | Where-Object { $_.Extension -in '.pdf', '.ai', '.png', '.jpg', '.svg' } | Sort-Object Name | ForEach-Object Name
Run 'B-ui' (Load $all (Cut 2 1) $off) @A3

$scenarios | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 "$Out\validasi.json"
"tersimpan: $Out\validasi.json"
