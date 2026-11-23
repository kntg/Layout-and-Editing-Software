param([string]$Exe, [string]$Samples, [string]$Out, [string]$Pack = '', [string]$Layout = '', [string[]]$Only = @())
# Uji UI daftar tugas 2026-11-20 (bagian A-C) di aplikasi asli: stickerpack, pisah item, garis cross, satuan,
# crop 4 sisi di Rapikan gambar, dan kotak stiker yang mengikuti garis smart select.
# -Pack: gambar stickerpack (kotak latar gelap berisi stiker). -Layout: gambar yang sudah di-layout (latar polos atau transparan).
# Keduanya biasanya file customer: simpan di 01-input/customer (tidak ikut git). Tanpa -Pack atau -Layout, uji itu dilewati.
# Jalankan dengan Windows PowerShell 5.1 (bukan pwsh 7):
#   powershell -ExecutionPolicy Bypass -File ui-tugas-20nov.ps1 -Exe <StickerLayout.exe> -Samples <folder samples> -Out <folder hasil> [-Pack <gambar>] [-Layout <gambar>] [-Only B,A4,C2,C1,A2,A3]
# Lebar desain, menu baris desain, crop, dan smart select memakai mouse dan keyboard sungguhan: jangan sentuh keduanya selama uji.
# Exit code 0 meski ada GAGAL; baca baris RINGKAS. Tangkapan layar, file export, dan hasil.txt disimpan di -Out.
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
  [StructLayout(LayoutKind.Explicit, Size = 40)]
  struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] public ushort vk; [FieldOffset(10)] public ushort scan; [FieldOffset(12)] public uint flags; }
  [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] inputs, int size);
  // Ketik teks sebagai karakter Unicode (tidak bergantung tata letak keyboard). Struktur INPUT 40 byte = proses 64-bit.
  public static void Type(string s) {
    foreach (char ch in s) {
      SendInput(2, new[] { new INPUT { type = 1, scan = ch, flags = 4 }, new INPUT { type = 1, scan = ch, flags = 6 } }, 40);
      System.Threading.Thread.Sleep(5);
    }
  }
  public struct RECT { public int L, T, R, B; }
  public static Bitmap Shot(IntPtr h) {
    RECT r; GetWindowRect(h, out r);
    var bmp = new Bitmap(r.R - r.L, r.B - r.T);
    using (var g = Graphics.FromImage(bmp)) { var hdc = g.GetHdc(); PrintWindow(h, hdc, 2); g.ReleaseHdc(hdc); }
    return bmp;
  }
  public static void Drag(int x0, int y0, int x1, int y1) {
    SetCursorPos(x0, y0); System.Threading.Thread.Sleep(250);
    mouse_event(2, 0, 0, 0, IntPtr.Zero);
    for (int i = 1; i <= 25; i++) { SetCursorPos(x0 + (x1 - x0) * i / 25, y0 + (y1 - y0) * i / 25); System.Threading.Thread.Sleep(20); }
    System.Threading.Thread.Sleep(150);
    mouse_event(4, 0, 0, 0, IntPtr.Zero);
    System.Threading.Thread.Sleep(250);
  }
  public static void Click(int x, int y) { SetCursorPos(x, y); System.Threading.Thread.Sleep(150); mouse_event(2, 0, 0, 0, IntPtr.Zero); System.Threading.Thread.Sleep(60); mouse_event(4, 0, 0, 0, IntPtr.Zero); }
  public static void Key(byte vk, bool ctrl) {
    if (ctrl) keybd_event(0x11, 0, 0, IntPtr.Zero);
    keybd_event(vk, 0, 0, IntPtr.Zero); keybd_event(vk, 0, 2, IntPtr.Zero);
    if (ctrl) keybd_event(0x11, 0, 2, IntPtr.Zero);
    System.Threading.Thread.Sleep(80);
  }
}
"@
$AE = [System.Windows.Automation.AutomationElement]
$PC = [System.Windows.Automation.PropertyCondition]
$CT = [System.Windows.Automation.ControlType]
$dash = [char]0x2014; $times = [char]0xD7
$results = [System.Collections.Generic.List[string]]::new()
New-Item -ItemType Directory -Force $Out | Out-Null
function Res($id, $ok, $detail) { $line = "{0} {1}: {2}" -f $id, $(if ($ok) { 'LULUS' } else { 'GAGAL' }), $detail; $results.Add($line); $line }
function Find($prop, $value, $scope = $root, $tries = 40) {
  $c = $PC::new($prop, $value)
  for ($i = 0; $i -lt $tries; $i++) { $e = $scope.FindFirst('Descendants', $c); if ($e) { return $e }; Start-Sleep -Milliseconds 250 }
  throw "tidak ditemukan: $value"
}
function Id($id, $scope = $root) { Find $AE::AutomationIdProperty $id $scope }
function ByName($n, $scope = $root) { Find $AE::NameProperty $n $scope }
# Invoke hanya untuk tombol (WPF menjalankannya async). Item menu yang membuka dialog diklik dengan mouse: Invoke item menu menunggu dialognya ditutup.
function Invoke($el) { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 500 }
function Val($el) { $el.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value }
function SetVal($el, $v) { $el.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($v); Start-Sleep -Milliseconds 200 }
function Toggle($el, $on) {
  $t = $el.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
  for ($i = 0; $i -lt 3 -and (($t.Current.ToggleState -eq 'On') -ne $on); $i++) { $t.Toggle(); Start-Sleep -Milliseconds 300 }
}
function Choose($el) { $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 500 }
function Status { (Id 'Status').Current.Name }
function Warn { (Id 'WarnText').Current.Name }
function Sheets { $m = [regex]::Match((Status), 'di (\d+) lembar'); if ($m.Success) { [int]$m.Groups[1].Value } else { -1 } }
function Focus($hw = $h) { [E]::SetForegroundWindow($hw) | Out-Null; Start-Sleep -Milliseconds 400 }
function ClickEl($el) { $br = $el.Current.BoundingRectangle; [E]::Click([int]($br.X + $br.Width / 2), [int]($br.Y + $br.Height / 2)); Start-Sleep -Milliseconds 400 }
function TypeKeys($text) { foreach ($ch in $text.ToCharArray()) { [E]::Key([byte][char]$ch, $false) } }
# Tangkapan layar penuh untuk bukti, dan salinan setengah ukuran untuk dilihat cepat.
function Snap($name, $hw = $h) {
  Start-Sleep -Milliseconds 600
  $bmp = [E]::Shot($hw); $bmp.Save((Join-Path $Out "$name.png"))
  $s = New-Object System.Drawing.Bitmap $bmp, ([int]($bmp.Width / 2)), ([int]($bmp.Height / 2)); $s.Save((Join-Path $Out "$name-kecil.png")); $s.Dispose(); $bmp.Dispose()
}
function Nest { Invoke (ByName 'Susun otomatis'); Start-Sleep 2 }
function Fit { Invoke (ByName 'Pas layar'); Start-Sleep 1 }
function RowEdits { @((Id 'List').FindAll('Descendants', $PC::new($AE::ControlTypeProperty, $CT::Edit))) }
function RowTexts { @((Id 'List').FindAll('Descendants', $PC::new($AE::ControlTypeProperty, $CT::Text)) | ForEach-Object { $_.Current.Name }) }
function RowH { @(RowTexts | Where-Object { $_ -like "$times *" })[0] }
# Lebar desain pertama seperti operator: klik kotak lebar di daftar desain, Ctrl+A, ketik angka bulat, Tab.
function SetWidth($mm) { Focus; $e = (RowEdits)[0]; ClickEl $e; [E]::Key(0x41, $true); TypeKeys "$mm"; [E]::Key(0x09, $false); Start-Sleep 2 }
function SetCopies($n) { SetVal (RowEdits)[1] "$n"; Start-Sleep 1 }
function Pick($id, $item) {
  $cb = Id $id
  $cb.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep -Milliseconds 500
  $li = $cb.FindFirst('Descendants', $PC::new($AE::NameProperty, $item))
  if (-not $li) { $li = $AE::RootElement.FindFirst('Descendants', [System.Windows.Automation.AndCondition]::new($PC::new($AE::ProcessIdProperty, $p.Id), $PC::new($AE::NameProperty, $item))) }
  Choose $li
  try { $cb.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse() } catch { }
  Start-Sleep 2
}
# Jendela milik proses ini: jendela utama, dialog WPF, popup, dan kotak pesan (#32770).
function Wins { @($AE::RootElement.FindAll('Children', $PC::new($AE::ProcessIdProperty, $p.Id))) + @($root.FindAll('Children', $PC::new($AE::ControlTypeProperty, $CT::Window))) }
function Win($prefix, $seconds = 15) {
  for ($i = 0; $i -lt $seconds * 4; $i++) { foreach ($w in Wins) { if ($w.Current.Name.StartsWith($prefix)) { return $w } }; Start-Sleep -Milliseconds 250 }
  throw "jendela tidak muncul: $prefix"
}
# Kotak pesan: @(elemen, teks) atau $null. Dialog simpan file (punya kotak nama file 1001) tidak dihitung.
function Dialog($seconds = 4) {
  $walk = [System.Windows.Automation.TreeWalker]::RawViewWalker
  for ($i = 0; $i -lt $seconds * 4; $i++) {
    foreach ($w in Wins) {
      $d = if ($w.Current.ClassName -eq '#32770') { $w } else { $w.FindFirst('Children', $PC::new($AE::ClassNameProperty, '#32770')) }
      if ($d -and -not $d.FindFirst('Descendants', $PC::new($AE::AutomationIdProperty, '1001'))) {
        $c = $walk.GetFirstChild($d); $t = @()
        while ($c) { if ($c.Current.Name -and $c.Current.ClassName -ne 'Button') { $t += $c.Current.Name }; $c = $walk.GetNextSibling($c) }
        return @($d, ($t -join ' '))
      }
    }
    Start-Sleep -Milliseconds 250
  }
  $null
}
function Msg($d) { if ($d) { $d[1] } else { '' } }
function CloseDlg($d) { if ($d) { $d[0].GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close(); Start-Sleep -Milliseconds 600 } }
# Dialog simpan Windows: kotak nama file = kontrol Edit 1001. Di Windows 11 versi baru kontrolnya tampil sebagai Pane tanpa pola UIA, dan teks
# yang dikirim lewat WM_SETTEXT tidak dipakai dialog (file tersimpan dengan nama bawaan di Documents). Jadi path diketik seperti operator.
function SaveAs($path) {
  for ($i = 0; $i -lt 60; $i++) {
    foreach ($w in Wins) {
      $d = if ($w.Current.ClassName -eq '#32770') { $w } else { $w.FindFirst('Children', $PC::new($AE::ClassNameProperty, '#32770')) }
      if (-not $d) { continue }
      $box = @($d.FindAll('Descendants', $PC::new($AE::AutomationIdProperty, '1001')) | Where-Object { $_.Current.ClassName -eq 'Edit' })[0]
      if (-not $box) { continue }
      Focus ([IntPtr]$d.Current.NativeWindowHandle); ClickEl $box
      [E]::Key(0x41, $true); [E]::Type($path); Start-Sleep -Milliseconds 400
      # Enter hanya bila kotak berisi path uji persis; selain itu Batal, supaya tidak ada file tersimpan di folder lain.
      if ($box.Current.Name -ne $path) { [E]::Key(0x1B, $false); throw "isi kotak nama file tidak sesuai: '$($box.Current.Name)'" }
      [E]::Key(0x0D, $false)
      return $true
    }
    Start-Sleep -Milliseconds 250
  }
  $false
}
# Export lewat dialog Export (Ctrl+E): format = FmtPdf/FmtSvg, lalu nama file di dialog simpan.
function ExportAs($fmt, $file) {
  Focus; [E]::Key(0x45, $true)
  $ex = Win 'Export'
  Choose (Id $fmt $ex); Invoke (Id 'OkBtn' $ex)
  if (-not (SaveAs $file)) { throw 'dialog simpan tidak muncul' }
  for ($i = 0; $i -lt 120 -and -not (Test-Path $file); $i++) { Start-Sleep -Milliseconds 250 }
  Start-Sleep 1
}
# Menu ... di baris desain pertama, diklik dengan mouse. Urutan item: 0 Rapikan gambar, 1 Edit garis potong, 2 Opsi garis potong, 3 Pisah item.
function RowMenu($index) {
  Focus; Invoke (Id 'MoreBtn')
  for ($i = 0; $i -lt 20; $i++) {
    foreach ($w in @($AE::RootElement.FindAll('Children', $PC::new($AE::ProcessIdProperty, $p.Id)))) {
      $items = @($w.FindAll('Descendants', $PC::new($AE::ControlTypeProperty, $CT::MenuItem)))
      if ($items.Count -ge 4 -and @($items | Where-Object { $_.Current.Name -eq 'Pisah item' }).Count -gt 0) { ClickEl $items[$index]; return }
    }
    Start-Sleep -Milliseconds 250
  }
  throw 'menu baris desain tidak muncul'
}
function Launch($files, [switch]$NoWait) {
  $script:p = Start-Process $Exe -ArgumentList @($files | ForEach-Object { '"' + $_ + '"' }) -PassThru
  $script:root = $null
  for ($i = 0; $i -lt 120 -and -not $script:root; $i++) {
    Start-Sleep -Milliseconds 500
    $script:root = @($AE::RootElement.FindAll('Children', $PC::new($AE::ProcessIdProperty, $script:p.Id)) | Where-Object { $_.Current.ClassName -eq 'Window' -and $_.Current.Name -like 'Printech*' })[0]
  }
  if (-not $script:root) { throw 'jendela utama tidak muncul' }
  $script:h = [IntPtr]$script:root.Current.NativeWindowHandle
  # Kotak pesan yang sudah terbuka (file gagal dimuat) mengunci jendela utama; uji tetap jalan tanpa maximize.
  try { $script:root.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).SetWindowVisualState([System.Windows.Automation.WindowVisualState]::Maximized) } catch { }
  if (-not $NoWait) { for ($i = 0; $i -lt 120 -and (Status) -notmatch 'stiker di'; $i++) { Start-Sleep -Milliseconds 250 } }
  Start-Sleep 1
}
function Quit { if ($script:p -and -not $script:p.HasExited) { $script:p.Kill(); $script:p.WaitForExit(5000) | Out-Null }; $script:p = $null }
# -Only lewat -File datang sebagai satu teks "A2,A4": dipecah di koma.
function Want($k) { $o = @($Only | ForEach-Object { $_ -split ',' } | Where-Object { $_ }); $o.Count -eq 0 -or $o -contains $k }

