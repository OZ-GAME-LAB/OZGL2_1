# 승인된 배경 제거/분할/크기 정렬만 수행한다. 생성 원본과 기존 게임 아트는 수정하지 않는다.
param([string]$GeneratedFrame = 'C:\Users\sudea\.codex\generated_images\01a0a4a8-3799-7db1-907e-69bb9af378b8\exec-29ce3210-e746-4d7c-8a1c-080b4d0ba057.png')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sourceRoot = Join-Path $PSScriptRoot 'Sources/DifficultyRecordHover'
$outputRoot = Join-Path $projectRoot 'Assets/06.UI/LobbyMutedPreview/DifficultySelection_v1/RecordHover/Sprites'
New-Item -ItemType Directory -Force -Path $sourceRoot,$outputRoot | Out-Null
$original = Join-Path $sourceRoot 'Frame_Generated_Original.png'
if (!(Test-Path -LiteralPath $original)) { Copy-Item -LiteralPath $GeneratedFrame -Destination $original }

Add-Type -ReferencedAssemblies System.Drawing.Common,System.Drawing.Primitives,System.Collections,System.Runtime,System.Private.Windows.GdiPlus,System.Private.Windows.Core -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

public static class DifficultyFrameParts
{
    // 테두리 바깥에서 연결된 밝은 무채색 체크무늬만 제거한다. 내부 금색 하이라이트는 유지한다.
    public static Bitmap RemoveExterior(Bitmap source)
    {
        int w=source.Width, h=source.Height;
        Bitmap result=new Bitmap(w,h,PixelFormat.Format32bppArgb);
        using(Graphics g=Graphics.FromImage(result)) { g.CompositingMode=CompositingMode.SourceCopy; g.DrawImageUnscaled(source,0,0); }
        bool[] visited=new bool[w*h];
        Queue<int> queue=new Queue<int>();
        Action<int,int> visit=(x,y)=>{
            if(x<0||y<0||x>=w||y>=h) return;
            int i=y*w+x; if(visited[i]) return; visited[i]=true;
            Color c=result.GetPixel(x,y);
            int min=Math.Min(c.R,Math.Min(c.G,c.B)), max=Math.Max(c.R,Math.Max(c.G,c.B));
            if(c.A==0 || (min>115 && max-min<45)) { queue.Enqueue(i); result.SetPixel(x,y,Color.Transparent); }
        };
        for(int x=0;x<w;x++) { visit(x,0); visit(x,h-1); }
        for(int y=0;y<h;y++) { visit(0,y); visit(w-1,y); }
        while(queue.Count>0) { int i=queue.Dequeue(), x=i%w, y=i/w; visit(x-1,y);visit(x+1,y);visit(x,y-1);visit(x,y+1); }
        return result;
    }
    public static Rectangle Bounds(Bitmap image)
    {
        int l=image.Width,t=image.Height,r=-1,b=-1;
        for(int y=0;y<image.Height;y++) for(int x=0;x<image.Width;x++)
        { if(image.GetPixel(x,y).A>24) { l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);b=Math.Max(b,y); } }
        if(r<l) throw new InvalidOperationException("Visible pixels missing");
        return new Rectangle(l,t,r-l+1,b-t+1);
    }
    public static void SavePart(Bitmap source, Rectangle rect, int width, int height, string output)
    {
        using(Bitmap target=new Bitmap(width,height,PixelFormat.Format32bppArgb))
        using(Graphics g=Graphics.FromImage(target))
        {
            g.Clear(Color.Transparent);
            g.CompositingMode=CompositingMode.SourceCopy;
            g.InterpolationMode=InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode=PixelOffsetMode.Half;
            g.DrawImage(source,new Rectangle(0,0,width,height),rect,GraphicsUnit.Pixel);
            target.Save(output,ImageFormat.Png);
        }
    }
    public static void SaveIcon(string input,string output)
    {
        using(Bitmap source=new Bitmap(input))
        {
            Rectangle bounds=Bounds(source);
            using(Bitmap target=new Bitmap(64,64,PixelFormat.Format32bppArgb))
            using(Graphics g=Graphics.FromImage(target))
            {
                float scale=Math.Min(56f/bounds.Width,56f/bounds.Height);
                int w=(int)Math.Round(bounds.Width*scale),h=(int)Math.Round(bounds.Height*scale);
                g.Clear(Color.Transparent);g.CompositingMode=CompositingMode.SourceCopy;
                g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
                g.DrawImage(source,new Rectangle((64-w)/2,(64-h)/2,w,h),bounds,GraphicsUnit.Pixel);
                target.Save(output,ImageFormat.Png);
            }
        }
    }
}
'@

