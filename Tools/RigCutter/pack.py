import json, os, glob, sys
from PIL import Image
SC = 0.6
DEMO = sys.argv[1]
man = json.load(open(DEMO + '/manifest.json'))
for k in [k for k in man if '/rig/' in k]: del man[k]
for f in glob.glob(DEMO + '/char_*_rig_*'): os.remove(f)
for cid in ('LEO', 'PLAYER', 'MAYA'):
    d = f'out2/{cid}/'
    m = json.load(open(d + 'rig.json'))
    def sc(v): return round(v * SC, 1)
    for k in ('arm_l', 'arm_r'):
        for kk in ('x', 'y', 'py', 'e'): m[k][kk] = sc(m[k][kk])
    for k in ('leg_l', 'leg_r'):
        for kk in ('x', 'y'): m[k][kk] = sc(m[k][kk])
    m['torso'] = {'w': sc(m['torso']['w']), 'h': sc(m['torso']['h'])}
    m['head']['dy'] = sc(m['head']['dy'])
    for kk in ('H', 'top', 'feet'): m[kk] = sc(m[kk])
    for f in sorted(os.listdir(d)):
        if not f.endswith('.png'): continue
        im = Image.open(d + f).convert('RGBA')
        im = im.convert('RGBa').resize((max(1, round(im.width * SC)), max(1, round(im.height * SC))), Image.LANCZOS).convert('RGBA')
        name = f[:-4]
        fn = f'char_{cid}_rig_{name}.png'
        im.save(DEMO + '/' + fn, optimize=True)
        man[f'char/{cid}/rig/{name}'] = fn
    fn = f'char_{cid}_rig_meta.json'
    json.dump(m, open(DEMO + '/' + fn, 'w'))
    man[f'char/{cid}/rig/_meta'] = fn
json.dump(man, open(DEMO + '/manifest.json', 'w'), indent=0)
tot = sum(os.path.getsize(f) for f in glob.glob(DEMO + '/*'))
print('demo_art MB', round(tot / 1e6, 1))
