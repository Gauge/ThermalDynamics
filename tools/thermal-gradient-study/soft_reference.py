"""Appearance target only: normalized temperature blur on each isolated corpus ship's visible surfaces."""
import argparse, json, re
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
from corpus_render import raster, font, W, H

# blur operation.
def blur(values, mask, sigma):
    radius=int(np.ceil(3*sigma));x=np.arange(-radius,radius+1);kernel=np.exp(-.5*(x/sigma)**2);kernel/=kernel.sum()
# filt operation.
    def filt(a):
        for axis in (0,1):
            a=np.apply_along_axis(lambda row: np.convolve(np.pad(row,(radius,radius)),kernel,'valid'),axis,a)
        return a
    weights=filt(mask.astype(float));weighted=filt(np.where(mask,values,0))
    return np.divide(weighted,weights,out=np.zeros_like(weighted),where=weights>1e-12)

# main operation.
def main(data,out):
    out.mkdir(parents=True,exist_ok=True)
    src=Path('Thermodynamics/Presentation/ThermalVision/ThermalVisionPalette.cs').read_text()
    palette=np.array(re.findall(r'new Vector3\(([0-9.]+)f, ([0-9.]+)f, ([0-9.]+)f\)',src)[:256],float)
    html=['<!doctype html><meta charset="utf-8"><title>Thermal softness target</title><style>body{background:#111820;color:#dde6ec;font:17px sans-serif;margin:28px}img{width:100%}a{color:#9dd7ef}</style><h1>Heat-location-preserving softness: appearance target</h1><p>Offline reference, NOT implemented game rendering. Real corpus temperatures on simplified block-bound surfaces. Temperature is blurred before palette mapping, independently within each ship silhouette. No colours spread into the background. All columns use the same geometry, exposure and display size so softness is easy to compare. Blur widths are illustrative, not distance calibration.</p><p>Near keeps exact visible block readings. Farther views should reduce detail by softening this same spatial pattern. Broad blurred areas can hide small hot or cooled patches; native occlusion boundaries, separate grids and interiors need additional treatment. This image-space reference is not directly available through the Workshop API. The next renderer candidate is temperature-encoded per-vertex UVs sampling a palette ramp, retaining native depth testing.</p>']
    for path in sorted(data.glob('*.json')):
        s=json.loads(path.read_text());temps=np.array([n['kelvin'] for n in s['nodes']]);lo=temps.min();hi=max(lo+50,temps.max());owner,_=raster(s['nodes']);mask=owner>=0;field=temps[np.maximum(owner,0)]
        variants=[field,blur(field,mask,2),blur(field,mask,6)]
        for mode in ('colour','grey'):
            sheet=Image.new('RGB',(W*3,H+140),(17,24,32));d=ImageDraw.Draw(sheet)
            d.text((18,12),f"{s['ship']} / {s['seconds']} s / {mode.upper()} / APPEARANCE TARGET ONLY",font=font(22),fill='white')
            for i,(v,label) in enumerate(zip(variants,['Near: exact temperatures','Medium: gentle softness (2 px)','Far: broad softness (6 px)'])):
                t=np.clip((v-lo)/(hi-lo),0,1)
                rgb=palette[np.rint(t*255).astype(int)]*255 if mode=='colour' else np.repeat((18+225*t)[:,:,None],3,axis=2)
                rgb[~mask]=[5,8,12];sheet.paste(Image.fromarray(np.uint8(rgb)),(W*i,90));d.text((W*i+18,57),label,font=font(20),fill='white')
            d.text((18,H+106),f"Temperature-space blur / common range {lo-273.15:.1f} to {hi-273.15:.1f} C / offline proxy, not native validation",font=font(17),fill='#c7d8e7')
            name=f"{s['workshopId']}-{s['seconds']}-{mode}.png";sheet.save(out/name);html.append(f'<h2>{s["ship"]} / {s["seconds"]} s / {mode}</h2><a href="{name}"><img src="{name}"></a>')
    (out/'index.html').write_text('\n'.join(html))

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('data',type=Path);p.add_argument('output',type=Path);a=p.parse_args();main(a.data,a.output)
