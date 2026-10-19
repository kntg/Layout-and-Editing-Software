param([string]$Exe, [string]$In)
# Profil RAM Printech: private working set (angka Task Manager) 8 dan 20 detik setelah start, per kelompok file 01-input.
# Target spesifikasi: ±120 MB (UJI-STABILITAS P6). Jalankan: powershell -ExecutionPolicy Bypass -File ram-profil.ps1 -Exe <StickerLayout.exe> -In <folder 01-input>
function MeasureRam($label, [string[]]$files) {
  $p = if ($files) { Start-Process $Exe -ArgumentList ($files | ForEach-Object { '"' + (Join-Path $In $_) + '"' }) -PassThru } else { Start-Process $Exe -PassThru }
  $vals = foreach ($t in 8, 20) {
    Start-Sleep -Seconds ($t - $(if ($t -eq 8) { 0 } else { 8 }))
    [math]::Round((Get-CimInstance Win32_PerfFormattedData_PerfProc_Process -Filter "IDProcess=$($p.Id)").WorkingSetPrivate / 1MB)
  }
  $p.Kill(); $p.WaitForExit()
  "{0,-46} {1,4} MB (8 s)  {2,4} MB (20 s)" -f $label, $vals[0], $vals[1]
}
$all = Get-ChildItem $In -File | Where-Object { $_.Extension -in '.pdf', '.ai', '.png', '.jpg', '.svg' } | Sort-Object Name | ForEach-Object Name
MeasureRam 'tanpa file' @()
MeasureRam '4 desain kecil (single, ai, png, svg)' @('pdf-single-page.pdf', 'ai-compatible.ai', 'png-transparent.png', 'svg-vector.svg')
MeasureRam 'jpg-large-24mp.jpg saja' @('jpg-large-24mp.jpg')
MeasureRam 'pdf-large-a3.pdf + pdf-oversize.pdf' @('pdf-large-a3.pdf', 'pdf-oversize.pdf')
MeasureRam 'semua kecuali 3 file besar (16 desain)' @($all | Where-Object { $_ -notin 'jpg-large-24mp.jpg', 'pdf-large-a3.pdf', 'pdf-oversize.pdf' })
MeasureRam 'semua file 01-input (19 desain)' $all

