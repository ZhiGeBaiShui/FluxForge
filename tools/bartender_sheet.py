# -*- coding: utf-8 -*-
"""生成对比图：原版上层区角色 vs 新设计稿。"""
import os
from PIL import Image, ImageDraw, ImageFont

REF = r"C:\Users\Administrator\AppData\Roaming\Tencent\TIM\Temp\b3ad03b7a9559f2a8128690291dea419.png"
FIN = r"E:/code/FluxForge/generated-images/bartender/final"
OUT = r"E:/code/FluxForge/generated-images/bartender/bartender_compare.png"

CELL = 200
GAME = 160
PAD = 16
BG = (24, 24, 30, 255)
TXT = (236, 230, 218, 255)
SUB = (146, 142, 136, 255)
ACCENT = (198, 158, 92, 255)


def font(size, bold=False):
    for name in ("msyhbd.ttc" if bold else "msyh.ttc", "msyh.ttf", "simhei.ttf"):
        p = os.path.join(r"C:\Windows\Fonts", name)
        if os.path.exists(p):
            try:
                return ImageFont.truetype(p, size)
            except Exception:
                pass
    return ImageFont.load_default()


def fit(im, size):
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    r = min(size / im.width, size / im.height)
    w, h = max(1, int(im.width * r)), max(1, int(im.height * r))
    im = im.resize((w, h), Image.LANCZOS)
    canvas.alpha_composite(im, ((size - w) // 2, (size - h) // 2))
    return canvas


def main():
    ref = Image.open(REF).convert("RGBA")
    top = [(64, 223), (354, 511), (640, 800), (928, 1087), (1224, 1376), (1512, 1663)]
    orig = [ref.crop((a - 4, 44, b + 4, 214)) for a, b in top]

    picks = [
        ("v11_final_512.png", "主推 · 微醺"),
        ("v6_danger_512.png", "冷艳 · 危险"),
        ("v9_warm_512.png", "知性 · 温润"),
        ("v10_allure_512.png", "慵懒 · 诱惑"),
    ]
    news = [(Image.open(os.path.join(FIN, f)).convert("RGBA"), t) for f, t in picks]

    cols = 6
    W = PAD * 2 + cols * (CELL + PAD)
    h_title = 40
    h_sub = 30
    H = PAD + h_title + h_sub + CELL + 20 + h_sub + CELL + 20 + h_sub + GAME + PAD
    sheet = Image.new("RGBA", (W, H), BG)
    d = ImageDraw.Draw(sheet)
    f_title = font(24, True)
    f_sub = font(15)

    y = PAD
    d.text((PAD, y), "上层区调酒师 · 立绘设计稿", font=f_title, fill=TXT)
    d.text((PAD + 300, y + 8), "Upper-District Bartender / NPC Portrait", font=f_sub, fill=SUB)
    y += h_title

    d.text((PAD, y), "原版上层区角色（游戏内比例）", font=f_sub, fill=ACCENT)
    y += h_sub
    for i, im in enumerate(orig):
        sheet.alpha_composite(fit(im, CELL), (PAD + i * (CELL + PAD), y))
    y += CELL + 20

    d.text((PAD, y), "新设计稿（同构图 / 同画布）", font=f_sub, fill=ACCENT)
    y += h_sub
    for i, (im, label) in enumerate(news):
        x = PAD + i * (CELL + PAD)
        sheet.alpha_composite(fit(im, CELL), (x, y))
        d.text((x + 2, y + CELL - 26), label, font=f_sub, fill=SUB)
    y += CELL + 20

    d.text((PAD, y), "游戏内实际尺寸 160×160", font=f_sub, fill=ACCENT)
    y += h_sub
    for i, (im, _) in enumerate(news):
        x = PAD + i * (CELL + PAD)
        sheet.alpha_composite(fit(im, GAME), (x, y + (GAME - GAME) // 2))
    for i, im in enumerate(orig[:2]):
        x = PAD + (i + 4) * (CELL + PAD)
        sheet.alpha_composite(fit(im, GAME), (x, y))

    sheet.convert("RGB").save(OUT, quality=95)
    print("saved", OUT, sheet.size)


if __name__ == "__main__":
    main()
