param([string]$Config = "Release", [string]$Repo = "$PSScriptRoot\..", [string]$Work = (Join-Path ([IO.Path]::GetTempPath()) "printech-selfcheck"))
# Jalankan self-check sesuai dokumentasi: StickerLayout.exe --selfcheck "<folder>".
# Lulus hanya bila exit code 0 DAN baris terakhir selfcheck.txt = "OK". Uji negatif harus menghasilkan exit code 1.
# Contoh: pwsh -File run-selfcheck.ps1 -Config Debug. Uji negatif 3 butuh Python dengan PyMuPDF. Exit code 1 bila ada hasil SALAH.
$ErrorActionPreference = 'Stop'
$exe = Join-Path $Repo "StickerLayout\bin\$Config\net10.0-windows\StickerLayout.exe"
if (-not (Test-Path $exe)) { throw "exe tidak ada: $exe (jalankan dotnet build -c $Config dulu)" }
"exe: $exe ($((Get-Item $exe).LastWriteTime))"

function Run([string]$name, [string]$dir, [int]$expect) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process $exe -ArgumentList "--selfcheck", "`"$dir`"" -Wait -PassThru
    $log = Join-Path $dir "selfcheck.txt"
    $lines = if (Test-Path $log) { @(Get-Content $log) } else { @() }
    $last = if ($lines.Count) { $lines[-1] } else { "(selfcheck.txt tidak ada)" }
    $sc = @($lines | Select-String '^(S|P)\d+ ')
    $ok = @($lines | Select-String '^(S|P)\d+ LULUS').Count
    $gagal = @($lines | Where-Object { $_ -match '^GAGAL: ' }) | ForEach-Object { ($_ -split "`r?`n")[0] -replace '^GAGAL: System\.\w+: ', '' }
    $pass = $p.ExitCode -eq $expect -and ($expect -ne 0 -or $last -eq 'OK')
    Write-Host ("[{0}] {1}: exit {2} (diharapkan {3}), {4}/{5} skenario LULUS, {6:N1} s, baris akhir: {7}" -f ($(if ($pass) { 'BENAR' } else { 'SALAH' })), $name, $p.ExitCode, $expect, $ok, $sc.Count, $sw.Elapsed.TotalSeconds, $last.Substring(0, [Math]::Min(110, $last.Length)))
    if ($gagal) { Write-Host ("        pesan gagal: " + ($gagal -join ' ').Substring(0, [Math]::Min(160, ($gagal -join ' ').Length))) }
    return $pass
}

$results = @()
# 1. Folder baru (self-check membuat sampel sendiri).
$t = Join-Path $Work "sc-baru"; Remove-Item $t -Recurse -Force -ErrorAction SilentlyContinue
$results += Run "folder baru" $t 0
# 2. Folder samples di repo (perintah di PRD §11).
$results += Run "folder samples (repo)" (Join-Path $Repo "samples") 0
# 3. Uji negatif: logo.pdf diganti PDF lain berukuran beda -> satu Check gagal.
$t = Join-Path $Work "sc-rusak"; Remove-Item $t -Recurse -Force -ErrorAction SilentlyContinue; New-Item -ItemType Directory $t | Out-Null
python -I -c "import fitz,sys; d=fitz.open(); p=d.new_page(width=300,height=300); p.draw_rect(fitz.Rect(50,50,250,250), color=None, fill=(0,0,1)); d.save(sys.argv[1])" (Join-Path $t "logo.pdf")
$results += Run "negatif: logo.pdf salah ukuran" $t 1
# 4. Uji negatif: folder tidak bisa dibuat (induknya sebuah file).
$f = Join-Path $Work "sc-file.txt"; Set-Content $f "bukan folder"
$results += Run "negatif: folder tidak bisa dibuat" (Join-Path $f "sub") 1
"RINGKAS: {0}/{1} hasil sesuai harapan" -f @($results | Where-Object { $_ }).Count, $results.Count
if ($results -contains $false) { exit 1 }

