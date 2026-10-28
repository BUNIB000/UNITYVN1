import numpy as np
from PIL import Image
from scipy import ndimage as ndi

def key_green(path):
    im = np.asarray(Image.open(path).convert('RGB')).astype(np.float32)
    r, g, b = im[..., 0], im[..., 1], im[..., 2]
    d = g - np.maximum(r, b)
    a = np.clip(1 - (d - 30) / 60, 0, 1)
    # despill: pull green down on partly transparent / green-tinted edge pixels
    spill = np.clip(g - np.maximum(r, b) * 1.0, 0, None)
    edge = (a < 1) & (a > 0)
    gg = np.where(edge | (d > 0), np.minimum(g, np.maximum(r, b)), g)
    out = np.dstack([r, gg, b, a * 255]).astype(np.uint8)
    # drop tiny islands
    m = out[..., 3] > 128
    lab, n = ndi.label(m)
    if n:
        sizes = ndi.sum(m, lab, range(1, n + 1))
        keep = np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s > 400])
        out[..., 3] = np.where(keep | (out[..., 3] > 0) & ndi.binary_dilation(keep, iterations=2), out[..., 3], 0)
    return Image.fromarray(out, 'RGBA')

if __name__ == '__main__':
    import sys
    for k in 'abcdef':
        key_green(f'../new/{k}.jpg').save(f'k_{k}.png')
