/* 생성 원본의 배경 제거·슬라이스·시트 패킹 전용. Unity Scene/Prefab/Meta는 수정하지 않습니다. */
const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const ROOT = path.resolve(__dirname, '../../Assets/06.UI/LobbyMutedPreview/Overlays/TreeArt_v1');
const CELL = 256;
const CLEAR = { r: 0, g: 0, b: 0, alpha: 0 };
const branches = [
  { id: 'Legion', title: '마왕군 조련', color: '#809571', names: [
    ['Sharpness','예리함'],['Onslaught','맹공'],['Execution','처형 본능'],
    ['Toughness','강건함'],['UndyingFlesh','불굴의 살'],['IronArmor','철갑'],
    ['Agility','민첩'],['RepeatedStrikes','연격'],['PiercingVolley','관통 사격'],['LegionAdvance','마왕군의 진격']] },
  { id: 'Curse', title: '인간계 저주', color: '#9B80AB', names: [
    ['Weakness','쇠약'],['Powerlessness','무력화'],['Lethargy','나태'],
    ['Disarm','무장 해제'],['Shatter','파쇄'],['VulnerabilityMark','취약 각인'],
    ['Slowness','둔족'],['Quagmire','수렁'],['Mire','진창'],['DespairBrand','절망의 낙인']] },
  { id: 'Spells', title: '주문 연구', color: '#799DB8', names: [
    ['RuinEssence','파괴의 정수'],['PreciseSpell','정밀 주문'],['MagicPenetration','마력 관통'],
    ['SwiftSpell','신속한 주문'],['Acceleration','가속'],['TimeDistortion','시간 왜곡'],
    ['AreaDominion','광역 지배'],['LegionShout','군단의 함성'],['AdditionalCommand','추가 지령'],['DemonPower','마왕의 권능']] },
  { id: 'Wisdom', title: '지배의 지혜', color: '#B19663', names: [
    ['Insight','통찰'],['DeepInsight','심층 통찰'],['Plunder','약탈'],
    ['FastGrowth','속성 성장'],['ExperienceCrystal','숙련의 결정'],['Teaching','가르침'],
    ['Expansion','규모 확장'],['EfficientFormation','효율 편성'],['GreatArmy','대군세'],['RulerWisdom','지배자의 지혜']] }
];
const records = [];
const verification = [];
function rel(p) { return p.replaceAll('\\', '/'); }
async function save(input, file) {
  const full = path.join(ROOT, file);
  fs.mkdirSync(path.dirname(full), { recursive: true });
  await sharp(input).png().toFile(full);
  return full;
}
async function rgba(file) {
  return sharp(file).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
}
function png(data, width, height) {
  return sharp(data, { raw: { width, height, channels: 4 } }).png().toBuffer();
}
async function keyGreen(file) {
  const { data, info } = await rgba(file);
  let removed = 0;
  for (let i = 0; i < data.length; i += 4) {
    const excess = data[i + 1] - Math.max(data[i], data[i + 2]);
    // 채도가 낮은 실제 녹색 프레임은 보존하고, 고채도 분리용 배경만 제거합니다.
    if (excess > 64) {
      data[i] = data[i + 1] = data[i + 2] = data[i + 3] = 0;
      removed++;
    }
  }
  // 색상이 섞인 경계 픽셀의 녹색 잔광만 줄이고 안쪽의 실제 녹색 아트는 보존합니다.
  const original = Buffer.from(data);
  for (let y = 1; y < info.height - 1; y++) for (let x = 1; x < info.width - 1; x++) {
    const i = (y * info.width + x) * 4;
    if (!original[i + 3] || original[i + 1] <= Math.max(original[i], original[i + 2]) + 8) continue;
    const touchesMatte = [-1, 1, -info.width, info.width].some(d => !original[i + d * 4 + 3]);
    if (touchesMatte) data[i + 1] = Math.max(original[i], original[i + 2]) + 8;
  }
  if (removed < info.width * info.height * 0.12) throw Error('분리용 배경을 찾지 못했습니다: ' + file);
  return { data, width: info.width, height: info.height, buffer: await png(data, info.width, info.height) };
}
function bounds(data, width, height) {
  let minX = width, minY = height, maxX = -1, maxY = -1, count = 0;
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
    if (data[(y * width + x) * 4 + 3] < 32) continue;
    minX = Math.min(minX, x); minY = Math.min(minY, y);
    maxX = Math.max(maxX, x); maxY = Math.max(maxY, y); count++;
  }
  if (!count) throw Error('빈 스프라이트 영역');
  return { left: minX, top: minY, width: maxX - minX + 1, height: maxY - minY + 1, count };
}
async function normalize(buffer, region, visualSize, canvasSize = CELL) {
  const cut = await sharp(buffer).extract(region).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const b = bounds(cut.data, cut.info.width, cut.info.height);
  const art = await sharp(cut.data, { raw: { width: cut.info.width, height: cut.info.height, channels: 4 } })
    .extract({ left: b.left, top: b.top, width: b.width, height: b.height })
    .resize(visualSize, visualSize, { fit: 'inside', kernel: 'nearest' }).png().toBuffer({ resolveWithObject: true });
  return sharp({ create: { width: canvasSize, height: canvasSize, channels: 4, background: CLEAR } })
    .composite([{ input: art.data, left: Math.floor((canvasSize - art.info.width) / 2), top: Math.floor((canvasSize - art.info.height) / 2) }]).png().toBuffer();
}
function gridRegion(width, height, columns, rows, index) {
  const col = index % columns, row = Math.floor(index / columns);
  const left = Math.round(col * width / columns), top = Math.round(row * height / rows);
  return { left, top, width: Math.round((col + 1) * width / columns) - left, height: Math.round((row + 1) * height / rows) - top };
}
async function addSprite(buffer, file, fields) {
  await save(buffer, file);
  const { data, info } = await rgba(buffer);
  const b = bounds(data, info.width, info.height);
  let transparent = 0, greenLeaks = 0;
  for (let i = 0; i < data.length; i += 4) {
    if (!data[i + 3]) transparent++;
    else if (data[i + 1] - Math.max(data[i], data[i + 2]) > 64) greenLeaks++;
  }
  if (greenLeaks || !transparent) throw Error('알파 검증 실패: ' + file);
  verification.push({ file, width: info.width, height: info.height, alpha: true, transparentPixels: transparent, greenLeaks, contentBounds: b });
  const rec = { ...fields, file, width: info.width, height: info.height, pivot: { x: 0.5, y: 0.5 } };
  records.push(rec);
  return rec;
}
async function makeSheet(items, file, columns) {
  const rows = Math.ceil(items.length / columns);
  await save(await sharp({ create: { width: columns * CELL, height: rows * CELL, channels: 4, background: CLEAR } }).composite(
    items.map((r, i) => ({ input: path.join(ROOT, r.file), left: i % columns * CELL, top: Math.floor(i / columns) * CELL }))
  ).png().toBuffer(), file);
  items.forEach((r, i) => {
    r.sheets ??= [];
    r.sheets.push({ file, column: i % columns, rowFromTop: Math.floor(i / columns), rectTopLeft: { x: i % columns * CELL, y: Math.floor(i / columns) * CELL, width: CELL, height: CELL }, rectUnity: { x: i % columns * CELL, y: (rows - 1 - Math.floor(i / columns)) * CELL, width: CELL, height: CELL } });
  });
}
async function icons() {
  const core = await keyGreen(path.join(ROOT, 'Source/Icons_Core_Chroma.png'));
  const coreDefs = [['DemonKing','마왕','Core',136],['Header_Legion','마왕군 조련','Header',196],['Header_Wisdom','지배의 지혜','Header',196],['Header_Spells','주문 연구','Header',196],['Header_Curse','인간계 저주','Header',196]];
  for (let i = 0; i < coreDefs.length; i++) {
    const [id, label, kind, size] = coreDefs[i];
    await addSprite(await normalize(core.buffer, gridRegion(core.width, core.height, 3, 2, i), size), `Sprites/Icons/Core/Icon_${id}.png`, { id, label, kind, group: 'Core' });
  }
  for (const group of branches) {
    const source = await keyGreen(path.join(ROOT, `Source/Icons_${group.id}_Chroma.png`));
    for (let i = 0; i < group.names.length; i++) {
      const [id, label] = group.names[i];
      await addSprite(await normalize(source.buffer, gridRegion(source.width, source.height, 5, 2, i), 112), `Sprites/Icons/${group.id}/Icon_${group.id}_${id}.png`, { id: `${group.id}_${id}`, label, kind: i === 9 ? 'Specialized' : 'Normal', group: group.id, lane: i < 9 ? Math.floor(i / 3) : null, tier: i < 9 ? i % 3 + 1 : null });
    }
  }
  const all = records.filter(r => r.file.includes('/Icons/'));
  await makeSheet(all, 'Sheets/Icons_All_45.png', 9);
  for (const group of ['Core', ...branches.map(b => b.id)]) await makeSheet(all.filter(r => r.group === group), `Sheets/Icons_${group}.png`, 5);
}
function connectedComponents(data, width, height) {
  const seen = new Uint8Array(width * height), queue = new Int32Array(width * height), boxes = [];
  for (let start = 0; start < seen.length; start++) {
    if (seen[start] || data[start * 4 + 3] < 32) continue;
    let head = 0, tail = 0, count = 0, minX = width, minY = height, maxX = 0, maxY = 0;
    queue[tail++] = start; seen[start] = 1;
    while (head < tail) {
      const p = queue[head++], x = p % width, y = Math.floor(p / width);
      minX = Math.min(minX, x); minY = Math.min(minY, y); maxX = Math.max(maxX, x); maxY = Math.max(maxY, y); count++;
      for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
        const nx = x + dx, ny = y + dy;
        if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
        const n = ny * width + nx;
        if (!seen[n] && data[n * 4 + 3] >= 32) { seen[n] = 1; queue[tail++] = n; }
      }
    }
    if (count > 1000) boxes.push({ left: minX, top: minY, width: maxX - minX + 1, height: maxY - minY + 1, count });
  }
  return boxes;
}
async function frames() {
  const source = await keyGreen(path.join(ROOT, 'Source/Frames_Chroma.png'));
  const boxes = connectedComponents(source.data, source.width, source.height);
  if (boxes.length !== 9) throw Error(`프레임 개수 불일치: ${boxes.length}`);
  boxes.sort((a,b) => (Math.floor((a.top + a.height / 2) / (source.height / 3)) - Math.floor((b.top + b.height / 2) / (source.height / 3))) || a.left - b.left);
  const defs = [['Normal','Legion'],['Normal','Wisdom'],['Normal','Spells'],['Normal','Curse'],['Specialized','Legion'],['Specialized','Wisdom'],['Specialized','Spells'],['Specialized','Curse'],['Central','DemonKing']];
  for (let i = 0; i < defs.length; i++) {
    const [kind, group] = defs[i], { count, ...region } = boxes[i];
    await addSprite(await normalize(source.buffer, region, kind === 'Normal' ? 224 : 240), `Sprites/Frames/Frame_${kind}_${group}.png`, { id: `Frame_${kind}_${group}`, label: `${kind} / ${group}`, kind: 'Frame', frameType: kind, group });
  }
  await makeSheet(records.filter(r => r.kind === 'Frame'), 'Sheets/Frames_All_9.png', 3);
}
async function magicCircle() {
  const source = await keyGreen(path.join(ROOT, 'Source/MagicCircle_Chroma.png'));
  // 단색 잉크 장식의 경계에 남은 매트 혼합색을 제거합니다. 명도는 알파로 보존합니다.
  for (let i = 0; i < source.data.length; i += 4) {
    if (!source.data[i + 3]) continue;
    source.data[i + 3] = Math.min(255, Math.round(source.data[i] / 113 * 255));
    source.data[i] = 113; source.data[i + 1] = 50; source.data[i + 2] = 56;
  }
  const circle = await png(source.data, source.width, source.height);
  await addSprite(await normalize(circle, { left: 0, top: 0, width: source.width, height: source.height }, 960, 1024), 'Sprites/Decor/MagicCircle_Garnet.png', { id: 'MagicCircle_Garnet', label: '중앙 배경 마법진', kind: 'Decoration', recommendedAlpha: 0.22 });
}
async function composedTree() {
  // 체크무늬 원본의 글자를 훼손하지 않도록, 새로 분리한 아트를 같은 기획 구조로 재조립합니다.
  // 미리보기 이미지 합성일 뿐 Unity 오브젝트나 게임 로직을 생성하지 않습니다.
  const nodes=[], links=[], labels=[], layers=[];
  const center={x:1024,y:1024,r:104};
  const directions={Legion:{x:0,y:-1},Curse:{x:1,y:0},Spells:{x:0,y:1},Wisdom:{x:-1,y:0}};
  const put=(icon,frame,x,y,size,label)=>{nodes.push({icon,frame,x,y,size});if(label)labels.push({text:label,x,y:y+size/2+22});};
  put('Sprites/Icons/Core/Icon_DemonKing.png','Sprites/Frames/Frame_Central_DemonKing.png',1024,1024,220,'');
  for(const b of branches) {
    const d=directions[b.id];
    const place=(distance,lane)=>({x:1024+d.x*distance+(d.x===0?lane*184:0),y:1024+d.y*distance+(d.y===0?lane*184:0),r:63});
    const root=place(228,0);root.r=70;
    put(`Sprites/Icons/Core/Icon_Header_${b.id}.png`,null,root.x,root.y,170,b.title);
    links.push({a:center,b:root,color:b.color});
    const end=place(928,0);end.r=75;
    for(let lane=0;lane<3;lane++) {
      let prev=root;
      for(let tier=0;tier<3;tier++) {
        const p=place(416+tier*184,lane-1),[id,label]=b.names[lane*3+tier];
        put(`Sprites/Icons/${b.id}/Icon_${b.id}_${id}.png`,`Sprites/Frames/Frame_Normal_${b.id}.png`,p.x,p.y,144,label);
        links.push({a:prev,b:p,color:b.color});prev=p;
      }
      links.push({a:prev,b:end,color:b.color});
    }
    const [id,label]=b.names[9];
    put(`Sprites/Icons/${b.id}/Icon_${b.id}_${id}.png`,`Sprites/Frames/Frame_Specialized_${b.id}.png`,end.x,end.y,160,label);
    if(d.x===0) {labels[labels.length-1].x=end.x+200;labels[labels.length-1].y=end.y+8;}
  }
  const circle=await sharp(path.join(ROOT,'Sprites/Decor/MagicCircle_Garnet.png')).resize(620,620,{kernel:'nearest'}).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  for(let i=3;i<circle.data.length;i+=4)circle.data[i]=Math.round(circle.data[i]*0.35);
  layers.push({input:await png(circle.data,620,620),left:714,top:714});
  const lineSvg=Buffer.from(`<svg width="2048" height="2048">${links.map(l=>{
    const dx=l.b.x-l.a.x,dy=l.b.y-l.a.y,den=Math.abs(dx)+Math.abs(dy),t1=l.a.r/den,t2=l.b.r/den;
    return `<path d="M${l.a.x+dx*t1} ${l.a.y+dy*t1} L${l.b.x-dx*t2} ${l.b.y-dy*t2}" stroke="#111014" stroke-width="6"/><path d="M${l.a.x+dx*t1} ${l.a.y+dy*t1} L${l.b.x-dx*t2} ${l.b.y-dy*t2}" stroke="${l.color}" stroke-width="3"/>`;
  }).join('')}</svg>`);
  layers.push({input:lineSvg,left:0,top:0});
  for(const n of nodes)for(const file of [n.frame,n.icon].filter(Boolean))layers.push({input:await sharp(path.join(ROOT,file)).resize(n.size,n.size,{kernel:'nearest'}).png().toBuffer(),left:Math.round(n.x-n.size/2),top:Math.round(n.y-n.size/2)});
  const textSvg=Buffer.from(`<svg width="2048" height="2048"><style>text{font-family:Malgun Gothic,sans-serif;font-size:24px;font-weight:bold;fill:#eee2cd;stroke:#151217;stroke-width:5px;paint-order:stroke;stroke-linejoin:round;text-anchor:middle}</style>${labels.map(l=>`<text x="${l.x}" y="${Math.min(l.y,2040)}">${l.text}</text>`).join('')}</svg>`);
  layers.push({input:textSvg,left:0,top:0});
  const art=await sharp({create:{width:2048,height:2048,channels:4,background:CLEAR}}).composite(layers).png().toBuffer();
  await save(art,'Reference/Tree_Layout_Transparent.png');
  await sharp(art).flatten({background:'#17151A'}).png().toFile(path.join(ROOT,'Preview/Tree_Composed_Dark.png'));
}
async function previews() {
  const canvas = () => sharp({ create: { width: 1536, height: 1024, channels: 4, background: '#151316' } });
  const layers = [];
  const circle = await sharp(path.join(ROOT, 'Sprites/Decor/MagicCircle_Garnet.png')).resize(900,900,{kernel:'nearest'}).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  for (let i=3;i<circle.data.length;i+=4) circle.data[i] = Math.round(circle.data[i]*0.26);
  layers.push({input:await png(circle.data,900,900),left:318,top:56});
  // 실제로 별도 PNG를 겹친 결과를 QA용으로 저장합니다. 게임 스크린샷이 아닙니다.
  for (let i=0;i<branches.length;i++) {
    const b=branches[i], x=36+i*384;
    layers.push({input:path.join(ROOT,`Sprites/Icons/Core/Icon_Header_${b.id}.png`),left:x+28,top:12});
    layers.push({input:path.join(ROOT,`Sprites/Frames/Frame_Normal_${b.id}.png`),left:x+28,top:332});
    layers.push({input:path.join(ROOT,`Sprites/Icons/${b.id}/Icon_${b.id}_${b.names[0][0]}.png`),left:x+28,top:332});
    layers.push({input:path.join(ROOT,`Sprites/Frames/Frame_Specialized_${b.id}.png`),left:x+28,top:690});
    layers.push({input:path.join(ROOT,`Sprites/Icons/${b.id}/Icon_${b.id}_${b.names[9][0]}.png`),left:x+28,top:690});
  }
  await save(await canvas().composite(layers).png().toBuffer(),'Preview/Layered_Samples.png');
  const legend = Buffer.from(`<svg width="1536" height="1024"><style>text{font-family:Malgun Gothic,sans-serif;fill:#e5dace;text-anchor:middle;font-size:25px}</style>${branches.map((b,i)=>`<text x="${192+i*384}" y="270">${b.title}</text><text x="${192+i*384}" y="303" style="font-size:18px;fill:#a89eaa">계열 상징 · 테두리 없음</text><text x="${192+i*384}" y="626">일반 · ${b.names[0][1]}</text><text x="${192+i*384}" y="986">특화 · ${b.names[9][1]}</text>`).join('')}</svg>`);
  const composed = await sharp(path.join(ROOT,'Preview/Layered_Samples.png')).composite([{input:legend}]).png().toBuffer();
  await save(composed,'Preview/Layered_Samples.png');
  for (const [input, output] of [['Sheets/Icons_All_45.png','Preview/Icons_All_45_Dark.png'],['Sheets/Frames_All_9.png','Preview/Frames_All_9_Dark.png'],['Sprites/Decor/MagicCircle_Garnet.png','Preview/MagicCircle_Dark.png']]) {
    await sharp(path.join(ROOT,input)).flatten({background:'#19171B'}).png().toFile(path.join(ROOT,output));
  }
}
(async()=>{
  await icons(); await frames(); await magicCircle(); await previews(); await composedTree();
  const manifest = { version:1, cellSize:CELL, coordinateNote:'개별 PNG는 중심 pivot 0.5/0.5. 시트 top-left 및 Unity bottom-left rect 둘 다 제공.', layerOrder:['MagicCircle','Connections','Frame','Icon','Text'], sceneModified:false, records };
  fs.writeFileSync(path.join(ROOT,'AssetMap.json'),JSON.stringify(manifest,null,2)+'\n','utf8');
  fs.writeFileSync(path.join(ROOT,'Validation.json'),JSON.stringify({ sprites:verification.length, iconCount:45, frameCount:9, decorationCount:1, results:verification },null,2)+'\n','utf8');
  console.log(JSON.stringify({root:ROOT,icons:45,frames:9,decorations:1,totalSprites:verification.length,alphaVerified:verification.every(v=>v.alpha&&v.transparentPixels>0&&v.greenLeaks===0)},null,2));
})().catch(e=>{console.error(e);process.exitCode=1;});
