// 승인된 생성 이미지의 분할, 배경 정리, 균일 축소만 수행한다. 원본과 기존 에셋은 보존한다.
const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const root = path.resolve(__dirname, '../..');
const sources = path.join(__dirname, 'Sources/SkillSettings_v1');
const output = path.join(root, 'Assets/06.UI/LobbyMutedPreview/Skills_v1/Sprites');
const generated = 'C:/Users/sudea/.codex/generated_images/01a0a4a8-3799-7db1-907e-69bb9af378b8';
const clear = { r: 0, g: 0, b: 0, alpha: 0 };
const manifest = require('./BuildSkillSettingsAssets.inputs.json');

function eraseBackground(data, w, h, extraSeeds = []) {
  const seen = new Uint8Array(w * h), q = new Int32Array(w * h);
  let head = 0, tail = 0;
  function visit(p) {
    if (p < 0 || p >= w*h || seen[p]) return;
    const i=p*4, rgb=[data[i],data[i+1],data[i+2]];
    const min=Math.min(...rgb), max=Math.max(...rgb);
    if(data[i+3]<16 || (min>70 && max-min<16)) {seen[p]=1; q[tail++]=p;}
  }
  for(let x=0;x<w;x++){visit(x);visit((h-1)*w+x);}
  for(let y=0;y<h;y++){visit(y*w);visit(y*w+w-1);}
  for(const [x,y] of extraSeeds)visit(Math.floor(y*h)*w+Math.floor(x*w));
  while(head<tail){const p=q[head++],x=p%w,y=Math.floor(p/w);if(x)visit(p-1);if(x<w-1)visit(p+1);if(y)visit(p-w);if(y<h-1)visit(p+w);}
  for(let p=0;p<w*h;p++)if(seen[p])data.fill(0,p*4,p*4+4);
}

function bounds(data,w,h) {
  let l=w,t=h,r=-1,b=-1;
  for(let y=0;y<h;y++)for(let x=0;x<w;x++)if(data[(y*w+x)*4+3]>24){l=Math.min(l,x);r=Math.max(r,x);t=Math.min(t,y);b=Math.max(b,y);}
  if(r<l)throw new Error('Empty sprite');
  return {left:l,top:t,width:r-l+1,height:b-t+1};
}

async function exportCell(file, spec) {
  const {data,info}=await sharp(file).extract(spec.crop).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  let transparent=0;for(let i=3;i<data.length;i+=4)if(data[i]<16)transparent++;
  if(spec.chroma) {
    for(let i=0;i<data.length;i+=4)if(data[i+1]>data[i]+18&&data[i+1]>data[i+2]+18&&data[i+1]>60)data.fill(0,i,i+4);
  } else if(transparent<info.width*info.height*.15) {
    eraseBackground(data,info.width,info.height,spec.seeds||[]);
    // 아이콘 구멍 안에 갇힌 무채색 체크 배경도 제거. 아이보리/유색 심볼은 유지한다.
    if(spec.clearAllGray)for(let i=0;i<data.length;i+=4){const lo=Math.min(data[i],data[i+1],data[i+2]),hi=Math.max(data[i],data[i+1],data[i+2]);if(lo>70&&hi<235&&hi-lo<16)data.fill(0,i,i+4);}
  }
  const crop=bounds(data,info.width,info.height);
  const sprite=await sharp(data,{raw:{width:info.width,height:info.height,channels:4}}).extract(crop)
    .resize(spec.width-8,spec.height-8,{fit:'inside',kernel:'nearest'}).png().toBuffer();
  const meta=await sharp(sprite).metadata();
  const result=await sharp({create:{width:spec.width,height:spec.height,channels:4,background:clear}})
    .composite([{input:sprite,left:Math.floor((spec.width-meta.width)/2),top:Math.floor((spec.height-meta.height)/2)}]).png().toBuffer();
  const dest=path.join(output,spec.name+'.png');await sharp(result).toFile(dest);
  return {name:spec.name,source:path.relative(root,file).replaceAll('\\','/'),crop:spec.crop,foreground:crop,width:spec.width,height:spec.height};
}

(async()=>{
  fs.mkdirSync(sources,{recursive:true});fs.mkdirSync(output,{recursive:true});
  const report=[];
  for(const sheet of manifest.sheets){
    const source=path.join(sources,sheet.name+'.png');
    if(!fs.existsSync(source))fs.copyFileSync(path.join(generated,sheet.generated),source);
    if(sheet.background){fs.copyFileSync(source,path.join(output,'Skill_Background.png'));continue;}
    for(const cell of sheet.cells)report.push(await exportCell(source,{...cell,chroma:sheet.name==='Icons_Chroma'}));
  }
  const preview=path.join(root,'Temp/SkillSettingsValidation');fs.mkdirSync(preview,{recursive:true});
  const thumbSize=190,columns=6,layers=[];
  for(let i=0;i<report.length;i++){
    const buffer=await sharp(path.join(output,report[i].name+'.png')).resize(174,174,{fit:'inside',kernel:'nearest'}).png().toBuffer();
    layers.push({input:buffer,left:(i%columns)*thumbSize+8,top:Math.floor(i/columns)*thumbSize+8});
  }
  await sharp({create:{width:columns*thumbSize,height:Math.ceil(report.length/columns)*thumbSize,channels:4,background:'#252830'}}).composite(layers).png().toFile(path.join(preview,'sprites-contact-sheet.png'));
  fs.writeFileSync(path.join(root,'Assets/06.UI/LobbyMutedPreview/Skills_v1/ArtValidation.json'),JSON.stringify(report,null,2)+'\n');
  console.log(JSON.stringify({spriteCount:report.length,output,preview},null,2));
})().catch(error=>{console.error(error);process.exitCode=1;});
