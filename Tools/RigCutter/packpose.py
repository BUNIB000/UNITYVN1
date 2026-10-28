import json,glob,os,numpy as np
from PIL import Image
OUT='../web/demo_art'; pl=json.load(open('place.json'))
man=json.load(open(f'{OUT}/manifest.json'))
for k in [k for k in man if '/pose/' in k]: man.pop(k); 
for cid in ['MAYA','LEO','PLAYER']:
    head=Image.open(f'../proc2/out2/{cid}/head.png'); meta={'poses':{}}
    fs=[]
    for n,p in pl[cid].items():
        s=p['s']; f=0.6/s; fs.append(f)
        im=Image.open(f'out/{cid}/{n}.png').convert('RGBA')
        hw,hh=head.width*s,head.height*s
        cut=int(p["y"]+hh*0.95)
        a=np.array(im); a[:max(cut,0),:,3]=0; im=Image.fromarray(a)
        im=im.resize((round(im.width*f),round(im.height*f)),Image.LANCZOS)
        bb=im.getchannel('A').point(lambda v:255 if v>8 else 0).getbbox()
        im.save(f'{OUT}/char_{cid}_pose_{n}.png')
        meta['poses'][n]=dict(cx=round((p['x']+hw/2)*f,1),yb=round((p['y']+hh)*f,1),y1=round(p['y1']*f,1))
        man[f'char/{cid}/pose/{n}']=f'char_{cid}_pose_{n}.png'
    f0=float(np.median(fs)); hb=pl[cid]['hands_back']; Hf=hb['y1']-0   # front figure height in pose px (top of box is 0 for the tallest hair)
    ws=[Image.open(f'out/{cid}/walk{i}.png').convert('RGBA') for i in range(4)]
    Hw=np.mean([np.asarray(ws[i])[...,3].astype(bool).any(1).nonzero()[0][-1]-np.asarray(ws[i])[...,3].astype(bool).any(1).nonzero()[0][0] for i in (1,3)])
    ys=np.asarray(Image.open(f'out/{cid}/hands_back.png'))[...,3].astype(bool).any(1).nonzero()[0]; Hfront=ys[-1]-ys[0]
    fw=0.6/hb['s']*Hfront/Hw
    for i,w in enumerate(ws):
        w=w.resize((round(w.width*fw),round(w.height*fw)),Image.LANCZOS); w.save(f'{OUT}/char_{cid}_pose_walk{i}.png'); man[f'char/{cid}/pose/walk{i}']=f'char_{cid}_pose_walk{i}.png'
    meta['walk']=dict(n=4,w=w.width,h=w.height,f=round(fw,3)); meta['ground']=round(hb['y1']*0.6/hb['s'],1)-round(hb['yb'] if 'yb' in hb else 0,1) if False else None
    m=meta['poses']['hands_back']; meta['ground']=round(m['y1']-m['yb'],1)
    json.dump(meta,open(f'{OUT}/char_{cid}_pose_meta.json','w')); man[f'char/{cid}/pose/_meta']=f'char_{cid}_pose_meta.json'
    print(cid,meta['walk'],meta['ground'],meta['poses']['hands_back'])
json.dump(man,open(f'{OUT}/manifest.json','w'),indent=0)
