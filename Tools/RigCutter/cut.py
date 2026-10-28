import sys,json,os
sys.path.insert(0,'../proc2')
import numpy as np
from PIL import Image
from scipy import ndimage as ndi
from key import key_green
SH={'PLAYER':dict(A=('maya_A',['arms_crossed','phone','hands_hips']),B=('maya_B',['hands_back','hands_clasped','hand_pocket']),W='maya_walk'),
    'LEO':dict(A=('leo_A',['arms_crossed','phone','hands_hips']),B=('leo_B',['hands_back','hands_clasped','hand_pocket']),W='leo_walk'),
    'MAYA':dict(A=('player_A',['arms_crossed','phone','hands_hips']),B=('player_B',['hands_back','hands_clasped','hand_pocket']),W='player_walk')}
def panels(img,n,label):
    W,H=img.size; out=[]
    for i in range(n):
        pw=W//n; x0=i*pw+6; x1=x0+(790 if n==3 else 588)
        p=np.array(img.crop((x0,0,x1,H)))
        m=p[...,3]>128
        if label: m[:150]=False; 
        lab,k=ndi.label(m)
        sz=ndi.sum(m,lab,range(1,k+1)); keep=np.isin(lab,[j+1 for j,s in enumerate(sz) if s>1500])
        keep=ndi.binary_dilation(keep,iterations=3)
        p[...,3]=np.where(keep,p[...,3],0)
        out.append(Image.fromarray(p))
    return out
res={}
for cid,d in SH.items():
    os.makedirs(f'out/{cid}',exist_ok=True)
    items=[]
    for k in 'AB':
        f,names=d[k]; items+=list(zip(names,panels(key_green(f'src/{f}.jpg'),3,True)))
    w=panels(key_green(f"src/{d['W']}.jpg"),4,False)
    items+=[(f'walk{i}',p) for i,p in enumerate(w)]
    # union bbox for poses and for walk separately
    def ub(sel):
        bs=[np.array(p)[...,3].astype(bool) for n,p in items if sel(n)]
        ys=np.where(np.any([b.any(1) for b in bs],0))[0]; xs=np.where(np.any([b.any(0) for b in bs],0))[0]
        return xs[0],ys[0],xs[-1]+1,ys[-1]+1
    meta={}
    for nm,sel in (('pose',lambda n:not n.startswith('walk')),('walk',lambda n:n.startswith('walk'))):
        x0,y0,x1,y1=ub(sel); meta[nm]=[int(x0),int(y0),int(x1),int(y1)]
        for n,p in items:
            if sel(n): p.crop((x0,y0,x1,y1)).save(f'out/{cid}/{n}.png')
    json.dump(meta,open(f'out/{cid}/meta.json','w')); print(cid,meta)
