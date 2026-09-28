param(
    [Parameter(Mandatory=$true)][string]$Source,
    [Parameter(Mandatory=$true)][string]$Destination
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
# 생성본의 외곽 8px 안티앨리어싱 띠는 보존하고 내부 알파만 255로 만든다.
[System.Drawing.Bitmap].GetInterfaces() | Out-Null
$waveReferences = @([AppDomain]::CurrentDomain.GetAssemblies() |
    Where-Object { $_.GetName().Name -match '^System\.(Drawing|Private\.Windows)' } |
    ForEach-Object { $_.Location } | Select-Object -Unique)
Add-Type -ReferencedAssemblies $waveReferences -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
public static class WaveFrameOpacity
{
    public static string Process(string source, string destination)
    {
        using (var bitmap = new Bitmap(source))
        {
            int width = bitmap.Width, height = bitmap.Height;
            var inside = new bool[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                inside[y * width + x] = bitmap.GetPixel(x, y).A >= 240;

            // 투명한 외부와 맞닿은 픽셀을 제외한다. 색상/형상은 재생성하지 않는다.
            for (int pass = 0; pass < 8; pass++)
            {
                var inset = new bool[inside.Length];
                for (int y = 1; y < height - 1; y++)
                for (int x = 1; x < width - 1; x++)
                {
                    int index = y * width + x;
                    inset[index] = inside[index] && inside[index - 1] && inside[index + 1]
                        && inside[index - width] && inside[index + width];
                }
                inside = inset;
            }
            int changed = 0;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (!inside[y * width + x]) continue;
                Color color = bitmap.GetPixel(x, y);
                if (color.A == 255) continue;
                bitmap.SetPixel(x, y, Color.FromArgb(255, color.R, color.G, color.B));
                changed++;
            }
            bitmap.Save(destination, ImageFormat.Png);
            return width + "x" + height + "; alpha-only corrected pixels=" + changed;
        }
    }
}
'@
[WaveFrameOpacity]::Process((Resolve-Path -LiteralPath $Source).Path, [System.IO.Path]::GetFullPath($Destination))
