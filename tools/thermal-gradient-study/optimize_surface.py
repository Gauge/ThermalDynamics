"""Merge adjacent coplanar thermal patches with bounded sampled temperature error."""
import argparse
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
from continuous_surface import mesh, render, fixture, W, H
from corpus_render import font


# interpolate operation.
def interpolate(points, lo, hi, axes, temperatures):
    u=(points[:,axes[0]]-lo[axes[0]])/(hi[axes[0]]-lo[axes[0]])
    v=(points[:,axes[1]]-lo[axes[1]])/(hi[axes[1]]-lo[axes[1]])
    a,b,c,d=temperatures
    return np.where(u>=v,a*(1-u)+b*(u-v)+c*v,a*(1-v)+c*u+d*(v-u))


# optimize operation.
def optimize(triangles,tolerance=2., reverse_axes=False):
    groups={}
    for xyz,temp,truth in triangles:
        axis=int(np.argmin(np.ptp(xyz,axis=0)))
        groups.setdefault((axis,float(xyz[0,axis])),[]).append((xyz,temp,truth))
    output=[]
    for (axis,plane),items in groups.items():
        axes=[a for a in range(3) if a!=axis]
        patches={}
        for xyz,temp,truth in items:
            lo=xyz.min(0);hi=xyz.max(0)
            key=(tuple(lo),tuple(hi))
            patches.setdefault(key,[]).append((xyz,temp,truth))
        active=[]
        for (low,high),parts in patches.items():
            lo=np.array(low);hi=np.array(high)
            area=sum(np.linalg.norm(np.cross(x[0][1]-x[0][0],x[0][2]-x[0][0]))*.5 for x in parts)
            full=np.prod((hi-lo)[axes])
            if len(parts)!=2 or abs(area-full)>1e-8:
                output.extend(parts);continue
            points=np.concatenate([x[0] for x in parts]);values=np.concatenate([x[1] for x in parts])
            active.append((lo,hi,points,values,parts))
        for sweep in range(12):
            changed=False
            for direction in (axes[::-1] if reverse_axes else axes):
                other=next(a for a in axes if a!=direction)
                buckets={}
                for patch in active:
                    lo,hi=patch[:2]
                    buckets.setdefault((lo[other],hi[other]),[]).append(patch)
                active=[]
                for bucket in buckets.values():
                    bucket.sort(key=lambda p:p[0][direction]);current=bucket[0]
                    for nxt in bucket[1:]:
                        lo,hi,points,values,parts=current
                        if abs(hi[direction]-nxt[0][direction])<1e-9:
                            upper=np.maximum(hi,nxt[1]);p=np.concatenate([points,nxt[2]]);t=np.concatenate([values,nxt[3]])
                            corners=[]
                            for u,v in ((0,0),(1,0),(1,1),(0,1)):
                                q=lo.copy();q[axes[0]]=upper[axes[0]] if u else lo[axes[0]];q[axes[1]]=upper[axes[1]] if v else lo[axes[1]];corners.append(q)
                            ts=np.array([t[np.flatnonzero(np.all(np.isclose(p,q,atol=1e-9,rtol=0),axis=1))[0]] for q in corners])
                            checks=[p];truths=[t]
                            allparts=parts+nxt[4]
                            for xyz,temps,_ in allparts:
                                uv=(xyz[:,axes]-lo[axes])/(upper[axes]-lo[axes]);sign=uv[:,0]-uv[:,1]
                                for i,j in ((0,1),(1,2),(2,0)):
                                    if sign[i]*sign[j]<0:
                                        f=sign[i]/(sign[i]-sign[j]);checks.append((xyz[i]+f*(xyz[j]-xyz[i]))[None,:]);truths.append(np.array([temps[i]+f*(temps[j]-temps[i])]))
                            if np.max(np.abs(interpolate(np.concatenate(checks),lo,upper,axes,ts)-np.concatenate(truths)))<=tolerance:
                                current=(lo,upper,p,t,allparts);changed=True;continue
                        active.append(current);current=nxt
                    active.append(current)
            if not changed:break
        for lo,hi,points,values,parts in active:
            if len(parts)==2:output.extend(parts);continue
            corners=[];temps=[]
            for u,v in ((0,0),(1,0),(1,1),(0,1)):
                q=lo.copy();q[axes[0]]=hi[axes[0]] if u else lo[axes[0]];q[axes[1]]=hi[axes[1]] if v else lo[axes[1]];corners.append(q)
                temps.append(values[np.flatnonzero(np.all(np.isclose(points,q,atol=1e-9,rtol=0),axis=1))[0]])
            for ids in ((0,1,2),(0,2,3)):output.append((np.array(corners)[list(ids)],np.array(temps)[list(ids)],0.))
    return output


