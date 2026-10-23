param([string]$Exe, [string]$Samples, [string]$Out)
# Uji UI alur edit layout dengan mouse, keyboard, dan kontrol asli: EDIT-01..08, EDIT-11, CUT-12 di docs/testing/e2e-validation.md.
# Memakai samples/circle.png dan samples/star.jpg. Exit code 0 meski ada GAGAL; baca baris RINGKAS. Jangan sentuh mouse selama uji.
# Jalankan dengan Windows PowerShell 5.1 (bukan pwsh 7): powershell -ExecutionPolicy Bypass -File ui-edit-layout.ps1 -Exe <StickerLayout.exe> -Samples <folder samples> -Out <folder tangkapan layar>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System; using System.Runtime.InteropServices; using System.Drawing;
public static class E {
  [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint f, IntPtr e);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  public struct RECT { public int L, T, R, B; }
  public static Bitmap Shot(IntPtr h) {
    RECT r; GetWindowRect(h, out r);
    var bmp = new Bitmap(r.R - r.L, r.B - r.T);
    using (var g = Graphics.FromImage(bmp)) { var hdc = g.GetHdc(); PrintWindow(h, hdc, 2); g.ReleaseHdc(hdc); }
    return bmp;
  }
  public static int[] Find(Bitmap b, int r, int g, int bl, int tol, int maxX) {
    long sx = 0, sy = 0, n = 0;
    for (int y = 150; y < b.Height - 60; y++) for (int x = 70; x < Math.Min(maxX, b.Width); x++) {
      var c = b.GetPixel(x, y);
      if (Math.Abs(c.R - r) <= tol && Math.Abs(c.G - g) <= tol && Math.Abs(c.B - bl) <= tol) { sx += x; sy += y; n++; }
    }
    return n == 0 ? new int[] { -1, -1, 0 } : new int[] { (int)(sx / n), (int)(sy / n), (int)n };
  }
  public static void Drag(int x0, int y0, int x1, int y1) {
    SetCursorPos(x0, y0); System.Threading.Thread.Sleep(150);
    mouse_event(2, 0, 0, 0, IntPtr.Zero);
    for (int i = 1; i <= 25; i++) { SetCursorPos(x0 + (x1 - x0) * i / 25, y0 + (y1 - y0) * i / 25); System.Threading.Thread.Sleep(20); }
    System.Threading.Thread.Sleep(100);
    mouse_event(4, 0, 0, 0, IntPtr.Zero);
  }
  public static void Click(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(120); mouse_event(2, 0, 0, 0, IntPtr.Zero); System.Threading.Thread.Sleep(60); mouse_event(4, 0, 0, 0, IntPtr.Zero); }
  public static void Key(byte vk, bool shift, bool ctrl) {
    if (ctrl) keybd_event(0x11, 0, 0, IntPtr.Zero);
    if (shift) keybd_event(0x10, 0, 0, IntPtr.Zero);
    uint x = (vk >= 0x21 && vk <= 0x2E) ? 1u : 0u; // panah, Delete: tombol extended
    keybd_event(vk, 0, x, IntPtr.Zero); keybd_event(vk, 0, x | 2, IntPtr.Zero);
    if (shift) keybd_event(0x10, 0, 2, IntPtr.Zero);
    if (ctrl) keybd_event(0x11, 0, 2, IntPtr.Zero);
    System.Threading.Thread.Sleep(80);
  }
}
"@
$AE = [System.Windows.Automation.AutomationElement]
$PC = [System.Windows.Automation.PropertyCondition]
$results = [System.Collections.Generic.List[string]]::new()
function Res($id, $ok, $detail) { $line = "{0} {1}: {2}" -f $id, $(if ($ok) { 'LULUS' } else { 'GAGAL' }), $detail; $results.Add($line); $line }
function Find($prop, $value, $scope = $root) {
  $c = $PC::new($prop, $value)
  for ($i = 0; $i -lt 40; $i++) { $e = $scope.FindFirst('Descendants', $c); if ($e) { return $e }; Start-Sleep -Milliseconds 250 }
  throw "tidak ditemukan: $value"
}
function Id($id) { Find $AE::AutomationIdProperty $id }
function Text($id) { (Id $id).Current.Name }
function Val($id) { (Id $id).GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value }
function SetVal($el, $v) { $el.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($v) }
function Toggle($id, $on) {
  $t = (Id $id).GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
  for ($i = 0; $i -lt 3 -and (($t.Current.ToggleState -eq 'On') -ne $on); $i++) { $t.Toggle(); Start-Sleep -Milliseconds 200 }
}
function IsOn($id) { (Id $id).GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -eq 'On' }
function Nest() { (Find $AE::NameProperty 'Susun otomatis').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep 2 }
function Pick($id, $item) {
  $cb = Id $id
  $cb.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep -Milliseconds 500
  $li = $cb.FindFirst('Descendants', $PC::new($AE::NameProperty, $item))
  if (-not $li) { $li = $AE::RootElement.FindFirst('Descendants', [System.Windows.Automation.AndCondition]::new($PC::new($AE::ProcessIdProperty, $p.Id), $PC::new($AE::NameProperty, $item))) }
  $li.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 300
  try { $cb.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse() } catch { }
  Start-Sleep 2
}
# Dialog pesan (#32770): kembalikan @(elemen, teks) atau $null.
function Dialog($seconds = 4) {
  $byPid = $PC::new($AE::ProcessIdProperty, $p.Id); $walk = [System.Windows.Automation.TreeWalker]::RawViewWalker
  for ($i = 0; $i -lt $seconds * 4; $i++) {
    foreach ($w in @($AE::RootElement.FindAll('Children', $byPid))) {
      $d = if ($w.Current.ClassName -eq '#32770') { $w } else { $w.FindFirst('Children', $PC::new($AE::ClassNameProperty, '#32770')) }
      if ($d) { $c = $walk.GetFirstChild($d); $t = @(); while ($c) { if ($c.Current.Name -and $c.Current.ClassName -ne 'Button') { $t += $c.Current.Name }; $c = $walk.GetNextSibling($c) }; return @($d, ($t -join ' ')) }
    }
    Start-Sleep -Milliseconds 250
  }
  $null
}
function Press($dlg, $name) {
  $walk = [System.Windows.Automation.TreeWalker]::RawViewWalker; $all = @(); $q = [System.Collections.Queue]::new(); $q.Enqueue($dlg)
  while ($q.Count) { $e = $q.Dequeue(); $c = $walk.GetFirstChild($e); while ($c) { $all += $c; $q.Enqueue($c); $c = $walk.GetNextSibling($c) } }
  $btns = @($all | Where-Object { $_.Current.ClassName -eq 'Button' -or $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button })
  $b = @($btns | Where-Object { $_.Current.Name -match $name })[0]; if (-not $b) { $b = $btns[-1] }
  "tombol dialog: " + (($btns | ForEach-Object { $_.Current.Name }) -join ' / ') + " -> " + $b.Current.Name
  $br = $b.Current.BoundingRectangle; [E]::Click([int]($br.X + $br.Width / 2), [int]($br.Y + $br.Height / 2)); Start-Sleep 1
}
function Locate($r, $g, $b) { $bmp = [E]::Shot($h); $res = [E]::Find($bmp, $r, $g, $b, 30, 1500); $bmp.Dispose(); $res }
function ClickEl($el) { $br = $el.Current.BoundingRectangle; [E]::Click([int]($br.X + $br.Width / 2), [int]($br.Y + $br.Height / 2)); Start-Sleep -Milliseconds 300 }
function TypeKeys($text) { foreach ($ch in $text.ToCharArray()) { [E]::Key([byte][char]$ch, $false, $false) } }
function Selected() { [bool]$root.FindFirst('Descendants', $PC::new($AE::AutomationIdProperty, 'SelRot')) }
function SelectAt($rgb) {
  for ($t = 0; $t -lt 3; $t++) { Focus; $q = Locate $rgb[0] $rgb[1] $rgb[2]; [E]::Click($r.L + $q[0], $r.T + $q[1]); Start-Sleep 1; if (Selected) { return $true } }
  $false
}
function Focus() { [E]::SetForegroundWindow($h) | Out-Null; Start-Sleep -Milliseconds 300 }
function Snap($name) { $bmp = [E]::Shot($h); $bmp.Save("$Out\ui-edit-$name.png"); $bmp.Dispose() }

