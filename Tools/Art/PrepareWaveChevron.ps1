param(
    [Parameter(Mandatory=$true)][string]$Source,
    [Parameter(Mandatory=$true)][string]$Destination
)

# 승인된 여백/크기 정리만 수행하며 원본 픽셀의 도형과 색은 수정하지 않는다.
Add-Type -AssemblyName System.Drawing
$inputBitmap = [System.Drawing.Bitmap]::new($Source)
try {
    $left = $inputBitmap.Width
    $top = $inputBitmap.Height
    $right = -1
    $bottom = -1
    $transparentCount = 0
    for ($y = 0; $y -lt $inputBitmap.Height; $y++) {
        for ($x = 0; $x -lt $inputBitmap.Width; $x++) {
            $alpha = $inputBitmap.GetPixel($x, $y).A
            if ($alpha -eq 0) { $transparentCount++ }
            if ($alpha -gt 16) {
                $left = [Math]::Min($left, $x)
                $right = [Math]::Max($right, $x)
                $top = [Math]::Min($top, $y)
                $bottom = [Math]::Max($bottom, $y)
            }
        }
    }
    if ($right -lt $left -or $transparentCount -eq 0) { throw '유효한 투명 원본이 아닙니다.' }
    $width = $right - $left + 1
    $height = $bottom - $top + 1
    $scale = 232.0 / [Math]::Max($width, $height)
    $targetWidth = [int][Math]::Round($width * $scale)
    $targetHeight = [int][Math]::Round($height * $scale)
    $outputBitmap = [System.Drawing.Bitmap]::new(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($outputBitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $targetRect = [System.Drawing.Rectangle]::new([int]((256-$targetWidth)/2), [int]((256-$targetHeight)/2), $targetWidth, $targetHeight)
        $graphics.DrawImage($inputBitmap, $targetRect, $left, $top, $width, $height, [System.Drawing.GraphicsUnit]::Pixel)
        $outputBitmap.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Output "Alpha crop ($left,$top)-($right,$bottom); 256x256 centered PNG; transparent source pixels=$transparentCount"
    }
    finally { $graphics.Dispose(); $outputBitmap.Dispose() }
}
finally { $inputBitmap.Dispose() }
