using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VNEngine
{
    /// <summary>
    /// Quiz / trivia. Questions go inside the minigame block:
    ///   minigame quiz pass=2
    ///       Q: What does Maya always order?
    ///       * Oat latte
    ///       - Black coffee
    ///       - Green tea
    /// '*' marks the right answer. time= is seconds per question (0 = no limit).
    /// </summary>
    [Minigame("quiz", "trivia", "questions")]
    public sealed class QuizGame : Minigame
    {
        public override string DefaultTitle => "Quiz";
        public override string DefaultInstructions => "Answer the questions. Choose wisely!";
        protected override bool UsesTimer => false;

        sealed class Question
        {
            public string Text;
            public List<(string text, bool correct)> Answers = new List<(string, bool)>();
        }

        protected override IEnumerator Play()
        {
            var questions = new List<Question>();
            foreach (var raw in Ctx.Data)
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("Q:") || line.StartsWith("q:") || line.EndsWith("?") && !line.StartsWith("*") && !line.StartsWith("-"))
                    questions.Add(new Question { Text = line.StartsWith("Q:") || line.StartsWith("q:") ? line.Substring(2).Trim() : line });
                else if (questions.Count > 0 && (line.StartsWith("*") || line.StartsWith("+")))
                    questions[questions.Count - 1].Answers.Add((line.Substring(1).Trim().Trim('"'), true));
                else if (questions.Count > 0 && line.StartsWith("-"))
                    questions[questions.Count - 1].Answers.Add((line.Substring(1).Trim().Trim('"'), false));
            }
            if (questions.Count == 0)
            {
                questions.Add(new Question { Text = "No questions were written for this quiz. Is this the right answer?", Answers = { ("Yes", true), ("No", false) } });
            }
            if (Ctx.GetBool("shuffle", true)) questions = questions.OrderBy(_ => Ctx.Next(1000)).ToList();
            int pass = Ctx.GetInt("pass", Mathf.CeilToInt(questions.Count * 0.6f));
            float perQuestion = Ctx.GetFloat("time", 0f);
            int correct = 0;

            for (int qi = 0; qi < questions.Count; qi++)
            {
                var q = questions[qi];
                var page = UIKit.Rect("Question " + qi, Board);
                var g = UIKit.Group(page.gameObject);
                g.alpha = 0;
                var counter = UIKit.Text(page, "Counter", $"QUESTION {qi + 1} OF {questions.Count}", 32, new Color(1, 1, 1, 0.6f), TextAlignmentOptions.Center, true);
                UIKit.Place(counter.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(Width, 60));
                var card = UIKit.Image(page, "Card", ProceduralArt.RoundedRect(40), Color.white);
                var qt = UIKit.Text(card.transform, "Q", q.Text, 46, Theme.bubbleTextColor);
                float qh = Mathf.Max(220, qt.GetPreferredValues(q.Text, Width - 140, 0).y + 90);
                UIKit.Place(card.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(Width - 60, qh));
                UIKit.Stretch(qt.rectTransform, 40);

                int chosen = -1;
                var answers = Ctx.GetBool("shuffleanswers", true) ? q.Answers.OrderBy(_ => Ctx.Next(1000)).ToList() : q.Answers;
                var buttons = new List<Button>();
                float y = -100 - qh - 50;
                for (int a = 0; a < answers.Count; a++)
                {
                    int ai = a;
                    var b = UIKit.Button(page, "Answer " + a, answers[a].text, new Color(1, 1, 1, 0.12f), Color.white, () => { if (chosen < 0) chosen = ai; }, 42, 50);
                    UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, y - a * 150), new Vector2(Width - 60, 128));
                    buttons.Add(b);
                }
                yield return Tween.Fade(g, 1, 0.25f);

                float t = 0;
                Image bar = null;
                if (perQuestion > 0)
                {
                    bar = UIKit.Image(page, "Time", ProceduralArt.White(), Theme.accentColor);
                    UIKit.Place(bar.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(Width - 60, 10));
                    bar.type = Image.Type.Filled; bar.fillMethod = Image.FillMethod.Horizontal;
                }
                while (chosen < 0)
                {
                    t += Time.deltaTime;
                    if (bar != null) { bar.fillAmount = 1 - t / perQuestion; if (t >= perQuestion) break; }
                    yield return null;
                }
                bool right = chosen >= 0 && answers[chosen].correct;
                if (right) correct++;
                for (int a = 0; a < answers.Count; a++)
                {
                    if (answers[a].correct) buttons[a].image.color = new Color(0.35f, 0.8f, 0.45f);
                    else if (a == chosen) buttons[a].image.color = new Color(0.9f, 0.3f, 0.35f);
                }
                if (right) { Ctx.PlaySound("right", "success", 0.5f); StartCoroutine(Bounce((RectTransform)buttons[chosen].transform)); }
                else { Ctx.PlaySound("wrong", "fail", 0.5f); if (chosen >= 0) StartCoroutine(Shake((RectTransform)buttons[chosen].transform)); }
                SetScore($"{correct} correct");
                yield return Tween.Wait(1.1f);
                yield return Tween.Fade(g, 0, 0.2f);
                Destroy(page.gameObject);
            }
            Ctx.Score = correct;
            Ctx.Won = correct >= pass;
        }

        protected override int StarsFor()
        {
            int total = Mathf.Max(1, Ctx.Data.Count(l => l.Trim().StartsWith("*") || l.Trim().StartsWith("+")));
            float k = Ctx.Score / total;
            return k >= 0.99f ? 3 : k >= 0.7f ? 2 : 1;
        }
    }

    /// <summary>
    /// Keypad: enter the right code. Safes, phones, door locks.
    /// minigame keypad code=0614 tries=3 hint="Maya's birthday"
    /// Skin: key, display, background
    /// </summary>
    [Minigame("keypad", "code", "passcode", "safe")]
    public sealed class KeypadGame : Minigame
    {
        public override string DefaultTitle => "Enter the Code";
        public override string DefaultInstructions => "Type the code on the keypad.";
        protected override bool UsesTimer => Ctx.GetFloat("time", 0) > 0;
        protected override float DefaultTime => 0f;

        protected override IEnumerator Play()
        {
            string code = (Ctx.Get("code") ?? Ctx.Get("arg1") ?? "1234").ToUpperInvariant();
            int tries = Ctx.GetInt("tries", 3);
            string hint = Ctx.Get("hint");
            string entry = "";
            bool solved = false;
            int used = 0;

            if (!string.IsNullOrEmpty(hint))
            {
                var h = UIKit.Text(Board, "Hint", hint, 38, new Color(1, 1, 1, 0.8f));
                UIKit.Place(h.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(Width - 60, 100));
            }
            var display = SkinImage(Board, "display", ProceduralArt.RoundedRect(30), new Color(0.05f, 0.08f, 0.1f, 0.95f));
            UIKit.Place(display.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(Mathf.Min(700, Width - 80), 160));
            var digits = UIKit.Text(display.transform, "Digits", "", 90, new Color(0.45f, 1f, 0.7f), TextAlignmentOptions.Center, true);
            digits.characterSpacing = 30;

            void Refresh() => digits.text = entry + new string('_', Mathf.Max(0, code.Length - entry.Length));
            Refresh();

            string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "⌫", "0", "OK" };
            float key = Mathf.Min(200, (Width - 120) / 3f);
            for (int i = 0; i < keys.Length; i++)
            {
                string k = keys[i];
                var b = SkinButton(Board, "key", k == "⌫" ? "DEL" : k, k == "OK" ? Theme.accentColor : new Color(1, 1, 1, 0.14f), null, k.Length > 1 ? 40 : 64, 36);
                b.GetComponentInChildren<TextMeshProUGUI>().font = UIKit.TitleFont;
                UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2((i % 3 - 1) * (key + 24), -340 - (i / 3) * (key * 0.8f + 24)), new Vector2(key, key * 0.8f));
                b.onClick.AddListener(() =>
                {
                    if (solved || used >= tries) return;
                    if (k == "⌫") { if (entry.Length > 0) entry = entry.Substring(0, entry.Length - 1); }
                    else if (k == "OK") Submit();
                    else if (entry.Length < code.Length) { entry += k; AudioDirector.Instance?.PlaySound("blip", 0.8f, 1f + i * 0.04f); if (entry.Length == code.Length && Ctx.GetBool("autosubmit", true)) Submit(); }
                    Refresh();
                });
            }

            void Submit()
            {
                if (entry.Length == 0) return;
                used++;
                if (entry == code) { solved = true; digits.color = Theme.accentColor; return; }
                Ctx.PlaySound("wrong", "fail", 0.6f);
                StartCoroutine(Shake(display.rectTransform));
                entry = "";
                SetScore($"{tries - used} tries left");
                Refresh();
            }

            SetScore($"{tries} tries");
            while (!solved && used < tries && !TimedOut) yield return null;
            Ctx.Won = solved;
            Ctx.Score = solved ? tries - used + 1 : 0;
        }

        protected override int StarsFor() => Mathf.Clamp((int)Ctx.Score, 1, 3);
    }

    /// <summary>
    /// Sequence (Simon): watch the pads light up, then repeat the pattern.
    /// minigame sequence length=5 pads=4
    /// Skin: pad_1 ... pad_4 sprites, note_1 ... note_4 sounds
    /// </summary>
    [Minigame("sequence", "simon", "pattern", "repeat")]
    public sealed class SequenceGame : Minigame
    {
        public override string DefaultTitle => "Follow the Pattern";
        public override string DefaultInstructions => "Watch the lights, then tap them in the same order.";
        protected override bool UsesTimer => false;

        protected override IEnumerator Play()
        {
            int length = Ctx.GetInt("length", Mathf.RoundToInt(Ctx.Difficulty(4, 5, 7)));
            int pads = Mathf.Clamp(Ctx.GetInt("pads", 4), 2, 6);
            int startLen = Mathf.Min(length, Ctx.GetInt("start", 2));
            var seq = Enumerable.Range(0, length).Select(_ => Ctx.Next(pads)).ToList();
            var buttons = new List<Button>();
            var baseColors = new List<Color>();
            float size = Mathf.Min(380, (Width - 100) / 2f);
            int cols = pads <= 4 ? 2 : 3;
            float[] freqs = { 1f, 1.26f, 1.5f, 1.68f, 2f, 2.24f };
            int input = -1;
            for (int i = 0; i < pads; i++)
            {
                int pi = i;
                var color = Palette[i % Palette.Length];
                var b = SkinButton(Board, "pad_" + (i + 1), null, color * 0.7f, () => input = pi, 40, 60);
                int rows = Mathf.CeilToInt(pads / (float)cols);
                UIKit.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2((i % cols - (cols - 1) / 2f) * (size + 30), ((rows - 1) / 2f - i / cols) * (size + 30) - 40), new Vector2(size, size));
                buttons.Add(b);
                baseColors.Add(b.image.color);
            }

            IEnumerator Light(int i, float time)
            {
                var img = buttons[i].image;
                var clip = Ctx.Sound("note_" + (i + 1));
                if (clip != null) AudioDirector.Instance?.PlayClip(clip);
                else AudioDirector.Instance?.PlaySound("blip", 1f, freqs[i % freqs.Length]);
                img.color = Color.Lerp(baseColors[i], Color.white, 0.65f);
                StartCoroutine(Bounce((RectTransform)buttons[i].transform, 1.06f));
                yield return Tween.Wait(time);
                img.color = baseColors[i];
            }

            int reached = 0;
            for (int len = startLen; len <= length; len++)
            {
                foreach (var b in buttons) b.interactable = false;
                SetScore($"{len - 1} / {length}");
                yield return Tween.Wait(0.6f);
                float speed = Mathf.Lerp(0.55f, 0.32f, len / (float)length);
                for (int s = 0; s < len; s++)
                {
                    yield return Light(seq[s], speed);
                    yield return Tween.Wait(0.12f);
                }
                foreach (var b in buttons) b.interactable = true;
                for (int s = 0; s < len; s++)
                {
                    input = -1;
                    while (input < 0) yield return null;
                    StartCoroutine(Light(input, 0.18f));
                    if (input != seq[s])
                    {
                        Ctx.PlaySound("wrong", "fail", 0.6f);
                        StartCoroutine(Shake(Board, 14));
                        Ctx.Score = reached;
                        Ctx.Won = false;
                        yield break;
                    }
                }
                reached = len;
                FloatText(new Vector2(0, Height / 2 - 120), "Good!", Theme.accentColor);
            }
            Ctx.Score = reached;
            Ctx.Won = true;
        }
    }
}
