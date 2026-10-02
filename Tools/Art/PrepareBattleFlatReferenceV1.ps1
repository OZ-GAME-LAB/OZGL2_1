param(
    [string]$SourcePath = "Tools/Art/Sources/BattleFlatReference_v1/SourceKit.png",
    [string]$OutputDirectory = "Assets/06.UI/BattleMutedPreview/FlatReference_v1/Sprites",
    [string]$OnlyName = ""
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

# 팔레트 변환 후 남는 고립된 생성 노이즈만 제거한다. 긴 선과 마름모 장식은 유지한다.
if (-not ("BattleFlatPixelPolish" -as [type])) {
    Add-Type -ReferencedAssemblies @([System.Drawing.Bitmap].Assembly.Location, [System.Drawing.Rectangle].Assembly.Location) -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class BattleFlatPixelPolish
{
    public static void Clean(Bitmap bitmap)
    {
        var area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(area, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            int width = bitmap.Width, height = bitmap.Height;
            int[] pixels = new int[width * height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            for (int pass = 0; pass < 2; pass++)
            {
                int[] clean = (int[])pixels.Clone();
                for (int y = 1; y < height - 1; y++)
                for (int x = 1; x < width - 1; x++)
                {
                    int index = y * width + x;
                    for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int candidate = pixels[(y + oy) * width + x + ox];
                        if (candidate == pixels[index]) continue;
                        int count = 0;
                        for (int ny = -1; ny <= 1; ny++)
                        for (int nx = -1; nx <= 1; nx++)
                            if (pixels[(y + ny) * width + x + nx] == candidate) count++;
                        if (count >= 6) { clean[index] = candidate; oy = 2; break; }
                    }
                }
                pixels = clean;
            }
            // 투명 바탕에 떨어져 있는 작은 점 성분은 프레임으로 취급하지 않는다.
            bool[] visited = new bool[pixels.Length];
            int[] queue = new int[pixels.Length];
            for (int start = 0; start < pixels.Length; start++)
            {
                if (visited[start] || (uint)pixels[start] >> 24 < 40) continue;
                int head = 0, tail = 1;
                queue[0] = start;
                visited[start] = true;
                while (head < tail)
                {
                    int at = queue[head++], x = at % width, y = at / width;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx < 0 || xx >= width || yy < 0 || yy >= height) continue;
                        int next = yy * width + xx;
                        if (visited[next] || (uint)pixels[next] >> 24 < 40) continue;
                        visited[next] = true;
                        queue[tail++] = next;
                    }
                }
                if (tail < 24)
                    for (int i = 0; i < tail; i++) pixels[queue[i]] = 0;
            }
            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        }
        finally { bitmap.UnlockBits(data); }
    }
}
'@
}

$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$sourceAbsolute = Join-Path $projectRoot $SourcePath
$outputAbsolute = Join-Path $projectRoot $OutputDirectory

if (-not (Test-Path -LiteralPath $sourceAbsolute)) {
    throw "Source image not found: $sourceAbsolute"
}

New-Item -ItemType Directory -Path $outputAbsolute -Force | Out-Null

