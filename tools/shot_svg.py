# -*- coding: utf-8 -*-
"""截取单个 SVG 文件。"""
import asyncio
from playwright.async_api import async_playwright

SRC = r"E:/code/FluxForge/generated-images/bartender/art.svg"
OUT = r"E:/code/FluxForge/.workbuddy-ai/ref/art.png"


async def main():
    async with async_playwright() as p:
        b = await p.chromium.launch()
        pg = await b.new_page(viewport={"width": 600, "height": 600}, device_scale_factor=2)
        await pg.goto("file:///" + SRC.replace("\\", "/"))
        await pg.wait_for_timeout(400)
        await pg.screenshot(path=OUT)
        await b.close()
    print("saved", OUT)


asyncio.run(main())
