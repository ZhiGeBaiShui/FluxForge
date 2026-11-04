#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""build_mask_atlas.py — 把终稿视觉烘成 3 张亮度蒙版 + 1 张墙（运行时染色用）。

契约见 docs/flow-visual/final/REFACTOR-A.md §2：
- key = `content|state|dir`，flow 追加 `|f0..f11`；
  每个 (content,state,dir) 保留 1 个静态槽 + 12 个帧槽 = 13 个槽位，
  4 含量 × 5 状态 × 4 向 × 13 = 1040 个 key（flow 13 个变体都不同；
  非 flow 的 12 个帧槽是静态图的重复，运行时不会请求，只为维持统一布局）。
- 每张 256×1040（每行 16 个 tile），tile 16×16 RGBA。
- RGB = 亮度蒙版（染色时相乘），A = 形状 alpha。
- distinct 自检：flow 右向 13 槽（静态 + f0..f11）对 large 要求 13/13；
  small 只做 ±1px 挤压，12 帧为 9 种不同形态（要求 > 1，防止退回静态重复）；
  mid 由边缘行抖动产出 9 种，empty 由单行双次平移产出 5 种。
- `flow_mask_wall.png` 只画 `W`（固定 #C9C2B8），其它像素全透明。
- 索引 {"tiles": {"<key>": [x, y]}}，UTF-8 无 BOM，key 全小写。

像素来源只有一个：`render_final.build_grid()`（形状/角色）+ REFACTOR-A §2 的角色表
（亮度/alpha）。本脚本不重画任何形状，也不改 render_final.py 的一个字节。

烘焙基准：water + normal。蒙版没有 comp/temp 维度：
- 成分只影响颜色（X/Y/Z/K/N 纹理在 A 方案删除，保留形状 alpha、亮度并入 accent）；
- 温度只影响运行时 edge 色，形状按 normal 烘焙（REFACTOR-A §3）。
墙 `W` 只出现在 blocked / large。**墙图已经带 #C9C2B8，运行时墙层 tint 必须是白**
（若再乘 WallColor 会二次变暗，实测 Δ≈51）。

颜色保真（与旧彩色 tile 的固有差异，REFACTOR-A §7 已接受）：
- accent = mix(body, white, 0.62)：与旧图 C 角色逐字节一致；同族的 S(0.60)、G(0.72)、
  ~(0.55) 都落在这条白化曲线上，油/酸的暖亮核不会再被固定近白 #F2FAFF 染成冷色。
- S（汇聚蒸汽）与 ~（抽吸流）旧版误归 body（亮 255）→ 染色后 = 本体色，与旧图
  Δmax=130/119；本版迁到 accent 层（alpha 不变），Δ 降到个位数/十几。
- edge 光扫 s/S2、纹理位 X/Y/Z/K/N 仍被压成单层色（A 方案删除成分纹理）。
- alpha 不受影响：4 层并集与旧 lay 逐像素 100% 相等（20 key 与全量 272 key）。

确定性：同一份输入两次运行输出字节相同
（PNG 不写时间戳类元数据；JSON 按固定枚举顺序插入）。

用法：
    python tools/flow-visual/build_mask_atlas.py
