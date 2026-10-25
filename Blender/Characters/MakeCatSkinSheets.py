"""Lay out actual Blender renders for visual review; no synthesized character images."""
from PIL import Image, ImageDraw, ImageFont
from pathlib import Path
import json
import zipfile

ROOT=Path(__file__).resolve().parent
OUT=ROOT.parent/'Previews'/'Characters'/'Skins'
manifest=json.loads((ROOT/'Exports'/'CatSkins'/'Skin_Manifest.json').read_text(encoding='utf8'))
fontpath='C:/Windows/Fonts/segoeuib.ttf'
def font(size): return ImageFont.truetype(fontpath,size)
sheet=Image.new('RGB',(2000,1960),'#151f2b'); draw=ImageDraw.Draw(sheet)
draw.text((34,22),'PAW ARENA / CAT SKIN LIBRARY',font=font(38),fill='#f4eee3')
draw.text((36,75),'19 material variants  |  One unchanged base, UV and rig  |  Actual Blender renders',font=font(22),fill='#a7b8c7')
for idx,skin in enumerate(manifest['skins']):
    name=skin['name']; slug=name.replace(' ','_')
    x=24+(idx%5)*396; y=126+(idx//5)*450
    draw.rounded_rectangle((x,y,x+376,y+430),radius=16,fill='#263443',outline='#526475',width=2)
    img=Image.open(OUT/f'{idx+1:02d}_{slug}_Front.png').convert('RGB').resize((372,372),Image.Resampling.LANCZOS)
    sheet.paste(img,(x+2,y+55))
    draw.text((x+14,y+12),f'{idx+1:02d}  {name}',font=font(25),fill='#f7f0e4')
    strip=Image.new('RGB',(1920,725),'#182432'); sd=ImageDraw.Draw(strip)
    sd.text((26,16),name.upper()+' / MATERIAL-ONLY SKIN',font=font(30),fill='#f7f1e7')
    for col,view in enumerate(['Front','ThreeQuarter','Back']):
        im=Image.open(OUT/f'{idx+1:02d}_{slug}_{view}.png').convert('RGB')
        strip.paste(im,(col*640,65)); sd.text((col*640+24,687),{'Front':'FRONT','ThreeQuarter':'3/4 VIEW','Back':'BACK'}[view],font=font(21),fill='#aebecd')
    strip.save(OUT/f'{idx+1:02d}_{slug}_Review.jpg',quality=94)
draw.text((36,1920),'Flat painted clothing / seams. Crystal and hologram are stylized surface treatments.',font=font(20),fill='#a7b8c7')
sheet.save(OUT/'00_All_19_Skins.jpg',quality=95)
print('SHEETS_READY',OUT/'00_All_19_Skins.jpg')
audit=json.loads((ROOT/'Exports'/'CatSkins'/'Skin_RoundTrip_Audit.json').read_text(encoding='utf8'))
assert audit['passed'] and len(audit['skin_checks'])==19 and len(audit['animation_checks'])==26
package=ROOT/'PawArena_Cat_Skins_Package.zip'
with zipfile.ZipFile(package,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    z.write(ROOT/'PawArena_Cat_Skins.blend','PawArena_Cat_Skins.blend')
    for path in (ROOT/'Exports'/'CatSkins').iterdir():
        if path.suffix in ['.png','.json','.md']: z.write(path,'Exports/CatSkins/'+path.name)
    for path in OUT.glob('*_Review.jpg'): z.write(path,'Previews/'+path.name)
    z.write(OUT/'00_All_19_Skins.jpg','Previews/00_All_19_Skins.jpg')
with zipfile.ZipFile(package) as z:
    assert z.testzip() is None
    assert len([n for n in z.namelist() if n.endswith('_BaseColor.png')])==19
print('PACKAGE_VALIDATED',package)
