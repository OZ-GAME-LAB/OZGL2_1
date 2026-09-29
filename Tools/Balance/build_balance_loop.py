# -*- coding: utf-8 -*-
"""
밸런스 테스트 루프 생성기 (성민 파트).

밸런스 시트(마왕성디펜스_밸런스시트_v0.6.xlsx)의 05.보통 / 06.어려움 / 07.헬 라운드 표를 읽어서
  1) Assets/01.Scripts/Sandbox/Resources/Balance/RoundScaling.csv   — 라운드별 물량·용사 배율(인게임 패널이 읽음)
  2) Assets/03.ScriptableObjects/Stage/BalanceLoop/*.asset          — 3난이도 StageDataSO + 카탈로그 + InGame 설정 사본
  3) Assets/00.Scenes/JOB_SUNGMIN/JOB_SUNGMIN_Balance_{Lobby,StageChoice,InGame}.unity
를 만든다. 같은 이름으로 다시 돌리면 GUID(이름 기반 고정)를 유지한 채 값만 갈아끼운다.

사용법:  python Tools/Balance/build_balance_loop.py <밸런스시트.xlsx 경로>
※ 팀 공용 파일(Builds 씬, InGamePrototypeConfig, StageSpawnCounts.csv 등)은 읽기만 하고 절대 수정하지 않는다.
   유니티 에디터가 열려 있으면 저장 후 다시 임포트(Reimport)만 해주면 된다.
"""
import io
import os
import re
import sys
import uuid

import openpyxl

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sim_noskill as sim  # wave_plan / spawn_entries / interleave 를 튜너와 공유

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

SCENE_DIR = "Assets/00.Scenes/JOB_SUNGMIN"
DATA_DIR = "Assets/03.ScriptableObjects/Stage/BalanceLoop"
CSV_DIR = "Assets/01.Scripts/Sandbox/Resources/Balance"
SCRIPT_DIR = "Assets/01.Scripts/Sandbox/BalanceLoop"

LOBBY_SCENE = "JOB_SUNGMIN_Balance_Lobby"
CHOICE_SCENE = "JOB_SUNGMIN_Balance_StageChoice"
INGAME_SCENE = "JOB_SUNGMIN_Balance_InGame"

# 기존(읽기 전용) 참조
TEAM_CONFIG = "Assets/_Project/Data/InGame/InGamePrototypeConfig.asset"
TEAM_INGAME_SCENE = "Assets/00.Scenes/Builds/InGame.unity"
TEAM_TEMPLATE_SCENE = "Assets/00.Scenes/JOB_SUNGMIN/JOB_SUNGMIN_Lobby.unity"
BOOTSTRAP_TARGET_FILEID = "8204448387257716529"      # InGamePrototype.prefab 안 InGamePrototypeBootstrap 컴포넌트
INGAME_PREFAB_GUID = "6681132ce2a6971459706f9cb14eaa58"

STAGEDATA_SCRIPT_GUID = "d11d821f0fe278a4ea6c4c80b7b8457f"   # StageDataSO
CATALOG_SCRIPT_GUID = "c0df217aa83401645b25cd37f0dc9608"     # StageCatalogSO
SCENE_NAVIGATOR_GUID = "9526383636ca4504eabc9327526ede8f"    # UISceneNavigator
STAGE_CONTROLLER_GUID = "4e77d25ac41f463438313adbbb90265c"   # StageSelectionController

NS = uuid.UUID("6f1c2b9e-8d0a-4c53-9a6e-2c1d0b7a5e11")


def gid(name):
    return uuid.uuid5(NS, "ozgl2-balance-loop/" + name).hex


JOB_TO_HERO = {
    "전사": "H_WAR_01", "방패병": "H_SHD_01", "도적": "H_ROG_01",
    "궁수": "H_ARC_01", "마법사": "H_MAG_01", "힐러": "H_HEL_01",
}
HERO_ORDER = ["H_WAR_01", "H_SHD_01", "H_ROG_01", "H_ARC_01", "H_MAG_01", "H_HEL_01"]

# (stage_id, 시트 접두, 라운드 수, 보스 라운드 목록, 증강 티어(보스 라운드 순서), 에셋 이름)
S, G, P = "S", "G", "P"
STAGES = [
    ("bal_normal_30", "05.", 30, [10, 20, 30], [S, G, P], "StageBalNormal30"),
    ("bal_hard_50", "06.", 50, [10, 20, 30, 40, 50], [S, S, G, G, P], "StageBalHard50"),
    ("bal_hell_100", "07.", 100, list(range(10, 101, 10)), [S, S, S, G, G, G, P, P, P, P], "StageBalHell100"),
]


