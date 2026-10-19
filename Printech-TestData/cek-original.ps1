param([switch]$Catat)
# Cek bahwa file original di 01-input dan 01-input/customer tidak berubah. SHA-256 tiap file dibandingkan dengan
# CHECKSUMS.sha256 di folder yang sama.
# Exit code 0 = semua sama dan tercatat; 1 = ada file yang berubah, hilang, atau belum tercatat.
# -Catat: tambahkan hash file yang belum tercatat (misalnya file customer baru). Hash yang sudah ada tidak pernah ditimpa.
# Jalankan: powershell -ExecutionPolicy Bypass -File cek-original.ps1 [-Catat]
$bad = 0
foreach ($dir in "$PSScriptRoot\01-input", "$PSScriptRoot\01-input\customer") {
  if (-not (Test-Path $dir)) { continue }
  $list = Join-Path $dir 'CHECKSUMS.sha256'
  $known = @{}
  if (Test-Path $list) { foreach ($line in Get-Content $list -Encoding UTF8) { $h, $f = $line -split ' [ *]', 2; $known[$f] = $h } }
  foreach ($file in Get-ChildItem $dir -File | Where-Object { $_.Extension -in '.pdf', '.ai', '.cdr', '.png', '.jpg', '.jpeg', '.svg' }) {
    $h = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLower()
    if (-not $known.ContainsKey($file.Name)) {
      if ($Catat) { [IO.File]::AppendAllText($list, "$h *$($file.Name)`n", [Text.UTF8Encoding]::new($false)); "dicatat         $($file.Name)" }
      else { "BELUM TERCATAT  $($file.Name) (jalankan dengan -Catat)"; $bad++ }
    }
    elseif ($known[$file.Name] -ne $h) { "BERUBAH         $($file.Name)"; $bad++ }
    else { "sama            $($file.Name)" }
  }
  foreach ($f in $known.Keys) { if (-not (Test-Path -LiteralPath (Join-Path $dir $f))) { "HILANG          $f"; $bad++ } }
}
exit [int]($bad -gt 0)
