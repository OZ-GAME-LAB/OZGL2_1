param(
    [string] $ProjectRoot = 'D:/GitHub/OZGL2_1'
)

# 승인 범위: 원본 RGB와 외곽 안티앨리어싱은 보존하고 안전한 내부 알파만 255로 변경한다.
Add-Type -AssemblyName System.Drawing
$sourceRoot = Join-Path $ProjectRoot 'Tools/Art/Sources/CombatRoundHud_v1'
$destinationRoot = Join-Path $ProjectRoot 'Assets/06.UI/BattleMutedPreview/Heraldry_Battle_v5/Source'
$null = New-Item -ItemType Directory -Path $destinationRoot -Force
$specs = @(
    @{ Source = (Join-Path $sourceRoot 'skill_round_damage.png'); Name = 'skill_round_damage.png' },
    @{ Source = (Join-Path $sourceRoot 'skill_round_buff.png'); Name = 'skill_round_buff.png' },
    @{ Source = (Join-Path $sourceRoot 'skill_round_debuff.png'); Name = 'skill_round_debuff.png' },
    @{ Source = (Join-Path $ProjectRoot 'Assets/06.UI/BattleMutedPreview/Heraldry_Battle_v4/Source/currency_medallion_no_flags.png'); Name = 'currency_medallion_opaque.png' }
)
$threshold = 200
$erosion = 3
$reports = foreach ($spec in $specs) {
    $destination = Join-Path $destinationRoot $spec.Name
    if (Test-Path -LiteralPath $destination) { throw "기존 파일을 덮어쓰지 않습니다: $destination" }
    $bitmap = [System.Drawing.Bitmap]::new($spec.Source)
    $width = $bitmap.Width
    $height = $bitmap.Height
    $rectangle = [System.Drawing.Rectangle]::new(0, 0, $width, $height)
    $locked = $bitmap.LockBits($rectangle, [System.Drawing.Imaging.ImageLockMode]::ReadWrite, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $locked.Stride
    if ($stride -le 0) { throw '예상하지 못한 음수 stride' }
    $original = [byte[]]::new($stride * $height)
    [System.Runtime.InteropServices.Marshal]::Copy($locked.Scan0, $original, 0, $original.Length)
    $edited = [byte[]]$original.Clone()
    $prefixWidth = $width + 1
    $prefix = [int[]]::new(($height + 1) * $prefixWidth)
    for ($y = 0; $y -lt $height; $y++) {
        $rowBad = 0
        for ($x = 0; $x -lt $width; $x++) {
            if ($original[$y * $stride + $x * 4 + 3] -lt $threshold) { $rowBad++ }
            $prefix[($y + 1) * $prefixWidth + $x + 1] = $prefix[$y * $prefixWidth + $x + 1] + $rowBad
        }
    }
    $safeMask = [bool[]]::new($width * $height)
    $changedCount = 0
    $safeCount = 0
    $minSafeBefore = 255
    $maxSafeBefore = 0
    $changeMinX = $width; $changeMinY = $height; $changeMaxX = -1; $changeMaxY = -1
    for ($y = $erosion; $y -lt ($height - $erosion); $y++) {
        for ($x = $erosion; $x -lt ($width - $erosion); $x++) {
            $x0 = $x - $erosion; $x1 = $x + $erosion + 1
            $y0 = $y - $erosion; $y1 = $y + $erosion + 1
            $badCount = $prefix[$y1 * $prefixWidth + $x1] - $prefix[$y0 * $prefixWidth + $x1] - $prefix[$y1 * $prefixWidth + $x0] + $prefix[$y0 * $prefixWidth + $x0]
            if ($badCount -ne 0) { continue }
            $safeMask[$y * $width + $x] = $true
            $safeCount++
            $index = $y * $stride + $x * 4 + 3
            $alpha = $original[$index]
            if ($alpha -lt $minSafeBefore) { $minSafeBefore = $alpha }
            if ($alpha -gt $maxSafeBefore) { $maxSafeBefore = $alpha }
            if ($alpha -eq 255) { continue }
            $edited[$index] = 255
            $changedCount++
            if ($x -lt $changeMinX) { $changeMinX = $x }; if ($x -gt $changeMaxX) { $changeMaxX = $x }
            if ($y -lt $changeMinY) { $changeMinY = $y }; if ($y -gt $changeMaxY) { $changeMaxY = $y }
        }
    }
    [System.Runtime.InteropServices.Marshal]::Copy($edited, 0, $locked.Scan0, $edited.Length)
    $bitmap.UnlockBits($locked)
    $bitmap.Save($destination, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()

    # 저장된 PNG를 다시 읽어 RGB, 보호 영역, 보정 영역의 완전 불투명을 검사한다.
    $verification = [System.Drawing.Bitmap]::new($destination)
    $verifyLocked = $verification.LockBits($rectangle, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $actual = [byte[]]::new($verifyLocked.Stride * $height)
    [System.Runtime.InteropServices.Marshal]::Copy($verifyLocked.Scan0, $actual, 0, $actual.Length)
    $rgbDifferences = 0; $protectedAlphaDifferences = 0; $unsafeInteriorCount = 0
    $visibleMinX = $width; $visibleMinY = $height; $visibleMaxX = -1; $visibleMaxY = -1
    for ($y = 0; $y -lt $height; $y++) {
        for ($x = 0; $x -lt $width; $x++) {
            $index = $y * $stride + $x * 4
            $actualIndex = $y * $verifyLocked.Stride + $x * 4
            for ($channel = 0; $channel -lt 3; $channel++) { if ($original[$index + $channel] -ne $actual[$actualIndex + $channel]) { $rgbDifferences++ } }
            $alpha = $actual[$actualIndex + 3]
            if ($safeMask[$y * $width + $x]) { if ($alpha -ne 255) { $unsafeInteriorCount++ } }
            elseif ($original[$index + 3] -ne $alpha) { $protectedAlphaDifferences++ }
            if ($alpha -ge 128) {
                if ($x -lt $visibleMinX) { $visibleMinX = $x }; if ($x -gt $visibleMaxX) { $visibleMaxX = $x }
                if ($y -lt $visibleMinY) { $visibleMinY = $y }; if ($y -gt $visibleMaxY) { $visibleMaxY = $y }
            }
        }
    }
    $verification.UnlockBits($verifyLocked)
    $verification.Dispose()
    if ($rgbDifferences -gt 0 -or $protectedAlphaDifferences -gt 0 -or $unsafeInteriorCount -gt 0) { throw "보존 검증 실패: $($spec.Name), RGB=$rgbDifferences ProtectedAlpha=$protectedAlphaDifferences Interior=$unsafeInteriorCount" }
    [pscustomobject]@{
        File = $spec.Name
        Source = $spec.Source
        Destination = $destination
        Dimensions = @($width, $height)
        Threshold = $threshold
        ErosionPixels = $erosion
        SafeInteriorPixelCount = $safeCount
        ChangedAlphaPixelCount = $changedCount
        BeforeSafeAlphaRange = @($minSafeBefore, $maxSafeBefore)
        AfterSafeAlphaRange = @(255, 255)
        ChangedAlphaBounds = @($changeMinX, $changeMinY, ($changeMaxX + 1), ($changeMaxY + 1))
        VisibleAlpha128Bounds = @($visibleMinX, $visibleMinY, ($visibleMaxX + 1), ($visibleMaxY + 1))
        RgbDifferences = $rgbDifferences
        OutsideAndEdgeAlphaDifferences = $protectedAlphaDifferences
        SourceSha256 = (Get-FileHash -LiteralPath $spec.Source -Algorithm SHA256).Hash
        FinalSha256 = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    }
}
$reports | ConvertTo-Json -Depth 5
