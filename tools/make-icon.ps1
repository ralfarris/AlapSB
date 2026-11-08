param([string]$OutPath)
# Membuat ikon AlapSB (gradient ungu-pink + bar gelombang suara) dalam format .ico multi-size (PNG).
Add-Type -AssemblyName System.Drawing

function New-IconPng([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)

    $rect = New-Object System.Drawing.RectangleF 0, 0, ($s - 1), ($s - 1)
    $d = [single]($s * 0.46)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $c1 = [System.Drawing.Color]::FromArgb(124, 92, 255)
    $c2 = [System.Drawing.Color]::FromArgb(236, 72, 153)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $c1, $c2, ([single]45)
    $g.FillPath($brush, $path)

    $heights = @(0.26, 0.46, 0.62, 0.46, 0.26)
    $bw = [single]($s * 0.095)
    $gap = [single]($s * 0.06)
    $total = 5 * $bw + 4 * $gap
    $x = ($s - $total) / 2
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    foreach ($h in $heights) {
        $bh = [single]($s * $h)
        $y = ($s - $bh) / 2
        $bp = New-Object System.Drawing.Drawing2D.GraphicsPath
        $r = $bw
        $bp.AddArc($x, $y, $r, $r, 180, 180)
        $bp.AddArc($x, $y + $bh - $r, $r, $r, 0, 180)
        $bp.CloseFigure()
        $g.FillPath($white, $bp)
        $x += $bw + $gap
    }
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

$sizes = @(16, 24, 32, 48, 64, 256)
$images = @()
foreach ($s in $sizes) { $images += , (New-IconPng $s) }

$dir = Split-Path -Parent $OutPath
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
$fs = [System.IO.File]::Create($OutPath)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $images[$i].Length
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32); $bw.Write([UInt32]$len); $bw.Write([UInt32]$offset)
    $offset += $len
}
foreach ($img in $images) { $bw.Write($img) }
$bw.Close()
Write-Host "Ikon dibuat: $OutPath"
