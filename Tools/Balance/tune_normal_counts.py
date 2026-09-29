# -*- coding: utf-8 -*-
"""
보통(30R) 물량·용사 스탯 재산정 — sim_noskill.py(스킬 없이 깨지는지 시뮬)로 라운드별 물량을 이분 탐색한다.

  · 용사 HP·공격 배율은 라운드마다 꾸준히 오르는 곡선을 직접 지정한다(HP_GROWTH / ATK_GROWTH) — 시트 05의 C·D열을 덮어씀.
    (v0.8은 "물량 상한 전에는 HP 고정"이라 R16까지 용사 체력이 안 늘어난다는 피드백을 받아 폐기)
  · 물량 = 그 스탯·스폰 간격에서 "스킬 없이" MIN_WIN 이상 깨지는 최대 물량(직전 라운드 대비 +MAX_STEP 이내, 줄지 않게, 상한 ABS_CAP)
  · 스폰 간격은 sim_noskill.spawn_interval(라운드) — 라운드가 갈수록 뭉쳐서 밀려오게, 직업은 interleave로 섞어서 내보냄
  · R1=3, R2=6은 사용자가 "밸런스 괜찮다"고 확인한 값이라 고정
  · 직업 구성·공속 배율은 시트 05 그대로 사용

사용법: python Tools/Balance/tune_normal_counts.py <입력 시트.xlsx> <출력 시트.xlsx>
"""
import os
import re
import sys

import openpyxl

sys.path.insert(0, os.path.dirname(__file__))
import sim_noskill as sim  # noqa: E402

BOSS_ROUNDS = {10: "보스", 20: "보2", 30: "최종"}   # H_BOSS_01 / H_BOSS_02 / H_BOSS_FINAL_01
FIXED = {1: 3, 2: 6}      # 사용자가 확인한 라운드 물량
RUNS = 80

HP_GROWTH = 1.03          # 용사 HP 배율 = 1.03^(R-1)  (R10 x1.31 · R20 x1.75 · R30 x2.36)
ATK_GROWTH = 1.02         # 용사 공격 배율 = 1.02^(R-1) (R10 x1.20 · R20 x1.46 · R30 x1.78)
ABS_CAP = 80              # 물량 절대 상한(화면·경로·성능 한계)
MAX_STEP = 10             # 직전 일반 라운드 대비 최대 증가폭


def min_win(r):
    """스킬 없이 이 확률 이상은 깨져야 한다 — 초반은 스킬 없이 넉넉히, 후반은 스킬·시너지·증강이 있어야 하도록 낮춘다."""
    return 0.75 if r <= 5 else 0.60 if r <= 15 else 0.45
BOSS_MIN_RATIO = 0.4      # 보스 라운드 물량 하한 = 직전 일반 라운드 × 0.4


def parse_comp(text):
    out = []
    for part in text.split("/"):
        m = re.match(r"\s*(\S+)\s+(\d+(?:\.\d+)?)\s*$", part)
        if m:
            out.append((m.group(1)[0], float(m.group(2))))    # 전사→전, 방패병→방, 도적→도, 궁수→궁, 마법사→마, 힐러→힐
    return out


def split_counts(total, weights):
    s = sum(w for _, w in weights)
    exact = [total * w / s for _, w in weights]
    base = [int(x) for x in exact]
    left = total - sum(base)
    for i in sorted(range(len(weights)), key=lambda i: exact[i] - base[i], reverse=True)[:left]:
        base[i] += 1
    return [(j, n) for (j, _), n in zip(weights, base) if n > 0]


def build_seq(total, weights, boss_key):
    """스폰 순서대로 나열한 직업 키 목록(직업 섞기 + 보스는 마지막)."""
    escorts = total - 1 if boss_key else total
    seq = sim.expand(sim.interleave(split_counts(max(escorts, 0), weights))) if escorts > 0 else []
    if boss_key:
        seq.append(boss_key)
    return seq


def win_rate(r, n, weights, boss_key, hp, atk, spd):
    schedule = sim.make_schedule(r, build_seq(n, weights, boss_key))
    win, rem, dur = sim.evaluate(r, schedule, hp, atk, spd_mult=spd, runs=RUNS, seed=r * 101 + n)
    return win, rem, dur


def main():
    src, dst = sys.argv[1], sys.argv[2]
    wb = openpyxl.load_workbook(src)
    ws = next(wb[n] for n in wb.sheetnames if n.startswith("05."))
    prev = 0
    for r in range(1, 31):
        row = 3 + r
        hp = round(HP_GROWTH ** (r - 1), 3)
        atk = round(ATK_GROWTH ** (r - 1), 3)
        spd = float(ws.cell(row=row, column=6).value)
        weights = parse_comp(str(ws.cell(row=row, column=10).value))
        boss_key = BOSS_ROUNDS.get(r)

        if r in FIXED:
            n = FIXED[r]
        else:
            lo, hi = 2, min(prev + MAX_STEP, ABS_CAP)
            best = lo
            while lo <= hi:
                mid = (lo + hi) // 2
                if win_rate(r, mid, weights, boss_key, hp, atk, spd)[0] >= min_win(r):
                    best, lo = mid, mid + 1
                else:
                    hi = mid - 1
            n = best
            if not boss_key:
                n = min(max(n, prev - 2), prev + MAX_STEP)   # 거의 줄지 않게(최대 -2), 급점프 방지
            else:
                # 보스에도 용사 배율이 곱해져 후반 보스가 매우 세지므로, 시뮬만 따르면 호위가 2~3마리로 쪼그라든다.
                # 설계 원칙(보스 라운드 물량 = 직전 일반 라운드의 40%, 단일 고체력)을 하한으로 둔다.
                n = max(n, int(round(prev * BOSS_MIN_RATIO)))
        if not boss_key:
            prev = n

        win, rem, dur = win_rate(r, n, weights, boss_key, hp, atk, spd)
        ws.cell(row=row, column=3).value = hp
        ws.cell(row=row, column=4).value = atk
        ws.cell(row=row, column=5).value = float(n)
        size, gap = sim.wave_plan(r, n)
        print("R%2d  물량 %2d  HP x%.2f 공격 x%.2f  웨이브 %2d마리/%.1fs  스킬없이 클리어 %3.0f%%  남은HP %2.0f%%  %3.0f초"
              % (r, n, hp, atk, size, gap, win * 100, rem * 100, dur))

    ws["A2"] = (f"HP배율={HP_GROWTH}^(R-1) · 공격={ATK_GROWTH}^(R-1) · 힐량=1.0141^(R-1) · 물량=스킬 없이 R2~5 75% / R6~15 60% / R16~ 45% 이상 깨지는 최대치(상한 {ABS_CAP}) · "
                "스폰=웨이브(웨이브 안 동시 스폰, 크기 1→20마리·간격 3.0→1.5초), 직업 섞어서 — Tools/Balance/sim_noskill.py · tune_normal_counts.py "
                "(v1.0: R1=3·R2=6 고정, 2성 x1.7·3성 x3.2 기준)")
    wb.save(dst)
    print("saved", dst)


if __name__ == "__main__":
    main()