# optimize best order operation.
def optimize_best_order(triangles,tolerance=20.):
    """Choose the smaller partition per plane, retaining original error bounds."""
    groups={}
    for triangle in triangles:
        xyz=triangle[0];axis=int(np.argmin(np.ptp(xyz,axis=0)))
        groups.setdefault((axis,float(xyz[0,axis])),[]).append(triangle)
    result=[]
    for parts in groups.values():
        a=optimize(parts,tolerance)
        b=optimize(parts,tolerance,reverse_axes=True)
        result.extend(a if len(a)<=len(b) else b)
    return result


# Removes the buried_faces.
def remove_buried_faces(triangles,nodes):
    """Cull only faces whose whole bounding rectangle has solid cells outside it.

    Applies to the positive-facing, integer block-bound proxy geometry used here.
    It is independent of pixel resolution and camera occlusion queries. Does not
    apply to detailed native block meshes, which can have holes within their bounds.
    """
    import itertools
    occupied=set()
    for node in nodes:
        low=np.asarray(node['min']);high=np.asarray(node['max'])
        if not (np.all(low==np.floor(low)) and np.all(high==np.floor(high))):
            raise ValueError('Buried-face removal requires integer block bounds')
        occupied.update(itertools.product(*(range(int(a),int(b)) for a,b in zip(low,high))))
    retained=[]
    for triangle in triangles:
        xyz=triangle[0];axis=int(np.argmin(np.ptp(xyz,axis=0)))
        lo=xyz.min(0)+.5;hi=xyz.max(0)+.5
        plane=lo[axis]
        if abs(plane-round(plane))>1e-8:
            retained.append(triangle);continue
        ranges=[range(int(np.floor(lo[a])),int(np.ceil(hi[a]))) if a!=axis else [int(round(plane))] for a in range(3)]
        if not all(cell in occupied for cell in itertools.product(*ranges)):
            retained.append(triangle)
    return retained


# cull occluded triangles operation.
def cull_occluded_triangles(triangles,eye_direction=(1.,.7,1.2)):
    """Conservative fixed-view occlusion: one nearer triangle must cover all of another.

    Analytic projected containment and depth, not pixel-centre visibility. Must be
    recomputed when the view changes; currently an offline cost experiment only.
    """
    if not triangles:return []
    eye=np.asarray(eye_direction,dtype=float);eye/=np.linalg.norm(eye)
    seed=np.array([0.,1.,0.]) if abs(eye[1])<.99 else np.array([1.,0.,0.])
    right=np.cross(seed,eye);right/=np.linalg.norm(right);up=np.cross(eye,right)
    xyz=np.array([t[0] for t in triangles]);xy=np.stack((xyz@right,xyz@up),axis=-1);depth=xyz@eye
    low=xy.min(1);high=xy.max(1);nearest=depth.max(1);farthest=depth.min(1);retained=[]
    for index,points in enumerate(xy):
        candidates=np.flatnonzero(np.all(low<=low[index]+1e-10,axis=1)&np.all(high>=high[index]-1e-10,axis=1)&(nearest>farthest[index]+1e-7))
        hidden=False
        for other in candidates:
            if other==index:continue
            a,b,c=xy[other];den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
            if abs(den)<1e-12:continue
            wa=((b[1]-c[1])*(points[:,0]-c[0])+(c[0]-b[0])*(points[:,1]-c[1]))/den
            wb=((c[1]-a[1])*(points[:,0]-c[0])+(a[0]-c[0])*(points[:,1]-c[1]))/den
            weights=np.stack((wa,wb,1-wa-wb),axis=1)
            if np.all(weights>=-1e-10) and np.all(weights@depth[other]>depth[index]+1e-7):
                hidden=True;break
        if not hidden:retained.append(triangles[index])
    return retained