$p = Start-Process $Exe -ArgumentList ('circle.png', 'star.jpg' | ForEach-Object { '"' + (Join-Path $Samples $_) + '"' }) -PassThru
for ($i = 0; $i -lt 80; $i++) { Start-Sleep -Milliseconds 250; $p.Refresh(); if ($p.MainWindowHandle -ne 0) { break } }
Start-Sleep 4
$h = $p.MainWindowHandle; $root = $AE::FromHandle($h)
$root.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Maximized); Start-Sleep 1
(Find $AE::NameProperty 'Pas layar').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep 3
Focus
$r = New-Object E+RECT; [E]::GetWindowRect($h, [ref]$r) | Out-Null
"awal: " + (Text 'Status')

# EDIT-01: seret bintang ke area kosong (harus pindah ke sana), lalu ke atas lingkaran (snap ke tempat kosong, tanpa tabrakan)
Focus; $c = Locate 255 165 0; $s = Locate 220 20 60
"posisi awal: lingkaran ($($c[0]),$($c[1])) $($c[2]) px, bintang ($($s[0]),$($s[1])) $($s[2]) px"
[E]::Drag($r.L + $s[0], $r.T + $s[1], $r.L + $s[0] + 200, $r.T + $s[1] + 150); Start-Sleep 2
$s1 = Locate 220 20 60; $moved = [Math]::Sqrt([Math]::Pow($s1[0] - $s[0], 2) + [Math]::Pow($s1[1] - $s[1], 2))
Focus; [E]::Drag($r.L + $s1[0], $r.T + $s1[1], $r.L + $c[0], $r.T + $c[1]); Start-Sleep 2
$s2 = Locate 220 20 60; $warn = Text 'WarnText'
$d = [Math]::Sqrt([Math]::Pow($s2[0] - $c[0], 2) + [Math]::Pow($s2[1] - $c[1], 2))
Res 'EDIT-01' ($moved -gt 150 -and $warn -notmatch 'tumpang tindih' -and $d -gt 20) ("seret ke area kosong: pindah {0:N0} px (target 250); seret ke atas lingkaran: berakhir {1:N0} px dari pusatnya; peringatan '{2}'" -f $moved, $d, $warn)
Snap '01-seret'

