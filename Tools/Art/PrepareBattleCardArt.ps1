param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path,
    [string]$Only = '',
    [switch]$DiamondIcons
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') {
    $forwardArguments = @('-NoProfile','-ExecutionPolicy','Bypass','-File',$PSCommandPath,'-ProjectRoot',$ProjectRoot)
    if ($Only -ne '') { $forwardArguments += @('-Only',$Only) }
    if ($DiamondIcons) { $forwardArguments += '-DiamondIcons' }
    & "$env:SystemRoot/System32/WindowsPowerShell/v1.0/powershell.exe" @forwardArguments
    if ($LASTEXITCODE -ne 0) { throw 'Battle card art preparation failed.' }
    return
}
Add-Type -AssemblyName System.Drawing
$source = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class BattleCardArtPreparation
{
    private static int A(int p) { return (int)((uint)p >> 24); }
    private static bool Background(int p)
    {
        int r=(p>>16)&255,g=(p>>8)&255,b=p&255;
        int min=Math.Min(r,Math.Min(g,b)),max=Math.Max(r,Math.Max(g,b));
        return A(p)<16 || (min>=65 && max-min<=34);
    }
    private static void Add(int i,int[] p,bool[] gone,Queue<int> q)
    {
        if(gone[i] || !Background(p[i]))return;
        gone[i]=true;q.Enqueue(i);
    }
    public static string Prepare(string source,string output,int width,int height,bool white,bool fit,int diamondRadius=0)
    {
        using(var raw=new Bitmap(source))
        using(var src=raw.Clone(new Rectangle(0,0,raw.Width,raw.Height),PixelFormat.Format32bppArgb))
        {
            int w=src.Width,h=src.Height;var p=new int[w*h];
            var bits=src.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try{for(int y=0;y<h;y++)Marshal.Copy(IntPtr.Add(bits.Scan0,y*bits.Stride),p,y*w,w);}
            finally{src.UnlockBits(bits);}
            int originalClear=0;foreach(int pixel in p)if(A(pixel)<16)originalClear++;
            bool nativeAlpha=originalClear>p.Length/50;
            if(!nativeAlpha)
            {
                if(white)
                {
                    // 흰색 픽토그램의 생성 체크 배경만 제거하고 남은 색은 보존한다.
                    for(int i=0;i<p.Length;i++)
                    {
                        int r=(p[i]>>16)&255,g=(p[i]>>8)&255,b=p[i]&255;
                        if(Math.Min(r,Math.Min(g,b))<222 || Math.Max(r,Math.Max(g,b))-Math.Min(r,Math.Min(g,b))>36)p[i]=0;
                    }
                }
                else
                {
                    // 외부와 연결된 무채색 체크만 제거한다. 카드 내부와 삽화 내부는 채색하지 않는다.
                    var gone=new bool[p.Length];var q=new Queue<int>();
                    for(int x=0;x<w;x++){Add(x,p,gone,q);Add((h-1)*w+x,p,gone,q);}
                    for(int y=0;y<h;y++){Add(y*w,p,gone,q);Add(y*w+w-1,p,gone,q);}
                    while(q.Count>0)
                    {
                        int i=q.Dequeue(),x=i%w,y=i/w;
                        if(x>0)Add(i-1,p,gone,q);if(x+1<w)Add(i+1,p,gone,q);
                        if(y>0)Add(i-w,p,gone,q);if(y+1<h)Add(i+w,p,gone,q);
                    }
                    for(int i=0;i<p.Length;i++)if(gone[i])p[i]=0;
                }
            }
            // 알파 정리 후 남은 5픽셀 이하 배경 찌꺼기만 제외한다. 분리된 반짝임/석편은 유지한다.
            var seen=new bool[p.Length];var queue=new Queue<int>();var part=new List<int>();
            for(int i=0;i<p.Length;i++)
            {
                if(seen[i]||A(p[i])<16)continue;
                seen[i]=true;queue.Enqueue(i);part.Clear();
                while(queue.Count>0)
                {
                    int n=queue.Dequeue(),x=n%w,y=n/w;part.Add(n);
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                    {
                        int nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=w||ny>=h)continue;
                        int k=ny*w+nx;if(seen[k]||A(p[k])<16)continue;
                        seen[k]=true;queue.Enqueue(k);
                    }
                }
                if(part.Count<=5)foreach(int n in part)p[n]=0;
            }
            int left=w,top=h,right=-1,bottom=-1;
            for(int i=0;i<p.Length;i++)if(A(p[i])>=16)
            {
                left=Math.Min(left,i%w);right=Math.Max(right,i%w);
                top=Math.Min(top,i/w);bottom=Math.Max(bottom,i/w);
            }
            if(right<left||bottom<top)throw new InvalidOperationException("No visible art: "+source);
            int bw=right-left+1,bh=bottom-top+1,pad=4,dw=width-pad*2,dh=height-pad*2;
            if(fit)
            {
                double k=Math.Min(dw/(double)bw,dh/(double)bh);
                if(diamondRadius>0)
                {
                    // 모양은 변형하지 않고 알파 실루엣의 사선 경계에 맞춰 균등 축소한다.
                    double cx=(left+right)*0.5,cy=(top+bottom)*0.5,radius=1;
                    for(int i=0;i<p.Length;i++)if(A(p[i])>=16)
                        radius=Math.Max(radius,Math.Abs(i%w-cx)+Math.Abs(i/w-cy));
                    k=Math.Min(k,(diamondRadius-1)/radius);
                }
                dw=(int)Math.Round(bw*k);dh=(int)Math.Round(bh*k);
            }
            int ox=(width-dw)/2,oy=(height-dh)/2,clear=0;
            using(var dst=new Bitmap(width,height,PixelFormat.Format32bppArgb))
            {
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                {
                    int value=0;
                    if(x>=ox&&x<ox+dw&&y>=oy&&y<oy+dh)
                    {
                        int sx=left+Math.Min(bw-1,(int)((x-ox+0.5)*bw/dw));
                        int sy=top+Math.Min(bh-1,(int)((y-oy+0.5)*bh/dh));
                        value=p[sy*w+sx];
                    }
                    if(A(value)==0)clear++;
                    dst.SetPixel(x,y,Color.FromArgb(value));
                }
                dst.Save(output,ImageFormat.Png);
            }
            return System.IO.Path.GetFileName(output)+" | "+width+"x"+height+" | original="+w+"x"+h+" | nativeAlpha="+nativeAlpha+" | crop="+left+","+top+","+bw+","+bh+" | clear="+clear;
        }
    }
}
'@
Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing,System.Core
$inputDirectory=Join-Path $ProjectRoot 'Tools/Art/Sources/BattleCards_v1'
$outputDirectory=Join-Path $ProjectRoot 'Assets/06.UI/BattleMutedPreview/Cards_v1/Sprites'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$specs=@(
    @('CardShell_Unit',600,1000,$false,$false),
    @('CardShell_LandSlot',600,1000,$false,$false),
    @('CardShell_Relic',600,1000,$false,$false),
    @('Badge_Star',256,256,$false,$true),
    @('Icon_Sword',128,224,$true,$true),
    @('Icon_Mountain',192,144,$true,$true),
    @('Artwork_ShadowSwordsman',320,448,$false,$true),
    @('Artwork_ManaAmplifier',448,448,$false,$true)
)
if($DiamondIcons) {
    $inputDirectory=Join-Path $ProjectRoot 'Tools/Art/Sources/BattleCardDiamondIcons_v2'
    $specs=@(
        @('Icon_Type_Unit_Diamond_v2',256,256,$true,$true),
        @('Icon_Type_Land_Diamond_v2',256,256,$true,$true),
        @('Icon_Type_Relic_Diamond_v2',256,256,$true,$true)
    )
}
$diamondRadius=0
if($DiamondIcons) { $diamondRadius=112 }
foreach($spec in $specs) {
    if($Only -ne '' -and $Only -ne $spec[0]) { continue }
    [BattleCardArtPreparation]::Prepare((Join-Path $inputDirectory ($spec[0]+'_Original.png')),(Join-Path $outputDirectory ($spec[0]+'.png')),$spec[1],$spec[2],$spec[3],$spec[4],$diamondRadius)
}
