param([string]$ProjectRoot = 'D:/GitHub/OZGL2_1', [switch]$ExtractOnly)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') {
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath, '-ProjectRoot', $ProjectRoot)
    if ($ExtractOnly) { $arguments += '-ExtractOnly' }
    & "$env:SystemRoot/System32/WindowsPowerShell/v1.0/powershell.exe" @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Crest cleanup failed.' }
    return
}
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.IO;
using System.Collections.Generic;

public static class AugmentCrestCleanup
{
    static bool Backdrop(Color c) {
        int hi=Math.Max(c.R,Math.Max(c.G,c.B)),lo=Math.Min(c.R,Math.Min(c.G,c.B));
        return c.A<16 || (lo>=88 && hi-lo<=40);
    }
    static List<int> Flood(int seed,int w,int h,bool[] eligible,bool[] removed) {
        var found=new List<int>();
        if(removed[seed]||!eligible[seed]) return found;
        found.Add(seed); removed[seed]=true;
        for(int i=0;i<found.Count;i++) {
            int p=found[i],x=p%w,y=p/w;
            if(x>0) Add(p-1,eligible,removed,found);
            if(x+1<w) Add(p+1,eligible,removed,found);
            if(y>0) Add(p-w,eligible,removed,found);
            if(y+1<h) Add(p+w,eligible,removed,found);
        }
        return found;
    }
    static void Add(int p,bool[] e,bool[] r,List<int> q) { if(e[p]&&!r[p]) {r[p]=true;q.Add(p);} }
    static Rectangle Bounds(Bitmap b) {
        int l=b.Width,t=b.Height,r=-1,d=-1;
        for(int y=0;y<b.Height;y++) for(int x=0;x<b.Width;x++) if(b.GetPixel(x,y).A>0) {l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);d=Math.Max(d,y);}
        return r<l?Rectangle.Empty:new Rectangle(l,t,r-l+1,d-t+1);
    }
    public static string Extract(string input,string output,int cx,int cy) {
        using(var b=new Bitmap(input)) {
            int w=b.Width,h=b.Height,n=w*h; var colors=new Color[n];var eligible=new bool[n];var removed=new bool[n];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){int p=y*w+x;colors[p]=b.GetPixel(x,y);eligible[p]=Backdrop(colors[p]);}
            for(int x=0;x<w;x++){Flood(x,w,h,eligible,removed);Flood((h-1)*w+x,w,h,eligible,removed);}
            for(int y=0;y<h;y++){Flood(y*w,w,h,eligible,removed);Flood(y*w+w-1,w,h,eligible,removed);}
            var hole=Flood(cy*w+cx,w,h,eligible,removed);
            int hl=w,ht=h,hr=-1,hb=-1;
            foreach(int p in hole){hl=Math.Min(hl,p%w);hr=Math.Max(hr,p%w);ht=Math.Min(ht,p/w);hb=Math.Max(hb,p/w);}
            // 투명 배경에 고립된 작은 노이즈만 제거하고, 본체 금속 색은 그대로 보존한다.
            var seen=new bool[n];int noise=0;
            for(int start=0;start<n;start++){
                if(removed[start]||seen[start])continue;
                var q=new List<int>();q.Add(start);seen[start]=true;
                for(int i=0;i<q.Count;i++) {int p=q[i],x=p%w,y=p/w;for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){
                    int xx=x+dx,yy=y+dy;if(xx<0||xx>=w||yy<0||yy>=h)continue;int k=yy*w+xx;
                    if(!removed[k]&&!seen[k]){seen[k]=true;q.Add(k);}
                }}
                if(q.Count<32)foreach(int p in q){removed[p]=true;noise++;}
            }
            using(var clean=new Bitmap(w,h,PixelFormat.Format32bppArgb)) {
                for(int y=0;y<h;y++)for(int x=0;x<w;x++){int p=y*w+x;if(!removed[p])clean.SetPixel(x,y,colors[p]);}
                clean.Save(output,ImageFormat.Png);
                return Path.GetFileName(input)+" | source="+w+"x"+h+" visible="+Bounds(clean)+" hole="+new Rectangle(hl,ht,hr-hl+1,hb-ht+1)+" removedHole="+hole.Count+" noise="+noise;
            }
        }
    }
    public static void Preview(string[] paths,string output) {
        int size=512;
        using(var preview=new Bitmap(size*paths.Length,size,PixelFormat.Format32bppArgb))
        using(var g=Graphics.FromImage(preview)) {
            g.Clear(Color.FromArgb(25,20,24));g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
            for(int i=0;i<paths.Length;i++) using(var b=new Bitmap(paths[i])) g.DrawImage(b,new Rectangle(i*size,0,size,size));
            preview.Save(output,ImageFormat.Png);
        }
    }
    public static string Normalize(string input,string output,double cx,double cy,double holeWidth) {
        // 빈 아이콘 영역의 수평 지름만 공통화하고, 모든 장식·이름판은 같은 비율로 이동/축소한다.
        double scale=200.0/holeWidth;
        using(var source=new Bitmap(input))
        using(var target=new Bitmap(512,512,PixelFormat.Format32bppArgb)) {
            for(int y=0;y<512;y++)for(int x=0;x<512;x++){
                int sx=(int)Math.Floor((x+0.5-256)/scale+cx);
                int sy=(int)Math.Floor((y+0.5-220)/scale+cy);
                if(sx>=0&&sy>=0&&sx<source.Width&&sy<source.Height) target.SetPixel(x,y,source.GetPixel(sx,sy));
            }
            target.Save(output,ImageFormat.Png);
            int clear=0,opaque=0,partial=0,edgeOpaque=0,mirrorMismatch=0;
            var eligible=new bool[512*512];var visited=new bool[512*512];
            for(int y=0;y<512;y++)for(int x=0;x<512;x++){
                int alpha=target.GetPixel(x,y).A;
                if(alpha==0)clear++;else if(alpha==255)opaque++;else partial++;
                eligible[y*512+x]=alpha==0;
                if((x==0||y==0||x==511||y==511)&&alpha>0)edgeOpaque++;
                if((alpha>0)!=(target.GetPixel(511-x,y).A>0))mirrorMismatch++;
            }
            var hole=Flood(220*512+256,512,512,eligible,visited);
            int l=512,t=512,r=-1,b=-1;
            foreach(int p in hole){l=Math.Min(l,p%512);r=Math.Max(r,p%512);t=Math.Min(t,p/512);b=Math.Max(b,p/512);}
            bool gap=l==0||t==0||r==511||b==511;
            Rectangle bounds=Bounds(target);
            string report=Path.GetFileName(output)+" | canvas=512x512 | scale="+scale.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+" | visible="+bounds+" | hole="+new Rectangle(l,t,r-l+1,b-t+1)+" | clear="+clear+" opaque="+opaque+" partial="+partial+" centerAlpha="+target.GetPixel(256,220).A+" borderOpaque="+edgeOpaque+" holeConnectsOutside="+gap+" alphaMirrorMismatch="+mirrorMismatch;
            if(gap||edgeOpaque>0)throw new InvalidOperationException(report);
            return report;
        }
    }
    public static string PlaqueMetrics(string path,int plaqueTop,int sideScanTop,int textTop) {
        using(var b=new Bitmap(path)) {
            int l=512,r=-1,bottom=-1,minAlpha=255;
            for(int y=sideScanTop;y<512;y++)for(int x=0;x<512;x++)if(b.GetPixel(x,y).A>0){l=Math.Min(l,x);r=Math.Max(r,x);bottom=Math.Max(bottom,y);}
            Rectangle safe=new Rectangle(176,textTop,160,32);
            for(int y=safe.Top;y<safe.Bottom;y++)for(int x=safe.Left;x<safe.Right;x++)minAlpha=Math.Min(minAlpha,b.GetPixel(x,y).A);
            return Path.GetFileName(path)+" | plaqueApproxBounds="+new Rectangle(l,plaqueTop,r-l+1,bottom-plaqueTop+1)+" | plaqueCenter=("+((l+r+1)/2.0)+","+((plaqueTop+bottom+1)/2.0)+") | textSafeRect="+safe+" textSafeMinAlpha="+minAlpha;
        }
    }
}
'@
$work = Join-Path $PSScriptRoot 'Cleaned'
New-Item -ItemType Directory -Path $work -Force | Out-Null
$variants = @(
    @{Name='Silver'; X=627; Y=563},
    @{Name='Gold'; X=627; Y=500},
    @{Name='Platinum'; X=627; Y=628}
)
$cleanPaths = @()
foreach ($v in $variants) {
    $source = Join-Path $PSScriptRoot ('Raw/Crest_' + $v.Name + '.png')
    $clean = Join-Path $work ('Crest_' + $v.Name + '_Clean.png')
    [AugmentCrestCleanup]::Extract($source,$clean,$v.X,$v.Y)
    $cleanPaths += $clean
}
[AugmentCrestCleanup]::Preview($cleanPaths,(Join-Path $PSScriptRoot 'Crests_Extraction_Dark.png'))
if ($ExtractOnly) { return }
$output = Join-Path $ProjectRoot 'Assets/06.UI/BattleMutedPreview/Overlays_v1/Augment'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$layout = @(
    @{Name='Silver'; X=627.5; Y=562; Width=529; PlaqueTop=334; SideScan=358; TextTop=362},
    @{Name='Gold'; X=626.5; Y=504.5; Width=541; PlaqueTop=347; SideScan=366; TextTop=368},
    @{Name='Platinum'; X=627; Y=631.5; Width=540; PlaqueTop=331; SideScan=351; TextTop=350}
)
$finalPaths = @()
$metrics = @()
foreach ($v in $layout) {
    $clean = Join-Path $work ('Crest_' + $v.Name + '_Clean.png')
    $final = Join-Path $output ('Crest_' + $v.Name + '.png')
    $metrics += [AugmentCrestCleanup]::Normalize($clean,$final,$v.X,$v.Y,$v.Width)
    $metrics += [AugmentCrestCleanup]::PlaqueMetrics($final,$v.PlaqueTop,$v.SideScan,$v.TextTop)
    $finalPaths += $final
}
$metrics
$metrics | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'CleanupMetrics.txt') -Encoding UTF8
[AugmentCrestCleanup]::Preview($finalPaths,(Join-Path $PSScriptRoot 'Crests_Final_Dark.png'))
