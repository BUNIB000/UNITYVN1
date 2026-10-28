# Writing Stories

Stories are plain text files. Put them in **`Assets/StoryContent/Resources/Stories/`** as `.story` or `.txt` files, and they show up in the story library automatically.

If you've written Episode scripts before, most of this will feel familiar. The engine also forgives a lot: capitals in commands, extra words like "screen" and "the", `{ }` braces, and smart quotes all work.

The sample stories in `Resources/Stories/coffee_ep1.story` and `coffee_ep2.story` use almost every feature in this guide.

---

## The basics

```
title: The New Barista
series: Coffee & Secrets
episode: 1

character MAYA name="Maya" color=#ff6f91

INT. COFFEE SHOP - DAY
@MAYA enters from right to center

MAYA (talk_happy)
Welcome to the Moonbean Café!
It's your first day, right?

The café smells like cinnamon and burnt sugar.
```

- **A name in CAPITALS on its own line** starts dialogue. Every line of text after it is a new speech bubble from that character, until a blank line or a command.
- **Text with no speaker** is narration.
- **`(talk_happy)`** after the name plays an animation or expression while they talk.
- **Lines starting with `#` or `//`** are notes to yourself and are ignored.
- **Indentation** (spaces or tabs) groups lines under choices, ifs, and minigames.

One-line dialogue also works: `MAYA (smile): Welcome back!` or just `MAYA: Hi.`

---

## Story header

Put these at the top of the file. All are optional.

| Line | What it does |
|---|---|
| `title: The New Barista` | Episode title, shown in the library and on the end screen. |
| `series: Coffee & Secrets` | Files with the same series are grouped as episodes of one story. |
| `episode: 1` | Episode order within the series. |
| `description: ...` | Shown on the series page. |
| `genre: Romance` | Shown on the library card. |
| `cover: coffee_cover` | Cover image from `Resources/Covers/`. |
| `start: my_label` | Start somewhere other than the top. |

---

## Characters

Define characters near the top (optional, but it gives them nice names and colors):

```
character MAYA name="Maya" color=#ff6f91
character PLAYER name="{player}" color=#7a9cff
```

| Option | Meaning |
|---|---|
| `name="Maya"` | Name shown on the bubble tag. Can use variables, like `"{player}"`. |
| `color=#ff6f91` | Name tag color. |
| `height=1.1` | Make this character taller or shorter than others. |
| `faces=left` | Your art is drawn facing left (so the engine flips it the right way). |
| `outfit=casual` / `hair=bun` | Starting outfit and hairstyle. |
| `voice=blip_high` | A short sound from `Resources/Sounds` played while they type. |
| `expression=happy` | Starting expression. |
| `haircolor=#3b2230` / `skin=#e8b892` | Colors for the placeholder doll, before you have art. |

With no art yet, every character is drawn as an animated placeholder doll in their color, so you can write first and draw later.

### Moving characters

Lines starting with `@NAME` direct a character. Chain several with `and`:

```
@MAYA enters from left to center
@MAYA walks to right and is talk_happy
@MAYA runs to offscreen left
@LEO enters from right to right and faces left
@MAYA exits right
```

| Command | What happens |
|---|---|
| `enters from left` / `from right` | Walks in from that side (to the center unless you say `to ...`). |
| `enters from top` / `from bottom` | Slides in vertically. |
| `enters` | Fades in where they are. |
| `exits` / `exits left` / `exits fade` | Walks off the nearest side, the side you name, or fades out. |
| `walks to <place>` | Walks with a step bounce, turning to face where they're going. |
| `runs to <place>` | Same, faster. |
| `moves to <place>` | Glides without walking. |
| `stands <place>` | Appears there instantly. |
| `appears at <place>` / `disappears` | Fades in or out. |
| `faces left` / `faces right` / `faces LEO` / `turns around` | Turns. |
| `is <animation>` | Plays an animation (see below). |
| `looks <expression>` | Changes only the face. |
| `wears <outfit>` / `wears hair ponytail` | Changes clothes or a part. |
| `layer 2` / `at layer 2` | Draw order: higher layers are in front. |

Add `in 1.5` to any move to set how many seconds it takes, or `slowly` / `quickly`.

