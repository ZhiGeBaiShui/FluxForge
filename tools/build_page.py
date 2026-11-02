# -*- coding: utf-8 -*-
"""把 art.svg 内联进页面，生成最终交付的 HTML。"""
import re
from pathlib import Path

BASE = Path(r"E:/code/FluxForge/generated-images/bartender")
svg = (BASE / "art.svg").read_text(encoding="utf-8")

# 去掉根元素的宽高，交给 CSS 控制
svg = svg.replace('width="600" height="600"', 'class="art" preserveAspectRatio="xMidYMid meet"', 1)

HTML = """<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>薇拉 · Vera — 上层区调酒师立绘</title>
<style>
  :root{
    --bg:#0e0c14;
    --panel:#1a1722;
    --line:#322b3d;
    --ink:#e9e2d4;
    --dim:#8d8579;
    --gold:#d8a94f;
  }
  *{box-sizing:border-box;}
  html,body{height:100%;}
  body{
    margin:0;
    background:
      radial-gradient(1100px 700px at 28% 18%, #251f31 0%, #14111b 52%, #0b0910 100%);
    color:var(--ink);
    font-family:"Microsoft YaHei","PingFang SC",-apple-system,"Segoe UI",sans-serif;
    display:flex; align-items:center; justify-content:center;
    padding:44px 24px;
  }
  .stage{
    display:flex; gap:40px; align-items:stretch;
    max-width:1160px; width:100%;
  }
  .card{
    position:relative; flex:0 0 auto;
    width:560px; height:560px;
    border-radius:20px;
    background:linear-gradient(180deg,#221d2c 0%,#16131e 100%);
    border:1px solid var(--line);
    box-shadow:0 34px 90px rgba(0,0,0,.6), inset 0 1px 0 rgba(255,255,255,.05);
    overflow:hidden;
  }
  .card::after{
    content:""; position:absolute; inset:0; border-radius:20px;
    box-shadow:inset 0 0 100px rgba(0,0,0,.5); pointer-events:none;
  }
  svg.art{display:block; width:100%; height:100%;}

  .panel{
    flex:1 1 auto; display:flex; flex-direction:column; gap:18px;
    padding:6px 2px; min-width:320px;
  }
  .kicker{
    font-size:11px; letter-spacing:.34em; color:var(--gold);
    text-transform:uppercase; font-weight:600;
  }
  h1{margin:8px 0 2px; font-size:42px; font-weight:700; letter-spacing:.05em;}
  h1 span{
    font-size:15px; color:var(--dim); font-weight:400;
    letter-spacing:.2em; margin-left:12px;
  }
  .role{color:var(--dim); font-size:14px; margin:0 0 6px; letter-spacing:.04em;}
  .desc{font-size:13.5px; line-height:1.95; color:#b7afa2; margin:0;}
  .desc b{color:#e6dcc9; font-weight:600;}
  .rule{height:1px; background:linear-gradient(90deg,var(--line),transparent); margin:2px 0;}
  .swatches{display:flex; gap:12px; flex-wrap:wrap;}
  .sw{width:32px; height:32px; border-radius:8px; border:1px solid rgba(255,255,255,.10); position:relative;}
  .sw i{
    position:absolute; left:0; bottom:-17px;
    font-size:9px; color:#6b6459; font-style:normal; letter-spacing:.02em;
  }
  .pixelbox{
    margin-top:auto; display:flex; align-items:center; gap:18px; padding:16px;
    border-radius:16px; border:1px solid var(--line);
    background:rgba(255,255,255,.022);
  }
  canvas#pixel{
    width:128px; height:128px; image-rendering:pixelated;
    border-radius:8px; background:#12101a; flex:0 0 auto;
  }
  .pixelmeta{font-size:12px; color:var(--dim); line-height:1.85;}
  .pixelmeta b{display:block; color:#cfc5b3; font-size:13px; margin-bottom:3px; font-weight:600;}
  .hint{font-size:11px; color:#5d5750; letter-spacing:.03em;}
  .foot{font-size:11px; color:#575149; letter-spacing:.03em;}
  @media (max-width:980px){
    .stage{flex-direction:column; align-items:center;}
    .card{width:min(92vw,520px); height:min(92vw,520px);}
  }
</style>
</head>
<body>
<div class="stage">
  <div class="card">
__SVG__
  </div>

  <aside class="panel">
    <div>
      <div class="kicker">Upper District · Bartender</div>
      <h1>薇拉 <span>VERA</span></h1>
      <p class="role">上层区私人沙龙 · 首席调酒师</p>
    </div>

    <p class="desc">
      深紫天鹅绒马甲、象牙高领、酒红丝巾——<b>没有一件是制服</b>。
      左耳一只长金坠，喉间挂着一小瓶会发光的琥珀色酒液，那是她的招牌。
    </p>
    <p class="desc">
      一手抬杯至胸前，手腕微沉，<b>像表演开始前的那半秒停顿</b>。
      眼神半阖、唇微启，不必再多动作。
    </p>

    <div class="rule"></div>

    <div class="swatches">
      <div class="sw" style="background:#3c2140"><i>#3C2140</i></div>
      <div class="sw" style="background:#ece0c6"><i>#ECE0C6</i></div>
      <div class="sw" style="background:#6a3a26"><i>#6A3A26</i></div>
      <div class="sw" style="background:#d8a94f"><i>#D8A94F</i></div>
      <div class="sw" style="background:#9c3446"><i>#9C3446</i></div>
      <div class="sw" style="background:#e6b189"><i>#E6B189</i></div>
    </div>

    <div class="pixelbox">
      <canvas id="pixel" width="160" height="160"></canvas>
      <div class="pixelmeta">
        <b>游戏内实际尺寸</b>
        160 × 160 · 同一份矢量缩放<br>
        右边就是缩小后的真实观感
        <div class="hint">非另画一版，改代码即改图</div>
      </div>
    </div>

    <div class="foot">纯 SVG 绘制 · 无位图 · 可直接改色改形</div>
  </aside>
</div>

<script>
  const svg = document.querySelector('svg.art');
  const canvas = document.getElementById('pixel');
  const ctx = canvas.getContext('2d');

  function drawPixel(){
    const clone = svg.cloneNode(true);
    clone.setAttribute('xmlns','http://www.w3.org/2000/svg');
    clone.setAttribute('width','160');
    clone.setAttribute('height','160');
    const data = new XMLSerializer().serializeToString(clone);
    const img = new Image();
    img.onload = () => {
      ctx.clearRect(0,0,160,160);
      ctx.imageSmoothingEnabled = true;
      ctx.drawImage(img, 0, 0, 160, 160);
    };
    img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(data);
  }
  drawPixel();
</script>
</body>
</html>
"""

out = HTML.replace("__SVG__", svg)
(BASE / "portrait.html").write_text(out, encoding="utf-8")
print("written", BASE / "portrait.html", len(out))
