#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""palette_preview.py - 生成液体颜色预览图。

模拟 C# FlowVisualLayer.ComputeBodyColor 的最终逻辑：
  - 每种液体用自己的原版配色（Liquid.InitLiquid 的 color 字段）
  - 加权公式对齐原版 WaterHelper.GetLiquidContainerColor：
      weight = amount × max(1, colorMultiplier)，Σ(rgb×w)/Σw，截断取整
  - 调色板本身统一提过饱和度（S × 2.85，L < 0.55 时向 0.55 靠 30%）
  - 混合时不同色相加权平均自然去饱和，无运行时调制
"""
import math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent.parent
OUT = ROOT / "docs/flow-visual/palette_final.png"

FONT_R = "C:/Windows/Fonts/msyh.ttc"
FONT_B = "C:/Windows/Fonts/msyhbd.ttc"

# ── 36 种液体原版配色（与 FlowLiquidPalette.cs Colors 同步）──
COLORS = {
    "water":                  (0x2B, 0xE8, 0xDA),
    "heavy_metal":            (0x4E, 0x55, 0x6B),
    "organic_waste":          (0xEA, 0x3B, 0x07),
    "microplastic":           (0x4E, 0x55, 0x6B),
    "microbe":                (0x00, 0xDA, 0x06),
    "chemical_contaminant":   (0x71, 0xFF, 0x0A),
    "physical_contaminant":   (0x4E, 0x55, 0x6B),
    "mineral":                (0x4E, 0x55, 0x6B),
    "dream_cap_mush":         (0xEA, 0x3B, 0x07),
    "dream_cap_extract":      (0xFF, 0x81, 0x21),
    "plant_matter":           (0x75, 0x8F, 0x27),
    "alcohol":                (0xFF, 0xFF, 0xFF),
    "caffeine":               (0xC1, 0x2E, 0x1E),
    "protein":                (0x2B, 0xE8, 0xDA),
    "fat":                    (0x2B, 0xE8, 0xDA),
    "starch":                 (0x2B, 0xE8, 0xDA),
    "sugar":                  (0x2B, 0xE8, 0xDA),
    "coffee_mush":            (0x8A, 0x1F, 0x43),
    "contraceptive":          (0x2B, 0xE8, 0xDA),
    "zyanide":                (0x00, 0xAC, 0xA4),
    "nightmare_extract":      (0xE8, 0x00, 0x02),
    "nightmare_cap_mush":     (0xC8, 0x00, 0x1E),
    "chlorine":               (0x9E, 0xF8, 0x06),
    "synaprest":              (0x8C, 0x7C, 0x6A),
    "fanta":                  (0xC5, 0x3C, 0xE2),
    "methylol":               (0x00, 0x61, 0xC8),
    "reducing_agent":         (0x2B, 0xE8, 0xDA),
    "acid":                   (0x71, 0xFF, 0x0A),
    "base":                   (0x2B, 0xE8, 0xDA),
    "stabilizing_agent":      (0x59, 0xF0, 0xE5),
    "debris":                 (0x4E, 0x55, 0x6B),
    "lipobond_agent":         (0xFF, 0x61, 0x22),
    "livestock_steroid":      (0xFF, 0xCE, 0x08),
    "livestock_immunity":     (0x08, 0xCE, 0xFF),
    "wine_extract":           (0xD6, 0x19, 0xE2),
    "nomore":                 (0xFF, 0x66, 0xB3),
}

# ── 稀有度（与 FlowLiquidPalette.cs Rarity 同步）──
# ── 染色权重（与 FlowLiquidPalette.cs ColorMultiplier 同步）──
# 原版 Liquid.colorMultiplier：36 种里只有 6 个 ≠ 1。
# nightmare_cap_mush 按原版运行值是 1（源码里 liquid21 被赋值两次的 bug）。
MULTIPLIER = {
    "wine_extract": 150,
    "organic_waste": 7,
    "chemical_contaminant": 6,
    "heavy_metal": 4,
    "microbe": 4,
    "nightmare_extract": 4,
}

DEFAULT = (0x2B, 0xE8, 0xDA)

# 中文短名：图例用，避免长 id 挤在一起
CN = {
    "water": "水", "heavy_metal": "重金属", "organic_waste": "有机废物",
    "microplastic": "微塑料", "microbe": "微生物", "chemical_contaminant": "化学污染",
    "physical_contaminant": "物理污染", "mineral": "矿物", "dream_cap_mush": "梦帽菇",
    "dream_cap_extract": "梦帽提取", "plant_matter": "植物纤维", "alcohol": "酒精",
    "caffeine": "咖啡因", "protein": "蛋白质", "fat": "脂肪", "starch": "淀粉",
    "sugar": "糖", "coffee_mush": "咖啡菇", "contraceptive": "避孕药",
    "zyanide": "氰化物", "nightmare_extract": "梦魇提取", "nightmare_cap_mush": "梦魇菇",
    "chlorine": "氯", "synaprest": "镇静剂", "fanta": "芬达", "methylol": "甲醇",
    "reducing_agent": "还原剂", "acid": "酸", "base": "碱",
    "stabilizing_agent": "稳定剂", "debris": "碎屑", "lipobond_agent": "脂质结合剂",
    "livestock_steroid": "类固醇", "livestock_immunity": "免疫剂",
    "wine_extract": "酒提取", "nomore": "诺莫尔",
}


def compute_body_color(liquids):
    """与 C# ComputeBodyColor 逐行对齐。

    加权公式对齐原版 WaterHelper.GetLiquidContainerColor：
      weight = amount × max(1, colorMultiplier)
      color  = Σ(rgb × weight) / Σweight，截断取整
    调色板本身已统一提过饱和度（见 COLORS 表），混合时不同色相
    按加权平均自然去饱和——不需要任何运行时调制。
    """
    wr = wg = wb = wt = 0.0

    for lid, amount in liquids.items():
        if amount <= 0:
            continue

        color = COLORS.get(lid, DEFAULT)
        weight = amount * max(1, MULTIPLIER.get(lid, 1))
        wr += color[0] * weight
        wg += color[1] * weight
        wb += color[2] * weight
        wt += weight

    if wt <= 0:
        return DEFAULT

    # 原版是 Color → Color32 的隐式转换，向零截断，不是四舍五入
    return (int(wr / wt), int(wg / wt), int(wb / wt))


def make_preview():
    # 覆盖：每种液体单独 + 常见混合 + 复合饮料
    test_cases = [
        # ── 单质 ──
        ("pure_water",          "纯水",           {"water": 100}),
        ("pure_alcohol",        "纯酒精",         {"alcohol": 100}),
        ("pure_zyanide",        "纯氰化物",       {"zyanide": 100}),
        ("pure_fanta",          "纯芬达",         {"fanta": 100}),
        ("pure_nightmare",      "纯梦魇提取",     {"nightmare_extract": 100}),
        ("pure_methylol",       "纯甲醇",         {"methylol": 100}),
        ("pure_acid",           "纯酸",           {"acid": 100}),
        ("pure_steroid",        "纯类固醇",       {"livestock_steroid": 100}),
        ("pure_wine_extract",   "纯酒提取",       {"wine_extract": 100}),
        ("pure_plant",          "纯植物纤维",     {"plant_matter": 100}),
        ("pure_heavy_metal",    "纯重金属",       {"heavy_metal": 100}),
        ("pure_coffee_mush",    "纯咖啡菇",       {"coffee_mush": 100}),
        # ── 稀释 ──
        ("water_zyanide_1%",    "水+1%氰",        {"water": 99, "zyanide": 1}),
        ("water_zyanide_10%",   "水+10%氰",       {"water": 90, "zyanide": 10}),
        ("water_zyanide_50%",   "水+50%氰",       {"water": 50, "zyanide": 50}),
        ("water_wine_1%",       "水+1%酒提取",    {"water": 99, "wine_extract": 1}),
        ("water_wine_10%",      "水+10%酒提取",   {"water": 90, "wine_extract": 10}),
        ("water_metal_20%",     "水+20%重金属",   {"water": 80, "heavy_metal": 20}),
        ("water_waste_20%",     "水+20%有机废物", {"water": 80, "organic_waste": 20}),
        ("water_chem_20%",      "水+20%化学污染", {"water": 80, "chemical_contaminant": 20}),
        ("water_nightmare_20%", "水+20%梦魇提取", {"water": 80, "nightmare_extract": 20}),
        ("water_ncap_20%",      "水+20%梦魇菇",   {"water": 80, "nightmare_cap_mush": 20}),
        ("water_fanta_10%",     "水+10%芬达",     {"water": 90, "fanta": 10}),
        ("water_alcohol_40%",   "水+40%酒精",     {"water": 60, "alcohol": 40}),
        ("water_methylol_20%",  "水+20%甲醇",     {"water": 80, "methylol": 20}),
        ("water_steroid_20%",   "水+20%类固醇",   {"water": 80, "livestock_steroid": 20}),
        # ── 常见饮品 ──
        ("beer",                "啤酒",           {"water": 60, "alcohol": 20, "sugar": 10, "protein": 10}),
        ("wine",                "葡萄酒",         {"water": 50, "alcohol": 25, "sugar": 20, "wine_extract": 5}),
        ("spirit",              "烈酒",           {"alcohol": 70, "water": 30}),
        ("coffee",              "咖啡",           {"water": 80, "caffeine": 15, "sugar": 5}),
        ("soda",                "汽水",           {"water": 70, "sugar": 20, "fanta": 10}),
        # ── 污染 / 复合 ──
        ("dirty_water",         "脏水",           {"water": 50, "microbe": 15, "heavy_metal": 15, "debris": 20}),
        ("sludge",              "污泥",           {"water": 40, "organic_waste": 30, "microplastic": 30}),
        ("acid_bath",           "酸浴",           {"water": 50, "acid": 30, "chlorine": 20}),
        ("medicine_mix",        "药剂",           {"water": 60, "synaprest": 20, "contraceptive": 20}),
    ]

    cols = 7
    tile_size = 68
    gap = 16
    label_h = 34
    sublabel_h = 18
    pad = 28
    header_h = 68
    rows = (len(test_cases) + cols - 1) // cols

    cell_total = tile_size + label_h + sublabel_h
    width = pad * 2 + cols * (tile_size + gap) - gap
    height = pad + header_h + rows * (cell_total + gap) + pad + 400

    im = Image.new("RGBA", (width, height), (0x1B, 0x17, 0x15, 255))
    d = ImageDraw.Draw(im)

    try:
        font_title = ImageFont.truetype(FONT_B, 21)
        font_r = ImageFont.truetype(FONT_R, 12)
        font_s = ImageFont.truetype(FONT_R, 9)
    except Exception:
        font_title = ImageFont.load_default()
        font_r = ImageFont.load_default()
        font_s = ImageFont.load_default()

    d.text((pad, pad), "液体混合色（完全对齐原版 GetLiquidContainerColor）", font=font_title, fill=(0xE8, 0xE2, 0xD8, 255))
    d.text((pad, pad + 30), "weight = 存量 x max(1, colorMultiplier) · 无含量阈值 · 无水权重压制 · 截断取整",
           font=font_r, fill=(0xA8, 0xA0, 0x94, 255))

    for i, (key, label, liquids) in enumerate(test_cases):
        col = i % cols
        row = i // cols
        x = pad + col * (tile_size + gap)
        y = pad + header_h + row * (cell_total + gap)

        color = compute_body_color(liquids)
        d.rounded_rectangle([x + 3, y + 3, x + tile_size - 3, y + tile_size - 3],
                            radius=7, fill=color + (255,))
        d.text((x + tile_size // 2, y + tile_size + 4), label,
               font=font_r, fill=(0xE8, 0xE2, 0xD8, 255), anchor="ma")
        d.text((x + tile_size // 2, y + tile_size + 19), "#%02X%02X%02X" % color,
               font=font_s, fill=(0x8A, 0x84, 0x7A, 255), anchor="ma")

    # 底部：36 种原版色一览
    y_base = pad + header_h + rows * (cell_total + gap) + 14
    d.line([(pad, y_base), (width - pad, y_base)], fill=(0x2A, 0x23, 0x1F, 255))
    y_base += 10
    d.text((pad, y_base), "36 种液体原版配色（游戏 Liquid.InitLiquid）",
           font=font_r, fill=(0xA8, 0xA0, 0x94, 255))
    y_base += 22

    names = list(COLORS.keys())
    per_row = 6
    sw = (width - pad * 2) // per_row
    for j, n in enumerate(names):
        r = j // per_row
        c = j % per_row
        cx = pad + c * sw
        cy = y_base + r * 34
        d.rectangle([cx, cy, cx + 34, cy + 16], fill=COLORS[n] + (255,))
        d.text((cx + 40, cy + 2), CN.get(n, n), font=font_r, fill=(0xC8, 0xC0, 0xB4, 255))

    OUT.parent.mkdir(parents=True, exist_ok=True)
    im.convert("RGB").save(OUT, format="PNG")
    print("Saved: %s" % OUT)
    return OUT


if __name__ == "__main__":
    make_preview()
