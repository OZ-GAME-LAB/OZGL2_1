param([string]$ProjectRoot='D:/GitHub/OZGL2_1')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$outDir=$PSScriptRoot
foreach($tier in @('Silver','Gold','Platinum')) {
    $src=[System.Drawing.Bitmap]::new((Join-Path $ProjectRoot ('Assets/06.UI/BattleMutedPreview/Overlays_v1/Augment/Augment_Body_'+$tier+'.png')))
    $qa=[System.Drawing.Bitmap]::new(1200,800)
    $g=[System.Drawing.Graphics]::FromImage($qa)
    $g.Clear([System.Drawing.Color]::FromArgb(210,210,210))
    $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $top=if($tier -eq 'Platinum'){155}else{220}
    $g.DrawImage($src,[System.Drawing.Rectangle]::new(0,0,1200,150),[System.Drawing.Rectangle]::new(0,$top,600,75),[System.Drawing.GraphicsUnit]::Pixel)
    $g.DrawImage($src,[System.Drawing.Rectangle]::new(0,170,1200,150),[System.Drawing.Rectangle]::new(0,770,600,75),[System.Drawing.GraphicsUnit]::Pixel)
    $g.DrawImage($src,[System.Drawing.Rectangle]::new(100,340,1000,440),[System.Drawing.Rectangle]::new(175,885,250,110),[System.Drawing.GraphicsUnit]::Pixel)
    $qa.Save((Join-Path $outDir ('SourceEdges_'+$tier+'.png')),[System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose();$qa.Dispose();$src.Dispose()
}
