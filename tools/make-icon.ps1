# Tạo Assets/Icons/app.ico (16/24/32/48/64/256 px) từ một sprite PNG.
#   powershell -ExecutionPolicy Bypass -File tools/make-icon.ps1 [-Source Assets/Sprites/Mobs/mushmom/0.png]
param([string]$Source = 'Assets/Sprites/Mobs/mushmom/0.png')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$src = [System.Drawing.Image]::FromFile((Join-Path $root $Source))
$sizes = 16, 24, 32, 48, 64, 256
$pngs = foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    # Phóng to giữ nét pixel art, thu nhỏ thì làm mượt
    $g.InterpolationMode = if ($size -gt $src.Width) { 'NearestNeighbor' } else { 'HighQualityBicubic' }
    $g.PixelOffsetMode = 'Half'
    $scale = [Math]::Min($size / $src.Width, $size / $src.Height)
    $w = [int]($src.Width * $scale); $h = [int]($src.Height * $scale)
    $g.DrawImage($src, [int](($size - $w) / 2), [int](($size - $h) / 2), $w, $h)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}
$src.Dispose()

# ICO: header 6 byte + 16 byte mỗi entry, dữ liệu từng ảnh là PNG nguyên khối
$out = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $out
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$dim); $writer.Write([byte]$dim); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$pngs[$i].Length); $writer.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($png in $pngs) { $writer.Write($png) }
$writer.Flush()
$target = Join-Path $root 'Assets/Icons/app.ico'
[System.IO.File]::WriteAllBytes($target, $out.ToArray())
Write-Host "Đã tạo $target"
