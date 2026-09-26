param([string]$ProjectRoot='D:/GitHub/OZGL2_1')
$ErrorActionPreference='Stop'
if ($PSVersionTable.PSEdition -eq 'Core') {
    & "$env:SystemRoot/System32/WindowsPowerShell/v1.0/powershell.exe" -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -ProjectRoot $ProjectRoot
    if ($LASTEXITCODE -ne 0) { throw 'Result art preparation failed.' }
    return
}
Add-Type -AssemblyName System.Drawing
$source=@'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.IO;

// 기존 PrepareBattleReferenceArt/PrepareBattleCardArt의 배경 연결영역 제거 방식을 재사용한다.
public static class ResultArtPreparation
{
    sealed class Art
    {
        public string Name; public int W,H; public int[] Pixels; public Rectangle Bounds;
        public Art(string name,int w,int h,int[] pixels,Rectangle bounds) { Name=name;W=w;H=h;Pixels=pixels;Bounds=bounds; }
    }
    static int Alpha(int p) { return (int)((uint)p>>24); }
    static bool Backdrop(int p,int threshold,int maxChroma)
    {
        int r=(p>>16)&255,g=(p>>8)&255,b=p&255;
        int min=Math.Min(r,Math.Min(g,b)),max=Math.Max(r,Math.Max(g,b));
        return Alpha(p)<16 || (min>=threshold && max-min<=maxChroma);
    }
    static void Seed(int start,int w,int h,bool[] eligible,bool[] removed,int[] queue)
    {
        if(start<0||start>=eligible.Length||!eligible[start]||removed[start])return;
        int head=0,tail=1;queue[0]=start;removed[start]=true;
        while(head<tail)
        {
            int p=queue[head++],x=p%w,y=p/w;
            if(x>0)Add(p-1,eligible,removed,queue,ref tail);
            if(x<w-1)Add(p+1,eligible,removed,queue,ref tail);
            if(y>0)Add(p-w,eligible,removed,queue,ref tail);
            if(y<h-1)Add(p+w,eligible,removed,queue,ref tail);
        }
    }
    static void Add(int p,bool[] eligible,bool[] removed,int[] queue,ref int tail)
    { if(eligible[p]&&!removed[p]) {removed[p]=true;queue[tail++]=p;} }
    static void PointSeed(double x,double y,Art a,bool[] eligible,bool[] removed,int[] queue)
    { Seed((int)(y*a.H)*a.W+(int)(x*a.W),a.W,a.H,eligible,removed,queue); }
    static Art Read(string path,string name)
    {
        using(var raw=new Bitmap(path))
        using(var src=raw.Clone(new Rectangle(0,0,raw.Width,raw.Height),PixelFormat.Format32bppArgb))
        {
            int w=src.Width,h=src.Height;int[] p=new int[w*h];
            var bits=src.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try{for(int y=0;y<h;y++)Marshal.Copy(IntPtr.Add(bits.Scan0,y*bits.Stride),p,y*w,w);}finally{src.UnlockBits(bits);}
            return new Art(name,w,h,p,new Rectangle());
        }
    }
    static Art Extract(string path,string name)
    {
        Art a=Read(path,name);int w=a.W,h=a.H;int[] p=a.Pixels,queue=new int[p.Length];
        bool[] eligible=new bool[p.Length],removed=new bool[p.Length];
        int threshold=name=="Result_TitleAccent"?190:102;
        int maxChroma=name=="Result_TitleAccent"?38:10;
        for(int i=0;i<p.Length;i++)eligible[i]=Backdrop(p[i],threshold,maxChroma);
        for(int x=0;x<w;x++){Seed(x,w,h,eligible,removed,queue);Seed((h-1)*w+x,w,h,eligible,removed,queue);}
        for(int y=0;y<h;y++){Seed(y*w,w,h,eligible,removed,queue);Seed(y*w+w-1,w,h,eligible,removed,queue);}
        if(name=="Result_CrestFrame"||name=="Result_RuneRing")PointSeed(.5,.5,a,eligible,removed,queue);
        if(name=="Result_Crown_Victory")
        {PointSeed(.28,.585,a,eligible,removed,queue);PointSeed(.717,.585,a,eligible,removed,queue);}
        if(name=="Result_Crown_Defeat")
        {PointSeed(.252,.585,a,eligible,removed,queue);PointSeed(.746,.585,a,eligible,removed,queue);}
        if(name.StartsWith("Result_Banner"))
        {
            // 가로대 아래, 체인 안, 패배 천의 구멍에 갇힌 체크 배경만 선택한다.
            PointSeed(.57,.187,a,eligible,removed,queue);
            double[] loops={.198,.226,.251,.278};foreach(double y in loops)PointSeed(.85,y,a,eligible,removed,queue);
            if(name.EndsWith("Defeat"))
            {
                double[] holes={.66,.278,.481,.318,.702,.378,.482,.438,.693,.46,.646,.508};
                for(int i=0;i<holes.Length;i+=2)PointSeed(holes[i],holes[i+1],a,eligible,removed,queue);
            }
        }
        // 단색 회색 장식의 밝은 체크만 제거해 조각 사이에 갇힌 배경을 남기지 않는다.
        if(name=="Result_TitleAccent")for(int i=0;i<p.Length;i++)if(eligible[i])removed[i]=true;
        // 24픽셀 미만의 배경 찌꺼기만 제외한다. 작은 룬/석편은 유지한다.
        bool[] seen=new bool[p.Length];
        for(int start=0;start<p.Length;start++)
        {
            if(removed[start]||seen[start])continue;
            int head=0,tail=1;queue[0]=start;seen[start]=true;
            while(head<tail)
            {
                int n=queue[head++],x=n%w,y=n/w;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    int nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=w||ny>=h)continue;
                    int k=ny*w+nx;if(removed[k]||seen[k])continue;seen[k]=true;queue[tail++]=k;
                }
            }
            if(tail<24)for(int i=0;i<tail;i++)removed[queue[i]]=true;
        }
        int left=w,top=h,right=-1,bottom=-1;
        for(int i=0;i<p.Length;i++)
        {
            if(removed[i])p[i]=0;
            if(Alpha(p[i])<16)continue;
            left=Math.Min(left,i%w);right=Math.Max(right,i%w);top=Math.Min(top,i/w);bottom=Math.Max(bottom,i/w);
        }
        if(right<left)throw new InvalidOperationException("Empty sprite: "+name);
        a.Bounds=Rectangle.FromLTRB(left,top,right+1,bottom+1);return a;
    }
    static Bitmap Fit(Art a,Rectangle crop,int width,int height,int padX,int padY)
    {
        double scale=Math.Min((width-padX*2)/(double)crop.Width,(height-padY*2)/(double)crop.Height);
        int dw=(int)Math.Round(crop.Width*scale),dh=(int)Math.Round(crop.Height*scale),ox=(width-dw)/2,oy=(height-dh)/2;
        var result=new Bitmap(width,height,PixelFormat.Format32bppArgb);
        for(int y=0;y<dh;y++)for(int x=0;x<dw;x++)
        {
            int sx=crop.X+Math.Min(crop.Width-1,(int)((x+.5)*crop.Width/dw));
            int sy=crop.Y+Math.Min(crop.Height-1,(int)((y+.5)*crop.Height/dh));
            result.SetPixel(ox+x,oy+y,Color.FromArgb(a.Pixels[sy*a.W+sx]));
        }
        return result;
    }
    static string Metrics(Bitmap b,Art a)
    {
        int clear=0,edge=0,left=b.Width,top=b.Height,right=-1,bottom=-1;
        for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)
        {
            int alpha=b.GetPixel(x,y).A;if(alpha==0)clear++;else {left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
            if(alpha>0&&(x==0||y==0||x==b.Width-1||y==b.Height-1))edge++;
        }
        return a.Name+" | "+b.Width+"x"+b.Height+" | visible="+left+","+top+","+(right-left+1)+","+(bottom-top+1)+" | clear="+clear+" | centerAlpha="+b.GetPixel(b.Width/2,b.Height/2).A+" | opaqueEdge="+edge+" | sourceCrop="+a.Bounds;
    }
    public static void Run(string root)
    {
        string docs=Path.Combine(root,"Docs/UI/References/BattleOverlays_20260926/Production01/Result");
        string output=Path.Combine(root,"Assets/06.UI/BattleMutedPreview/Overlays_v1/Result");Directory.CreateDirectory(output);
        string[] names={"Result_RecordPanel","Result_CrestFrame","Result_RuneRing","Result_Crown_Victory","Result_Crown_Defeat","Result_Banner_Victory","Result_Banner_Defeat","Result_TitleAccent"};
        var art=new List<Art>();foreach(string name in names)art.Add(Extract(Path.Combine(docs,"Raw",name+".png"),name));
        Rectangle bannerBounds=Rectangle.Union(art[5].Bounds,art[6].Bounds);
        var metrics=new List<string>();var results=new List<Bitmap>();
        for(int i=0;i<art.Count;i++)
        {
            int width=512,height=512,padX=24,padY=24;Rectangle crop=art[i].Bounds;
            if(i==0){width=1200;height=400;padX=20;padY=20;}
            if(i==3||i==4){padX=40;padY=96;}
            if(i==5||i==6){width=384;height=768;padX=24;padY=24;crop=bannerBounds;}
            if(i==7){width=768;height=256;padX=24;padY=24;}
            Bitmap result=Fit(art[i],crop,width,height,padX,padY);results.Add(result);
            result.Save(Path.Combine(output,names[i]+".png"),ImageFormat.Png);metrics.Add(Metrics(result,art[i]));
        }
        using(var sheet=new Bitmap(1600,1180,PixelFormat.Format32bppArgb))
        using(var g=Graphics.FromImage(sheet))
        using(var font=new Font("Consolas",16))
        using(var brush=new SolidBrush(Color.FromArgb(215,215,215)))
        {
            g.Clear(Color.FromArgb(21,22,26));g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
            int[] x={20,20,405,790,1175,810,1200,20};int[] y={46,385,385,385,385,742,742,928};
            int[] maxW={1560,360,360,360,360,360,360,760};int[] maxH={290,340,340,340,340,400,400,200};
            for(int i=0;i<results.Count;i++)
            {
                g.DrawString(names[i],font,brush,x[i],y[i]-28);double s=Math.Min(maxW[i]/(double)results[i].Width,maxH[i]/(double)results[i].Height);
                g.DrawImage(results[i],new Rectangle(x[i],y[i],(int)(results[i].Width*s),(int)(results[i].Height*s)),0,0,results[i].Width,results[i].Height,GraphicsUnit.Pixel);
            }
            sheet.Save(Path.Combine(docs,"Result_ContactSheet.png"),ImageFormat.Png);
        }
        foreach(Bitmap b in results)b.Dispose();
        File.WriteAllLines(Path.Combine(docs,"AlphaMetrics.txt"),metrics.ToArray());foreach(string line in metrics)Console.WriteLine(line);
    }
}
'@
Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing,System.Core
[ResultArtPreparation]::Run($ProjectRoot)
