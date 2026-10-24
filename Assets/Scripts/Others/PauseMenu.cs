using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    public GameObject panel;
    public Button resumeButton;
    public Button restartButton;
    public Button quitButton;
    public TextMeshProUGUI statsText;

    private PlayerStats stats;
    private LevelUpUIController levelUp;
    private float timeScaleBefore = 1f;
    private RectTransform chart;
    private Button[] column;
    private TextMeshProUGUI hintText;

    public bool IsPaused { get; private set; }

    private void Start()
    {
        stats = FindFirstObjectByType<PlayerStats>();
        if (stats != null) levelUp = stats.GetComponent<LevelUpUIController>();

        resumeButton.onClick.AddListener(Resume);
        restartButton.onClick.AddListener(Restart);
        quitButton.onClick.AddListener(MainMenu.BackToMenu);
        quitButton.GetComponentInChildren<TextMeshProUGUI>().text = "Main Menu";
        AddCombosButton();
        RuntimeUI.Restyle(panel.transform);
        foreach (TextMeshProUGUI t in panel.GetComponentsInChildren<TextMeshProUGUI>(true))
            if (t.text.Trim() == "PAUSED") { RuntimeUI.ReplaceTitle(t, "PAUSED", t.fontSize); break; }
        // Taller stats box (grows downward, away from the title) so the build text isn't squeezed to its minimum size.
        var box = (RectTransform)statsText.rectTransform.parent;
        box.anchoredPosition += Vector2.down * 110f;
        box.sizeDelta = new Vector2(box.sizeDelta.x + 40f, box.sizeDelta.y + 220f);
        panel.SetActive(false);
    }

    private const float StatsSplit = 0.42f; // build stats above, damage chart below

    private void Update()
    {
        bool toggle = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) || Keybinds.Pressed(Keybinds.Action.Pause)
                      || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
        if (!toggle && GamepadUI.Back && (IsPaused || Settings.IsOpen || ComboBook.IsOpen)) toggle = true;
        if (!toggle) return;

        if (Settings.IsOpen) { if (!Settings.Busy) Settings.Back(); return; }
        if (ComboBook.IsOpen) { ComboBook.Close(); return; }
        if (IsPaused) Resume();
        else if (CanPause()) Pause();
    }

    // Alt-tab / clicking away opens the pause menu, so returning players aren't hit before they're ready.
    private void OnApplicationFocus(bool focused)
    {
        if (!focused && !IsPaused && !Settings.IsOpen && !ComboBook.IsOpen && CanPause()) Pause();
    }

    private bool CanPause() => !MainMenu.IsOpen &&
        stats != null && stats.currentHealth > 0f && (levelUp == null || !levelUp.IsOpen) && !CombineMenu.IsOpen && !RewardMenu.IsOpen && !GameOverScreen.IsShowing;

    public void Pause()
    {
        IsPaused = true;
        Sfx.Play(Sfx.Id.Click, 0.6f, 0.8f);
        Juice.CancelHitStop();
        timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        // Build stats on top, damage chart below.
        statsText.text = BuildStats(stats);
        statsText.rectTransform.anchorMin = new Vector2(0f, StatsSplit);
        if (chart != null) Destroy(chart.gameObject);
        HUD hud = FindFirstObjectByType<HUD>();
        chart = GameOverScreen.DamageChart(statsText.rectTransform.parent, Vector2.zero, Vector2.zero, hud != null ? hud.RunTime : 0f);
        chart.anchorMin = Vector2.zero;
        chart.anchorMax = new Vector2(1f, StatsSplit);
        chart.offsetMin = new Vector2(36f, 24f);
        chart.offsetMax = new Vector2(-36f, -8f);
        // Left from the buttons jumps to the HUD gear slots (hover/select shows details).
        foreach (Button b in column) { Navigation n = b.navigation; n.selectOnLeft = hud != null ? hud.FirstGearSlot : null; b.navigation = n; }
        if (hintText != null) hintText.text = GamepadUI.Connected ? $"{Keybinds.PadLabel(UnityEngine.InputSystem.LowLevel.GamepadButton.Start)} / {Keybinds.PadLabel(UnityEngine.InputSystem.LowLevel.GamepadButton.East)} to resume" : $"Esc / {Keybinds.Label(Keybinds.Action.Pause)} to resume";
        panel.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
    }

    private void AddCombosButton()
    {
        Button combos = CopyOfQuit("Combos");
        combos.onClick.AddListener(() => ComboBook.Open(combos.gameObject));
        Button settings = CopyOfQuit("Settings");
        settings.onClick.AddListener(() => Settings.Open(settings.gameObject));

        const float top = 200f, step = 100f;
        column = new[] { resumeButton, restartButton, combos, settings, quitButton };
        float x = ((RectTransform)quitButton.transform).anchoredPosition.x;
        for (int i = 0; i < column.Length; i++)
        {
            var rt = (RectTransform)column[i].transform;
            rt.anchoredPosition = new Vector2(x, top - i * step);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 82f);
        }
        Transform hint = quitButton.transform.parent.Find("Hint");
        if (hint != null) ((RectTransform)hint).anchoredPosition = new Vector2(x, top - column.Length * step + 30f);
        if (hint != null) hintText = hint.GetComponentInChildren<TextMeshProUGUI>();

        for (int i = 0; i < column.Length; i++)
        {
            Navigation nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnUp = column[(i + column.Length - 1) % column.Length];
            nav.selectOnDown = column[(i + 1) % column.Length];
            column[i].navigation = nav;
        }
    }

    private Button CopyOfQuit(string label)
    {
        Button b = Instantiate(quitButton, quitButton.transform.parent);
        b.name = label + "Button";
        b.onClick.RemoveAllListeners();
        TextMeshProUGUI text = b.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null) text.text = label;
        return b;
    }

    public void Resume()
    {
        Sfx.Play(Sfx.Id.Click, 0.6f);
        Settings.Close();
        ComboBook.Close();
        IsPaused = false;
        panel.SetActive(false);
        Time.timeScale = timeScaleBefore;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void Restart()
    {
        Time.timeScale = 1f;
        SceneFade.Reload();
    }

    public static string BuildStats(PlayerStats stats)
    {
        string Row(string label, string value) => $"<color=#9AA3C0>{label}</color><pos=66%><color=#FFFFFF>{value}</color>\n";

        return "<size=120%><b><color=#FFE08A>Your Build</color></b></size>\n\n" +
               Row("Level", stats.level.ToString()) +
               Row("HP", $"{Mathf.CeilToInt(stats.currentHealth)}/{Mathf.CeilToInt(stats.maxHealth)}") +
               Row(Hero.Current.stats[0], stats.AttackDamage.ToString("0.#")) +
               Row(Hero.Current.stats[1], stats.AttackSpeed.ToString("0.##")) +
               Row(Hero.Current.stats[2], (NyxDrones.Active != null ? NyxDrones.Active.Count : stats.ProjectileCount).ToString()) +
               Row(Hero.Current.stats[3], Hero.Range(stats).ToString("0.##")) +
               Row("Crit", $"{stats.critChance:P0}  x{stats.critDamage:0.##}") +
               Row("Move speed", stats.MoveSpeed.ToString("0.#")) +
               Row("Pickup range", stats.pickupRange.ToString("0.#")) +
               Row("Dmg reduction", stats.damageReduction.ToString("P0")) +
               Row("Dodge", stats.dodgeChance.ToString("P0")) +
               Row("Cat Buddy", Companion.Instance != null ? $"Lv {Companion.Instance.Level}" : "-");
    }
}
