# 승인 목업의 실제 장식 픽셀에서 배경만 제거하고 부품을 분리한다. 원본 색/디자인은 다시 그리지 않는다.
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source = Join-Path $projectRoot 'Docs/UI/References/LobbyDifficulty/difficulty_record_frame_hover_v2.png'
$destination = Join-Path $projectRoot 'Assets/06.UI/LobbyMutedPreview/DifficultySelection_v1/RecordHover/Sprites'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing.Common,System.Drawing.Primitives,System.Runtime,System.Private.Windows.GdiPlus,System.Private.Windows.Core -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
public static class ApprovedRecordExtraction
{
    static bool Box(int x,int y,int l,int t,int r,int b){return x>=l&&x<=r&&y>=t&&y<=b;}
    static double Distance(int x,int y,double ax,double ay,double bx,double by){double dx=bx-ax,dy=by-ay;double u=Math.Max(0,Math.Min(1,((x-ax)*dx+(y-ay)*dy)/(dx*dx+dy*dy)));return Math.Sqrt(Math.Pow(x-ax-u*dx,2)+Math.Pow(y-ay-u*dy,2));}
    static bool Region(int x,int y){
        int mx=Math.Min(x,523-x);
        return Box(x,y,85,12,438,32)||Box(x,y,242,0,278,40)
          ||Distance(mx,y,85,21,34,70)<13||Distance(mx,y,34,70,34,149)<10
          ||(Math.Abs(mx-34)/38.0+Math.Abs(y-168)/30.0<1.05)
          ||Distance(mx,y,37,179,86,218)<13
          ||Box(x,y,82,204,440,226)||Box(x,y,241,198,279,233);
    }
    static bool Metal(Color c){return (c.R>42&&c.R>c.G*1.38&&c.R>c.B*1.18)||(c.R>145&&c.G>95&&c.B>65);}
    public static void Extract(string source,string destination){
        using(Bitmap input=new Bitmap(source))
        using(Bitmap crop=input.Clone(new Rectangle(994,491,524,241),PixelFormat.Format32bppArgb))
        using(Bitmap frame=new Bitmap(524,234,PixelFormat.Format32bppArgb)){
            // 좁은 장식 영역에서 원래 금속색을 찾고 2px 검정 외곽까지 보존한다.
            for(int y=0;y<234;y++)for(int x=0;x<524;x++){
                if(!Region(x,y))continue;bool keep=false;
                for(int oy=-2;oy<=2&&!keep;oy++)for(int ox=-2;ox<=2;ox++){
                    int xx=x+ox,yy=y+oy;if(xx<0||yy<0||xx>=524||yy>=234||!Region(xx,yy))continue;
                    if(Metal(crop.GetPixel(xx,yy))){keep=true;break;}
                }
                if(keep)frame.SetPixel(x,y,crop.GetPixel(x,y));
            }
            Save(frame,new Rectangle(0,0,524,70),destination+"/Ref_Top.png",false);
            Save(frame,new Rectangle(0,140,524,94),destination+"/Ref_Bottom.png",false);
            Save(frame,new Rectangle(28,80,17,60),destination+"/Ref_LeftRail.png",false);
            Save(frame,new Rectangle(476,80,17,60),destination+"/Ref_RightRail.png",false);
            Save(crop,new Rectangle(82,50,34,36),destination+"/Ref_Trophy.png",true);
            Save(crop,new Rectangle(313,48,30,38),destination+"/Ref_Hourglass.png",true);
            Save(crop,new Rectangle(70,114,380,16),destination+"/Ref_Divider.png",true);
            Save(crop,new Rectangle(271,44,8,68),destination+"/Ref_VerticalDivider.png",true);
            Save(crop,new Rectangle(81,161,362,8),destination+"/Ref_NameDivider.png",false,true);
        }
    }
    static void Save(Bitmap image,Rectangle rect,string path,bool gold,bool red=false){
        using(Bitmap output=new Bitmap(rect.Width,rect.Height,PixelFormat.Format32bppArgb)){
            for(int y=0;y<rect.Height;y++)for(int x=0;x<rect.Width;x++){
                Color c=image.GetPixel(rect.X+x,rect.Y+y);
                if(gold&&(c.R<100||c.G<75||c.B>c.R*.85))continue;
                if(red&&(c.R<45||c.R<c.G*1.4))continue;
                output.SetPixel(x,y,c);
            }
            output.Save(path,ImageFormat.Png);
        }
    }
}
'@
[ApprovedRecordExtraction]::Extract($source,$destination)
Get-ChildItem -LiteralPath $destination -Filter 'Ref_*.png' | Select-Object Name,Length
