"""生成秋穰子的稻穗、葡萄、谷粒与像素 buff 图标；固定随机种子。"""
from pathlib import Path
import math
import random

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/Spells/Minoriko/Textures"
BUILD = ROOT / "Build/Minoriko"
GOLD = (238, 184, 81, 220)
CREAM = (255, 231, 158, 235)
INK = (118, 68, 41, 255)


def curve(points, steps=60):
    a, b, c, d = np.array(points, dtype=float)
    return [tuple((1-t)**3*a + 3*(1-t)**2*t*b + 3*(1-t)*t*t*c + t**3*d)
            for t in np.linspace(0, 1, steps)]


def rice(im, origin, scale=1, angle=0, white=False):
    """垂首稻穗：细茎、交错的米粒与芒。"""
    d = ImageDraw.Draw(im)
    ca, sa = math.cos(angle), math.sin(angle)

    def p(x, y):
        return (origin[0] + scale*(x*ca-y*sa), origin[1] + scale*(x*sa+y*ca))

    stem = curve([(0, 0), (-8, -56), (20, -112), (51, -114)])
    d.line([p(x, y) for x, y in stem], fill=CREAM if not white else "white", width=max(1, round(scale*1.4)))
    for i in range(13):
        t = 0.24 + i*0.054
        x, y = stem[min(59, int(t*59))]
        side = -1 if i % 2 else 1
        end = (x + 11*side, y + 10)
        body = curve([(x,y),(x+9*side,y-3),(x+17*side,y+4),end],20)
        body += curve([end,(x+4*side,y+11),(x+side,y+6),(x,y)],20)
        d.polygon([p(*v) for v in body], fill="white" if white else GOLD)
        d.line([p(x+side*3, y+1), p(*end)], fill="white" if white else CREAM, width=max(1, round(scale)))
        d.line([p(*end), p(end[0]+5*side, end[1]+10)], fill="white" if white else (230, 185, 105, 145), width=max(1, round(scale*0.6)))


def sheaf():
    im = Image.new("RGBA", (900, 900))
    for x, y, scale, angle in [(328,643,4.2,-.38),(378,665,4.9,-.04),(410,645,3.9,.43)]:
        rice(im,(x,y),scale,angle)
    d=ImageDraw.Draw(im)
    # 朱红结绳呼应服装；保留独立穗束轮廓。
    d.line(curve([(337,606),(356,600),(395,628),(416,610)]),fill=(116,43,38,255),width=17)
    d.line(curve([(337,601),(356,595),(395,623),(416,605)]),fill=(201,76,49,255),width=7)
    for pts in [[(379,615),(322,576),(305,646),(379,615)],[(379,615),(442,590),(442,654),(379,615)],[(379,615),(358,647),(355,675),(328,694)],[(379,615),(403,644),(395,672),(415,687)]]:
        d.line(curve(pts),fill=(188,66,44,255),width=9)
    d.ellipse((369,605,387,623),fill=(233,144,74,255))
    return im.resize((512,512),Image.Resampling.LANCZOS)


def grapes():
    im=Image.new("RGBA",(512,512))
    d=ImageDraw.Draw(im)
    d.line(curve([(255,145),(236,98),(265,64),(304,54)]),fill=(177,135,67,255),width=12)
    d.line(curve([(261,106),(319,56),(369,125),(326,132)]),fill=(235,196,112,255),width=5)
    # 先画后排，再叠前排果实；高光与果粉在透明贴图内完成。
    berries=[(197,170,49),(285,170,51),(155,241,51),(334,241,49),
             (203,323,47),(286,323,49),(244,399,43),
             (242,222,56),(190,277,51),(292,277,51),(241,335,51)]
    yy,xx=np.mgrid[0:512,0:512]
    for i,(x,y,r) in enumerate(berries):
        distance=np.sqrt(((xx-x)/r)**2+((yy-y)/r)**2)
        light=np.exp(-(((xx-x+r*.33)/(r*.85))**2+((yy-y+r*.38)/(r*.85))**2)*2)
        rim=np.clip((distance-.83)/.17,0,1)
        pixels=np.zeros((512,512,4),dtype=np.uint8)
        for channel,base in enumerate([95+i%3*6,50,106+i%2*9]):
            pixels[:,:,channel]=np.clip(base+light*[108,96,118][channel]-rim*29,0,255)
        pixels[:,:,3]=np.clip((1-distance)*r,0,1)*255
        im=Image.alpha_composite(im,Image.fromarray(pixels))
        d=ImageDraw.Draw(im)
        d.arc((x-r*.68,y-r*.69,x+r*.45,y+r*.42),204,266,fill=(233,212,231,190),width=3)
    return im.resize((256,256),Image.Resampling.LANCZOS)


