param([string]$Exe, [string]$In, [string]$Shots, [double]$Offset)
# Uji editor garis potong dengan mouse dan keyboard sungguhan. Node, handle, dan garis dicari dari tangkapan layar (kotak node
# putih 7 px, node terpilih berisi warna garis, handle = cincin putih bertengah gelap, garis kiss magenta), lalu dibandingkan
# sebelum dan sesudah. Jangan sentuh mouse dan keyboard selama uji (±2 menit).
# - Temuan leader 2026-10-26: kiss Bintang + die Kotak lewat "Garis yang diedit" (CUT-19, CUT-19b), klik node tanpa geser,
#   seret relatif, Shift+klik dua node lalu seret dan Delete (EDIT-20).
# - Handle kurva: handle tampil, node halus menjaga handle segaris, node sudut tidak, Jadikan garis/kurva (EDIT-18).
# - Operasi node: berhenti di batas kotak, Simpan nonaktif saat kiss keluar die, Ctrl+Z/Ctrl+Y, klik di garis menambah node
#   tanpa mengubah bentuk, kontur dalam terhapus saat sisa < 3 node (EDIT-14).
# - Smart select dengan klik di kanvas pada gambar uji buatan skrip: donat berwatermark dan kotak terpisah (EDIT-16).
# Jalankan dengan Windows PowerShell 5.1: powershell -ExecutionPolicy Bypass -File ui-editor-mouse.ps1 -Exe <StickerLayout.exe> -In <folder 01-input> -Shots <folder gambar> [-Offset <mm>]
# -Offset: offset kiss untuk bagian D (smart select); tanpa argumen ini dipakai offset bawaan aplikasi.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Drawing;
public static class M {
  [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint f, IntPtr e);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
  [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h, uint f);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
  static string Title(IntPtr h) { var t = new System.Text.StringBuilder(256); GetWindowText(h, t, 256); return "'" + t + "'"; }
  public struct RECT { public int L, T, R, B; }
  public struct POINT { public int X, Y; }
  // Jendela tujuan input. Sebelum klik atau tombol, jendela ini harus aktif dan (untuk klik) tidak tertutup jendela lain di
  // titik itu. Bila tidak, uji berhenti supaya input tidak masuk ke aplikasi lain.
  public static IntPtr Target;
  static void Guard(int x, int y, bool point) {
    for (int i = 0; i < 10; i++) {
      if (GetForegroundWindow() == Target && (!point || GetAncestor(WindowFromPoint(new POINT { X = x, Y = y }), 2) == Target)) return;
      SetForegroundWindow(Target); System.Threading.Thread.Sleep(200);
    }
    string seen = "jendela aktif " + Title(GetForegroundWindow()) + (point ? ", jendela di titik itu " + Title(GetAncestor(WindowFromPoint(new POINT { X = x, Y = y }), 2)) : "");
    throw new InvalidOperationException((point ? "titik (" + x + "," + y + ") bukan jendela editor yang aktif" : "jendela editor tidak aktif") + "; input dibatalkan (" + seen + ")");
  }
  public static Bitmap Shot(IntPtr h) {
    RECT r; GetWindowRect(h, out r);
    var bmp = new Bitmap(r.R - r.L, r.B - r.T);
    using (var g = Graphics.FromImage(bmp)) { var hdc = g.GetHdc(); PrintWindow(h, hdc, 2); g.ReleaseHdc(hdc); }
    return bmp;
  }
  // Cincin handle 1,2 px ber-antialias: ambang 190 supaya cincinnya tetap utuh (garis abu Dim 155-167 dan magenta tidak ikut).
  static bool White(Color c) { return c.R >= 190 && c.G >= 190 && c.B >= 190; }
  static bool Like(Color c, int r, int g, int b, int tol) { return Math.Abs(c.R - r) <= tol && Math.Abs(c.G - g) <= tol && Math.Abs(c.B - b) <= tol; }
  // Kotak node: komponen piksel putih (kotak penuh atau cincin) selebar 5-14 px di area (x0,y0)-(x1,y1). Pusat = tengah batasnya.
  // Jenis dari warna tengah: 0 = node (putih), 1 = node terpilih (warna garis), 2 = handle (gelap).
  public static List<int[]> Nodes(Bitmap b, int x0, int y0, int x1, int y1) {
    x0 = Math.Max(0, x0); y0 = Math.Max(0, y0); x1 = Math.Min(b.Width, x1); y1 = Math.Min(b.Height, y1);
    var seen = new bool[b.Width, b.Height]; var res = new List<int[]>();
    for (int y = y0; y < y1; y++)
      for (int x = x0; x < x1; x++) {
        if (seen[x, y] || !White(b.GetPixel(x, y))) continue;
        int minX = x, maxX = x, minY = y, maxY = y; var st = new Stack<int[]>(); st.Push(new[] { x, y }); seen[x, y] = true;
        while (st.Count > 0) {
          var q = st.Pop();
          minX = Math.Min(minX, q[0]); maxX = Math.Max(maxX, q[0]); minY = Math.Min(minY, q[1]); maxY = Math.Max(maxY, q[1]);
          foreach (var d in new[] { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } }) {
            int nx = q[0] + d[0], ny = q[1] + d[1];
            if (nx < x0 || ny < y0 || nx >= x1 || ny >= y1 || seen[nx, ny] || !White(b.GetPixel(nx, ny))) continue;
            seen[nx, ny] = true; st.Push(new[] { nx, ny });
          }
        }
        int w = maxX - minX + 1, h = maxY - minY + 1, cx = (minX + maxX) / 2, cy = (minY + maxY) / 2;
        if (w < 5 || w > 14 || h < 5 || h > 14) continue;
        var c = b.GetPixel(cx, cy);
        res.Add(new[] { cx, cy, White(c) ? 0 : (c.R + c.G + c.B < 200 ? 2 : 1) });
      }
    return res;
  }
  // Cincin handle di (x, y) ± 2 px: tengah gelap dan >= 5 piksel terang (>= 170) pada jarak 2,5-5 px. Cincin 1,2 px ber-antialias
  // sering putus, jadi handle dicek di posisi yang dihitung dari geometri, bukan dicari sebagai komponen.
  public static bool Ring(Bitmap b, int x, int y) {
    for (int dy = -2; dy <= 2; dy++)
      for (int dx = -2; dx <= 2; dx++) {
        int cx = x + dx, cy = y + dy;
        if (cx < 6 || cy < 6 || cx >= b.Width - 6 || cy >= b.Height - 6) continue;
        var c = b.GetPixel(cx, cy);
        if (c.R + c.G + c.B >= 200) continue;
        int n = 0;
        for (int yy = -5; yy <= 5; yy++)
          for (int xx = -5; xx <= 5; xx++) {
            double d = Math.Sqrt(xx * xx + yy * yy);
            if (d < 2.5 || d > 5) continue;
            var q = b.GetPixel(cx + xx, cy + yy);
            if (q.R >= 170 && q.G >= 170 && q.B >= 170) n++;
          }
        if (n >= 5) return true;
      }
    return false;
  }
  // Node terpilih di (x, y) ± rad px: blok 3x3 berwarna garis (isi kotak 10 px; garis 1,5 px tidak setebal itu).
  public static bool Block(Bitmap b, int x, int y, int rad, int r, int g, int bl, int tol) {
    for (int cy = Math.Max(1, y - rad); cy <= Math.Min(b.Height - 2, y + rad); cy++)
      for (int cx = Math.Max(1, x - rad); cx <= Math.Min(b.Width - 2, x + rad); cx++) {
        bool all = true;
        for (int yy = -1; yy <= 1 && all; yy++) for (int xx = -1; xx <= 1 && all; xx++) all = Like(b.GetPixel(cx + xx, cy + yy), r, g, bl, tol);
        if (all) return true;
      }
    return false;
  }
  // Batas piksel berwarna (r,g,b ± tol) di area: minX, minY, maxX, maxY, jumlah piksel.
  public static int[] BBox(Bitmap b, int x0, int y0, int x1, int y1, int r, int g, int bl, int tol) {
    int a = int.MaxValue, c = int.MaxValue, e = -1, f = -1, n = 0;
    for (int y = Math.Max(0, y0); y < Math.Min(b.Height, y1); y++)
      for (int x = Math.Max(0, x0); x < Math.Min(b.Width, x1); x++)
        if (Like(b.GetPixel(x, y), r, g, bl, tol)) { a = Math.Min(a, x); c = Math.Min(c, y); e = Math.Max(e, x); f = Math.Max(f, y); n++; }
    return new[] { a, c, e, f, n };
  }
  // Piksel berwarna (r,g,b ± tol) yang muncul atau hilang antara dua gambar, di luar lingkaran (cx,cy) berjari-jari rad.
  public static int Diff(Bitmap p, Bitmap q, int x0, int y0, int x1, int y1, int r, int g, int bl, int tol, int cx, int cy, int rad) {
    int n = 0;
    for (int y = Math.Max(0, y0); y < Math.Min(Math.Min(p.Height, q.Height), y1); y++)
      for (int x = Math.Max(0, x0); x < Math.Min(Math.Min(p.Width, q.Width), x1); x++) {
        if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= rad * rad) continue;
        if (Like(p.GetPixel(x, y), r, g, bl, tol) != Like(q.GetPixel(x, y), r, g, bl, tol)) n++;
      }
    return n;
  }
  // Posisi kursor terakhir yang diatur uji. Saat tombol dilepas kursor harus masih di sana; bila tidak, mouse disentuh selama uji.
  static int lastX, lastY;
  static void Move(int x, int y) { SetCursorPos(x, y); lastX = x; lastY = y; }
  public static void Down(int x, int y) { Guard(x, y, true); Move(x, y); System.Threading.Thread.Sleep(150); mouse_event(2, 0, 0, 0, IntPtr.Zero); System.Threading.Thread.Sleep(80); }
  public static void Up() {
    System.Threading.Thread.Sleep(100);
    POINT p; GetCursorPos(out p);
    mouse_event(4, 0, 0, 0, IntPtr.Zero); System.Threading.Thread.Sleep(200);
    if (Math.Abs(p.X - lastX) > 1 || Math.Abs(p.Y - lastY) > 1)
      throw new InvalidOperationException("kursor pindah ke (" + p.X + "," + p.Y + "), bukan (" + lastX + "," + lastY + "): mouse disentuh selama uji, hasil tidak sah");
  }
  public static void Click(int x, int y) { Down(x, y); Up(); }
  public static void Drag(int x0, int y0, int x1, int y1) {
    Down(x0, y0);
    for (int i = 1; i <= 25; i++) { Move(x0 + (x1 - x0) * i / 25, y0 + (y1 - y0) * i / 25); System.Threading.Thread.Sleep(20); }
    Up();
  }
  public static void Shift(bool down) { if (down) Guard(0, 0, false); keybd_event(0x10, 0, down ? 0u : 2u, IntPtr.Zero); System.Threading.Thread.Sleep(80); }
  public static void Key(byte vk, bool ctrl) {
    Guard(0, 0, false);
    if (ctrl) keybd_event(0x11, 0, 0, IntPtr.Zero);
    uint x = (vk >= 0x21 && vk <= 0x2E) ? 1u : 0u; // Delete: tombol extended
    keybd_event(vk, 0, x, IntPtr.Zero); keybd_event(vk, 0, x | 2, IntPtr.Zero);
    if (ctrl) keybd_event(0x11, 0, 2, IntPtr.Zero);
    System.Threading.Thread.Sleep(300);
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
function Invoke($e) { $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep -Milliseconds 400 }
function Button($name) { Invoke (Find $AE::NameProperty $name $ed) }
function Choose($name) { (Find $AE::NameProperty $name $ed).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 500 }
function Chosen($name) { (Find $AE::NameProperty $name $ed).GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected }
function Status { (Find $AE::AutomationIdProperty 'Status').Current.Name }
function Info { (Find $AE::AutomationIdProperty 'Info' $ed).Current.Name }
# Tunggu sampai Info cocok dengan pola (garis baru selesai dibuat dan digambar).
function Wait($re) { for ($i = 0; $i -lt 40 -and (Info) -notmatch $re; $i++) { Start-Sleep -Milliseconds 250 } }
function Kiss { if ((Info) -match '^Kiss (\d+) titik') { [int]$Matches[1] } else { -1 } }
function CanSave { (Find $AE::AutomationIdProperty 'SaveBtn' $ed).Current.IsEnabled }
function Badge($text) { @($root.FindAll('Descendants', $PC::new($AE::NameProperty, $text)) | Where-Object { -not $_.Current.IsOffscreen -and -not $_.Current.BoundingRectangle.IsEmpty }).Count }
function Focus { [M]::SetForegroundWindow($eh) | Out-Null; Start-Sleep -Milliseconds 300 }
# Area kanvas (koordinat gambar jendela): kanan panel kiri (slider Simplify), di atas baris Info.
function Area {
  $r = New-Object M+RECT; [M]::GetWindowRect($eh, [ref]$r) | Out-Null
  $left = (Find $AE::AutomationIdProperty 'Eps' $ed).Current.BoundingRectangle.Right
  $bottom = (Find $AE::AutomationIdProperty 'Info' $ed).Current.BoundingRectangle.Top
  [pscustomobject]@{ L = $r.L; T = $r.T; X0 = [int]($left - $r.L + 25); Y0 = 45; Y1 = [int]($bottom - $r.T - 12) }
}
# Node dan handle yang tampil, koordinat layar: @(x, y, jenis) dengan jenis 0 = node, 1 = node terpilih, 2 = handle.
function Nodes {
  Start-Sleep -Milliseconds 400
  $a = Area; $bmp = [M]::Shot($eh)
  $n = [M]::Nodes($bmp, $a.X0, $a.Y0, $bmp.Width - 10, $a.Y1)
  $bmp.Dispose()
  @($n | ForEach-Object { , @(($_[0] + $a.L), ($_[1] + $a.T), $_[2]) })
}
# Batas warna di kanvas, koordinat layar: @(minX, minY, maxX, maxY, jumlah piksel).
function Bounds($r, $g, $b, $tol) {
  Start-Sleep -Milliseconds 300
  $a = Area; $bmp = [M]::Shot($eh)
  $q = [M]::BBox($bmp, $a.X0, $a.Y0, $bmp.Width - 10, $a.Y1, $r, $g, $b, $tol)
  $bmp.Dispose()
  @(($q[0] + $a.L), ($q[1] + $a.T), ($q[2] + $a.L), ($q[3] + $a.T), $q[4])
}
function Kind($nodes, $k) { @($nodes | Where-Object { $_[2] -eq $k }) }
function Near($nodes, $x, $y, $tol) { @($nodes | Where-Object { [Math]::Abs($_[0] - $x) -le $tol -and [Math]::Abs($_[1] - $y) -le $tol }).Count -gt 0 }
function Dist($q, $x, $y) { [Math]::Sqrt([Math]::Pow($q[0] - $x, 2) + [Math]::Pow($q[1] - $y, 2)) }
# Titik terdekat ke (x, y), jenis k (-1 = semua): .P = titik, .D = jarak px.
function Nearest($nodes, $x, $y, $k) {
  $best = $null; $bd = 1e9
  foreach ($q in $nodes) { if ($k -ge 0 -and $q[2] -ne $k) { continue }; $d = Dist $q $x $y; if ($d -lt $bd) { $bd = $d; $best = $q } }
  [pscustomobject]@{ P = $best; D = $bd }
}
function Save($name) { if ($Shots) { $bmp = [M]::Shot($eh); $bmp.Save((Join-Path $Shots "editor-mouse-$name.png")); $bmp.Dispose() } }
# Handle (cincin) di titik layar q; node terpilih (kotak berisi magenta) di dekat titik layar q.
function RingAt($q) { Start-Sleep -Milliseconds 300; $a = Area; $bmp = [M]::Shot($eh); $ok = [M]::Ring($bmp, [int]$q[0] - $a.L, [int]$q[1] - $a.T); $bmp.Dispose(); $ok }
function SelAt($q, $rad) { Start-Sleep -Milliseconds 300; $a = Area; $bmp = [M]::Shot($eh); $ok = [M]::Block($bmp, [int]$q[0] - $a.L, [int]$q[1] - $a.T, $rad, 236, 0, 140, 12); $bmp.Dispose(); $ok }
function Editor { $w = $AE::RootElement.FindFirst('Children', $byTitle); if (-not $w) { $w = $root.FindFirst('Children', $byTitle) }; $w }
function Open {
  Invoke (Find $AE::NameProperty 'Edit garis potong')
  $script:ed = $null
  for ($i = 0; $i -lt 60 -and -not $script:ed; $i++) { Start-Sleep -Milliseconds 250; $script:ed = Editor }
  if (-not $script:ed) { throw 'jendela editor tidak muncul' }
  Start-Sleep -Milliseconds 800
  $script:eh = [IntPtr]$script:ed.Current.NativeWindowHandle
  [M]::Target = $script:eh
  Focus
  if ([M]::GetForegroundWindow() -ne $script:eh) { throw 'jendela editor tidak bisa jadi jendela aktif' }
}
function Launch($file) {
  $script:p = Start-Process $Exe -ArgumentList ('"' + $file + '"') -PassThru
  $script:root = $null
  for ($i = 0; $i -lt 120 -and -not $script:root; $i++) { Start-Sleep -Milliseconds 500; $script:p.Refresh(); if ($script:p.MainWindowHandle -ne 0) { $script:root = $AE::FromHandle($script:p.MainWindowHandle) } }
  for ($i = 0; $i -lt 120 -and (Status) -notmatch 'stiker di'; $i++) { Start-Sleep -Milliseconds 250 }
  $script:byTitle = [System.Windows.Automation.AndCondition]::new($PC::new($AE::ProcessIdProperty, $script:p.Id), $PC::new($AE::NameProperty, "Garis potong — $(Split-Path $file -Leaf)"))
}

try {
# ---------- Bagian A: temuan leader 2026-10-26 ----------
Launch (Join-Path $In 'png-transparent.png')
"awal: $(Status)"
# Lebar desain baru dan offset kiss bawaan aplikasi. Pengaturan tidak disimpan antar-jalan, jadi proses bagian D memakai nilai yang sama.
function Num($id) { [double]((Find $AE::AutomationIdProperty $id).GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value -replace ',', '.') }
$W = Num 'WidthBox'; $O0 = Num 'KissOff'
(Find $AE::AutomationIdProperty 'DieOn').GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
Open

# CUT-19: kiss Bintang, lalu Die-cut + Kotak. Tombol bentuk mengikuti garis yang dipilih.
Choose 'Kiss-cut'; Choose 'Bintang'
Choose 'Die-cut'; Choose 'Kotak'
$i1 = Info; $rectOn = Chosen 'Kotak'
Choose 'Kiss-cut'; $starOn = Chosen 'Bintang'
Res 'CUT-19' ($i1 -eq 'Kiss 10 titik · die 4 titik · Bintang · die Kotak' -and $rectOn -and $starOn) "Info '$i1'; saat Die-cut tombol Kotak terpilih $rectOn; saat Kiss-cut tombol Bintang terpilih $starOn"
Save 'bintang-kotak'

# EDIT-20a: klik 3 px dari node tanpa menggeser mouse: node tetap di tempatnya, garis tidak jadi manual.
Focus
$n0 = Nodes
$a = @($n0 | Sort-Object { $_[1] })[0]
[M]::Click($a[0] + 3, $a[1] + 2)
$n1 = Nodes; $i2 = Info; $pick = SelAt $a 3
Res 'EDIT-20a' ($n0.Count -eq 10 -and $pick -and (Near $n1 $a[0] $a[1] 1) -and $i2 -match 'Bintang · die Kotak$') "node kiss terdeteksi $($n0.Count); node atas ($($a[0]),$($a[1])) terpilih: $pick, masih di tempat: $(Near $n1 $a[0] $a[1] 1); Info '$i2'"

# EDIT-20b: tekan 4 px dari node, seret +30/+15 px: node bergeser 30/15 px (tidak melompat ke kursor), garis jadi manual.
[M]::Drag($a[0] + 4, $a[1] + 3, $a[0] + 34, $a[1] + 18)
$n2 = Nodes; $i3 = Info
$rel = Near $n2 ($a[0] + 30) ($a[1] + 15) 2; $jump = Near $n2 ($a[0] + 34) ($a[1] + 18) 1
Res 'EDIT-20b' ($rel -and -not $jump -and $i3 -match 'garis manual$') "node di (+30,+15) px: $rel; di posisi kursor (+34,+18): $jump; Info '$i3'"
Save 'seret-relatif'

# EDIT-20c: Ctrl+Z, klik node kiri, Shift+klik node kanan, seret 25 px ke bawah: keduanya bergeser; Delete menghapus keduanya.
Focus; [M]::Key(0x5A, $true)
$n3 = Nodes; $i4 = Info
$b = @($n3 | Sort-Object { $_[0] })[0]; $c = @($n3 | Sort-Object { $_[0] })[-1]
[M]::Click($b[0], $b[1])
[M]::Shift($true); [M]::Click($c[0], $c[1]); [M]::Shift($false)
[M]::Drag($c[0], $c[1], $c[0], $c[1] + 25)
$n4 = Nodes
$both = (Near $n4 $b[0] ($b[1] + 25) 2) -and (Near $n4 $c[0] ($c[1] + 25) 2)
Save 'shift-dua-node'
Focus; [M]::Key(0x2E, $false)
$i5 = Info
Res 'EDIT-20c' ($i4 -match 'Bintang · die Kotak$' -and $both -and $i5 -match '^Kiss 8 titik') "Ctrl+Z: '$i4'; Shift+klik 2 node lalu seret 25 px: keduanya bergeser $both; Delete: '$i5'"

# CUT-19b: kembali ke Bintang + Kotak, Simpan: daftar desain menampilkan "Bintang · die Kotak".
Focus; [M]::Key(0x5A, $true); [M]::Key(0x5A, $true)
$i6 = Info
Button 'Simpan'
for ($i = 0; $i -lt 60 -and (Editor); $i++) { Start-Sleep -Milliseconds 250 }
for ($i = 0; $i -lt 40 -and (Badge 'Bintang · die Kotak') -ne 1; $i++) { Start-Sleep -Milliseconds 250 }
$nb = Badge 'Bintang · die Kotak'
Res 'CUT-19b' ($i6 -match 'Bintang · die Kotak$' -and $nb -eq 1) "sebelum Simpan '$i6'; tanda 'Bintang · die Kotak' di daftar desain: $nb"

# ---------- Bagian B: handle kurva (EDIT-18) ----------
Open
Choose 'Kiss-cut'; Choose 'Lingkaran'; Wait 'Lingkaran · die Kotak$'
$m0 = Nodes
$t = @($m0 | Sort-Object { $_[1] })[0]; $l = @($m0 | Sort-Object { $_[0] })[0]; $rt = @($m0 | Sort-Object { $_[0] })[-1]
# Lingkaran = 4 node halus; handle node atas di garis singgung mendatar sejauh K x jari-jari (K = 0,5523).
$rad = ($rt[0] - $l[0]) / 2; $kk = 0.5522847498
[M]::Click($t[0], $t[1])
$hr = @([int]($t[0] + $kk * $rad), $t[1]); $hl = @([int]($t[0] - $kk * $rad), $t[1])
$shown = (SelAt $t 3) -and (RingAt $hr) -and (RingAt $hl)
# Node halus: handle kanan diseret 20 px ke atas, handle kiri ikut segaris dengan panjang tetap (pindah ±20 px ke bawah).
[M]::Drag($hr[0], $hr[1], $hr[0], $hr[1] - 20)
$i7 = Info
$hr2 = @($hr[0], ($hr[1] - 20))
$len = $kk * $rad; $vx = $hr2[0] - $t[0]; $vy = $hr2[1] - $t[1]; $vl = [Math]::Sqrt($vx * $vx + $vy * $vy)
$hl2 = @([int]($t[0] - $vx / $vl * $len), [int]($t[1] - $vy / $vl * $len))
$smooth = (RingAt $hr2) -and (RingAt $hl2) -and -not (RingAt $hl) -and $i7 -match 'garis manual$'
Save 'handle-halus'
# Node sudut: handle kanan diseret 30 px ke bawah, handle kiri tetap di tempat.
Button 'Node sudut'
[M]::Drag($hr2[0], $hr2[1], $hr2[0], $hr2[1] + 30)
$hr3 = @($hr2[0], ($hr2[1] + 30))
$cusp = (RingAt $hr3) -and (RingAt $hl2)
# Jadikan garis: handle node hilang. Jadikan kurva: handle baru di 1/3 tali busur ke node kiri dan kanan.
Button 'Jadikan garis'
$line = -not (RingAt $hr3) -and -not (RingAt $hl2)
Button 'Jadikan kurva'
$c1 = @([int]($t[0] + ($l[0] - $t[0]) / 3), [int]($t[1] + ($l[1] - $t[1]) / 3)); $c2 = @([int]($t[0] + ($rt[0] - $t[0]) / 3), [int]($t[1] + ($rt[1] - $t[1]) / 3))
$curve = (RingAt $c1) -and (RingAt $c2)
Save 'handle-kurva'
Res 'EDIT-18' ($m0.Count -eq 4 -and $shown -and $smooth -and $cusp -and $line -and $curve) ("lingkaran {0} node, node atas ({7},{8}), handle kanan dihitung ({9},{10}); node atas diklik: terpilih dan 2 handle tampil {1}; node halus: handle kanan +20 px ke atas, handle kiri ikut segaris dengan panjang tetap {2}; node sudut: handle kiri tetap {3}; Jadikan garis: handle hilang {4}; Jadikan kurva: handle di 1/3 tali busur {5}; Info '{6}'" -f $m0.Count, $shown, $smooth, $cusp, $line, $curve, $i7, $t[0], $t[1], $hr[0], $hr[1])

# ---------- Bagian C: operasi node (EDIT-14) ----------
Choose 'Bintang'; Wait 'Bintang · die Kotak$'
$s0 = Nodes
$r0 = @($s0 | Sort-Object { $_[0] })[-1]
$box = Bounds 155 157 167 25
# Node diseret jauh ke kanan: berhenti di batas kotak kerja; kiss keluar die, Simpan nonaktif.
[M]::Drag($r0[0], $r0[1], $box[2] + 150, $r0[1])
$r1 = @($box[2], $r0[1]); $off = -not (CanSave)
# Posisi node relatif terhadap ujung kiri dan atas bintang (tidak terpengaruh skala: kotak kerja melebar 1 mm saat
# Ctrl+Y mengembalikan node yang keluar kotak bawaan, jadi skala kanvas berubah).
function Ratio($nodes, $rn) { $ln = @($nodes | Sort-Object { $_[0] })[0]; $tn = @($nodes | Sort-Object { $_[1] })[0]; $w = $tn[0] - $ln[0]; @((($rn[0] - $ln[0]) / $w), (($rn[1] - $tn[1]) / $w)) }
$q1 = Ratio $s0 $r1
$clamp = (SelAt $r1 3) -and -not (Near (Kind (Nodes) 0) $r0[0] $r0[1] 2)
Save 'batas-kotak'
Focus; [M]::Key(0x5A, $true); $undo = (Near (Nodes) $r0[0] $r0[1] 1) -and (CanSave)
Focus; [M]::Key(0x59, $true); $s3 = Nodes; $q3 = Ratio $s3 (@($s3 | Sort-Object { $_[0] })[-1])
$redo = [Math]::Abs($q3[0] - $q1[0]) -le 0.01 -and [Math]::Abs($q3[1] - $q1[1]) -le 0.01 -and -not (CanSave)
Focus; [M]::Key(0x5A, $true)
# Klik di tengah sisi bintang: node baru di titik klik, garis lain tidak berubah; Delete menghapusnya.
$s2 = Nodes; $k0 = Kiss
$top = @($s2 | Sort-Object { $_[1] })[0]
$nb2 = @($s2 | Where-Object { (Dist $_ $top[0] $top[1]) -gt 5 } | Sort-Object { Dist $_ $top[0] $top[1] })[0]
$mx = [int](($top[0] + $nb2[0]) / 2); $my = [int](($top[1] + $nb2[1]) / 2)
$ar = Area; $bmp0 = [M]::Shot($eh)
[M]::Click($mx, $my)
Start-Sleep -Milliseconds 400
$bmp1 = [M]::Shot($eh)
$diff = [M]::Diff($bmp0, $bmp1, $ar.X0, $ar.Y0, $bmp0.Width - 10, $ar.Y1, 236, 0, 140, 60, $mx - $ar.L, $my - $ar.T, 9)
$bmp0.Dispose(); $bmp1.Dispose()
$k1 = Kiss; $added = SelAt @($mx, $my) 3
Focus; [M]::Key(0x2E, $false); $k2 = Kiss
# Garis otomatis (kiss luar + kiss dalam di lubang): node kontur dalam dihapus satu per satu; sisa < 3 node = kontur terhapus.
Button 'Reset otomatis'
$art = (Find $AE::AutomationIdProperty 'Art' $ed).Current.BoundingRectangle
$cx = $art.X + $art.Width / 2; $cy = $art.Y + $art.Height / 2; $rad = $art.Width * 0.2
function Inner { @(Nodes | Where-Object { $_[2] -ne 2 -and (Dist $_ $cx $cy) -lt $rad }) }
$k3 = Kiss; $in0 = (Inner).Count; $presses = 0
for ($i = 0; $i -lt 8 -and (Inner).Count -gt 0; $i++) { $q = (Inner)[0]; [M]::Click($q[0], $q[1]); Focus; [M]::Key(0x2E, $false); $presses++ }
$k4 = Kiss; $in1 = (Inner).Count
Res 'EDIT-14' ($clamp -and $off -and $undo -and $redo -and $k1 -eq $k0 + 1 -and $added -and $diff -le 6 -and $k2 -eq $k0 -and $in0 -ge 1 -and $in1 -eq 0 -and $presses -eq [Math]::Max(1, $in0 - 2) -and $k4 -eq $k3 - $in0) ("node diseret jauh: node terpilih berhenti di batas kanan kotak (x {2}) {0}, Simpan nonaktif {3}; Ctrl+Z kembali {4}, Ctrl+Y {5}; klik di sisi: kiss {6} -> {7}, node baru di titik klik {8}, piksel garis berubah di luar node baru {9}; Delete: {10}; kontur dalam {11} node: {12} kali Delete lalu seluruh kontur hilang (sisa < 3 node), kiss {13} -> {14}, sisa node dalam {15}" -f $clamp, $r1[0], $box[2], $off, $undo, $redo, $k0, $k1, $added, $diff, $k2, $in0, $presses, $k3, $k4, $in1)
Button 'Batal'
$p.Kill()

# ---------- Bagian D: smart select dengan klik di kanvas (EDIT-16) ----------
# Gambar uji 600 x 400 px: donat oranye, watermark putih alpha 38 yang menempel di bawahnya, kotak biru terpisah. Isi (alpha > 16)
# = x 40..559, y 70..319, jadi artwork di layar = area itu, selebar $W mm. Lubang donat sempit seperti donat user: jari-jari
# R = (offset + 1,3) / 2 mm, di tengah antara 0,8 mm (garis dalam bisa dibuat dengan klik: R - 0,5 >= 0,3) dan offset + 0,5 mm
# (lubang selebar itu langsung dapat garis dalam), jadi garis dalamnya baru muncul saat lubang diklik.
$O = if ($PSBoundParameters.ContainsKey('Offset')) { $Offset } else { $O0 }
$Rmm = ($O + 1.3) / 2; $Rpx = $Rmm * 520 / $W
$img = Join-Path $env:TEMP ("printech-smart-" + [guid]::NewGuid().ToString('N') + '.png')
$bmp = New-Object System.Drawing.Bitmap 600, 400, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp); $g.Clear([System.Drawing.Color]::Transparent)
$g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 245, 140, 30))), 60, 70, 240, 240)
$g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
$g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::Transparent)), [float](180 - $Rpx), [float](190 - $Rpx), [float](2 * $Rpx), [float](2 * $Rpx))
$g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
$g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(38, 255, 255, 255))), 40, 300, 520, 20)
$g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 40, 90, 200))), 420, 100, 120, 120)
$g.Dispose(); $bmp.Save($img, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
Launch $img
if ($O -ne $O0) { (Find $AE::AutomationIdProperty 'KissOff').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue("$O") }
Open
$sm = (Find $AE::AutomationIdProperty 'SmartBox' $ed).GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern); $sm.Toggle(); Start-Sleep -Milliseconds 300
# Skala kanvas berubah tiap kali garis berubah, jadi posisi artwork dibaca ulang setiap kali dipakai.
function ArtRect { Start-Sleep -Milliseconds 300; (Find $AE::AutomationIdProperty 'Art' $ed).Current.BoundingRectangle }
function At($u, $v) { $r = ArtRect; , @([int]($r.X + ($u - 40) / 520 * $r.Width), [int]($r.Y + ($v - 70) / 250 * $r.Height)) }
# Tepi kanan garis kiss dalam piksel gambar uji.
function Right { $b = Bounds 236 0 140 60; $r = ArtRect; [int](($b[2] - $r.X) / $r.Width * 520 + 40) }
$q = At 180 100; [M]::Click($q[0], $q[1]); $iA = Info; $kA = Kiss; $rA = Right
$h = At 180 190; [M]::Click($h[0], $h[1]); $kB = Kiss
$h = At 180 190; [M]::Click($h[0], $h[1]); $kC = Kiss
$q = At 480 160; [M]::Shift($true); [M]::Click($q[0], $q[1]); [M]::Shift($false); $kD = Kiss; $rD = Right
Save 'smart-dua-objek'
(Find $AE::AutomationIdProperty 'Tol' $ed).GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).SetValue(16); Start-Sleep -Milliseconds 800
$kE = Kiss; $rE = Right
Save 'smart-toleransi-16'
Res 'EDIT-16' ($iA -match 'garis manual$' -and $rA -lt 420 -and $kB -gt $kA -and $kC -eq $kA -and $kD -gt $kA -and $rD -ge 540 -and $rE -ge $rD + 10) ("lebar {8} mm, offset kiss {9} mm, lubang R {10:N2} mm; klik donat: kiss {0} node, tepi kanan garis x {1} px gambar (< kotak biru 420, watermark diabaikan); klik lubang: {2} node (garis dalam muncul); klik lagi: {3} (hilang); Shift+klik kotak biru: {4} node, tepi kanan x {5} (>= 540, tepi kotak); Toleransi 16: {6} node, tepi kanan x {7} (watermark sampai 559 ikut)" -f $kA, $rA, $kB, $kC, $kD, $rD, $kE, $rE, $W, $O, $Rmm)
}
finally {
  if ($p -and -not $p.HasExited) { "responding: " + $p.Responding; $p.Kill() }
  if ($img -and (Test-Path $img)) { Remove-Item $img }
}
$bad = @($results | Where-Object { $_ -match ' GAGAL:' }).Count
"RINGKAS: {0} uji, {1} GAGAL" -f $results.Count, $bad
