title: The Party
series: Coffee & Secrets
episode: 2

character PLAYER name="{player}" color=#7a9cff hair=loose
character MAYA name="Maya" color=#ff6f91 haircolor=#3b2230 hair=bun outfit=dress
character LEO name="Leo" color=#46b39d haircolor=#20283a hair=short outfit=suit

INT. BEDROOM - EVENING
@music getting_ready

PLAYER (talk_excited) [thought]
Maya invited me to the café's anniversary party. I need the perfect look.

minigame dressup character=PLAYER title="Get Ready"
    outfit: casual, dress, suit, sweater
    hair: loose, ponytail, bun

PLAYER (happy)
Perfect. Let's go!

EXT. ROOFTOP PARTY - NIGHT [fade]
@music party
@weather sparkles

@MAYA spot 1.0 80 0 and faces right
@LEO spot 1.05 250 0 and faces left
@PLAYER enters from bottom to center

MAYA (talk_happy)
You came! And you look amazing in that {PLAYER_outfit}.

if leo_likes >= 2
    LEO (smug)
    I'll admit it. You clean up nicely, {player}.
    @PLAYER is blush
elif opened_door
    LEO (serious)
    We need to talk about last night.
else
    LEO (neutral): Hey.

MAYA (talk_excited)
Dance-off! Loser buys tomorrow's pastries!

minigame rhythm title="Dance-Off" bpm=118 notes=20 lanes=3 need=65
    win
        @PLAYER is dance_loop
        MAYA (laugh): Okay, okay, you win!
        @PLAYER is idle_happy
    lose
        @MAYA is cheer
        MAYA (laugh): Croissants are on you!

&LEO exits right
@MAYA walks to spot 1.0 82 0 and is talk_happy
@PLAYER faces left
MAYA
Thanks for being here. It's been a long time since this place felt like home.

choice timed 6 "Quick! What do you do?"
    "Hug her." [default]
        @PLAYER walks to next to MAYA
        @MAYA is love
        MAYA (happy): ...Thank you.
        set maya_likes + 2
    "Toast to the café!"
        @PLAYER is cheer
        MAYA (laugh): To the Moonbean!

To be continued...
