# Rig cutter

Cuts a full-body A-pose figure (green background) and a 4x3 head sheet into the pieces the puppet needs.
Prompts: see `rig_art_prompts.txt` in the project files. The scripts need Python with Pillow, numpy and scipy.

1. Put the figure and the head sheet next to the scripts as `k_*` after keying: `python3 key.py` turns the JPG files
   `a.jpg` (heads) and `b.jpg` (figure) in `../new/` into `k_a.png` and `k_b.png`.
2. `python3 build2.py` cuts every character listed at the bottom of the file into `out2/NAME/`.
3. `python3 pack.py <demo_art folder>` scales the pieces and writes them (and `rig.json`) where the preview expects them.

Copy the contents of `out2/NAME/` (png files and `rig.json`) to `Assets/StoryContent/Resources/Characters/NAME/rig/`
for the Unity project.
