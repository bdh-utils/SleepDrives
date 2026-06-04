# Generates Resources\SleepDrives.ico from the same stacked-disk glyph the tray
# renderer draws. Run once when the brand glyph changes; the .ico is committed.
param(
    [string]$OutPath = (Join-Path $PSScriptRoot '..\SleepDrives\Resources\SleepDrives.ico')
)

Add-Type -AssemblyName System.Drawing

function New-DriveBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = $size / 32.0
    $accent = [System.Drawing.Color]::FromArgb(0xF1, 0x50, 0x25)
    $bg     = [System.Drawing.Color]::FromArgb(0x19, 0x19, 0x19)
    $fill   = New-Object System.Drawing.SolidBrush($accent)
    $groove = New-Object System.Drawing.Pen($bg, [single](1.8 * $s))

    $left = [single](6 * $s); $top = [single](5 * $s)
    $width = [single](20 * $s); $eh = [single](7 * $s)
    $bodyBottom = [single](24 * $s)

    $topRect = New-Object System.Drawing.RectangleF($left, $top, $width, $eh)
    $bodyRect = New-Object System.Drawing.RectangleF($left, [single]($top + $eh / 2), $width, [single]($bodyBottom - ($top + $eh)))
    $botRect = New-Object System.Drawing.RectangleF($left, [single]($bodyBottom - $eh), $width, $eh)
    $sep1 = New-Object System.Drawing.RectangleF($left, [single]($top + 7 * $s), $width, $eh)
    $sep2 = New-Object System.Drawing.RectangleF($left, [single]($top + 12 * $s), $width, $eh)

    $g.FillEllipse($fill, $topRect)
    $g.FillRectangle($fill, $bodyRect)
    $g.FillEllipse($fill, $botRect)
    $g.DrawArc($groove, $sep1, [single]0, [single]180)
    $g.DrawArc($groove, $sep2, [single]0, [single]180)
    $g.DrawEllipse($groove, $topRect)

    $g.Dispose(); $fill.Dispose(); $groove.Dispose()
    return $bmp
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngs = foreach ($sz in $sizes) {
    $bmp = New-DriveBitmap $sz
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

$dir = Split-Path -Parent $OutPath
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }

$fs = [System.IO.File]::Create($OutPath)
$bw = New-Object System.IO.BinaryWriter($fs)

# ICONDIR
$bw.Write([uint16]0)          # reserved
$bw.Write([uint16]1)          # type: icon
$bw.Write([uint16]$sizes.Count)

# Directory entries follow the header; image data follows all entries.
$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]; $bytes = $pngs[$i]
    $dim = if ($sz -ge 256) { 0 } else { $sz }
    $bw.Write([byte]$dim)     # width  (0 => 256)
    $bw.Write([byte]$dim)     # height (0 => 256)
    $bw.Write([byte]0)        # palette
    $bw.Write([byte]0)        # reserved
    $bw.Write([uint16]1)      # colour planes
    $bw.Write([uint16]32)     # bits per pixel
    $bw.Write([uint32]$bytes.Length)
    $bw.Write([uint32]$offset)
    $offset += $bytes.Length
}
foreach ($bytes in $pngs) { $bw.Write($bytes) }

$bw.Flush(); $bw.Close(); $fs.Close()
Write-Output "Wrote $OutPath ($((Get-Item $OutPath).Length) bytes)"
