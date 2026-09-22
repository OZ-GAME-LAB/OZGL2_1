"""승인된 생성 UI 이미지의 실제 알파를 보존하며 crop/크기 정렬만 수행한다."""
from pathlib import Path
import argparse
import shutil
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
FILES = {
    'CodexCard': ('exec-93fe84f1-fbf3-4924-83dc-00ad51c284ac.png', (288, 376)),
    'AchievementCard': ('exec-16764a97-f6e1-4b74-97ec-b1713e1a40b3.png', (544, 240)),
    'FactionDemon': ('exec-bc9d4a48-9400-40fb-846b-ffe7787db67e.png', (320, 320)),
    'FactionHero': ('exec-a031942e-f4e4-4180-ac8e-8e05589914c7.png', (320, 320)),
    'Lock': ('exec-25d53239-8c54-4ca4-9b88-85db62671cfe.png', (64, 80)),
}

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path)
    args = parser.parse_args()
    originals = ROOT / 'Tools/Art/Sources/LobbyCollections_v1'
    output = ROOT / 'Assets/06.UI/LobbyMutedPreview/Collections_v1/Sprites'
    originals.mkdir(parents=True, exist_ok=True)
    output.mkdir(parents=True, exist_ok=True)
    for name, (source_name, size) in FILES.items():
        source = args.source / source_name
        destination = output / (name + '.png')
        if destination.exists():
            print('Existing asset retained:', destination)
            continue
        original = originals / (name + '_Original.png')
        if not original.exists():
            shutil.copy2(source, original)
        image = Image.open(source).convert('RGBA')
        assert image.getchannel('A').getextrema()[0] == 0, 'True transparency required'
        # 반투명 테두리를 잘라내지 않고 전체 알파 경계를 사용한다.
        image = image.crop(image.getchannel('A').getbbox())
        image.thumbnail((size[0] - 8, size[1] - 8), Image.Resampling.NEAREST)
        canvas = Image.new('RGBA', size)
        canvas.alpha_composite(image, ((size[0] - image.width) // 2, (size[1] - image.height) // 2))
        canvas.save(destination)
        print(name, size, 'source alpha preserved')

if __name__ == '__main__':
    main()
