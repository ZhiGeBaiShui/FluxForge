# 蒸馏器生图提示词与迭代记录

使用内置 imagegen，4轮上限已执行完毕。参考：原版 items_tool / items_winemaking 图集，首轮另参考直通管。

技能来源：https://github.com/anthropics/skills/tree/main/skills/canvas-design
安装位置：C:/Users/Administrator/.codex/skills/canvas-design/SKILL.md

## 方案

以可理解的功能形体替代巨大观察窗平板外壳。主锅炉、冷凝塔与共用底座构成主次关系，入口约10逻辑像素开口，通过短颈接入锅炉，出口为冷凝塔顶部的短出流口。独立液位窗与指示灯预留动画。

## 逐轮提示词

### R1

```text
Create ONE game inventory sprite: original compact water distiller for Probably Stolen / FluxForge. References 1-2 are ORIGINAL GAME ATLASES for visual style only, NOT layouts to copy; reference3 is a tiny straight pipe for material/interface language only. This is a NEW STRUCTURE, not a washing machine. Coarse restrained native48x48 logical pixel concept. Front slightly above: narrow top plane, no side faces/isometric view. Pure white background. Canvas1024 square, intended64x64 logical blocks16px each; centered object fits48x48 logical blocks with8-block margins. No text/grid/shadow/smoke.
STRUCTURE-FIRST silhouette: low wide pressure-boiler module LEFT joined to narrower condenser RIGHT, shared heavy base. Casing about46 logical pixels wide and32 high, not a tall square flat face. Short outlet tower rises from condenser UPPER RIGHT, total height44-46. Modest seam between functional modules, no exposed elaborate coils. Boiler26px wide, condenser12px wide. Integrated LEFT intake faces left, aligned with middle row of3x3 footprint: dark rectangular socket10 logical pixels tall, only4 pixels deep, thick blunt metal lips, flush to silhouette; physically part of boiler. NOT cutout arrow, NO teal dots forming arrow, NO long pipe or giant funnel. TOP RIGHT output faces up: short squared12px-wide10px-high chimney cast into condenser, clearly open mouth10px wide, light rim dark interior, no rain cap. Both ports2/3 of ONE16px tile, not2/3 whole machine. Port centers align left-middle and top-right tiles. Small6x8px vertical liquid gauge on boiler, NOT giant circular window. Tiny2x2 amber lamp on lower condenser. One pair of ventilation slits on shared base. Broad flat planes, functional joined masses, no random detailing.
Use ONLY10 object colors: #2f2b3a outline #373541 recess #44464c dark metal #5f6868 middle metal #88948a sage #a8aea5 light sage #c4c4b0 edge #275654 dark liquid #45918b liquid #bb9165 lamp. Each material maximum3 flat shades. Absolutely NO gradients, smooth shading, texture, noise, small screws, orphan pixels, dithering, antialiasing. Light front-top; raised ledges highlight above dark underside; recess dark upper lip light bottom lip;1-block dark plum exterior outline, bottom2 blocks. Readable as low boiler → condenser → short outlet, not window appliance. Outlet mouth, lamp and gauge distinct for future animation. Crisp true pixel clusters, limited palette. Single original isolated sprite.
```

### R2

