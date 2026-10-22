param([string]$Exe, [string]$In)
# Uji UI undo/redo global (masukan leader 2026-10-21): Penuhi lembar, lalu urungkan lewat menu Edit, Ctrl+Y, Ctrl+Z,
# Ctrl+Shift+Z. Ctrl+Shift+Z tidak boleh memilih alat Zoom. Uji keyboard dilewati (NOT RUN) bila jendela Printech tidak
# bisa jadi jendela aktif, supaya tombol tidak terkirim ke aplikasi lain. Jangan sentuh mouse dan keyboard selama uji.
# Jalankan dengan Windows PowerShell 5.1: powershell -ExecutionPolicy Bypass -File ui-undo.ps1 -Exe <StickerLayout.exe> -In <folder 01-input>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices;
public static class K {
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint f, IntPtr e);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  public static void Key(byte vk, bool shift) {
    keybd_event(0x11, 0, 0, IntPtr.Zero);
    if (shift) keybd_event(0x10, 0, 0, IntPtr.Zero);
    keybd_event(vk, 0, 0, IntPtr.Zero); keybd_event(vk, 0, 2, IntPtr.Zero);
    if (shift) keybd_event(0x10, 0, 2, IntPtr.Zero);
    keybd_event(0x11, 0, 2, IntPtr.Zero);
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
function Status { (Find $AE::AutomationIdProperty 'Status').Current.Name }
function Count { if ((Status) -match '(\d+) stiker di \d+ lembar') { [int]$Matches[1] } else { -1 } }
function WaitCount($want) { for ($i = 0; $i -lt 40; $i++) { if ((Count) -eq $want) { return $true }; Start-Sleep -Milliseconds 150 }; $false }

$files = 'pdf-single-page.pdf', 'ai-compatible.ai', 'png-transparent.png', 'svg-vector.svg' | ForEach-Object { '"' + (Join-Path $In $_) + '"' }
$p = Start-Process $Exe -ArgumentList $files -PassThru
$root = $null
for ($i = 0; $i -lt 120 -and -not $root; $i++) { Start-Sleep -Milliseconds 500; $p.Refresh(); if ($p.MainWindowHandle -ne 0) { $root = $AE::FromHandle($p.MainWindowHandle) } }
$h = $p.MainWindowHandle
for ($i = 0; $i -lt 120 -and (Count) -lt 0; $i++) { Start-Sleep -Milliseconds 250 }
$n0 = Count
"awal: $(Status)"

(Find $AE::NameProperty 'Penuhi lembar').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
for ($i = 0; $i -lt 40 -and (Count) -eq $n0; $i++) { Start-Sleep -Milliseconds 150 }
$n1 = Count
Res 'UNDO-01' $(if ($n1 -gt $n0) { 'LULUS' } else { 'GAGAL' }) "Penuhi lembar: $n0 -> $n1 stiker ($(Status))"

$edit = Find $AE::NameProperty 'Edit'
$edit.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
(Find $AE::NameProperty 'Urungkan' $AE::RootElement).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
Res 'UNDO-02' $(if (WaitCount $n0) { 'LULUS' } else { 'GAGAL' }) "menu Edit > Urungkan: kembali ke $(Count) stiker (harapan $n0)"

[K]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 400
if ([K]::GetForegroundWindow() -ne $h) {
  Res 'UNDO-03' 'NOT RUN' 'jendela Printech tidak bisa jadi jendela aktif; uji Ctrl+Y/Ctrl+Z/Ctrl+Shift+Z dilewati'
} else {
  [K]::Key(0x59, $false)
  $a = WaitCount $n1; $ca = Count
  [K]::Key(0x5A, $false)
  $b = WaitCount $n0; $cb = Count
  [K]::Key(0x5A, $true)
  $c = WaitCount $n1; $cc = Count
  $zoom = (Find $AE::AutomationIdProperty 'ToolZoom').GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected
  Res 'UNDO-03' $(if ($a -and $b -and $c -and -not $zoom) { 'LULUS' } else { 'GAGAL' }) ("Ctrl+Y {0}, Ctrl+Z {1}, Ctrl+Shift+Z {2} stiker (harapan {3}/{4}/{3}); alat Zoom terpilih: {5}" -f $ca, $cb, $cc, $n1, $n0, $zoom)
}
"responding: " + $p.Responding
$p.Kill()
$bad = @($results | Where-Object { $_ -match ' GAGAL:' }).Count
"RINGKAS: {0} uji, {1} GAGAL" -f $results.Count, $bad
