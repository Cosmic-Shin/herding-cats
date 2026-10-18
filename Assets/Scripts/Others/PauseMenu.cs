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
        panel.SetActive(false);
    }

    private void Update()
    {
        bool toggle = (Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame))
                      || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
        if (!toggle && GamepadUI.Back && (IsPaused || Settings.IsOpen || ComboBook.IsOpen)) toggle = true;
        if (!toggle) return;

        if (Settings.IsOpen) { Settings.Close(); return; }
        if (ComboBook.IsOpen) { ComboBook.Close(); return; }
        if (IsPaused) Resume();
        else if (CanPause()) Pause();
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
        statsText.text = BuildStats(stats) + DamageStats.Report();
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
        Button[] column = { resumeButton, restartButton, combos, settings, quitButton };
        float x = ((RectTransform)quitButton.transform).anchoredPosition.x;
        for (int i = 0; i < column.Length; i++)
        {
            var rt = (RectTransform)column[i].transform;
            rt.anchoredPosition = new Vector2(x, top - i * step);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 82f);
        }
        Transform hint = quitButton.transform.parent.Find("Hint");
        if (hint != null) ((RectTransform)hint).anchoredPosition = new Vector2(x, top - column.Length * step + 30f);

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
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public static string BuildStats(PlayerStats stats)
    {
        string Row(string label, string value) => $"<color=#9AA3C0>{label}</color><pos=66%><color=#FFFFFF>{value}</color>\n";

        return "<size=120%><b><color=#FFE08A>Your Build</color></b></size>\n\n" +
               Row("Level", stats.level.ToString()) +
               Row("HP", $"{Mathf.CeilToInt(stats.currentHealth)}/{Mathf.CeilToInt(stats.maxHealth)}") +
               Row("Bomb damage", stats.AttackDamage.ToString("0.#")) +
               Row("Throws/sec", stats.AttackSpeed.ToString("0.##")) +
               Row("Bombs/throw", stats.ProjectileCount.ToString()) +
               Row("Blast radius", stats.BombRadius.ToString("0.##")) +
               Row("Crit", $"{stats.critChance:P0}  x{stats.critDamage:0.##}") +
               Row("Move speed", stats.MoveSpeed.ToString("0.#")) +
               Row("Pickup range", stats.pickupRange.ToString("0.#")) +
               Row("Dmg reduction", stats.damageReduction.ToString("P0")) +
               Row("Dodge", stats.dodgeChance.ToString("P0")) +
               Row("Cat Buddy", Companion.Instance != null ? $"Lv {Companion.Instance.Level}" : "-");
    }
}