$definitions = @(
    @{ Name = "Frame_TopHud_Flat.png"; X = 0; Y = 50; Width = 1448; Height = 150; Padding = 4; Mode = "Frame"; Square = $false },
    @{ Name = "Frame_WavePreview_Flat.png"; X = 0; Y = 190; Width = 770; Height = 190; Padding = 4; Mode = "Frame"; Square = $false },
    @{ Name = "Frame_SynergyNameplate_Flat.png"; X = 770; Y = 190; Width = 678; Height = 190; Padding = 4; Mode = "Frame"; Square = $false },
    @{ Name = "Panel_BottomHud_Flat.png"; X = 0; Y = 380; Width = 1448; Height = 170; Padding = 4; Mode = "Frame"; Square = $false },
    @{ Name = "Frame_CurrencyDiamond_Flat.png"; X = 0; Y = 550; Width = 280; Height = 280; Padding = 4; Mode = "Frame"; Square = $true },
    @{ Name = "Frame_RerollDiamond_Flat.png"; X = 280; Y = 550; Width = 190; Height = 280; Padding = 4; Mode = "Frame"; Square = $true },
    @{ Name = "Frame_WaveToggle_Flat.png"; X = 470; Y = 550; Width = 160; Height = 280; Padding = 4; Mode = "Frame"; Square = $true },
    @{ Name = "Frame_MenuDiamond_Flat.png"; X = 630; Y = 550; Width = 200; Height = 280; Padding = 4; Mode = "Frame"; Square = $true },
    @{ Name = "Frame_DiamondLarge_Flat.png"; X = 630; Y = 550; Width = 200; Height = 280; Padding = 4; Mode = "Frame"; Square = $true },
    @{ Name = "Bar_LevelTrack_Chevron_Flat.png"; X = 820; Y = 550; Width = 628; Height = 150; Padding = 4; Mode = "Frame"; Square = $false },
    @{ Name = "Bar_LevelFill_Chevron_Flat.png"; X = 820; Y = 690; Width = 628; Height = 130; Padding = 4; Mode = "Frame"; Square = $false },
    @{ Name = "Frame_StartCombat_Flat.png"; X = 0; Y = 820; Width = 540; Height = 266; Padding = 4; Mode = "Frame"; Square = $false },
    @{ Name = "Icon_CrossedSwords_Casual.png"; X = 790; Y = 820; Width = 100; Height = 266; Padding = 4; Mode = "Icon"; Square = $true },
    @{ Name = "Icon_Hourglass_Casual.png"; X = 890; Y = 820; Width = 80; Height = 266; Padding = 4; Mode = "Icon"; Square = $true },
    @{ Name = "Icon_Menu_Casual.png"; X = 970; Y = 820; Width = 90; Height = 266; Padding = 4; Mode = "Icon"; Square = $true }
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
            if ($Bitmap.GetPixel($x, $y).A -lt 40) {
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
    param([System.Drawing.Color]$Color, [string]$Mode)

    # 생성 노이즈만 제거하고 사선 가장자리의 반투명 안티앨리어싱은 보존한다.
    if ($Color.A -lt 40) {
        return [System.Drawing.Color]::FromArgb(0, 0, 0, 0)
    }

    $luma = (0.2126 * $Color.R) + (0.7152 * $Color.G) + (0.0722 * $Color.B)
    if ($Mode -eq "Icon") {
        if ($luma -lt 92) { return [System.Drawing.Color]::FromArgb(0, 0, 0, 0) }
        return [System.Drawing.Color]::FromArgb($Color.A, 248, 244, 232)
    }

    $redDominant = $Color.R -gt 70 -and $Color.R -gt ($Color.G * 1.35) -and $Color.R -gt ($Color.B * 1.25)
    if ($redDominant) {
        return [System.Drawing.Color]::FromArgb($Color.A, 122, 46, 46)
    }

    if ($luma -gt 112) {
        return [System.Drawing.Color]::FromArgb($Color.A, 232, 217, 196)
    }
    if ($luma -lt 18) { return [System.Drawing.Color]::FromArgb($Color.A, 8, 8, 9) }
    return [System.Drawing.Color]::FromArgb($Color.A, 24, 24, 25)
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
                $trimmed.SetPixel($x, $y, (Get-FlatColor -Color $pixel -Mode $definition.Mode))
            }
        }

        $output = $trimmed
        [BattleFlatPixelPolish]::Clean($trimmed)
        if ([bool]$definition.Square) {
            $side = [Math]::Max($trimmed.Width, $trimmed.Height)
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

# 반복되는 장식은 정확한 정사각형과 동일한 선 굵기로 만든다. 프레임의 배경/테두리는 위에서 한 장으로 분리한다.
function Save-SimpleDecoration {
    param([string]$Name, [int]$Width, [int]$Height, [string]$Shape)
    if ($OnlyName -and $Name -ne $OnlyName) { return }
    $bitmap = New-Object System.Drawing.Bitmap($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $ivory = [System.Drawing.Color]::FromArgb(255, 232, 217, 196)
    $outline = [System.Drawing.Color]::FromArgb(255, 8, 8, 9)
    $pen = New-Object System.Drawing.Pen($ivory, 3)
    $brush = New-Object System.Drawing.SolidBrush($ivory)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        if ($Shape -eq "Diamond" -or $Shape -eq "TitleDiamond") {
            $points = [System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new($Width / 2, 3), [System.Drawing.PointF]::new($Width - 3, $Height / 2),
                [System.Drawing.PointF]::new($Width / 2, $Height - 3), [System.Drawing.PointF]::new(3, $Height / 2))
            if ($Shape -eq "TitleDiamond") {
                $brush.Color = $outline
                $graphics.FillPolygon($brush, $points)
                $pen.Color = [System.Drawing.Color]::FromArgb(255, 182, 49, 44)
                $graphics.DrawPolygon($pen, $points)
                $brush.Color = $ivory
                $graphics.FillRectangle($brush, ($Width / 2 - 2), ($Height / 2 - 2), 4, 4)
            } else {
                $graphics.FillPolygon($brush, $points)
                $pen.Color = $outline
                $pen.Width = 4
                $graphics.DrawPolygon($pen, $points)
            }
        } elseif ($Shape -eq "Chevron") {
            $pen.Color = [System.Drawing.Color]::FromArgb(255, 248, 244, 232)
            $pen.Width = 6
            $graphics.DrawLines($pen, [System.Drawing.PointF[]]@(
                [System.Drawing.PointF]::new(8, 13), [System.Drawing.PointF]::new(20, 25), [System.Drawing.PointF]::new(32, 13)))
        } else {
            $graphics.DrawLine($pen, ($Width / 2), 2, ($Width / 2), ($Height - 2))
            if ($Shape -eq "CappedDivider") {
                $graphics.DrawLine($pen, 1, 2, ($Width - 1), 2)
                $graphics.DrawLine($pen, 1, ($Height - 2), ($Width - 1), ($Height - 2))
            }
        }
        $bitmap.Save((Join-Path $outputAbsolute $Name), [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Host "Created $Name ($Width x $Height)"
    } finally {
        $brush.Dispose()
        $pen.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

Save-SimpleDecoration "Ornament_Diamond_Flat.png" 64 64 "Diamond"
Save-SimpleDecoration "Ornament_WaveTitle_Flat.png" 32 32 "TitleDiamond"
Save-SimpleDecoration "Divider_Vertical_Flat.png" 8 96 "Divider"
Save-SimpleDecoration "Divider_WaveUnit_Flat.png" 16 192 "CappedDivider"
Save-SimpleDecoration "Icon_WaveChevron_Casual.png" 40 40 "Chevron"
