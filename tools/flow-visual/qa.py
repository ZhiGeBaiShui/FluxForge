"""视觉 QA 工具：色盲模拟、灰度、接触表、盲测（隐藏标签 + 答案键）。

用法：
    python qa.py colorblind --in a.png --out b.png --kind deutan
    python qa.py sheet --manifest tiles.json --out sheet.png --cols 6 --scale 4
    python qa.py blind --manifest tiles.json --out blind.png --key key.json --cols 6 --scale 4
    python qa.py contrast --colors 2FE0C8,B47CFF --bg 1B1715

manifest 格式：[{"path": "...", "answer": "water", "meta": "cold_high"}, ...]
"""
import argparse
import json
import random
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

# Machado et al. (2009) 线性 RGB 色觉缺陷矩阵，severity = 1.0
CB_MATRICES = {
    "deutan": np.array([[0.367, 0.861, -0.228],
                        [0.280, 0.673, 0.047],
                        [-0.012, 0.043, 0.969]], dtype=float),
    "protan": np.array([[0.152, 1.053, -0.205],
                        [0.115, 0.786, 0.099],
                        [-0.004, -0.048, 1.052]], dtype=float),
    "tritan": np.array([[1.256, -0.078, -0.178],
                        [-0.078, 0.931, 0.148],
                        [0.005, 0.691, 0.304]], dtype=float),
}


def srgb_to_linear(c):
    c = np.asarray(c, dtype=float) / 255.0
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def linear_to_srgb(c):
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * (c ** (1 / 2.4)) - 0.055) * 255.0


def simulate_rgb(rgb, kind):
    lin = srgb_to_linear(np.asarray(rgb, dtype=float)).reshape(3)
    out = np.clip(CB_MATRICES[kind] @ lin, 0.0, 1.0)
    return np.round(linear_to_srgb(out)).astype(np.uint8)


def simulate_image(img, kind):
    a = np.asarray(img.convert("RGB")).astype(float)
    lin = srgb_to_linear(a)
    h, w, _ = lin.shape
    flat = lin.reshape(-1, 3)
    out = (CB_MATRICES[kind] @ flat.T).T.reshape(h, w, 3)
    return Image.fromarray(np.round(linear_to_srgb(out)).astype(np.uint8), "RGB")


def to_gray(img):
    return img.convert("L").convert("RGB")


def wcag_luminance(rgb):
    lin = srgb_to_linear(rgb)
    return float(0.2126 * lin[0] + 0.7152 * lin[1] + 0.0722 * lin[2])


def contrast(rgb_a, rgb_b):
    la, lb = wcag_luminance(rgb_a), wcag_luminance(rgb_b)
    hi, lo = max(la, lb), min(la, lb)
    return (hi + 0.05) / (lo + 0.05)


def resolve_item(p, base):
    """manifest 里的路径可能是相对仓库根或相对 manifest 目录，按顺序找。"""
    path = Path(p)
    if path.is_absolute():
        return path
    for c in (base / path, Path.cwd() / path, base.parent.parent / path):
        if c.exists():
            return c
    return base / path


def upscale(img, factor):
    return img.resize((img.width * factor, img.height * factor), Image.NEAREST)


