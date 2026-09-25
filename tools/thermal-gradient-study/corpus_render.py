"""Render real corpus solver snapshots as block-bound proxies, without launching Space Engineers."""
import argparse
import heapq
import html
import json
import math
import re
import subprocess
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from study import BinaryCell

W,H=640,580
FONT=subprocess.check_output(['fc-match','-f','%{file}','sans'],text=True).strip()
# font operation.
def font(n): return ImageFont.truetype(FONT,n)

# estimates operation.
def estimates(nodes):
    points=[tuple(n['position'])+(n['kelvin'],) for n in nodes]
    assert len(set(p[:3] for p in points)) == len(points)
    width=1
    while True:
        groups={}
        for p in points:
            k=tuple(math.floor(v/width) for v in p[:3]); groups[k]=max(groups.get(k,-math.inf),p[3])
        if len(groups)<=512: break
        width*=2
    uniform=np.array([groups[tuple(math.floor(v/width) for v in p[:3])] for p in points])
    origin=tuple(min(p[a] for p in points) for a in range(3))
    extent=max(max(p[a] for p in points)-origin[a]+1 for a in range(3))
    size=2**math.ceil(math.log2(max(1,extent)))
    root=BinaryCell(origin,(size,)*3,points)
    leaves={0:root};queue=[(-root.error,0,root)];serial=1
    while queue:
        _,key,cell=heapq.heappop(queue)
        if cell.width==1 or cell.error<1e-6:continue
        children=cell.split()
        if len(leaves)-1+len(children)>512:continue
        del leaves[key]
        for c in children:
            leaves[serial]=c;heapq.heappush(queue,(-c.error,serial,c));serial+=1
    predicted={p[:3]:c.peak for c in leaves.values() for p in c.points}
    directed=np.array([predicted[p[:3]] for p in points])
    truth=np.array([p[3] for p in points])
    return [truth,uniform,directed],{'uniformCells':len(groups),'uniformWidthBlocks':width,'directedCells':len(leaves),
        'uniformMeanErrorK':float(np.mean(uniform-truth)),'directedMeanErrorK':float(np.mean(directed-truth))}

# raster operation.
def raster(nodes,cutaway=False):
    mins=np.array([n['min'] for n in nodes],float)-.5
    maxs=np.array([n['max'] for n in nodes],float)-.5
    centre=(mins.min(0)+maxs.max(0))/2
    eye=np.array([1.,.7,1.2]);eye/=np.linalg.norm(eye)
    right=np.cross([0,1,0],eye);right/=np.linalg.norm(right);up=np.cross(eye,right)
    corners=np.array([[x,y,z] for x in [mins[:,0].min(),maxs[:,0].max()] for y in [mins[:,1].min(),maxs[:,1].max()] for z in [mins[:,2].min(),maxs[:,2].max()]])-centre
    scale=min((W-50)/np.ptp(corners@right),(H-45)/np.ptp(corners@up))
    owner=np.full((H,W),-1,int);depth=np.full((H,W),-np.inf);shade=np.ones((H,W))
    for index,(lo,hi) in enumerate(zip(mins,maxs)):
        if cutaway and ((lo+hi)/2)[2]>centre[2]:continue
        for axis in range(3):
            side=eye[axis]>0
            axes=[a for a in range(3) if a!=axis]
            quad=[]
            for u,v in [(0,0),(1,0),(1,1),(0,1)]:
                p=lo.copy();p[axis]=hi[axis] if side else lo[axis]
                p[axes[0]]=hi[axes[0]] if u else lo[axes[0]]
                p[axes[1]]=hi[axes[1]] if v else lo[axes[1]]
                q=p-centre;quad.append([W/2+q@right*scale,H/2-q@up*scale,q@eye])
            for ids in [(0,1,2),(0,2,3)]:
                a,b,c=np.array([quad[i] for i in ids])
                xmin=max(0,int(math.floor(min(a[0],b[0],c[0]))));xmax=min(W-1,int(math.ceil(max(a[0],b[0],c[0]))))
                ymin=max(0,int(math.floor(min(a[1],b[1],c[1]))));ymax=min(H-1,int(math.ceil(max(a[1],b[1],c[1]))))
                if xmin>xmax or ymin>ymax:continue
                yy,xx=np.mgrid[ymin:ymax+1,xmin:xmax+1];xx=xx+.5;yy=yy+.5
                den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
                if abs(den)<1e-10:continue
                wa=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den
                wb=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den
                wc=1-wa-wb;z=wa*a[2]+wb*b[2]+wc*c[2]
                view=depth[ymin:ymax+1,xmin:xmax+1];mask=(wa>=-1e-8)&(wb>=-1e-8)&(wc>=-1e-8)&(z>view)
                view[mask]=z[mask];owner[ymin:ymax+1,xmin:xmax+1][mask]=index
                shade[ymin:ymax+1,xmin:xmax+1][mask]=[.94,1.,.88][axis]
    assert (owner>=0).any()
    return owner,shade

