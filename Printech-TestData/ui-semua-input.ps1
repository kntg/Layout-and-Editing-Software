param([string]$Exe, [string]$Data, [string]$Evidence)
# Uji UI: semua file 01-input sekaligus (urutan nama, sama dengan skenario B-ui di validasi.ps1). Mencatat dialog CDR,
# status, peringatan, pemakaian bahan, judul per lembar, waktu muat, RAM, dan responsif. Hasil benar: 02-expected/expected-results.md.
# Jalankan dengan Windows PowerShell 5.1 (bukan pwsh 7): powershell -ExecutionPolicy Bypass -File ui-semua-input.ps1 -Exe <StickerLayout.exe> -Data <folder Printech-TestData> -Evidence <folder tangkapan layar>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System; using System.Runtime.InteropServices; using System.Drawing;
public static class Cap6 {
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
$names = Get-ChildItem "$Data\01-input" -File | Where-Object { $_.Extension -in '.pdf', '.ai', '.png', '.jpg', '.svg', '.cdr' } | Sort-Object Name
"file: $($names.Count) ($(($names | ForEach-Object Name) -join ', '))"
$files = $names | ForEach-Object { '"' + $_.FullName + '"' }
$sw = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process $Exe -ArgumentList $files -PassThru
$byPid = $PC::new($AE::ProcessIdProperty, $p.Id)
$dlg = $null
while ($sw.Elapsed.TotalSeconds -lt 60 -and -not $dlg) {
  Start-Sleep -Milliseconds 200
  foreach ($w in $AE::RootElement.FindAll('Children', $byPid)) {
    if ($w.Current.ClassName -eq '#32770') { $dlg = $w; break }
    $sub = $w.FindFirst('Children', $PC::new($AE::ClassNameProperty, '#32770')); if ($sub) { $dlg = $sub; break }
  }
}
if ($dlg) {
  $walk = [System.Windows.Automation.TreeWalker]::RawViewWalker; $c = $walk.GetFirstChild($dlg); $t = @()
  while ($c) { if ($c.Current.Name -ne 'OK') { $t += $c.Current.Name }; $c = $walk.GetNextSibling($c) }
  "dialog '{0}' setelah {1:N1} s: {2}" -f $dlg.Current.Name, $sw.Elapsed.TotalSeconds, ($t -join ' ')
  $dlg.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
} else { "dialog error TIDAK muncul" }
$root = $null
while ($sw.Elapsed.TotalSeconds -lt 60 -and -not $root) {
  Start-Sleep -Milliseconds 500
  $p.Refresh(); $h = $p.MainWindowHandle
  if ($h -ne [IntPtr]::Zero) { try { $root = $AE::FromHandle($h); if ($root.Current.ClassName -eq '#32770') { $root = $null } } catch { $root = $null } }
}
function Id($id) { $root.FindFirst('Descendants', $PC::new($AE::AutomationIdProperty, $id)) }
$status = ''
while ($sw.Elapsed.TotalSeconds -lt 60 -and $status -notmatch 'stiker di') { Start-Sleep -Milliseconds 200; $status = (Id 'Status').Current.Name }
"semua file selesai dimuat dan disusun {0:N1} s setelah start" -f $sw.Elapsed.TotalSeconds
$root.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Maximized)
Start-Sleep 2
"status: $status"
"peringatan: " + (Id 'WarnText').Current.Name
"pemakaian bahan: " + (Id 'UseText').Current.Name
$heads = $root.FindAll('Descendants', $PC::new($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)) | ForEach-Object { $_.Current.Name } | Where-Object { $_ -match 'pcs / lembar' }
"judul lembar: " + ($heads -join ' | ')
$fit = $root.FindFirst('Descendants', $PC::new($AE::NameProperty, 'Pas layar')); if ($fit) { $fit.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep 1 }
$shot = Join-Path $Evidence ("{0}-semua-input-printech.png" -f (Get-Date -Format yyyy-MM-dd))
[Cap6]::Save($h, $shot); "tangkapan layar: " + (Split-Path $shot -Leaf)
Start-Sleep 3
$p.Refresh()
$pws = (Get-CimInstance Win32_PerfFormattedData_PerfProc_Process -Filter "IDProcess=$($p.Id)").WorkingSetPrivate
"RAM private working set (Task Manager): {0:N0} MB; responding: {1}" -f ($pws / 1MB), $p.Responding
$p.Kill()