# EDIT-02: R memutar bintang terpilih 90 derajat, tetap tanpa tabrakan
$selB = SelectAt @(220, 20, 60); $rot0 = Text 'SelRot'; [E]::Key(0x52, $false, $false); Start-Sleep 2
$rot1 = Text 'SelRot'; $warn = Text 'WarnText'
Res 'EDIT-02' ($rot1 -ne $rot0 -and $warn -notmatch 'tumpang tindih') "rotasi '$rot0' -> '$rot1'; peringatan '$warn'"

# EDIT-03: Shift+panah kiri menggeser 10 mm per tekan tanpa snap -> keluar margin = merah + peringatan
$pos0 = Text 'SelPos'
for ($i = 0; $i -lt 3; $i++) { [E]::Key(0x25, $true, $false) }
[E]::Key(0x25, $false, $false); [E]::Key(0x25, $false, $false)
Start-Sleep 2
$pos1 = Text 'SelPos'; $warn = Text 'WarnText'
$x0 = [double]($pos0 -replace '^X (\-?[\d,]+).*', '$1' -replace ',', '.'); $x1 = [double]($pos1 -replace '^X (\-?[\d,]+).*', '$1' -replace ',', '.')
Res 'EDIT-03' ([Math]::Abs(($x0 - $x1) - 32) -lt 0.01 -and $warn -match 'tumpang tindih') "3 x Shift+kiri + 2 x kiri: posisi '$pos0' -> '$pos1' (geser $($x0 - $x1) mm, harapan 32); peringatan '$warn'"
Snap '03-panah'

# EDIT-07: export saat ada stiker merah -> dialog konfirmasi; Tidak = export batal (tidak ada dialog simpan)
[E]::Key(0x45, $false, $true)
$dl = Dialog 5
if ($dl) { $q = $dl[1]; Press $dl[0] '^(No|Tidak)$'; $save = Dialog 2; if ($save) { $save[0].GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close() } }
Res 'EDIT-07' ($dl -and $q -match 'Tetap export' -and -not $save) ("dialog: '{0}'; dialog simpan setelah Tidak: {1}" -f $q, [bool]$save)

