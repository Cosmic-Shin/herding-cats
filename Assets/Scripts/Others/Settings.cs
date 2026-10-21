using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

public class Settings : MonoBehaviour
{
    public static bool PostProcessing = true, Lights = true, LowParticles, DamageNumbers = true, VSync = true, Fullscreen = true;
    public static int FpsCap = 3, Volume = 5, MusicVolume = 6;
    public static int Flashes = 2, Shake = 2, Effects = 3;
    public static float ShakeScale => Shake == 2 ? 1f : Shake == 1 ? 0.4f : 0f;
    public static float FlashScale => Flashes == 2 ? 1f : Flashes == 1 ? 0.4f : 0f;
    private static readonly string[] FlashNames = { "Off", "Reduced", "Full" };
    private static readonly float[] EffectAlphas = { 0.35f, 0.55f, 0.75f, 1f };
    public static float EffectAlpha => EffectAlphas[Effects];

    private static readonly int[] FpsCaps = { 30, 60, 120, -1 };
    private static readonly string[] FpsNames = { "30", "60", "120", "Unlimited" };
    private static readonly string[] QualityNames = { "Low", "Medium", "High", "Custom" };

    public static bool IsOpen => instance != null;
    private static Settings instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        bool B(string key, bool fallback) => PlayerPrefs.GetInt(key, fallback ? 1 : 0) == 1;
        PostProcessing = B("set_post", true);
        Lights = B("set_lights", true);
        LowParticles = B("set_lowParticles", false);
        DamageNumbers = B("set_damageNumbers", true);
        Shake = Mathf.Clamp(PlayerPrefs.GetInt("set_shake_level", B("set_shake", true) ? 2 : 0), 0, 2);
        VSync = B("set_vsync", true);
        Fullscreen = B("set_fullscreen", Screen.fullScreen);
        FpsCap = Mathf.Clamp(PlayerPrefs.GetInt("set_fps", 3), 0, FpsCaps.Length - 1);
        Volume = Mathf.Clamp(PlayerPrefs.GetInt("set_volume", 5), 0, 10);
        MusicVolume = Mathf.Clamp(PlayerPrefs.GetInt("set_music", 6), 0, 10);
        Flashes = Mathf.Clamp(PlayerPrefs.GetInt("set_flashes", 2), 0, 2);
        Effects = Mathf.Clamp(PlayerPrefs.GetInt("set_effects", 3), 0, EffectAlphas.Length - 1);
        Apply();
    }

    private static void Save()
    {
        void B(string key, bool value) => PlayerPrefs.SetInt(key, value ? 1 : 0);
        B("set_post", PostProcessing);
        B("set_lights", Lights);
        B("set_lowParticles", LowParticles);
        B("set_damageNumbers", DamageNumbers);
        PlayerPrefs.SetInt("set_shake_level", Shake);
        B("set_vsync", VSync);
        B("set_fullscreen", Fullscreen);
        PlayerPrefs.SetInt("set_fps", FpsCap);
        PlayerPrefs.SetInt("set_volume", Volume);
        PlayerPrefs.SetInt("set_music", MusicVolume);
        PlayerPrefs.SetInt("set_flashes", Flashes);
        PlayerPrefs.SetInt("set_effects", Effects);
        PlayerPrefs.Save();
    }

    private static void Apply()
    {
        Sfx.Volume = Volume / 10f;
        Music.Volume = MusicVolume / 10f;
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        Application.targetFrameRate = FpsCaps[FpsCap];
        if (Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
        CameraFollow.SetPostProcessing(PostProcessing);
        Lighting.SetDynamic(Lights);
    }

    private static int Preset => !PostProcessing && !Lights && LowParticles ? 0
                               : PostProcessing && !Lights && !LowParticles ? 1
                               : PostProcessing && Lights && !LowParticles ? 2 : 3;

    private static void SetPreset(int preset)
    {
        PostProcessing = preset >= 1;
        Lights = preset >= 2;
        LowParticles = preset == 0;
    }

    private class Option
    {
        public string label;
        public Func<string> value;
        public Action<int> step;
        public TextMeshProUGUI valueText;
    }

    private readonly List<Option> options = new List<Option>();
    private GameObject returnFocus;

    public static void Open(GameObject focusAfterClose)
    {
        if (instance != null) return;
        instance = new GameObject("Settings").AddComponent<Settings>();
        instance.returnFocus = focusAfterClose;
        instance.Build();
    }

    public static void Close()
    {
        if (instance == null) return;
        GameObject focus = instance.returnFocus;
        Destroy(instance.gameObject);
        instance = null;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(focus);
    }

    private static string OnOff(bool on) => on ? "On" : "Off";
    private static int Wrap(int v, int count) => (v % count + count) % count;

    private void Build()
    {
        options.Add(new Option { label = "Quality", value = () => QualityNames[Preset], step = d => SetPreset(Wrap((Preset == 3 ? 2 : Preset) + d, 3)) });
        options.Add(new Option { label = "Bloom & vignette", value = () => OnOff(PostProcessing), step = _ => PostProcessing = !PostProcessing });
        options.Add(new Option { label = "Dynamic lights", value = () => OnOff(Lights), step = _ => Lights = !Lights });
        options.Add(new Option { label = "Particles", value = () => LowParticles ? "Low" : "High", step = _ => LowParticles = !LowParticles });
        options.Add(new Option { label = "Effect opacity", value = () => $"{Mathf.RoundToInt(EffectAlpha * 100)}%", step = d => Effects = Mathf.Clamp(Effects + d, 0, EffectAlphas.Length - 1) });
        options.Add(new Option { label = "Damage numbers", value = () => OnOff(DamageNumbers), step = _ => DamageNumbers = !DamageNumbers });
        options.Add(new Option { label = "Screen shake", value = () => FlashNames[Shake], step = d => Shake = Wrap(Shake + d, 3) });
        options.Add(new Option { label = "Screen flashes", value = () => FlashNames[Flashes], step = d => Flashes = Wrap(Flashes + d, 3) });
        options.Add(new Option { label = "FPS limit", value = () => FpsNames[FpsCap], step = d => FpsCap = Wrap(FpsCap + d, FpsCaps.Length) });
        options.Add(new Option { label = "VSync", value = () => OnOff(VSync), step = _ => VSync = !VSync });
        options.Add(new Option { label = "Fullscreen", value = () => OnOff(Fullscreen), step = _ => Fullscreen = !Fullscreen });
        options.Add(new Option { label = "Music volume", value = () => $"{MusicVolume * 10}%", step = d => MusicVolume = Mathf.Clamp(MusicVolume + d, 0, 10) });
        options.Add(new Option { label = "Sound volume", value = () => $"{Volume * 10}%", step = d => Volume = Mathf.Clamp(Volume + d, 0, 10) });

        Canvas canvas = RuntimeUI.Canvas("SettingsCanvas", 600);
        canvas.transform.SetParent(transform, false);
        Image dim = RuntimeUI.Box(canvas.transform, "Dim", Vector2.zero, Vector2.one, new Color(0.03f, 0.02f, 0.08f, 0.85f));
        RectTransform panel = RuntimeUI.Frame(dim.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).rectTransform;
        const float rowH = 44f;
        float height = 150f + options.Count * rowH;
        panel.sizeDelta = new Vector2(640, height);
        float top = height / 2f;

        RuntimeUI.Title(panel, "SETTINGS", 48f, new Vector2(0, top - 40), 600f);

        Button firstArrow = null;
        for (int i = 0; i < options.Count; i++)
        {
            Option o = options[i];
            float y = top - 95f - i * rowH;
            RuntimeUI.Outline(RuntimeUI.Text(panel, o.label, 22, new Color(0.8f, 0.84f, 0.95f), new Vector2(-130, y), new Vector2(300, rowH), TextAlignmentOptions.MidlineLeft));
            Button left = RuntimeUI.Button(panel, "<", new Vector2(80, y), new Vector2(44, 38), () => Change(o, -1));
            o.valueText = RuntimeUI.Outline(RuntimeUI.Text(panel, "", 22, Color.white, new Vector2(175, y), new Vector2(140, rowH)));
            RuntimeUI.Button(panel, ">", new Vector2(270, y), new Vector2(44, 38), () => Change(o, +1));
            if (firstArrow == null) firstArrow = left;
        }

        RuntimeUI.Button(panel, "Controls", new Vector2(-200, -top + 38), new Vector2(200, 48), ShowControls);
        RuntimeUI.Button(panel, "Back", new Vector2(0, -top + 38), new Vector2(180, 48), Close);
        RuntimeUI.Text(panel, "Esc / B to close", 16, new Color(0.6f, 0.64f, 0.75f), new Vector2(215, -top + 38), new Vector2(190, 30));
        Refresh();
        mainPanel = panel;
        mainFocus = firstArrow.gameObject;
        BuildControls(dim.transform);
        Select(mainFocus);
    }

    // ---- Controls page: click an action, then press the new key. ----

    private RectTransform mainPanel, controlsPanel;
    private GameObject mainFocus;
    private Button[] keyButtons;
    private int listening = -1, busyFrame = -1;

    // True while waiting for a key (or on the frame one was taken), so menus don't also treat that key as "back".
    public static bool Busy => instance != null && (instance.listening >= 0 || instance.busyFrame == Time.frameCount);

    // Esc / gamepad back: cancel a rebind, then leave Controls, then close.
    public static void Back()
    {
        if (instance == null) return;
        if (instance.listening >= 0) instance.StopListening();
        else if (instance.controlsPanel.gameObject.activeSelf) instance.ShowMainPage();
        else Close();
    }

    private void BuildControls(Transform parent)
    {
        int n = Keybinds.Names.Length;
        const float rowH = 52f;
        controlsPanel = RuntimeUI.Frame(parent, "Controls", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).rectTransform;
        float height = 230f + n * rowH;
        controlsPanel.sizeDelta = new Vector2(640, height);
        float top = height / 2f;

        RuntimeUI.Title(controlsPanel, "CONTROLS", 48f, new Vector2(0, top - 40), 600f);
        keyButtons = new Button[n];
        for (int i = 0; i < n; i++)
        {
            int action = i;
            float y = top - 100f - i * rowH;
            RuntimeUI.Outline(RuntimeUI.Text(controlsPanel, Keybinds.Names[i], 22, new Color(0.8f, 0.84f, 0.95f), new Vector2(-130, y), new Vector2(300, rowH), TextAlignmentOptions.MidlineLeft));
            keyButtons[i] = RuntimeUI.Button(controlsPanel, "", new Vector2(160, y), new Vector2(240, 44), () => Listen(action));
        }
        RuntimeUI.Text(controlsPanel, "Pad: stick move  A dash  Y special  View combine  Start pause", 16, new Color(0.8f, 0.84f, 0.95f), new Vector2(0, -top + 116), new Vector2(620, 24));
        RuntimeUI.Text(controlsPanel, "Arrow keys always move.  Esc always pauses.", 16, new Color(0.6f, 0.64f, 0.75f), new Vector2(0, -top + 88), new Vector2(600, 24));
        RuntimeUI.Button(controlsPanel, "Reset", new Vector2(-110, -top + 38), new Vector2(180, 48), () => { Keybinds.ResetAll(); RefreshKeys(); });
        RuntimeUI.Button(controlsPanel, "Back", new Vector2(110, -top + 38), new Vector2(180, 48), ShowMainPage);
        RefreshKeys();
        controlsPanel.gameObject.SetActive(false);
    }

    private void ShowControls()
    {
        mainPanel.gameObject.SetActive(false);
        controlsPanel.gameObject.SetActive(true);
        Select(keyButtons[0].gameObject);
    }

    private void ShowMainPage()
    {
        controlsPanel.gameObject.SetActive(false);
        mainPanel.gameObject.SetActive(true);
        Select(mainFocus);
    }

    private void Listen(int action)
    {
        if (listening >= 0) return;
        listening = action;
        SetKeyLabel(action, "<color=#FFE08A>Press a key...</color>");
        Select(null); // so the key we're waiting for can't also "click" a button
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (listening < 0 || kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame) { StopListening(); return; }
        foreach (KeyControl k in kb.allKeys)
        {
            if (k == null || !k.wasPressedThisFrame || k.keyCode == Key.Escape) continue;
            Keybinds.Set((Keybinds.Action)listening, k.keyCode);
            StopListening();
            return;
        }
    }

    private void StopListening()
    {
        int action = listening;
        listening = -1;
        busyFrame = Time.frameCount;
        RefreshKeys();
        StartCoroutine(SelectNextFrame(keyButtons[action].gameObject));
    }

    private IEnumerator SelectNextFrame(GameObject go)
    {
        yield return null;
        Select(go);
    }

    private void RefreshKeys()
    {
        for (int i = 0; i < keyButtons.Length; i++) SetKeyLabel(i, Keybinds.Label((Keybinds.Action)i));
    }

    private void SetKeyLabel(int i, string text)
    {
        TextMeshProUGUI t = keyButtons[i].GetComponentInChildren<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = 22;
    }

    private static void Select(GameObject go)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(go);
    }

    private void Change(Option o, int direction)
    {
        o.step(direction);
        Save();
        Apply();
        Refresh();
    }

    private void Refresh()
    {
        foreach (Option o in options) o.valueText.text = o.value();
    }
}
