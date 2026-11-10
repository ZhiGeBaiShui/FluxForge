from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import math

ROOT = Path(__file__).resolve().parents[1]
REF = ROOT / 'output/tutorial-note'
OUT = ROOT / 'assets/流源入门摆放便条.png'
canvas = Image.new('RGBA', (1024, 1408), (0, 0, 0, 0))
draw = ImageDraw.Draw(canvas)
ink = '#293434'
blue = '#387e89'
font_path = 'C:/Windows/Fonts/msyh.ttc'
def text(x, y, value, size=32, center=False, color=ink):
    font = ImageFont.truetype(font_path, size)
    draw.text((x,y), value, font=font, fill=color, anchor='mt' if center else 'lt', stroke_width=0)

def arrow(points, color=ink, width=7):
    draw.line(points, fill=color, width=width, joint='curve')
    x,y=points[-1]; px,py=points[-2]
    angle=math.atan2(y-py,x-px)
    length=22
    draw.polygon([(x,y),(x-length*math.cos(angle-.5),y-length*math.sin(angle-.5)),
                  (x-length*math.cos(angle+.5),y-length*math.sin(angle+.5))],fill=color)

def curve(p0,p1,p2,p3,color=ink):
    pts=[]
    for i in range(101):
        t=i/100; u=1-t
        pts.append((u**3*p0[0]+3*u*u*t*p1[0]+3*u*t*t*p2[0]+t**3*p3[0],
                    u**3*p0[1]+3*u*u*t*p1[1]+3*u*t*t*p2[1]+t**3*p3[1]))
    arrow(pts,color)

def sprite(path, x, y, scale=6, rotation=0, crop=None):
    im=Image.open(path).convert('RGBA')
    if crop: im=im.crop(crop)
    if rotation: im=im.transpose(rotation)
    im=im.resize((im.width*scale,im.height*scale),Image.Resampling.NEAREST)
    canvas.alpha_composite(im,(x,y))

text(560,65,'流源装置 · 初次操作',46,True)
sprite(REF/'med_bottle_red.png',230,330,7)
sprite(ROOT/'assets/flow_emitter_1x2.png',680,330,6)
curve((295,350),(380,240),(560,240),(655,350))
text(480,210,'① 将液体倒入发射器',30,True)
curve((740,535),(1000,610),(850,770),(250,830))
text(395,650,'② 按图摆放',34)
text(395,710,'注意发射口朝向',27)
# Native top nozzle rotated clockwise to point right; receiver faces left.
sprite(ROOT/'assets/flow_emitter_1x2.png',135,850,5,Image.Transpose.ROTATE_270)
sprite(ROOT/'assets/chem_reactor_heat.png',415,850,5)
sprite(ROOT/'assets/chem_reactor_alkali.png',615,850,5)
sprite(ROOT/'assets/flow_emitter_1x2.png',800,850,5,Image.Transpose.ROTATE_90)
for x1,x2 in [(335,394),(520,594),(720,779)]: arrow([(x1,890),(x2,890)],blue,6)
for x,label in [(455,'升温装置'),(655,'净化装置')]: text(x,970,label,27,True)
text(455,1015,'（升温能提升净化装置的净化效果）',20,True,color='#777d76')
text(865,970,'接收液体同样使用发射器',22,True)
arrow([(240,1060),(215,946)])
text(145,1090,'③ 激活',34)
text(145,1150,'（左键双击）',29)
# Reuse a water droplet from the project's liquid preview, clipped at the nozzle.
liquid=Image.open(ROOT/'assets/liquid_preview.png').convert('RGBA').crop((34,99,83,136))
# Remove the preview's flat background; retain only original colored droplet pixels.
pixels=liquid.load()
for y in range(liquid.height):
    for x in range(liquid.width):
        r,g,b,a=pixels[x,y]
        if max(r,g,b)-min(r,g,b)<18 or g<65: pixels[x,y]=(0,0,0,0)
liquid=liquid.resize((44,33),Image.Resampling.NEAREST)
canvas.alpha_composite(liquid.crop((22,0,44,33)),(295,873))
text(560,1255,'从右侧发射器取出处理后的液体。',30,True)
canvas.save(OUT)
print(OUT)
