# Gas Ventilation Systems

A RimWorld 1.6 mod that pipes four weaponised gases around your base using Vanilla Expanded Framework pipes.

| Gas | Affects | Effect |
|---|---|---|
| Toxin | Organic, breathing creatures | Burns the lungs |
| Sedative | Organic, breathing creatures | Knocks them unconscious (great for taking prisoners) |
| Haywire | Mechanoids, drones, robots, VRE androids | Internal damage, stun pulses, berserk at critical levels |
| Insecticide | Insectoids | Slowly burns them |

**How it works:**
1. Research **gas ventilation** (after Machining).
2. Make empty canisters at the machining table, and fill them at the **gas compounder**.
3. Build a **canister manifold**. Haulers keep it stocked, and drained canisters come back as empty shells.
4. Lay visible or hidden pipes, one colour per gas; different gases never mix.
5. Place vents: **wall** (replaces a wall), **wall-mounted**, **ceiling** or **floor grate**.
6. Switch vents on by hand, or set them to *Sensor* and add an **intruder sensor** to the room.

Gas spreads cell by cell like vanilla gas: it is blocked by walls and closed doors, and lingers longer indoors.
Creatures path around gas that hurts them and try to escape it. Trapped raiders and wild animals will smash their
way out.

**Requirements:** Harmony and Vanilla Expanded Framework. No DLC required.

**Compatibility:**
- Supports VRE Android, Humanoid Alien Races robot races and common robot-mod flesh types.
- Biotech gas masks protect against toxin and sedative gas.
- Works with Combat Extended; the gas damage ignores armour.
- Safe to add to an existing save. Before removing it, use the dev-mode action "Remove all Gas Ventilation content".

## Development

- Build: `dotnet build Source -c Release`. Output goes to `1.6/Assemblies`. VEF is referenced from the Workshop
  folder; override with `-p:VefDir=...`.
- Unit tests: `dotnet test Source -c Release`
- Package: `tools/package.ps1`, which writes `dist/GasVentilationSystems`. Upload that folder, never the repo.
- In-game tests: `tools/smoke-test.ps1 -Scenarios boot,grid,pipes,vents,effects,sensor,flee,breakout`. Needs Steam
  running and RimWorld closed. Add `perf` (per-tick budgets) and `balance` (balance metrics) for a full run.
- Design: `docs/superpowers/specs/`. Research: `docs/research/`. Art: `docs/texture-spec.md`.

Licence: MIT (see `LICENSE`).
