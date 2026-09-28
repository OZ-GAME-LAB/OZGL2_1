param([string]$ProjectRoot='D:/GitHub/OZGL2_1')
$ErrorActionPreference='Stop'
if($PSVersionTable.PSEdition -eq 'Core') {
    & "$env:SystemRoot/System32/WindowsPowerShell/v1.0/powershell.exe" -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -ProjectRoot $ProjectRoot
    if($LASTEXITCODE -ne 0){throw 'Description panel extraction failed.'}
    return
}
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing,System.Core -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class DescriptionPanelExtraction {
    static int Alpha(int p){return (int)((uint)p>>24);}
    static int[] Load(Bitmap image){
        int[] pixels=new int[image.Width*image.Height];
        var bits=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try{for(int y=0;y<image.Height;y++)Marshal.Copy(IntPtr.Add(bits.Scan0,y*bits.Stride),pixels,y*image.Width,image.Width);}
        finally{image.UnlockBits(bits);}
        return pixels;
    }
    static void Save(string path,int[] pixels,int width,int height){
        using(var image=new Bitmap(width,height,PixelFormat.Format32bppArgb)){
            var bits=image.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            try{for(int y=0;y<height;y++)Marshal.Copy(pixels,y*width,IntPtr.Add(bits.Scan0,y*bits.Stride),width);}
            finally{image.UnlockBits(bits);}
            image.Save(path,ImageFormat.Png);
        }
    }
    static bool InPolygon(double x,double y,PointF[] poly){
        bool inside=false;
        for(int i=0,j=poly.Length-1;i<poly.Length;j=i++){
            var a=poly[i];var b=poly[j];
            if(((a.Y>y)!=(b.Y>y))&&(x<(b.X-a.X)*(y-a.Y)/(b.Y-a.Y)+a.X))inside=!inside;
        }
        return inside;
    }
    // Exact source-pixel mask: no drawing, recoloring, resizing, or repositioning.
    static PointF[] Outline(string tier){
        if(tier=="Silver") return new[]{new PointF(99,232),new PointF(501,232),new PointF(505,238),new PointF(505,810),new PointF(315,953),new PointF(316,957),new PointF(301,977),new PointF(297,977),new PointF(283,957),new PointF(284,953),new PointF(94,810),new PointF(94,238)};
        if(tier=="Gold") return new[]{new PointF(112,229),new PointF(487,229),new PointF(494,235),new PointF(494,803),new PointF(314,926),new PointF(315,931),new PointF(302,947),new PointF(297,947),new PointF(284,931),new PointF(285,926),new PointF(105,803),new PointF(105,235)};
        return new[]{new PointF(122,168),new PointF(477,168),new PointF(480,174),new PointF(480,814),new PointF(314,934),new PointF(315,938),new PointF(302,958),new PointF(296,958),new PointF(283,938),new PointF(284,934),new PointF(119,814),new PointF(119,174)};
    }
    static bool IsVelvet(int p,string tier){
        int r=(p>>16)&255,g=(p>>8)&255,b=p&255;
        return r>g*(tier=="Gold"?4.0:1.35)+3 && r>b*1.30+3;
    }
    public static string Extract(string tier,string input,string panelPath,string maskPath){
        using(var original=new Bitmap(input))
        using(var source=original.Clone(new Rectangle(0,0,original.Width,original.Height),PixelFormat.Format32bppArgb)){
            int w=source.Width,h=source.Height;
            if(w!=600||h!=1100)throw new Exception("Unexpected source canvas");
            var src=Load(source);var output=new int[src.Length];var mask=new int[src.Length];var keep=new bool[src.Length];
            var poly=Outline(tier);
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)keep[y*w+x]=InPolygon(x+0.5,y+0.5,poly)&&Alpha(src[y*w+x])>0;
            // Remove red velvet connected to the mask boundary; preserve enclosed gems.
            var pending=new Queue<int>();var seen=new bool[src.Length];
            for(int y=1;y<h-1;y++)for(int x=1;x<w-1;x++){
                int k=y*w+x;if(keep[k]&&IsVelvet(src[k],tier)&&(!keep[k-1]||!keep[k+1]||!keep[k-w]||!keep[k+w])){pending.Enqueue(k);seen[k]=true;}
            }
            while(pending.Count>0){
                int k=pending.Dequeue();keep[k]=false;
                foreach(int n in new[]{k-1,k+1,k-w,k+w})if(n>=0&&n<src.Length&&keep[n]&&!seen[n]&&IsVelvet(src[n],tier)){seen[n]=true;pending.Enqueue(n);}
            }
            int opaque=0,left=w,top=h,right=-1,bottom=-1,difference=0,border=0;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){
                int k=y*w+x;if(!keep[k])continue;
                output[k]=src[k];mask[k]=unchecked((int)0xffffffff);opaque++;
                left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
                if(output[k]!=src[k])difference++;
                if(x==0||x==w-1||y==0||y==h-1)border++;
            }
            Save(panelPath,output,w,h);Save(maskPath,mask,w,h);
            using(var savedPanel=new Bitmap(panelPath))using(var savedMask=new Bitmap(maskPath)){
                var verified=Load(savedPanel);var verifiedMask=Load(savedMask);
                for(int i=0;i<src.Length;i++){
                    if(keep[i]&&verified[i]!=src[i])difference++;
                    if(Alpha(verified[i])!=Alpha(verifiedMask[i]))throw new Exception("Mask alpha mismatch: "+tier);
                    if(!keep[i]&&verified[i]!=0)throw new Exception("Nonzero transparent pixel: "+tier);
                }
            }
            // Keep the text-safe rectangle away from the rim and corner ornament.
            Rectangle safe=tier=="Platinum"?new Rectangle(145,205,310,580):tier=="Gold"?new Rectangle(130,270,340,505):new Rectangle(120,275,360,510);
            int safeTransparent=0;for(int y=safe.Top;y<safe.Bottom;y++)for(int x=safe.Left;x<safe.Right;x++)if(Alpha(output[y*w+x])!=255)safeTransparent++;
            int[] probes={0,300,1099*w+599,100*w+300,1080*w+300,500*w+80,500*w+520};
            int outsideFailures=0;foreach(int p in probes)if(Alpha(output[p])!=0)outsideFailures++;
            if(difference!=0||border!=0||safeTransparent!=0||outsideFailures!=0)throw new Exception("Validation failed: "+tier);
            return String.Format("{0}|canvas=600x1100|panelBounds={1},{2},{3},{4}|opaque={5}|transparent={6}|sourcePixelDifferences={7}|canvasBorderOpaque={8}|outsideProbeFailures={9}|centerAlpha={10}|tmpSafeRect={11},{12},{13},{14}|tmpNonOpaque={15}",tier,left,top,right-left+1,bottom-top+1,opaque,src.Length-opaque,difference,border,outsideFailures,Alpha(output[550*w+300]),safe.X,safe.Y,safe.Width,safe.Height,safeTransparent);
        }
    }
}
'@
$assetRoot=Join-Path $ProjectRoot 'Assets/06.UI/BattleMutedPreview/Overlays_v1/Augment'
$outputRoot=Join-Path $assetRoot 'Separated'
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$metrics=[Collections.Generic.List[string]]::new()
foreach($tier in @('Silver','Gold','Platinum')){
    $input=Join-Path $assetRoot ('Augment_Body_'+$tier+'.png')
    $hashBefore=(Get-FileHash -LiteralPath $input -Algorithm SHA256).Hash
    $metrics.Add([DescriptionPanelExtraction]::Extract($tier,$input,(Join-Path $outputRoot ('DescriptionPanel_'+$tier+'.png')),(Join-Path $PSScriptRoot ('Mask_'+$tier+'.png'))))
    $hashAfter=(Get-FileHash -LiteralPath $input -Algorithm SHA256).Hash
    if($hashBefore -ne $hashAfter){throw 'Source image changed.'}
    $metrics.Add($tier+'|originalSHA256='+$hashBefore+'|originalHashUnchanged=True')
}
$metrics | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'ExtractionMetrics.txt') -Encoding UTF8
$metrics
foreach($background in @('Dark','Light')){
    $sheet=[Drawing.Bitmap]::new(1800,1180)
    $g=[Drawing.Graphics]::FromImage($sheet)
    $g.Clear($(if($background -eq 'Dark'){[Drawing.Color]::FromArgb(32,34,37)}else{[Drawing.Color]::FromArgb(216,216,216)}))
    $font=[Drawing.Font]::new('Arial',18)
    $brush=if($background -eq 'Dark'){[Drawing.Brushes]::White}else{[Drawing.Brushes]::Black}
    $i=0
    foreach($tier in @('Silver','Gold','Platinum')){
        $img=[Drawing.Bitmap]::new((Join-Path $outputRoot ('DescriptionPanel_'+$tier+'.png')))
        $g.DrawImageUnscaled($img,$i*600,30)
        $g.DrawString($tier,$font,$brush,[single]($i*600+220),[single]1125)
        $img.Dispose();$i++
    }
    $sheet.Save((Join-Path $PSScriptRoot ('DescriptionPanels_'+$background+'.png')),[Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose();$font.Dispose();$sheet.Dispose()
}
