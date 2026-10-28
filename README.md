# VN Engine

An Episode-style visual novel engine for Unity. You write stories as plain text, drop in your art and sounds, and the engine handles the rest: staging, character animation, speech bubbles, choices, mini games, saving, menus and the story library.

```
INT. COFFEE SHOP - DAY [fade]
@music cafe_morning
@MAYA enters from right to center
MAYA (talk_happy)
Welcome to the Moonbean! What can I get you?

choice
    "An oat latte, please."
        set maya_likes + 1
        MAYA (happy): Great taste!
    "Surprise me."
        MAYA (talk_excited): Ooh, a challenge.
```

## Getting started

1. Install **Unity 6** or **Unity 2022.3 LTS** from Unity Hub.
2. In Unity Hub choose **Add > Add project from disk** and pick this folder. Unity builds the project files the first time it opens.
3. When it asks, import **TMP Essentials** (the engine also tries to do this for you).
4. Use the **VN Engine > Play** menu, or open `Assets/Scenes/Story.unity` and press Play. The sample series *Coffee & Secrets* shows up in the library.
5. To write your own: **VN Engine > New Story File**, then right-click the file and choose **Play This Story**.

For rigged characters (arms, head turns, full body gestures), install **2D Animation** and **2D PSD Importer** from *Window > Package Manager > Unity Registry*. Everything else works without them.

## What you write, what you drop in

| You provide | Where | Guide |
|---|---|---|
| Stories (`.story` or `.txt`) | `Resources/Stories/` | [Writing stories](Docs/WRITING_STORIES.md) |
| Backgrounds, characters, items, covers | `Resources/Backgrounds/`, `Characters/`, `Images/`, `Covers/` | [Art guide](Docs/ART_GUIDE.md) |
| Music and sounds | `Resources/Music/`, `Resources/Sounds/` | [Art guide](Docs/ART_GUIDE.md#sound) |
| Mini game skins | `Resources/Minigames/<game>/<skin>/` | [Mini games](Docs/MINIGAMES.md) |
| Look and feel | `Resources/VNTheme.asset` (VN Engine > Create Theme) | [Art guide](Docs/ART_GUIDE.md#theme) |

Nothing is required to start. Missing art becomes painted placeholders and animated placeholder characters, so you can write and play a whole episode before any art exists.

## Features

- **Story script**: screenplay-style scene headings, speaker lines with animations and bubble styles, narration, choices (conditional, locked, timed, with icons), variables, if/elif/else, labels, jumps, calls, text input for names, and episodes that chain into series.
- **Directing**: enter, exit, walk, run, spot directing with zones, facing, layering, camera zoom, pan, shake, flash, fades, tints, weather, scene transitions, item close-ups and notifications.
- **Characters**: three art pipelines (one image per pose, layered parts, or rigs built with Unity 2D Animation), with blinking, lip flap synced to the text, breathing, walk bounce and procedural gestures layered on top.
- **Mini games**: 13 built in (quick-time, timing bar, button mash, memory, sliding puzzle, hidden object, word scramble, quiz, keypad, follow the pattern, catch, rhythm, dress-up). Every one can be reskinned with your art, and you can add your own with one C# class.
- **Player experience**: speech, thought, shout, whisper, phone and narration bubbles, typewriter text, auto and skip modes, history, settings, autosave per series, a library with series and episode pages, and end-of-episode cards.
- **Editor tools**: `.story` files import as assets and are checked for errors, art is imported with the right settings automatically, and the VN Engine menu covers setup, validation and playing a single story.

## Project layout

```
Assets/
  VNEngine/            the engine (you shouldn't need to edit this)
    Runtime/Core/        story parser, runner, variables, save data
    Runtime/Stage/       backgrounds, characters, animation, weather
    Runtime/UI/          bubbles, choices, menus, library
    Runtime/Minigames/   mini game framework and built-in games
    Editor/              importers and the VN Engine menu
  StoryContent/        your stories and assets (the sample lives here)
Docs/                  writing, art and mini game guides
```

## Fonts

Varela Round and Lilita One are included under the SIL Open Font License (see the OFL files next to the fonts).

## Web preview

`WebPreview/index.html` is a single-file browser version of the engine that plays the sample series on a phone: same story language, built-in character art, all 13 mini games, and a script lab. Rebuild it after editing a story or anything in `WebPreview/src/` with `python3 WebPreview/build.py`.
