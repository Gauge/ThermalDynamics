"""Pegasus appearance ladder: progressively relax thermal error, retaining geometry."""
import argparse
import json
import re
from pathlib import Path
import numpy as np
from PIL import Image
from continuous_surface import mesh, render
from optimize_surface import optimize


# main operation.
def main(source,out):
    out.mkdir(parents=True,exist_ok=True)
    scene=json.loads(source.read_text());nodes=scene['nodes']
    original,_=mesh(nodes,.45);reference=render(original,nodes);mask=np.isfinite(reference)
    lo=min(n['kelvin'] for n in nodes);hi=max(lo+50,max(n['kelvin'] for n in nodes))
    palette=np.array(re.findall(r'new Vector3\(([0-9.]+)f, ([0-9.]+)f, ([0-9.]+)f\)',Path('Thermodynamics/Presentation/ThermalVision/ThermalVisionPalette.cs').read_text())[:256],float)
    levels=[]
    for index,tolerance in enumerate((0,2,5,10,20,40,80,160,320)):
        triangles=original if index==0 else optimize(original,tolerance)
        values=reference if index==0 else render(triangles,nodes)
        np.testing.assert_array_equal(mask,np.isfinite(values))
        delta=np.abs(values[mask]-reference[mask])
        if delta.max()>tolerance+1e-5:raise AssertionError('Temperature bound exceeded')
        info=dict(level=index,toleranceK=tolerance,triangles=len(triangles),savedPercent=round(100*(1-len(triangles)/len(original)),1),maxErrorK=round(float(delta.max()),2),meanErrorK=round(float(delta.mean()),2))
        levels.append(info);print(info,flush=True)
        t=np.clip((np.nan_to_num(values,nan=lo)-lo)/(hi-lo),0,1);u=t*255;i=np.minimum(u.astype(int),254);f=(u-i)[...,None]
        for mode in ('colour','grey'):
            rgb=((1-f)*palette[i]+f*palette[i+1])*255 if mode=='colour' else np.repeat((18+225*t)[...,None],3,axis=2)
            rgb[~mask]=[5,8,12];Image.fromarray(np.uint8(rgb)).save(out/f'{index}-{mode}.png')
    (out/'metrics.json').write_text(json.dumps(levels,indent=2))
    html='''<!doctype html><meta charset="utf-8"><title>Pegasus simplification ladder</title>
<style>body{background:#111820;color:#e3edf5;font:18px/1.5 system-ui;margin:24px auto;max-width:1400px;padding:0 24px}h1{font-size:28px}button,select{font:inherit;padding:6px 14px;background:#263847;color:white;border:1px solid #60788a;border-radius:5px}input{width:min(600px,80%)}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}.pair img{width:100%;background:#05080c}h2{font-size:21px;margin-bottom:4px}.muted{color:#b1c2ce}.strip{display:grid;grid-template-columns:repeat(3,1fr);gap:12px}.strip img{width:100%}.strip button{padding:10px;text-align:left} @media(max-width:800px){.pair,.strip{grid-template-columns:1fr}}</style>
<h1>Pegasus — increasingly aggressive simplification</h1>
<p>Same temperature-interpolated surface method. Increase the level to merge more surfaces and allow more temperature error. The original stays on the left.</p>
<label>Palette <select id="palette"><option value="colour">Colour</option><option value="grey">Grayscale</option></select></label>
<p><button id="prev">Previous</button> <input aria-label="Simplification level" id="level" type="range" min="0" max="8" value="1" step="1"> <button id="next">Next</button></p>
<div class="pair"><section><h2>Original · 9,894 triangles</h2><p class="muted">Accepted smooth gradient</p><img id="original" alt="Original Pegasus thermal gradient"></section><section><h2 id="heading"></h2><p id="stats" class="muted"></p><img id="candidate" alt="Simplified Pegasus thermal gradient"></section></div>
<p id="error"></p><p class="muted">Pegasus, 60 s · near view · fixed temperature scale RANGE. Geometry and camera stay unchanged. Error is relative to the accepted smooth image, not to real-world temperature. Triangle counts cover this one synthetic ship, excluding native rendering overhead.</p>
<p class="muted">If the counts stop falling, this merge method has reached a geometry limit. A higher allowance does not necessarily produce more simplification or a strictly lower count.</p>
<h2>All levels</h2><div class="strip" id="strip"></div>
<script>const levels=DATA;const slider=document.querySelector('#level'),palette=document.querySelector('#palette');function update(){const n=+slider.value,l=levels[n],mode=palette.value;document.querySelector('#original').src='0-'+mode+'.png';document.querySelector('#candidate').src=n+'-'+mode+'.png';document.querySelector('#heading').textContent='Level '+n+' · '+l.triangles.toLocaleString()+' triangles';document.querySelector('#stats').textContent=l.savedPercent+'% fewer · allowance '+l.toleranceK+' K';document.querySelector('#error').textContent='Observed difference: average '+l.meanErrorK+' K; maximum '+l.maxErrorK+' K.';document.querySelector('#strip').innerHTML=levels.map(x=>'<button onclick="choose('+x.level+')">Level '+x.level+' · '+x.triangles.toLocaleString()+' triangles<img alt="Level '+x.level+' preview" src="'+x.level+'-'+mode+'.png">Up to '+x.toleranceK+' K allowed</button>').join('')}function choose(n){slider.value=Math.max(0,Math.min(8,n));update()}slider.oninput=update;palette.onchange=update;document.querySelector('#prev').onclick=()=>choose(+slider.value-1);document.querySelector('#next').onclick=()=>choose(+slider.value+1);update();</script>'''
    html=html.replace('RANGE',f'{lo-273.15:.1f} to {hi-273.15:.1f} °C').replace('DATA',json.dumps(levels))
    (out/'index.html').write_text(html)

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('source',type=Path);p.add_argument('output',type=Path);a=p.parse_args();main(a.source,a.output)
