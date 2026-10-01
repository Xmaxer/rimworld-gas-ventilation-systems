---
name: mod-art
description: Use when adding, regenerating, or fixing a texture asset for this mod (building sprites, item icons, UI/gizmo icons, Workshop images). Covers the Gemini generation pipeline, real-alpha finalization, dimension verification, wall-attachment offset tuning, and placing finished art into Textures/.
---

# Mod art pipeline

Everything needed to take a new or replacement texture asset from "nothing" to "correctly
placed in `Textures/`" lives under `tools/asset-pipeline/`:

```
tools/asset-pipeline/
  generate_asset.py       single-shot Gemini image generator
  process_asset.py        real-alpha extraction + crop-to-content + fit-to-canvas
  .env                     GEMINI_API_KEY (gitignored — never read or print its value)
  comfy/
    bg_remove_wf.json      ComfyUI workflow: LoadImage -> BiRefNet -> real alpha -> SaveImage
    swap_ops_template.json template for retargeting the workflow at a new input file
  drafts/<slug>/vNN.png    every generation attempt, kept forever, never overwritten
  reference-mods/          a large local corpus of other RimWorld mods' textures, for
                            grounding prompts in real conventions (see below)
  backups/                 timestamped zips taken before bulk-replacing shipped art
```

## Golden rule: never bulk-generate

`generate_asset.py` deliberately does **one image per invocation**. Run it, look at the
result, decide what to do next — new prompt, new reference, or accept it. Never loop or
batch multiple assets/versions in one go, even if the spec lists many remaining files.

## Step by step

1. **Read `docs/texture-spec.md` first.** It has the exact path, size, and notes for every
   asset (e.g. "N/S 448x192, E 192x448", "keep alpha around 0.6-0.8", directional-file
   conventions). Never guess a size or path. This file is gitignored (dev-process notes,
   not shipped in the public repo) -- it only exists in this local checkout. If it's
   missing (e.g. a fresh clone), ask the user for it before inventing sizes/paths.

2. **Look for a real precedent before inventing a design.** `reference-mods/` holds a large
   scraped corpus of other RimWorld mods (Vanilla Expanded family and others). Before
   designing a UI icon or an unfamiliar object from imagination, search it:
   ```
   find tools/asset-pipeline/reference-mods -iname "*keyword*"
   ```
   This project's UI icons were reliably wrong on the first attempt whenever invented from
   scratch, and right on the first or second attempt whenever grounded in a real match —
   e.g. a "hidden/buried pipe" icon convention (diagonal hazard stripes painted across the
   pipe body) and a "deconstruct" icon convention (a stepped brick-rubble wedge overlaid on
   one corner) both turned out to be consistent, real patterns used across several
   unrelated mods, not something to invent freely.

3. **Generate a draft.**
   ```
   python tools/asset-pipeline/generate_asset.py --asset <slug> \
     --ref <reference image path...> \
     --prompt "..."
   ```
   Output goes to `drafts/<slug>/vNN.png` with a sidecar `vNN.json` recording the exact
   prompt/refs/model used. `--ref` is repeatable; pass 1-2 images (the thing being edited
   or matched, plus a style reference) — more than that tends to confuse the model about
   which image is the geometry source vs. the style source, so say explicitly in the prompt
   which image is which.

   Two established house styles, pick the one that fits the asset:
   - **Painted building/item art**: flat cel-shading, 2-3 discrete flat color bands per
     rounded form (base/shadow/optional highlight), hard or barely-feathered edges between
     bands, no gradients, no bevels, no small bright highlight dots, a single thin uniform
     dark outline, transparent background. (`generate_asset.py`'s `DEFAULT_PROMPT` is one
     example of this phrasing — reuse the language, not necessarily the exact subject.)
   - **UI/gizmo icons**: bold simple shapes, thin uniform dark outline, mostly flat neutral
     color with at most one accent color, 1-2 tone bands, matching vanilla's own gizmo icon
     language — noticeably simpler/flatter than the painted building style.

4. **Never trust Gemini's background as real transparency.** It always bakes a fake
   checkerboard (neutral grey/white, R==G==B) into plain RGB pixel data to *simulate*
   transparency — this is never real alpha, even though it looks transparent in a preview.

