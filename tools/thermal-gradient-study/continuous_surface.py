"""Synthetic world-space temperature interpolation on depth-tested block-bound surfaces.
No image blur. Not a native renderer or a GPU performance prediction.
"""
import argparse, json, math, re, time
from pathlib import Path
import numpy as np
from scipy.spatial import cKDTree
from PIL import Image, ImageDraw
from corpus_render import font
W,H=520,480

class Field:
#   init   operation.
    def __init__(self,nodes):
        self.centres=np.array([(np.array(n['min'])+n['max'])/2-.5 for n in nodes]);self.temp=np.array([n['kelvin'] for n in nodes]);self.tree=cKDTree(self.centres)
# sample operation.
    def sample(self,points,sigma):
        points=np.asarray(points);distance,index=self.tree.query(points,k=min(32,len(self.temp)))
        if len(self.temp)==1:return np.full(len(points),self.temp[0])
        weights=np.exp(-.5*(distance/sigma)**2)
        total=weights.sum(1);cold=total<1e-15
        result=(weights*self.temp[index]).sum(1)/np.maximum(total,1e-15)
        result[cold]=self.temp[index[cold,0]]
        return result

# faces operation.
def faces(nodes):
    pending={}
    for n in nodes:
        lo=np.array(n['min'],float)-.5;hi=np.array(n['max'],float)-.5
        for axis in range(3):
            axes=[a for a in range(3) if a!=axis]
            for side in (0,1):
                quad=[]
                for u,v in ((0,0),(1,0),(1,1),(0,1)):
                    p=lo.copy();p[axis]=hi[axis] if side else lo[axis]
                    p[axes[0]]=hi[axes[0]] if u else lo[axes[0]];p[axes[1]]=hi[axes[1]] if v else lo[axes[1]];quad.append(p)
                key=(axis,tuple(np.min(quad,axis=0)),tuple(np.max(quad,axis=0)))
                if key in pending: del pending[key]
                else: pending[key]=(np.array(quad),axis,side,n['kelvin'])
    return [f for f in pending.values() if f[2]==1] # fixed eye is positive on all axes