```text
Use case: precise-object-edit / game inventory sprite refinement. Reference is round1 machine design, preserve its FUNCTIONAL DIVISION but REBUILD proportions, not simply scaling whole image. One Probably Stolen coarse pixel industrial water distiller, 3x3 inventory tiles. Rebuild silhouette nearly square: total46 logical pixels wide44 high. Shared body36px high, short right outlet adds8px above. Left broad boiler and narrow right condenser on one heavy base. Increase plain boiler/condenser wall height while keeping narrow water gauge small. Make left boiler recognizably pressure boiler with simple flat lid and subtle stepped cylindrical shoulders rather than giant empty appliance face. No big round window. LEFT INTAKE is a solid shallow industrial socket/collar, NOT C-clamp, carrying handle, bracket, or giant cutout. Visible dark throat10 logical pixels tall4 deep, horizontal-facing to LEFT, upper/lower metal lips only1-2 logical pixels thick. The opening is much taller than rim, projects only2 pixels beyond main case, with continuous solid casing behind it; should look like a flush receiver slot on original game machine, not a decorative grip. Its center is mid-height of entire3x3 footprint. Right output OPEN chimney mouth faces UP at upper-right tile, opening10px wide, tower12px wide8px tall, short square casting, not a tall smokestack. It is flow launcher, no rain cap, no smoke. Tiny2x2 amber lamp lower right, one4x8 water gauge left, one2-slot base vent. Broad flat quiet surfaces. Remove extra ornament. Material seams make physical sense.
CRITICAL PIXEL MEDIUM: emulate a native48x48 sprite enlarged by nearest neighbor, NO continuous shading. Entire canvas1024x1024 white with64x64 equal logical blocks, every block16x16 uniform pixels, object48x48 blocks centered. All contours step in identical block size, no sub-block linework. Actual FLAT PALETTE ONLY: #2f2b3a #373541 #44464c #5f6868 #88948a #a8aea5 #c4c4b0 #275654 #45918b #bb9165. Each plane is one single flat color, shadows are hard-edged separate blocks, NOT gradients or banded ramps. No texture, noise, antialiasing, glossy lighting, shaded vignette, soft hue variations. Front view tiny top strip, no side faces/isometric. Exterior dark plum1-block outline, bottom2, inner seams darkest adjacent material. White background no cast shadow, no text arrows grid or diagrams. Keep functional simple salvaged-space-industrial aesthetic. Do not decorate empty surfaces.
```

### R3

```text
NEW DESIGN FROM SCRATCH. One original Probably Stolen / FluxForge water distiller game inventory sprite. Use reference atlas only for modest coarse pixel style, not atlas layout. Do not draw tall flat washing-machine box. Build understandable industrial hardware from THREE shaped masses: rounded-shoulder upright pressure boiler left28 logical pixels wide34 high; narrower condenser column right12 wide30 high with TWO broad simple cooling bands; shared low heavy base. Combined silhouette almost square48x46 logical pixels, fits3x3 tiles. Boiler and column have distinct contours, NOT just a seam painted on a rectangle. Boiler has flat lid, stepped rounded shoulders, only2-3 flat color faces. At left-front shoulder attach a small thick-walled OCTAGONAL SUCTION INTAKE: outer12x12 logical pixels, visible DARK OPEN THROAT8-10 wide10 high. Mouth oriented left but slightly cheated toward viewer so the actual dark interior is clearly visible. This is an open suction mouth, not a circular glass window: dark interior has no liquid, no glass highlight, perhaps ONE small dark inner ledge. Surround throat by a2-pixel metal rim. Short3px neck attaches rim into boiler shoulder; projects just2-3px beyond tank, never long pipe. Its center lies in middle-left cell of3x3 footprint. Intake is a compact solid mechanical module, NOT edge-on U, NOT carrying handle, NOT clamp, NOT arrow cutout. Upper-right condenser ends in short open squared output chimney, dark10-pixel mouth facing upward, rim and collar integrated into tower, outlet not a giant separate smokestack. Small narrow3x7 teal liquid gauge on boiler away from intake. Tiny amber2x2 lamp on condenser lower face. Strong readable silhouette, functional size relations. Keep three animation landmarks separate: intake throat, tiny lamp, empty upper output mouth for later steam overlay. No smoke yet.
Authentic low-resolution original game pixel asset: broad flat muted sage metal, dark plum outline; no ornament, no screw noise, no dense exposed coils. Mild front-above inventory viewpoint; whole machine mainly front elevation, inlet lip alone cheats toward camera for readability. Top planes are shallow bands; no isometric render. 1024x1024 canvas intended64x64 uniform blocks16px each; object48x48 blocks centered8-block white margins. All contours crisp equal-size stepped blocks. White #FFFFFF background. EXACT object palette only #2f2b3a #373541 #44464c #5f6868 #88948a #a8aea5 #c4c4b0 #275654 #45918b #bb9165. ONE constant flat color per face; hard pixel highlights and shadows. No gradients, antialiasing, brushwork, dithering, texture or lighting ramps. Exterior1px dark plum outline2px on bottom, interior seams material-dark shade. Small top-front highlights, recess top dark/lower lip light. Single sprite only no text no arrows no grid no floor shadow. Prioritize plausible suction head and boiler/condenser identity, not abstract appliance.
```