**Places** can be: `left`, `center`, `right`, `far left`, `far right`, `offscreen left`, `offscreen right`, `next to MAYA`, a number (x position), or spot directing.

### Spot directing

Just like Episode: `spot <scale> <x> <y>`. The screen is 320 units wide, x goes right, y goes up, and scale 1 is normal size.

```
@MAYA spot 1.2 100 0
@LEO spot 0.8 250 40 in zone 2 at layer 1
@MAYA walks to spot 1.0 160 0 in 2
```

Characters with smaller scales and higher y look further away. Use `at layer` so far characters are drawn behind near ones.

### Animations and expressions

Animations use Episode-style names: `gesture_emotion`, with `_loop` to keep it going.

```
@MAYA is talk_happy
@MAYA is idle_think_loop
MAYA (laugh)
@LEO is wave
@MAYA jumps
```

- **Emotions** change the face: neutral, happy, sad, angry, surprised, scared, think, love, laugh, embarrassed, smug, cry, tired, worried, flirty, disgusted, excited, and synonyms like shocked, blush, nervous.
- **Gestures** move the body. Any animation your art provides plays by name. These built-in motions work on any art, even a single still image: `jump`, `hop`, `bounce`, `cheer`, `nod`, `shake_head`, `laugh`, `giggle`, `gasp`, `sigh`, `shiver`, `cry`, `angry`, `stomp`, `dance`, `wave`, `spin`, `shrug`, `bow`, `think`, `lean`, `pout`, `shy`, `faint`, `point`, `talk`, `love`.
- Shortcuts: `@MAYA jumps`, `nods`, `laughs`, `waves`, `gasps`, `sighs`, `shrugs`, `bows`, `cheers`, `dances`, `spins`, `cries`, `faints`, `blushes`.

Characters also blink, breathe, move their lips while their text types, and dim slightly while someone else talks. You don't need to write anything for that.

---

## Dialogue styles

Add a style in square brackets after the speaker:

```
MAYA (think) [thought]
I hope they like me.

LEO (angry) [shout]
GET OUT!

MAYA [whisper]
Over here...

LEO [phone]
Meet me at the back door.
```

| Style | Look |
|---|---|
| *(none)* | Speech bubble with a tail pointing at the speaker. |
| `[thought]` | Rounded bubble with thought dots. |
| `[shout]` | Bold, shaking bubble. |
| `[whisper]` | Soft, italic text. |
| `[phone]` / `[text]` | A chat-message bubble. |
| `NARRATOR` or no speaker | Narration box. |

Use **`{variable}`** anywhere in text to insert a value: `Nice to meet you, {player}!`. `{MAYA}` inserts a character's display name. Rich text tags like `<b>bold</b>`, `<i>italic</i>` and `<color=#ff0000>red</color>` work too.

---

## Scenes and backgrounds

A scene heading changes the background and clears the stage for a new scene:

```
INT. COFFEE SHOP - DAY
EXT. ROOFTOP - NIGHT [fade 1.5]
INT. BEDROOM - MORNING [dissolve]
```

The engine looks in `Resources/Backgrounds/` for an image named any of: `INT. COFFEE SHOP - DAY`, `int_coffee_shop_day`, `coffee_shop_day`, or `coffee_shop`. If none exists it paints a placeholder that matches the time of day, and shows the location name.

To change the background without clearing characters: `@background coffee_shop_night with dissolve`.

**Transitions:** `dissolve` (default), `fade` (through black), `fade_white`, `cut`, `slide_left`, `slide_right`, `zoom`. Add seconds: `[fade 2]`.

**Zones:** a background wider than the screen is split into zones (zone 1 is the left edge). Start on another zone with `[zone 2]`, and move the camera with `@pan to zone 2`.

---

## Camera and effects

