import numpy as np, glob, json, os
from PIL import Image
from scipy.signal import fftconvolve
D=4; S={'PLAYER':0.70,'LEO':0.70,'MAYA':0.69}
out={}
for cid in ['MAYA','LEO','PLAYER']:
    head=Image.open(f'../proc2/out2/{cid}/head.png'); s=S[cid]
    hs=head.resize((int(head.width*s),int(head.height*s)),Image.BILINEAR)
    keep=int(hs.height*0.80); M=(np.asarray(hs)[:keep,:,3]>128)[::D,::D].astype(np.float32)
    for f in sorted(glob.glob(f'out/{cid}/*.png')):
        n=os.path.basename(f)[:-4]
        if n.startswith('walk'): continue
        P=np.asarray(Image.open(f))[...,3]>128; ys=np.where(P.any(1))[0]; y0,y1=ys[0],ys[-1]
        top=P[y0:y0+int(0.26*(y1-y0))][::D,::D].astype(np.float32)
        inter=fftconvolve(top,M[::-1,::-1],mode='valid'); iou=inter/(M.sum()+top.sum()-inter+1e-6)
        iou[12:]=0  # head sits at the top of the figure
        j=np.unravel_index(np.argmax(iou),iou.shape)
        out.setdefault(cid,{})[n]=dict(x=int(j[1]*D),y=int(y0+j[0]*D),s=s,iou=round(float(iou[j]),3),y1=int(y1))
    print(cid,out[cid])
json.dump(out,open('place.json','w'))