### R4

```text
FINAL PRECISION EDIT of this exact inventory distiller sprite. KEEP overall boiler/condenser shapes, sizes, lid, base, feet, tiny gauge, status lamp and upper-right outlet unchanged. Only TWO corrections:
1. MOVE THE ENTIRE LEFT SUCTION HEAD DOWN as ONE assembled unit: octagonal rim, dark throat, short neck and attached hard shadow. Present head center is around y520 on1254 canvas; move center to y665, about140 actual image pixels /6-7 logical pixels lower. Align inlet center exactly to vertical midpoint of object silhouette bounding box (object top~200 bottom~1133), so it occupies the middle-left tile of3x3 grid. Reconstruct plain boiler metal where old head was. Do NOT resize head, do NOT enlarge opening or change throat-to-rim proportion, do NOT leave a second port behind. Keep leftward short attachment and clear visible dark open mouth. It is NOT glass/window. Only lower entire unit; maintain its short mechanical neck.
2. REMOVE ALL CONTINUOUS GRADIENTS from broad surfaces and use HARD FLAT PIXEL COLOR REGIONS. Boiler main front is ONE uniform #88948a. Boiler left shadow ONE #5f6868. Narrow rim highlights ONE #a8aea5 or #c4c4b0. Same flat treatment on condenser. Dark throat #373541, exterior outline #2f2b3a, base dark metal #44464c, liquid #275654 and #45918b, lamp #bb9165. Total object palette ONLY these10 colors: #2f2b3a #373541 #44464c #5f6868 #88948a #a8aea5 #c4c4b0 #275654 #45918b #bb9165. White background #FFFFFF. No third-party colors, airbrush, hue variations, texture, dithering, edge smoothing, little pixels or new details. Broad surfaces must be visibly solid painted areas. Preserve coarse logical48x48 pixel sprite block language and clean staircase contours; all fine material variation replaced by clear pixel planes. No shadow on white background. Keep status/steam animation landmarks. No smoke, arrows, text, grid, diagrams or labels. This is a restrained vanilla Probably Stolen item sprite, not a shaded illustration. Do not redesign any other geometry.
```

## 评审摘要

- R1：功能分块改善，但左口像夹钳、整体偏矮；不采用。
- R2：入口更像把手，主锅炉成为大空白方罐；不采用。
- R3：改为圆肩锅炉＋窄冷凝塔；可见吸入口解决夹钳问题，整体比例明显改善，但入口偏高。
- R4：保持R3整体结构，下移整个吸入口到机体中线，减少零碎阴影；推荐作为下一步像素制作的形体设计参考。

## 硬约束检查

## 子代理最终独立评审

推荐R4而非R3，整体造型与比例评审通过，严格像素贴图验收未通过。保留圆肩锅炉、细长冷凝塔、共用底座、中部厚壁吸入口、小液位窗及单指示灯。入口的可见口腔有朝左前方的视角妥协；整理原生贴图时仍以左中格为入口，并将口径落实为10~11像素。顶部发射口还应补足向上的顶面暗口，以配合蒸汽向上播放。达到4轮上限后停止生图。

R4 文件实测1254×1254，16839个RGB颜色。并未满足1024画布、严格64×64色块网格或10色限制。仍有渐变和不等像素尺度，不能称为可直接实装的48×48贴图。本轮只完成整体造型与比例探索，未改项目资源或代码。

