param([string]$Exe, [string]$Dir)
# Uji UI: semua file 01-input/khusus sekaligus. Setiap dialog error ditutup dan isinya dicatat, lalu status dan peringatan dibaca.
# Hasil benar: 02-expected/expected-results.md bagian Kasus khusus.
# Jalankan dengan Windows PowerShell 5.1 (bukan pwsh 7): powershell -ExecutionPolicy Bypass -File ui-file-khusus.ps1 -Exe <StickerLayout.exe> -Dir <folder 01-input\khusus>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$AE = [System.Windows.Automation.AutomationElement]
$PC = [System.Windows.Automation.PropertyCondition]
$names = Get-ChildItem $Dir -File | Where-Object { $_.Extension -ne '.sha256' } | Sort-Object Name
"file: " + (($names | ForEach-Object Name) -join ', ')
$sw = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process $Exe -ArgumentList ($names | ForEach-Object { '"' + $_.FullName + '"' }) -PassThru
$byPid = $PC::new($AE::ProcessIdProperty, $p.Id)
$walk = [System.Windows.Automation.TreeWalker]::RawViewWalker
$dialogs = 0; $root = $null; $status = ''
while ($sw.Elapsed.TotalSeconds -lt 60 -and $status -notmatch 'stiker di') {
  Start-Sleep -Milliseconds 300
  foreach ($w in @($AE::RootElement.FindAll('Children', $byPid))) {
    $dlg = if ($w.Current.ClassName -eq '#32770') { $w } else { $w.FindFirst('Children', $PC::new($AE::ClassNameProperty, '#32770')) }
    if ($dlg) {
      $c = $walk.GetFirstChild($dlg); $t = @()
      while ($c) { if ($c.Current.Name -and $c.Current.Name -ne 'OK') { $t += $c.Current.Name }; $c = $walk.GetNextSibling($c) }
      $dialogs++
      "dialog {0} '{1}' ({2:N1} s): {3}" -f $dialogs, $dlg.Current.Name, $sw.Elapsed.TotalSeconds, ($t -join ' ')
      $dlg.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
      Start-Sleep -Milliseconds 300
    } elseif ($w.Current.ClassName -ne '#32770') { $root = $w }
  }
  if ($root) { $st = $root.FindFirst('Descendants', $PC::new($AE::AutomationIdProperty, 'Status')); if ($st) { $status = $st.Current.Name } }
}
"dialog ditutup: $dialogs"
"status: $status"
"peringatan: " + $root.FindFirst('Descendants', $PC::new($AE::AutomationIdProperty, 'WarnText')).Current.Name
$list = $root.FindFirst('Descendants', $PC::new($AE::AutomationIdProperty, 'List'))
"daftar desain: " + (($list.FindAll('Descendants', $PC::new($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)) | ForEach-Object { $_.Current.Name } | Where-Object { $_ }) -join ' | ')
$p.Refresh(); "responding: $($p.Responding)"
$p.Kill()
