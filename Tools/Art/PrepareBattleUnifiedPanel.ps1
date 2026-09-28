param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path,
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$SourcePath,
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$SourceTopPoints,
    [Parameter(Mandatory = $true)]
    [double]$SourceBottom,
    [string]$TargetTopPoints = '0,171;159,12;311,164;375,100;535,260;1509,260;1757,12;1919,174',
    [double]$TargetBottom = 420,
    [string]$OutputPath = 'Assets/06.UI/BattleMutedPreview/NobleBottomPanel/Panel_Unified.png'
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') {
    $arguments = @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath,
        '-ProjectRoot', $ProjectRoot, '-SourcePath', $SourcePath,
        '-SourceTopPoints', $SourceTopPoints,
        '-SourceBottom', $SourceBottom.ToString([Globalization.CultureInfo]::InvariantCulture),
        '-TargetTopPoints', $TargetTopPoints,
        '-TargetBottom', $TargetBottom.ToString([Globalization.CultureInfo]::InvariantCulture),
        '-OutputPath', $OutputPath
    )
    & "$env:SystemRoot/System32/WindowsPowerShell/v1.0/powershell.exe" @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Battle unified panel preparation failed.' }
    return
}

Add-Type -AssemblyName System.Drawing
$processor = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

public static class BattleUnifiedPanelPreparation
{
    private const int OUTPUT_WIDTH = 1920;
    private const int OUTPUT_HEIGHT = 432;
    private const int POINT_COUNT = 8;
    private const double EPSILON = 0.000001;

    private struct Point
    {
        public double X;
        public double Y;
        public Point(double x, double y) { X = x; Y = y; }
    }

    private static int GetAlpha(int pixel) { return (int)((uint)pixel >> 24); }
    private static bool IsFinite(double value) { return !Double.IsNaN(value) && !Double.IsInfinity(value); }
    private static string Number(double value) { return value.ToString("0.######", CultureInfo.InvariantCulture); }