# main operation.
def main(data,out,tolerance=2.,best_order=False,remove_buried=False,occlusion=False):
    out.mkdir(parents=True,exist_ok=True)
    import re
    palette=np.array(re.findall(r'new Vector3\(([0-9.]+)f, ([0-9.]+)f, ([0-9.]+)f\)',Path('Thermodynamics/Presentation/ThermalVision/ThermalVisionPalette.cs').read_text())[:256],float)
    page=['<meta charset="utf-8"><title>Optimized thermal surfaces</title><style>body{background:#111820;color:white;font:18px sans-serif;margin:28px}img{width:100%}</style><h1>Same gradients, fewer triangles</h1><p>Temperature-interpolated surface mesh. Adjacent flat surfaces merge only when their temperature error stays below TOLERANCE K. Hotspot refinements remain intact.</p><p>Left: accepted version. Middle: optimized. Right: temperature difference (black = identical, white = TOLERANCE K). These are offline comparisons; native rendering cost is not yet measured.</p>'];report=[]
    for s in [fixture(False),fixture(True)]+[json.loads(p.read_text()) for p in sorted(data.glob('*.json'))]:
        for label,sigma in [('near',.45),('far',2.25)]:
            original,_=mesh(s['nodes'],sigma)
            source=remove_buried_faces(original,s['nodes']) if remove_buried else original
            reduced=(optimize_best_order if best_order else optimize)(source,tolerance)
            before_occlusion=len(reduced)
            unculled=render(reduced,s['nodes']) if occlusion else None
            if occlusion:reduced=cull_occluded_triangles(reduced)
            a=render(original,s['nodes']);b=render(reduced,s['nodes']);mask=np.isfinite(a)
            if occlusion:np.testing.assert_allclose(unculled,b,equal_nan=True,atol=1e-8,rtol=0)
            if remove_buried:
                np.testing.assert_allclose(a,render(source,s['nodes']),equal_nan=True,atol=1e-8,rtol=0)
            if not np.array_equal(mask,np.isfinite(b)):raise AssertionError('Silhouette changed')
            error=float(np.max(np.abs(a[mask]-b[mask])))
            if error>tolerance+1e-5:raise AssertionError(f'Pixel temperature error {error}')
            stats={'ship':s['ship'],'seconds':s['seconds'],'level':label,'before':len(original),'occludedTrianglesRemoved':before_occlusion-len(reduced),'buriedTrianglesRemoved':len(original)-len(source),'after':len(reduced),'savedPercent':100*(1-len(reduced)/len(original)),'maxErrorK':error,'meanErrorK':float(np.mean(np.abs(a[mask]-b[mask])))};report.append(stats);print(stats,flush=True)
            lo=min(n['kelvin'] for n in s['nodes']);hi=max(lo+50,max(n['kelvin'] for n in s['nodes']))
            if s['workshopId'] in ('hotspot','cooled'):lo,hi=300,900
            for mode in ('colour','grey'):
                sheet=Image.new('RGB',(W*3,H+110),(17,24,32));draw=ImageDraw.Draw(sheet)
                draw.text((12,8),f"{s['ship']} / {s['seconds']} s / {label} / {mode}",font=font(22),fill='white')
                for col,values in enumerate((a,b,np.abs(a-b))):
                    t=np.clip((np.nan_to_num(values,nan=lo)-lo)/(hi-lo),0,1)
                    u=t*255;i=np.minimum(u.astype(int),254);f=(u-i)[...,None]
                    rgb=((1-f)*palette[i]+f*palette[i+1])*255 if mode=='colour' else np.repeat((18+225*t)[...,None],3,axis=2)
                    if col==2:rgb=np.repeat(np.clip(np.nan_to_num(values)/tolerance,0,1)[...,None]*255,3,axis=2)
                    rgb[~mask]=[5,8,12];sheet.paste(Image.fromarray(np.uint8(rgb)),(W*col,75))
                for col,text in enumerate((f'Original: {len(original):,} triangles',f'Optimized: {len(reduced):,} triangles',f'Difference: 0 to {tolerance:g} K')):draw.text((W*col+12,44),text,font=font(20),fill='white')
                draw.text((12,H+82),f"{stats['savedPercent']:.1f}% fewer triangles / maximum difference {error:.2f} K / unchanged silhouette",font=font(18),fill='white')
                name=f"{s['workshopId']}-{s['seconds']}-{label}-{mode}.png";sheet.save(out/name);page.append(f'<img src="{name}">')
    if occlusion:
        page.insert(1,'<p>Additional optimization: analytically remove triangles fully covered by a nearer triangle for this camera. No pixel-based culling. Visibility must be recomputed as the camera moves; native preparation cost remains unmeasured.</p>')
    if remove_buried:
        page.insert(1,'<p>Method: temperature-interpolated surface mesh with covered internal faces removed before merging. Coverage uses solid block bounds in this synthetic model; native meshes can have openings and need separate validation.</p>')
    (out/'metrics.json').write_text(json.dumps(report,indent=2));(out/'index.html').write_text('\n'.join(page).replace('TOLERANCE',f'{tolerance:g}'))

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('data',type=Path);p.add_argument('output',type=Path);p.add_argument('--tolerance',type=float,default=2.);p.add_argument('--best-order',action='store_true');p.add_argument('--remove-buried',action='store_true');p.add_argument('--occlusion',action='store_true');a=p.parse_args();main(a.data,a.output,a.tolerance,a.best_order,a.remove_buried,a.occlusion)
