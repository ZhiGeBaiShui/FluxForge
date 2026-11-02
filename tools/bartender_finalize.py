# -*- coding: utf-8 -*-
"""把抠好的立绘裁成统一半身构图，并导出 HD / 游戏尺寸。"""
import os
import numpy as np
from PIL import Image

CUT = r"E:/code/FluxForge/generated-images/bartender/cut"
OUT = r"E:/code/FluxForge/generated-images/bartender/final"
os.makedirs(OUT, exist_ok=True)


def frame(im, head_ratio=0.42, head_top_pad=0.02):
    """按“头宽占画面宽 head_ratio”统一切构图。"""
    a = np.array(im)[..., 3]
    ys, xs = np.nonzero(a > 8)
    top, bottom = ys.min(), ys.max()
    bh = bottom - top + 1

    band = (ys >= top + 0.03 * bh) & (ys < top + 0.30 * bh)
    head_cx = xs[band].mean()

    widths = []
    for y in range(int(top + 0.03 * bh), int(top + 0.30 * bh), 4):
        row = np.flatnonzero(a[y] > 8)
        if row.size:
            widths.append(row[-1] - row[0])
    head_w = float(np.median(widths))

    side = int(head_w / head_ratio)
    cx = int(head_cx)
    left_new = cx - side // 2
    top_new = top - int(side * head_top_pad)

    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    canvas.paste(im, (-left_new, -top_new), im)
    return canvas


def main():
    files = {"v6_danger": "v6_danger.png", "v9_warm": "v9_warm.png",
             "v10_allure": "v10_allure.png", "v11_final": "v11_final.png"}
    for key, f in files.items():
        im = Image.open(os.path.join(CUT, f)).convert("RGBA")
        sq = frame(im)
        sq.resize((1024, 1024), Image.LANCZOS).save(os.path.join(OUT, f"{key}_hd.png"))
        sq.resize((512, 512), Image.LANCZOS).save(os.path.join(OUT, f"{key}_512.png"))
        sq.resize((160, 160), Image.LANCZOS).save(os.path.join(OUT, f"{key}_160.png"))
        print(key, sq.size, "-> 1024/512/160")


if __name__ == "__main__":
    main()