# mesh operation.
def mesh(nodes,sigma,budget=6000,field_nodes=None):
    field=Field(nodes if field_nodes is None else field_nodes);quads=faces(nodes)
    points=np.array([p for q,_,_,_ in quads for p in [*q,q.mean(0)]])
    samples=field.sample(points,sigma).reshape(-1,5)
    errors=np.abs(samples[:,4]-(samples[:,0]+samples[:,2])*.5)
    refine=set(np.argsort(-errors)[:max(0,(budget-2*len(quads))//2)])
    result=[]
    for i,((q,_,_,truth),t) in enumerate(zip(quads,samples)):
        if i in refine and errors[i]>2:
            for a in range(4):result.append((np.array([q[a],q[(a+1)%4],q.mean(0)]),np.array([t[a],t[(a+1)%4],t[4]]),truth))
        else:
            for ids in ((0,1,2),(0,2,3)):result.append((q[list(ids)],t[list(ids)],truth))
    return result,{'triangles':len(result),'baseTriangles':2*len(quads),'budget':budget,'overBudget':len(result)>budget,'refinedFaces':(len(result)-2*len(quads))//2,'sigmaGridUnits':sigma}

# render operation.
def render(triangles,nodes,reference=False,scale_factor=1):
    mins=np.array([n['min'] for n in nodes],float)-.5;maxs=np.array([n['max'] for n in nodes],float)-.5;centre=(mins.min(0)+maxs.max(0))/2
    eye=np.array([1.,.7,1.2]);eye/=np.linalg.norm(eye);right=np.cross([0,1,0],eye);right/=np.linalg.norm(right);up=np.cross(eye,right)
    bounds=np.array([[x,y,z] for x in [mins[:,0].min(),maxs[:,0].max()] for y in [mins[:,1].min(),maxs[:,1].max()] for z in [mins[:,2].min(),maxs[:,2].max()]])-centre
    scale=min((W-40)/np.ptp(bounds@right),(H-40)/np.ptp(bounds@up))*scale_factor
    depth=np.full((H,W),-np.inf);kelvin=np.full((H,W),np.nan)
    for xyz,temp,truth in triangles:
        q=xyz-centre;projected=np.column_stack((W/2+q@right*scale,H/2-q@up*scale,q@eye));a,b,c=projected
        x0=max(0,int(np.floor(projected[:,0].min())));x1=min(W-1,int(np.ceil(projected[:,0].max())))
        y0=max(0,int(np.floor(projected[:,1].min())));y1=min(H-1,int(np.ceil(projected[:,1].max())))
        if x0>x1 or y0>y1:continue
        yy,xx=np.mgrid[y0:y1+1,x0:x1+1];xx=xx+.5;yy=yy+.5
        den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
        if abs(den)<1e-12:continue
        wa=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den;wb=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den;wc=1-wa-wb
        z=wa*a[2]+wb*b[2]+wc*c[2];d=depth[y0:y1+1,x0:x1+1];mask=(wa>=-1e-8)&(wb>=-1e-8)&(wc>=-1e-8)&(z>d)
        d[mask]=z[mask];values=np.full_like(z,truth) if reference else wa*temp[0]+wb*temp[1]+wc*temp[2]
        kelvin[y0:y1+1,x0:x1+1][mask]=values[mask]
    return kelvin

# fixture operation.
def fixture(cooled):
    nodes=[]
    for x in range(24):
        for y in range(16):
            t=300+600*math.exp(-((x-12)**2+(y-8)**2)/24)
            if cooled:t=max(300,t-480*math.exp(-((x-12)**2+(y-8)**2)/3))
            nodes.append({'min':[x,y,0],'max':[x+1,y+1,1],'kelvin':t})
    return {'ship':'Controlled hot plate'+(' / cooled patch' if cooled else ''),'workshopId':'cooled' if cooled else 'hotspot','seconds':0,'nodes':nodes}

# main operation.
def main(data,out):
    out.mkdir(parents=True,exist_ok=True)
    palette=np.array(re.findall(r'new Vector3\(([0-9.]+)f, ([0-9.]+)f, ([0-9.]+)f\)',Path('Thermodynamics/Presentation/ThermalVision/ThermalVisionPalette.cs').read_text())[:256],float)
    pages=['<!doctype html><meta charset="utf-8"><title>Method: temperature-interpolated surface mesh</title><style>body{background:#111820;color:#dce7ef;font:17px/1.5 sans-serif;margin:28px}img{width:100%}a{color:#9de0fa}h2{margin-top:48px}table{border-collapse:collapse;width:100%}td,th{padding:12px;border:1px solid #405060;text-align:left}details{margin:24px 0;color:#b4c5d2}</style><h1>Method: temperature-interpolated surface mesh</h1><p><strong>Every set below compares the same proposed implementation against a per-block reference.</strong> The three right-hand images are different softness settings of this one method, not three competing implementations.</p><p>The method samples neighbouring block temperatures at surface vertices, interpolates temperature across each triangle, then maps that temperature to colour. This replaces flat temperature regions with a smooth field anchored to the heat sources.</p><table><tr><th>Column 1 — Reference</th><th>Column 2 — Proposed method / near</th><th>Column 3 — Proposed method / middle</th><th>Column 4 — Proposed method / far</th></tr><tr><td>Flat per-block temperature.<br>One measured temperature per block; no smoothing.</td><td>Temperature-interpolated mesh.<br>Narrow Gaussian sampling: σ 0.45 grid cells.</td><td>Same interpolated mesh.<br>Broader sampling: σ 0.90 grid cells.</td><td>Same interpolated mesh.<br>Broadest sampling: σ 2.25 grid cells.</td></tr></table><p>All four columns use the same temperature range and camera. Ship size is held constant so you can compare heat location and softness. Near/middle/far are proposed softness levels, not calibrated game distances.</p><details><summary>Implementation status and rendering cost</summary><p>This is an offline software renderer using block-bound surfaces and a depth buffer. It is not installed in the game. Samples use Gaussian weights over up to 32 nearest block centres within each grid; triangle interpolation precedes palette lookup. There is no image-space blur or lighting modulation.</p><p>Shared faces are removed and face-centre errors can trigger refinement within a 6,000-triangle target. Base geometry above that target is retained and flagged. Counts exclude native volume entry/exit passes, near caps and context. Native surface ownership, engine performance, temporal LOD stability and cross-grid occlusion remain unvalidated. Smoothing can weaken small hot or cooled patches.</p></details>']
    report=[]
    for s in [fixture(False),fixture(True)]+[json.loads(p.read_text()) for p in sorted(data.glob('*.json'))]:
        values=[n['kelvin'] for n in s['nodes']];lo=min(values);hi=max(lo+50,max(values))
        if s['workshopId'] in ('hotspot','cooled'):lo,hi=300,900
        images=[];stats=[];start=time.perf_counter()
        for distance in (0,.5,1):
            sigma=.45+1.8*distance*distance
            triangles,info=mesh(s['nodes'],sigma)
            if distance==0:images.append(render(triangles,s['nodes'],True))
            images.append(render(triangles,s['nodes']));stats.append(info)
        report.append({'ship':s['ship'],'seconds':s['seconds'],'levels':stats,'offlineSeconds':time.perf_counter()-start})
        for mode in ('colour','grey'):
            sheet=Image.new('RGB',(W*4,H+230),(17,24,32));d=ImageDraw.Draw(sheet)
            d.text((16,10),'METHOD: TEMPERATURE-INTERPOLATED SURFACE MESH',font=font(24),fill='white')
            d.text((16,44),s['ship']+' / '+str(s['seconds'])+' s / '+('Cividis colour' if mode=='colour' else 'White-hot grayscale'),font=font(20),fill='#c2d5e3')
            labels=['REFERENCE: flat per-block','INTERPOLATED MESH / near','INTERPOLATED MESH / middle','INTERPOLATED MESH / far']
            descriptions=['One temperature per block','Gaussian width: 0.45 grid cells','Gaussian width: 0.90 grid cells','Gaussian width: 2.25 grid cells']
            for col,(v,label) in enumerate(zip(images,labels)):
                mask=np.isfinite(v);t=np.clip((np.nan_to_num(v,nan=lo)-lo)/(hi-lo),0,1)
                u=t*255;i=np.minimum(np.floor(u).astype(int),254);f=(u-i)[...,None]
                rgb=((1-f)*palette[i]+f*palette[i+1])*255 if mode=='colour' else np.repeat((18+225*t)[...,None],3,axis=2)
                rgb[~mask]=[5,8,12];sheet.paste(Image.fromarray(np.uint8(np.clip(rgb,0,255))),(W*col,145));d.text((W*col+12,86),label,font=font(18),fill='white')
                d.text((W*col+12,113),descriptions[col],font=font(17),fill='#c2d5e3')
                if col:d.text((W*col+12,H+163),f"{stats[col-1]['triangles']} triangles"+(' / OVER BUDGET' if stats[col-1]['overBudget'] else ''),font=font(17),fill='#c2d5e3')
            d.text((16,H+200),f"Shared temperature scale: {lo-273.15:.1f} to {hi-273.15:.1f} C / left: block reference; right three: same method, increasing softness",font=font(16),fill='white')
            name=f"{s['workshopId']}-{s['seconds']}-{mode}.png";sheet.save(out/name);pages.append(f'<h2>Temperature-interpolated surface mesh — {s["ship"]} / {s["seconds"]} s / {mode}</h2><p>Left: flat per-block reference. Right three: the proposed mesh method at increasing sampling widths.</p><a href="{name}"><img src="{name}"></a>')
        print(s['ship'],[v['triangles'] for v in stats],flush=True)
    (out/'index.html').write_text('\n'.join(pages));(out/'metrics.json').write_text(json.dumps(report,indent=2))

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('data',type=Path);p.add_argument('output',type=Path);a=p.parse_args();main(a.data,a.output)
