# -*- coding: utf-8 -*-
"""
MVS（市场经济框架）离线数值稳定性仿真器 · v3
================================================================
目的：闭合设计方案 §9.1「门禁③ 数值稳定性」的**离线部分**
     （参数 1–5、9–12、14、15；参数 6–8、13、16 需游戏内长时观测，本仿真给可行区间）。

闭环（每步 = 1 游戏日）：存量 S → 价格因子 F → 消费 δ → 存量 S'
----------------------------------------------------------------
  F*       = (S_eq / S_t) ** k_d                       # 库存失衡 → 目标价（乘法式，天然有界）
  F_tgt    = F_{t-1} + alpha*(F* - F_{t-1}) + revert*(1 - F_{t-1})   # 平滑 + 向中性回归（参数 6/8）
  F_tgt    = clamp(F_tgt, Fmin, Fmax)                  # (10)
  F_tgt   *= (1 + shock_t)                             # 外生冲击（若有）
  F_t      = F_{t-1} + clamp(F_tgt - F_{t-1}, ±shockCap)  # 单 tick 变化上限（参数 9）
  delta_t  = delta0 * F_t^(-e) * (1 + noise)           # 价格弹性消费（参数 2/3）
  sigma_t  = sigma0                                    # v1 供应外生（产能，§11）
  export_t = exportRate * max(0, S_t - mult*S_eq)      # 软出口 sink（参数 12）
  S_{t+1}  = S_t + sigma_t - delta_t - export_t

解析（本仿真验证）
----------------------------------------------------------------
  J = [[1, e·δ0], [-α·k_d/S_eq, 1-α-rev]]，G = e·k_d/τ = e·k_d·δ0/S_eq
  线性稳定 ⟺ det<1（G 小）；复特征值（阻尼振荡）⟺ G > (α+rev)/4
  ⇒ 三区制：G<(α+rev)/4 单调；中间阻尼振荡；超界发散。
"""
from __future__ import annotations

import json
import random
from dataclasses import dataclass, asdict

CONS_TOL = 1e-6


@dataclass
class Cfg:
    e: float = 1.0                # (2) 价格弹性
    k_d: float = 1.0              # (3) 需求/价格系数（F* 斜率）
    alpha: float = 0.15           # (6) 因子更新平滑系数
    revert_rate: float = 0.0      # (8) 向中性回归速率
    shock_cap: float = 0.05       # (9) 单 tick 变化上限
    f_min: float = 0.5            # (10)
    f_max: float = 2.5            # (10)
    tau_days: float = 3.0         # (4/5) S_eq = tau * delta0
    delta0: float = 100.0
    sigma0: float = 100.0
    s_min_mult: float = 0.0       # (11)
    s_max_mult: float = 4.0       # (11)
    export_rate: float = 0.0      # (12)
    export_mult: float = 2.0      # (12)
    noise: float = 0.0
    s0_mult: float = 1.2
    step_at: int = -1
    step_mult: float = 1.0
    seed: int = 0

    @property
    def S_eq(self) -> float:
        return self.tau_days * self.delta0

    @property
    def G(self) -> float:
        return self.e * self.k_d / self.tau_days

    @property
    def osc_thr(self) -> float:
        return (self.alpha + self.revert_rate) / 4.0


def _sign_flips(seq):
    d = [seq[i + 1] - seq[i] for i in range(len(seq) - 1)]
    return sum(1 for i in range(1, len(d))
               if d[i - 1] != 0 and d[i] != 0 and (d[i - 1] > 0) != (d[i] > 0))