def build_sheet(tiles, cols=6, gap=4, bg=(25, 21, 20), scale=4, labels=None, label_h=10):
    """tiles: list[PIL.Image]（同尺寸小图）。返回拼好的接触表。"""
    if not tiles:
        raise ValueError("no tiles")
    w, h = tiles[0].size
    rows = (len(tiles) + cols - 1) // cols
    head = label_h if labels else 0
    sheet = Image.new("RGB", (cols * (w * scale + gap) + gap,
                              rows * (h * scale + gap + head) + gap), bg)
    draw = ImageDraw.Draw(sheet)
    for i, t in enumerate(tiles):
        r, c = divmod(i, cols)
        x = gap + c * (w * scale + gap)
        y = gap + r * (h * scale + gap + head)
        sheet.paste(upscale(t, scale), (x, y + head))
        if labels:
            draw.text((x, y + 1), labels[i], fill=(220, 210, 200))
    return sheet


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)

    cb = sub.add_parser("colorblind")
    cb.add_argument("--in", dest="src", required=True)
    cb.add_argument("--out", dest="dst", required=True)
    cb.add_argument("--kind", choices=list(CB_MATRICES), default="deutan")
    cb.add_argument("--scale", type=int, default=1)

    sh = sub.add_parser("sheet")
    sh.add_argument("--manifest", required=True)
    sh.add_argument("--out", required=True)
    sh.add_argument("--cols", type=int, default=6)
    sh.add_argument("--scale", type=int, default=4)

    bl = sub.add_parser("blind")
    bl.add_argument("--manifest", required=True)
    bl.add_argument("--out", required=True)
    bl.add_argument("--key", required=True)
    bl.add_argument("--cols", type=int, default=6)
    bl.add_argument("--scale", type=int, default=4)
    bl.add_argument("--seed", type=int, default=7)

    ct = sub.add_parser("contrast")
    ct.add_argument("--colors", required=True, help="逗号分隔 hex，例如 2FE0C8,B47CFF")
    ct.add_argument("--bg", default="1B1715")

    args = ap.parse_args()

    if args.cmd == "colorblind":
        img = Image.open(args.src).convert("RGB")
        out = simulate_image(img, args.kind)
        if args.scale > 1:
            out = upscale(out, args.scale)
        out.save(args.dst)
        print(f"saved {args.dst} ({args.kind})")

    elif args.cmd == "sheet":
        items = json.loads(Path(args.manifest).read_text(encoding="utf-8"))
        base = Path(args.manifest).parent
        tiles = [Image.open(resolve_item(i["path"], base)).convert("RGB") for i in items]
        labels = [i.get("label", "") for i in items] or None
        build_sheet(tiles, args.cols, scale=args.scale, labels=labels).save(args.out)
        print(f"saved {args.out} ({len(tiles)} tiles)")

    elif args.cmd == "blind":
        items = json.loads(Path(args.manifest).read_text(encoding="utf-8"))
        order = list(range(len(items)))
        random.Random(args.seed).shuffle(order)
        base = Path(args.manifest).parent
        tiles = [Image.open(resolve_item(items[i]["path"], base)).convert("RGBA") for i in order]
        # 每格：24×28 画布，顶部 9px 只放编号，tile 放 (4,10)，编号绝不压图
        numbered = []
        for n, t in enumerate(tiles, 1):
            cell = Image.new("RGBA", (24, 28), (25, 21, 20, 255))
            cell.alpha_composite(t, (4, 10))
            d = ImageDraw.Draw(cell)
            d.rectangle([0, 0, 23, 8], fill=(20, 17, 16, 255))
            d.text((2, 1), str(n), fill=(240, 230, 220, 255))
            d.rectangle([3, 9, 20, 26], outline=(58, 50, 45, 255))
            numbered.append(cell.convert("RGB"))
        build_sheet(numbered, args.cols, scale=args.scale).save(args.out)
        key = {str(n): {"answer": items[order[n - 1]].get("answer"),
                        "meta": items[order[n - 1]].get("meta"),
                        "path": items[order[n - 1]]["path"]}
               for n in range(1, len(items) + 1)}
        Path(args.key).write_text(json.dumps(key, ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"saved {args.out} + {args.key} ({len(items)} tiles)")

    elif args.cmd == "contrast":
        cols = [tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) for h in args.colors.split(",")]
        bg = tuple(int(args.bg[i:i + 2], 16) for i in (0, 2, 4))
        print("bg", bg)
        for c in cols:
            d = simulate_rgb(c, "deutan")
            p = simulate_rgb(c, "protan")
            print(f"#{c[0]:02X}{c[1]:02X}{c[2]:02X}  L={wcag_luminance(c):.4f}  "
                  f"contrast={contrast(c, bg):.2f}  deutan=L{wcag_luminance(d):.3f}  "
                  f"protan=L{wcag_luminance(p):.3f}")


if __name__ == "__main__":
    main()

