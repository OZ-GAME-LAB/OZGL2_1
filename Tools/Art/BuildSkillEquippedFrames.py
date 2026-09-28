"""생성 원본 보존. 승인된 외곽 배경 정리 및 256px 중앙 정렬만 수행한다."""
from pathlib import Path
import shutil
import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Tools/Art/Sources/SkillEquippedFrames_v4'
OUTPUT = ROOT / 'Assets/06.UI/LobbyMutedPreview/Skills_v1/Sprites'
GENERATED = Path('C:/Users/sudea/.codex/generated_images/01a0a4a8-3799-7db1-907e-69bb9af378b8')
INPUTS = {
    'Damage': 'exec-849d934f-90df-4e95-aeb4-b855a4780954.png',
    'Buff': 'exec-054ab1a0-d17b-4225-a562-c1187c564288.png',
    'Debuff': 'exec-dd7b804a-ca58-43dc-aa57-61b4a45322cd.png',
}

SOURCE.mkdir(parents=True, exist_ok=True)
for name, filename in INPUTS.items():
    original = SOURCE / (name + '_Source.png')
    if not original.exists():
        shutil.copy2(GENERATED / filename, original)
    source = Image.open(original).convert('RGBA')
    pixels = np.array(source)
    rgb = pixels[:, :, :3]
    # 색이 있는 금속의 좌우 외곽을 행별로 따라간다. 내부 검정은 지우지 않는다.
    colored = (rgb.max(axis=2) > 40) & (rgb.max(axis=2) - rgb.min(axis=2) > 12) & (pixels[:, :, 3] > 32)
    colored = np.array(Image.fromarray(colored.astype('uint8') * 255).filter(ImageFilter.MedianFilter(5))) > 0
    silhouette = np.zeros(colored.shape, dtype=bool)
    for y in range(source.height):
        xs = np.flatnonzero(colored[y])
        if len(xs):
            silhouette[y, max(0, xs[0]-3):min(source.width, xs[-1]+4)] = True
    # 생성된 알파가 있으면 그대로 존중하고, 검은 외부 배경만 제거한다.
    pixels[:, :, 3] = np.minimum(pixels[:, :, 3], silhouette.astype('uint8') * 255)
    cleaned = Image.fromarray(pixels)
    box = cleaned.getchannel('A').getbbox()
    if box is None:
        raise ValueError('빈 알파 마스크: ' + name)
    cropped = cleaned.crop(box)
    cropped.thumbnail((248, 242), Image.Resampling.NEAREST)
    result = Image.new('RGBA', (256, 256))
    result.alpha_composite(cropped, ((256-cropped.width)//2, (256-cropped.height)//2))
    destination = OUTPUT / ('Equip_' + name + '_Muted.png')
    result.save(destination)
    print(name, 'source', source.size, 'trim', box, 'output', result.size, 'alpha', result.getchannel('A').getextrema())
