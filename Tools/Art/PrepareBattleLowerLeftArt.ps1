param([string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') {
    & "$env:SystemRoot/System32/WindowsPowerShell/v1.0/powershell.exe" -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -ProjectRoot $ProjectRoot
    if ($LASTEXITCODE -ne 0) { throw 'Lower left art preparation failed.' }
    return
}
Add-Type -AssemblyName System.Drawing
$source = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class BattleLowerLeftArt
{
    private static int Alpha(int p) { return (int)((uint)p >> 24); }
    private static bool Background(int p)
    {
        int r=(p>>16)&255,g=(p>>8)&255,b=p&255;
        return Alpha(p)<16 || (Math.Min(r,Math.Min(g,b))>=64 && Math.Max(r,Math.Max(g,b))-Math.Min(r,Math.Min(g,b))<=40);
    }
    private static void Add(int i,int[] p,bool[] removed,Queue<int> queue)
    {
        if(removed[i]||!Background(p[i]))return;
        removed[i]=true;queue.Enqueue(i);
    }
    public static string Prepare(string source,string output,int width,int height,string mode,int keep,int padding,bool fit)
    {
        using(var raw=new Bitmap(source))
        using(var input=raw.Clone(new Rectangle(0,0,raw.Width,raw.Height),PixelFormat.Format32bppArgb))
        {
            int w=input.Width,h=input.Height;int[] p=new int[w*h];
            var data=input.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try { for(int y=0;y<h;y++)Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),p,y*w,w); }
            finally { input.UnlockBits(data); }
            if(mode=="frame")
            {
                bool[] gone=new bool[p.Length];var q=new Queue<int>();
                for(int x=0;x<w;x++){Add(x,p,gone,q);Add((h-1)*w+x,p,gone,q);}
                for(int y=0;y<h;y++){Add(y*w,p,gone,q);Add(y*w+w-1,p,gone,q);}
                Add((h/2)*w+w/2,p,gone,q);
                while(q.Count>0)
                {
                    int i=q.Dequeue(),x=i%w,y=i/w;
                    if(x>0)Add(i-1,p,gone,q);if(x+1<w)Add(i+1,p,gone,q);
                    if(y>0)Add(i-w,p,gone,q);if(y+1<h)Add(i+w,p,gone,q);
                }
                for(int i=0;i<p.Length;i++)if(gone[i])p[i]=0;
            }
            else if(mode=="white")
            {
                // 흰색 아이콘보다 어두운 생성 체크 배경만 제거한다. 남는 픽셀은 재도색하지 않는다.
                for(int i=0;i<p.Length;i++)
                {
                    int r=(p[i]>>16)&255,g=(p[i]>>8)&255,b=p[i]&255;
                    if(Math.Min(r,Math.Min(g,b))<230 || Math.Max(r,Math.Max(g,b))-Math.Min(r,Math.Min(g,b))>35)p[i]=0;
                }
            }
            else if(mode!="alpha")throw new ArgumentException("Unknown cleanup mode.");
            int[] labels=new int[p.Length];var counts=new List<int>();counts.Add(0);var queue=new Queue<int>();int label=0;
            for(int i=0;i<p.Length;i++)
            {
                if(labels[i]!=0||Alpha(p[i])<16)continue;
                label++;counts.Add(0);labels[i]=label;queue.Enqueue(i);
                while(queue.Count>0)
                {
                    int n=queue.Dequeue(),x=n%w,y=n/w;counts[label]++;
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                    {
                        int nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=w||ny>=h)continue;
                        int k=ny*w+nx;if(labels[k]!=0||Alpha(p[k])<16)continue;
                        labels[k]=label;queue.Enqueue(k);
                    }
                }
            }
            var ranked=new List<int>();for(int i=1;i<counts.Count;i++)ranked.Add(i);
            ranked.Sort((a,b)=>counts[b].CompareTo(counts[a]));
            if(ranked.Count<keep)throw new InvalidOperationException("Missing icon components: "+source);
            var valid=new HashSet<int>();for(int i=0;i<keep;i++)valid.Add(ranked[i]);
            int left=w,top=h,right=-1,bottom=-1;
            for(int i=0;i<p.Length;i++)
            {
                if(!valid.Contains(labels[i])){p[i]=0;continue;}
                left=Math.Min(left,i%w);right=Math.Max(right,i%w);top=Math.Min(top,i/w);bottom=Math.Max(bottom,i/w);
            }
            int bw=right-left+1,bh=bottom-top+1,dw=width-2*padding,dh=height-2*padding;
            if(fit){double scale=Math.Min(dw/(double)bw,dh/(double)bh);dw=(int)Math.Round(bw*scale);dh=(int)Math.Round(bh*scale);}
            int ox=(width-dw)/2,oy=(height-dh)/2,transparent=0;
            using(var result=new Bitmap(width,height,PixelFormat.Format32bppArgb))
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
                    if(Alpha(value)==0)transparent++;
                    result.SetPixel(x,y,Color.FromArgb(value));
                }
                result.Save(output,ImageFormat.Png);
            }
            return output+" | "+width+"x"+height+" | bbox="+left+","+top+","+bw+","+bh+" | alpha0="+transparent;
        }
    }
}
'@
Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing,System.Core
$sourceDirectory = Join-Path $ProjectRoot 'Tools/Art/Sources/BattleLowerLeftReference'
$outputDirectory = Join-Path $ProjectRoot 'Assets/06.UI/BattleMutedPreview/LowerLeftReference'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$specs = @(
    @('Frame_Cost',512,512,'frame',1,6,$false),
    @('Icon_Flame',96,144,'white',1,3,$true),
    @('Frame_Reroll',512,512,'frame',1,6,$false),
    @('Icon_Reroll',128,112,'white',2,3,$true),
    @('Frame_Price',512,160,'frame',1,5,$false),
    @('Icon_CostGem',128,128,'alpha',1,3,$true)
)
foreach($item in $specs) {
    [BattleLowerLeftArt]::Prepare((Join-Path $sourceDirectory ($item[0]+'_Original.png')),(Join-Path $outputDirectory ($item[0]+'.png')),$item[1],$item[2],$item[3],$item[4],$item[5],$item[6])
}