# ---------- B: satuan ----------
if (Want 'B') { try {
  Launch @((Join-Path $Samples 'circle.png'))
  $boxes = 'GapBox', 'KissOff', 'CanvasW', 'CrossMmBox'
  function Vals { ($boxes | ForEach-Object { Val (Id $_) }) -join ' ' }
  function Unit($name) {
    (ByName 'Satuan').GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep -Milliseconds 700
    # Label satuan di panel Pilihan juga bernama "mm"/"cm": cari khusus item menu.
    $it = $AE::RootElement.FindFirst('Descendants', [System.Windows.Automation.AndCondition]::new($PC::new($AE::ProcessIdProperty, $p.Id), $PC::new($AE::NameProperty, $name), $PC::new($AE::ControlTypeProperty, $CT::MenuItem)))
    Invoke $it; Start-Sleep 1
  }
  $mm = Vals; $hmm = RowH
  Unit 'cm'; $cm = Vals; $hcm = RowH
  Nest; $dcm = Dialog 2; $stcm = Status; CloseDlg $dcm
  Unit 'px (300 ppi)'; $px = Vals
  Unit 'mm'; $back = Vals; $hback = RowH
  # Kertas bawaan A3+ (lebar 320 mm); px = mm / (25,4 / 300).
  Res 'T20-B' ($mm -eq '2 2 320 1' -and $cm -eq '0.2 0.2 32 0.1' -and $hmm -match ' mm$' -and $hcm -match "^$times [\d.]+ cm$" -and -not $dcm -and $stcm -match 'stiker di' -and $px -eq '23.62 23.62 3779.53 11.81' -and $back -eq $mm -and $hback -eq $hmm) `
    "jarak/kiss/lebar kertas/cross: mm '$mm', cm '$cm', px '$px', kembali mm '$back'; tinggi di daftar '$hmm' -> '$hcm' -> '$hback'; Susun di cm: pesan '$(Msg $dcm)', status '$stcm'"
} catch { Res 'T20-B' $false "galat: $_" } finally { Quit } }

# ---------- A4: garis cross ----------
if (Want 'A4') { try {
  Launch @((Join-Path $Samples 'logo-on-blue.jpg'))
  SetWidth 120; SetCopies 4
  Choose (Id 'RowsBox')
  $n = @{}
  foreach ($k in 1, 2) {
    Pick 'CrossBox' $(if ($k -eq 1) { 'Cross 1: tunggal' } else { 'Cross 2: ganda' })
    Nest; $w = Warn; Fit; Snap "cross$k"
    $f = Join-Path $Out "cross$k.svg"
    ExportAs 'FmtSvg' $f
    $t = [IO.File]::ReadAllText($f)
    $g = [regex]::Match($t, '<g id="cross"[^>]*>(.*?)</g>', 'Singleline')
    $n[$k] = "{0} garis, kiss-cut {1}, peringatan '{2}'" -f $(if ($g.Success) { ([regex]::Matches($g.Groups[1].Value, '<path ')).Count } else { -1 }), ($t -match 'id="kiss-cut"'), $w
  }
  # 4 stiker 120 mm dalam 2 x 2: Cross 1 = 3 + 3 garis, Cross 2 = 4 + 4 garis; garis kiss tidak ikut. Peringatan resolusi rendah boleh (gambar 300 px).
  Res 'T20-A4' ($n[1] -like '6 garis, kiss-cut False,*' -and $n[2] -like '8 garis, kiss-cut False,*' -and "$($n[1]) $($n[2])" -notmatch 'tumpang tindih') "Cross 1: $($n[1]); Cross 2: $($n[2])"
} catch { Res 'T20-A4' $false "galat: $_" } finally { Quit } }

# ---------- C2: crop 4 sisi ----------
if (Want 'C2') { try {
  Launch @((Join-Path $Samples 'logo-on-blue.jpg'))
  $h0 = RowH
  RowMenu 0
  $cw = Win 'Rapikan gambar'; $ch = [IntPtr]$cw.Current.NativeWindowHandle; Focus $ch
  Toggle (Id 'CropBox' $cw) $true
  $v = (Id 'View' $cw).Current.BoundingRectangle
  $mx = [int]($v.X + $v.Width / 2); $my = [int]($v.Y + $v.Height / 2)
  # Sisi kiri ke 25 %, sisi bawah ke 60 %, lalu pojok kanan atas ke (90 %, 10 %): sisa 65 % x 50 % = 32,5 %, terhapus 67,5 %.
  [E]::Drag([int]$v.X + 2, $my, [int]($v.X + 0.25 * $v.Width), $my)
  [E]::Drag($mx, [int]$v.Bottom - 2, $mx, [int]($v.Y + 0.6 * $v.Height))
  [E]::Drag([int]$v.Right - 2, [int]$v.Y + 2, [int]($v.X + 0.9 * $v.Width), [int]($v.Y + 0.1 * $v.Height))
  $on = (Id 'CropApply' $cw).Current.IsEnabled
  Snap 'crop-bingkai' $ch
  Invoke (Id 'CropApply' $cw); Start-Sleep 2
  $info = (Id 'Info' $cw).Current.Name
  $pct = [double]([regex]::Match($info, '(\d+)\s*%').Groups[1].Value)
  Snap 'crop-hasil' $ch
  Invoke (Id 'ApplyBtn' $cw); Start-Sleep 3
  $h1 = RowH
  Res 'T20-C2' ($on -and [Math]::Abs($pct - 67.5) -le 3 -and $h1 -ne $h0) "tombol Terapkan crop aktif $on; info '$info' (harapan +-67 %); tinggi desain '$h0' -> '$h1'"
} catch { Res 'T20-C2' $false "galat: $_" } finally { Quit } }

# ---------- C1: kotak stiker ikut garis smart select ----------
if (Want 'C1') { try {
  $img = Join-Path $Out 'dua-objek.png'
  $bmp = New-Object System.Drawing.Bitmap 600, 400, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $gr = [System.Drawing.Graphics]::FromImage($bmp); $gr.Clear([System.Drawing.Color]::Transparent)
  $gr.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 245, 140, 30))), 60, 70, 240, 240)
  $gr.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 40, 90, 200))), 420, 100, 120, 120)
  $gr.Dispose(); $bmp.Save($img, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  Launch @($img)
  SetWidth 200; SetCopies 8
  Choose (Id 'RowsBox'); Nest
  $s0 = Status; $n0 = Sheets
  RowMenu 1
  $ed = Win "Garis potong $dash"; $eh = [IntPtr]$ed.Current.NativeWindowHandle; Focus $eh
  Toggle (Id 'SmartBox' $ed) $true
  # Artwork di editor = kotak isi gambar uji (x 60..540, y 70..310); klik pusat lingkaran (180, 190).
  $a = (Id 'Art' $ed).Current.BoundingRectangle
  [E]::Click([int]($a.X + (180 - 60) / 480 * $a.Width), [int]($a.Y + (190 - 70) / 240 * $a.Height)); Start-Sleep 2
  $info = (Id 'Info' $ed).Current.Name
  Snap 'smart-editor' $eh
  Invoke (Id 'SaveBtn' $ed); Start-Sleep 3
  Nest
  $s1 = Status; $n1 = Sheets
  Fit; Snap 'smart-hasil'
  # Susunan Rapi memakai kotak stiker: kotak 200 x 100 mm (dua objek) = 3 per lembar; kotak lingkaran 100 mm = 6 per lembar.
  Res 'T20-C1' ($n1 -ge 1 -and $n1 -lt $n0) "editor: '$info'; sebelum '$s0'; sesudah smart select '$s1'"
} catch { Res 'T20-C1' $false "galat: $_" } finally { Quit } }

# ---------- A2: stickerpack ----------
if ($Pack -and (Want 'A2')) {
  try {
    Launch @($Pack)
    SetWidth 200
    RowMenu 2
    $cw = Win 'Opsi garis potong'
    Toggle (Id 'FollowBox' $cw) $false
    Toggle (Id 'DieOn' $cw) $true
    Toggle (Id 'PackBox' $cw) $true
    SetVal (Id 'KissOff' $cw) '1'; SetVal (Id 'DieOff' $cw) '0.5'
    Invoke (Id 'OkBtn' $cw); Start-Sleep 5
    $d = Dialog 3; CloseDlg $d
    $badge = @(RowTexts | Where-Object { $_ -match 'pack' })
    Fit; Snap 'pack'
    # Jumlah figur garis = jumlah perintah M di layer SVG: die 1 figur (tepi kotak), kiss satu figur per stiker.
    $f = Join-Path $Out 'pack.svg'; ExportAs 'FmtSvg' $f
    $t = [IO.File]::ReadAllText($f)
    function Figures($layer) { $g = [regex]::Match($t, '<g id="' + $layer + '"[^>]*>(.*?)</g>', 'Singleline'); if ($g.Success) { ([regex]::Matches($g.Groups[1].Value, 'M')).Count } else { -1 } }
    $kf = Figures 'kiss-cut'; $df = Figures 'die-cut'
    Res 'T20-A2' (-not $d -and $badge.Count -gt 0 -and $df -eq 1 -and $kf -gt 10) "tanda di daftar '$($badge -join ' | ')'; garis kiss $kf figur, die $df figur; pesan '$(Msg $d)'; status '$(Status)'"
  } catch { Res 'T20-A2' $false "galat: $_" } finally { Quit }
} else { "T20-A2 dilewati" }

# ---------- A3: pisah item ----------
# a: langsung dari file. b: latar dihapus dulu di Rapikan gambar (gambar tanpa transparansi), lalu dipisah.
if ($Layout -and (Want 'A3')) {
  foreach ($k in 'a', 'b') {
    try {
      Launch @($Layout)
      SetWidth 150
      if ($k -eq 'b') {
        RowMenu 0
        $cw = Win 'Rapikan gambar'; Focus ([IntPtr]$cw.Current.NativeWindowHandle)
        Invoke (ByName 'Hapus latar otomatis' $cw); Start-Sleep 3
        $info = (Id 'Info' $cw).Current.Name
        Invoke (Id 'ApplyBtn' $cw); Start-Sleep 3
      }
      Fit; Snap "pisah-$k-sebelum"
      RowMenu 3; Start-Sleep 4
      $st = Status; $m = [regex]::Match($st, 'menjadi (\d+) item')
      $w = Warn
      Fit; Snap "pisah-$k-sesudah"
      $how = if ($k -eq 'b') { "hapus latar dulu ('$info'); " } else { 'langsung; ' }
      Res "T20-A3$k" ($m.Success -and [int]$m.Groups[1].Value -ge 2 -and $w -notmatch 'tumpang tindih') "$how status '$st'; peringatan '$w' (cek pisah-$k-sesudah.png)"
    } catch { Res "T20-A3$k" $false "galat: $_" } finally { Quit }
  }
} else { "T20-A3 dilewati" }

$bad = @($results | Where-Object { $_ -match ' GAGAL:' }).Count
$sum = "RINGKAS: {0} uji, {1} GAGAL" -f $results.Count, $bad
$sum
[IO.File]::WriteAllLines((Join-Path $Out 'hasil.txt'), [string[]]($results + $sum))