def run(cfg: Cfg, days: int = 3000, shocks: dict | None = None) -> dict:
    rng = random.Random(cfg.seed)
    S_eq = cfg.S_eq
    s_max, s_min = cfg.s_max_mult * S_eq, cfg.s_min_mult * S_eq
    S = cfg.s0_mult * S_eq
    F = 1.0
    delta0 = cfg.delta0

    hsF, hsS = [], []
    sig_sum, out_sum, emerg_in, over_out = 0.0, 0.0, 0.0, 0.0

    for t in range(days):
        if cfg.step_at >= 0 and t == cfg.step_at:
            delta0 = cfg.delta0 * cfg.step_mult
        F_star = (S_eq / max(S, 1e-9)) ** cfg.k_d
        F_tgt = F + cfg.alpha * (F_star - F) + cfg.revert_rate * (1.0 - F)
        F_tgt = min(max(F_tgt, cfg.f_min), cfg.f_max)
        if shocks and t in shocks:
            F_tgt *= (1.0 + shocks[t])
        d = max(-cfg.shock_cap, min(cfg.shock_cap, F_tgt - F))
        F = min(max(F + d, cfg.f_min), cfg.f_max)

        n = 1.0 + (rng.uniform(-cfg.noise, cfg.noise) if cfg.noise > 0 else 0.0)
        delta = max(0.0, delta0 * (F ** (-cfg.e)) * n)
        export = cfg.export_rate * max(0.0, S - cfg.export_mult * S_eq)
        S_new = S + cfg.sigma0 - delta - export
        if S_new < s_min:
            emerg_in += (s_min - S_new); S_new = s_min
        if S_new > s_max:
            over_out += (S_new - s_max); S_new = s_max
        sig_sum += cfg.sigma0; out_sum += delta + export
        S = S_new
        hsF.append(F); hsS.append(S)

    q = days // 4
    osc_trans = _sign_flips(hsF)                       # 全程（含瞬态）
    osc_tail = _sign_flips(hsF[3 * q:])                # 后 1/4（持续振荡判据）
    tailF, tailS = hsF[3 * q:], hsS[3 * q:]
    late_amp = (max(tailF) - min(tailF)) if tailF else 0.0
    overshoot = max((abs(f - 1.0) for f in hsF), default=0.0)

    settle = -1
    for t in range(days):
        ok = True
        for u in range(t, min(days, t + 200), 10):
            if abs(hsF[u] - 1.0) >= 0.02 or abs(hsS[u] - S_eq) / S_eq >= 0.02:
                ok = False; break
        if ok:
            settle = t; break

    F_final, S_final = hsF[-1], hsS[-1]
    bounded = (cfg.f_min - 1e-9 <= min(hsF) and max(hsF) <= cfg.f_max + 1e-9
               and s_min - 1e-6 <= min(hsS) and max(hsS) <= s_max + 1e-6)

    if not bounded:
        regime = "发散/越界"
    elif osc_tail > 1:
        regime = "持续振荡"
    elif osc_trans > 1:
        regime = "阻尼振荡"
    else:
        regime = "单调收敛"

    # 库存锚定：相对 S_eq 的终值偏离（越小越"有锚"，与需求阶跃场景相关）
    anchor_dev = (S_final - S_eq) / S_eq

    S_expected = S_eq * cfg.s0_mult + sig_sum - out_sum + emerg_in - over_out
    cons_res = abs(S_final - S_expected) / max(1.0, abs(S_expected))

    return {
        "k_d": cfg.k_d, "alpha": cfg.alpha, "revert": cfg.revert_rate,
        "G": round(cfg.G, 4), "osc_thr": round(cfg.osc_thr, 4),
        "regime": regime,
        "osc_transient": osc_trans, "osc_tail": osc_tail,
        "settle": settle, "overshoot": round(overshoot, 4),
        "late_amp": round(late_amp, 4),
        "F_final": round(F_final, 4), "anchor_dev": round(anchor_dev, 4),
        "bounded": bool(bounded),
        "over_out_rel": round(over_out / max(1.0, S_eq), 4),
        "emerg_in_rel": round(emerg_in / max(1.0, S_eq), 4),
        "cons_res": cons_res, "cons_ok": cons_res < CONS_TOL,
    }


def sweep_g(alpha=0.15, tau=3.0):
    rows = []
    for k_d in [0.05, 0.1125, 0.3, 0.6, 1.0, 1.5, 2.0, 3.0, 4.0, 6.0, 9.0]:
        cfg = Cfg(k_d=k_d, alpha=alpha, tau_days=tau, s0_mult=1.2)
        r = run(cfg)
        r["G_over_thr"] = round(cfg.G / cfg.osc_thr, 1)
        rows.append(r)
    return rows


