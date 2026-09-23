param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path,
    [string]$WaveSource,
    [string]$SynergySource,
    [string]$ActionSource,
    [string]$BackgroundSource,
    [switch]$WaveOnly
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') {
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath, '-ProjectRoot', $ProjectRoot)
    foreach ($entry in @{'WaveSource'=$WaveSource; 'SynergySource'=$SynergySource; 'ActionSource'=$ActionSource; 'BackgroundSource'=$BackgroundSource}.GetEnumerator()) {
        if ($entry.Value) { $arguments += @((('-' + $entry.Key)), $entry.Value) }
    }
    if ($WaveOnly) { $arguments += '-WaveOnly' }
    & "$env:SystemRoot/System32/WindowsPowerShell/v1.0/powershell.exe" @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Battle reference art preparation failed.' }
    return
}

Add-Type -AssemblyName System.Drawing
$processor = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class BattleReferenceArtPreparation
{
    // 외부 및 빈 중앙에서 연결된 체크 배경만 제거한다. 검은 외곽선 안의 흰 하이라이트는 보존한다.
    private static bool IsBackdrop(Color pixel)
    {
        int maximum = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
        int minimum = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
        return pixel.A < 16 || (minimum > 102 && maximum - minimum < 40);
    }

    private static void Flood(int start, int width, int height, bool[] eligible, bool[] removed, int[] queue)
    {
        if (!eligible[start] || removed[start]) return;
        int head = 0, tail = 1;
        queue[0] = start;
        removed[start] = true;
        while (head < tail)
        {
            int point = queue[head++], x = point % width, y = point / width;
            if (x > 0) Add(point - 1, eligible, removed, queue, ref tail);
            if (x + 1 < width) Add(point + 1, eligible, removed, queue, ref tail);
            if (y > 0) Add(point - width, eligible, removed, queue, ref tail);
            if (y + 1 < height) Add(point + width, eligible, removed, queue, ref tail);
        }
    }

    private static void Add(int point, bool[] eligible, bool[] removed, int[] queue, ref int tail)
    {
        if (eligible[point] && !removed[point]) { removed[point] = true; queue[tail++] = point; }
    }

    private static Bitmap Extract(Bitmap original, bool hollow, bool square, bool waveBody)
    {
        int width = original.Width, height = original.Height, count = width * height;
        Color[] colors = new Color[count];
        bool[] eligible = new bool[count], removed = new bool[count];
        int[] queue = new int[count];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            int index = y * width + x;
            colors[index] = original.GetPixel(x, y);
            eligible[index] = IsBackdrop(colors[index]);
        }
        for (int x = 0; x < width; x++) { Flood(x, width, height, eligible, removed, queue); Flood((height - 1) * width + x, width, height, eligible, removed, queue); }
        for (int y = 0; y < height; y++) { Flood(y * width, width, height, eligible, removed, queue); Flood(y * width + width - 1, width, height, eligible, removed, queue); }
        if (hollow) Flood((height / 2) * width + width / 2, width, height, eligible, removed, queue);

        // Wave의 하단 세 칸에 생성기가 구운 짙은 회색 체크만 제거한다.
        // 헤더 위쪽은 제외하고, 검정 외곽선·붉은 구분선·아이보리 테두리는 색 조건으로 보존한다.
        if (waveBody)
        {
            int bodyLeft = (int)Math.Round(width * 0.048);
            int bodyRight = (int)Math.Round(width * 0.952);
            int bodyTop = (int)Math.Round(height * 0.332);
            int bodyBottom = (int)Math.Round(height * 0.676);
            for (int y = bodyTop; y <= bodyBottom; y++) for (int x = bodyLeft; x <= bodyRight; x++)
            {
                int index = y * width + x;
                Color pixel = colors[index];
                int maximum = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                int minimum = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
                if (minimum >= 12 && maximum <= 64 && maximum - minimum <= 8) removed[index] = true;
            }
        }

        // 배경 노이즈로 떨어진 작은 조각만 제거한다. 프레임 색·형태는 변경하지 않는다.
        bool[] seen = new bool[count];
        for (int start = 0; start < count; start++)
        {
            if (removed[start] || seen[start]) continue;
            int head = 0, tail = 1;
            queue[0] = start; seen[start] = true;
            while (head < tail)
            {
                int point = queue[head++], x = point % width, y = point / width;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    int next = ny * width + nx;
                    if (!removed[next] && !seen[next]) { seen[next] = true; queue[tail++] = next; }
                }
            }
            if (tail < 24) for (int i = 0; i < tail; i++) removed[queue[i]] = true;
        }

        int left = width, top = height, right = -1, bottom = -1;
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            if (!removed[y * width + x] && colors[y * width + x].A > 0)
            { left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
        if (right < left) throw new InvalidOperationException("Background removal produced an empty frame.");
        int cropWidth = right - left + 1, cropHeight = bottom - top + 1;
        int outputWidth = (square ? Math.Max(cropWidth, cropHeight) : cropWidth) + 8;
        int outputHeight = (square ? Math.Max(cropWidth, cropHeight) : cropHeight) + 8;
        int offsetX = (outputWidth - cropWidth) / 2, offsetY = (outputHeight - cropHeight) / 2;
        Bitmap result = new Bitmap(outputWidth, outputHeight, PixelFormat.Format32bppArgb);
        for (int y = top; y <= bottom; y++) for (int x = left; x <= right; x++)
            if (!removed[y * width + x]) result.SetPixel(offsetX + x - left, offsetY + y - top, colors[y * width + x]);
        return result;
    }

    public static string Process(string root, string name, string sourcePath, bool hollow, bool square, bool background)
    {
        string sourceDirectory = Path.Combine(root, "Tools/Art/Sources/BattleMutedReference_v2");
        string outputDirectory = Path.Combine(root, "Assets/06.UI/BattleMutedPreview/Reference_v2");
        Directory.CreateDirectory(sourceDirectory); Directory.CreateDirectory(outputDirectory);
        string storedSource = Path.Combine(sourceDirectory, name + "_Original.png");
        if (!String.IsNullOrEmpty(sourcePath))
        {
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("Generated source image not found", sourcePath);
            if (!String.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(storedSource), StringComparison.OrdinalIgnoreCase)) File.Copy(sourcePath, storedSource, true);
        }
        if (!File.Exists(storedSource)) return name + ": no source yet; skipped.";
        string outputPath = Path.Combine(outputDirectory, name + ".png");
        using (Bitmap original = new Bitmap(storedSource))
        {
            if (background)
            {
                File.Copy(storedSource, outputPath, true);
                return name + " | " + original.Width + "x" + original.Height + " | unchanged opaque background";
            }
            using (Bitmap result = Extract(original, hollow, square, name == "Frame_Wave"))
            {
                result.Save(outputPath, ImageFormat.Png);
                int clear = 0, opaque = 0;
                for (int y = 0; y < result.Height; y++) for (int x = 0; x < result.Width; x++)
                    if (result.GetPixel(x, y).A == 0) clear++; else opaque++;
                return name + " | " + result.Width + "x" + result.Height + " | transparent=" + clear + " opaque=" + opaque + " | centerAlpha=" + result.GetPixel(result.Width / 2, result.Height / 2).A;
            }
        }
    }
}
'@
Add-Type -TypeDefinition $processor -ReferencedAssemblies System.Drawing
[BattleReferenceArtPreparation]::Process($ProjectRoot, 'Frame_Wave', $WaveSource, $false, $false, $false)
if (-not $WaveOnly) {
    [BattleReferenceArtPreparation]::Process($ProjectRoot, 'Frame_DiamondSynergy', $SynergySource, $true, $true, $false)
    [BattleReferenceArtPreparation]::Process($ProjectRoot, 'Frame_DiamondAction', $ActionSource, $true, $true, $false)
    [BattleReferenceArtPreparation]::Process($ProjectRoot, 'Background_Obsidian', $BackgroundSource, $false, $false, $true)
}
