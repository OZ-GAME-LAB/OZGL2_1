# -*- coding: utf-8 -*-
"""
스킬 없이(마왕 스킬·증강·시너지 제외) 라운드를 깰 수 있는지 보는 간이 전투 시뮬레이터 — 밸런스 시트 물량 산정용.
게임 안 실제 지오메트리(그리드 크기·이동 거리)를 그대로 재현하는 게 아니라, SO 스탯(HP·공격력·공속·방어%·사거리·
스플래시)으로 "누가 누구를 몇 초에 잡는가"를 계산해서 물량이 대략 어느 선이면 스킬 없이 깨질지 가늠하는 용도다.
실제 판정은 밸런스 테스트 루프(JOB_SUNGMIN_Balance_*)에서 플레이로 확인한다.

가정(시트 03.전투공식 하단에도 적어둠):
  · 마왕군: 시작 전사(M_WAR_01) 1기 + 라운드 클리어마다 보상으로 1기 추가
  · 보상은 3택1 — "무난한 플레이어": 같은 유닛이 이미 있으면(합성 가능) 그걸 고르고, 아니면 (HP×공격×공속)이 큰 유닛을 고른다
  · 합성: 1성 2기 → 2성(HP·공격·힐 ×1.7), 2성 2기 → 3성(×3.2) — UnitStatData.GetStarMultiplier와 같은 값(2026-09-29 하향)
    2성 이상 궁수·도적은 세진의 직업 기믹(스턴·처형)으로 공격을 약 15% 더 세게 친다고 근사
  · 가장 튼튼한 유닛을 앞줄에 세운다(용사는 앞줄부터 공격). 시너지·증강·스킬·특성은 전부 없음
  · 마왕군은 라운드마다 풀피로 리셋, 원거리 마왕군은 접근하는 용사를 사거리만큼 먼저 쏜다
  · 용사는 웨이브 단위로 스폰: 웨이브 안의 용사는 한꺼번에(스폰 간격 0), 웨이브 사이는 wave_gap 초 — wave_plan / make_schedule
    (스폰 코드는 '한 마리 소환 → 직전 마리 엔트리의 간격만큼 대기 → 다음 마리'라서 간격 0으로 이어 붙이면 동시 스폰이 된다)
"""
import random
import statistics

# hp, atk, spd(공속), df(방어%), rng(사거리), splash(한 번에 맞는 총 대상 수·주 표적 포함), pct(주 표적 외 피해 비율), heal, mv(이동속도)
HERO = {
    "전": dict(hp=60, atk=12, spd=1.0, df=0.10, rng=1.0, splash=1, pct=0.0, heal=0, mv=1.0),
    "방": dict(hp=120, atk=10, spd=0.8, df=0.30, rng=1.0, splash=1, pct=0.0, heal=0, mv=0.7),
    "도": dict(hp=40, atk=11, spd=1.5, df=0.05, rng=1.0, splash=1, pct=0.0, heal=0, mv=1.4),
    "궁": dict(hp=45, atk=14, spd=1.2, df=0.05, rng=3.0, splash=1, pct=0.0, heal=0, mv=0.9),
    "마": dict(hp=50, atk=20, spd=0.35, df=0.03, rng=3.0, splash=3, pct=0.35, heal=0, mv=0.8),
    "힐": dict(hp=55, atk=0, spd=0.7, df=0.05, rng=2.5, splash=1, pct=0.0, heal=6, mv=0.9),
    "보스": dict(hp=900, atk=36, spd=0.8, df=0.15, rng=1.0, splash=1, pct=0.0, heal=0, mv=0.7),       # H_BOSS_01
    "보2": dict(hp=1400, atk=44, spd=0.85, df=0.18, rng=1.0, splash=1, pct=0.0, heal=0, mv=0.65),     # H_BOSS_02
    "최종": dict(hp=2400, atk=60, spd=0.9, df=0.20, rng=1.0, splash=4, pct=0.5, heal=0, mv=0.6),      # H_BOSS_FINAL_01
}
DEFENDER = {
    "전": dict(hp=140, atk=18, spd=1.0, df=0.10, rng=1.0, splash=4, pct=1.0, heal=0),   # 스플래시 반경 1.5, 주 표적 + 최대 3명 100%
    "방": dict(hp=320, atk=11, spd=0.8, df=0.35, rng=1.0, splash=1, pct=0.0, heal=0),
    "궁": dict(hp=70, atk=12, spd=1.3, df=0.05, rng=3.5, splash=1, pct=0.0, heal=0),
    "마": dict(hp=60, atk=22, spd=0.35, df=0.03, rng=3.0, splash=4, pct=0.6, heal=0),
    "도": dict(hp=90, atk=19, spd=1.6, df=0.05, rng=1.0, splash=1, pct=0.0, heal=0),
    "힐": dict(hp=80, atk=0, spd=0.7, df=0.05, rng=2.5, splash=1, pct=0.0, heal=8),
}
DEF_TYPES = list(DEFENDER)
APPROACH = 4.5      # 용사가 마왕군 앞줄에 닿기까지 이동 거리(칸)
ATTACK_DELAY = 0.3  # 공격 모션 지연(UnitBase.DelayedAttack) — 대략값
DT = 0.1
TIME_LIMIT = 120.0