5. **Extract real alpha with BiRefNet, not a plain heuristic,** whenever precision matters
   (small icons, near-white/near-grey subjects, or anything you're about to verify pixel by
   pixel):
   - Find ComfyUI's real configured input directory — don't assume the per-install default:
     `comfy --json system_stats` → `argv` → `--input-directory`.
   - Copy the draft PNG there, then:
     ```
     comfy nodes refresh
     comfy workflow apply tools/asset-pipeline/comfy/bg_remove_wf.json \
       --ops <ops file setting the LoadImage widget to your filename>
     comfy --json run --workflow tools/asset-pipeline/comfy/bg_remove_wf.json
     comfy --json jobs watch <prompt_id>
     comfy --json download <prompt_id> --out-dir <dest>
     ```
   - `tools/asset-pipeline/comfy/swap_ops_template.json` is a ready template for the `--ops`
     file — copy it and replace the filename.
   - `process_asset.py`'s own `rebuild_alpha()` heuristic (classify background by "neutral
     color AND close to one of the two known checker shades") is fine for quick iteration
     but unreliable whenever the subject's own paint is itself near-neutral — it has
     confused a flat-shaded grey pipe fill with the fake checkerboard before. Prefer the
     BiRefNet path for anything final.

6. **Crop and fit to the exact target canvas:**
   ```
   python tools/asset-pipeline/process_asset.py --in <path> --out <path> \
     --width <W> --height <H>
   ```
   This crops tight to content (alpha > threshold) and scales to fit the target box,
   preserving aspect ratio, centered on a transparent canvas of the exact target size.

   Some assets need a *non-centered* finalization instead — e.g. a wall-mounted device
   whose art should be anchored to one edge of the canvas rather than centered (see the
   wall-attachment section below for why). There's no flag for this; do a one-off
   crop+scale+paste-at-edge rather than extending the shared script for a one-off need.

7. **Verify the final pixel size matches the spec table** (`PIL.Image.open(p).size`) before
   copying anywhere — don't assume a resize produced exactly what you asked for.

8. **Back up before bulk-replacing shipped art, then place each file at its exact spec
   path.** Zip the current `Textures/` (and `About/` if touching Workshop images) before
   overwriting, same pattern as the zips already in `tools/asset-pipeline/backups/`. Only
   touch the specific files you're actually replacing — check `git status --short`
   afterward and confirm nothing unexpected was modified, renamed, or deleted.

## Composing UI icons that represent state (on/off/armed/mode/etc.)

Generate **one neutral base icon**, then composite small colored badges onto copies of it
locally with PIL (e.g. a red X / green check / blue eye circle in a corner) rather than
re-generating the whole icon per state. This guarantees every state shares pixel-identical
base art and is far faster to iterate than re-prompting an image model per variant.

## Wall-attachment buildings (devices mounted on an existing wall)

A building like a wall-mounted vent or sensor (`isAttachment: true`,
`placeWorkers: Placeworker_AttachedToWall`) is placed on the room-side cell *adjacent* to a
wall, and its `GraphicData.drawOffsetNorth/South/East/West` shifts the rendered sprite
toward that wall so it appears mounted on it. Vanilla's own wall-mounted fixtures
(`WallLamp`, `TorchWallLamp`) use `drawOffsetNorth="(0,0,0.9)"` — but that value is tuned
for a *thin* object (its visible content fills only ~13% of its canvas, anchored hard
against one edge). A chunkier custom object that fills ~80% of its own canvas will overshoot
badly at the same 0.9 offset, ending up deep inside the wall tile with a visible gap instead
of sitting flush against — or slightly protruding past — the wall's near edge.

How this was actually tuned, without waiting on a full in-game rebuild each iteration:
write a small offline script that pastes the real PNG onto a flat two-color grid (one color
for the wall cell, one for the room cell) at the exact pixel offset the engine would use —
`cell_size_px * drawOffset`, shifted the correct screen direction for the rotation (north =
up = toward smaller row index; east = right = toward larger column index) — and look at the
result. This reproduces the actual positioning in seconds and was how the correct offset
magnitude (and later, a deliberately smaller one for a visible protrusion into the room) was
found, confirmed, and re-tuned per follow-up feedback, entirely offline.

Keep in mind the needed offset depends on how much of the canvas the art actually fills —
a narrower asset (e.g. a side-profile silhouette) needs a smaller offset than a wider one to
land in the same place relative to the wall, even on the same building.
