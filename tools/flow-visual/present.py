"""把 16×16 tile 组装成交付图版：网格场景、属性行、矩阵、1x/4x 对照。

只依赖 tile 图片，不依赖任何设计的内部实现——最终设计换了也能用。

用法示例：
    from present import Scene, row_sheet, matrix_sheet
    Scene(cols=16, rows=7, scale=6).add_flow(x, y, "tiles/water.png")...
"""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

CELL = 16
BG = (22, 19, 18)
GRID_A = (30, 25, 23)
GRID_B = (27, 23, 21)
LINE = (46, 39, 36)
TEXT = (235, 225, 215)
DIM = (150, 140, 130)
FONT_PATH = r"C:\Windows\Fonts\msyh.ttc"


def font(size):
    try:
        return ImageFont.truetype(FONT_PATH, size)
    except Exception:
        return ImageFont.load_default()


def load_tile(path):
    return Image.open(path).convert("RGBA")


class Scene:
    """一块像素网格 + 若干 16×16 tile + 可选图标占位。"""

    def __init__(self, cols, rows, scale=6, pad=8):
        self.cols, self.rows, self.scale, self.pad = cols, rows, scale, pad
        self.w = cols * CELL * scale
        self.h = rows * CELL * scale
        self.tiles = []   # (cell_x, cell_y, tile)
        self.icons = []   # (cell_x, cell_y, kind)

    def add_flow(self, x, y, tile):
        self.tiles.append((x, y, tile if isinstance(tile, Image.Image) else load_tile(tile)))
        return self

    def add_icon(self, x, y, kind="bottle"):
        self.icons.append((x, y, kind))
        return self

    def _icon(self, d, x, y):
        px = x * CELL * self.scale
        py = y * CELL * self.scale
        s = self.scale
        d.rectangle([px + 2*s, py + 2*s, px + 13*s, py + 13*s], fill=(58, 50, 45))
        # 一个极简的瓶形占位（12×12 内）
        d.rectangle([px + 6*s, py + 1*s, px + 9*s, py + 3*s], fill=(138, 127, 114))
        d.rectangle([px + 4*s, py + 4*s, px + 11*s, py + 12*s], fill=(110, 139, 122))
        d.rectangle([px + 5*s, py + 5*s, px + 7*s, py + 10*s], fill=(169, 195, 178))

    def render(self):
        img = Image.new("RGB", (self.w, self.h), BG)
        d = ImageDraw.Draw(img)
        s = self.scale
        for r in range(self.rows):
            for c in range(self.cols):
                col = GRID_A if (r + c) & 1 else GRID_B
                d.rectangle([c*CELL*s, r*CELL*s, (c+1)*CELL*s - 1, (r+1)*CELL*s - 1], fill=col)
        for c in range(self.cols + 1):
            d.rectangle([c*CELL*s, 0, c*CELL*s + max(1, s//3), self.h], fill=LINE)
        for r in range(self.rows + 1):
            d.rectangle([0, r*CELL*s, self.w, r*CELL*s + max(1, s//3)], fill=LINE)
        for (x, y, kind) in self.icons:
            self._icon(d, x, y)
        for (x, y, t) in self.tiles:
            big = t.resize((CELL*s, CELL*s), Image.NEAREST)
            img.paste(big, (x*CELL*s, y*CELL*s), big)
        return img


def row_sheet(tiles, labels, scale=6, gap=8, title=None, subtitle=None, cols=None):
    """一排 tile，带标签；tiles 可以是路径或 PIL 图。"""
    tiles = [t if isinstance(t, Image.Image) else load_tile(t) for t in tiles]
    cols = cols or len(tiles)
    w = CELL * scale
    rows = (len(tiles) + cols - 1) // cols
    top = 34 if title else 8
    img = Image.new("RGB", (gap + cols*(w+gap), top + rows*(w+24) + gap), BG)
    d = ImageDraw.Draw(img)
    if title:
        d.text((gap, 6), title, fill=TEXT, font=font(15))
        if subtitle:
            d.text((gap, 22), subtitle, fill=DIM, font=font(11))
    for i, (t, lab) in enumerate(zip(tiles, labels)):
        r, c = divmod(i, cols)
        x = gap + c*(w+gap)
        y = top + r*(w+24)
        d.rectangle([x-1, y-1, x+w, y+w], outline=(52, 45, 40))
        img.paste(t.resize((w, w), Image.NEAREST), (x, y))
        d.text((x, y + w + 4), lab, fill=DIM, font=font(11))
    return img


def matrix_sheet(rows, row_labels, col_labels, scale=5, title=None):
    """rows[r][c] = tile。"""
    w = CELL * scale
    gap = 6
    top = 46 if title else 8
    left = 60
    cols = len(col_labels)
    img = Image.new("RGB", (left + cols*(w+gap) + gap, top + len(rows)*(w+gap) + gap), BG)
    d = ImageDraw.Draw(img)
    if title:
        d.text((8, 8), title, fill=TEXT, font=font(15))
    for c, lab in enumerate(col_labels):
        d.text((left + c*(w+gap) + 2, top - 14), lab, fill=DIM, font=font(10))
    for r, row in enumerate(rows):
        y = top + r*(w+gap)
        d.text((6, y + w//2 - 6), row_labels[r], fill=DIM, font=font(11))
        for c, t in enumerate(row):
            x = left + c*(w+gap)
            img.paste(t.resize((w, w), Image.NEAREST), (x, y))
    return img
