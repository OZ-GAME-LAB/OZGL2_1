param(
    [string]$SourcePath = "Tools/Art/Sources/BattleFlatReference_v1/SourceKit.png",
    [string]$OutputDirectory = "Assets/06.UI/BattleMutedPreview/FlatReference_v1/Sprites",
    [string]$OnlyName = ""
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$sourceAbsolute = Join-Path $projectRoot $SourcePath
$outputAbsolute = Join-Path $projectRoot $OutputDirectory

if (-not (Test-Path -LiteralPath $sourceAbsolute)) {
    throw "Source image not found: $sourceAbsolute"
}

New-Item -ItemType Directory -Path $outputAbsolute -Force | Out-Null

$definitions = @(
    @{ Name = "Frame_TopHud_Flat.png"; X = 20; Y = 20; Width = 1635; Height = 115; Padding = 4; WavePadding = $false },
    @{ Name = "Frame_WavePreview_Flat.png"; X = 25; Y = 140; Width = 835; Height = 165; Padding = 4; WavePadding = $false },
    @{ Name = "Frame_SynergyNameplate_Flat.png"; X = 885; Y = 170; Width = 755; Height = 115; Padding = 4; WavePadding = $false },
    @{ Name = "Frame_DiamondLarge_Flat.png"; X = 55; Y = 305; Width = 335; Height = 305; Padding = 4; WavePadding = $false },
    @{ Name = "Frame_WaveToggle_Flat.png"; X = 445; Y = 345; Width = 230; Height = 245; Padding = 2; WavePadding = $true },
    @{ Name = "Frame_MenuDiamond_Flat.png"; X = 720; Y = 350; Width = 205; Height = 210; Padding = 4; WavePadding = $false },
    @{ Name = "Bar_LevelTrack_Chevron_Flat.png"; X = 945; Y = 355; Width = 665; Height = 105; Padding = 3; WavePadding = $false },
    @{ Name = "Bar_LevelFill_Chevron_Flat.png"; X = 945; Y = 450; Width = 665; Height = 100; Padding = 3; WavePadding = $false },
    @{ Name = "Panel_BottomHud_Flat.png"; X = 15; Y = 608; Width = 1645; Height = 125; Padding = 4; WavePadding = $false },
    @{ Name = "Frame_StartCombat_Flat.png"; X = 225; Y = 732; Width = 540; Height = 209; Padding = 4; WavePadding = $false },
    @{ Name = "Frame_CostPlate_Flat.png"; X = 875; Y = 775; Width = 280; Height = 115; Padding = 4; WavePadding = $false },
    @{ Name = "Divider_Vertical_Flat.png"; X = 1235; Y = 765; Width = 70; Height = 150; Padding = 3; WavePadding = $false }
)

function Get-AlphaBounds {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [System.Drawing.Rectangle]$SearchRegion
    )

    $minX = $SearchRegion.Right
    $minY = $SearchRegion.Bottom
    $maxX = -1
    $maxY = -1

    for ($y = $SearchRegion.Top; $y -lt $SearchRegion.Bottom; $y++) {
        for ($x = $SearchRegion.Left; $x -lt $SearchRegion.Right; $x++) {
            if ($Bitmap.GetPixel($x, $y).A -eq 0) {
                continue
            }

            if ($x -lt $minX) { $minX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }

    if ($maxX -lt 0 -or $maxY -lt 0) {
        throw "No opaque pixels found in region $SearchRegion"
    }

    return [System.Drawing.Rectangle]::FromLTRB($minX, $minY, $maxX + 1, $maxY + 1)
}

function Get-FlatColor {
    param([System.Drawing.Color]$Color)

    # Image generation can leave faint semi-transparent edge noise. A firm
    # alpha threshold keeps the final runtime sprites genuinely clean and flat.
    if ($Color.A -lt 96) {
        return [System.Drawing.Color]::FromArgb(0, 0, 0, 0)
    }

    $redDominant = $Color.R -gt 70 -and $Color.R -gt ($Color.G * 1.35) -and $Color.R -gt ($Color.B * 1.25)
    if ($redDominant) {
        return [System.Drawing.Color]::FromArgb(255, 122, 46, 46)
    }

    $luma = (0.2126 * $Color.R) + (0.7152 * $Color.G) + (0.0722 * $Color.B)
    if ($luma -gt 112) {
        return [System.Drawing.Color]::FromArgb(255, 232, 217, 196)
    }

    # Collapse every dark shade into one charcoal. This intentionally removes
    # gradients, bevel shading, texture and black-noise variations.
    return [System.Drawing.Color]::FromArgb(255, 17, 17, 18)
}

$source = [System.Drawing.Bitmap]::FromFile($sourceAbsolute)

try {
    foreach ($definition in $definitions) {
        if ($OnlyName -and $definition.Name -ne $OnlyName) {
            continue
        }

        $region = [System.Drawing.Rectangle]::new(
            [int]$definition.X,
            [int]$definition.Y,
            [int]$definition.Width,
            [int]$definition.Height)

        $bounds = Get-AlphaBounds -Bitmap $source -SearchRegion $region
        $padding = [int]$definition.Padding
        $left = [Math]::Max(0, $bounds.Left - $padding)
        $top = [Math]::Max(0, $bounds.Top - $padding)
        $right = [Math]::Min($source.Width, $bounds.Right + $padding)
        $bottom = [Math]::Min($source.Height, $bounds.Bottom + $padding)
        $crop = [System.Drawing.Rectangle]::FromLTRB($left, $top, $right, $bottom)

        $trimmed = New-Object System.Drawing.Bitmap($crop.Width, $crop.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        for ($y = 0; $y -lt $crop.Height; $y++) {
            for ($x = 0; $x -lt $crop.Width; $x++) {
                $pixel = $source.GetPixel($crop.Left + $x, $crop.Top + $y)
                $trimmed.SetPixel($x, $y, (Get-FlatColor -Color $pixel))
            }
        }

        $output = $trimmed
        if ([bool]$definition.WavePadding) {
            $side = [Math]::Ceiling([Math]::Max($trimmed.Width, $trimmed.Height) / 0.70)
            $padded = New-Object System.Drawing.Bitmap($side, $side, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $graphics = [System.Drawing.Graphics]::FromImage($padded)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $offsetX = [int](($side - $trimmed.Width) / 2)
                $offsetY = [int](($side - $trimmed.Height) / 2)
                $graphics.DrawImageUnscaled($trimmed, $offsetX, $offsetY)
            }
            finally {
                $graphics.Dispose()
            }
            $output = $padded
        }

        try {
            $outputPath = Join-Path $outputAbsolute $definition.Name
            $output.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
            Write-Host "Created $outputPath ($($output.Width)x$($output.Height))"
        }
        finally {
            if ($output -ne $trimmed) {
                $output.Dispose()
            }
            $trimmed.Dispose()
        }
    }
}
finally {
    $source.Dispose()
}