| Command | Effect |
|---|---|
| `@zoom on MAYA 1.5` | Zoom toward a character's face (1.5 = 150%). |
| `@zoom on 160 300 2` | Zoom to a spot (x, y, zoom). |
| `@zoom reset` | Back to normal. |
| `@pan to zone 2` / `@pan right 100` | Move the camera. |
| `@shake` / `@shake 2 in 1` | Shake the screen (strength, seconds). |
| `@flash` / `@flash red` | Flash of light. |
| `@fade out` / `@fade out white` / `@fade in` | Fade the screen. |
| `@tint night` | Color wash: `night`, `sunset`, `sepia` (flashbacks), `dream`, `red`, `dark`, a hex color, or `none`. |
| `@weather rain` | `rain`, `storm`, `snow`, `petals`, `leaves`, `sparkles`, `dust`, or `none`. |
| `@show letter` / `@hide letter` | Show an item close-up from `Resources/Images/`. `@hide all` hides every one. |
| `@clear` | All characters leave. |
| `@pause 1` / `@wait 1` | Wait. |
| `@notify "Maya will remember that."` | A toast at the top of the screen. |
| `@vibrate` | Phone vibration plus a small shake. |

Most commands accept `in <seconds>`.

**Running things at the same time:** start a line with `&` instead of `@` and the next line starts immediately instead of waiting:

```
&MAYA walks to left
&LEO walks to right
@zoom reset
```

---

## Sound

| Command | Effect |
|---|---|
| `@music cafe` | Crossfades to `Resources/Music/cafe`. |
| `@music cafe fade=3 volume=0.6` | Options. |
| `@music stop` | Fade out. |
| `@sound door_bell` | One-shot sound from `Resources/Sounds/`. |
| `@ambience rain_loop` / `@ambience stop` | A background loop under the music. |

---

## Choices

```
choice "What do you say?"
    "Flirt back." [love]
        set leo_likes + 1
        LEO (smug): Bold.
    "Ask about the door." [if curious >= 1]
        goto door_scene
    "Use your charm." [locked if charm >= 3]
        MAYA: How could I say no?
    "Leave." -> ending_b
```

- Each option is in quotes, indented under `choice`, and what happens is indented under the option.
- After an option's lines finish, the story continues after the whole choice.
- `[if condition]` hides the option unless the condition is true.
- `[locked if condition]` shows it with a lock until the condition is true.
- `[love]`, `[premium]`, `[star]` or any image name adds an icon.
- `-> label` jumps to a label.
- `choice timed 5 "Quick!"` gives the player 5 seconds; the option marked `[default]` (or the last one) is picked if time runs out.

The text of the last option picked is saved in `{last_choice}`.

---

## Variables and branching

```
set trust + 1
set trust - 2
set met_maya
set player_job = "barista"
set score = score * 2

if trust >= 3 and met_maya
    MAYA: You're my favorite.
elif trust >= 1
    MAYA: You're okay.
else
    MAYA: Hmm.
```

- Variables don't need to be created first. One that was never set counts as 0 (false).
- Conditions understand `== != < <= > >=`, `and`, `or`, `not`, `+ - * /` and parentheses. `=` and `is` also mean equals.
- Variables carry over into the next episode of the same series.

**Asking for a name:**

```
input player "What's your name?" default="Alex"
```

---

## Jumping around

```
label secret_room
...
goto secret_room

call flashback      # play a labelled section, then come back here
...
label flashback
NARRATOR
Years ago...
return

end                 # end this episode now
next coffee_ep2     # continue straight into another story file
```

When an episode ends, players see an "Episode Complete" screen with a button for the next episode in the series.

---

## Mini games

```
minigame memory pairs=6 time=60 as=latte
    win
        MAYA (cheer): Amazing!
    lose
        MAYA (laugh): We'll practice.
```

- `win` and `lose` branches are optional. Afterwards, `{minigame_won}` and `{minigame_score}` are set, and with `as=name` also `{name_won}` and `{name_score}`, for use later.
- Common options: `title="..."`, `instructions="..."`, `time=`, `difficulty=easy|normal|hard`, `retries=2`, `skin=`, `background=`, `music=`.

See **[MINIGAMES.md](MINIGAMES.md)** for every game and how to reskin them with your own art.

---

## When something's wrong

- Mistakes are reported with the line number: in the Unity Console when you save the file, on screen when you press Play, and all together with **VN Engine > Validate All Stories**.
- Missing art never stops a story: you get a placeholder and a warning.
- Right-click a story file and choose **VN Engine > Play This Story** to test it straight away. Set **Start Label** on the Story Player to test from the middle.
- In play mode: tap or press Space to continue, hold Ctrl to fast-forward, Esc for the menu.
