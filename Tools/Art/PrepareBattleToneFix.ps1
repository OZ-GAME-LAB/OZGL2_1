param(
    [Parameter(Mandatory = $true)][string]$FrameSource,
    [Parameter(Mandatory = $true)][string]$IconSource,
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') {
    & "$env:SystemRoot/System32/WindowsPowerShell/v1.0/powershell.exe" -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -ProjectRoot $ProjectRoot -FrameSource $FrameSource -IconSource $IconSource
    if ($LASTEXITCODE -ne 0) { throw 'Battle tone sprite preparation failed.' }
    return
}
Add-Type -AssemblyName System.Drawing
$processor = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class BattleToneSprites
{
    // 생성 아트의 배경·외부 잔여 픽셀만 정리한다. 색상이나 실루엣은 재도색하지 않는다.
    private static bool IsBackdrop(int p)
    {
        int r=(p>>16)&255, g=(p>>8)&255, b=p&255;
        return ((uint)p>>24)<16 || (Math.Min(r,Math.Min(g,b))>=85 && Math.Max(r,Math.Max(g,b))-Math.Min(r,Math.Min(g,b))<=40);
    }
    private static void Add(int i, int[] p, bool[] gone, Queue<int> q)
    {
        if (gone[i] || !IsBackdrop(p[i])) return;
        gone[i]=true; q.Enqueue(i);
    }
    public static string Prepare(string source,string output,int size,bool frame)
    {
        using(var raw=new Bitmap(source))
        using(var src=raw.Clone(new Rectangle(0,0,raw.Width,raw.Height),PixelFormat.Format32bppArgb))
        {
            int w=src.Width,h=src.Height;
            int[] p=new int[w*h];
            var data=src.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try { for(int y=0;y<h;y++) Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),p,y*w,w); }
            finally { src.UnlockBits(data); }
            if(frame)
            {
                bool[] gone=new bool[p.Length]; var q=new Queue<int>();
                for(int x=0;x<w;x++){Add(x,p,gone,q);Add((h-1)*w+x,p,gone,q);}
                for(int y=0;y<h;y++){Add(y*w,p,gone,q);Add(y*w+w-1,p,gone,q);}
                Add((h/2)*w+w/2,p,gone,q);
                while(q.Count>0)
                {
                    int i=q.Dequeue(),x=i%w,y=i/w;
                    if(x>0)Add(i-1,p,gone,q); if(x+1<w)Add(i+1,p,gone,q);
                    if(y>0)Add(i-w,p,gone,q); if(y+1<h)Add(i+w,p,gone,q);
                }
                for(int i=0;i<p.Length;i++)if(gone[i])p[i]=0;
            }
            // 큰 연결 영역만 보존: 프레임 1개, 서로 떨어진 화살표 2개.
            int[] labels=new int[p.Length]; var counts=new List<int>(); counts.Add(0);
            var queue=new Queue<int>(); int label=0;
            for(int i=0;i<p.Length;i++)
            {
                if(labels[i]!=0 || ((uint)p[i]>>24)<16)continue;
                label++; counts.Add(0); labels[i]=label; queue.Enqueue(i);
                while(queue.Count>0)
                {
                    int n=queue.Dequeue(); counts[label]++;
                    int x=n%w,y=n/w;
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                    {
                        int nx=x+dx,ny=y+dy;
                        if(nx<0||nx>=w||ny<0||ny>=h)continue;
                        int k=ny*w+nx;
                        if(labels[k]!=0 || ((uint)p[k]>>24)<16)continue;
                        labels[k]=label;queue.Enqueue(k);
                    }
                }
            }
            var ranked=new List<int>();for(int i=1;i<counts.Count;i++)ranked.Add(i);
            ranked.Sort((a,b)=>counts[b].CompareTo(counts[a]));
            int keep=frame?1:2;
            if(ranked.Count<keep)throw new InvalidOperationException("Required sprite components missing: "+source);
            var allowed=new HashSet<int>();for(int i=0;i<keep;i++)allowed.Add(ranked[i]);
            int left=w,top=h,right=-1,bottom=-1;
            for(int i=0;i<p.Length;i++)
            {
                if(!allowed.Contains(labels[i])){p[i]=0;continue;}
                left=Math.Min(left,i%w);right=Math.Max(right,i%w);
                top=Math.Min(top,i/w);bottom=Math.Max(bottom,i/w);
            }
            if(right<=left||bottom<=top)throw new InvalidOperationException("Empty sprite: "+source);
            int bw=right-left+1,bh=bottom-top+1,padding=frame?4:3;
            double scale=(size-padding*2)/(double)Math.Max(bw,bh);
            int ow=(int)Math.Round(bw*scale),oh=(int)Math.Round(bh*scale);
            int ox=(size-ow)/2,oy=(size-oh)/2,transparent=0;
            using(var result=new Bitmap(size,size,PixelFormat.Format32bppArgb))
            {
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    int value=0;
                    if(x>=ox&&x<ox+ow&&y>=oy&&y<oy+oh)
                    {
                        int sx=left+Math.Min(bw-1,(int)((x-ox+0.5)*bw/ow));
                        int sy=top+Math.Min(bh-1,(int)((y-oy+0.5)*bh/oh));
                        value=p[sy*w+sx];
                    }
                    if(((uint)value>>24)==0)transparent++;
                    result.SetPixel(x,y,Color.FromArgb(value));
                }
                result.Save(output,ImageFormat.Png);
            }
            return output+" | "+size+"x"+size+" | transparent="+transparent+" | sourceBounds="+left+","+top+","+bw+","+bh;
        }
    }
}
'@
Add-Type -TypeDefinition $processor -ReferencedAssemblies System.Drawing,System.Core
$outputDirectory = Join-Path $ProjectRoot 'Assets/06.UI/BattleMutedPreview/ToneFix'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
[BattleToneSprites]::Prepare($FrameSource,(Join-Path $outputDirectory 'Frame_Reroll.png'),512,$true)
[BattleToneSprites]::Prepare($IconSource,(Join-Path $outputDirectory 'Icon_RerollWhite.png'),128,$false)
