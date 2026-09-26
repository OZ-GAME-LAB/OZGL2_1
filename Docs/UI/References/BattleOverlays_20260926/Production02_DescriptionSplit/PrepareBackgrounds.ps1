param([string]$ProjectRoot='D:/GitHub/OZGL2_1')
$ErrorActionPreference='Stop'
if ($PSVersionTable.PSEdition -eq 'Core') {
    & "$env:SystemRoot/System32/WindowsPowerShell/v1.0/powershell.exe" -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -ProjectRoot $ProjectRoot
    if($LASTEXITCODE -ne 0){throw 'Description split background preparation failed.'}
    return
}
Add-Type -AssemblyName System.Drawing
# 생성된 복원 배경의 배경색만 제거하고 원본과 동일한 외곽 여백으로 정렬한다.
# 원본 패널의 얇은 그림자가 배경에 남지 않도록 복원한 벨벳을 전체 배경으로 사용한다.
Add-Type -ReferencedAssemblies System.Drawing,System.Core -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class DescriptionSplitBackground
{
    static bool IsBackground(int p){int r=(p>>16)&255,g=(p>>8)&255,b=p&255;int lo=Math.Min(r,Math.Min(g,b)),hi=Math.Max(r,Math.Max(g,b));return (uint)p>>24<16||(lo>=145&&hi-lo<=36);}
    static void Add(int i,int[] p,bool[] gone,Queue<int> q){if(!gone[i]&&IsBackground(p[i])){gone[i]=true;q.Enqueue(i);}}
    static int[] Read(Bitmap image){
        using(var rgba=image.Clone(new Rectangle(0,0,image.Width,image.Height),PixelFormat.Format32bppArgb)){
            int[] result=new int[rgba.Width*rgba.Height];
            var bits=rgba.LockBits(new Rectangle(0,0,rgba.Width,rgba.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try{for(int y=0;y<rgba.Height;y++)Marshal.Copy(IntPtr.Add(bits.Scan0,y*bits.Stride),result,y*rgba.Width,rgba.Width);}finally{rgba.UnlockBits(bits);}
            return result;
        }
    }
    static void Save(int[] pixels,int w,int h,string path){
        using(var image=new Bitmap(w,h,PixelFormat.Format32bppArgb)){
            var bits=image.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            try{for(int y=0;y<h;y++)Marshal.Copy(pixels,y*w,IntPtr.Add(bits.Scan0,y*bits.Stride),w);}finally{image.UnlockBits(bits);}
            image.Save(path,ImageFormat.Png);
        }
    }
    public static string Build(string sourcePath,string generatedPath,string panelPath,string output,string recomposedPath){
        using(var source=new Bitmap(sourcePath))
        using(var generated=new Bitmap(generatedPath))
        using(var panel=new Bitmap(panelPath)){
            int w=source.Width,h=source.Height;
            if(w!=600||h!=1100||panel.Width!=w||panel.Height!=h)throw new Exception("Canvas mismatch");
            int[] original=Read(source),render=Read(generated),front=Read(panel),back=new int[w*h],recomposed=new int[w*h];
            int gw=generated.Width,gh=generated.Height;var gone=new bool[render.Length];var queue=new Queue<int>();
            for(int x=0;x<gw;x++){Add(x,render,gone,queue);Add((gh-1)*gw+x,render,gone,queue);}
            for(int y=0;y<gh;y++){Add(y*gw,render,gone,queue);Add(y*gw+gw-1,render,gone,queue);}
            while(queue.Count>0){int i=queue.Dequeue(),x=i%gw,y=i/gw;if(x>0)Add(i-1,render,gone,queue);if(x+1<gw)Add(i+1,render,gone,queue);if(y>0)Add(i-gw,render,gone,queue);if(y+1<gh)Add(i+gw,render,gone,queue);}
            for(int i=0;i<render.Length;i++)if(gone[i])render[i]=0;
            int left=gw,right=-1,top=gh,bottom=-1;
            for(int i=0;i<render.Length;i++)if((uint)render[i]>>24>=16){left=Math.Min(left,i%gw);right=Math.Max(right,i%gw);top=Math.Min(top,i/gw);bottom=Math.Max(bottom,i/gw);}
            if(right<left)throw new Exception("No restored velvet");
            int bw=right-left+1,bh=bottom-top+1,underPanel=0,holes=0,panelDiff=0;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){
                int i=y*w+x,a=(int)((uint)front[i]>>24);
                if(a!=0&&a!=255)throw new Exception("Expected exact binary extraction alpha");
                if(x>=30&&x<570&&y>=30&&y<1070){
                    int gx=left+Math.Min(bw-1,(int)((x-30+0.5)*bw/540));
                    int gy=top+Math.Min(bh-1,(int)((y-30+0.5)*bh/1040));
                    back[i]=render[gy*gw+gx];
                }
                if(a==255){
                    if((uint)back[i]>>24<255)holes++;
                    if(front[i]!=original[i])panelDiff++;
                    underPanel++;
                }
                recomposed[i]=a==255?front[i]:back[i];
            }
            if(holes>0)throw new Exception("Generated velvet has transparent holes beneath panel: "+holes);
            if(panelDiff!=0)throw new Exception("Original description panel mismatch");
            Save(back,w,h,output);Save(recomposed,w,h,recomposedPath);
            return System.IO.Path.GetFileName(output)+"|canvas=600x1100|visibleBounds=30,30,540,1040|sourceCrop="+left+","+top+","+bw+","+bh+"|underPanelPixels="+underPanel+"|panelPixelDiff="+panelDiff+"|underPanelAlphaHoles="+holes;
        }
    }
}
'@
$assetRoot=Join-Path $ProjectRoot 'Assets/06.UI/BattleMutedPreview/Overlays_v1/Augment'
$outDir=Join-Path $assetRoot 'Separated'
$previewDir=Join-Path $PSScriptRoot 'Verification'
New-Item -ItemType Directory -Force -Path $outDir,$previewDir | Out-Null
$reports=@()
foreach($tier in @('Silver','Gold','Platinum')){
    $reports += [DescriptionSplitBackground]::Build((Join-Path $assetRoot ('Augment_Body_'+$tier+'.png')),(Join-Path $PSScriptRoot ('Raw/VelvetRestored_'+$tier+'.png')),(Join-Path $outDir ('DescriptionPanel_'+$tier+'.png')),(Join-Path $outDir ('VelvetBackground_'+$tier+'.png')),(Join-Path $previewDir ('Recomposed_'+$tier+'.png')))
}
$reports | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'Validation.txt') -Encoding UTF8
$reports
# 분리된 두 레이어를 나란히 확인하는 검수 이미지다. PNG 아트에 글자를 합치지 않는다.
$sheet=New-Object Drawing.Bitmap 1260,960
$g=[Drawing.Graphics]::FromImage($sheet)
$g.Clear([Drawing.Color]::FromArgb(30,31,35))
$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$font=New-Object Drawing.Font 'Arial',14
$tiers=@('Silver','Gold','Platinum')
for($col=0;$col -lt 3;$col++){
    for($row=0;$row -lt 2;$row++){
        $prefix=if($row -eq 0){'VelvetBackground_'}else{'DescriptionPanel_'}
        $img=New-Object Drawing.Bitmap (Join-Path $outDir ($prefix+$tiers[$col]+'.png'))
        $x=100+$col*420;$y=18+$row*480
        $g.DrawImage($img,[Drawing.Rectangle]::new($x,$y,228,418))
        $g.DrawString(($tiers[$col]+' / '+$(if($row -eq 0){'Velvet'}else{'Description'})),$font,[Drawing.Brushes]::White,[single]($x-20),[single]($y+426))
        $img.Dispose()
    }
}
$sheet.Save((Join-Path $PSScriptRoot 'Separated_Preview.png'),[Drawing.Imaging.ImageFormat]::Png)
$font.Dispose();$g.Dispose();$sheet.Dispose()
