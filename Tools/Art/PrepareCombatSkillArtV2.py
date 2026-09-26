"""레퍼런스 비율에 맞춘 배경/여백/크기/중심/대칭 정리. 아이콘은 읽거나 수정하지 않는다."""
from collections import deque
from pathlib import Path
import importlib.util
import json
import sys
import numpy as np
from PIL import Image

sys.dont_write_bytecode = True

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Tools/Art/Sources/CombatSkills_v2'
DEST = ROOT / 'Assets/06.UI/BattleMutedPreview/CombatSkills_v2/Sprites'
spec = importlib.util.spec_from_file_location('combat_v1', Path(__file__).with_name('PrepareCombatSkillArt.py'))
legacy = importlib.util.module_from_spec(spec)
spec.loader.exec_module(legacy)


def largest_component(mask):
    h, w = mask.shape
    visited = np.zeros_like(mask)
    largest = []
    for pos in np.flatnonzero(mask):
        y, x = divmod(int(pos), w)
        if visited[y, x]:
            continue
        queue = deque([(x, y)])
        visited[y, x] = True
        points = []
        while queue:
            x, y = queue.popleft()
            points.append((x, y))
            for nx, ny in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
                if 0 <= nx < w and 0 <= ny < h and mask[ny, nx] and not visited[ny, nx]:
                    visited[ny, nx] = True
                    queue.append((nx, ny))
        if len(points) > len(largest):
            largest = points
    result = np.zeros_like(mask)
    xy = np.array(largest)
    result[xy[:, 1], xy[:, 0]] = True
    return result


def clean_ring(path):
    data = np.array(Image.open(path).convert('RGBA'))
    rgb = data[:, :, :3].astype(np.int16)
    if data[:, :, 3].min() == 255:
        # 생성된 따뜻한 흰색 링과 무채색 체크무늬를 분리한다.
        candidate = (rgb[:, :, 0] > 222) & (rgb[:, :, 1] > 211) & (rgb[:, :, 0] - rgb[:, :, 2] > 7)
    else:
        candidate = (data[:, :, 3] > 127) & (rgb.max(2) > 180)
    mask = largest_component(candidate)
    data[~mask, 3] = 0
    data[data[:, :, 3] == 0, :3] = 0
    return Image.fromarray(data)


def aligned(image, canvas_size, visible_size, both_axes=False):
    crop = image.crop(image.getchannel('A').getbbox())
    # BOX 축소는 원본 윤곽의 픽셀 커버리지를 보존해 얇은 밝은 선이 사라지지 않게 한다.
    fitted = crop.resize((visible_size, visible_size), Image.Resampling.BOX)
    half = visible_size // 2
    left = fitted.crop((0, 0, half, visible_size))
    fitted.paste(left.transpose(Image.Transpose.FLIP_LEFT_RIGHT), (half, 0))
    if both_axes:
        top = fitted.crop((0, 0, visible_size, half))
        fitted.paste(top.transpose(Image.Transpose.FLIP_TOP_BOTTOM), (0, half))
    result = Image.new('RGBA', (canvas_size, canvas_size))
    inset = (canvas_size - visible_size) // 2
    result.paste(fitted, (inset, inset))
    return result


def align_velvet():
    # 기존 생성 벨벳 픽셀을 보존하고 기준선 교점의 좌표만 정렬한다.
    old = np.array(Image.open(ROOT / 'Assets/06.UI/BattleMutedPreview/CombatSkills_v1/Sprites/Bottom_Velvet.png').convert('RGBA'))
    target_x = [0, 18, 62, 324, 425, 960, 1495, 1596, 1858, 1902, 1919]
    source_x = [0, 22, 80, 408, 479, 960, 1441, 1512, 1840, 1898, 1919]
    # 윗변 y=9, 날개 윗변 y=110, 바닥 y=247. 경사는 101×101(45도).
    xs = np.rint(np.interp(np.arange(1920), target_x, source_x)).astype(int)
    ys = np.rint(np.interp(np.arange(256), [0, 9, 110, 247, 255], [0, 9, 80, 247, 255])).astype(int)
    data = old[ys[:, None], xs[None, :]]
    data[:, 960:] = data[:, :960][:, ::-1]
    return Image.fromarray(data)


def main():
    DEST.mkdir(parents=True, exist_ok=True)
    records = []
    for name in ('Frame_Damage', 'Frame_Buff', 'Frame_Debuff', 'Ring_Track', 'Ring_Fill', 'Bottom_Velvet'):
        if name == 'Bottom_Velvet':
            image = align_velvet()
        elif name.startswith('Frame_'):
            image = aligned(legacy.clean_source(SOURCE / (name + '_source.png'), True), 336, 314)
        else:
            image = aligned(clean_ring(SOURCE / (name + '_source.png')), 200, 184, True)
        data = np.array(image)
        data[data[:, :, 3] == 0, :3] = 0
        image = Image.fromarray(data)
        image.save(DEST / (name + '.png'))
        records.append({'name': name, 'size': image.size, 'alpha_bbox': image.getchannel('A').getbbox(),
                        'transparent_pixels': int((data[:, :, 3] == 0).sum()),
                        'horizontal_alpha_symmetry': bool(np.array_equal(data[:, :, 3], data[:, ::-1, 3]))})
    (SOURCE / 'normalization.json').write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(records, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
