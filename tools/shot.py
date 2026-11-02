# -*- coding: utf-8 -*-
"""用 chromium 截图 HTML，便于迭代视觉。"""
import sys
import asyncio
from playwright.async_api import async_playwright

SRC = r"E:/code/FluxForge/generated-images/bartender/portrait.html"
OUT = r"E:/code/FluxForge/.workbuddy-ai/ref/render.png"


async def main():
    async with async_playwright() as p:
        b = await p.chromium.launch()
        pg = await b.new_page(viewport={"width": 1240, "height": 760}, device_scale_factor=2)
        await pg.goto("file:///" + SRC.replace("\\", "/"))
        await pg.wait_for_timeout(900)
        await pg.screenshot(path=OUT, full_page=True)
        # 单独截 SVG 区域
        el = await pg.query_selector("#portrait")
        if el:
            await el.screenshot(path=OUT.replace(".png", "_svg.png"))
        await b.close()
    print("saved", OUT)


asyncio.run(main())
