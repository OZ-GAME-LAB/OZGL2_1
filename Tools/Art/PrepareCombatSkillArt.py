"""승인된 생성 이미지의 배경 제거·중심/크기·좌우 대칭 정렬만 수행한다."""
from collections import deque
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Tools/Art/Sources/CombatSkills_v1'
DEST = ROOT / 'Assets/06.UI/BattleMutedPreview/CombatSkills_v1/Sprites'


def flood_background(blocked, seeds):
    h, w = blocked.shape
    seen = blocked.copy()
    todo = deque()
    for x, y in seeds:
        if not seen[y, x]:
            seen[y, x] = True
            todo.append((x, y))
    while todo:
        x, y = todo.popleft()
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= nx < w and 0 <= ny < h and not seen[ny, nx]:
                seen[ny, nx] = True
                todo.append((nx, ny))
    return seen & ~blocked


def clean_source(path, has_hole):
    image = Image.open(path).convert('RGBA')
    data = np.array(image)
    if data[:, :, 3].min() == 255:
        rgb = data[:, :, :3].astype(np.int16)
        # 회색 체크무늬와 실제 금속/유색 테두리를 구분한다.
        colored = (rgb.max(2) - rgb.min(2) > 26) & (rgb.max(2) > 28)
        barrier = np.array(Image.fromarray((colored * 255).astype('uint8')).filter(ImageFilter.MaxFilter(7))) > 0
        seeds = [(0, 0)]
        if has_hole:
            seeds.append((image.width // 2, image.height // 2))
        background = flood_background(barrier, seeds)
        data[background, 3] = 0
        # 금속 주위에 얇게 남은 무채색 체크 배경만 제거한다. 검은 외곽선은 보존한다.
        gray = (rgb.max(2) - rgb.min(2) < 22) & (rgb.min(2) > 70) & (rgb.max(2) < 239)
        near_background = np.array(Image.fromarray((background * 255).astype('uint8')).filter(ImageFilter.MaxFilter(7))) > 0
        data[gray & near_background, 3] = 0
    else:
        # 생성 과정에서 외곽에 생긴 저알파 잔여 픽셀을 제거한다.
        data[data[:, :, 3] < 80, 3] = 0
    return Image.fromarray(data)


def frame(image):
    cropped = image.crop(image.getchannel('A').getbbox())
    fitted = cropped.resize((240, 240), Image.Resampling.NEAREST)
    # 모든 분류에서 정확히 같은 중심과 마름모 외곽 크기를 사용한다.
    left = fitted.crop((0, 0, 120, 240))
    fitted.paste(left.transpose(Image.Transpose.FLIP_LEFT_RIGHT), (120, 0))
    out = Image.new('RGBA', (256, 256))
    out.paste(fitted, (8, 8))
    return out


def ring(image):
    cropped = image.crop(image.getchannel('A').getbbox())
    fitted = cropped.resize((240, 240), Image.Resampling.NEAREST)
    # 원형 링의 비대칭 오차를 제거한다. 원본 텍셀을 재배치할 뿐 새 픽셀 아트는 그리지 않는다.
    quarter = fitted.crop((0, 0, 120, 120))
    fitted.paste(quarter.transpose(Image.Transpose.FLIP_LEFT_RIGHT), (120, 0))
    top = fitted.crop((0, 0, 240, 120))
    fitted.paste(top.transpose(Image.Transpose.FLIP_TOP_BOTTOM), (0, 120))
    out = Image.new('RGBA', (256, 256))
    out.paste(fitted, (8, 8))
    return out


def panel(image):
    cropped = image.crop(image.getchannel('A').getbbox())
    cropped.thumbnail((1904, 240), Image.Resampling.NEAREST)
    # 좌우 날개와 경사, 금속 모서리의 정렬을 동일하게 만든다.
    if cropped.width % 2:
        cropped = cropped.crop((0, 0, cropped.width - 1, cropped.height))
    left = cropped.crop((0, 0, cropped.width // 2, cropped.height))
    cropped.paste(left.transpose(Image.Transpose.FLIP_LEFT_RIGHT), (cropped.width // 2, 0))
    out = Image.new('RGBA', (1920, 256))
    out.paste(cropped, ((1920 - cropped.width) // 2, (256 - cropped.height) // 2))
    return out


def main():
    DEST.mkdir(parents=True, exist_ok=True)
    records = []
    for name in ('Frame_Damage', 'Frame_Buff', 'Frame_Debuff', 'Cooldown_Ring', 'Bottom_Velvet'):
        image = clean_source(SOURCE / (name + '_source.png'), name != 'Bottom_Velvet')
        output = panel(image) if name == 'Bottom_Velvet' else ring(image) if name == 'Cooldown_Ring' else frame(image)
        data = np.array(output)
        # 투명 픽셀의 RGB에도 체크무늬를 남기지 않는다(프리뷰/필터링 시 번짐 방지).
        data[data[:, :, 3] == 0, :3] = 0
        output = Image.fromarray(data)
        output.save(DEST / (name + '.png'))
        records.append({'name': name, 'size': output.size, 'alpha_bbox': output.getchannel('A').getbbox(),
                        'transparent_pixels': int((data[:, :, 3] == 0).sum()),
                        'horizontal_alpha_symmetry': bool(np.array_equal(data[:, :, 3], data[:, ::-1, 3]))})
    (SOURCE / 'normalization.json').write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(records, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
