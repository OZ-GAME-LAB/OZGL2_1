param([string]$ProjectRoot = 'D:\GitHub\OZGL2_1')

# 생성 원본을 보존하고 동일한 알파 경계/여백만 적용한다. 색상과 디자인은 수정하지 않는다.
Add-Type -AssemblyName System.Drawing
$sourceRoot = 'C:\Users\sudea\.codex\generated_images\01a0e5ae-0fcd-7542-b58a-151370afada1'
$referenceRoot = Join-Path $ProjectRoot 'Docs/UI/References/BattleResultRefinement_20260928'
$rawRoot = Join-Path $referenceRoot 'Raw'
$spriteRoot = Join-Path $ProjectRoot 'Assets/06.UI/BattleMutedPreview/ResultRefinement_v2/Sprites'
[System.IO.Directory]::CreateDirectory($rawRoot) | Out-Null
[System.IO.Directory]::CreateDirectory($spriteRoot) | Out-Null
$inputs = @(
    @{ Name = 'Underframe_Victory'; File = 'exec-3712f20f-a9dd-4aa6-9490-5d0899e0a500.png' },
    @{ Name = 'Underframe_Defeat'; File = 'exec-22b4ee28-64d7-475e-814c-59616e58d552.png' }
)
$entries = @()
$unionLeft = [int]::MaxValue
$unionTop = [int]::MaxValue
$unionRight = -1
$unionBottom = -1
foreach ($item in $inputs) {
    $rawPath = Join-Path $rawRoot ($item.Name + '.png')
    Copy-Item -LiteralPath (Join-Path $sourceRoot $item.File) -Destination $rawPath
    $bitmap = [System.Drawing.Bitmap]::new($rawPath)
    $rect = [System.Drawing.Rectangle]::new(0, 0, $bitmap.Width, $bitmap.Height)
    $data = $bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bytes = [byte[]]::new([Math]::Abs($data.Stride) * $data.Height)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
    $transparentCount = 0
    $opaqueCount = 0
    $left = $bitmap.Width
    $top = $bitmap.Height
    $right = -1
    $bottom = -1
    for ($y = 0; $y -lt $bitmap.Height; $y++) {
        for ($x = 0; $x -lt $bitmap.Width; $x++) {
            $alpha = $bytes[$y * $data.Stride + $x * 4 + 3]
            if ($alpha -eq 0) { $transparentCount++ }
            if ($alpha -eq 255) { $opaqueCount++ }
            if ($alpha -gt 8) {
                $left = [Math]::Min($left, $x)
                $top = [Math]::Min($top, $y)
                $right = [Math]::Max($right, $x)
                $bottom = [Math]::Max($bottom, $y)
            }
        }
    }
    $bitmap.UnlockBits($data)
    if ($transparentCount -eq 0 -or $right -lt 0) { throw ('알파 검증 실패: ' + $item.Name) }
    $unionLeft = [Math]::Min($unionLeft, $left)
    $unionTop = [Math]::Min($unionTop, $top)
    $unionRight = [Math]::Max($unionRight, $right)
    $unionBottom = [Math]::Max($unionBottom, $bottom)
    $entries += @{ Name=$item.Name; Bitmap=$bitmap; Width=$bitmap.Width; Height=$bitmap.Height; Bounds=@($left,$top,$right,$bottom); Transparent=$transparentCount; Opaque=$opaqueCount }
}
$padding = 8
$crop = [System.Drawing.Rectangle]::new($unionLeft-$padding, $unionTop-$padding, $unionRight-$unionLeft+1+2*$padding, $unionBottom-$unionTop+1+2*$padding)
foreach ($entry in $entries) {
    $output = $entry.Bitmap.Clone($crop, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $destination = Join-Path $spriteRoot ($entry.Name + '.png')
    $output.Save($destination, [System.Drawing.Imaging.ImageFormat]::Png)
    [PSCustomObject]@{ Name=$entry.Name; Raw=($entry.Width.ToString()+'x'+$entry.Height); AlphaBounds=($entry.Bounds -join ','); TransparentPixels=$entry.Transparent; OpaquePixels=$entry.Opaque; Final=($output.Width.ToString()+'x'+$output.Height); Path=$destination }
    $output.Dispose()
    $entry.Bitmap.Dispose()
}
