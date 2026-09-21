// 승인된 생성 원본의 배경 투명화와 균일 축소/중앙 정렬만 수행한다.
const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const root = path.resolve(__dirname, '../..');
const sourceDir = path.join(__dirname, 'Sources/SkillCardStates_v2');
const spriteDir = path.join(root, 'Assets/06.UI/LobbyMutedPreview/Skills_v1/Sprites');

function bounds(data, width, height) {
  let left = width, top = height, right = -1, bottom = -1;
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
    if (data[(y * width + x) * 4 + 3] <= 24) continue;
    left = Math.min(left, x); top = Math.min(top, y);
    right = Math.max(right, x); bottom = Math.max(bottom, y);
  }
  if (right < left) throw new Error('프레임 영역이 비어 있습니다.');
  return { left, top, width: right - left + 1, height: bottom - top + 1 };
}

function clearChecker(data, width, height, seeds) {
  const visited = new Uint8Array(width * height);
  const queue = new Int32Array(width * height);
  let head = 0, tail = 0;
  function visit(p) {
    if (visited[p]) return;
    const i = p * 4;
    const low = Math.min(data[i], data[i + 1], data[i + 2]);
    const high = Math.max(data[i], data[i + 1], data[i + 2]);
    // 외곽과 지정된 빈 구멍에 연결된 밝은 무채색만 제거한다. 금속/어두운 내부는 보존한다.
    if (data[i + 3] < 16 || (low > 90 && high - low < 18)) {
      visited[p] = 1; queue[tail++] = p;
    }
  }
  for (let x = 0; x < width; x++) { visit(x); visit((height - 1) * width + x); }
  for (let y = 0; y < height; y++) { visit(y * width); visit(y * width + width - 1); }
  for (const [x, y] of seeds) visit(Math.floor(y * height) * width + Math.floor(x * width));
  while (head < tail) {
    const p = queue[head++], x = p % width, y = Math.floor(p / width);
    if (x > 0) visit(p - 1); if (x + 1 < width) visit(p + 1);
    if (y > 0) visit(p - width); if (y + 1 < height) visit(p + width);
  }
  for (let p = 0; p < visited.length; p++) if (visited[p]) data.fill(0, p * 4, p * 4 + 4);
}

(async () => {
  const normal = await sharp(path.join(spriteDir, 'Card_Normal.png')).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const target = bounds(normal.data, normal.info.width, normal.info.height);
  const states = [
    { name: 'Card_Hover_Amber', seeds: [] },
    { name: 'Card_Selected_Gold', seeds: [[0.5, 0.105]] }
  ];
  const report = [];
  for (const state of states) {
    const source = path.join(sourceDir, state.name + '_Source.png');
    const { data, info } = await sharp(source).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    clearChecker(data, info.width, info.height, state.seeds);
    const crop = bounds(data, info.width, info.height);
    const scaled = await sharp(data, { raw: { width: info.width, height: info.height, channels: 4 } })
      .extract(crop).resize(target.width, target.height, { fit: 'inside', kernel: 'nearest' }).png().toBuffer();
    const size = await sharp(scaled).metadata();
    const left = target.left + Math.floor((target.width - size.width) / 2);
    const top = target.top + Math.floor((target.height - size.height) / 2);
    const output = path.join(spriteDir, state.name + '.png');
    await sharp({ create: { width: normal.info.width, height: normal.info.height, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } } })
      .composite([{ input: scaled, left, top }]).png().toFile(output);
    report.push({ name: state.name, crop, source: path.relative(root, source), output: path.relative(root, output), width: normal.info.width, height: normal.info.height, content: { left, top, width: size.width, height: size.height } });
  }
  fs.writeFileSync(path.join(sourceDir, 'Processing.json'), JSON.stringify(report, null, 2) + '\n');
  console.log(JSON.stringify(report, null, 2));
})().catch(error => { console.error(error); process.exitCode = 1; });
