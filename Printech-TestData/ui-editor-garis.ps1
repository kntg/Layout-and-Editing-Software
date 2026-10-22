param([string]$Exe, [string]$In, [string]$Shots)
# Uji UI editor garis potong (masukan leader 2026-10-21) lewat UI Automation, tanpa mouse dan keyboard: die diaktifkan,
# editor dibuka dari daftar desain (kiss dan die), slider Simplify mengubah jumlah titik garis yang diedit, tombol
# Urungkan/Ulangi editor, kotak Smart select dan slider Toleransi, Simpan memberi tanda "garis manual", menu Edit > Urungkan
# mengembalikan garis otomatis. Geser, tambah, dan hapus titik serta klik smart select di kanvas butuh mouse: logikanya
# diuji selfcheck S12 dan S13.
# Jalankan dengan Windows PowerShell 5.1:
# powershell -ExecutionPolicy Bypass -File ui-editor-garis.ps1 -Exe <StickerLayout.exe> -In <folder 01-input> -Shots <folder gambar>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System; using System.Runtime.InteropServices; using System.Drawing;
public static class CapE {
  [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
  public struct RECT { public int L, T, R, B; }
  public static void Save(IntPtr h, string path) {
    RECT r; GetWindowRect(h, out r);
    using (var bmp = new Bitmap(r.R - r.L, r.B - r.T)) {
      using (var g = Graphics.FromImage(bmp)) { var hdc = g.GetHdc(); PrintWindow(h, hdc, 2); g.ReleaseHdc(hdc); }
      bmp.Save(path);
    }
  }
}
"@
$AE = [System.Windows.Automation.AutomationElement]
$PC = [System.Windows.Automation.PropertyCondition]
$results = [System.Collections.Generic.List[string]]::new()
function Res($id, $state, $detail) { $line = "$id ${state}: $detail"; $results.Add($line); $line }
function Find($prop, $value, $scope = $root) {
  $c = $PC::new($prop, $value)
  for ($i = 0; $i -lt 40; $i++) { $e = $scope.FindFirst('Descendants', $c); if ($e) { return $e }; Start-Sleep -Milliseconds 250 }
  throw "tidak ditemukan: $value"
}
function Invoke($e) { $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
function Status { (Find $AE::AutomationIdProperty 'Status').Current.Name }
# Tanda "garis manual" yang tampil di daftar desain (elemen Collapsed tidak dihitung).
function Badges { @($root.FindAll('Descendants', $PC::new($AE::NameProperty, 'garis manual')) | Where-Object { -not $_.Current.IsOffscreen -and -not $_.Current.BoundingRectangle.IsEmpty }).Count }
function Points($info) { if ($info -match 'Kiss (\d+) titik · die (\d+) titik · (.+)$') { @([int]$Matches[1], [int]$Matches[2], $Matches[3]) } else { @(-1, -1, $info) } }

$name = 'png-transparent.png'
$p = Start-Process $Exe -ArgumentList ('"' + (Join-Path $In $name) + '"') -PassThru
$root = $null
for ($i = 0; $i -lt 120 -and -not $root; $i++) { Start-Sleep -Milliseconds 500; $p.Refresh(); if ($p.MainWindowHandle -ne 0) { $root = $AE::FromHandle($p.MainWindowHandle) } }
for ($i = 0; $i -lt 120 -and (Status) -notmatch 'stiker di'; $i++) { Start-Sleep -Milliseconds 250 }
"awal: $(Status)"
# Bawaan: die mati. Diaktifkan supaya editor menampilkan kiss dan die (garis otomatis dihitung ulang saat editor dibuka).
(Find $AE::AutomationIdProperty 'DieOn').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()

Invoke (Find $AE::NameProperty 'Edit garis potong')
$title = "Garis potong — $name"
$byTitle = [System.Windows.Automation.AndCondition]::new($PC::new($AE::ProcessIdProperty, $p.Id), $PC::new($AE::NameProperty, $title))
$ed = $null
# Jendela milik (Owner) jendela utama tampil sebagai anak jendela utama di pohon UI Automation.
function Editor { $w = $AE::RootElement.FindFirst('Children', $byTitle); if (-not $w) { $w = $root.FindFirst('Children', $byTitle) }; $w }
for ($i = 0; $i -lt 60 -and -not $ed; $i++) { Start-Sleep -Milliseconds 250; $ed = Editor }
if (-not $ed) { Res 'EDIT-01' 'GAGAL' "jendela '$title' tidak muncul"; $p.Kill(); return }
Start-Sleep -Milliseconds 600
$info0 = (Find $AE::AutomationIdProperty 'Info' $ed).Current.Name
$a = Points $info0
if ($Shots) { [CapE]::Save([IntPtr]$ed.Current.NativeWindowHandle, (Join-Path $Shots 'editor-garis-potong.png')) }
Res 'EDIT-01' $(if ($a[0] -gt 0 -and $a[1] -gt 0 -and $a[2] -eq 'garis otomatis') { 'LULUS' } else { 'GAGAL' }) "editor terbuka: '$info0'"

$slider = Find $AE::AutomationIdProperty 'Eps' $ed
$slider.GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue(0.5)
Start-Sleep -Milliseconds 400
$info1 = (Find $AE::AutomationIdProperty 'Info' $ed).Current.Name
$b = Points $info1
if ($Shots) { [CapE]::Save([IntPtr]$ed.Current.NativeWindowHandle, (Join-Path $Shots 'editor-garis-simplify.png')) }
Res 'EDIT-02' $(if ($b[0] -lt $a[0] -and $b[1] -eq $a[1] -and $b[2] -eq 'garis manual') { 'LULUS' } else { 'GAGAL' }) "Simplify 0,5 mm: kiss $($a[0]) -> $($b[0]) titik, die $($a[1]) -> $($b[1]) titik, '$($b[2])'"

Invoke (Find $AE::NameProperty 'Urungkan' $ed)
Start-Sleep -Milliseconds 300
$u = Points (Find $AE::AutomationIdProperty 'Info' $ed).Current.Name
Invoke (Find $AE::NameProperty 'Ulangi' $ed)
Start-Sleep -Milliseconds 300
$r = Points (Find $AE::AutomationIdProperty 'Info' $ed).Current.Name
Res 'EDIT-03' $(if ($u[0] -eq $a[0] -and $u[2] -eq 'garis otomatis' -and $r[0] -eq $b[0] -and $r[2] -eq 'garis manual') { 'LULUS' } else { 'GAGAL' }) "tombol Urungkan: kiss $($u[0]) titik, '$($u[2])'; Ulangi: kiss $($r[0]) titik, '$($r[2])'"

$smart = Find $AE::AutomationIdProperty 'SmartBox' $ed
$tg = $smart.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
$tg.Toggle(); Start-Sleep -Milliseconds 200
$state = "$($tg.Current.ToggleState)"
$tolv = (Find $AE::AutomationIdProperty 'Tol' $ed).GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).Current.Value
$tg.Toggle()
Res 'EDIT-04' $(if ($state -eq 'On' -and $tolv -eq 64) { 'LULUS' } else { 'GAGAL' }) "Smart select: kotak centang $state, Toleransi awal $tolv (harapan On, 64 untuk PNG transparan); klik di kanvas butuh mouse (NOT RUN)"

Invoke (Find $AE::NameProperty 'Simpan' $ed)
for ($i = 0; $i -lt 60 -and (Editor); $i++) { Start-Sleep -Milliseconds 250 }
for ($i = 0; $i -lt 40 -and (Status) -notmatch 'disimpan'; $i++) { Start-Sleep -Milliseconds 250 }
$st = Status
$n1 = Badges
if ($Shots) { [CapE]::Save($p.MainWindowHandle, (Join-Path $Shots 'editor-garis-manual.png')) }
Res 'EDIT-05' $(if ($st -match 'disimpan' -and $n1 -eq 1) { 'LULUS' } else { 'GAGAL' }) "Simpan: status '$st', tanda 'garis manual' $n1"

$edit = Find $AE::NameProperty 'Edit'
$edit.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
Invoke (Find $AE::NameProperty 'Urungkan' $AE::RootElement)
for ($i = 0; $i -lt 40 -and (Badges) -ne 0; $i++) { Start-Sleep -Milliseconds 250 }
$n2 = Badges
Res 'EDIT-06' $(if ($n2 -eq 0) { 'LULUS' } else { 'GAGAL' }) "Edit > Urungkan: tanda 'garis manual' $n2 (harapan 0), status '$(Status)'"
"responding: " + $p.Responding
$p.Kill()
$bad = @($results | Where-Object { $_ -match ' GAGAL:' }).Count
"RINGKAS: {0} uji, {1} GAGAL" -f $results.Count, $bad
