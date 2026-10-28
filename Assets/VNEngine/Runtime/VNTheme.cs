using TMPro;
using UnityEngine;

namespace VNEngine
{
    /// <summary>
    /// Every look-and-feel setting in one place. Create one with
    /// Assets > Create > VN Engine > Theme and name it "VNTheme" inside a Resources folder,
    /// or assign it on the StoryPlayer. Without one, these defaults are used.
    /// </summary>
    [CreateAssetMenu(menuName = "VN Engine/Theme", fileName = "VNTheme")]
    public sealed class VNTheme : ScriptableObject
    {
        [Header("Screen")]
        [Tooltip("Width / height of the story frame. 0.5625 = 9:16 portrait like Episode. 1.7778 = 16:9 landscape.")]
        public float designAspect = 9f / 16f;
        [Tooltip("Color around the story frame when the window is a different shape.")]
        public Color letterboxColor = new Color(0.06f, 0.06f, 0.08f);
        [Tooltip("Width of the screen in spot-directing units. Episode uses 320.")]
        public float stageUnitsWide = 320f;

        [Header("Fonts")]
        public TMP_FontAsset font;
        public TMP_FontAsset titleFont;
        [Tooltip("Optional .ttf/.otf used when no TMP font asset is set.")]
        public Font sourceFont;

        [Header("Dialogue")]
        public Color bubbleColor = Color.white;
        public Color bubbleTextColor = new Color(0.13f, 0.13f, 0.16f);
        public Color bubbleOutline = new Color(0.12f, 0.12f, 0.15f, 1f);
        public Color narrationColor = new Color(0.08f, 0.08f, 0.12f, 0.88f);
        public Color narrationTextColor = Color.white;
        public Color thoughtColor = new Color(0.94f, 0.96f, 1f);
        public Color phoneColor = new Color(0.25f, 0.55f, 1f);
        public Color defaultNameColor = new Color(0.98f, 0.36f, 0.55f);
        public float dialogueFontSize = 46f;
        public float nameFontSize = 38f;
        [Tooltip("Characters per second for the typewriter effect.")]
        public float textSpeed = 50f;
        public float autoAdvanceDelay = 1.6f;
        public Sprite bubbleSprite;
        public Sprite narrationSprite;
        [Tooltip("Make characters who aren't talking a little darker.")]
        public bool dimListeners = true;

        [Header("Choices")]
        public Color choiceColor = Color.white;
        public Color choiceTextColor = new Color(0.15f, 0.15f, 0.2f);
        public Color choiceAccent = new Color(0.98f, 0.36f, 0.55f);
        public Color choiceLockedColor = new Color(0.75f, 0.75f, 0.8f);
        public Sprite choiceSprite;

        [Header("Characters")]
        [Tooltip("Character height at spot scale 1, as a fraction of the screen height.")]
        public float characterHeight = 1.0f;
        [Tooltip("Where the character's feet sit at spot y 0, as a fraction of screen height (negative = below the screen edge, so you see them from the knees up).")]
        public float characterBaseline = -0.36f;
        public float walkSpeed = 190f; // stage units per second
        public float runSpeed = 420f;
        public float enterDuration = 0.9f;
        public float defaultMoveDuration = 0.8f;
        public bool breathing = true;
        public bool blinking = true;

        [Header("Named positions (stage units)")]
        public float leftX = 72f;
        public float centerX = 160f;
        public float rightX = 248f;
        public float farLeftX = 20f;
        public float farRightX = 300f;
        public float offscreenMargin = 140f;

        [Header("Scenes")]
        public string defaultTransition = "dissolve";
        public float transitionDuration = 0.8f;

        [Header("UI")]
        public Color accentColor = new Color(0.98f, 0.36f, 0.55f);
        public Color panelColor = new Color(0.1f, 0.09f, 0.14f, 0.96f);
        public Color panelTextColor = Color.white;
        public Sprite menuBackground;

        [Header("Sound")]
        [Tooltip("Optional click for buttons. A soft built-in click is used when empty.")]
        public AudioClip clickSound;
        [Tooltip("Optional blip for typing. Leave empty for silence.")]
        public AudioClip typeSound;
        public bool proceduralClicks = true;

        static VNTheme cached;

        public static VNTheme Load()
        {
            if (cached != null) return cached;
            cached = Resources.Load<VNTheme>("VNTheme");
            if (cached == null)
            {
                cached = CreateInstance<VNTheme>();
                cached.name = "Default VNTheme";
            }
            return cached;
        }

        public static void Use(VNTheme theme)
        {
            if (theme != null) cached = theme;
        }
    }
}
