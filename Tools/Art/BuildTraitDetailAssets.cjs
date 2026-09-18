// 승인된 생성 아트만 후처리한다. 외부 원본, Unity Meta, 기존 공용 Sprite는 변경하지 않는다.
const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const manifest = require('./BuildTraitDetailAssets.inputs.json');
const project = path.resolve(__dirname, '../..');
const sourceDir = path.join(__dirname, 'Sources/TraitDetailRefresh');
const outputDir = path.join(project, 'Assets/06.UI/LobbyMutedPreview/Overlays/DetailArt_v2');
const generatedDir = 'C:/Users/sudea/.codex/generated_images/01a0a4a8-3799-7db1-907e-69bb9af378b8';
const files = [
  'exec-b1a82e0f-12cf-40fe-a807-116150ee588e.png',
  'exec-1a65c3aa-d58d-4b08-8334-82f96260c6bf.png',
  'exec-06c45fb8-262b-4d21-a2e0-7210024c2ca8.png',
  'exec-6d4e94db-f63c-4c89-b2c1-8f86c2c9a11b.png',
  'exec-d5cf50a8-c91a-4f33-a2bd-9de2b45e521d.png',
  'exec-db8d08df-bec2-4646-a230-411b4f5801ab.png'
];
const clear = {r:0,g:0,b:0,alpha:0};
function bounds(data,w,h) {
  let left=w,top=h,right=-1,bottom=-1;
  for(let y=0;y<h;y++)for(let x=0;x<w;x++)if(data[(y*w+x)*4+3]>32){
    left=Math.min(left,x);right=Math.max(right,x);top=Math.min(top,y);bottom=Math.max(bottom,y);
  }
  if(right<left)throw Error('Empty image');
  return {left,top,width:right-left+1,height:bottom-top+1};
}
function clearExterior(data,w,h) {
  const n=w*h,seen=new Uint8Array(n),queue=new Int32Array(n);let head=0,tail=0;
  const visit=p=>{if(seen[p])return;const i=p*4,r=data[i],g=data[i+1],b=data[i+2];
    if(data[i+3]<16 || (Math.min(r,g,b)>135 && Math.max(r,g,b)-Math.min(r,g,b)<38)){
      seen[p]=1;queue[tail++]=p;
    }
  };
  for(let x=0;x<w;x++){visit(x);visit((h-1)*w+x);}for(let y=0;y<h;y++){visit(y*w);visit(y*w+w-1);}
  while(head<tail){const p=queue[head++],x=p%w,y=Math.floor(p/w);if(x)visit(p-1);if(x+1<w)visit(p+1);if(y)visit(p-w);if(y+1<h)visit(p+w);}
  for(let p=0;p<n;p++)if(seen[p])data.fill(0,p*4,p*4+4);
  // 배경 체크의 고립된 작은 조각만 정리. 새 그림을 그리지 않는다.
  seen.fill(0);let largest=[];
  for(let start=0;start<n;start++){
    if(seen[start]||data[start*4+3]<16)continue;
    head=0;tail=1;queue[0]=start;seen[start]=1;
    const add=p=>{if(!seen[p]&&data[p*4+3]>=16){seen[p]=1;queue[tail++]=p;}};
    while(head<tail){const p=queue[head++],x=p%w,y=Math.floor(p/w);if(x)add(p-1);if(x+1<w)add(p+1);if(y)add(p-w);if(y+1<h)add(p+w);}
    if(tail>largest.length)largest=Array.from(queue.subarray(0,tail));
  }
  const keep=new Uint8Array(n);for(const p of largest)keep[p]=1;
  for(let p=0;p<n;p++)if(!keep[p])data.fill(0,p*4,p*4+4);
}
async function shortenEmptyCenter(buffer,width,height,targetRatio) {
  const wanted=Math.round(height*targetRatio);
  if(wanted>=width)return {buffer,width,height,removed:0};
  const leftWidth=Math.floor(wanted/2),rightWidth=wanted-leftWidth;
  // 모서리는 그대로 두고, 비어 있는 중앙 부분/직선 레일만 잘라 합친다. X/Y 개별 늘리기 금지.
  const left=await sharp(buffer).extract({left:0,top:0,width:leftWidth,height}).png().toBuffer();
  const right=await sharp(buffer).extract({left:width-rightWidth,top:0,width:rightWidth,height}).png().toBuffer();
  const result=await sharp({create:{width:wanted,height,channels:4,background:clear}})
    .composite([{input:left,left:0,top:0},{input:right,left:leftWidth,top:0}]).png().toBuffer();
  return {buffer:result,width:wanted,height,removed:width-wanted};
}
(async()=>{
  fs.mkdirSync(sourceDir,{recursive:true});fs.mkdirSync(outputDir,{recursive:true});
  const results=[],layers=[];
  for(let i=0;i<manifest.specs.length;i++){
    const spec=manifest.specs[i],source=path.join(sourceDir,spec.key+'.png');
    if(!fs.existsSync(source))fs.copyFileSync(path.join(generatedDir,files[i]),source);
    const {data,info}=await sharp(source).ensureAlpha().raw().toBuffer({resolveWithObject:true});
    let transparent=0;for(let p=3;p<data.length;p+=4)if(data[p]<128)transparent++;
    const removedBackground=transparent<info.width*info.height*.1;
    if(removedBackground)clearExterior(data,info.width,info.height);
    const crop=bounds(data,info.width,info.height);
    let buffer=await sharp(data,{raw:{width:info.width,height:info.height,channels:4}}).extract(crop).png().toBuffer();
    let removed=0;
    if(spec.key.startsWith('Button_')){
      const fit=await shortenEmptyCenter(buffer,crop.width,crop.height,(spec.w-8)/(spec.h-8));buffer=fit.buffer;removed=fit.removed;
    }
    buffer=await sharp(buffer).resize(spec.w-8,spec.h-8,{fit:'inside',kernel:'nearest'}).png().toBuffer();
    const size=await sharp(buffer).metadata();
    buffer=await sharp({create:{width:spec.w,height:spec.h,channels:4,background:clear}})
      .composite([{input:buffer,left:Math.floor((spec.w-size.width)/2),top:Math.floor((spec.h-size.height)/2)}]).png().toBuffer();
    if(spec.key==='Frame_DemonKing_Symmetric'){
      // 한 사분면을 두 축으로 반사하여 픽셀 단위 대칭을 보장한다.
      const q=await sharp(buffer).extract({left:0,top:0,width:128,height:128}).png().toBuffer();
      buffer=await sharp({create:{width:256,height:256,channels:4,background:clear}}).composite([
        {input:q,left:0,top:0},{input:await sharp(q).flop().toBuffer(),left:128,top:0},
        {input:await sharp(q).flip().toBuffer(),left:0,top:128},{input:await sharp(q).flip().flop().toBuffer(),left:128,top:128}
      ]).png().toBuffer();
    }
    const output=path.join(outputDir,spec.key+'.png');await sharp(buffer).toFile(output);
    const raw=await sharp(buffer).ensureAlpha().raw().toBuffer();
    const alpha=raw.filter((v,p)=>p%4===3&&v===0).length;
    if(alpha===0)throw Error('Missing alpha: '+spec.key);
    let mismatch=0;
    if(i===5)for(let y=0;y<256;y++)for(let x=0;x<256;x++)for(let c=0;c<4;c++){
      if(raw[(y*256+x)*4+c]!==raw[(y*256+255-x)*4+c]||raw[(y*256+x)*4+c]!==raw[((255-y)*256+x)*4+c])mismatch++;
    }
    if(mismatch)throw Error('Symmetry mismatch');
    results.push({key:spec.key,source:'Tools/Art/Sources/TraitDetailRefresh/'+spec.key+'.png',generatedOriginal:files[i],width:spec.w,height:spec.h,crop,removedEmptyCenterColumns:removed,removedBackground,transparentPixels:alpha,symmetryMismatches:mismatch});
    layers.push({input:output,left:30,top:25+i*180});
  }
  const preview=path.join(project,'Temp/TraitTreeValidation/detail-art-v2.png');fs.mkdirSync(path.dirname(preview),{recursive:true});
  await sharp({create:{width:720,height:1190,channels:4,background:'#26262c'}}).composite(layers).png().toFile(preview);
  fs.writeFileSync(path.join(outputDir,'Validation.json'),JSON.stringify({mode:manifest.mode,postprocess:'approved exterior cleanup, central empty-span crop, uniform nearest-neighbor fit and frame mirror symmetry',results},null,2)+'\n');
  console.log(JSON.stringify({outputDir,preview,results},null,2));
})().catch(e=>{console.error(e);process.exitCode=1;});
