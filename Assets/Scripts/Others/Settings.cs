using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
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
#if UNITY_WEBGL && !UNITY_EDITOR
        Application.targetFrameRate = -1;
#else
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        Application.targetFrameRate = FpsCaps[FpsCap];
        if (Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
#endif
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
#if UNITY_WEBGL && !UNITY_EDITOR
        options.Add(new Option { label = "Fullscreen", value = () => OnOff(Screen.fullScreen), step = _ => Screen.fullScreen = !Screen.fullScreen });
#else
        options.Add(new Option { label = "FPS limit", value = () => FpsNames[FpsCap], step = d => FpsCap = Wrap(FpsCap + d, FpsCaps.Length) });
        options.Add(new Option { label = "VSync", value = () => OnOff(VSync), step = _ => VSync = !VSync });
        options.Add(new Option { label = "Fullscreen", value = () => OnOff(Fullscreen), step = _ => Fullscreen = !Fullscreen });
#endif
        options.Add(new Option { label = "Music volume", value = () => $"{MusicVolume * 10}%", step = d => MusicVolume = Mathf.Clamp(MusicVolume + d, 0, 10) });
        options.Add(new Option { label = "Sound volume", value = () => $"{Volume * 10}%", step = d => Volume = Mathf.Clamp(Volume + d, 0, 10) });

        Canvas canvas = RuntimeUI.Canvas("SettingsCanvas", 600);
        canvas.transform.SetParent(transform, false);
        // Cat theme: solid plum panel over a dark dim, so the menu underneath doesn't show through.
        Image dim = RuntimeUI.Box(canvas.transform, "Dim", Vector2.zero, Vector2.one, CatTheme.DimColor);
        RectTransform panel = CatTheme.Panel(dim.transform, "Panel", CatTheme.Style.Panel, Vector2.zero, Vector2.zero).rectTransform;
        const float rowH = 38f; // 13 rows plus the framed footer buttons fit inside the 720-high screen
        float height = 170f + options.Count * rowH;
        panel.sizeDelta = new Vector2(640, height);
        float top = height / 2f;

        RuntimeUI.Title(panel, "SETTINGS", 48f, new Vector2(0, top - 40), 600f);

        var rows = new List<Button>();
        var lefts = new List<Button>();
        var rights = new List<Button>();
        for (int i = 0; i < options.Count; i++)
        {
            Option o = options[i];
            float y = top - 95f - i * rowH;
            RuntimeUI.Outline(RuntimeUI.Text(panel, o.label, 22, new Color(0.8f, 0.84f, 0.95f), new Vector2(-130, y), new Vector2(300, rowH), TextAlignmentOptions.MidlineLeft));
            // "<" sits well left of the value so the selection arrow (just left of the value) doesn't cover it.
            Button left = RuntimeUI.Button(panel, "<", new Vector2(40, y), new Vector2(44, 36), () => Change(o, -1));
            Button row = RuntimeUI.Button(panel, "", new Vector2(175, y), new Vector2(140, 36), () => Change(o, +1));
            o.valueText = RuntimeUI.Outline(RuntimeUI.Text(panel, "", 22, Color.white, new Vector2(175, y), new Vector2(140, rowH)));
            Button right = RuntimeUI.Button(panel, ">", new Vector2(270, y), new Vector2(44, 36), () => Change(o, +1));
            rows.Add(row);
            lefts.Add(left);
            rights.Add(right);
        }

        Button controls = RuntimeUI.Button(panel, "Controls", new Vector2(-190, -top + 42), new Vector2(150, 36), ShowControls);
        Button back = RuntimeUI.Button(panel, "Back", new Vector2(0, -top + 42), new Vector2(140, 36), Close);
        CatTheme.Button(controls);
        CatTheme.Button(back);
        // Arrows only move: left/right steps between <, the value and >; Enter / A presses whichever is selected.
        for (int i = 0; i < rows.Count; i++)
        {
            bool first = i == 0, last = i == rows.Count - 1;
            RuntimeUI.Nav(rows[i], up: first ? back : rows[i - 1], down: last ? back : rows[i + 1], left: lefts[i], right: rights[i]);
            RuntimeUI.Nav(lefts[i], up: first ? back : lefts[i - 1], down: last ? back : lefts[i + 1], right: rows[i]);
            RuntimeUI.Nav(rights[i], up: first ? back : rights[i - 1], down: last ? back : rights[i + 1], left: rows[i]);
        }
        RuntimeUI.Nav(back, up: rows[rows.Count - 1], down: rows[0], left: controls);
        RuntimeUI.Nav(controls, up: rows[rows.Count - 1], down: rows[0], right: back);
        RuntimeUI.Text(panel, $"Esc / {Keybinds.PadLabel(GamepadButton.East)} to close", 16, new Color(0.6f, 0.64f, 0.75f), new Vector2(215, -top + 42), new Vector2(190, 30));
        Refresh();
        mainPanel = panel;
        mainFocus = rows[0].gameObject;
        BuildControls(dim.transform);
        Select(mainFocus);
    }

    // ---- Controls page: click an action, then press the new key. ----

    private RectTransform mainPanel, controlsPanel;
    private GameObject mainFocus;
    private Button[] keyButtons, padButtons;
    private TextMeshProUGUI[] padFixed;
    private int listening = -1, busyFrame = -1, listenFrame = -1;
    private bool listeningPad;

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
        const float rowH = 48f;
        controlsPanel = CatTheme.Panel(parent, "Controls", CatTheme.Style.Panel, Vector2.zero, Vector2.zero).rectTransform;
        float height = 230f + n * rowH;
        controlsPanel.sizeDelta = new Vector2(780, height);
        float top = height / 2f;

        RuntimeUI.Title(controlsPanel, "CONTROLS", 48f, new Vector2(0, top - 40), 700f);
        Color head = new Color(0.6f, 0.64f, 0.75f);
        RuntimeUI.Text(controlsPanel, "KEYBOARD", 16, head, new Vector2(60, top - 78), new Vector2(200, 24));
        RuntimeUI.Text(controlsPanel, "CONTROLLER", 16, head, new Vector2(270, top - 78), new Vector2(200, 24));
        keyButtons = new Button[n];
        padButtons = new Button[n];
        padFixed = new TextMeshProUGUI[n];
        for (int i = 0; i < n; i++)
        {
            int action = i;
            float y = top - 110f - i * rowH;
            RuntimeUI.Outline(RuntimeUI.Text(controlsPanel, Keybinds.Names[i], 22, new Color(0.8f, 0.84f, 0.95f), new Vector2(-230, y), new Vector2(260, rowH), TextAlignmentOptions.MidlineLeft));
            keyButtons[i] = RuntimeUI.Button(controlsPanel, "", new Vector2(60, y), new Vector2(200, 44), () => Listen(action, false));
            if (Keybinds.PadRebindable((Keybinds.Action)i))
                padButtons[i] = RuntimeUI.Button(controlsPanel, "", new Vector2(270, y), new Vector2(200, 44), () => Listen(action, true));
            else // movement and pause are fixed on a pad, shown as plain text so navigation skips them
                padFixed[i] = RuntimeUI.Outline(RuntimeUI.Text(controlsPanel, "", 22, head, new Vector2(270, y), new Vector2(200, 44)));
        }
        RuntimeUI.Text(controlsPanel, $"Arrow keys / stick always move.  Esc / {Keybinds.PadLabel(GamepadButton.Start)} always pause.", 16, head, new Vector2(0, -top + 88), new Vector2(740, 24));
        CatTheme.Button(RuntimeUI.Button(controlsPanel, "Reset", new Vector2(-110, -top + 38), new Vector2(180, 44), () => { Keybinds.ResetAll(); RefreshKeys(); }));
        CatTheme.Button(RuntimeUI.Button(controlsPanel, "Back", new Vector2(110, -top + 38), new Vector2(180, 44), ShowMainPage));
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

    private void Listen(int action, bool forPad)
    {
        if (listening >= 0) return;
        listening = action;
        listeningPad = forPad;
        listenFrame = Time.frameCount; // the press that opened this must not count as the new binding
        SetLabel(forPad ? padButtons[action] : keyButtons[action],
                 forPad ? "<color=#FFE08A>Press a button...</color>" : "<color=#FFE08A>Press a key...</color>");
        Select(null); // so the key we're waiting for can't also "click" a button
    }

    private void Update()
    {
        if (listening < 0 || Time.frameCount == listenFrame) return;
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame)) { StopListening(); return; }

        if (listeningPad)
        {
            if (pad == null) return;
            foreach (GamepadButton b in Keybinds.PadChoices)
            {
                if (!pad[b].wasPressedThisFrame) continue;
                Keybinds.SetPad((Keybinds.Action)listening, b);
                StopListening();
                return;
            }
            return;
        }

        if (kb == null) return;
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
        StartCoroutine(SelectNextFrame((listeningPad ? padButtons[action] : keyButtons[action]).gameObject));
    }

    private IEnumerator SelectNextFrame(GameObject go)
    {
        yield return null;
        Select(go);
    }

    private void RefreshKeys()
    {
        for (int i = 0; i < keyButtons.Length; i++)
        {
            var a = (Keybinds.Action)i;
            SetLabel(keyButtons[i], Keybinds.Label(a));
            if (padButtons[i] != null) SetLabel(padButtons[i], Keybinds.PadLabel(Keybinds.GetPad(a)).ToUpperInvariant());
            else padFixed[i].text = a == Keybinds.Action.Pause ? Keybinds.PadLabel(GamepadButton.Start).ToUpperInvariant() : "STICK";
        }
    }

    private static void SetLabel(Button b, string text)
    {
        TextMeshProUGUI t = b.GetComponentInChildren<TextMeshProUGUI>();
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
