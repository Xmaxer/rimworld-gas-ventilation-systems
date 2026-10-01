"""Reprocess a flattened (opaque, white-background) generated asset into a
game-ready transparent sprite at an exact target canvas size.

Steps: rebuild real alpha (flood-fill white background from the border, so
internal near-white details like gauge faces survive) -> crop tight to
content -> resize preserving aspect ratio to fit within the target box ->
center on a transparent canvas of the exact target size.

Usage:
    python process_asset.py --in compounder_east/v10.png \
        --out compounder_east/v10_final_east.png \
        --width 192 --height 448
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage


def rebuild_alpha(img: Image.Image, checker_colors=(255, 208), neutral_tol: int = 4, color_tol: int = 12) -> Image.Image:
    """Gemini bakes a neutral-grey checkerboard (R==G==B) into the RGB data to
    simulate transparency. The object's own paint always has a slight color
    tint (never perfectly neutral), so classify background by "is this pixel
    both neutral AND close to one of the two known checker shades"."""
    arr = np.array(img.convert("RGBA"))
    rgb = arr[:, :, :3].astype(np.int16)
    maxc = rgb.max(axis=2)
    minc = rgb.min(axis=2)
    neutral = (maxc - minc) <= neutral_tol
    gray = rgb.mean(axis=2)
    near_checker = np.zeros(gray.shape, dtype=bool)
    for c in checker_colors:
        near_checker |= np.abs(gray - c) <= color_tol
    bg_mask = neutral & near_checker

    alpha = np.where(bg_mask, 0, 255).astype(np.uint8)
    alpha_f = ndimage.gaussian_filter(alpha.astype(np.float32), sigma=0.6)
    arr[:, :, 3] = np.clip(alpha_f, 0, 255).astype(np.uint8)
    return Image.fromarray(arr, "RGBA")


def crop_to_content(img: Image.Image, alpha_thresh: int = 100) -> Image.Image:
    arr = np.array(img)
    mask = arr[:, :, 3] > alpha_thresh
    if not mask.any():
        return img
    ys, xs = np.where(mask)
    top, bottom = ys.min(), ys.max() + 1
    left, right = xs.min(), xs.max() + 1
    return img.crop((left, top, right, bottom))


def fit_to_canvas(img: Image.Image, target_w: int, target_h: int) -> Image.Image:
    src_w, src_h = img.size
    scale = min(target_w / src_w, target_h / src_h)
    new_w = max(1, round(src_w * scale))
    new_h = max(1, round(src_h * scale))
    resized = img.resize((new_w, new_h), Image.LANCZOS)

    canvas = Image.new("RGBA", (target_w, target_h), (0, 0, 0, 0))
    off_x = (target_w - new_w) // 2
    off_y = (target_h - new_h) // 2
    canvas.paste(resized, (off_x, off_y), resized)
    return canvas


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--in", dest="in_path", required=True)
    ap.add_argument("--out", dest="out_path", required=True)
    ap.add_argument("--width", type=int, required=True)
    ap.add_argument("--height", type=int, required=True)
    args = ap.parse_args()

    src = Image.open(args.in_path)
    print(f"input: {src.size}, mode={src.mode}")

    with_alpha = rebuild_alpha(src)
    cropped = crop_to_content(with_alpha)
    print(f"cropped to content: {cropped.size}")

    final = fit_to_canvas(cropped, args.width, args.height)
    out_path = Path(args.out_path)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    final.save(out_path)
    print(f"saved: {out_path} ({final.size})")


if __name__ == "__main__":
    main()
