using System.Collections.Generic;
using UnityEngine;

// Sound effects synthesized in code at startup, so no audio files are needed.
// Swap a clip for a real one later by assigning it in Clip() below.
// Each sound has a minimum gap between plays so a 200-cat crowd doesn't turn into noise.
public static class Sfx
{
    public enum Id { Boom, Hit, Pop, Xp, LevelUp, Hurt, Pickup, Alert, Crate, Heartbeat, Ready, Swish, Zap, Crackle, Whoosh, Shatter, Step, Meow, Dash, Click, Tick, Clang, Chest, Blink, GameOver }

    public static float Volume = 0.5f; // master volume for every effect

    private const int Rate = 44100;
    private const int Voices = 16;

    private static readonly Dictionary<Id, AudioClip> clips = new Dictionary<Id, AudioClip>();
    private static readonly Dictionary<Id, float> nextAllowed = new Dictionary<Id, float>();
    private static AudioSource[] sources;
    private static int nextSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        sources = null;
        nextAllowed.Clear();
    }

    // volume 0..1 on top of Volume; pitch 1 = as synthesized
    public static void Play(Id id, float volume = 1f, float pitch = 1f)
    {
        float now = Time.unscaledTime;
        if (nextAllowed.TryGetValue(id, out float t) && now < t) return;
        nextAllowed[id] = now + MinGap(id);

        if (sources == null || sources[0] == null) CreateSources();
        AudioSource s = sources[nextSource = (nextSource + 1) % Voices];
        s.pitch = pitch * Random.Range(0.95f, 1.05f);
        s.PlayOneShot(Clip(id), volume * Volume);
    }

    private static float MinGap(Id id) => id switch
    {
        Id.Hit => 0.045f,
        Id.Pop => 0.05f,
        Id.Xp => 0.04f,
        Id.Boom => 0.06f,
        Id.Swish => 0.05f,
        Id.Zap => 0.05f,
        Id.Step => 0.1f,
        Id.Meow => 0.4f,
        Id.Tick => 0.03f,
        Id.Click => 0.04f,
        Id.Dash => 0.1f,
        _ => 0.08f,
    };

    private static void CreateSources()
    {
        var go = new GameObject("Sfx");
        Object.DontDestroyOnLoad(go);
        sources = new AudioSource[Voices];
        for (int i = 0; i < Voices; i++)
        {
            sources[i] = go.AddComponent<AudioSource>();
            sources[i].playOnAwake = false;
            sources[i].ignoreListenerPause = true; // menus still click while paused
        }
    }

    private static AudioClip Clip(Id id)
    {
        if (clips.TryGetValue(id, out AudioClip c) && c != null) return c;
        c = id switch
        {
            // low thump + rumbling noise that darkens as it fades
            Id.Boom => Make(id, 0.5f, (t, s) => Noise(s, 1f - t * 1.6f) * Decay(t, 7f) * 0.8f
                                                 + Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(90f, 40f, t * 2f) * t) * Decay(t, 9f)),
            Id.Hit => Make(id, 0.05f, (t, s) => (Noise(s, 0.9f) * 0.6f + Square(900f, t) * 0.3f) * Decay(t, 60f) * 0.6f),
            Id.Pop => Make(id, 0.09f, (t, s) => Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(500f, 1300f, t / 0.09f) * t) * Decay(t, 30f) * 0.5f),
            Id.Xp => Make(id, 0.07f, (t, s) => Square(1320f + t * 4000f, t) * Decay(t, 40f) * 0.25f),
            Id.LevelUp => Make(id, 0.5f, (t, s) =>
            {
                float[] notes = { 523f, 659f, 784f, 1047f }; // C E G C
                int n = Mathf.Min(3, (int)(t / 0.09f));
                return Square(notes[n], t) * Decay(t - n * 0.09f, n == 3 ? 6f : 18f) * 0.3f;
            }),
            Id.Hurt => Make(id, 0.22f, (t, s) => (Square(Mathf.Lerp(320f, 110f, t / 0.22f), t) * 0.5f + Noise(s, 0.6f) * 0.3f) * Decay(t, 12f) * 0.7f),
            Id.Pickup => Make(id, 0.25f, (t, s) => Mathf.Sin(2f * Mathf.PI * (t < 0.08f ? 880f : 1320f) * t) * Decay(t < 0.08f ? t : t - 0.08f, 14f) * 0.5f),
            Id.Alert => Make(id, 0.8f, (t, s) => Square(Mathf.Repeat(t, 0.4f) < 0.2f ? 660f : 880f, t) * 0.22f * Mathf.Clamp01((0.8f - t) * 10f)),
            // lub-dub: two low sine thumps
            Id.Heartbeat => Make(id, 0.35f, (t, s) => Mathf.Sin(2f * Mathf.PI * 55f * t) * (Decay(t, 25f) + (t > 0.16f ? Decay(t - 0.16f, 30f) * 0.7f : 0f)) * 0.9f),
            // bright two-note sparkle
            Id.Ready => Make(id, 0.35f, (t, s) => Mathf.Sin(2f * Mathf.PI * (t < 0.1f ? 1047f : 1568f) * t) * Decay(t < 0.1f ? t : t - 0.1f, 10f) * 0.4f),
            // weapons
            Id.Swish => Make(id, 0.16f, (t, s) => Noise(s, Mathf.Lerp(0.15f, 0.6f, t / 0.16f)) * Mathf.Sin(Mathf.PI * t / 0.16f) * 0.6f),         // air cut
            Id.Zap => Make(id, 0.2f, (t, s) => Square(Mathf.Lerp(1400f, 300f, t / 0.2f) * (1f + 0.3f * Mathf.Sin(t * 400f)), t) * Decay(t, 14f) * 0.25f),
            Id.Crackle => Make(id, 0.3f, (t, s) => Noise(s, 1f) * (Random.value < 0.35f ? 1f : 0.2f) * Decay(t, 9f) * 0.7f),      // electric snap
            Id.Whoosh => Make(id, 0.35f, (t, s) => Noise(s, Mathf.Lerp(0.05f, 0.35f, t / 0.35f)) * Mathf.Sin(Mathf.PI * t / 0.35f) * 0.7f),
            Id.Shatter => Make(id, 0.3f, (t, s) => (Noise(s, 1f) * 0.5f + Mathf.Sin(2f * Mathf.PI * (2400f + 900f * Mathf.Sin(t * 90f)) * t) * 0.3f) * Decay(t, 12f) * 0.6f),
            Id.Step => Make(id, 0.05f, (t, s) => Noise(s, 0.2f) * Decay(t, 70f) * 0.6f),                                     // soft scuff
            Id.Meow => Make(id, 0.28f, (t, s) => Mathf.Sin(2f * Mathf.PI * (520f + 260f * Mathf.Sin(Mathf.PI * t / 0.28f)) * t)  // "mi-AOW"
                                                  * Mathf.Sin(Mathf.PI * t / 0.28f) * 0.35f),
            // quick rising air burst
            Id.Dash => Make(id, 0.18f, (t, s) => Noise(s, Mathf.Lerp(0.1f, 0.7f, t / 0.18f)) * Mathf.Sin(Mathf.PI * Mathf.Pow(t / 0.18f, 0.6f)) * 0.75f
                                                 + Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(200f, 600f, t / 0.18f) * t) * Decay(t, 20f) * 0.15f),
            // blink: a bright warp down then up
            Id.Blink => Make(id, 0.22f, (t, s) => Mathf.Sin(2f * Mathf.PI * (900f + 700f * Mathf.Cos(t * 28f)) * t) * Decay(t, 10f) * 0.35f + Noise(s, 0.8f) * Decay(t, 25f) * 0.2f),
            // menus: a crisp blip to confirm, a tiny tick when the highlight moves
            Id.Click => Make(id, 0.06f, (t, s) => Square(t < 0.025f ? 990f : 1480f, t) * Decay(t, 45f) * 0.3f),
            Id.Tick => Make(id, 0.025f, (t, s) => Square(1760f, t) * Decay(t, 140f) * 0.18f),
            // anvil: metallic ring with a few inharmonic partials
            Id.Clang => Make(id, 0.6f, (t, s) => (Mathf.Sin(2f * Mathf.PI * 880f * t) + Mathf.Sin(2f * Mathf.PI * 1390f * t) * 0.6f
                                                 + Mathf.Sin(2f * Mathf.PI * 2210f * t) * 0.4f) * Decay(t, 7f) * 0.3f + Noise(s, 1f) * Decay(t, 60f) * 0.5f),
            // game over: a slow, sad four-note slide down
            Id.GameOver => Make(id, 1.6f, (t, s) =>
            {
                float[] notes = { 659f, 587f, 523f, 392f }; // E D C G
                int n = Mathf.Min(3, (int)(t / 0.3f));
                return Square(notes[n] * (n == 3 ? 1f - 0.03f * (t - 0.9f) : 1f), t) * Decay(t - n * 0.3f, n == 3 ? 2f : 5f) * 0.22f;
            }),
            // chest: lid thunk, then a rising sparkle
            Id.Chest => Make(id, 0.7f, (t, s) => Noise(s, 0.3f) * Decay(t, 35f) * 0.7f
                                                 + (t > 0.12f ? Square(new[] { 784f, 988f, 1175f, 1568f }[Mathf.Min(3, (int)((t - 0.12f) / 0.07f))], t) * Decay(t - 0.12f, 4f) * 0.18f : 0f)),
            // Crate: wooden crunch (low knock + crackly noise) with a few splinter ticks
            _ => Make(id, 0.3f, (t, s) => Noise(s, 0.45f) * Decay(t, 18f) * 0.9f
                                       + Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(160f, 90f, t / 0.3f) * t) * Decay(t, 22f) * 0.6f
                                       + (Random.value < 0.04f ? (Random.value * 2f - 1f) * 1.5f : 0f) * Decay(t, 8f)),
        };
        clips[id] = c;
        return c;
    }

    // ---- tiny synth ----

    private static float noiseLow;
    private static System.Random rng = new System.Random(7);

    // white noise through a one-pole low-pass; brightness 0..1
    private static float Noise(int sample, float brightness)
    {
        if (sample == 0) noiseLow = 0f;
        float white = (float)(rng.NextDouble() * 2.0 - 1.0);
        noiseLow += (white - noiseLow) * Mathf.Clamp(brightness, 0.02f, 1f);
        return noiseLow;
    }

    private static float Square(float freq, float t) => Mathf.Repeat(freq * t, 1f) < 0.5f ? 1f : -1f;
    private static float Decay(float t, float speed) => Mathf.Exp(-Mathf.Max(0f, t) * speed);

    private static AudioClip Make(Id id, float seconds, System.Func<float, int, float> wave)
    {
        int n = Mathf.CeilToInt(seconds * Rate);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float fade = Mathf.Clamp01((n - i) / (Rate * 0.005f)); // 5ms tail fade, no click at the end
            data[i] = Mathf.Clamp(wave(t, i), -1f, 1f) * fade;
        }
        AudioClip clip = AudioClip.Create("sfx_" + id, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