# ─────────────────────────────── 스폰(웨이브) — 튜너와 스테이지 생성기가 공유

def wave_plan(round_no, total):
    """(웨이브 크기, 웨이브 사이 간격 초). R1~2는 예전처럼 한 마리씩 0.5초(사용자가 확인한 라운드).
    R3부터 웨이브 크기가 커지되(R10 6마리 · R20 13마리 · R30 20마리), 웨이브 사이 간격은 "크기 ÷ 유입 속도"로 잡아서
    작은 웨이브는 짧게·큰 웨이브는 길게 — 초당 나오는 용사 수가 R2(2마리/초)에서 R30(약 4.8마리/초)까지 꾸준히 늘어
    라운드가 바뀌자마자 텀이 갑자기 길어지는 일이 없다. 간격은 0.6~3.0초로 제한."""
    if round_no <= 2:
        return 1, 0.5
    size = min(total, max(2, int(round(2 + (round_no - 3) * 0.7))))
    rate = 2.0 + 0.1 * (round_no - 2)             # 초당 유입 용사 수
    gap = min(3.0, max(0.6, size / rate))
    return size, gap


def make_schedule(round_no, seq):
    """seq: 스폰 순서대로 나열한 키 목록 → [(키, 스폰 시각)]. 웨이브 안은 같은 시각, 웨이브 사이는 gap."""
    size, gap = wave_plan(round_no, len(seq))
    return [(k, (i // size) * gap) for i, k in enumerate(seq)]


def spawn_entries(round_no, seq):
    """seq → 스폰 코드가 그대로 읽을 (키, 마릿수, 간격초) 엔트리 목록.
    한 웨이브의 마지막 한 마리만 wave_gap 간격을 갖고, 나머지는 간격 0이라 같은 프레임에 연달아 소환된다."""
    size, gap = wave_plan(round_no, len(seq))
    entries = []
    for start in range(0, len(seq), size):
        wave = seq[start:start + size]
        last_wave = start + size >= len(seq)
        runs = []
        for k in wave:
            if runs and runs[-1][0] == k:
                runs[-1][1] += 1
            else:
                runs.append([k, 1])
        last_key = runs[-1][0]
        runs[-1][1] -= 1
        entries += [(k, c, 0.0) for k, c in runs if c > 0]
        entries.append((last_key, 1, 0.5 if last_wave else gap))
    return entries


def interleave(counts, passes=4):
    """[(키, 마릿수)] → 직업을 여러 번에 나눠 번갈아 내보내는 순서(전사 전부 → 방패병 전부처럼 몰아서 내보내지 않게)."""
    remaining = {k: n for k, n in counts}
    chunk = {k: max(1, -(-n // passes)) for k, n in counts}   # ceil(n / passes)
    out = []
    while any(v > 0 for v in remaining.values()):
        for k, _ in counts:
            take = min(chunk[k], remaining[k])
            if take > 0:
                out.append((k, take))
                remaining[k] -= take
    return out


def expand(counts):
    return [k for k, n in counts for _ in range(n)]


# ─────────────────────────────── 마왕군 구성

STAR_MULT = {1: 1.0, 2: 1.7, 3: 3.2}
PICK_VALUE = {k: v["hp"] * v["atk"] * v["spd"] for k, v in DEFENDER.items()}   # 힐러는 atk=0이라 자연히 최하


def make_roster(round_no, rng, smart=True):
    """R1=시작 전사 1기(1성), 이후 라운드마다 3택1 보상 1기. 반환: [(직업, 성급)] — 앞줄(튼튼한 순) 정렬."""
    units = [["전", 1]]
    for _ in range(round_no - 1):
        options = [rng.choice(DEF_TYPES) for _ in range(3)]
        if smart:
            fusible = [o for o in options if any(u[0] == o for u in units)]
            pick = fusible[0] if fusible else max(options, key=lambda o: PICK_VALUE[o])
        else:
            pick = options[0]
        units.append([pick, 1])
        merged = True
        while merged and smart:                       # 같은 직업·같은 성급 2기 → 다음 성급 1기
            merged = False
            for star in (1, 2):
                same = [u for u in units if u[1] == star]
                for job in DEF_TYPES:
                    pair = [u for u in same if u[0] == job]
                    if len(pair) >= 2:
                        units.remove(pair[0]); units.remove(pair[1])
                        units.append([job, star + 1])
                        merged = True
                        break
                if merged: break
    units.sort(key=lambda u: DEFENDER[u[0]]["hp"] * STAR_MULT[u[1]], reverse=True)
    return [(j, s) for j, s in units]


# ─────────────────────────────── 전투

def simulate(round_no, schedule, hp_mult, atk_mult, rng, roster=None, spd_mult=1.0):
    """schedule: [(용사 키, 스폰 시각)]. 반환: (승리여부, 남은 마왕군 HP 비율, 소요시간)."""
    roster = roster or make_roster(round_no, rng)
    defs = []
    for job, star in roster:
        s = DEFENDER[job]
        m = STAR_MULT[star]
        atk_bonus = 1.15 if star >= 2 and job in ("도", "궁") else 1.0     # 2성 이상 궁수·도적 직업 기믹 근사
        defs.append(dict(t=job, hp=s["hp"] * m, maxhp=s["hp"] * m, cd=rng.random() * 0.5, atk=s["atk"] * m * atk_bonus, heal=s["heal"] * m,
                         **{k: s[k] for k in ("spd", "df", "rng", "splash", "pct")}))
    heroes = []
    for job, spawn_t in schedule:
        s = HERO[job]
        arrive = spawn_t + APPROACH / s["mv"] - (s["rng"] - 1.0) / s["mv"]   # 사거리 긴 용사는 더 일찍 교전
        heroes.append(dict(t=job, hp=s["hp"] * hp_mult, maxhp=s["hp"] * hp_mult, atk=s["atk"] * atk_mult,
                           spd=s["spd"] * spd_mult, df=s["df"], splash=s["splash"], pct=s["pct"], heal=s["heal"],
                           arrive=arrive, mv=s["mv"], cd=rng.random() * 0.5, pending=[]))
    total_def_hp = sum(d["maxhp"] for d in defs)
    now = 0.0
    while now < TIME_LIMIT:
        engaged = [h for h in heroes if h["hp"] > 0 and h["arrive"] <= now]
        alive_h = [h for h in heroes if h["hp"] > 0]
        # 사거리 긴 마왕군은 용사가 접근하는 동안 먼저 쏜다 — 사거리별로 "사격 가능한 용사" 목록을 한 번씩만 만든다
        reach = {}
        for rg in {d["rng"] for d in defs}:
            lead = max(0.0, rg - 1.0)
            reach[rg] = sorted((h for h in alive_h if h["arrive"] - lead / h["mv"] <= now), key=lambda h: h["arrive"] - lead / h["mv"])
        alive_d = [d for d in defs if d["hp"] > 0]
        if not alive_d:
            return False, 0.0, now
        if not alive_h:
            return True, sum(max(d["hp"], 0) for d in defs) / total_def_hp, now
        # 마왕군 공격 — 가장 먼저 닿는 용사를 주 표적, 스플래시는 그 뒤의 용사들에게
        for d in alive_d:
            d["cd"] -= DT
            if d["heal"] > 0 and d["cd"] <= 0:
                d["cd"] += 1.0 / d["spd"]
                hurt = min((x for x in alive_d if x["hp"] < x["maxhp"]), key=lambda x: x["hp"] / x["maxhp"], default=None)
                if hurt: hurt["hp"] = min(hurt["maxhp"], hurt["hp"] + d["heal"])
                continue
            in_reach = reach[d["rng"]]
            if d["atk"] <= 0 or d["cd"] > 0 or not in_reach:
                continue
            d["cd"] += 1.0 / d["spd"]
            target = in_reach[0]
            hits = [target] + in_reach[1:d["splash"]] if d["splash"] > 1 else [target]
            for i, h in enumerate(hits):
                dmg = d["atk"] * (1 - h["df"]) * (1.0 if i == 0 else d["pct"])
                h["pending"].append((now + ATTACK_DELAY, dmg))
        # 용사 공격 — 앞줄(리스트 앞) 마왕군부터 집중
        for h in engaged:
            h["cd"] -= DT
            if h["heal"] > 0 and h["cd"] <= 0:
                h["cd"] += 1.0 / h["spd"]
                hurt = min((x for x in alive_h if x["hp"] < x["maxhp"]), key=lambda x: x["hp"] / x["maxhp"], default=None)
                if hurt: hurt["hp"] = min(hurt["maxhp"], hurt["hp"] + h["heal"])
                continue
            if h["atk"] <= 0 or h["cd"] > 0 or not alive_d:
                continue
            h["cd"] += 1.0 / h["spd"]
            target = alive_d[0]
            for i, d in enumerate([target] + (alive_d[1:h["splash"]] if h["splash"] > 1 else [])):
                dmg = h["atk"] * (1 - d["df"]) * (1.0 if i == 0 else h["pct"])
                d["hp"] -= dmg
        # 지연 피해 적용
        for h in heroes:
            if h["pending"]:
                rest = []
                for at, dmg in h["pending"]:
                    if at <= now: h["hp"] -= dmg
                    else: rest.append((at, dmg))
                h["pending"] = rest
        now += DT
    return False, 0.0, TIME_LIMIT


def evaluate(round_no, schedule, hp_mult, atk_mult, spd_mult=1.0, runs=200, seed=1):
    rng = random.Random(seed)
    res = [simulate(round_no, schedule, hp_mult, atk_mult, rng, spd_mult=spd_mult) for _ in range(runs)]
    win = sum(1 for r in res if r[0]) / runs
    rem = statistics.median(r[1] for r in res if r[0]) if any(r[0] for r in res) else 0.0
    dur = statistics.median(r[2] for r in res)
    return win, rem, dur


if __name__ == "__main__":
    for n in (1, 2, 3, 4, 5, 6):
        schedule = make_schedule(1, ["전"] * n)
        win, rem, dur = evaluate(1, schedule, 1.0, 1.0)
        print("R1 전사 %d마리: 클리어율 %3.0f%%  남은 마왕군 HP 중앙값 %2.0f%%  소요 %.0f초" % (n, win * 100, rem * 100, dur))
