using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VNEngine
{
    /// <summary>Music with crossfades, an ambience loop, sound effects and soft UI clicks.</summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        AudioSource musicA, musicB, ambience;
        readonly List<AudioSource> sfx = new List<AudioSource>();
        bool aIsActive = true;
        Coroutine musicFade, ambienceFade;

        public string CurrentMusic { get; private set; }
        public string CurrentAmbience { get; private set; }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat("vn_music_volume", 0.8f);
            set { PlayerPrefs.SetFloat("vn_music_volume", value); Instance?.ApplyVolumes(); }
        }

        public static float SoundVolume
        {
            get => PlayerPrefs.GetFloat("vn_sound_volume", 1f);
            set => PlayerPrefs.SetFloat("vn_sound_volume", value);
        }

        public static AudioDirector Instance { get; private set; }

        float musicTarget = 1f, ambienceTarget = 1f;
        AudioClip click, pop, blip, success, fail, whoosh;

        void Awake()
        {
            Instance = this;
            musicA = NewSource(true);
            musicB = NewSource(true);
            ambience = NewSource(true);
            for (int i = 0; i < 6; i++) sfx.Add(NewSource(false));
            click = Synth("click", 0.05f, f => 1400f - 900f * f, 0.25f);
            pop = Synth("pop", 0.09f, f => 500f + 600f * f, 0.3f);
            blip = Synth("blip", 0.03f, f => 900f, 0.08f);
            success = Chime("success", new[] { 523.25f, 659.25f, 783.99f, 1046.5f });
            fail = Chime("fail", new[] { 392f, 311.13f, 261.63f });
            whoosh = Noise("whoosh", 0.35f);
        }

        AudioSource NewSource(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        void ApplyVolumes()
        {
            var active = aIsActive ? musicA : musicB;
            if (musicFade == null) active.volume = musicTarget * MusicVolume;
            if (ambienceFade == null) ambience.volume = ambienceTarget * MusicVolume;
        }

        public void PlayMusic(string name, float fade = 1.2f, float volume = 1f, bool loop = true)
        {
            if (string.IsNullOrEmpty(name) || name == "stop" || name == "none") { StopMusic(fade); return; }
            if (name == CurrentMusic) return;
            var clip = AssetLibrary.Music(name);
            if (clip == null)
            {
                AssetLibrary.WarnOnce($"No music called '{name}'. Put it in a Resources/Music folder.");
                CurrentMusic = name;
                return;
            }
            CurrentMusic = name;
            var from = aIsActive ? musicA : musicB;
            var to = aIsActive ? musicB : musicA;
            aIsActive = !aIsActive;
            to.clip = clip;
            to.loop = loop;
            to.volume = 0f;
            to.Play();
            musicTarget = volume;
            if (musicFade != null) StopCoroutine(musicFade);
            musicFade = StartCoroutine(Crossfade(from, to, fade, volume));
        }

        IEnumerator Crossfade(AudioSource from, AudioSource to, float time, float volume)
        {
            float fromStart = from.volume;
            float t = 0;
            while (t < time)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / Mathf.Max(0.01f, time));
                from.volume = fromStart * (1 - k);
                to.volume = volume * MusicVolume * k;
                yield return null;
            }
            from.Stop();
            to.volume = volume * MusicVolume;
            musicFade = null;
        }

        public void StopMusic(float fade = 1f)
        {
            CurrentMusic = null;
            var active = aIsActive ? musicA : musicB;
            if (musicFade != null) StopCoroutine(musicFade);
            musicFade = StartCoroutine(FadeOut(active, fade, () => musicFade = null));
        }

        IEnumerator FadeOut(AudioSource s, float time, System.Action done)
        {
            float start = s.volume, t = 0;
            while (t < time)
            {
                t += Time.unscaledDeltaTime;
                s.volume = start * (1 - Mathf.Clamp01(t / Mathf.Max(0.01f, time)));
                yield return null;
            }
            s.Stop();
            done?.Invoke();
        }

        public void PlayAmbience(string name, float fade = 1.5f, float volume = 0.7f)
        {
            if (string.IsNullOrEmpty(name) || name == "stop" || name == "none")
            {
                CurrentAmbience = null;
                if (ambienceFade != null) StopCoroutine(ambienceFade);
                ambienceFade = StartCoroutine(FadeOut(ambience, fade, () => ambienceFade = null));
                return;
            }
            if (name == CurrentAmbience) return;
            var clip = AssetLibrary.Sound(name) ?? AssetLibrary.Music(name);
            CurrentAmbience = name;
            if (clip == null) { AssetLibrary.WarnOnce($"No ambience sound called '{name}'. Put it in Resources/Sounds."); return; }
            ambience.clip = clip;
            ambience.volume = 0;
            ambience.Play();
            ambienceTarget = volume;
            if (ambienceFade != null) StopCoroutine(ambienceFade);
            ambienceFade = StartCoroutine(FadeIn(ambience, fade, volume));
        }

        IEnumerator FadeIn(AudioSource s, float time, float volume)
        {
            float t = 0;
            while (t < time)
            {
                t += Time.unscaledDeltaTime;
                s.volume = volume * MusicVolume * Mathf.Clamp01(t / Mathf.Max(0.01f, time));
                yield return null;
            }
            ambienceFade = null;
        }

        /// <summary>Plays a sound by name. Built-in names: click, pop, blip, success, fail, whoosh.</summary>
        public void PlaySound(string name, float volume = 1f, float pitch = 1f)
        {
            if (string.IsNullOrEmpty(name)) return;
            var clip = AssetLibrary.Sound(name) ?? BuiltIn(name);
            if (clip == null) { AssetLibrary.WarnOnce($"No sound called '{name}'. Put it in a Resources/Sounds folder."); return; }
            PlayClip(clip, volume, pitch);
        }

        public void PlayClip(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            AudioSource free = sfx[0];
            foreach (var s in sfx) if (!s.isPlaying) { free = s; break; }
            free.pitch = pitch;
            free.PlayOneShot(clip, volume * SoundVolume);
        }

        AudioClip BuiltIn(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "click": return click;
                case "pop": return pop;
                case "blip": return blip;
                case "success": case "win": return success;
                case "fail": case "lose": return fail;
                case "whoosh": return whoosh;
            }
            return null;
        }

        public void Click()
        {
            var theme = VNTheme.Load();
            if (theme.clickSound != null) PlayClip(theme.clickSound, 0.8f);
            else if (theme.proceduralClicks) PlayClip(click, 0.5f, Random.Range(0.95f, 1.05f));
        }

        public void Pop() { if (VNTheme.Load().proceduralClicks) PlayClip(pop, 0.35f, Random.Range(0.95f, 1.1f)); }

        public void TypeBlip(AudioClip voice)
        {
            var clip = voice != null ? voice : VNTheme.Load().typeSound;
            if (clip != null) PlayClip(clip, 0.35f, Random.Range(0.92f, 1.08f));
        }

        // ---------- tiny synth for built-in UI sounds ----------

        static AudioClip Synth(string name, float length, System.Func<float, float> freq, float volume)
        {
            int rate = 44100, n = Mathf.CeilToInt(length * rate);
            var data = new float[n];
            float phase = 0;
            for (int i = 0; i < n; i++)
            {
                float f = i / (float)n;
                phase += 2 * Mathf.PI * freq(f) / rate;
                float env = Mathf.Min(1f, i / (rate * 0.003f)) * Mathf.Pow(1 - f, 2.5f);
                data[i] = Mathf.Sin(phase) * env * volume;
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Chime(string name, float[] notes)
        {
            int rate = 44100;
            float noteLen = 0.11f, tail = 0.35f;
            int n = Mathf.CeilToInt((noteLen * notes.Length + tail) * rate);
            var data = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = Mathf.RoundToInt(k * noteLen * rate);
                int len = Mathf.RoundToInt((noteLen + tail) * rate);
                for (int i = 0; i < len && start + i < n; i++)
                {
                    float t = i / (float)rate;
                    float env = Mathf.Min(1f, i / (rate * 0.005f)) * Mathf.Exp(-t * 7f);
                    data[start + i] += (Mathf.Sin(2 * Mathf.PI * notes[k] * t) + 0.3f * Mathf.Sin(4 * Mathf.PI * notes[k] * t)) * env * 0.18f;
                }
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Noise(string name, float length)
        {
            int rate = 44100, n = Mathf.CeilToInt(length * rate);
            var data = new float[n];
            var rnd = new System.Random(7);
            float last = 0;
            for (int i = 0; i < n; i++)
            {
                float f = i / (float)n;
                float env = Mathf.Sin(f * Mathf.PI);
                float white = (float)(rnd.NextDouble() * 2 - 1);
                float cutoff = 0.05f + 0.25f * f;
                last += cutoff * (white - last);
                data[i] = last * env * 0.6f;
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