# main operation.
def main(data,out,palette_source,flat=False):
    out.mkdir(parents=True,exist_ok=True)
    files=sorted(data.glob('*.json')); snapshots=sorted([json.loads(p.read_text()) for p in files],key=lambda s:(s['workshopId'],s['seconds']))
    matches=re.findall(r'new Vector3\(([0-9.]+)f, ([0-9.]+)f, ([0-9.]+)f\)',palette_source.read_text())
    palette=np.array(matches[:256],float);assert len(palette)==256
    ranges={}
    for s in snapshots:
        ranges.setdefault(s['workshopId'],[]).extend(n['kelvin'] for n in s['nodes'])
    ranges={k:(min(v)-2,max(min(v)+20,max(v)+2)) for k,v in ranges.items()}
    index=['<!doctype html><meta charset="utf-8"><title>Corpus thermal appearance lab</title><style>body{background:#111820;color:#dde6ec;font:17px sans-serif;margin:32px}img{width:100%;max-width:1920px}a{color:#9dd7ef}section{margin-bottom:40px}</style><h1>Corpus thermal appearance lab</h1><p>Real blueprints and solver temperatures. Offline block-bound proxies, not native game screenshots. Each comparison has identical camera, exposure and geometry. Surface shading is an illustrative 0–12% cue, not an implemented shader.</p><p>Reference = individual simulated block temperatures. Uniform = coarse maximum groups. Directed = temperature-driven partition assigned at block positions, not a native volume-rendering prediction. Cutaways deliberately remove half the hull.</p>']
    if flat:
        index[0]=index[0].replace("Surface shading is an illustrative 0–12% cue, not an implemented shader.", "FLAT TEMPERATURE COLOURS: no illustrative surface shading. These remain offline geometry proxies.")
    report=[]
    for s in snapshots:
        fields,stats=estimates(s['nodes']);lo,hi=ranges[s['workshopId']]
        report.append(dict(ship=s['ship'],scenario=s['scenario'],seconds=s['seconds'],rangeKelvin=[lo,hi],**stats))
        for cut in (False,True):
            owner,shade=raster(s['nodes'],cut);mask=owner>=0;safe=np.maximum(owner,0)
            if flat: shade=np.ones_like(shade)
            for mode in ('grey','colour'):
                sheet=Image.new('RGB',(W*3,H+160),(17,24,32));d=ImageDraw.Draw(sheet)
                title=f"{s['ship']} | {s['scenario']} | {s['seconds']} s | {'CUTAWAY' if cut else 'EXTERIOR'} | {mode.upper()}"
                d.text((18,12),title,font=font(23),fill='#eef4f8')
                d.text((18,43),f"CORPUS SIMULATION / BLOCK-BOUND GEOMETRY / {s['blocks']} blocks / {'FLAT' if flat else 'SHADED PROXY'} / scale {lo-273.15:.1f} to {hi-273.15:.1f} C",font=font(16),fill='#aebfca')
                for column,(label,values) in enumerate(zip(('Reference: per-block temperature',f"Uniform maximum: {stats['uniformCells']} cells",f"Directed maximum: {stats['directedCells']} cells"),fields)):
                    v=np.clip((values[safe]-lo)/(hi-lo),0,1)
                    rgb=np.repeat((18+225*v)[:,:,None],3,axis=2) if mode=='grey' else palette[np.rint(v*255).astype(int)]*255
                    rgb*=shade[:,:,None];rgb[~mask]=[5,8,12]
                    sheet.paste(Image.fromarray(np.uint8(np.clip(rgb,0,255))),(column*W,110))
                    d.text((column*W+18,80),label,font=font(20),fill='white')
                for x in range(500):
                    v=x/499;c=(int(18+225*v),)*3 if mode=='grey' else tuple((palette[round(v*255)]*255).astype(int))
                    d.line((20+x,H+126,20+x,H+139),fill=c)
                d.text((535,H+122),f"{lo-273.15:.1f} C cold  to  {hi-273.15:.1f} C hot | fixed across this ship's phases",font=font(16),fill='#cbd8e0')
                name=f"{s['workshopId']}-{s['seconds']}-{'cutaway' if cut else 'exterior'}-{mode}.png"
                sheet.save(out/name)
                index.append(f'<section><h2>{html.escape(title)}</h2><a href="{name}"><img src="{name}" loading="lazy"></a></section>')
    (out/'index.html').write_text('\n'.join(index));(out/'metrics.json').write_text(json.dumps(report,indent=2))
    print(json.dumps(report,indent=2));print('Gallery:',out/'index.html')

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('data',type=Path);p.add_argument('output',type=Path)
    p.add_argument('--palette',type=Path,default=Path('Thermodynamics/Presentation/ThermalVision/ThermalVisionPalette.cs'))
    p.add_argument('--flat',action='store_true')
    a=p.parse_args();main(a.data,a.output,a.palette,a.flat)