def grain():
    im=Image.new("RGBA",(256,256))
    d=ImageDraw.Draw(im)
    points=curve([(74,207),(25,97),(149,13),(170,40)])+curve([(170,40),(233,141),(126,241),(74,207)])
    d.polygon(points,fill=(242,199,100,255))
    d.line(points+[points[0]],fill=(173,113,49,255),width=5)
    d.line(curve([(83,188),(81,124),(124,70),(154,53)]),fill=(255,236,173,255),width=14)
    return im.resize((128,128),Image.Resampling.LANCZOS)


def icon():
    im=Image.new("RGBA",(48,48))
    d=ImageDraw.Draw(im)
    paper=[(8,3),(37,5),(40,13),(38,20),(41,28),(37,44),(7,42),(8,30),(5,22)]
    d.polygon([(x+1,y+2) for x,y in paper],fill=(40,28,24,110))
    d.polygon(paper,fill=(209,176,106),outline=INK)
    rng=random.Random(10001)
    for _ in range(75):
        x,y=rng.randrange(10,36),rng.randrange(8,40)
        d.point((x,y),fill=rng.choice([(192,158,91),(225,195,126),(203,167,99)]))
    # 像素稻穗与朱红结绳，48 px 下仍有清楚轮廓。
    d.line([(16,36),(19,29),(22,21),(24,11)],fill=(115,79,39),width=2)
    d.line([(29,36),(28,27),(30,20),(29,10)],fill=(115,79,39),width=2)
    for x,y in [(22,13),(21,18),(19,23),(17,28),(29,12),(29,18),(28,24)]:
        d.polygon([(x,y),(x-5,y-3),(x-6,y),(x-2,y+3)],fill=(245,217,124),outline=(137,92,40))
        d.polygon([(x+1,y+2),(x+5,y-1),(x+6,y+2),(x+2,y+5)],fill=(238,196,89),outline=(137,92,40))
    d.line([(15,32),(30,35),(16,35),(29,32)],fill=(157,56,39),width=2)
    d.line([(23,35),(19,40)],fill=(157,56,39),width=2)
    d.point((24,34),fill=(255,231,170))
    return im


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    BUILD.mkdir(parents=True,exist_ok=True)
    sheaf().save(OUT/"rice.png")
    grapes().save(OUT/"grapes.png")
    grain().save(OUT/"grain.png")
    yy,xx=np.mgrid[-1:1:128j,-1:1:128j]
    for name,a in [("mote",np.exp(-9*(xx*xx+yy*yy))),
                   ("spark",np.maximum(np.exp(-10*np.abs(xx)-3*np.abs(yy)),np.exp(-10*np.abs(yy)-3*np.abs(xx))))]:
        pixels=np.full((128,128,4),255,dtype=np.uint8)
        pixels[:,:,3]=np.clip(a*255,0,255).astype(np.uint8)
        Image.fromarray(pixels).save(OUT/f"{name}.png")
    icon().save(BUILD/"10001.png")
    # 静态素材审阅板；不是游戏实测截图。
    preview=Image.new("RGBA",(1400,660),(34,32,31,255))
    preview.alpha_composite(sheaf(),(40,50))
    preview.alpha_composite(grapes().resize((360,360),Image.Resampling.LANCZOS),(570,120))
    preview.alpha_composite(grain(),(1030,110))
    preview.alpha_composite(icon().resize((192,192),Image.Resampling.NEAREST),(998,310))
    preview.convert("RGB").save(BUILD/"art-review.jpg",quality=95)
    print(f"Generated Minoriko textures: {OUT}")


if __name__ == "__main__":
    main()
