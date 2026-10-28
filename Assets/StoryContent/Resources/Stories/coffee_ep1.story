title: The New Barista
series: Coffee & Secrets
episode: 1
genre: Romance · Mystery
description: Your first day at the Moonbean Café. A charming owner, a mysterious regular, and a locked door nobody talks about.
cover: coffee_cover

# ---- Cast ----
# name= is what shows on the name tag. color= tints the tag (and the placeholder art).
character PLAYER name="{player}" color=#7a9cff hair=loose
character MAYA name="Maya" color=#ff6f91 haircolor=#3b2230 hair=ponytail outfit=apron
character LEO name="Leo" color=#46b39d haircolor=#20283a hair=short outfit=jacket

# ---- Scene 1 ----
EXT. CITY STREET - MORNING
@music morning_walk
@weather petals

The city is just waking up.
And today, so are you... on your first day at a brand new job.

input player "What's your name?" default="Alex"

@PLAYER enters from left to center and is idle_happy
PLAYER (think) [thought]
Okay, {player}. Deep breath. You've got this.

@PLAYER walks to offscreen right
@weather none

# ---- Scene 2 ----
INT. COFFEE SHOP - DAY [dissolve 1.2]
@music cafe
@ambience cafe_chatter

@MAYA spot 1.0 230 0 and faces left
@PLAYER enters from left to spot 1.0 70 0
&MAYA is wave
MAYA (talk_excited)
You must be {player}! Welcome to the Moonbean Café!

PLAYER (talk_happy)
Thanks for hiring me! I can't wait to start.

@MAYA walks to center and is talk_happy
MAYA (talk_happy)
First lesson: the perfect latte. Watch closely.
It's all about timing.

minigame timing title="Pour the Latte" instructions="Tap when the milk hits the sweet spot!" rounds=3 need=2 as=latte retries=1
    win
        MAYA (cheer)
        Look at that leaf! You're a natural!
        set maya_likes + 1
        @notify "Maya was impressed."
    lose
        MAYA (laugh)
        That's... an abstract leaf. We'll practice!

@zoom on MAYA 1.4 in 0.6
MAYA (think)
One more thing. See that door behind the counter?
Never, ever go in there.
@zoom reset

PLAYER (surprised)
Why not?

MAYA (nervous): Just... house rules. Anyway!

# ---- A mysterious customer ----
@sound door_bell
@LEO enters from right to right and is idle_think
&PLAYER walks to center and faces right
@MAYA walks to spot 1.0 62 0 and faces right

LEO (talk_neutral)
The usual. And tell the new hire to stop staring.

PLAYER (embarrassed) [thought]
Was I staring? I was totally staring.

choice "How do you respond?"
    "Flirt: 'Hard not to stare.'" [love]
        set leo_likes + 1
        LEO (smug)
        Bold. I like bold.
        @notify "Leo will remember that."
    "Stay professional."
        PLAYER (talk_happy)
        One usual, coming right up!
        LEO (neutral): Hm. Efficient.
    "Ask about the locked door." [if latte_won]
        PLAYER (nervous) [whispers]
        Hey... what's behind the door in the back?
        @LEO is gasp
        LEO (shocked) [whispers]
        You shouldn't ask that here.
        set curious + 1
    "Ask Maya for help." [locked if maya_likes >= 2]
        MAYA (happy): I'll take this one!

@LEO looks neutral
LEO
Quiz time, new hire. If you're going to serve me every morning, you should know the menu.

minigame quiz title="Menu Quiz" pass=2
    Q: What's in a cortado?
    * Espresso and a little warm milk
    - Espresso and whipped cream
    - Cold brew and oat milk
    Q: Which drink has the most foam?
    * Cappuccino
    - Flat white
    - Americano
    Q: What's the café's secret ingredient?
    * Love (and a pinch of cinnamon)
    - Glitter
    - Nobody knows
    win
        LEO (smile)
        Not bad. Maybe you'll last a week.
        set leo_likes + 1
    lose
        LEO (sigh)
        We'll work on it.

@LEO exits right
@MAYA walks to center

MAYA (talk_happy)
He's... a lot. But he tips well.
Let's clean up before closing.

# ---- Scene 3 ----
INT. COFFEE SHOP - NIGHT [fade 1.5]
@music night_theme
@tint night
@weather rain

You're alone, wiping down the counter, when your phone buzzes.

LEO [phone]
Meet me at the back door. Bring the key under the register.

PLAYER (scared) [thought]
How does he know about a key?

@show old_key
You find an old brass key, cold and heavy in your hand.
@hide old_key

@shake 0.6
@flash white
@sound thunder

PLAYER (gasp)
Okay. Okay. The door has a keypad too.

minigame keypad title="The Locked Door" code=0315 tries=3 hint="A note taped under the counter: 'Grand opening: March 15'" as=door
    win
        @sound door_open
        The lock clicks open.
        set opened_door
    lose
        The keypad flashes red and goes dark.
        PLAYER (sad): Maybe that's a sign.

if opened_door
    @MAYA appears at center and is angry
    MAYA (angry) [shouts]
    {player}! What are you doing?!
    @MAYA is cry
    MAYA (sad)
    That room... it's where my sister kept her things.
else
    @MAYA enters from left to center and is worried
    MAYA (worried)
    Still here? Go home, it's late.

@tint none
@weather none
@zoom on MAYA 1.3 in 1
MAYA (sigh)
Tomorrow. I'll tell you everything tomorrow.
@zoom reset

next coffee_ep2
