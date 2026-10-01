---
name: mod-art-linked-atlas
description: Use when adding or restyling a Linked-graphic connection atlas (a 4x4 grid of pipe/wire/conduit connection-shape tiles) for this mod. Covers the engine's tile-sampling convention and the geometry-preservation + verification method that keeps edge-to-edge connections from silently breaking.
---

# Linked atlas art (4x4 connection-shape grids)

A Linked atlas (e.g. the pipe atlas) is a single 256x256 PNG holding 16 tiles of 64x64 px,
one per possible neighbor-connection shape, in the same layout vanilla uses for its power
conduit atlas. See `docs/texture-spec.md` for the authoritative layout rules; the two that
matter most for art safety:

- The engine samples **row 0 from the bottom of the file** (a Unity V-flip specific to how
  this atlas is indexed into a grid — this is *not* a general property of ordinary sprites;
  regular directional building textures do not have this flip).
- Only the **inner 48x48 px of every 64x64 tile** is ever sampled in-game — an 8px border
  per edge is invisible. This is the connection-critical zone: whatever touches those 48x48
  edges on one tile must exactly match what the neighboring tile's matching edge expects,
  or pipes/wires will visibly fail to connect cleanly across map tiles.

Read before touching this atlas: even though a restyle can look completely clean and
correct by eye, a single shifted pixel at a sampled edge can break connections invisibly.
Always verify programmatically (below) before accepting a new version — do not rely on
visual inspection alone, no matter how clean it looks.

## Safe generation approach

Treat it as an **image-editing task, not a new-generation task**. Feed the model:
1. The existing atlas (placeholder or previously-verified art) as the geometry source, with
   an explicit instruction to preserve the exact silhouette, position, width, and
   edge-touch points of every shape — do not move, resize, or reinterpret any shape, and do
   not change the grid layout.
2. A separate small reference image purely for *style* (palette, shading technique),
   explicitly labeled in the prompt as the style reference, not the geometry source.

Ask for exactly the changes you want (e.g. "remove the number labels", "restyle the fill
using this cel-shading technique", "remove the colored dots") and nothing else. If an
attempt introduces unrelated visual corruption (compression-artifact-looking noise,
unrequested color shifts), don't try to patch it — just regenerate; it's a cheap single
image call.

## Mandatory verification before accepting a version

1. Extract **real alpha via BiRefNet** (see the `mod-art` skill) — never trust the plain
   heuristic checker-background classifier here. Pipe/wire fill colors are frequently close
   to neutral grey, which the heuristic can confuse with the fake checkerboard background,
   producing a false "broken" or false "fine" read.
2. Downscale the BiRefNet result to the atlas's real pixel size (e.g. 1024x1024 raw output
   down to 256x256) with high-quality resampling (LANCZOS).
3. For every one of the 16 tiles, extract the **48x48 inset zone** (8px border excluded) and
   compare its 4 edge strips (the actual pixel-by-pixel alpha>threshold boolean pattern
   along each edge, not just "does this edge touch alpha anywhere") against the same tile in
   the last-known-good atlas. Zero mismatched pixels across all 16 tiles x 4 edges = safe.
   Any mismatch = geometry drifted; do not ship that version.

   Parameters for this mod's pipe atlas: `TILE=64, BORDER=8, INSET=48, THRESH=100`.

   A lower interior silhouette overlap (IoU) between old and new is expected and fine when
   the new art intentionally removed something (e.g. number labels or indicator dots that
   lived outside the actual pipe shape) — only the edge-strip pixel comparison is the real
   safety signal; silhouette-area differences alone are not.

4. A pure color recolor of an **already-verified** atlas (e.g. producing a translucent
   blueprint-style variant) does not need to repeat this check — it's a pixel-for-pixel RGB
   remap of the same verified alpha mask, so it inherits the proven geometry automatically.