def sweep_alpha():
    rows = []
    for al in [0.05, 0.10, 0.15, 0.30, 0.50, 1.00]:
        cfg = Cfg(k_d=1.0, alpha=al, tau_days=3.0, s0_mult=1.2)
        r = run(cfg)
        r["thr_G"] = round(cfg.osc_thr, 4)
        rows.append(r)
    return rows


def mc_shocks(base: Cfg, trials=60, days=3000, period=600):
    rng = random.Random(2026)
    bounded = 0; max_osc = 0; max_over = 0.0; max_tail = 0.0
    for i in range(trials):
        shocks = {}; t = 100
        while t < days - 200:
            shocks[t] = rng.choice([-1, 1]) * rng.uniform(0.05, 0.40); t += period
        r = run(Cfg(**{**asdict(base), "seed": i}), days=days, shocks=shocks)
        bounded += int(r["bounded"])
        max_osc = max(max_osc, r["osc_tail"])
        max_over = max(max_over, r["overshoot"])
        max_tail = max(max_tail, r["late_amp"])
    return {"trials": trials, "bounded_rate": round(bounded / trials, 3),
            "max_tail_osc": max_osc, "max_overshoot": round(max_over, 4),
            "max_tail_amp": round(max_tail, 4)}


def sweep_shockcap():
    rows = []
    for sc in [0.005, 0.01, 0.02, 0.05, 0.10, 0.25, 0.50]:
        cfg = Cfg(k_d=1.0, shock_cap=sc, tau_days=3.0, s0_mult=1.2)
        r = run(cfg, days=1500, shocks={100: 3.0})
        rows.append({"shock_cap": sc, "F_peak": round(r["overshoot"] + 1.0, 4),
                     "osc_trans": r["osc_transient"], "settle": r["settle"],
                     "regime": r["regime"]})
    return rows


def s0_normalization():
    tau = 3.0
    fs = [{"id": "A小", "delta0": 30.0}, {"id": "B大", "delta0": 300.0}]
    Sb_flat = 3000.0
    S = {"A小": 2.0 * Sb_flat, "B大": 1.0 * Sb_flat}
    raw_flat = (sum(S.values()) / (2 * Sb_flat)) - 1.0
    Sb = {f["id"]: tau * f["delta0"] for f in fs}
    S2 = {"A小": 2.0 * Sb["A小"], "B大": 1.0 * Sb["B大"]}
    wtot = sum(f["delta0"] for f in fs)
    wt_prop = sum((S2[f["id"]] - Sb[f["id"]]) / Sb[f["id"]] * f["delta0"]
                  for f in fs) / wtot
    return {"平铺基线_Sb常数_global_dev": round(raw_flat, 4),
            "需求比例基线_Sb=τ·δ0_global_dev":
                round((sum(S2.values()) / sum(Sb.values())) - 1.0, 4),
            "需求加权_dev": round(wt_prop, 4),
            "结论": "基线必须 ∝ 该派系需求（τ·δ0）并按需求份额聚合；"
                    "平铺基线会把小派系的偏离按体量错配 → 全球因子被单派系牵引。"}


def conservation_check():
    out = []
    cases = [("无界·无出口", Cfg(export_rate=0.0, s_max_mult=1e9, s0_mult=1.5)),
             ("软出口", Cfg(export_rate=0.02, export_mult=2.0, s0_mult=1.5)),
             ("硬上界+溢出", Cfg(s_max_mult=1.5, s0_mult=2.0)),
             ("硬下界+应急进口", Cfg(s_min_mult=0.2, s0_mult=0.2))]
    for label, cfg in cases:
        r = run(cfg, days=2000)
        out.append({"case": label, "cons_res": r["cons_res"], "cons_ok": r["cons_ok"],
                    "over_out_rel": r["over_out_rel"], "emerg_in_rel": r["emerg_in_rel"]})
    return out


def step_response(revert):
    cfg = Cfg(k_d=1.0, tau_days=3.0, revert_rate=revert, s0_mult=1.0,
              step_at=300, step_mult=1.3)
    r = run(cfg, days=3000)
    return {"revert_rate": revert, "regime": r["regime"], "settle": r["settle"],
            "osc_transient": r["osc_transient"], "anchor_dev": r["anchor_dev"],
            "F_final": r["F_final"]}


