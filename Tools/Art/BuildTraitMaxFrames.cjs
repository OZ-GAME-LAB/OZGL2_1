// 승인된 생성 이미지의 배경 제거/크기 정렬만 수행한다. 기존 아트와 Unity Meta는 변경하지 않는다.
const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const manifest = require('./BuildTraitMaxFrames.inputs.json');
const PROJECT = path.resolve(__dirname, '../..');
const OUTPUT = path.join(PROJECT, 'Assets/06.UI/LobbyMutedPreview/Overlays/TreeArt_v1/MaxLevel');
const CLEAR = {r:0,g:0,b:0,alpha:0};
const SIZE = 256;

function bounds(data, width, height) {
  let left=width, top=height, right=-1, bottom=-1;
  for(let y=0;y<height;y++) for(let x=0;x<width;x++) if(data[(y*width+x)*4+3]>24) {
    left=Math.min(left,x);right=Math.max(right,x);top=Math.min(top,y);bottom=Math.max(bottom,y);
  }
  if(right<left) throw Error('Empty sprite');
  return {left,top,width:right-left+1,height:bottom-top+1};
}

function clearExterior(data, width, height) {
  const total=width*height, seen=new Uint8Array(total), queue=new Int32Array(total);
  let head=0,tail=0;
  const isBackground=p=>{
    const i=p*4,r=data[i],g=data[i+1],b=data[i+2];
    return data[i+3]<16 || (Math.min(r,g,b)>135 && Math.max(r,g,b)-Math.min(r,g,b)<38)
      || (r>170 && b>150 && Math.min(r,b)-g>80);
  };
  const visit=p=>{if(!seen[p] && isBackground(p)){seen[p]=1;queue[tail++]=p;}};
  for(let x=0;x<width;x++){visit(x);visit((height-1)*width+x);}
  for(let y=0;y<height;y++){visit(y*width);visit(y*width+width-1);}
  while(head<tail){const p=queue[head++],x=p%width,y=Math.floor(p/width);
    if(x>0)visit(p-1);if(x+1<width)visit(p+1);if(y>0)visit(p-width);if(y+1<height)visit(p+width);
  }
  if(tail<total*0.20) throw Error('Exterior background not found');
  for(let p=0;p<total;p++) if(seen[p]) data.fill(0,p*4,p*4+4);
  // 체크 배경의 압축 잡점이 독립 섬으로 남으면 제거한다. 프레임과 내부는 하나의 연결 영역이다.
  seen.fill(0);let largest=[];
  for(let start=0;start<total;start++){
    if(seen[start] || data[start*4+3]<16)continue;
    head=0;tail=1;queue[0]=start;seen[start]=1;
    const add=p=>{if(!seen[p] && data[p*4+3]>=16){seen[p]=1;queue[tail++]=p;}};
    while(head<tail){const p=queue[head++],x=p%width,y=Math.floor(p/width);
      if(x>0)add(p-1);if(x+1<width)add(p+1);if(y>0)add(p-width);if(y+1<height)add(p+width);
    }
    if(tail>largest.length)largest=Array.from(queue.subarray(0,tail));
  }
  const keep=new Uint8Array(total);for(const p of largest)keep[p]=1;
  for(let p=0;p<total;p++)if(!keep[p])data.fill(0,p*4,p*4+4);
}

(async()=>{
  fs.mkdirSync(OUTPUT,{recursive:true});
  const results=[],layers=[];
  for(let index=0;index<manifest.specs.length;index++){
    const item=manifest.specs[index];
    const original=await sharp(path.join(PROJECT,item.inputFrame)).ensureAlpha().raw().toBuffer({resolveWithObject:true});
    const originalBounds=bounds(original.data,original.info.width,original.info.height);
    const source=await sharp(path.join(__dirname,item.source)).ensureAlpha().raw().toBuffer({resolveWithObject:true});
    clearExterior(source.data,source.info.width,source.info.height);
    const cropped=bounds(source.data,source.info.width,source.info.height);
    const art=await sharp(source.data,{raw:{width:source.info.width,height:source.info.height,channels:4}})
      .extract(cropped).resize(Math.max(originalBounds.width,originalBounds.height),Math.max(originalBounds.width,originalBounds.height),{fit:'inside',kernel:'nearest'})
      .png().toBuffer({resolveWithObject:true});
    const output=path.join(OUTPUT,`Frame_${item.key}_Max.png`);
    await sharp({create:{width:SIZE,height:SIZE,channels:4,background:CLEAR}})
      .composite([{input:art.data,left:Math.floor((SIZE-art.info.width)/2),top:Math.floor((SIZE-art.info.height)/2)}]).png().toFile(output);
    const final=await sharp(output).ensureAlpha().raw().toBuffer();
    let transparent=0;for(let i=3;i<final.length;i+=4)if(final[i]===0)transparent++;
    if(transparent<SIZE*SIZE*.2 || final[(128*SIZE+128)*4+3]!==255) throw Error('Invalid alpha/interior: '+item.key);
    const b=bounds(final,SIZE,SIZE);
    results.push({key:item.key,file:path.basename(output),width:SIZE,height:SIZE,transparentPixels:transparent,center:Array.from(final.subarray((128*SIZE+128)*4,(128*SIZE+128)*4+4)),bounds:b,originalBounds});
    layers.push({input:output,left:(index%4)*280+12,top:Math.floor(index/4)*302+30});
  }
  const text=Buffer.from(`<svg width="1120" height="634"><style>text{font-family:Arial;fill:#e5dace;font-size:16px;text-anchor:middle}</style>${manifest.specs.map((s,i)=>`<text x="${i%4*280+140}" y="${Math.floor(i/4)*302+22}">${s.key.replace('_',' / ')}</text>`).join('')}</svg>`);
  const preview=path.join(PROJECT,'Temp/TraitTreeValidation/max-frames-eight.png');
  fs.mkdirSync(path.dirname(preview),{recursive:true});
  await sharp({create:{width:1120,height:634,channels:4,background:'#151316'}}).composite([...layers,{input:text,left:0,top:0}]).png().toFile(preview);
  fs.writeFileSync(path.join(OUTPUT,'Validation.json'),JSON.stringify({generator:'built-in image_gen',postprocess:'approved background removal and nearest-neighbor size alignment only',results},null,2)+'\n');
  console.log(JSON.stringify({sprites:results.length,preview,output:OUTPUT,results},null,2));
})().catch(e=>{console.error(e);process.exitCode=1;});
