Add-Type -AssemblyName System.Drawing

$size = 256
$bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

$background = [System.Drawing.Drawing2D.GraphicsPath]::new()
$background.AddArc(10, 10, 48, 48, 180, 90)
$background.AddArc(198, 10, 48, 48, 270, 90)
$background.AddArc(198, 198, 48, 48, 0, 90)
$background.AddArc(10, 198, 48, 48, 90, 90)
$background.CloseFigure()

$brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
    [System.Drawing.Point]::new(0, 0),
    [System.Drawing.Point]::new($size, $size),
    [System.Drawing.ColorTranslator]::FromHtml('#101e29'),
    [System.Drawing.ColorTranslator]::FromHtml('#071018'))
$graphics.FillPath($brush, $background)
$graphics.DrawPath([System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#b9e46e'), 8), $background)

$accent = [System.Drawing.ColorTranslator]::FromHtml('#b9e46e')
$accentDark = [System.Drawing.ColorTranslator]::FromHtml('#7dbb40')
$linePen = [System.Drawing.Pen]::new($accent, 10)
$linePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$linePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$linePen.DashPattern = @(12.0, 10.0)

$trajectory = [System.Drawing.Drawing2D.GraphicsPath]::new()
$trajectory.AddBezier(
    [System.Drawing.PointF]::new(58, 172),
    [System.Drawing.PointF]::new(91, 86),
    [System.Drawing.PointF]::new(150, 53),
    [System.Drawing.PointF]::new(198, 91))
$graphics.DrawPath($linePen, $trajectory)

$mortar = [System.Drawing.Drawing2D.GraphicsPath]::new()
$mortar.AddPolygon([System.Drawing.Point[]]@(
    [System.Drawing.Point]::new(110, 176),
    [System.Drawing.Point]::new(145, 142),
    [System.Drawing.Point]::new(163, 160),
    [System.Drawing.Point]::new(128, 194)))
$graphics.FillPath([System.Drawing.SolidBrush]::new($accent), $mortar)
$graphics.DrawPath([System.Drawing.Pen]::new($accent, 5), $mortar)

$base = [System.Drawing.Drawing2D.GraphicsPath]::new()
$base.AddPolygon([System.Drawing.Point[]]@(
    [System.Drawing.Point]::new(94, 192),
    [System.Drawing.Point]::new(128, 194),
    [System.Drawing.Point]::new(111, 212)))
$graphics.FillPath([System.Drawing.SolidBrush]::new($accentDark), $base)
$graphics.DrawPath([System.Drawing.Pen]::new($accent, 5), $base)

$targetPen = [System.Drawing.Pen]::new($accent, 6)
$graphics.DrawEllipse($targetPen, 172, 70, 44, 44)
$graphics.DrawLine($targetPen, 194, 58, 194, 72)
$graphics.DrawLine($targetPen, 194, 112, 194, 126)
$graphics.DrawLine($targetPen, 160, 92, 174, 92)
$graphics.DrawLine($targetPen, 214, 92, 228, 92)

$pixels = New-Object byte[] ($size * $size * 4)
$rectangle = [System.Drawing.Rectangle]::new(0, 0, $size, $size)
$data = $bitmap.LockBits($rectangle, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
$bitmap.UnlockBits($data)

$outputPath = Join-Path $PSScriptRoot '..\assets\PubgMortarRanger.ico'
$stream = [System.IO.File]::Open($outputPath, [System.IO.FileMode]::Create)
$writer = [System.IO.BinaryWriter]::new($stream)
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]1)
$writer.Write([byte]0)
$writer.Write([byte]0)
$writer.Write([byte]0)
$writer.Write([byte]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]32)
$writer.Write([UInt32](40 + $pixels.Length))
$writer.Write([UInt32]22)
$writer.Write([UInt32]40)
$writer.Write([Int32]$size)
$writer.Write([Int32]($size * 2))
$writer.Write([UInt16]1)
$writer.Write([UInt16]32)
$writer.Write([UInt32]0)
$writer.Write([UInt32]$pixels.Length)
$writer.Write([Int32]0)
$writer.Write([Int32]0)
$writer.Write([UInt32]0)
$writer.Write([UInt32]0)

for ($row = $size - 1; $row -ge 0; $row--) {
    $writer.Write($pixels, $row * $size * 4, $size * 4)
}

$writer.Write((New-Object byte[] (($size * $size) / 8)))
$writer.Dispose()
$graphics.Dispose()
$bitmap.Dispose()
