import json, sys, os, warnings, numpy as np, matplotlib
import matplotlib.pyplot as plt
import pandas as pd
from scipy.optimize import minimize
warnings.filterwarnings("ignore", category=UserWarning)

# ──────────────────── аргументы ────────────────────────────────
if len(sys.argv) < 3:
    sys.stderr.write("usage: diplomaApp.py <config.json> <out_dir>\n")
    sys.exit(1)

cfg_path = os.path.abspath(sys.argv[1])
out_dir  = os.path.abspath(sys.argv[2])
img_dir = out_dir

# ──────────────────── читаем JSON ──────────────────────────────
with open(cfg_path, encoding="utf-8") as f:
    cfg = json.load(f)

p_ph, p_geo, p_pump = cfg["Physical"], cfg["Geometry"], cfg["Pump"]
p_spd, p_w          = cfg["Speed"],    cfg["Weights"]

ρ, c_p, k           = p_ph["WaterDensity"], p_ph["SpecificHeatCapacity"], p_ph["ThermalConductivity"]
T_env, T_in, T_req  = p_ph["T_env"], p_ph["T_in"], p_ph["T_req"]
β_env, τ            = p_ph["Beta_env"], p_ph["Tau_h"]*3600

L, D_int, λ_fric    = p_geo["Length_m"], p_geo["D_int_m"], p_geo["LambdaFric"]

g = 9.81
H_static, η_pump, η_motor = p_pump["H_static_m"], p_pump["Eta_pump"], p_pump["Eta_motor"]
η_tot = η_pump*η_motor

V_MIN, V_MAX, Mseg  = p_spd["V_MIN"], p_spd["V_MAX"], p_spd["Mseg"]

α_J, β_J, γ_J, δT, λ_band = (
    p_w["Alpha_J"], p_w["Beta_J"], p_w["Gamma_J"], p_w["DeltaT"], p_w["LambdaBand"])

α   = k/(ρ*c_p)
A_cs = np.pi*(D_int/2)**2

# ───── DEBUG: вывод всех считанных/рассчитанных параметров ─────
print("\n===========  INPUT / PARAM DUMP  ===========")
print("• физика")
print(f"  ρ                = {ρ}  кг/м³")
print(f"  c_p              = {c_p}  Дж/(кг·К)")
print(f"  k                = {k}  Вт/(м·К)")
print(f"  T_env / T_in / T_req = {T_env}  {T_in}  {T_req}  K")
print(f"  β_env            = {β_env}  1/с")
print(f"  Tau_h (из JSON)  = {p_ph['Tau_h']}  {'ч' if p_ph['Tau_h']<=24 else 'c'}")
print(f"  τ (в секундах!)  = {τ}")

print("\n• геометрия")
print(f"  L                = {L}  м")
print(f"  D_int            = {D_int}  м")
print(f"  λ_fric           = {λ_fric}")

print("\n• насос")
print(f"  H_static         = {H_static}  м")
print(f"  η_pump / η_motor = {η_pump}  {η_motor}")
print(f"  η_tot            = {η_tot}")

print("\n• диапазон скоростей")
print(f"  V_MIN .. V_MAX   = {V_MIN} .. {V_MAX}  м/с")
print(f"  Mseg             = {Mseg}")

print("\n• веса целевой функции")
print(f"  α_J  β_J  γ_J    = {α_J}  {β_J}  {γ_J}")
print(f"  δT, λ_band       = {δT}  {λ_band:e}")

print("============================================\n", flush=True)
# ───── конец debug‑блока ─────

# ──────────────────── ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ ──────────────────
def Δp_total(v):
    return ρ*g*H_static + λ_fric * L/D_int * 0.5*ρ*v**2

def P_electric(v):
    return Δp_total(v) * A_cs * v / η_tot

def dt_stable(dx, v_max):
    v_max = max(v_max, 1e-4)
    return 0.8 * min(dx*dx/(2*α), dx/v_max)

def solve_heat(v_seg, x):
    dx = x[1] - x[0]
    dt = dt_stable(dx, v_seg.max())
    Nt = int(np.ceil(τ / dt))
    t  = np.linspace(0, Nt*dt, Nt+1)
    seg = np.minimum((t/τ*len(v_seg)).astype(int), len(v_seg)-1)

    T = np.full((Nt+1, x.size), T_env)
    r = α * dt / dx**2
    for n in range(Nt):
        s = v_seg[seg[n]] * dt / dx
        T[n+1,1:-1] = (
            T[n,1:-1]
            + r*(T[n,2:] - 2*T[n,1:-1] + T[n,:-2])
            - s*(T[n,1:-1] - T[n,:-2])
            - β_env*dt*(T[n,1:-1] - T_env)
        )
        T[n+1,0]   = T_in
        T[n+1,-1]  = T[n+1,-2]
    return t, T

