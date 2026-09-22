# 승인된 알파 여백/크기 정렬만 수행한다. 기존 원본 아트는 수정하지 않는다.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sourceRoot = Join-Path $PSScriptRoot 'Sources/SkillRoster_v2'
$outputRoot = Join-Path $projectRoot 'Assets/06.UI/LobbyMutedPreview/Skills_v2/Sprites'
New-Item -ItemType Directory -Force -Path $sourceRoot,$outputRoot | Out-Null
$v1 = 'Assets/06.UI/LobbyMutedPreview/Skills_v1/Sprites/'
$trait = 'Assets/06.UI/LobbyMutedPreview/Overlays/TreeArt_v1/Sprites/Icons/'
$reuse = [ordered]@{
    Icon_Fire = $v1+'Icon_Fire.png'
    Icon_FireTornado = $v1+'Icon_Vortex.png'
    Icon_Meteor = $v1+'Icon_Strike.png'
    Icon_VoidCollapse = $v1+'Icon_Abyss.png'
    Icon_SlowSwamp = $v1+'Icon_Portal.png'
    Icon_CurseMark = $v1+'Icon_Curse.png'
    Icon_TimeStop = $trait+'Spells/Icon_Spells_Acceleration.png'
    Icon_Berserk = $trait+'Legion/Icon_Legion_RepeatedStrikes.png'
    Icon_IronSkin = $trait+'Legion/Icon_Legion_IronArmor.png'
    Icon_Judgment = $v1+'Icon_Trident.png'
}

function Convert-RosterArt([string]$sourcePath, [string]$assetName) {
    $source = [Drawing.Bitmap]::new($sourcePath)
    try {
        $w=$source.Width; $h=$source.Height
        $rect=[Drawing.Rectangle]::new(0,0,$w,$h)
        $locked=$source.LockBits($rect,[Drawing.Imaging.ImageLockMode]::ReadOnly,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $stride=$locked.Stride
            $bytes=[byte[]]::new($stride*$h)
            [Runtime.InteropServices.Marshal]::Copy($locked.Scan0,$bytes,0,$bytes.Length)
        } finally { $source.UnlockBits($locked) }
        $left=$w; $top=$h; $right=-1; $bottom=-1; $transparent=$false
        for($y=0;$y -lt $h;$y++) { for($x=0;$x -lt $w;$x++) {
            $a=$bytes[$y*$stride+$x*4+3]
            if($a -eq 0) { $transparent=$true }
            if($a -gt 24) {
                $left=[Math]::Min($left,$x); $right=[Math]::Max($right,$x)
                $top=[Math]::Min($top,$y); $bottom=[Math]::Max($bottom,$y)
            }
        }}
        if(!$transparent -or $right -lt $left) { throw "실제 알파/아트가 없습니다: $assetName" }
        $size=192; $padding=18
        if($assetName.StartsWith('Ultimate_')) { $size=256; $padding=3 }
        $bw=$right-$left+1; $bh=$bottom-$top+1
        $scale=[Math]::Min(($size-2*$padding)/$bw,($size-2*$padding)/$bh)
        $dw=[int][Math]::Round($bw*$scale); $dh=[int][Math]::Round($bh*$scale)
        $dest=[Drawing.Bitmap]::new($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics=[Drawing.Graphics]::FromImage($dest)
        try {
            $graphics.Clear([Drawing.Color]::Transparent)
            $graphics.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
            $graphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
            $target=[Drawing.Rectangle]::new([int][Math]::Floor(($size-$dw)/2),[int][Math]::Floor(($size-$dh)/2),$dw,$dh)
            $graphics.DrawImage($source,$target,$left,$top,$bw,$bh,[Drawing.GraphicsUnit]::Pixel)
            $dest.Save((Join-Path $outputRoot ($assetName+'.png')),[Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $dest.Dispose() }
        [pscustomobject]@{Name=$assetName;Source=$sourcePath;Size=$size;Bounds=@($left,$top,$bw,$bh)}
    } finally { $source.Dispose() }
}

$report=@()
$manifest=Get-Content -LiteralPath (Join-Path $sourceRoot 'generation.json') -Raw | ConvertFrom-Json
foreach($item in $manifest) {
    $original=Join-Path $sourceRoot ($item.name+'_Original.png')
    if(!(Test-Path -LiteralPath $original)) { Copy-Item -LiteralPath $item.source -Destination $original }
    $report+=Convert-RosterArt $original $item.name
}
foreach($item in $reuse.GetEnumerator()) { $report+=Convert-RosterArt (Join-Path $projectRoot $item.Value) $item.Key }
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $sourceRoot 'normalization.json') -Encoding utf8

$files=@(Get-ChildItem -LiteralPath $outputRoot -Filter '*.png' | Sort-Object Name)
$sheet=[Drawing.Bitmap]::new(1160,[int]([Math]::Ceiling($files.Count/5)*236))
$g=[Drawing.Graphics]::FromImage($sheet)
$font=[Drawing.Font]::new('Consolas',10)
try {
    $g.Clear([Drawing.Color]::FromArgb(23,21,25))
    $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    for($i=0;$i -lt $files.Count;$i++) {
        $art=[Drawing.Bitmap]::new($files[$i].FullName)
        try {
            $x=($i%5)*232; $y=[int][Math]::Floor($i/5)*236
            $g.DrawImage($art,[Drawing.Rectangle]::new($x+20,$y+8,192,192))
            $g.DrawString($files[$i].BaseName,$font,[Drawing.Brushes]::Ivory,$x+6,$y+210)
        } finally { $art.Dispose() }
    }
    $previewRoot=Join-Path $projectRoot 'Temp/SkillRoster_v2'
    New-Item -ItemType Directory -Force -Path $previewRoot | Out-Null
    $sheet.Save((Join-Path $previewRoot 'ArtContactSheet.png'),[Drawing.Imaging.ImageFormat]::Png)
} finally { $g.Dispose(); $font.Dispose(); $sheet.Dispose() }
Write-Output ('Prepared '+$report.Count+' assets; originals and source alpha retained.')
