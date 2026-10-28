# Art Guide

Everything the engine shows comes from folders named `Resources` (anywhere under `Assets`). The sample project keeps them in `Assets/StoryContent/Resources/`:

```
Resources/
  Stories/        your .story / .txt scripts
  Backgrounds/    scene images
  Characters/     one folder per character, named after them (MAYA/)
  Images/         item close-ups for @show, choice icons, toast icons
  Covers/         library cover images
  Music/          music loops
  Sounds/         sound effects and ambience
  Minigames/      skins for mini games (see MINIGAMES.md)
  VNTheme.asset   optional look-and-feel settings
```

Image import settings are applied for you the first time a picture lands in one of these folders (sprite, no mipmaps, bottom-center pivot for characters).

Nothing is required. Missing backgrounds become painted placeholders, missing characters become animated placeholder dolls, and missing sounds are skipped with a warning.

---

## Backgrounds

- Portrait stories (the default, like Episode) are a 9:16 frame. A background as tall as the screen and **9:16 wide is one zone**; **twice as wide is two zones**, three times is three, and so on. Episode-style 3-zone backgrounds are about 3:1.68 (e.g. 3240 × 1920).
- Recommended height: 1920 px (2048 max is plenty).
- Name the file after the scene heading: `INT. COFFEE SHOP - DAY` finds `int_coffee_shop_day.png`, `coffee_shop_day.png`, or `coffee_shop.png`.
- Characters stand on the bottom of the screen, so leave the floor area in the lower third.

## Characters

There are three ways to make a character, and you can mix them in one story. The engine picks the richest one it finds in `Characters/NAME/`.

### 1. One image per expression (quickest)

```
Characters/MAYA/
  neutral.png
  happy.png
  sad.png
  angry.png
  talk_happy.png        optional: a pose for the 'talk_happy' animation
  happy_talk.png        optional: mouth-open frame, used for lip flap
  happy_blink.png       optional: eyes-closed frame, used for blinking
```

`@MAYA is talk_happy` looks for `talk_happy`, then `happy`, then `neutral`. Pictures crossfade when they change. Built-in motions (jump, nod, laugh, walk bounce, breathing) are added on top, so even one picture per mood feels alive.

### 2. Layered parts (recommended, Episode-like)

Draw the character once on a fixed canvas (same size for every part, e.g. 1200 × 2400) and export each part as its own transparent PNG, aligned on that canvas.

```
Characters/MAYA/
  body/       idle.png, wave.png, point.png, think.png, walk.png   (poses)
  outfit/     casual.png, party.png        or per pose: party_wave.png
  face/       neutral.png, happy.png, sad.png ...                   (whole face)
  eyes/       open.png, closed.png, happy.png, happy_closed.png     (optional)
  mouth/      closed.png, open.png, happy.png, happy_open.png       (optional)
  brows/      neutral.png, angry.png ...                            (optional)
  hair/       loose.png, ponytail.png, bun.png
  accessory/  glasses.png ...                                       (optional)
```

- Poses come from `body/` and the matching `outfit/` piece. `@MAYA is wave` shows `body/wave` + `outfit/casual_wave` (or `outfit/casual`).
- Emotions come from `face/`, `eyes/`, `mouth/` and `brows/`. `MAYA (talk_angry)` shows the `angry` parts.
- Lip flap alternates `mouth/<emotion>` and `mouth/<emotion>_open`; blinking uses `eyes/<emotion>_closed` or `eyes/closed`.
- `@MAYA wears party` swaps the outfit; `@MAYA wears hair bun` swaps any slot.
- The dress-up mini game uses these same slots.

### 3. Rigged with Unity 2D Animation (full Episode-style gestures)

This gives real body animation: arms that wave, heads that turn, hips that sway.

1. Install **2D Animation** and **2D PSD Importer** from *Window > Package Manager > Unity Registry* (they come with Unity's 2D project template).
2. Draw the character in Photoshop, Krita or Affinity as a layered `.psb` file: one layer per body part (torso, upper arm, forearm, hand, head, hair, each eye, each mouth shape...).
3. Drop the file into Unity. In its import settings, enable *Use as Rig* and open the **Skinning Editor**: add bones, auto-generate geometry and weights.
4. For expressions, put alternative mouths/eyes in **Sprite Library categories** named `Mouth`, `Eyes`, `Brows` or `Face`, with labels like `neutral`, `happy`, `happy_open`, `closed`. Add a **Sprite Resolver** to each of those parts.
5. Drag the character into a scene, add an **Animator**, and make animation clips whose **state names match your script names**: `idle`, `talk_happy`, `wave`, `idle_think_loop`, `walk`...
6. Save it as a prefab in `Resources/Characters/MAYA/MAYA.prefab`.
7. Optional: add the **VN Character Rig** component to tell the engine your idle state, which Sprite Library categories are the mouth and eyes, and where the top of the head is (for bubble placement).

How the engine drives a rigged character:

- `@MAYA is talk_happy` plays the Animator state `talk_happy`. If there's no such state, it plays `talk` and sets the face to `happy`.
- One-shot animations return to `idle_<emotion>` or `idle` when they finish. Names ending in `_loop` keep playing.
- Lip flap and blinking swap the Sprite Resolver labels (`happy_open`, `closed`...), or toggle the objects you set on the VN Character Rig.
- Walking plays a `walk` state if you have one; the engine still moves the character and adds a step bounce.
- Anything your rig doesn't have falls back to the built-in motions, so scripts never break while you're still animating.

### Sizes and positions

- All characters are scaled to the same height on screen (set in the theme), so canvas size doesn't matter, as long as **every image of one character uses the same canvas**.
- Leave a little transparent space above the head. The engine takes the top of the image as the top of the head; adjust with `head=0.95` in the character line, or a Head Top object on the rig.
- Draw characters facing the viewer or facing right. If your art faces left, add `faces=left` to the character line.

## Item close-ups, icons and covers

- `@show letter` shows `Images/letter.png` in the middle of the screen with a pop. `@show letter top` places it higher.
- Choice icons: `"Kiss him" [kiss_icon]` shows `Images/kiss_icon.png`.
- `@notify "..." icon=heart_icon` uses an icon from `Images/`.
- Library covers: `Covers/<series name or cover: value>`, portrait or landscape, around 1080 × 1350.

## Sound

- Music: `Music/` (loops, .ogg or .mp3). Ambience and effects: `Sounds/`.
- Character voices: a short blip sound, set with `voice=` on the character line.

## Theme

**VN Engine > Create Theme** makes `Resources/VNTheme.asset`. In it you can change:

- Frame shape (portrait 9:16 or landscape 16:9), letterbox color.
- Fonts (any TextMeshPro font asset, or a .ttf).
- Bubble, narration, thought and phone colors, or your own bubble and choice button sprites (9-sliced).
- Text speed, auto-advance delay, listener dimming.
- Character height and baseline, walk and run speeds, named positions (left / center / right x values).
- Default scene transition, UI accent and panel colors, menu background, click and typing sounds.
