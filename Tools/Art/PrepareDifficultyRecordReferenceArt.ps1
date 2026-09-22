# 승인한 실루엣의 새 생성본만 분할한다. 원본/기존 PNG meta/아이콘/구분선은 유지한다.
param(
    [string]$GeneratedFrame = 'C:\Users\sudea\.codex\generated_images\01a0a4a8-3799-7db1-907e-69bb9af378b8\exec-c8749636-b863-476a-9c44-d04b8cea353a.png',
    [string]$TopFrame = 'C:\Users\sudea\.codex\generated_images\01a0a4a8-3799-7db1-907e-69bb9af378b8\exec-7ce2f107-45b0-46e3-bf36-d42c112e8058.png'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sourceRoot = Join-Path $PSScriptRoot 'Sources/DifficultyRecordHover'
$outputRoot = Join-Path $projectRoot 'Assets/06.UI/LobbyMutedPreview/DifficultySelection_v1/RecordHover/Sprites'
$previewRoot = Join-Path $projectRoot 'Temp/LobbyDifficulty/RecordHover/ReferenceCorrection'
New-Item -ItemType Directory -Force -Path $sourceRoot,$outputRoot,$previewRoot | Out-Null
# 생성 입력용으로 승인 목업의 실제 프레임 영역만 잘라 배경/다른 UI에 의한 해석 차이를 줄인다.
$reference = [Drawing.Bitmap]::new((Join-Path $projectRoot 'Docs/UI/References/LobbyDifficulty/difficulty_record_frame_hover_v2.png'))
try {
    $referenceCrop = $reference.Clone([Drawing.Rectangle]::new(994,491,524,241),[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try { $referenceCrop.Save((Join-Path $previewRoot 'ApprovedPanel_Crop.png'),[Drawing.Imaging.ImageFormat]::Png) } finally { $referenceCrop.Dispose() }
} finally { $reference.Dispose() }
$original = Join-Path $sourceRoot 'Frame_ApprovedSilhouette_Original.png'
if (!(Test-Path -LiteralPath $original)) { Copy-Item -LiteralPath $GeneratedFrame -Destination $original }

Add-Type -ReferencedAssemblies System.Drawing.Common,System.Drawing.Primitives,System.Collections,System.Runtime,System.Private.Windows.GdiPlus,System.Private.Windows.Core -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

public static class ApprovedDifficultyFrameParts
{
    // 바깥과 연결된 무채색 체크무늬만 제거한다. 내부 장식/불투명 바탕의 색은 변경하지 않는다.
    public static Bitmap RemoveExterior(Bitmap source)
    {
        int w=source.Width,h=source.Height;
        Bitmap result=new Bitmap(w,h,PixelFormat.Format32bppArgb);
        using(Graphics g=Graphics.FromImage(result)){g.CompositingMode=CompositingMode.SourceCopy;g.DrawImageUnscaled(source,0,0);}
        bool[] visited=new bool[w*h];Queue<int> queue=new Queue<int>();
        Action<int,int> visit=(x,y)=>{
            if(x<0||y<0||x>=w||y>=h)return;
            int i=y*w+x;if(visited[i])return;visited[i]=true;
            Color c=result.GetPixel(x,y);
            int min=Math.Min(c.R,Math.Min(c.G,c.B)),max=Math.Max(c.R,Math.Max(c.G,c.B));
            if(c.A==0||(min>65&&max-min<24)){queue.Enqueue(i);result.SetPixel(x,y,Color.Transparent);}
        };
        for(int x=0;x<w;x++){visit(x,0);visit(x,h-1);}
        for(int y=0;y<h;y++){visit(0,y);visit(w-1,y);}
        while(queue.Count>0){int i=queue.Dequeue(),x=i%w,y=i/w;visit(x-1,y);visit(x+1,y);visit(x,y-1);visit(x,y+1);}
        return result;
    }
    public static Rectangle Bounds(Bitmap image)
    {
        int l=image.Width,t=image.Height,r=-1,b=-1;
        for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)if(image.GetPixel(x,y).A>24){l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);b=Math.Max(b,y);}
        if(r<l)throw new InvalidOperationException("Visible pixels missing");
        return new Rectangle(l,t,r-l+1,b-t+1);
    }
    public static void SavePart(Bitmap source,Rectangle rect,int width,int height,string output)
    {
        using(Bitmap target=new Bitmap(width,height,PixelFormat.Format32bppArgb))
        using(Graphics g=Graphics.FromImage(target)){
            g.Clear(Color.Transparent);g.CompositingMode=CompositingMode.SourceCopy;
            g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
            g.DrawImage(source,new Rectangle(0,0,width,height),rect,GraphicsUnit.Pixel);
            target.Save(output,ImageFormat.Png);
        }
    }
}
'@
$source = [Drawing.Bitmap]::new($original)
try {
    $clean = [ApprovedDifficultyFrameParts]::RemoveExterior($source)
    try {
        $bounds = [ApprovedDifficultyFrameParts]::Bounds($clean)
        $clean.Save((Join-Path $previewRoot 'Frame_Clean.png'),[Drawing.Imaging.ImageFormat]::Png)
        # 상하 캡의 공통 전체 폭을 유지해 장식과 레일의 중심이 어긋나지 않게 한다.
        # 좌우 돌출 장식은 하단 캡에 포함하며 직선 레일은 안쪽 별도 좌표를 사용한다.
        if($source.Width -ne 1922 -or $source.Height -ne 818) { throw '다른 생성본은 분할 좌표를 재검토해야 합니다.' }
        $topCut=245; $bottomCut=494
        $leftRailStart=84; $leftRailEnd=143; $rightRailStart=1779; $rightRailEnd=1837
        $scale=512.0/$bounds.Width
        $topHeight=[int][Math]::Round(($topCut-$bounds.Top)*$scale)
        $bottomHeight=[int][Math]::Round(($bounds.Bottom-$bottomCut)*$scale)
        [ApprovedDifficultyFrameParts]::SavePart($clean,[Drawing.Rectangle]::new($bounds.Left,$bounds.Top,$bounds.Width,$topCut-$bounds.Top),512,$topHeight,(Join-Path $outputRoot 'Frame_Top.png'))
        [ApprovedDifficultyFrameParts]::SavePart($clean,[Drawing.Rectangle]::new($bounds.Left,$bottomCut,$bounds.Width,$bounds.Bottom-$bottomCut),512,$bottomHeight,(Join-Path $outputRoot 'Frame_Bottom.png'))
        [ApprovedDifficultyFrameParts]::SavePart($clean,[Drawing.Rectangle]::new($leftRailStart,340,$leftRailEnd-$leftRailStart,64),[int][Math]::Round(($leftRailEnd-$leftRailStart)*$scale),[int][Math]::Round(64*$scale),(Join-Path $outputRoot 'Frame_LeftRail.png'))
        [ApprovedDifficultyFrameParts]::SavePart($clean,[Drawing.Rectangle]::new($rightRailStart,340,$rightRailEnd-$rightRailStart,64),[int][Math]::Round(($rightRailEnd-$rightRailStart)*$scale),[int][Math]::Round(64*$scale),(Join-Path $outputRoot 'Frame_RightRail.png'))
        [ApprovedDifficultyFrameParts]::SavePart($clean,[Drawing.Rectangle]::new($leftRailEnd,$topCut,$rightRailStart-$leftRailEnd,$bottomCut-$topCut),[int][Math]::Round(($rightRailStart-$leftRailEnd)*$scale),[int][Math]::Round(($bottomCut-$topCut)*$scale),(Join-Path $outputRoot 'Frame_Body.png'))
        [PSCustomObject]@{Width=$source.Width;Height=$source.Height;Bounds=$bounds.ToString();TopHeight=$topHeight;BottomHeight=$bottomHeight;LeftInset=($leftRailStart-$bounds.Left)*$scale;RightInset=($bounds.Right-$rightRailEnd)*$scale;RailWidth=($leftRailEnd-$leftRailStart)*$scale;BodyWidth=($rightRailStart-$leftRailEnd)*$scale}
    } finally { $clean.Dispose() }
} finally { $source.Dispose() }

# 대각 어깨가 짧아지지 않도록 새 상단은 좌우/직선/중앙 장식으로 더 나눈다.
# 각 장식은 Unity에서 같은 비율로 축소하고 직선 부분의 길이만 조절한다.
$topOriginal = Join-Path $sourceRoot 'Frame_ReferenceMatched_Original.png'
if (!(Test-Path -LiteralPath $topOriginal)) { Copy-Item -LiteralPath $TopFrame -Destination $topOriginal }
$source = [Drawing.Bitmap]::new($topOriginal)
try {
    if($source.Width -ne 1851 -or $source.Height -ne 850) { throw '다른 상단 생성본은 분할 좌표를 재검토해야 합니다.' }
    $clean = [ApprovedDifficultyFrameParts]::RemoveExterior($source)
    try {
        $clean.Save((Join-Path $previewRoot 'Frame_ReferenceMatched_Clean.png'),[Drawing.Imaging.ImageFormat]::Png)
        [ApprovedDifficultyFrameParts]::SavePart($clean,[Drawing.Rectangle]::new(84,0,268,292),268,292,(Join-Path $outputRoot 'Frame_TopLeftShoulder.png'))
        [ApprovedDifficultyFrameParts]::SavePart($clean,[Drawing.Rectangle]::new(1499,0,268,292),268,292,(Join-Path $outputRoot 'Frame_TopRightShoulder.png'))
        [ApprovedDifficultyFrameParts]::SavePart($clean,[Drawing.Rectangle]::new(420,0,380,292),380,292,(Join-Path $outputRoot 'Frame_TopStrip.png'))
        [ApprovedDifficultyFrameParts]::SavePart($clean,[Drawing.Rectangle]::new(838,0,176,140),176,140,(Join-Path $outputRoot 'Frame_TopCrest.png'))
        [PSCustomObject]@{TopSource=$topOriginal;TopHeight=60;UniformScale=(60.0/292);ShoulderWidth=(268*60.0/292)}
    } finally { $clean.Dispose() }
} finally { $source.Dispose() }
