import numpy as np, json, math, os
from PIL import Image
from scipy import ndimage as ndi
from key import key_green

M = 14  # overlap margin (px) across joints
def runs(row, minlen=4):
    r=[];s=None
    for x,v in enumerate(row):
        if v and s is None: s=x
        if not v and s is not None:
            if x-s>=minlen: r.append((s,x-1))
            s=None
    if s is not None and len(row)-s>=minlen: r.append((s,len(row)-1))
    return r

def runs_g(row, minlen=20, mingap=14):
    r = runs(row, 1)
    out = []
    for s_, e_ in r:
        if out and s_ - out[-1][1] <= mingap: out[-1] = (out[-1][0], e_)
        else: out.append((s_, e_))
    return [(s_, e_) for s_, e_ in out if e_ - s_ + 1 >= minlen]

def run_at(a, y, x):
    for s,e in runs(a[y]):
        if s<=x<=e: return s,e
    return None

def analyse(img):
    A = np.asarray(img)[...,3]>128
    H,W = A.shape
    ys = np.where(A.any(1))[0]; y0,y1 = ys[0], ys[-1]
    cx = int(np.mean(np.where(A[int(y0+0.5*(y1-y0))])[0]))  # around the middle of the body
    cx = (np.where(A[y0+int(0.5*(y1-y0))])[0].min()+np.where(A[y0+int(0.5*(y1-y0))])[0].max())//2
    # width of the central run for each row
    w = np.zeros(H, int)
    for y in range(y0, y1):
        r = run_at(A, y, cx)
        w[y] = (r[1]-r[0]+1) if r else 0
    # shoulder start: first row (after the head) where the central run exceeds 330 px
    ysh = next(y for y in range(y0+300, y1) if w[y] > 400 and w[y+110] > 480)
    yn = ysh-200 + int(np.argmin(w[ysh-200:ysh]))
    # arms separate from the torso
    ya = None
    for y in range(ysh+100, y1):
        if len(runs_g(A[y]))>=3 and len(runs_g(A[y+25]))>=3:
            ya = y; break
    # legs separate (crotch or apron hem)
    yc = None
    for y in range(ya+150, y1):
        cen = [r for r in runs_g(A[y]) if abs((r[0]+r[1])/2-cx) < 200]
        if len(cen)>=2: yc=y; break
    return dict(A=A,H=H,W=W,y0=y0,y1=y1,cx=cx,yn=yn,ysh=ysh,ya=ya,yc=yc,w=w)

def body_edges(A, y, cx, lim=170):
    cen = [r for r in runs_g(A[y], 12) if abs((r[0]+r[1])/2-cx) < lim]
    return (min(r[0] for r in cen), max(r[1] for r in cen)) if cen else (cx-50,cx+50)

def crop_alpha(img, mask, pad=2):
    arr = np.asarray(img).copy()
    arr[...,3] = np.where(mask, arr[...,3], 0)
    im = Image.fromarray(arr, 'RGBA')
    return im

def rotate_about(im, ang, c):
    pm = im.convert('RGBa').rotate(ang, resample=Image.BICUBIC, center=c, expand=False)
    return pm.convert('RGBA')

def tight(im, pad=2):
    bb = im.getchannel('A').point(lambda v: 255 if v>8 else 0).getbbox()
    return im.crop((max(bb[0]-pad,0), max(bb[1]-pad,0), bb[2]+pad, bb[3]+pad)), (max(bb[0]-pad,0), max(bb[1]-pad,0))