def write(path, text):
    full = os.path.join(ROOT, path)
    os.makedirs(os.path.dirname(full), exist_ok=True)
    with io.open(full, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def read(path):
    with io.open(os.path.join(ROOT, path), "r", encoding="utf-8") as f:
        return f.read()


def meta_native(guid):
    return ("fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n" % guid)


def meta_default(guid):
    return ("fileFormatVersion: 2\nguid: %s\nDefaultImporter:\n  externalObjects: {}\n  userData: \n"
            "  assetBundleName: \n  assetBundleVariant: \n" % guid)


def meta_text(guid):
    return ("fileFormatVersion: 2\nguid: %s\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n"
            "  assetBundleName: \n  assetBundleVariant: \n" % guid)


def meta_script(guid):  # 이 프로젝트 스크립트 .meta는 guid만 적는 최소 형식
    return "fileFormatVersion: 2\nguid: %s\n" % guid


# ───────────────────────────── 시트 읽기

def load_rows(wb, prefix, rounds):
    ws = next(wb[n] for n in wb.sheetnames if n.startswith(prefix))
    rows = []
    for r in range(4, 4 + rounds):
        R = int(ws.cell(row=r, column=1).value)
        rows.append({
            "round": R,
            "hp": float(ws.cell(row=r, column=3).value),
            "atk": float(ws.cell(row=r, column=4).value),
            "count": int(ws.cell(row=r, column=5).value),
            "spd": float(ws.cell(row=r, column=6).value),
            "heal": float(ws.cell(row=r, column=8).value),
            "comp": str(ws.cell(row=r, column=10).value),
        })
        assert R == r - 3, "라운드 번호가 어긋남: %s" % prefix
    return rows


def parse_comp(text):
    weights = {}
    for part in text.split("/"):
        part = part.strip()
        m = re.match(r"(\S+)\s+(\d+(?:\.\d+)?)$", part)
        if m and m.group(1) in JOB_TO_HERO:
            weights[JOB_TO_HERO[m.group(1)]] = float(m.group(2))
    if not weights:
        raise ValueError("직업 조합을 못 읽음: " + text)
    return weights


def split_counts(total, weights):
    """가중치 비율로 total을 나눈다(최대잉여법). 합이 정확히 total."""
    s = sum(weights.values())
    exact = {h: total * w / s for h, w in weights.items()}
    base = {h: int(v) for h, v in exact.items()}
    left = total - sum(base.values())
    for h in sorted(weights, key=lambda k: (exact[k] - base[k]), reverse=True)[:left]:
        base[h] += 1
    return base


# ───────────────────────────── 에셋 생성

def boss_id(index, total):
    """보스 라운드 순서(index)에 따라 H_BOSS_01(앞쪽 절반) → H_BOSS_02(뒤쪽 절반) → 마지막은 H_BOSS_FINAL_01."""
    if index == total - 1:
        return "H_BOSS_FINAL_01"
    return "H_BOSS_01" if index < (total - 1) / 2.0 else "H_BOSS_02"


def stage_yaml(stage_id, name, rows, boss_rounds, tiers):
    out = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
           "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
           "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
           "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % STAGEDATA_SCRIPT_GUID,
           "  m_Name: %s" % name,
           "  m_EditorClassIdentifier: Assembly-CSharp::OZGL2.Stage.StageDataSO",
           "  _stageId: %s" % stage_id, "  _rounds:"]
    for row in rows:
        R, total = row["round"], row["count"]
        is_boss = R in boss_rounds
        escorts = total - 1 if is_boss else total
        shares = split_counts(max(escorts, 0), parse_comp(row["comp"])) if escorts > 0 else {}
        out.append("  - _roundId: %s_round_%02d" % (stage_id, R))
        out.append("    _spawns:")
        # 스폰 코드는 엔트리를 앞에서부터 한 마리씩 소환하고, 다음 마리 전에 "직전 마리 엔트리의 간격"만큼 기다린다.
        # 그래서 간격 0으로 이어 붙이면 한꺼번에 나오고(웨이브), 웨이브 마지막 마리의 간격이 웨이브 사이 시간이 된다.
        # 직업은 섞어서(interleave), 웨이브 크기·간격은 sim_noskill.wave_plan(라운드)을 따른다.
        ordered = [(hero, shares[hero]) for hero in HERO_ORDER if shares.get(hero, 0) > 0]
        seq = sim.expand(sim.interleave(ordered))
        if is_boss:
            seq.append(boss_id(boss_rounds.index(R), len(boss_rounds)))
        for hero, count, interval in sim.spawn_entries(R, seq):
            out += ["    - _heroId: %s" % hero, "      _count: %d" % count, "      _intervalSeconds: %.3f" % interval]
        tier = tiers[boss_rounds.index(R)] if is_boss else S
        out += ["    _isBossRound: %d" % (1 if is_boss else 0),
                "    _rewardId: reward_dummy_general",
                "    _silverWeight: %d" % (100 if tier == S else 0),
                "    _goldWeight: %d" % (100 if tier == G else 0),
                "    _platinumWeight: %d" % (100 if tier == P else 0)]
    return "\n".join(out) + "\n"


