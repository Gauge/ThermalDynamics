"""Compare the native mixed-detail estimator on corpus block-bound proxies (not game screenshots)."""
import argparse
import html
import json
from pathlib import Path
import re
import numpy as np
from PIL import Image, ImageDraw
from corpus_render import raster, font, W, H

# main operation.
def main(data, output):
    output.mkdir(parents=True, exist_ok=True)
    source = Path('Thermodynamics/Presentation/ThermalVision/ThermalVisionPalette.cs').read_text()
    palette = np.array(re.findall(r'new Vector3\(([0-9.]+)f, ([0-9.]+)f, ([0-9.]+)f\)', source)[:256],float)
    pages = ['<!doctype html><meta charset="utf-8"><title>Mixed thermal detail</title><style>body{background:#111820;color:#dde6ec;font:17px sans-serif;margin:28px}img{width:100%}a{color:#9dd7ef}</style><h1>Mixed thermal detail evaluation</h1><p>Real corpus simulations and the same C# estimator used by the mod. Left: exact block temperatures. Other columns: budgets of 128, 512 and 1400 regions. These are independent per-grid budgets, not one simultaneous fleet or guaranteed distance thresholds.</p><p>Offline block-bound geometry, flat colours, shared exposure. Estimated values are sampled at block centres; native volume intersections, multiple rotated grids, motion and GPU performance still require game validation. Refinement eye is beyond the maximum XYZ corner; all blocks are eligible for detail in this comparison. Dark background has no measurement.</p>']
    metrics=[]
    for path in sorted(data.glob('*.json')):
        s=json.loads(path.read_text()); truth=np.array([n['kelvin'] for n in s['nodes']]);lo=float(truth.min());hi=max(lo+50,float(truth.max()))
        estimates=s['mixedDetail'];metrics.append({'ship':s['ship'],'seconds':s['seconds'],'levels':[{k:v for k,v in f.items() if k!='kelvin'} for f in estimates]})
        for mode in ('colour','grey'):
            owner,_=raster(s['nodes']);mask=owner>=0;safe=np.maximum(owner,0)
            sheet=Image.new('RGB',(W*4,H+170),(17,24,32));draw=ImageDraw.Draw(sheet)
            draw.text((18,12),f"{s['ship']} / {s['seconds']} s / {mode.upper()} / OFFLINE BLOCK-BOUND PROXY",font=font(23),fill='white')
            draw.text((18,46),'Exact nearby blocks + volume-weighted coarse temperatures. No illustrative lighting.',font=font(18),fill='#b6c7d5')
            fields=[truth]+[np.array(f['kelvin']) for f in estimates]
            labels=['Reference: exact per block']+[f"Budget {f['budget']}: {f['RefinedBlocks']} exact blocks" for f in estimates]
            for col,(values,label) in enumerate(zip(fields,labels)):
                v=np.clip((values[safe]-lo)/(hi-lo),0,1)
                rgb=palette[np.rint(v*255).astype(int)]*255 if mode=='colour' else np.repeat((18+225*v)[:,:,None],3,axis=2)
                rgb[~mask]=[5,8,12];sheet.paste(Image.fromarray(np.uint8(rgb)),(W*col,110))
                draw.text((W*col+16,80),label,font=font(19),fill='white')
                if col: draw.text((W*col+16,H+119),f"Mean block error {estimates[col-1]['meanErrorK']:.1f} K",font=font(17),fill='#b6c7d5')
            draw.text((18,H+144),f"Shared range: {lo-273.15:.1f} to {hi-273.15:.1f} C. Higher budgets need not occur at fixed distances.",font=font(17),fill='white')
            name=f"{s['workshopId']}-{s['seconds']}-{mode}.png";sheet.save(output/name)
            pages.append(f'<h2>{html.escape(s["ship"])} / {s["seconds"]} s / {mode}</h2><a href="{name}"><img src="{name}"></a>')
    (output/'index.html').write_text('\n'.join(pages));(output/'metrics.json').write_text(json.dumps(metrics,indent=2))

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('data',type=Path);p.add_argument('output',type=Path);a=p.parse_args();main(a.data,a.output)
