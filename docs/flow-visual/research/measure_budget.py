# -*- coding: utf-8 -*-
"""
流源视觉预算推算（不依赖游戏运行时）。

用途：把「一个 quad 多少字节、64 quads 能画多少、1024 粒子上限有多贵」
算成可复核的数字，供 docs/flow-visual/research/engine-constraints.md 引用。

用法：
  C:\\Users\\Administrator\\.dsh\\dsh-runtimes\\dsh-primary-runtime\\dependencies\\python\\python.exe measure_budget.py

说明：
  · UIVertex 的 108 B 是按 UnityEngine.UI 程序集里 VertexHelper.AddVert 的
    重载签名推出来的：position(12) + normal(12) + tangent(16) + color32(4)
    + uv0..uv3(4 x 16) = 108。未在本机用 Marshal.SizeOf 实测。
  · 其余全部是算术，不是实测帧率。
"""

CELL = 16                 # 1 格 = 16 局部单位（像素），BRIEF §1
VERT_BYTES = 108          # UIVertex，见文件头说明
INDEX_BYTES = 2           # UInt16
VERTS_PER_QUAD = 4
INDICES_PER_QUAD = 6

MAX_PARTICLES = 1024      # src/Presentation/Particles/FlowParticleField.cs:51
QUADS_PER_PARTICLE = 2    # 本体 + Softness 外圈（FlowParticleGraphic.cs:95-103）
MAX_PER_FRAME = 10        # DefaultEmissionPolicy.cs:53
MAX_LIFE = 0.70           # DefaultEmissionPolicy.cs:73
MIN_PER_FRAME = 1         # DefaultEmissionPolicy.cs:50
MIN_LIFE = 0.12           # DefaultEmissionPolicy.cs:70


def quad_bytes():
    return VERTS_PER_QUAD * VERT_BYTES + INDICES_PER_QUAD * INDEX_BYTES


def kib(n):
    return n / 1024.0


def row(name, quads, note=""):
    verts = quads * VERTS_PER_QUAD
    idx = quads * INDICES_PER_QUAD
    vbytes = verts * VERT_BYTES
    ibytes = idx * INDEX_BYTES
    print("| %s | %d | %d | %s | %s | %s |" % (
        name, quads, verts, format(vbytes, ","), format(ibytes, ","),
        ("%.1f KiB" % kib(vbytes + ibytes)) + ((" · " + note) if note else "")))


def main():
    # Windows 控制台默认 GBK，中文/上标会炸；统一按 UTF-8 输出
    try:
        import sys
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

    print("## 单位换算")
    print()
    print("- 1 quad = %d 顶点 + %d 索引 = %s B（顶点 %s B + 索引 %s B）" % (
        VERTS_PER_QUAD, INDICES_PER_QUAD, format(quad_bytes(), ","),
        format(VERTS_PER_QUAD * VERT_BYTES, ","),
        format(INDICES_PER_QUAD * INDEX_BYTES, ",")))
    print("- 1 格 = %d px = %d px²；64 quads（1x1 像素块）= 64 px² = 1/4 格" % (CELL, CELL * CELL))
    print()

    print("## 场景预算")
    print()
    print("| 场景 | quads | verts | 顶点字节 | 索引字节 | 合计 |")
    print("|---|---|---|---|---|---|")
    row("单流源静态设计预算（C7 上限）", 64)
    row("单流源示例构成（见 §7 建议 1）", 18)
    row("当前实现：低含量 1 源（7 粒）", 7 * QUADS_PER_PARTICLE)
    row("当前实现：满含量 1 源 @60fps（420 粒）", 420 * QUADS_PER_PARTICLE)
    row("当前实现：满含量 1 源 @144fps（1024 粒封顶）", MAX_PARTICLES * QUADS_PER_PARTICLE)
    row("当前实现：单网格 1024 粒上限", MAX_PARTICLES * QUADS_PER_PARTICLE)
    row("新设计：20 个流源 x 64 quads（同层合批）", 20 * 64)
    row("新设计：4 块网格 x 512 quads", 4 * 512)
    print()

    print("## 当前粒子实现的稳态存活数（Emit 每帧 %d 粒、寿命 %.2fs）" % (
        MAX_PER_FRAME, MAX_LIFE))
    print()
    print("| 帧率 | 单源存活 | 单源 quads | 单源顶点字节 | 达到 1024 上限所需源数 |")
    print("|---|---|---|---|---|")
    for fps in (30, 60, 90, 120, 144, 240):
        alive = min(MAX_PARTICLES, MAX_PER_FRAME * MAX_LIFE * fps)
        quads = alive * QUADS_PER_PARTICLE
        print("| %d | %.0f | %.0f | %s | %.1f |" % (
            fps, alive, quads, format(int(quads * VERTS_PER_QUAD) * VERT_BYTES, ","),
            MAX_PARTICLES / alive))
    print()

    print("## 64 quads 的容量（1x1 像素块）")
    print()
    print("| 形态 | 占用 quads | 覆盖 |")
    print("|---|---|---|")
    print("| 16x16 逐像素 | %d | 整格，超预算 %.0f 倍 |" % (
        CELL * CELL, CELL * CELL / 64.0))
    print("| 8x8 逐像素 | 64 | 8x8 px，刚好用满、无动画余量 |")
    print("| 4x4 的 2x2 像素块 | 16 | 8x8 px，留 48 quads |")
    print("| 12x12 图标的 1px 外框 | 4 | 44 px² |")
    print("| 12x12 全填充 | 1 | 144 px²（会盖住图标，C5 禁止） |")
    print()

    print("## 64 quads 的一个可行静态构成")
    print()
    parts = [
        ("主体/底色", 1),
        ("1px 内框（上/下/左/右）", 4),
        ("含量 3 段", 3),
        ("温度 3 档 pips", 3),
        ("方向头", 3),
        ("射程 4 缺口", 4),
    ]
    total = 0
    print("| 通道 | quads |")
    print("|---|---|")
    for name, n in parts:
        total += n
        print("| %s | %d |" % (name, n))
    print("| **合计** | **%d** |" % total)
    print()
    print("静态 %d / 64，余量 %d quads（%.1f 倍）给动效与细节。" % (
        total, 64 - total, 64.0 / total))
    print()

    print("## 重建成本粗估（未实测，仅数量级）")
    print()
    print("| 规模 | AddVert 调用 | 假设 40ns/次 | 顶点内存 |")
    print("|---|---|---|---|")
    for quads in (18, 64, 512, 2048):
        calls = quads * VERTS_PER_QUAD + quads * 2  # 4 AddVert + 2 AddTriangle
        print("| %d quads | %d | %.2f ms | %s |" % (
            quads, calls, calls * 40 / 1e6,
            format(quads * VERTS_PER_QUAD * VERT_BYTES, ",")))
    print()
    print("注：40ns 是托管侧 List.Add + 结构体写入的量级假设，不含 FillMesh 与"
          " CanvasRenderer 上传；真实值需 Profiler 采样。")


if __name__ == "__main__":
    main()
