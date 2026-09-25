"""Build an actual coarse geometry ladder from corpus block bounds and original heat samples."""
import argparse,json,re,math
from pathlib import Path
import numpy as np
from PIL import Image
from continuous_surface import mesh,render
from optimize_surface import optimize_best_order,remove_buried_faces
from distance_lod import coarse_nodes


# main operation.
def main(data,out):
    out.mkdir(parents=True,exist_ok=True);result=[]
    palette=np.array(re.findall(r'new Vector3\(([0-9.]+)f, ([0-9.]+)f, ([0-9.]+)f\)',Path('Thermodynamics/Presentation/ThermalVision/ThermalVisionPalette.cs').read_text())[:256],float)
    for file in ('605516903-60.json','1650715158-60.json','2771284480-60.json'):
        s=json.loads((data/file).read_text());nodes=s['nodes'];levels=[]
        span=np.ptp(np.array([n['min'] for n in nodes]+[n['max'] for n in nodes]),axis=0)
        lo=min(n['kelvin'] for n in nodes);hi=max(lo+50,max(n['kelvin'] for n in nodes))
        for index,cell in enumerate((0,.5,1,2,4,8,16,32,64,128)):
            geo=nodes if cell<=1 else coarse_nodes(nodes,cell)
            tris,_=mesh(geo,.45 if cell<=1 else max(.45,cell*.3),budget=6000,field_nodes=nodes)
            if cell==.5:
                source=remove_buried_faces(tris,geo)
                eye=np.array([1.,.7,1.2]);eye/=np.linalg.norm(eye)
                order=sorted(range(len(source)),key=lambda k:-float(source[k][0].mean(0)@eye))
                protected=set(order[:max(1,len(source)//4)])
                tris=[t for k,t in enumerate(source) if k in protected]+optimize_best_order([t for k,t in enumerate(source) if k not in protected],20)
            elif index:tris=optimize_best_order(remove_buried_faces(tris,geo),20)
            values=render(tris,nodes);mask=np.isfinite(values)
            t=np.clip((np.nan_to_num(values,nan=lo)-lo)/(hi-lo),0,1);u=t*255;i=np.minimum(u.astype(int),254);f=(u-i)[...,None]
            for mode in ('colour','grey'):
                rgb=((1-f)*palette[i]+f*palette[i+1])*255 if mode=='colour' else np.repeat((18+225*t)[...,None],3,axis=2)
                rgba=np.dstack((np.uint8(rgb),np.uint8(mask)*255));Image.fromarray(rgba).save(out/f'{s["workshopId"]}-{index}-{mode}.png')
            levels.append(dict(triangles=len(tris),cell=cell,errorMetres=0 if cell<=1 else math.sqrt(3)*cell*2.5,index=index,name=('Full source' if cell==0 else 'Closest surfaces full' if cell==.5 else 'Merged source' if cell==1 else f'Coarse cells {cell}')))
        geo=[{'min':np.min([n['min'] for n in nodes],axis=0).tolist(),'max':np.max([n['max'] for n in nodes],axis=0).tolist(),'kelvin':0}]
        tris,_=mesh(geo,max(span),budget=6,field_nodes=nodes);values=render(tris,nodes);mask=np.isfinite(values)
        t=np.clip((np.nan_to_num(values,nan=lo)-lo)/(hi-lo),0,1);u=t*255;i=np.minimum(u.astype(int),254);f=(u-i)[...,None]
        for mode in ('colour','grey'):
            rgb=((1-f)*palette[i]+f*palette[i+1])*255 if mode=='colour' else np.repeat((18+225*t)[...,None],3,axis=2)
            Image.fromarray(np.dstack((np.uint8(rgb),np.uint8(mask)*255))).save(out/f'{s["workshopId"]}-10-{mode}.png')
        levels.append(dict(triangles=len(tris),cell=float(max(span)),errorMetres=float(np.linalg.norm(span)*2.5),index=10,name='Distant box'))
        result.append(dict(name=s['ship'],id=s['workshopId'],spanMetres=float(np.linalg.norm(span)*2.5),levels=levels))
        print(s['ship'],[l['triangles'] for l in levels],flush=True)
    (out/'data.json').write_text(json.dumps(result,indent=2))
    template=Path(__file__).with_name('distance_preview.html').read_text()
    (out/'index.html').write_text(template.replace('CORPUS_DATA',json.dumps(result)))

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('data',type=Path);p.add_argument('output',type=Path);a=p.parse_args();main(a.data,a.output)
