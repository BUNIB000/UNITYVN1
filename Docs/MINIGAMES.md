# Mini Games

Start a mini game from any story:

```
minigame memory pairs=6 time=60 as=latte_art skin=cafe
    win
        MAYA (cheer): You did it!
    lose
        MAYA (laugh): Next time!
```

Every game gets the same polished frame: an intro card with a Start button, a 3-2-1 countdown, a timer bar, a score, a result card with stars, confetti on a win, and optional retries. While it runs, the story scene stays visible behind a soft veil, so it feels like part of the scene.

After it ends:

- `{minigame_won}` (true/false) and `{minigame_score}` are set.
- With `as=name`, `{name_won}` and `{name_score}` are set too, so you can check them later: `[if latte_art_won]`.

## Options every game understands

| Option | Meaning |
|---|---|
| `title="Pour the Latte"` | Heading on the intro card and during play. |
| `instructions="..."` | Text on the intro card. |
| `time=45` | Seconds allowed (where the game is timed). |
| `difficulty=easy` / `normal` / `hard` | Adjusts the game's defaults. |
| `retries=2` (or `retry=yes` for unlimited) | Lets the player try again after losing. |
| `skin=cafe` | Which art folder to use (see below). |
| `background=kitchen` | Use a background from `Backgrounds/`. |
| `dim=0.4` | How dark the veil over a custom background is. |
| `music=minigame_theme` | Music while playing (the story's music comes back after). |
| `wintext="Delicious!"` / `losetext="Burnt..."` | Result headings. |
| `skipintro=yes` | Go straight to the countdown. |

## Reskinning with your own art

Each game draws clean built-in visuals until you give it art. Put images in:

```
Resources/Minigames/<game>/<skin>/      e.g. Minigames/memory/cafe/card_back.png
Resources/Minigames/<game>/             art for every skin of that game
Resources/Minigames/Shared/             art for every game (e.g. background.png)
```

Sounds work the same way (`hit`, `miss`, `win`, `lose`, plus per-game names below).

You can also point a single option at any image: `card_back=MyCardBack` uses `Images/MyCardBack.png`.

---

## The games

### `qte`: Quick-time reaction
Targets pop up; tap each before its ring closes. Escapes, catches, dodges.
`count=6 window=1.2 need=5`
Skin: `target`, `ring`. Sounds: `hit`, `miss`.

### `timing`: Timing bar
Stop the moving marker inside the zone. Lockpicking, pouring, archery, a dance move.
`rounds=3 need=2 zone=0.18 speed=1`
Skin: `bar`, `zone`, `needle`, `icon` (a picture above the bar).

### `mash`: Button mash
Tap fast to fill the meter before time runs out. Arm wrestling, pushing a door, cheering.
`taps=30 time=7 decay=3`
Skin: `button`, `meter`, `icon`.

### `memory`: Memory cards
Flip two cards at a time to find pairs.
`pairs=6 time=60`
Skin: `card_back`, and any images whose names start with `face` (`face_1`, `face_croissant`...). Sounds: `match`, `miss`.

### `slide`: Sliding picture puzzle
Rebuild a scrambled picture.
`size=3 image=old_photo time=150`
Skin: `image` (or `image=` any picture in `Images/` or `Backgrounds/`). Without a picture, numbered tiles are used.

### `hidden`: Hidden object
Find every listed item.
`items=key,ring,letter time=45` — or list them on indented lines. Place an item yourself with `key@0.2,0.7` (x, y from 0 to 1).
`size=130 visibility=0.8`
Skin: one image per item name, plus `background`. Wrong taps cost 1.5 seconds.

### `scramble`: Word scramble
Tap letters to spell the word.
`word=COFFEE hint="Maya's favorite drink" time=40`
Skin: `tile`, `slot`. Sounds: `wrong`.

### `quiz`: Quiz / trivia
Questions go in the block. `*` marks the right answer.

```
minigame quiz pass=2 time=15
    Q: What does Maya always order?
    * Oat latte
    - Black coffee
    - Green tea
```

`pass=` correct answers needed (default 60%), `time=` seconds per question (default none), `shuffle=no` keeps question order, `shuffleanswers=no` keeps answer order. Sounds: `right`, `wrong`.

### `keypad`: Code entry
Enter the code to open a safe, phone or door.
`code=0315 tries=3 hint="The grand opening date" time=30 autosubmit=no`
Skin: `key`, `display`.

### `sequence`: Follow the pattern (Simon)
Watch the pads light up, then repeat.
`length=5 pads=4 start=2`
Skin: `pad_1` ... `pad_6`. Sounds: `note_1` ... `note_6` (built-in tones otherwise).

### `catch`: Catch falling things
Drag the basket; catch good things, dodge bad ones (−2).
`need=12 time=25 speed=1 bad=0.3`
Skin: `basket`, images starting with `good` and `bad`. Sounds: `catch`, `ouch`.

### `rhythm`: Rhythm taps
Notes fall down lanes; tap as they cross the line. Dance-offs, concerts.
`bpm=118 notes=20 lanes=3 need=70 music=dance_song travel=1.4`
`need` is the hit percentage required. Skin: `note`, `lane`, `button_1` ... `button_4`.

### `dressup`: Outfit picker
The character stays on stage while the player flips through options; each pick is applied live and saved as a variable.

```
minigame dressup character=PLAYER
    outfit: casual, dress, suit
    hair: loose, ponytail, bun
```

Saves `{PLAYER_outfit}`, `{PLAYER_hair}`. Uses the same slots as layered character art (`outfit/`, `hair/`...), or Sprite Library categories on rigged characters. Skin: preview icons named `<slot>_<option>` (e.g. `outfit_dress`). `confirm="Looks great!"` changes the button.

---

## Making your own

Write a C# class anywhere in your project:

```csharp
using System.Collections;
using VNEngine;

[Minigame("fishing")]
public class FishingGame : Minigame
{
    public override string DefaultTitle => "Gone Fishing";
    public override string DefaultInstructions => "Tap when the float dips!";

    protected override IEnumerator Play()
    {
        // Build UI inside Board (a RectTransform), read options with Ctx.GetInt("fish", 3),
        // load art with Ctx.Sprite("float"), and finish by setting:
        Ctx.Won = true;
        Ctx.Score = 3;
        yield break;
    }
}
```

Then `minigame fishing fish=3` works in any story, with the intro, countdown, timer, results and retries handled for you.