    private static Point[] ParsePoints(string text, string name)
    {
        if (String.IsNullOrWhiteSpace(text)) throw new ArgumentException(name + " is required.");
        string[] entries = text.Split(';');
        if (entries.Length != POINT_COUNT) throw new ArgumentException(name + " must contain exactly eight x,y points.");
        Point[] points = new Point[POINT_COUNT];
        for (int i = 0; i < entries.Length; i++)
        {
            string[] pair = entries[i].Split(',');
            double x, y;
            if (pair.Length != 2 ||
                !Double.TryParse(pair[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
                !Double.TryParse(pair[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out y) ||
                !IsFinite(x) || !IsFinite(y))
                throw new ArgumentException(name + " contains an invalid or non-finite point at index " + i + ".");
            points[i] = new Point(x, y);
        }
        return points;
    }

    private static void ValidatePoints(Point[] points, double bottom, int width, int height, string name, bool isTarget)
    {
        if (!IsFinite(bottom) || bottom < 0 || bottom > height - 1)
            throw new ArgumentOutOfRangeException(name + "Bottom", "Bottom must lie inside the image.");
        for (int i = 0; i < points.Length; i++)
        {
            Point point = points[i];
            if (point.X < 0 || point.X > width - 1 || point.Y < 0 || point.Y > height - 1)
                throw new ArgumentOutOfRangeException(name, "Point " + i + " lies outside the image.");
            if (bottom - point.Y <= EPSILON)
                throw new ArgumentException(name + "Bottom must be strictly below every top point.");
            if (i == 0) continue;
            double dx = point.X - points[i - 1].X;
            double dy = point.Y - points[i - 1].Y;
            if (dx <= EPSILON) throw new ArgumentException(name + " X coordinates must be strictly increasing.");
            if (isTarget && Math.Abs(dy) > EPSILON && Math.Abs(Math.Abs(dy) - dx) > EPSILON)
                throw new ArgumentException("Target segment " + (i - 1) + " must have slope 0, +1, or -1.");
        }
        if (isTarget && (points[0].X != 0 || points[points.Length - 1].X != OUTPUT_WIDTH - 1))
            throw new ArgumentException("TargetTopPoints must start at x=0 and end at x=1919.");
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

    private static bool IsBackdrop(int pixel)
    {
        if (GetAlpha(pixel) == 0) return true;
        int r = (pixel >> 16) & 255, g = (pixel >> 8) & 255, b = pixel & 255;
        int minimum = Math.Min(r, Math.Min(g, b));
        int maximum = Math.Max(r, Math.Max(g, b));
        // 외곽과 이어진 밝은 무채색만 제거하며, 남은 픽셀의 색은 변경하지 않는다.
        return GetAlpha(pixel) == 255 && minimum >= 64 && maximum - minimum <= 32;
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

    private static void RemoveBackdrop(int[] pixels, int width, int height, out bool nativeAlpha, out int removedCount, out int componentCount)
    {
        nativeAlpha = false;
        foreach (int pixel in pixels)
            if (GetAlpha(pixel) < 255) { nativeAlpha = true; break; }
        bool[] removed = new bool[pixels.Length];
        int[] queue = new int[pixels.Length];
        // 실제 알파가 있는 원본은 무채색을 추가 제거하지 않고 원래 RGBA를 보존한다.
        if (!nativeAlpha)
        {
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
        }

        // 대각선으로 이어지는 테두리도 같은 연결 성분으로 보고 가장 큰 패널만 남긴다.
        int[] labels = new int[pixels.Length];
        int largestLabel = 0, largestCount = 0;
        componentCount = 0;
        for (int start = 0; start < pixels.Length; start++)
        {
            if (removed[start] || labels[start] != 0 || GetAlpha(pixels[start]) == 0) continue;
            componentCount++;
            int head = 0, tail = 1;
            queue[0] = start;
            labels[start] = componentCount;
            while (head < tail)
            {
                int point = queue[head++], x = point % width, y = point / width;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    int next = ny * width + nx;
                    if (removed[next] || labels[next] != 0 || GetAlpha(pixels[next]) == 0) continue;
                    labels[next] = componentCount;
                    queue[tail++] = next;
                }
            }
            if (tail > largestCount) { largestLabel = componentCount; largestCount = tail; }
        }
        if (largestCount == 0) throw new InvalidOperationException("Background removal produced an empty panel.");
        removedCount = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            if (labels[i] == largestLabel) continue;
            if (GetAlpha(pixels[i]) > 0) removedCount++;
            pixels[i] = 0;
        }
    }

    private static int[] AlignPixels(int[] pixels, int width, int height, Point[] source, double sourceBottom, Point[] target, double targetBottom)
    {
        int[] result = new int[OUTPUT_WIDTH * OUTPUT_HEIGHT];
        int segment = 0;
        for (int x = 0; x < OUTPUT_WIDTH; x++)
        {
            while (segment < POINT_COUNT - 2 && x > target[segment + 1].X) segment++;
            double t = (x - target[segment].X) / (target[segment + 1].X - target[segment].X);
            double sourceX = source[segment].X + t * (source[segment + 1].X - source[segment].X);
            double sourceTop = source[segment].Y + t * (source[segment + 1].Y - source[segment].Y);
            double targetTop = target[segment].Y + t * (target[segment + 1].Y - target[segment].Y);
            int sx = (int)Math.Round(sourceX, MidpointRounding.AwayFromZero);
            for (int y = 0; y < OUTPUT_HEIGHT; y++)
            {
                // 상단선·하단선 바깥도 동일한 변환을 적용해 경계 밖의 장식을 보존한다.
                double fraction = (y - targetTop) / (targetBottom - targetTop);
                double sourceY = sourceTop + fraction * (sourceBottom - sourceTop);
                if (sourceY < 0 || sourceY > height - 1) continue;
                int sy = (int)Math.Round(sourceY, MidpointRounding.AwayFromZero);
                // 최근접 원본 RGBA를 그대로 사용한다. 새 색·도형·보간 픽셀은 생성하지 않는다.
                result[y * OUTPUT_WIDTH + x] = pixels[sy * width + sx];
            }
        }
        return result;
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

    private static string DescribeBounds(Rectangle bounds)
    {
        return "x=" + bounds.X + ",y=" + bounds.Y + ",width=" + bounds.Width + ",height=" + bounds.Height;
    }

    private static string DescribePixel(int pixel)
    {
        return "(" + ((pixel >> 16) & 255) + "," + ((pixel >> 8) & 255) + "," + (pixel & 255) + "," + GetAlpha(pixel) + ")";
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

    public static string Process(string root, string sourcePath, string sourcePoints, double sourceBottom, string targetPoints, double targetBottom, string outputRelativePath)
    {
        root = Path.GetFullPath(root);
        sourcePath = Path.GetFullPath(sourcePath);
        string outputPath = Path.GetFullPath(Path.Combine(root, outputRelativePath));
        if (!outputPath.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("OutputPath must remain inside the project.");
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Generated source image not found.", sourcePath);
        if (String.Equals(sourcePath, outputPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SourcePath must not be the output image; the original is read-only.");
        Point[] source = ParsePoints(sourcePoints, "SourceTopPoints");
        Point[] target = ParsePoints(targetPoints, "TargetTopPoints");
        ValidatePoints(target, targetBottom, OUTPUT_WIDTH, OUTPUT_HEIGHT, "Target", true);

        int width, height;
        int[] cleaned;
        using (Bitmap original = new Bitmap(sourcePath))
        {
            width = original.Width; height = original.Height;
            ValidatePoints(source, sourceBottom, width, height, "Source", false);
            cleaned = ReadPixels(original);
        }
        bool nativeAlpha;
        int removedCount, componentCount;
        RemoveBackdrop(cleaned, width, height, out nativeAlpha, out removedCount, out componentCount);
        Rectangle sourceBounds = GetBounds(cleaned, width, height);
        int[] aligned = AlignPixels(cleaned, width, height, source, sourceBottom, target, targetBottom);
        Rectangle outputBounds = GetBounds(aligned, OUTPUT_WIDTH, OUTPUT_HEIGHT);
        if (outputBounds.IsEmpty) throw new InvalidOperationException("Mapping produced an empty panel; check source landmarks.");

        int transparent = 0, partialAlpha = 0;
        int minR = 255, minG = 255, minB = 255, minA = 255;
        int maxR = 0, maxG = 0, maxB = 0, maxA = 0;
        foreach (int pixel in aligned)
        {
            int alpha = GetAlpha(pixel);
            if (alpha == 0) { transparent++; continue; }
            if (alpha < 255) partialAlpha++;
            int r = (pixel >> 16) & 255, g = (pixel >> 8) & 255, b = pixel & 255;
            minR = Math.Min(minR, r); maxR = Math.Max(maxR, r);
            minG = Math.Min(minG, g); maxG = Math.Max(maxG, g);
            minB = Math.Min(minB, b); maxB = Math.Max(maxB, b);
            minA = Math.Min(minA, alpha); maxA = Math.Max(maxA, alpha);
        }
        List<string> report = new List<string>();
        report.Add("Source (unchanged): " + sourcePath);
        report.Add("Source size: " + width + "x" + height + " | nativeAlpha=" + nativeAlpha + " | removedPixels=" + removedCount + " | components=" + componentCount);
        report.Add("Source color bounds: " + DescribeBounds(sourceBounds));
        report.Add("Output canvas: " + OUTPUT_WIDTH + "x" + OUTPUT_HEIGHT + " | bottom=" + Number(targetBottom));
        report.Add("Output color bounds: " + DescribeBounds(outputBounds));
        report.Add("Transparent pixels: " + transparent + " | partialAlphaPixels=" + partialAlpha);
        report.Add("Visible RGBA ranges: R=" + minR + ".." + maxR + ",G=" + minG + ".." + maxG + ",B=" + minB + ".." + maxB + ",A=" + minA + ".." + maxA);
        report.Add("Corners RGBA: TL=" + DescribePixel(aligned[0]) + " | TR=" + DescribePixel(aligned[OUTPUT_WIDTH - 1]) +
            " | BL=" + DescribePixel(aligned[(OUTPUT_HEIGHT - 1) * OUTPUT_WIDTH]) + " | BR=" + DescribePixel(aligned[aligned.Length - 1]));
        report.Add("Segment tangent report (landmark mapping; final visible border still requires pixel inspection):");
        for (int i = 0; i < POINT_COUNT - 1; i++)
        {
            double sourceDx = source[i + 1].X - source[i].X, sourceDy = source[i + 1].Y - source[i].Y;
            double targetDx = target[i + 1].X - target[i].X, targetDy = target[i + 1].Y - target[i].Y;
            double slope = targetDy / targetDx;
            report.Add("  " + i + ": sourceTan=" + Number(sourceDy / sourceDx) + " -> targetTan=" + Number(slope) +
                " | targetAngle=" + Number(Math.Atan(slope) * 180 / Math.PI) + "deg | targetDelta=(" + Number(targetDx) + "," + Number(targetDy) + ")");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        SavePixels(outputPath, aligned);
        report.Add("Output: " + outputPath);
        return String.Join(Environment.NewLine, report.ToArray());
    }
}
'@
Add-Type -TypeDefinition $processor -ReferencedAssemblies System.Drawing
[BattleUnifiedPanelPreparation]::Process($ProjectRoot, $SourcePath, $SourceTopPoints, $SourceBottom, $TargetTopPoints, $TargetBottom, $OutputPath)
