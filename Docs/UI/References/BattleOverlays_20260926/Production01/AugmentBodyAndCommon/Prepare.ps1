param([string]$ProjectRoot='D:/GitHub/OZGL2_1')
$ErrorActionPreference='Stop'
if ($PSVersionTable.PSEdition -eq 'Core') {
    & "$env:SystemRoot/System32/WindowsPowerShell/v1.0/powershell.exe" -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -ProjectRoot $ProjectRoot
    if($LASTEXITCODE -ne 0){throw 'Overlay art preparation failed.'}
    return
}
Add-Type -AssemblyName System.Drawing
# 승인된 후처리만 수행한다: 배경 제거, 투명 여백, 크기와 중심 정렬. 원본 색상/문양은 변경하지 않는다.
Add-Type -ReferencedAssemblies System.Drawing,System.Core -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class OverlayCommonArt
{
    static int Alpha(int p){return (int)((uint)p>>24);}
    static bool Background(int p){int r=(p>>16)&255,g=(p>>8)&255,b=p&255;int lo=Math.Min(r,Math.Min(g,b)),hi=Math.Max(r,Math.Max(g,b));return Alpha(p)<16||(lo>=145&&hi-lo<=36)||(r>160&&b>160&&g<100);}
    static void Enqueue(int i,int[] p,bool[] gone,Queue<int> q){if(!gone[i]&&Background(p[i])){gone[i]=true;q.Enqueue(i);}}
    public static string Prepare(string input,string output,int width,int height,int padding,bool fit,bool hollow)
    {
        using(var raw=new Bitmap(input))
        using(var src=raw.Clone(new Rectangle(0,0,raw.Width,raw.Height),PixelFormat.Format32bppArgb))
        {
            int w=src.Width,h=src.Height;int[] p=new int[w*h];
            var bits=src.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try{for(int y=0;y<h;y++)Marshal.Copy(IntPtr.Add(bits.Scan0,y*bits.Stride),p,y*w,w);}finally{src.UnlockBits(bits);}
            int initialClear=0;foreach(int c in p)if(Alpha(c)==0)initialClear++;
            bool native=initialClear>p.Length/50;
            if(!native){
                bool[] gone=new bool[p.Length];var q=new Queue<int>();
                for(int x=0;x<w;x++){Enqueue(x,p,gone,q);Enqueue((h-1)*w+x,p,gone,q);}
                for(int y=0;y<h;y++){Enqueue(y*w,p,gone,q);Enqueue(y*w+w-1,p,gone,q);}
                if(hollow)Enqueue((h/2)*w+w/2,p,gone,q);
                while(q.Count>0){int i=q.Dequeue(),x=i%w,y=i/w;if(x>0)Enqueue(i-1,p,gone,q);if(x+1<w)Enqueue(i+1,p,gone,q);if(y>0)Enqueue(i-w,p,gone,q);if(y+1<h)Enqueue(i+w,p,gone,q);}
                for(int i=0;i<p.Length;i++)if(gone[i])p[i]=0;
            }
            // 배경 제거 뒤 떨어져 남은 극소 잡티만 없앤다.
            bool[] seen=new bool[p.Length];var pending=new Queue<int>();var part=new List<int>();
            for(int start=0;start<p.Length;start++){
                if(seen[start]||Alpha(p[start])<16)continue;
                seen[start]=true;pending.Enqueue(start);part.Clear();
                while(pending.Count>0){int i=pending.Dequeue(),x=i%w,y=i/w;part.Add(i);
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){int nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=w||ny>=h)continue;int k=ny*w+nx;if(!seen[k]&&Alpha(p[k])>=16){seen[k]=true;pending.Enqueue(k);}}
                }
                if(part.Count<=8)foreach(int i in part)p[i]=0;
            }
            int left=w,right=-1,top=h,bottom=-1;
            for(int i=0;i<p.Length;i++)if(Alpha(p[i])>=16){left=Math.Min(left,i%w);right=Math.Max(right,i%w);top=Math.Min(top,i/w);bottom=Math.Max(bottom,i/w);}
            if(right<left)throw new Exception("Empty image: "+input);
            int bw=right-left+1,bh=bottom-top+1,dw=width-padding*2,dh=height-padding*2;
            if(fit){double k=Math.Min(dw/(double)bw,dh/(double)bh);dw=Math.Max(2,(int)Math.Round(bw*k/2)*2);dh=Math.Max(2,(int)Math.Round(bh*k/2)*2);}
            int ox=(width-dw)/2,oy=(height-dh)/2,clear=0,centerAlpha=0;
            using(var result=new Bitmap(width,height,PixelFormat.Format32bppArgb)){
                var pixels=new int[width*height];
                for(int y=0;y<height;y++)for(int x=0;x<width;x++){
                    int value=0;if(x>=ox&&x<ox+dw&&y>=oy&&y<oy+dh){int sx=left+Math.Min(bw-1,(int)((x-ox+0.5)*bw/dw)),sy=top+Math.Min(bh-1,(int)((y-oy+0.5)*bh/dh));value=p[sy*w+sx];}
                    if(Alpha(value)<16)value=0;
                    pixels[y*width+x]=value;if(Alpha(value)==0)clear++;
                }
                centerAlpha=Alpha(pixels[(height/2)*width+width/2]);
                bits=result.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
                try{for(int y=0;y<height;y++)Marshal.Copy(pixels,y*width,IntPtr.Add(bits.Scan0,y*bits.Stride),width);}finally{result.UnlockBits(bits);}
                result.Save(output,ImageFormat.Png);
            }
            return String.Format("{0}|{1}x{2}|nativeAlpha={3}|sourceBounds={4},{5},{6},{7}|targetBounds={8},{9},{10},{11}|clear={12}|centerAlpha={13}",System.IO.Path.GetFileName(output),width,height,native,left,top,bw,bh,ox,oy,dw,dh,clear,centerAlpha);
        }
    }
}
'@
$assetRoot=Join-Path $ProjectRoot 'Assets/06.UI/BattleMutedPreview/Overlays_v1'
$specs=@(
    @('Augment_Body_Silver','Augment',600,1100,30,$false,$false),
    @('Augment_Body_Gold','Augment',600,1100,30,$false,$false),
    @('Augment_Body_Platinum','Augment',600,1100,30,$false,$false),
    @('Augment_TitlePlate','Augment',1024,320,16,$true,$false),
    @('Augment_EffectPlate','Augment',512,144,12,$true,$false),
    @('Result_LobbyButton','Result',640,240,12,$true,$false),
    @('Result_ExperienceFrame','Result',768,128,12,$true,$true),
    @('Icon_Deployment','Result',256,256,20,$true,$false)
)
$reports=@()
foreach($spec in $specs){
    $targetDir=Join-Path $assetRoot $spec[1]
    New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
    $reports += [OverlayCommonArt]::Prepare((Join-Path $PSScriptRoot ('Raw/'+$spec[0]+'.png')),(Join-Path $targetDir ($spec[0]+'.png')),$spec[2],$spec[3],$spec[4],$spec[5],$spec[6])
}
$reports | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'Validation.txt') -Encoding UTF8
$reports
# 검수용으로만 어두운 배경에 축소한 미리보기를 만든다. 배포 PNG에는 포함하지 않는다.
$sheet=New-Object Drawing.Bitmap 1400,1000
$g=[Drawing.Graphics]::FromImage($sheet)
$g.Clear([Drawing.Color]::FromArgb(24,24,28))
$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$font=New-Object Drawing.Font 'Arial',12
for($i=0;$i -lt $specs.Count;$i++){
    $s=$specs[$i]; $img=New-Object Drawing.Bitmap (Join-Path $assetRoot ($s[1]+'/'+$s[0]+'.png'))
    $col=$i%4; $row=[math]::Floor($i/4); $x=20+$col*350; $y=20+$row*490
    $scale=[math]::Min(310.0/$img.Width,435.0/$img.Height)
    $g.DrawImage($img,[Drawing.Rectangle]::new($x,$y,[int]($img.Width*$scale),[int]($img.Height*$scale)))
    $g.DrawString($s[0],$font,[Drawing.Brushes]::White,[single]$x,[single]($y+447))
    $img.Dispose()
}
$sheet.Save((Join-Path $PSScriptRoot 'ContactSheet.png'),[Drawing.Imaging.ImageFormat]::Png)
$font.Dispose();$g.Dispose();$sheet.Dispose()
