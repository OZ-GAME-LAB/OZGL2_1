param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path,
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$SourcePath,
    # 내부 기준점은 원본 전체 너비 기준 비율, 양 끝의 0/1은 배경 제거 후 경계이다.
    [string]$SourceXAnchors = '0,0.1015,0.190,0.2315,0.31,0.77,0.899,1',
    [string]$TargetXAnchors = '0,159,323,375,516,1490,1761,1919',
    [ValidateRange(0, 432)]
    [int]$FrameBandPixels = 24
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') {
    $arguments = @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath,
        '-ProjectRoot', $ProjectRoot, '-SourcePath', $SourcePath,
        '-SourceXAnchors', $SourceXAnchors, '-TargetXAnchors', $TargetXAnchors,
        '-FrameBandPixels', $FrameBandPixels
    )
    & "$env:SystemRoot/System32/WindowsPowerShell/v1.0/powershell.exe" @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Battle noble panel preparation failed.' }
    return
}

Add-Type -AssemblyName System.Drawing
$processor = @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

public static class BattleNoblePanelPreparation
{
    private const int OUTPUT_WIDTH = 1920;
    private const int OUTPUT_HEIGHT = 432;
    private const int Y_PADDING = 4;

    private static int GetAlpha(int pixel) { return (int)((uint)pixel >> 24); }

    private static bool IsBackdrop(int pixel)
    {
        int r = (pixel >> 16) & 255, g = (pixel >> 8) & 255, b = pixel & 255;
        int minimum = Math.Min(r, Math.Min(g, b));
        int maximum = Math.Max(r, Math.Max(g, b));
        return GetAlpha(pixel) < 16 || (minimum >= 45 && maximum - minimum <= 45);
    }

    private static bool IsGold(int pixel)
    {
        int r = (pixel >> 16) & 255, g = (pixel >> 8) & 255, b = pixel & 255;
        return r >= 75 && g >= 48 && g * 2 >= r && b * 100 <= g * 108;
    }