# EDIT-06: Susun otomatis (Ctrl+L) membuat layout ulang; geseran manual hilang, tanda merah hilang
Focus; [E]::Key(0x4C, $false, $true); Start-Sleep 2
$warn = Text 'WarnText'
Res 'EDIT-06' ($warn -notmatch 'tumpang tindih') "peringatan setelah Ctrl+L: '$warn'; status '$(Text 'Status')'"

# EDIT-04: pilih bintang (klik), Delete -> jumlah stiker turun
$sel = SelectAt @(220, 20, 60)
$st0 = Text 'Status'; [E]::Key(0x2E, $false, $false); Start-Sleep 2; $st1 = Text 'Status'
Res 'EDIT-04' ($sel -and $st0 -match '^2 stiker' -and $st1 -match '^1 stiker') "bintang terpilih $sel; status '$st0' -> '$st1'"

# EDIT-05: jumlah lingkaran 3, lebar bintang 80 mm, Susun -> 4 stiker, tinggi bintang ikut berubah
$list = Id 'List'
$edits = @($list.FindAll('Descendants', $PC::new($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)))
$texts = { @($list.FindAll('Descendants', $PC::new($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)) | ForEach-Object { $_.Current.Name } | Where-Object { $_ -match '^× ' }) }
$h0 = & $texts
SetVal $edits[1] '3'
ClickEl $edits[2]; [E]::Key(0x41, $false, $true); TypeKeys '80'; [E]::Key(0x09, $false, $false); Start-Sleep 1
Nest
$st = Text 'Status'; $h1 = & $texts
Res 'EDIT-05' ($st -match '^4 stiker' -and ($h1 -join ',') -ne ($h0 -join ',')) "status '$st'; tinggi desain '$($h0 -join ', ')' -> '$($h1 -join ', ')'"

# CUT-12: validasi pengaturan garis potong sebelum Susun
Toggle 'KissOn' $false; Toggle 'DieOn' $false; Nest
$a = Dialog 3; if ($a) { $a[0].GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close(); Start-Sleep 1 }
Toggle 'KissOn' $true; SetVal (Id 'KissOff') '2'; Toggle 'DieOn' $true; SetVal (Id 'DieOff') '2'; Nest
$b = Dialog 3; if ($b) { $b[0].GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close(); Start-Sleep 1 }
SetVal (Id 'DieOff') '4'; SetVal (Id 'GapBox') '2'; SetVal (Id 'BleedBox') '3'; Nest
$c3 = Dialog 3; if ($c3) { $c3[0].GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close(); Start-Sleep 1 }
SetVal (Id 'BleedBox') '0'
Res 'CUT-12' ($a -and $a[1] -match 'minimal satu garis' -and $b -and $b[1] -match 'harus lebih besar' -and $c3 -and $c3[1] -match 'Bleed tidak boleh') ("'{0}' | '{1}' | '{2}'" -f $a[1], $b[1], $c3[1])

# EDIT-08: ganti kertas -> layout dikosongkan lalu langsung disusun ulang di kertas baru (Paper_Changed)
Nest; $st0 = Text 'Status'
Pick 'PaperBox' 'A4'
$w = Val 'CanvasW'; $hh = Val 'CanvasH'; $st1 = Text 'Status'
$heads = @($root.FindAll('Descendants', $PC::new($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)) | ForEach-Object { $_.Current.Name } | Where-Object { $_ -match 'pcs / lembar' })
Res 'EDIT-08' ($w -eq '210' -and $hh -eq '297' -and $st1 -match 'stiker di' -and $heads.Count -gt 0 -and ($heads -join ' ') -match 'lembar A4') "kertas $w x $hh; status '$st0' -> '$st1'; judul lembar '$($heads -join ' | ')'"

# EDIT-11: bahasa English
$menu = Find $AE::NameProperty 'Bahasa'
$menu.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep 1
(Find $AE::AutomationIdProperty 'LangEn' $AE::RootElement).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep 2
$st = Text 'Status'; $btn = $root.FindFirst('Descendants', $PC::new($AE::NameProperty, 'Auto layout'))
Res 'EDIT-11' ($st -match 'stickers on' -and $btn) "status '$st'; tombol 'Auto layout' ada: $([bool]$btn)"
Snap '11-english'

$p.Refresh(); "responding: $($p.Responding)"
$p.Kill()
"RINGKAS: {0} dari {1} LULUS" -f @($results | Where-Object { $_ -match ' LULUS:' }).Count, $results.Count


