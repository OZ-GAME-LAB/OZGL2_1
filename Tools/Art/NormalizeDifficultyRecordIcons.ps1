# 새 생성 원본은 보존한다. 알파 영역 트리밍/비율 유지 크기 정렬/투명 여백 추가만 수행한다.
param(
    [string]$TrophySource = 'C:/Users/sudea/.codex/generated_images/01a0a4a8-3799-7db1-907e-69bb9af378b8/exec-352a08ad-b30b-4f0a-b2a0-31b74eb811f3.png',
    [string]$HourglassSource = 'C:/Users/sudea/.codex/generated_images/01a0a4a8-3799-7db1-907e-69bb9af378b8/exec-e576d546-6c5f-4558-8ca3-cd432cc4b723.png'
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$originalRoot = Join-Path $PSScriptRoot 'Sources/DifficultyRecordHover'
$outputRoot = Join-Path $projectRoot 'Assets/06.UI/LobbyMutedPreview/DifficultySelection_v1/RecordHover/Sprites'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing.Common,System.Drawing.Primitives,System.Runtime,System.Private.Windows.GdiPlus,System.Private.Windows.Core -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
public static class RecordIconNormalization
{
    public static string Normalize(string input,string output)
    {
        using(Bitmap source=new Bitmap(input))
        {
            if(source.GetPixel(0,0).A>0) throw new InvalidOperationException("Expected generated transparent alpha background");
            int left=source.Width,top=source.Height,right=-1,bottom=-1;
            for(int y=0;y<source.Height;y++) for(int x=0;x<source.Width;x++)
                if(source.GetPixel(x,y).A>=128){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
            if(right<left) throw new InvalidOperationException("Icon has no opaque shape");
            Rectangle bounds=new Rectangle(left,top,right-left+1,bottom-top+1);
            const int canvas=256,visible=224;
            double scale=(double)visible/Math.Max(bounds.Width,bounds.Height);
            // 양 변의 길이를 짝수로 맞춰 정확히 같은 투명 여백과 128,128 중심을 갖는다.
            int width=Math.Max(2,(int)Math.Round(bounds.Width*scale/2)*2);
            int height=Math.Max(2,(int)Math.Round(bounds.Height*scale/2)*2);
            Rectangle target=new Rectangle((canvas-width)/2,(canvas-height)/2,width,height);
            using(Bitmap result=new Bitmap(canvas,canvas,PixelFormat.Format32bppArgb))
            using(Graphics graphics=Graphics.FromImage(result))
            {
                graphics.CompositingMode=CompositingMode.SourceCopy;
                graphics.InterpolationMode=InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode=PixelOffsetMode.Half;
                graphics.Clear(Color.Transparent);
                graphics.DrawImage(source,target,bounds,GraphicsUnit.Pixel);
                result.Save(output,ImageFormat.Png);
            }
            return output+" | sourceBounds="+bounds+" targetBounds="+target+" center=(128,128)";
        }
    }
}
'@
$items = @(
    @{Source=$TrophySource; Original='Icon_Trophy_Generated_v2.png'; Output='Icon_Trophy_Centered_v2.png'},
    @{Source=$HourglassSource; Original='Icon_Hourglass_Generated_v2.png'; Output='Icon_Hourglass_Centered_v2.png'}
)
foreach($item in $items){
    $original = Join-Path $originalRoot $item.Original
    if(!(Test-Path -LiteralPath $original)){Copy-Item -LiteralPath $item.Source -Destination $original}
    [RecordIconNormalization]::Normalize($original,(Join-Path $outputRoot $item.Output))
}