def build_figure(img, st):
    A=st['A']; H,W=st['H'],st['W']; cx=st['cx']; yn=st['yn']; ya=st['ya']; yc=st['yc']; y1=st['y1']
    xs = np.arange(W)[None,:]; yy = np.arange(H)[:,None]
    ba = body_edges(A, ya+12, cx)
    xl, xr = ba
    out = {}
    # ----- torso mask
    torso = np.zeros((H,W), bool)
    for y in range(yn, yc+10):
        if y < ya:
            lo, hi = xl-M, xr+M
        else:
            lo, hi = body_edges(A, y, cx)
        torso[y, lo:hi+1] = True
    torso &= A
    # above the shoulders keep only the neck (drops a ponytail tip or hair strand that hangs next to it)
    for y in range(yn, st['ysh']):
        r = run_at(A, y, cx)
        if r: torso[y, :r[0]] = False; torso[y, r[1]+1:] = False
    # ----- arms
    arms = {}
    for side in (-1, 1):
        m = np.zeros((H,W), bool)
        for y in range(st['ysh']-60, st['ya']+700):
            if y > y1: break
            if y < ya:
                if side<0: m[y, :xl+M] = True
                else: m[y, xr-M+1:] = True
            else:
                lo, hi = body_edges(A, y, cx)
                if side<0: m[y, :lo-1] = True
                else: m[y, hi+2:] = True
        m &= A
        # keep only the largest connected piece (the arm), trimming noise; stop at hands bottom
        lab, n = ndi.label(m)
        if n>1:
            sizes = ndi.sum(m, lab, range(1,n+1)); m = lab==(1+int(np.argmax(sizes)))
        arms[side]=m
    # ----- legs
    legs = {}
    hl = (y1 - yc)
    yh = yc - int(0.10*hl)
    gap_mid = None
    cen = [r for r in runs_g(A[yc+12]) if abs((r[0]+r[1])/2-cx) < 260]
    gx = (cen[0][1] + cen[1][0])//2 if len(cen)>=2 else cx
    for side in (-1, 1):
        m = np.zeros((H,W), bool)
        for y in range(yh, y1+1):
            lo, hi = body_edges(A, y, cx)
            if side<0: m[y, lo:gx+M//2] = True
            else: m[y, gx-M//2:hi+1] = True
        m &= A
        legs[side]=m
    return dict(torso=torso, arms=arms, legs=legs, gx=gx, yh=yh, xl=xl, xr=xr)

HEAD_NAMES = ['head','head_talk','head_blink','head_happy','head_sad','head_angry','head_surprised','head_worried','head_smug','head_laugh','head_cry','head_embarrassed']
ALIAS = {'head_scared':'head_worried','head_love':'head_embarrassed'}

def save(im, path):
    os.makedirs(os.path.dirname(path), exist_ok=True); im.save(path)

def cut_char(name, sk, fk, out):
    fig = Image.open(f'k_{fk}.png'); st = analyse(fig); P = build_figure(fig, st)
    cx, yn, yc, y1 = int(st['cx']), int(st['yn']), int(st['yc']), int(st['y1'])
    meta = {'v': 2}
    # torso: symmetric about cx so its top-centre is the neck point
    tm = P['torso']; cols = np.where(tm.any(0))[0]
    half = max(cx - cols[0], cols[-1] - cx) + 2
    t = crop_alpha(fig, tm).crop((cx-half, yn, cx+half+1, yc+11))
    save(t, f'{out}/{name}/torso.png')
    meta['torso'] = {'w': t.width, 'h': t.height}
    # arms
    for side, tag in ((-1,'l'), (1,'r')):
        m = P['arms'][side]; A = crop_alpha(fig, m)
        rows = np.where(m.any(1))[0]; ytop = rows[0]
        yp = ytop + 60
        xs_ = np.where(m[yp+20])[0]; xp = int(xs_.mean())
        yb = rows[-1]
        hand = np.where(m[yb-120:yb+1])
        hx, hy = hand[1].mean(), hand[0].mean() + yb - 120
        th = -math.degrees(math.atan2(hx - xp, hy - yp))
        R = rotate_about(A, th, (xp, yp))
        a = np.asarray(R)[..., 3] > 8
        rr = np.where(a.any(1))[0]; cc = np.where(a.any(0))[0]
        y_top, y_bot = rr[0], rr[-1]
        half = max(xp - cc[0], cc[-1] - xp) + 3
        Lf = y_bot - yp; ey = int(yp + 0.47 * Lf)
        uh = int((ey - y_top) / 0.94)
        up = R.crop((xp-half, y_top, xp+half+1, y_top+uh))
        fo = R.crop((xp-half, ey, xp+half+1, y_bot+2))
        save(up, f'{out}/{name}/upper_arm_{tag}.png'); save(fo, f'{out}/{name}/forearm_{tag}.png')
        meta['arm_'+tag] = {'x': xp - cx, 'y': yp - yn, 'py': yp - y_top, 'e': ey - y_top, 'rot': round(th,1)}
    # legs
    for side, tag in ((-1,'l'), (1,'r')):
        m = P['legs'][side]; A = crop_alpha(fig, m)
        yh = P['yh']
        xs_ = np.where(m[yh+30])[0]; xp = int(xs_.mean())
        cc = np.where(m.any(0))[0]; half = max(xp - cc[0], cc[-1] - xp) + 3
        leg = A.crop((xp-half, yh, xp+half+1, y1+3))
        save(leg, f'{out}/{name}/leg_{tag}.png')
        meta['leg_'+tag] = {'x': xp - cx, 'y': yh - yn}
    # heads
    sh = Image.open(f'k_{sk}.png'); SA = np.asarray(sh)[..., 3] > 128
    Hh, Ww = SA.shape; cw, ch = Ww // 4, Hh // 3
    info = []
    for i in range(12):
        r, c = divmod(i, 4)
        cell = sh.crop((c*cw, r*ch, (c+1)*cw, (r+1)*ch)); a = np.asarray(cell)[..., 3] > 128
        ys = np.where(a.any(1))[0]; xs = np.where(a.any(0))[0]
        top, bot = ys[0], ys[-1]
        # neck centre from the lowest 20% of the head
        lowx = np.where(a[bot-int(0.2*(bot-top)):bot+1].any(0))[0]
        ncx = (lowx[0] + lowx[-1]) // 2
        info.append((cell, top, bot, ncx, xs[0], xs[-1]))
    # neck row from the neutral head
    cell, top, bot, ncx, x0, x1_ = info[0]; a = np.asarray(cell)[..., 3] > 128
    ws = []
    for y in range(top, bot+1):
        rn = run_at(a, y, ncx); ws.append((rn[1]-rn[0]+1) if rn else 0)
    ws = np.array(ws); lo = int(0.55*(bot-top)); hi = int(0.92*(bot-top))
    nrow = top + lo + int(np.argmin(ws[lo:hi])); nw = ws[nrow-top]
    s = st['w'][yn] / nw
    below = bot - nrow
    up_ = max(i[2] and (nrow - i[1]) for i in info)  # use neutral for vertical
    Hs = max((nrow - i[1]) for i in info) + 2; hb = below + 2
    hw = max(max(i[3]-i[4], i[5]-i[3]) for i in info) + 2
    for i, (cell, top, bot, ncx, x0, x1_) in enumerate(info):
        dy = (bot - nrow)  # not used; align bottoms
        canvas = Image.new('RGBA', (2*hw+1, Hs + hb), (0,0,0,0))
        canvas.alpha_composite(cell, (hw - ncx, Hs + hb - 1 - bot + 0)) if False else None
        # place so that the bottom edge sits at the canvas bottom and the neck column is centred
        canvas.alpha_composite(cell.crop((0, 0, cw, bot+1)), (hw - ncx, (Hs + hb) - (bot+1))) if (Hs+hb) >= bot+1 else canvas.alpha_composite(cell.crop((0, top - ((Hs+hb)-(bot+1-top)), cw, bot+1)), (hw - ncx, 0))
        canvas = canvas.resize((max(1, round(canvas.width*s)), max(1, round(canvas.height*s))), Image.LANCZOS)
        save(canvas, f'{out}/{name}/{HEAD_NAMES[i]}.png')
        if i == 0: hsize = canvas.size
    for al, src in ALIAS.items():
        Image.open(f'{out}/{name}/{src}.png').save(f'{out}/{name}/{al}.png')
    meta['head'] = {'dy': round(hb * s), 'scale': round(float(s), 4)}
    meta['H'] = int(y1 - st['y0']); meta['top'] = int(yn - st['y0']); meta['feet'] = int(y1 - yn)
    json.dump(meta, open(f'{out}/{name}/rig.json', 'w'), indent=1, default=int)
    print(name, json.dumps(meta, default=int))

if __name__ == '__main__':
    for name, sk, fk in (('LEO','a','b'),('PLAYER','c','d'),('MAYA','e','f')):
        cut_char(name, sk, fk, 'out2')
