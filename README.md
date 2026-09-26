# RimWorld Gas Ventilation (working title)

A RimWorld 1.6 mod built on the Vanilla Expanded Framework pipe system (PipeSystem).
Players build pipe networks that carry one of four gases from canister-fed feeders
to vents:

| Gas            | Affects                          | Effect                                   |
|----------------|----------------------------------|------------------------------------------|
| Toxic          | Organic lifeforms                | Lung damage                              |
| Sleep          | Organic lifeforms                | Rapid unconsciousness                    |
| Anti-mech      | Mechanoids, androids, robots     | Haywire-style damage                     |
| Anti-insectoid | Insectoids                       | Slow damage                              |

Status: research and design. See `docs/research/` and `docs/superpowers/specs/`.

## Local references

`refs/` is gitignored. It holds a Vanilla Expanded Framework clone and a decompiled
copy of the game's `Assembly-CSharp.dll`, for reference only. Never commit decompiled
game code.