def recommend():
    tau, e, k_d, rev = 3.0, 1.0, 1.0, 0.0
    return {"e": e, "k_d": k_d, "tau_days": tau, "alpha": 0.15,
            "revert_rate": rev, "shock_cap": 0.02, "f_min": 0.5, "f_max": 2.5,
            "s_min_mult": 0.0, "s_max_mult": 4.0,
            "export_rate": 0.02, "export_mult": 2.0,
            "G": round(e * k_d / tau, 4), "cons_tol": CONS_TOL}


def main():
    rep = {}
    rec = recommend()
    rep["推荐参数"] = rec
    rep["模型"] = "F*=(S_eq/S)^k_d; δ=δ0·F^-e; G=e·k_d/τ; 阻尼振荡阈值 G>(α+rev)/4"
    rec_cfg = Cfg(e=rec["e"], k_d=rec["k_d"], tau_days=rec["tau_days"],
                  alpha=rec["alpha"], revert_rate=rec["revert_rate"],
                  shock_cap=rec["shock_cap"], f_min=rec["f_min"],
                  f_max=rec["f_max"], s_max_mult=rec["s_max_mult"],
                  export_rate=rec["export_rate"], export_mult=rec["export_mult"],
                  s0_mult=1.2)
    rep["场景1_G扫描"] = sweep_g()
    rep["场景2_alpha敏感性"] = sweep_alpha()
    rep["场景3_蒙特卡洛冲击"] = mc_shocks(rec_cfg)
    rep["场景4_shockCap扫描"] = sweep_shockcap()
    rep["场景5_S0归一"] = s0_normalization()
    rep["场景6_守恒"] = conservation_check()
    rep["场景7_需求阶跃_revert=0"] = step_response(0.0)
    rep["场景7_需求阶跃_revert=0.02"] = step_response(0.02)

    with open("_scripts/_mvs_stability_results.json", "w", encoding="utf-8") as f:
        json.dump(rep, f, ensure_ascii=False, indent=2)

    print("=" * 78)
    print("MVS 数值稳定性仿真 v3 · 摘要")
    print("=" * 78)
    print(f"\n[推荐参数] {rec}")
    print("\n[场景1 G扫描]  k_d      G    G/阈值  分型     瞬态振荡 持续振荡 安定 overshoot")
    for r in rep["场景1_G扫描"]:
        print(f"  {r['k_d']:>7} {r['G']:>6} {r['G_over_thr']:>7}  {r['regime']:<7}"
              f" {r['osc_transient']:>7} {r['osc_tail']:>7} {r['settle']:>5}"
              f" {r['overshoot']:>8}")
    print("\n[场景2 α敏感性] α      阈值G    G      分型     瞬态 持续 overshoot")
    for r in rep["场景2_alpha敏感性"]:
        print(f"  {r['alpha']:>5} {r['thr_G']:>8} {r['G']:>6}  {r['regime']:<7}"
              f" {r['osc_transient']:>4} {r['osc_tail']:>4} {r['overshoot']:>8}")
    print("\n[场景3 蒙特卡洛冲击]", rep["场景3_蒙特卡洛冲击"])
    print("\n[场景4 shockCap]  cap      F峰值   瞬态振荡 安定")
    for r in rep["场景4_shockCap扫描"]:
        print(f"  {r['shock_cap']:>6} {r['F_peak']:>8} {r['osc_trans']:>8} {r['settle']:>5}")
    print("\n[场景5 S0归一]")
    for k, v in rep["场景5_S0归一"].items():
        print(f"  {k}: {v}")
    print("\n[场景6 守恒]")
    for r in rep["场景6_守恒"]:
        print(f"  {r['case']:<14} 残差={r['cons_res']:.2e} ok={r['cons_ok']}")
    print("\n[场景7 需求阶跃 +30%] revert=0   :", rep["场景7_需求阶跃_revert=0"])
    print("                        revert=0.02:", rep["场景7_需求阶跃_revert=0.02"])
    print("\n结果 → _scripts/_mvs_stability_results.json")


if __name__ == "__main__":
    main()
