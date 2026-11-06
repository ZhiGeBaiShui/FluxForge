#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""liquid_preview.py - 36 种液体 + 常见混合的流源贴图预览。

每格用真实图集蒙版（large 档）+ 运行时染色公式合成：
  body   = 本体色
  edge   = shade(body, -0.52)     # 每通道 × 0.48
  accent = mix(body, white, 0.62)
与 FlowTint.Body / Edge / Accent 逐式一致。
"""
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent.parent
ASSETS = ROOT / "assets"
OUT = ROOT / "docs/flow-visual/liquid_preview.png"

FONT_R = "C:/Windows/Fonts/msyh.ttc"
FONT_B = "C:/Windows/Fonts/msyhbd.ttc"

sys.path.insert(0, str(ROOT / "tools/flow-visual"))
from palette_preview import COLORS, CN, MULTIPLIER, DEFAULT, compute_body_color  # noqa: E402

BG = (0x1E, 0x1A, 0x16, 255)
CARD = (0x2A, 0x26, 0x22, 255)
TEXT = (0xE8, 0xE2, 0xD8, 255)
DIM = (0x9A, 0x93, 0x89, 255)
ACCENT_LINE = (0x3D, 0x37, 0x31, 255)


def shade(color, factor):
    return tuple(int(round(color[i] * (1.0 - factor))) for i in range(3))


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def load_masks():
    idx = json.load(open(ASSETS / "flow_mask.json", encoding="utf-8"))
    tiles = idx.get("tiles", idx)
    layers = {
        "body": Image.open(ASSETS / "flow_mask_body.png").convert("RGBA"),
        "edge": Image.open(ASSETS / "flow_mask_edge.png").convert("RGBA"),
        "accent": Image.open(ASSETS / "flow_mask_accent.png").convert("RGBA"),
    }
    return tiles, layers


def tint_tile(tiles, layers, content, body, scale):
    out = Image.new("RGBA", (16, 16), CARD)
    tints = {
        "body": body,
        "edge": shade(body, 0.52),
        "accent": mix(body, (255, 255, 255), 0.62),
    }
    for name, im in layers.items():
        x, y = tiles[f"{content}|flow|right"]
        tile = im.crop((x, y, x + 16, y + 16))
        px = tile.load()
        c = tints[name]
        for yy in range(16):
            for xx in range(16):
                r, g, b, a = px[xx, yy]
                if a > 0:
                    px[xx, yy] = (c[0], c[1], c[2], a)
        out = Image.alpha_composite(out, tile)
    return out.resize((16 * scale, 16 * scale), Image.NEAREST)


def main():
    tiles, layers = load_masks()
    font_title = ImageFont.truetype(FONT_B, 26)
    font_name = ImageFont.truetype(FONT_R, 15)
    font_hex = ImageFont.truetype(FONT_R, 12)

    scale = 4
    tile_px = 16 * scale
    cols = 6
    cell_w = tile_px + 96
    cell_h = tile_px + 26
    pad = 26
    header_h = 62

    pure = list(COLORS.keys())
    mixed = [
        ("纯水", {"water": 100}),
        ("水+1%氰", {"water": 99, "zyanide": 1}),
        ("水+10%氰", {"water": 90, "zyanide": 10}),
        ("水+1%酒提", {"water": 99, "wine_extract": 1}),
        ("水+20%金属", {"water": 80, "heavy_metal": 20}),
        ("水+20%化学", {"water": 80, "chemical_contaminant": 20}),
        ("水+40%酒精", {"water": 60, "alcohol": 40}),
        ("啤酒", {"water": 60, "alcohol": 20, "sugar": 10, "protein": 10}),
        ("咖啡", {"water": 80, "caffeine": 15, "sugar": 5}),
        ("脏水", {"water": 50, "microbe": 15, "heavy_metal": 15, "debris": 20}),
        ("污泥", {"water": 40, "organic_waste": 30, "microplastic": 30}),
        ("药剂", {"water": 60, "synaprest": 20, "contraceptive": 20}),
    ]

    pure_rows = (len(pure) + cols - 1) // cols
    mixed_rows = (len(mixed) + cols - 1) // cols
    section_gap = 44

    width = pad * 2 + cols * cell_w
    height = (pad + header_h + pure_rows * cell_h + section_gap
              + 30 + mixed_rows * cell_h + pad)

    canvas = Image.new("RGBA", (width, height), BG)
    d = ImageDraw.Draw(canvas)

    d.text((pad, pad - 4), "流源液体配色预览", font=font_title, fill=TEXT)
    d.text((pad, pad + 28), "36 种液体单质（上） + 常见混合（下） · large 档 12×8 水滴 · 按原版 colorMultiplier 加权",
           font=font_hex, fill=DIM)

    y = pad + header_h
    for i, lid in enumerate(pure):
        r, c = i // cols, i % cols
        cx = pad + c * cell_w
        cy = y + r * cell_h
        body = COLORS[lid]
        tile = tint_tile(tiles, layers, "large", body, scale)
        canvas.paste(tile, (cx, cy))
        d.text((cx + tile_px + 8, cy + 6), CN.get(lid, lid), font=font_name, fill=TEXT)
        d.text((cx + tile_px + 8, cy + 26), "#%02X%02X%02X" % body, font=font_hex, fill=DIM)

    y += pure_rows * cell_h + section_gap
    d.line([(pad, y - 22), (width - pad, y - 22)], fill=ACCENT_LINE, width=1)
    d.text((pad, y - 18), "常见混合", font=font_name, fill=TEXT)
    y += 16

    for i, (label, liquids) in enumerate(mixed):
        r, c = i // cols, i % cols
        cx = pad + c * cell_w
        cy = y + r * cell_h
        body = compute_body_color(liquids)
        tile = tint_tile(tiles, layers, "large", body, scale)
        canvas.paste(tile, (cx, cy))
        d.text((cx + tile_px + 8, cy + 6), label, font=font_name, fill=TEXT)
        d.text((cx + tile_px + 8, cy + 26), "#%02X%02X%02X" % body, font=font_hex, fill=DIM)

    canvas.convert("RGB").save(OUT, format="PNG")
    print("saved", OUT, canvas.size)


if __name__ == "__main__":
    main()