def catalog_yaml(asset_names):
    out = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
           "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
           "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
           "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % CATALOG_SCRIPT_GUID,
           "  m_Name: StageCatalogBalance",
           "  m_EditorClassIdentifier: Assembly-CSharp::OZGL2.InGame.StageCatalogSO", "  _stages:"]
    for n in asset_names:
        out.append("  - {fileID: 11400000, guid: %s, type: 2}" % gid("asset/" + n))
    return "\n".join(out) + "\n"


def config_yaml(first_stage_asset):
    text = read(TEAM_CONFIG)
    text = re.sub(r"m_Name: .*", "m_Name: InGameConfigBalance", text, count=1)
    text = re.sub(r"_stage: \{fileID: 11400000, guid: [0-9a-f]+, type: 2\}",
                  "_stage: {fileID: 11400000, guid: %s, type: 2}" % gid("asset/" + first_stage_asset), text)
    text = re.sub(r"_stageCatalog: \{fileID: 11400000, guid: [0-9a-f]+, type: 2\}",
                  "_stageCatalog: {fileID: 11400000, guid: %s, type: 2}" % gid("asset/StageCatalogBalance"), text)
    text = re.sub(r"_lobbyScenePath: .*", "_lobbyScenePath: %s/%s.unity" % (SCENE_DIR, LOBBY_SCENE), text)
    text = re.sub(r"_stageSelectionScenePath: .*", "_stageSelectionScenePath: %s/%s.unity" % (SCENE_DIR, CHOICE_SCENE), text)
    return text


# ───────────────────────────── 씬 생성

def mb_block(fid, go, script_guid, ident, fields=""):
    return ("--- !u!114 &%d\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: %d}\n  m_Enabled: 1\n"
            "  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: %s, type: 3}\n  m_Name: \n"
            "  m_EditorClassIdentifier: %s\n%s" % (fid, go, script_guid, ident, fields))


def go_block(fid, name, comps):
    lines = "".join("  - component: {fileID: %d}\n" % c for c in comps)
    return ("--- !u!1 &%d\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  serializedVersion: 6\n  m_Component:\n%s"
            "  m_Layer: 0\n  m_Name: %s\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n"
            "  m_StaticEditorFlags: 0\n  m_IsActive: 1\n" % (fid, lines, name))


def transform_block(fid, go):
    return ("--- !u!4 &%d\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: %d}\n"
            "  serializedVersion: 2\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
            "  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n"
            "  m_Father: {fileID: 0}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n" % (fid, go))


def scene_roots(fids):
    return ("--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n"
            + "".join("  - {fileID: %d}\n" % f for f in fids))


def build_lobby_scene():
    template = read(TEAM_TEMPLATE_SCENE)                          # 내 기존 로비 씬(헤더+카메라 재사용)
    prefix = template.split("--- !u!1 &1762229656")[0]
    go, tr, mb = 7100000001, 7100000002, 7100000003
    fields = "  _stageChoiceScene: %s\n" % CHOICE_SCENE
    body = (go_block(go, "LobbyScreen", [tr, mb])
            + mb_block(mb, go, "6284b33b7a24f064893365da83c3de12", "Assembly-CSharp::OZGL2.Sandbox.SandboxLobbyScreen", fields)
            + transform_block(tr, go)
            + scene_roots([424813261, tr]))
    return prefix + body


def build_choice_scene():
    template = read(TEAM_TEMPLATE_SCENE)
    prefix = template.split("--- !u!1 &1762229656")[0]
    go, tr, nav, ctrl, screen = 7200000001, 7200000002, 7200000003, 7200000004, 7200000005
    ctrl_fields = ("  _inGameConfig: {fileID: 11400000, guid: %s, type: 2}\n  _navigator: {fileID: %d}\n"
                   "  _inGameScenePath: %s/%s.unity\n  _lobbyScenePath: %s/%s.unity\n"
                   % (gid("asset/InGameConfigBalance"), nav, SCENE_DIR, INGAME_SCENE, SCENE_DIR, LOBBY_SCENE))
    screen_fields = "  _controller: {fileID: %d}\n" % ctrl
    body = (go_block(go, "BalanceStageChoice", [tr, nav, ctrl, screen])
            + mb_block(nav, go, SCENE_NAVIGATOR_GUID, "Assembly-CSharp::OZGL2.UIFlow.UISceneNavigator")
            + mb_block(ctrl, go, STAGE_CONTROLLER_GUID, "Assembly-CSharp::OZGL2.InGame.StageSelectionController", ctrl_fields)
            + mb_block(screen, go, gid("script/BalanceStageChoiceScreen"),
                       "Assembly-CSharp::OZGL2.BalanceTest.BalanceStageChoiceScreen", screen_fields)
            + transform_block(tr, go)
            + scene_roots([424813261, tr]))
    return prefix + body