def make_J(x):
    dx  = x[1] - x[0]
    S_h = (T_in - T_env)**2 * x[-1] * τ
    S_p = P_electric(V_MAX)**2 * τ
    S_o = (δT/2)**2 * τ

    def J(v):
        t, T = solve_heat(v, x)
        dt   = t[1] - t[0]
        J_h  = np.sum((T - T_env)**2) * dx * dt / S_h
        idx  = np.minimum((t/τ*len(v)).astype(int), len(v)-1)
        J_p  = np.sum(P_electric(v[idx])**2) * dt / S_p
        under= np.maximum(0, T_req - T[:,-1])
        J_o  = np.sum(under**2) * dt / S_o
        viol = np.maximum(0, np.abs(T[:,-1] - T_req) - δT/2)
        J_b  = λ_band * np.sum(viol**2) * dt
        return α_J*J_h + β_J*J_p + γ_J*J_o + J_b
    return J

# ──────────────────── ОПТИМИЗАЦИЯ ──────────────────────────────
Nx = 200
x  = np.linspace(0, L, Nx+1)
v0 = np.full(Mseg, 1.0)
bounds = [(V_MIN, V_MAX)] * Mseg

optimizers = [
    ("L-BFGS-B",   dict(method="L-BFGS-B",  options={'maxiter':300})),
    ("Nelder-Mead",dict(method="Nelder-Mead", options={'maxiter':900})),
]

try:
    import cma
    optimizers.append(("CMA-ES", {}))
except ModuleNotFoundError:
    pass

results = {}
for name, opts in optimizers:
    if name != "CMA-ES":
        res = minimize(make_J(x), v0, bounds=bounds, **opts)
        results[name] = res.x
    else:
        es = cma.CMAEvolutionStrategy(v0, 0.2,
                                      {'bounds':[V_MIN, V_MAX],'maxiter':350,'verb_log':0})
        es.optimize(make_J(x))
        results["CMA-ES"] = es.result.xbest

# ──────────────────── ВИЗУАЛИЗАЦИЯ ─────────────────────────────
plt.rcParams.update({'font.size':9})
rows = len(results)
fig  = plt.figure(figsize=(14, 4*rows))

for i, (meth, v_opt) in enumerate(results.items(), 1):
    tb = np.linspace(0, τ/60, Mseg+1)

    ax = fig.add_subplot(rows, 3, 3*i-2)
    ax.step(tb, np.r_[v_opt, v_opt[-1]], where='post')
    ax.set_xlabel('t, мин')
    ax.set_ylabel('v, м/с')
    ax.grid()
    ax.set_title(f"{meth}  |  v(t)")

    t_sim, T_sim = solve_heat(v_opt, x)
    X, Y = np.meshgrid(x, t_sim/3600)
    ax3 = fig.add_subplot(rows, 3, 3*i-1, projection='3d')
    ax3.plot_surface(X, Y, T_sim, cmap='viridis', rstride=8, cstride=10, linewidth=0)
    ax3.set_xlabel('x, м')
    ax3.set_ylabel('t, ч')
    ax3.set_zlabel('T, K')
    ax3.set_zlim(T_env-5, T_in+5)
    ax3.set_title("T(x,t)")

    P_seg = P_electric(v_opt)
    E_tot = P_seg.mean() * τ/3600
    axp = fig.add_subplot(rows, 3, 3*i)
    axp.step(v_opt, P_seg/1e3, where='mid')
    axp.scatter(v_opt, P_seg/1e3, s=10)
    axp.set_xlabel('v, м/с')
    axp.set_ylabel('P, кВт')
    axp.grid()
    axp.set_title(f"ΣEₑ = {E_tot:,.0f} кВт·ч")

fig.tight_layout()

os.makedirs(img_dir, exist_ok=True)                 # вдруг папки ещё нет
fig.savefig(os.path.join(img_dir, "profile.png"), dpi=150)

summary = []
for meth in ("Nelder-Mead", "L-BFGS-B"):          # две нужные методики
    v_opt   = results[meth]
    J_star  = make_J(x)(v_opt)                    # итог целевой функции
    E_kWh   = P_electric(v_opt).mean()*τ/3_600/1e3# ∑E насоса, кВт·ч
    T_min   = solve_heat(v_opt, x)[1][:,-1].min() # мин. T на выходе

    summary.append([meth, J_star, E_kWh, T_min])

df = pd.DataFrame(
        summary,
        columns=["Метод", "J*", "E_насоса_кВт·ч", "T_min_out_К"]
      )

out_csv = os.path.join(out_dir, "results.csv")
df.to_csv(out_csv, sep=';', index=False, float_format="%.6e", encoding="utf-8")
print(f"Таблица результатов сохранена в {out_csv}", flush=True)