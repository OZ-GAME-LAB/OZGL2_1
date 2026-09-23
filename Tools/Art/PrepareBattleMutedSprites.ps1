param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path,
    [switch]$ExperienceOnly
)

$ErrorActionPreference = 'Stop'
# System.Drawing의 전체 .NET Framework API를 사용하므로 PowerShell 7에서는 Windows PowerShell로 실행한다.
if ($PSVersionTable.PSEdition -eq 'Core') {
    $forwardedArguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath, '-ProjectRoot', $ProjectRoot)
    if ($ExperienceOnly) { $forwardedArguments += '-ExperienceOnly' }
    & "$env:SystemRoot/System32/WindowsPowerShell/v1.0/powershell.exe" @forwardedArguments
    if ($LASTEXITCODE -ne 0) { throw 'Battle UI sprite preparation failed.' }
    return
}
Add-Type -AssemblyName System.Drawing

# 승인된 생성 원본의 체크 배경 제거, 부품 분할, 표시 중심 정렬만 수행한다.
$spriteSource = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class BattleMutedSpritePreparation
{
    private class Part
    {
        public string Sheet, Name;
        public Rectangle Crop;
        public int Mode;
        public bool Square;
        public Part(string sheet, string name, int x, int y, int w, int h, int mode, bool square = false)
        { Sheet=sheet; Name=name; Crop=new Rectangle(x,y,w,h); Mode=mode; Square=square; }
    }

    private static bool Keep(Color c, int mode)
    {
        int max=Math.Max(c.R,Math.Max(c.G,c.B));
        int min=Math.Min(c.R,Math.Min(c.G,c.B));
        int chroma=max-min;
        if (mode==0) return max < 100 || chroma >= 17 || min >= 239;
        if (mode==1) return max < 105 || chroma >= 12;
        return c.R >= 210 && (chroma >= 8 || min >= 237);
    }

    // 배경 압축 노이즈로 분리된 아주 작은 조각은 알파에서 제외한다.
    private static void RemoveSmallIslands(bool[] mask, int width, int height, int minimum)
    {
        bool[] seen=new bool[mask.Length];
        int[] queue=new int[mask.Length];
        for(int start=0;start<mask.Length;start++)
        {
            if(!mask[start] || seen[start]) continue;
            int head=0,tail=1; queue[0]=start; seen[start]=true;
            while(head<tail)
            {
                int p=queue[head++], x=p%width, y=p/width;
                for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++)
                {
                    int nx=x+dx,ny=y+dy;
                    if(nx<0||ny<0||nx>=width||ny>=height) continue;
                    int n=ny*width+nx;
                    if(mask[n]&&!seen[n]) { seen[n]=true;queue[tail++]=n; }
                }
            }
            if(tail<minimum) for(int i=0;i<tail;i++) mask[queue[i]]=false;
        }
    }

    private static Bitmap Extract(Bitmap original, Part part)
    {
        int w=part.Crop.Width,h=part.Crop.Height;
        bool[] mask=new bool[w*h];
        for(int y=0;y<h;y++) for(int x=0;x<w;x++)
            mask[y*w+x]=Keep(original.GetPixel(part.Crop.X+x,part.Crop.Y+y),part.Mode);
        RemoveSmallIslands(mask,w,h,part.Mode==2?9:24);
        int left=w,top=h,right=-1,bottom=-1;
        for(int y=0;y<h;y++) for(int x=0;x<w;x++) if(mask[y*w+x])
        {left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
        if(right<left) throw new Exception("Empty part: "+part.Name);
        int cw=right-left+1,ch=bottom-top+1;
        // 사각형 아이콘/마름모는 정방형 캔버스의 정중앙에 배치한다.
        int ow=part.Square?Math.Max(cw,ch)+16:cw+16;
        int oh=part.Square?ow:ch+16;
        Bitmap output=new Bitmap(ow,oh,PixelFormat.Format32bppArgb);
        int ox=(ow-cw)/2,oy=(oh-ch)/2;
        for(int y=top;y<=bottom;y++) for(int x=left;x<=right;x++)
        {
            if(!mask[y*w+x]) continue;
            Color c=original.GetPixel(part.Crop.X+x,part.Crop.Y+y);
            output.SetPixel(ox+x-left,oy+y-top,Color.FromArgb(255,c.R,c.G,c.B));
        }
        return output;
    }

    // 로비 원본의 큰 투명 여백만 줄이고 원래 RGBA와 띠 무늬를 그대로 재사용한다.
    public static string ExportExperienceBars(string root)
    {
        string source=Path.Combine(root,"Assets/06.UI/LobbyMutedPreview/LevelHud_v1/Sprites");
        string target=Path.Combine(root,"Assets/06.UI/BattleMutedPreview/Sprites");
        Directory.CreateDirectory(target);
        List<string> report=new List<string>();
        foreach(string suffix in new[]{"Fill","Track"})
        using(Bitmap original=new Bitmap(Path.Combine(source,"Level"+suffix+".png")))
        {
            int w=original.Width,h=original.Height;
            bool[] mask=new bool[w*h];
            for(int y=0;y<h;y++) for(int x=0;x<w;x++)
                mask[y*w+x]=original.GetPixel(x,y).A>16;
            RemoveSmallIslands(mask,w,h,32);
            int left=w,top=h,right=-1,bottom=-1;
            for(int y=0;y<h;y++) for(int x=0;x<w;x++) if(mask[y*w+x])
            {left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
            if(right<left) throw new Exception("Empty experience strip: "+suffix);
            using(Bitmap output=new Bitmap(right-left+5,bottom-top+5,PixelFormat.Format32bppArgb))
            {
                for(int y=top;y<=bottom;y++) for(int x=left;x<=right;x++)
                    if(mask[y*w+x]) output.SetPixel(x-left+2,y-top+2,original.GetPixel(x,y));
                string name="Experience_"+suffix;
                output.Save(Path.Combine(target,name+".png"),ImageFormat.Png);
                report.Add(name+" | "+output.Width+"x"+output.Height+" | source bounds="+left+","+top+","+right+","+bottom);
            }
        }
        return string.Join(Environment.NewLine,report.ToArray());
    }

    public static string Run(string root)
    {
        string source=Path.Combine(root,"Tools/Art/Sources/BattleMutedPreview_v1");
        string target=Path.Combine(root,"Assets/06.UI/BattleMutedPreview/Sprites");
        string preview=Path.Combine(root,"Tools/Art/Previews");
        Directory.CreateDirectory(target); Directory.CreateDirectory(preview);
        List<Part> parts=new List<Part> {
            new Part("Chrome","Frame_Hud",45,108,1166,105,0),
            new Part("Chrome","Frame_Wave",150,312,951,303,0),
            new Part("Chrome","Frame_SynergyRow",222,683,814,212,0),
            new Part("Chrome","Frame_CostPlate",325,963,598,180,0),
            new Part("Shapes","Frame_DiamondRed",22,35,588,616,1,true),
            new Part("Shapes","Frame_DiamondNeutral",660,62,561,566,1,true),
            new Part("Shapes","Icon_CostGem",187,840,239,244,1,true),
            new Part("Shapes","Divider_Vertical",912,728,59,446,1),
        };
        string[] names={"Skull","Hourglass","Menu","Flame","Reroll","CrossedSwords","Arcane","Bow","BoneShield","CursedEye","EnemyMelee","EnemyRanged","EnemyElite"};
        for(int i=0;i<names.Length;i++)
        {
            int col=i%4,row=i/4;
            int x=(int)Math.Round(col*313.5),y=(int)Math.Round(row*313.5);
            int x2=(int)Math.Round((col+1)*313.5),y2=(int)Math.Round((row+1)*313.5);
            parts.Add(new Part("Icons","Icon_"+names[i],x,y,x2-x,y2-y,2,true));
        }
        Dictionary<string,Bitmap> sheets=new Dictionary<string,Bitmap>();
        Dictionary<string,Bitmap> outputs=new Dictionary<string,Bitmap>();
        List<string> report=new List<string>();
        try
        {
            foreach(string name in new[]{"Chrome","Shapes","Icons"})
                sheets[name]=new Bitmap(Path.Combine(source,name+"_Original.png"));
            foreach(Part part in parts)
            {
                Bitmap image=Extract(sheets[part.Sheet],part);outputs[part.Name]=image;
                image.Save(Path.Combine(target,part.Name+".png"),ImageFormat.Png);
                int clear=0,opaque=0;
                for(int y=0;y<image.Height;y++) for(int x=0;x<image.Width;x++)
                    if(image.GetPixel(x,y).A==0) clear++;else opaque++;
                report.Add(part.Name+" | "+image.Width+"x"+image.Height+" | transparent="+clear+" opaque="+opaque);
            }
            using(Bitmap contact=new Bitmap(1500,1470,PixelFormat.Format32bppArgb))
            using(Graphics g=Graphics.FromImage(contact))
            using(Font font=new Font("Consolas",12))
            using(Brush white=new SolidBrush(Color.FromArgb(238,233,219)))
            using(Pen divider=new Pen(Color.FromArgb(55,55,62)))
            {
                g.Clear(Color.FromArgb(18,18,22));
                g.InterpolationMode=InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode=PixelOffsetMode.Half;
                for(int i=0;i<parts.Count;i++)
                {
                    int col=i%3,row=i/3;
                    int cx=col*500,cy=row*210;
                    Bitmap item=outputs[parts[i].Name];
                    float scale=Math.Min(470f/item.Width,164f/item.Height);
                    int sw=(int)Math.Round(item.Width*scale),sh=(int)Math.Round(item.Height*scale);
                    g.DrawImage(item,new Rectangle(cx+(500-sw)/2,cy+18+(164-sh)/2,sw,sh));
                    g.DrawString(parts[i].Name,font,white,cx+10,cy+187);
                    g.DrawRectangle(divider,cx,cy,499,209);
                }
                contact.Save(Path.Combine(preview,"BattleMutedSprites_v1.png"),ImageFormat.Png);
            }
            return string.Join(Environment.NewLine,report.ToArray());
        }
        finally
        {
            foreach(Bitmap image in sheets.Values) image.Dispose();
            foreach(Bitmap image in outputs.Values) image.Dispose();
        }
    }
}
'@

Add-Type -TypeDefinition $spriteSource -ReferencedAssemblies System.Drawing
if (-not $ExperienceOnly) { [BattleMutedSpritePreparation]::Run($ProjectRoot) }
[BattleMutedSpritePreparation]::ExportExperienceBars($ProjectRoot)