$frameSource = [Drawing.Bitmap]::new($original)
try {
    $clean = [DifficultyFrameParts]::RemoveExterior($frameSource)
    try {
        $bounds = [DifficultyFrameParts]::Bounds($clean)
        # 기준 원본은 1774x887. 상하 장식 범위와 양쪽 레일을 서로 겹치지 않게 분리한다.
        if($frameSource.Width -ne 1774 -or $frameSource.Height -ne 887) { throw '다른 생성본은 분할 좌표를 재검토해야 합니다.' }
        $left=$bounds.Left; $right=$bounds.Right; $top=$bounds.Top; $bottom=$bounds.Bottom
        $topCut=188; $bottomCut=704; $leftCut=114; $rightCut=1661
        $scale=512.0/$bounds.Width
        [DifficultyFrameParts]::SavePart($clean,[Drawing.Rectangle]::new($left,$top,$bounds.Width,$topCut-$top),512,[int][Math]::Round(($topCut-$top)*$scale),(Join-Path $outputRoot 'Frame_Top.png'))
        [DifficultyFrameParts]::SavePart($clean,[Drawing.Rectangle]::new($left,$bottomCut,$bounds.Width,$bottom-$bottomCut),512,[int][Math]::Round(($bottom-$bottomCut)*$scale),(Join-Path $outputRoot 'Frame_Bottom.png'))
        [DifficultyFrameParts]::SavePart($clean,[Drawing.Rectangle]::new($left,420,$leftCut-$left,52),[int][Math]::Round(($leftCut-$left)*$scale),16,(Join-Path $outputRoot 'Frame_LeftRail.png'))
        [DifficultyFrameParts]::SavePart($clean,[Drawing.Rectangle]::new($rightCut,420,$right-$rightCut,52),[int][Math]::Round(($right-$rightCut)*$scale),16,(Join-Path $outputRoot 'Frame_RightRail.png'))
        [DifficultyFrameParts]::SavePart($clean,[Drawing.Rectangle]::new($leftCut,$topCut,$rightCut-$leftCut,$bottomCut-$topCut),480,128,(Join-Path $outputRoot 'Frame_Body.png'))
        [PSCustomObject]@{FrameBounds=$bounds.ToString();Source=$original;Output=$outputRoot}
    } finally { $clean.Dispose() }
} finally { $frameSource.Dispose() }
[DifficultyFrameParts]::SaveIcon((Join-Path $projectRoot 'Assets/06.UI/LobbyMutedPreview/Collections_v1/Sprites/CompletedTrophy_v2.png'),(Join-Path $outputRoot 'Icon_Trophy.png'))
[DifficultyFrameParts]::SaveIcon((Join-Path $projectRoot 'Assets/06.UI/LobbyMutedPreview/Overlays/TreeArt_v1/Sprites/Icons/Spells/Icon_Spells_Acceleration.png'),(Join-Path $outputRoot 'Icon_Hourglass.png'))
$divider=[Drawing.Bitmap]::new((Join-Path $projectRoot 'Assets/06.UI/LobbyMutedPreview/Overlays/DetailArt_v2/Divider_CurrentNext.png'))
try { $dividerBounds=[DifficultyFrameParts]::Bounds($divider); [DifficultyFrameParts]::SavePart($divider,$dividerBounds,440,24,(Join-Path $outputRoot 'Divider.png')) } finally { $divider.Dispose() }
Get-ChildItem -LiteralPath $outputRoot -Filter '*.png' | Select-Object Name,Length