    private static int[] ReadPixels(Bitmap image)
    {
        int[] pixels = new int[checked(image.Width * image.Height)];
        Rectangle bounds = new Rectangle(0, 0, image.Width, image.Height);
        using (Bitmap converted = image.Clone(bounds, PixelFormat.Format32bppArgb))
        {
            BitmapData data = converted.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < image.Height; y++)
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * image.Width, image.Width);
            }
            finally { converted.UnlockBits(data); }
        }
        return pixels;
    }

    private static void SavePixels(string path, int[] pixels)
    {
        using (Bitmap image = new Bitmap(OUTPUT_WIDTH, OUTPUT_HEIGHT, PixelFormat.Format32bppArgb))
        {
            Rectangle bounds = new Rectangle(0, 0, OUTPUT_WIDTH, OUTPUT_HEIGHT);
            BitmapData data = image.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < OUTPUT_HEIGHT; y++)
                    Marshal.Copy(pixels, y * OUTPUT_WIDTH, IntPtr.Add(data.Scan0, y * data.Stride), OUTPUT_WIDTH);
            }
            finally { image.UnlockBits(data); }
            image.Save(path, ImageFormat.Png);
        }
    }

    private static void AddBackdrop(int point, int[] pixels, bool[] removed, int[] queue, ref int tail)
    {
        if (removed[point] || !IsBackdrop(pixels[point])) return;
        removed[point] = true;
        queue[tail++] = point;
    }

    private static void FloodBackdrop(int start, int width, int height, int[] pixels, bool[] removed, int[] queue)
    {
        int head = 0, tail = 0;
        AddBackdrop(start, pixels, removed, queue, ref tail);
        while (head < tail)
        {
            int point = queue[head++], x = point % width, y = point / width;
            if (x > 0) AddBackdrop(point - 1, pixels, removed, queue, ref tail);
            if (x + 1 < width) AddBackdrop(point + 1, pixels, removed, queue, ref tail);
            if (y > 0) AddBackdrop(point - width, pixels, removed, queue, ref tail);
            if (y + 1 < height) AddBackdrop(point + width, pixels, removed, queue, ref tail);
        }
    }

    private static Rectangle RemoveBackdrop(int[] pixels, int width, int height, out int removedCount)
    {
        bool[] removed = new bool[pixels.Length];
        int[] queue = new int[pixels.Length];
        for (int x = 0; x < width; x++)
        {
            FloodBackdrop(x, width, height, pixels, removed, queue);
            FloodBackdrop((height - 1) * width + x, width, height, pixels, removed, queue);
        }
        for (int y = 0; y < height; y++)
        {
            FloodBackdrop(y * width, width, height, pixels, removed, queue);
            FloodBackdrop(y * width + width - 1, width, height, pixels, removed, queue);
        }

        // 대각선으로 이어지는 장식은 같은 패널로 보존하고, 떨어진 배경 잔해만 제외한다.
        int[] labels = new int[pixels.Length];
        int label = 0, largestLabel = 0, largestCount = 0;
        for (int start = 0; start < pixels.Length; start++)
        {
            if (removed[start] || labels[start] != 0 || GetAlpha(pixels[start]) == 0) continue;
            label++;
            int head = 0, tail = 1;
            queue[0] = start;
            labels[start] = label;
            while (head < tail)
            {
                int point = queue[head++], x = point % width, y = point / width;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    int next = ny * width + nx;
                    if (removed[next] || labels[next] != 0 || GetAlpha(pixels[next]) == 0) continue;
                    labels[next] = label;
                    queue[tail++] = next;
                }
            }
            if (tail > largestCount) { largestLabel = label; largestCount = tail; }
        }
        if (largestCount == 0) throw new InvalidOperationException("Background removal produced an empty panel.");

        removedCount = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            if (labels[i] == largestLabel) continue;
            if (GetAlpha(pixels[i]) > 0) removedCount++;
            pixels[i] = 0;
        }
        return GetBounds(pixels, width, height);
    }

    private static Rectangle GetBounds(int[] pixels, int width, int height)
    {
        int left = width, top = height, right = -1, bottom = -1;
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            if (GetAlpha(pixels[y * width + x]) == 0) continue;
            left = Math.Min(left, x); right = Math.Max(right, x);
            top = Math.Min(top, y); bottom = Math.Max(bottom, y);
        }
        return right < left ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    private static double[] ParseAnchors(string text)
    {
        string[] parts = text.Split(',');
        double[] values = new double[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            values[i] = Double.Parse(parts[i].Trim(), CultureInfo.InvariantCulture);
            if (Double.IsNaN(values[i]) || Double.IsInfinity(values[i]))
                throw new ArgumentException("Anchor values must be finite numbers.");
        }
        return values;
    }

    private static int[] AlignPixels(int[] pixels, int sourceWidth, Rectangle crop, string sourceText, string targetText)
    {
        double[] source = ParseAnchors(sourceText), target = ParseAnchors(targetText);
        if (source.Length < 2 || source.Length != target.Length)
            throw new ArgumentException("Source and target anchors must have equal lengths of at least two.");
        int last = source.Length - 1;
        if (source[0] != 0 || source[last] != 1 || target[0] != 0 || target[last] != OUTPUT_WIDTH - 1)
            throw new ArgumentException("Source anchors must start/end at 0/1; target anchors at 0/1919.");
        for (int i = 1; i <= last; i++)
        {
            if (source[i] <= source[i - 1] || source[i] > 1 || target[i] <= target[i - 1])
                throw new ArgumentException("Source and target anchors must be strictly increasing.");
        }
        source[0] = crop.Left;
        source[last] = crop.Right - 1;
        for (int i = 1; i < last; i++) source[i] *= sourceWidth;
        for (int i = 1; i <= last; i++)
            if (source[i] <= source[i - 1])
                throw new ArgumentException("An internal source anchor lies outside the cropped panel; adjust SourceXAnchors.");

        int[] sourceXs = new int[OUTPUT_WIDTH];
        int segment = 0;
        for (int x = 0; x < OUTPUT_WIDTH; x++)
        {
            while (segment < last - 1 && x > target[segment + 1]) segment++;
            double ratio = (x - target[segment]) / (target[segment + 1] - target[segment]);
            sourceXs[x] = (int)Math.Round(source[segment] + (source[segment + 1] - source[segment]) * ratio);
            sourceXs[x] = Math.Max(crop.Left, Math.Min(crop.Right - 1, sourceXs[x]));
        }

        // 최근접 원본 픽셀만 선택하여 보간색이나 새로운 그림을 생성하지 않는다.
        int[] result = new int[OUTPUT_WIDTH * OUTPUT_HEIGHT];
        int bottom = OUTPUT_HEIGHT - Y_PADDING - 1;
        for (int y = Y_PADDING; y <= bottom; y++)
        {
            double ratio = (double)(y - Y_PADDING) / (bottom - Y_PADDING);
            int sourceY = crop.Top + (int)Math.Round(ratio * (crop.Height - 1));
            for (int x = 0; x < OUTPUT_WIDTH; x++)
                result[y * OUTPUT_WIDTH + x] = pixels[sourceY * sourceWidth + sourceXs[x]];
        }
        return result;
    }

    private static void AddDistance(int point, int value, int[] distances, int[] queue, ref int tail)
    {
        if (distances[point] != Int32.MaxValue) return;
        distances[point] = value;
        queue[tail++] = point;
    }

    private static int[] GetEdgeDistances(int[] pixels, int band)
    {
        int[] distances = new int[pixels.Length], queue = new int[pixels.Length];
        int head = 0, tail = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            distances[i] = GetAlpha(pixels[i]) == 0 ? 0 : Int32.MaxValue;
            if (distances[i] == 0) queue[tail++] = i;
        }
        // 캔버스 바깥도 투명으로 취급한다. 거리 0인 항목 뒤에 거리 1인 가장자리를 넣는다.
        for (int x = 0; x < OUTPUT_WIDTH; x++)
        {
            AddDistance(x, 1, distances, queue, ref tail);
            AddDistance((OUTPUT_HEIGHT - 1) * OUTPUT_WIDTH + x, 1, distances, queue, ref tail);
        }
        for (int y = 0; y < OUTPUT_HEIGHT; y++)
        {
            AddDistance(y * OUTPUT_WIDTH, 1, distances, queue, ref tail);
            AddDistance(y * OUTPUT_WIDTH + OUTPUT_WIDTH - 1, 1, distances, queue, ref tail);
        }
        // 상하좌우 인접 픽셀 기준으로 요청한 폭까지만 거리를 계산한다.
        while (head < tail)
        {
            int point = queue[head++], distance = distances[point];
            if (distance >= band) continue;
            int x = point % OUTPUT_WIDTH, y = point / OUTPUT_WIDTH;
            if (x > 0) AddDistance(point - 1, distance + 1, distances, queue, ref tail);
            if (x + 1 < OUTPUT_WIDTH) AddDistance(point + 1, distance + 1, distances, queue, ref tail);
            if (y > 0) AddDistance(point - OUTPUT_WIDTH, distance + 1, distances, queue, ref tail);
            if (y + 1 < OUTPUT_HEIGHT) AddDistance(point + OUTPUT_WIDTH, distance + 1, distances, queue, ref tail);
        }
        return distances;
    }

    private static string DescribeBounds(Rectangle bounds)
    {
        return String.Format(CultureInfo.InvariantCulture, "x={0},y={1},width={2},height={3}",
            bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    public static string Process(string root, string sourcePath, string sourceAnchors, string targetAnchors, int band)
    {
        root = Path.GetFullPath(root);
        sourcePath = Path.GetFullPath(sourcePath);
        string sourceDirectory = Path.Combine(root, "Tools/Art/Sources/BattleNobleBottomPanel");
        string outputDirectory = Path.Combine(root, "Assets/06.UI/BattleMutedPreview/NobleBottomPanel");
        string previewDirectory = Path.Combine(root, "Tools/Art/Previews");
        string storedSource = Path.GetFullPath(Path.Combine(sourceDirectory, "Panel_Velvet_Original.png"));
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Generated source image not found.", sourcePath);
        if (!String.Equals(sourcePath, storedSource, StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(storedSource))
                throw new IOException("The preserved source already exists. Re-run with SourcePath set to: " + storedSource);
            Directory.CreateDirectory(sourceDirectory);
            File.Copy(sourcePath, storedSource, false);
        }

        int[] cleaned;
        int sourceWidth, sourceHeight, removedCount;
        Rectangle crop;
        using (Bitmap original = new Bitmap(storedSource))
        {
            sourceWidth = original.Width; sourceHeight = original.Height;
            cleaned = ReadPixels(original);
            crop = RemoveBackdrop(cleaned, sourceWidth, sourceHeight, out removedCount);
        }
        int[] aligned = AlignPixels(cleaned, sourceWidth, crop, sourceAnchors, targetAnchors);
        int[] distances = GetEdgeDistances(aligned, band);
        int[] body = new int[aligned.Length], frame = new int[aligned.Length], composite = new int[aligned.Length];
        int bodyTransparent = 0, frameTransparent = 0, compositeTransparent = 0;
        int overlap = 0, missing = 0, mismatches = 0;
        for (int i = 0; i < aligned.Length; i++)
        {
            int pixel = aligned[i];
            if (GetAlpha(pixel) > 0)
            {
                // 각 원본 픽셀을 단 하나의 레이어에 배분해 원래 알파와 색을 그대로 보존한다.
                if (distances[i] <= band || IsGold(pixel)) frame[i] = pixel;
                else body[i] = pixel;
            }
            bool hasBody = GetAlpha(body[i]) > 0, hasFrame = GetAlpha(frame[i]) > 0;
            if (!hasBody) bodyTransparent++;
            if (!hasFrame) frameTransparent++;
            if (hasBody && hasFrame) overlap++;
            if (GetAlpha(pixel) > 0 && !hasBody && !hasFrame) missing++;
            composite[i] = hasFrame ? frame[i] : body[i];
            if (GetAlpha(composite[i]) == 0) compositeTransparent++;
            if (composite[i] != pixel) mismatches++;
        }
        if (overlap != 0 || missing != 0 || mismatches != 0)
            throw new InvalidOperationException("Layer split failed pixel reconstruction validation.");

        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(previewDirectory);
        string bodyPath = Path.Combine(outputDirectory, "Panel_Body.png");
        string framePath = Path.Combine(outputDirectory, "Panel_Frame.png");
        string previewPath = Path.Combine(previewDirectory, "BattleNoblePanel_Composite.png");
        SavePixels(bodyPath, body); SavePixels(framePath, frame); SavePixels(previewPath, composite);
        return String.Join(Environment.NewLine, new string[] {
            "Preserved source: " + storedSource,
            "Source size: " + sourceWidth + "x" + sourceHeight + " | removed=" + removedCount + " | crop=" + DescribeBounds(crop),
            "Canvas: " + OUTPUT_WIDTH + "x" + OUTPUT_HEIGHT + " | frameBand=" + band,
            "Transparent pixels: Body=" + bodyTransparent + " | Frame=" + frameTransparent + " | Composite=" + compositeTransparent,
            "Validation: overlap=" + overlap + " | missing=" + missing + " | pixelMismatches=" + mismatches,
            "Body bbox: " + DescribeBounds(GetBounds(body, OUTPUT_WIDTH, OUTPUT_HEIGHT)),
            "Frame bbox: " + DescribeBounds(GetBounds(frame, OUTPUT_WIDTH, OUTPUT_HEIGHT)),
            "Composite bbox: " + DescribeBounds(GetBounds(composite, OUTPUT_WIDTH, OUTPUT_HEIGHT)),
            "Body: " + bodyPath,
            "Frame: " + framePath,
            "Composite: " + previewPath
        });
    }
}
'@
Add-Type -TypeDefinition $processor -ReferencedAssemblies System.Drawing
[BattleNoblePanelPreparation]::Process($ProjectRoot, $SourcePath, $SourceXAnchors, $TargetXAnchors, $FrameBandPixels)