def build_ingame_scene():
    text = read(TEAM_INGAME_SCENE)
    override = ("    - target: {fileID: %s, guid: %s, type: 3}\n      propertyPath: _config\n      value: \n"
                "      objectReference: {fileID: 11400000, guid: %s, type: 2}\n"
                % (BOOTSTRAP_TARGET_FILEID, INGAME_PREFAB_GUID, gid("asset/InGameConfigBalance")))
    marker = "    m_RemovedComponents: []"
    assert text.count(marker) == 1, "프리팹 오버라이드 위치를 못 찾음(팀 InGame 씬 구조가 바뀜)"
    text = text.replace(marker, override + marker)

    go, tr, mb = 7300000001, 7300000002, 7300000003
    panel = (go_block(go, "BalanceTestPanel", [tr, mb])
             + mb_block(mb, go, gid("script/BalanceTestPanel"), "Assembly-CSharp::OZGL2.BalanceTest.BalanceTestPanel")
             + transform_block(tr, go))
    roots_marker = "--- !u!1660057539 &9223372036854775807"
    assert text.count(roots_marker) == 1
    head, tail = text.split(roots_marker)
    tail = roots_marker + tail
    tail = tail.rstrip("\n") + "\n  - {fileID: %d}\n" % tr
    return head + panel + tail


def update_build_settings(scene_names):
    path = "ProjectSettings/EditorBuildSettings.asset"
    text = read(path)
    for name in scene_names:
        scene_path = "%s/%s.unity" % (SCENE_DIR, name)
        if scene_path in text:
            continue
        entry = "  - enabled: 1\n    path: %s\n    guid: %s\n" % (scene_path, gid("scene/" + name))
        text = text.replace("  m_configObjects:", entry + "  m_configObjects:", 1)
    write(path, text)


def main():
    if len(sys.argv) < 2:
        sys.exit("사용법: python Tools/Balance/build_balance_loop.py <밸런스시트.xlsx 경로>")
    sheet = sys.argv[1]
    wb = openpyxl.load_workbook(sheet, data_only=False)

    csv_lines = ["stage_id,round,count,hp,atk,spd,heal"]
    asset_names = []
    for stage_id, prefix, rounds, boss_rounds, tiers, asset in STAGES:
        rows = load_rows(wb, prefix, rounds)
        for r in rows:
            csv_lines.append("%s,%d,%d,%.3f,%.3f,%.3f,%.3f" % (stage_id, r["round"], r["count"], r["hp"], r["atk"], r["spd"], r["heal"]))
        write("%s/%s.asset" % (DATA_DIR, asset), stage_yaml(stage_id, asset, rows, boss_rounds, tiers))
        write("%s/%s.asset.meta" % (DATA_DIR, asset), meta_native(gid("asset/" + asset)))
        asset_names.append(asset)
        print("  stage %-14s %3d rounds  R1 %d -> R%d %d" % (stage_id, rounds, rows[0]["count"], rounds, rows[-1]["count"]))

    write("%s/RoundScaling.csv" % CSV_DIR, "\n".join(csv_lines) + "\n")
    write("%s/RoundScaling.csv.meta" % CSV_DIR, meta_text(gid("csv/RoundScaling")))

    write("%s/StageCatalogBalance.asset" % DATA_DIR, catalog_yaml(asset_names))
    write("%s/StageCatalogBalance.asset.meta" % DATA_DIR, meta_native(gid("asset/StageCatalogBalance")))
    write("%s/InGameConfigBalance.asset" % DATA_DIR, config_yaml(asset_names[0]))
    write("%s/InGameConfigBalance.asset.meta" % DATA_DIR, meta_native(gid("asset/InGameConfigBalance")))

    for script in ("BalanceRoundScaling", "BalanceTestPanel", "BalanceStageChoiceScreen"):
        write("%s/%s.cs.meta" % (SCRIPT_DIR, script), meta_script(gid("script/" + script)))

    scenes = {LOBBY_SCENE: build_lobby_scene(), CHOICE_SCENE: build_choice_scene(), INGAME_SCENE: build_ingame_scene()}
    for name, text in scenes.items():
        write("%s/%s.unity" % (SCENE_DIR, name), text)
        write("%s/%s.unity.meta" % (SCENE_DIR, name), meta_default(gid("scene/" + name)))
    update_build_settings(list(scenes))
    print("완료 - 씬 3개, 스테이지 3개, 카탈로그·설정 사본, RoundScaling.csv, 빌드 세팅 등록")


if __name__ == "__main__":
    main()
