# Battle Noble Unified Panel

- 내장 image_gen 편집으로 생성. CLI/API fallback 미사용.
- 편집 대상: 기존 `BattleNobleBottomPanel/Panel_Velvet_Original.png`.
- 톤/배치 참조: `Tools/Art/Previews/UI_Battle_MutedPreview_ToneFix.png`.
- 생성 원본 `Panel_Unified_Original.png` 보존 (2172×724, 체크 배경 포함).
- 최종 소비 이미지: `Assets/06.UI/BattleMutedPreview/NobleBottomPanel/Panel_Unified.png`, 1920×432, 투명 RGBA.
- `Tools/Art/PrepareBattleUnifiedPanel.ps1`로 외부 체크 배경 제거 및 상단 경사와 하단선의 2D 크기·좌표 정렬만 수행. 원본 RGBA 최근접 샘플링, 재도색/새 그림 그리기 없음.
- 원본 기준점은 각 경사선의 금색 외곽 픽셀 직선 회귀 교점으로 정했다. 기존처럼 X만 구간별 조정한 뒤 Y 전체를 독립 압축하지 않는다.
- SourceTopPoints: `0.000,443.042;214.529,249.330;401.889,421.233;488.966,339.817;664.947,507.000;1672.732,507.000;1955.647,249.083;2171.000,442.900`.
- SourceBottom: `673`; TargetBottom: `408` (돌출 하단 장식 여백 확보).
- TargetTopPoints: `0,171;159,12;311,164;375,100;535,260;1509,260;1757,12;1919,174`.
- Cost/리롤/전투 시작 중심 X=159/375/1757. 목표 모든 사선 |ΔX|=|ΔY|, 중앙은 수평.
- 배치 시 `BottomNobleBackground` Image Color RGB=0.55/Alpha=1로 어두운 기존 톤 유지. 원본 아트와 이전 Body/Frame PNG는 보존.

## 최종 생성 프롬프트

```text
Use case: precise-object-edit. Asset type: one-piece Unity bottom HUD background sprite, NOT a full screen mockup.
Input1 is the current full velvet panel edit target. Input2 is the current actual screen, used ONLY for the restrained dark tone and fixed foreground button geometry. Output ONLY the combined velvet-and-border panel in one image; exclude every foreground button, icon, card, text, number and all upper screen content.
Keep the noble gothic pixel-art style: very dark burgundy wine velvet damask filling the ENTIRE interior without black subdivisions, thin muted antique-gold double trim, dark crimson hairline accent, small symmetrical gold corner diamonds and delicate restrained filigree. Match the DARK brightness of the panel in input2, NOT the brighter raw input1. Do not brighten it. No soil/stone/rough terrain.
CRITICAL GEOMETRY CORRECTION: all long sloping top border segments must be straight precise 45-degree lines (equal pixel x/y distances), matching square diamond button edges, never one side steeper or curved. One tall left peak, one smaller reroll peak next to it, long LOW flat central bridge, one tall right peak. The large peaks have the same height. Borders sit with consistent offset outside existing diamond controls.
Composition: request a 1920x640 transparent canvas. The panel silhouette lives in its LOWER 432 pixels, leaving transparent area above. Within that lower1920x432 region, exact upper border vertices (x,y downwards) are: (0,171), (159,12), (311,164), (375,100), (535,260), (1509,260), (1757,12), (1919,174). Bottom edge at y420. Follow these proportions closely: left big peak at8.28% width, left small peak19.53%, bridge starts27.86% and ends78.59%, right big peak91.51%. All diagonal steps are 45degrees in final pixel space. Small ornaments may project slightly, keep tips within canvas. No perspective, no tilt, no shadow outside. Single continuous solid filled panel even under where cards would be, not disconnected wings. Entire outside contour genuinely transparent alpha, not baked checkerboard/white. No labels or measurement marks, no symbols inside, no controls. Preserve crisp pixel-art texture and subtle dark palette.
```
