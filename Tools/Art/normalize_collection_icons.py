"""신규 도감 초상/아이콘의 알파 여백 정렬. 기존 외부 아트는 수정하지 않는다."""
from pathlib import Path
import shutil
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
COLLECTION = ROOT / 'Assets/06.UI/LobbyMutedPreview/Collections_v1'
BACKUP = ROOT / 'Tools/Art/Sources/LobbyCollections_v1'
BACKUP.mkdir(parents=True, exist_ok=True)

def normalized(source, output, size, padding):
    image = Image.open(source).convert('RGBA')
    # 극소 알파 잡티로 생기는 과도한 여백만 제거한다.
    bbox = image.getchannel('A').point(lambda x: 255 if x > 16 else 0).getbbox()
    image = image.crop(bbox)
    scale = min((size[0] - 2 * padding) / image.width, (size[1] - 2 * padding) / image.height)
    image = image.resize((round(image.width * scale), round(image.height * scale)), Image.Resampling.NEAREST)
    canvas = Image.new('RGBA', size)
    canvas.alpha_composite(image, ((size[0] - image.width) // 2, (size[1] - image.height) // 2))
    canvas.save(output)

for portrait in (COLLECTION / 'Portraits').glob('*.png'):
    original = BACKUP / ('Portrait_' + portrait.name)
    if not original.exists():
        shutil.copy2(portrait, original)
    normalized(original, portrait, (512, 512), 24)

normalized(BACKUP / 'Lock_Original.png', COLLECTION / 'Sprites/Lock.png', (64, 80), 4)

icons = [
    'Legion/Icon_Legion_Sharpness', 'Wisdom/Icon_Wisdom_Expansion',
    'Legion/Icon_Legion_Toughness', 'Curse/Icon_Curse_Weakness',
    'Curse/Icon_Curse_Shatter', 'Legion/Icon_Legion_Execution',
    'Wisdom/Icon_Wisdom_FastGrowth', 'Spells/Icon_Spells_DemonPower',
    'Spells/Icon_Spells_LegionShout',
]
for index, name in enumerate(icons):
    source = ROOT / ('Assets/06.UI/LobbyMutedPreview/Overlays/TreeArt_v1/Sprites/Icons/' + name + '.png')
    normalized(source, COLLECTION / ('Sprites/AchievementIcon_%02d.png' % index), (128, 128), 12)
print('Normalized: 12 portraits, lock, 9 derived achievement icons. Originals retained.')