"""
import hashlib
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent.parent
FINAL = ROOT / "docs/flow-visual/final"
ASSETS = ROOT / "assets"
SAMPLE = FINAL / "png/mask-sample.png"

sys.path.insert(0, str(FINAL))
sys.dont_write_bytecode = True  # 不在 final/ 下留 __pycache__（只写自己名下的文件）
import render_final as rf  # noqa: E402

TILE = 16
PER_ROW = 16
FRAME_COUNT = 12
VARIANTS = 1 + FRAME_COUNT  # 静态 + f0..f11

# 枚举顺序 = REFACTOR-A §2 的书写顺序；索引插入序由此固定，必须稳定。
CONTENT = ("empty", "small", "mid", "large")
STATES = ("flow", "blocked", "split", "merge", "collect")
DIRS = ("up", "right", "down", "left")

TOTAL = len(CONTENT) * len(STATES) * len(DIRS) * VARIANTS
ROWS = (TOTAL + PER_ROW - 1) // PER_ROW

BAKE_COMP = "water"
BAKE_TEMP = "normal"
WALL_COLOR = (0xC9, 0xC2, 0xB8)

# 与渲染器对表：枚举字符串必须完全一致，避免两边各写一份而悄悄漂移。
assert tuple(rf.CONTENT) == CONTENT, rf.CONTENT
assert tuple(rf.STATES) == STATES, rf.STATES
assert set(rf.DIRS) == set(DIRS), rf.DIRS
assert TOTAL == 1040, TOTAL
assert ROWS * PER_ROW == TOTAL, (ROWS, PER_ROW)

# 角色 → (蒙版层, 亮度, alpha)。亮度按 REFACTOR-A §2 表；alpha 取旧 lay 的形状 alpha。
# 表中 `~` 的 alpha 写 200，但 render_final.role_rgba 给的是 255——以形状 alpha 为准取 255
# （否则 alpha 对拍必在 collect 的 4 个像素上失配）。
# X/Y/Z/K/N：A 方案删除成分专属纹理；这里不生成纹理色，改为中性 accent，
# 但保留旧形状的 alpha=255（否则 alpha 对拍在纹理位失配 1px）。
ROLE_MASK = {
    # body：按行三段 255 / 235 / 215（与 body_color 的 bbox 三段一致）
    "B": ("body", None, 208),
    "R": ("body", 75, 130),
    "u": ("body", 166, 170),
    # accent：亮核 / 前脸高光 / 霜点 / 白沫（纹理位并入）
    # + 汇聚蒸汽 S(0.60) / 抽吸流 ~(0.55)：旧版误归 body，染色后成了本体混合色（Δmax=130）。
    "C": ("accent", 255, 255),
    "G": ("accent", 255, 255),
    "I": ("accent", 255, 255),
    "F": ("accent", 255, 255),
    "X": ("accent", 255, 255),
    "Y": ("accent", 255, 255),
    "Z": ("accent", 255, 255),
    "K": ("accent", 255, 255),
    "N": ("accent", 255, 255),
    "S": ("accent", 255, 255),
    "~": ("accent", 255, 255),
    # edge：张力边 / 缝与唇 / 暗边 / 干痕 / 光扫
    "E": ("edge", 255, 255),
    "M": ("edge", 255, 255),
    "L": ("edge", 255, 255),
    "r": ("edge", 38, 130),
    "P": ("edge", 210, 200),
    "s": ("edge", 255, 200),
    "S2": ("edge", 255, 120),
    # wall：单独一张
    "W": ("wall", None, 255),
}

LAYER_FILES = {
    "body": ASSETS / "flow_mask_body.png",
    "edge": ASSETS / "flow_mask_edge.png",
    "accent": ASSETS / "flow_mask_accent.png",
    "wall": ASSETS / "flow_mask_wall.png",
}
INDEX_JSON = ASSETS / "flow_mask.json"


def body_lum(y, bbox):
    """与 render_final.body_color 同一套 bbox 三段：上 255 / 中 235 / 下 215。"""
    y0, h = bbox[1], bbox[3]
    t = (y - y0) / max(1, h - 1)
    if t < 1.0 / 3.0:
        return 255
    if t > 2.0 / 3.0:
        return 215
    return 235


def iter_keys():
    """按固定顺序产出 (key, content, state, dirn, frame)。

    variant 0 = 静态（frame=None）；variant 1..12 = f0..f11。
    非 flow 状态把 frame 置 None：render_final.build_grid 本来也只在 flow 用帧，
    这样 12 个帧槽与静态槽逐像素相同（布局占位，运行时不会请求）。
    """
    for variant in range(VARIANTS):
        for content in CONTENT:
            for state in STATES:
                for dirn in DIRS:
                    if variant == 0:
                        yield f"{content}|{state}|{dirn}", content, state, dirn, None
                    else:
                        frame = variant - 1
                        yield (f"{content}|{state}|{dirn}|f{frame}", content, state, dirn,
                               frame if state == "flow" else None)


def make_layers(content, state, dirn, frame):
    """返回 {layer: 16×16 RGBA}；四个层按角色互斥，形状 alpha = 旧 lay。"""
    g = rf.build_grid(BAKE_COMP, content, state, dirn, BAKE_TEMP, frame)
    # 与 render() 一致：bbox 取最终网格（旋转/泡沫/温度之后），空网格退回 CBBOX。
    bbox = rf.bbox_of(g) or rf.CBBOX[content]
    layers = {name: Image.new("RGBA", (TILE, TILE), (0, 0, 0, 0))
              for name in LAYER_FILES}
    px = {name: im.load() for name, im in layers.items()}
    for y in range(TILE):
        row = g[y]
        for x in range(TILE):
            role = row[x]
            if role == ".":
                continue
            layer, lum, alpha = ROLE_MASK[role]
            if layer == "body" and lum is None:
                lum = body_lum(y, bbox)
            if layer == "wall":
                px[layer][x, y] = WALL_COLOR + (alpha,)
            else:
                px[layer][x, y] = (lum, lum, lum, alpha)
    return layers


def build_atlases():
    """按行铺满 4 张图集，返回 (images, index)。"""
    images = {name: Image.new("RGBA", (PER_ROW * TILE, ROWS * TILE), (0, 0, 0, 0))
              for name in LAYER_FILES}
    index = {}
    i = 0
    for key, content, state, dirn, frame in iter_keys():
        x, y = (i % PER_ROW) * TILE, (i // PER_ROW) * TILE
        for name, tile in make_layers(content, state, dirn, frame).items():
            images[name].paste(tile, (x, y))
        index[key] = [x, y]
        i += 1
        if i % 208 == 0:
            print(f"  ... {i}/{TOTAL} tiles", flush=True)
    assert i == TOTAL, i
    return images, index


def check_flow_distinct(images, index):
    """flow 右向 13 槽 distinct：large 要求 13/13，small 只要求「不是静态重复」。

    small 8×6 的极值行 y=5/10 是 3px（x=7..9），±1px 后仍在 bbox 内，
    因此 build_grid 对它也做挤压：12 帧收敛成 9 种形态、每帧 2~4 px 差异。
    这里只守「> 1」——具体帧数由 FRAMES 表决定，不该在生成器里钉死。
    """
    print("\n=== flow 13 槽 distinct（静态 + f0..f11，右向）===")
    bad = []
    for content in CONTENT:
        keys = [f"{content}|flow|right"] + [
            f"{content}|flow|right|f{i}" for i in range(FRAME_COUNT)]
        sigs = []
        for key in keys:
            x, y = index[key]
            sigs.append(b"".join(
                images[name].crop((x, y, x + TILE, y + TILE)).tobytes()
                for name in LAYER_FILES))
        distinct = len(set(sigs))
        if content == "large":
            ok = distinct == VARIANTS
            note = "OK" if ok else f"FAIL（要求 {VARIANTS}/{VARIANTS}）"
            if not ok:
                bad.append(f"large flow distinct={distinct}/{VARIANTS}")
        elif content == "small":
            ok = distinct > 1
            note = "OK" if ok else f"FAIL（12 帧与静态逐字节相同，退回静态重复）"
            if not ok:
                bad.append(f"small flow distinct={distinct}/{VARIANTS}")
        else:
            note = "OK"
        print(f"{content:6s} distinct={distinct:2d}/{VARIANTS}  {note}")
    return bad


# ---------------------------------------------------------------- 抽样图
MIXES = (
    ("纯水 water", rf.COMPS["water"]),
    ("50% 毒 + 50% 水", rf.mix(rf.COMPS["water"], rf.COMPS["poison"], 0.5)),
    ("30% 油 + 70% 酸", rf.mix(rf.COMPS["oil"], rf.COMPS["acid"], 0.7)),
)
# 运行时 accent = mix(body, white, 0.62)（FlowTint.Accent 同式）；抽样图必须用同一式，
# 否则会把「固定近白」的旧行为画进预览。
ACCENT_MIX = 0.62

SAMPLE_ROWS = (
    # (分组标签, key 的 content|state|dir, 温度)
    ("方向 · 上", "large|flow|up", "normal"),
    ("方向 · 右", "large|flow|right", "normal"),
    ("方向 · 下", "large|flow|down", "normal"),
    ("方向 · 左", "large|flow|left", "normal"),
    ("状态 · 受阻", "large|blocked|right", "normal"),
    ("状态 · 分流", "large|split|right", "normal"),
    ("状态 · 汇聚", "large|merge|right", "normal"),
    ("状态 · 被收集", "large|collect|right", "normal"),
    ("温度 · 冷", "large|flow|right", "cold"),
    ("温度 · 热", "large|flow|right", "hot"),
    ("含量 · 少", "small|flow|right", "normal"),
    ("含量 · 中", "mid|flow|right", "normal"),
)

SAMPLE_SCALE = 8
SAMPLE_CELL = TILE * SAMPLE_SCALE


def tint(img, color):
    """模拟 UGUI Image.color：RGB 相乘、A 不变（8bit 四舍五入）。"""
    px = img.load()
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    op = out.load()
    r, g, b = color
    for y in range(img.height):
        for x in range(img.width):
            sr, sg, sb, sa = px[x, y]
            if sa == 0:
                continue
            op[x, y] = ((sr * r + 127) // 255, (sg * g + 127) // 255,
                        (sb * b + 127) // 255, sa)
    return out


def compose(layers, body, edge):
    out = Image.new("RGBA", (TILE, TILE), (0, 0, 0, 0))
    # 运行时三层色：body / edge 按成分+温度，accent = mix(body, 白 62%)。
    # 墙图已经带固定色 #C9C2B8，tint 必须是白；再乘 WallColor 会二次变暗。
    accent = rf.mix(body, (255, 255, 255), ACCENT_MIX)
    for name, color in (("body", body), ("edge", edge),
                        ("accent", accent), ("wall", (255, 255, 255))):
        out = Image.alpha_composite(out, tint(layers[name], color))
    return out


def checker(size, block=8):
    """深色棋盘：证明 tile 的透明区（8× 下每格 = 1 个源像素）。"""
    im = Image.new("RGBA", (size, size), (0x17, 0x13, 0x11, 255))
    px = im.load()
    for y in range(size):
        for x in range(size):
            if ((x // block) + (y // block)) % 2:
                px[x, y] = (0x1F, 0x1A, 0x16, 255)
    return im


def make_sample(images, index):
    cols = len(MIXES)
    pad, head, colh, rowlab, gap = 20, 124, 40, 176, 12
    rowh = SAMPLE_CELL + 22
    foot = 104
    width = pad * 2 + rowlab + cols * SAMPLE_CELL + (cols - 1) * gap
    height = pad + head + colh + len(SAMPLE_ROWS) * rowh + foot
    im = Image.new("RGBA", (width, height), rf.BG + (255,))
    d = ImageDraw.Draw(im)
    d.text((pad, pad), "A 方案蒙版抽样 · 12 格 × 3 种混色 · 8×",
           font=rf.F(20, True), fill=rf.INK + (255,))
    d.text((pad, pad + 30),
           "assets/flow_mask_{body,edge,accent,wall}.png · 256×1040 · 1040 key",
           font=rf.F(12), fill=rf.INK2 + (255,))
    d.text((pad, pad + 50),
           "RGB=亮度蒙版（染色时相乘）· A=形状 alpha · 形状按 water/normal 烘焙",
           font=rf.F(12), fill=rf.INK2 + (255,))
    d.text((pad, pad + 70),
           "成分决定 body/edge/accent 染色，温度只换边缘色，墙固定 #C9C2B8。",
           font=rf.F(12), fill=rf.INK2 + (255,))

    # 列头：混色 + 本体色块
    y_head = pad + head
    for c, (name, body) in enumerate(MIXES):
        x = pad + rowlab + c * (SAMPLE_CELL + gap)
        d.rectangle([x, y_head + 8, x + 12, y_head + 20], fill=body + (255,))
        d.text((x + 18, y_head + 6), name, font=rf.F(13, True), fill=rf.INK + (255,))
        d.text((x + 18, y_head + 21),
               "#%02X%02X%02X" % body, font=rf.F(10), fill=rf.INK2 + (255,))

    y0 = pad + head + colh
    check = checker(SAMPLE_CELL)
    for r, (label, key, temp) in enumerate(SAMPLE_ROWS):
        y = y0 + r * rowh
        d.text((pad, y + SAMPLE_CELL // 2 - 15), label, font=rf.F(14, True),
               fill=rf.INK + (255,))
        d.text((pad, y + SAMPLE_CELL // 2 + 4), key, font=rf.F(10), fill=rf.INK2 + (255,))
        assert key in index, f"抽样 key 不在索引里：{key}"
        bx, by = index[key]
        layers = {}
        for name, img in images.items():
            layers[name] = img.crop((bx, by, bx + TILE, by + TILE))
        for c, (_, body) in enumerate(MIXES):
            edge = rf.edge_color(body, temp)
            tile = compose(layers, body, edge)
            x = pad + rowlab + c * (SAMPLE_CELL + gap)
            im.alpha_composite(check, (x, y))
            im.alpha_composite(tile.resize((SAMPLE_CELL, SAMPLE_CELL), Image.NEAREST),
                               (x, y))
            d.rectangle([x - 1, y - 1, x + SAMPLE_CELL, y + SAMPLE_CELL],
                        outline=rf.GRID + (255,))

    y_foot = y0 + len(SAMPLE_ROWS) * rowh + 14
    d.line([(pad, y_foot), (width - pad, y_foot)], fill=rf.GRID + (255,))
    d.text((pad, y_foot + 10),
           "读法：每格 = 4 层（body / edge / accent / wall）按混色相乘后叠在棋盘上；"
           "透明角 = alpha 0。",
           font=rf.F(12), fill=rf.INK2 + (255,))
    d.text((pad, y_foot + 30),
           "温度行与方向·右 形状相同——温度只染 edge；accent 随成分；墙是独立 1×3 图元。",
           font=rf.F(12), fill=rf.INK2 + (255,))
    d.text((pad, y_foot + 50),
           "墙图自带 #C9C2B8：运行时墙层 tint 必须为白（再乘 WallColor 会二次变暗）。",
           font=rf.F(12), fill=rf.INK2 + (255,))
    d.text((pad, y_foot + 70),
           f"索引 assets/flow_mask.json：{len(index)} 个 key（content|state|dir[|f0..f11]），"
           "4 含量 × 5 状态 × 4 向 × 13 槽位。",
           font=rf.F(10), fill=rf.INK2 + (255,))
    SAMPLE.parent.mkdir(parents=True, exist_ok=True)
    im.convert("RGB").save(SAMPLE, format="PNG")


# ---------------------------------------------------------------- main
def sha256_of(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    print(f"keys={TOTAL} layout={PER_ROW * TILE}x{ROWS * TILE} "
          f"(content={len(CONTENT)} state={len(STATES)} dir={len(DIRS)} "
          f"slots={VARIANTS}: static + f0..f{FRAME_COUNT - 1})")
    images, index = build_atlases()

    bad = check_flow_distinct(images, index)
    if bad:
        for msg in bad:
            print("FAIL " + msg)
        return 1

    ASSETS.mkdir(parents=True, exist_ok=True)
    for name, img in images.items():
        img.save(LAYER_FILES[name], format="PNG")
    INDEX_JSON.write_bytes(
        json.dumps({"tiles": index}, ensure_ascii=False,
                   separators=(",", ":")).encode("utf-8"))
    make_sample(images, index)

    for name in ("body", "edge", "accent", "wall"):
        p = LAYER_FILES[name]
        print(f"{name:6s} {images[name].width}x{images[name].height} "
              f"{images[name].mode} {p.stat().st_size} bytes  sha256={sha256_of(p)}")
    print(f"json   assets/flow_mask.json {INDEX_JSON.stat().st_size} bytes  "
          f"keys={len(index)}  sha256={sha256_of(INDEX_JSON)}")
    print(f"sample docs/flow-visual/final/png/mask-sample.png "
          f"{SAMPLE.stat().st_size} bytes  sha256={sha256_of(SAMPLE)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
