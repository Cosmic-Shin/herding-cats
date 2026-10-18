using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public static bool Started;
    public static string PlayerName = "";
    public static int LastRank;
    public static bool IsOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Started = false; IsOpen = false; LastRank = 0; }

    public static void BackToMenu()
    {
        Started = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private const int RowsPerPage = 10, Pages = Leaderboard.Size / RowsPerPage, BuildIcons = PlayerWeapons.MaxWeapons + PlayerSkills.MaxSkills;
    private static readonly Color Gold = new Color(1f, 0.85f, 0.3f), Faint = new Color(0.6f, 0.64f, 0.75f);

    private GameObject root, mainPage, boardPage, achPage;
    private TextMeshProUGUI achText, backLabel;
    private RectTransform boardTitle;
    private System.Action boardBack;
    private TMP_InputField nameField;
    private Button startButton;
    private TextMeshProUGUI nameHint, pageText;
    private Button prevButton, nextButton;
    private int page;
    private List<Leaderboard.Entry> entries;

    private class Row
    {
        public Image bg, medal, shine, glow;
        public TextMeshProUGUI rank, name, score, time, level;
        public HUD.Slot[] build;
    }
    private readonly Row[] rows = new Row[RowsPerPage];

    private RectTransform title;

    private void Awake()
    {
        if (Started) return;
        IsOpen = true;
        Time.timeScale = 0f;
        Build();
    }

    private void Update()
    {
        if (root == null) return;
        Animate();
        if (!IsOpen) return;
        Time.timeScale = 0f;
        if (!Started) HideGameUi();

        if (mainPage != null && mainPage.activeSelf && !Settings.IsOpen && nameField.isFocused &&
            (GamepadUI.Move().sqrMagnitude > 0.25f || GamepadUI.Pressed(g => g.buttonSouth)))
        {
            if (TypedName == "") nameField.text = "Player";
            nameField.DeactivateInputField();
            Select(startButton.gameObject);
            return;
        }

        Keyboard kb = Keyboard.current;
        bool back = (kb != null && kb.escapeKey.wasPressedThisFrame) || GamepadUI.Back;
        if (Settings.IsOpen) { if (back) Settings.Close(); return; }
        if (achPage != null && achPage.activeSelf) { if (back) ShowMain(); return; }
        if (!boardPage.activeSelf) return;
        if (back) boardBack();
        else if ((kb != null && kb.leftArrowKey.wasPressedThisFrame) || GamepadUI.Pressed(g => g.leftShoulder)) SetPage(page - 1);
        else if ((kb != null && kb.rightArrowKey.wasPressedThisFrame) || GamepadUI.Pressed(g => g.rightShoulder)) SetPage(page + 1);
    }

    public void ShowRanking(int rank, System.Action onContinue)
    {
        IsOpen = true;
        Canvas canvas = RuntimeUI.Canvas("RankingCanvas", 300);
        root = canvas.gameObject;
        RuntimeUI.Box(canvas.transform, "Dim", Vector2.zero, Vector2.one, new Color(0.03f, 0.02f, 0.08f, 0.85f));
        BuildBoard(canvas.transform);
        SetTitle(boardTitle, rank == 1 ? "NEW HIGH SCORE!  <color=#7CFF9A>RANK #1</color>" : $"YOU MADE THE BOARD!  <color=#7CFF9A>RANK #{rank}</color>");
        backLabel.text = "CONTINUE";
        boardBack = () =>
        {
            Destroy(root);
            root = null;
            IsOpen = false;
            onContinue();
        };
        ShowBoard();
        Select(backLabel.gameObject);
    }

    private void Build()
    {
        Canvas canvas = RuntimeUI.Canvas("MainMenuCanvas", 500);
        root = canvas.gameObject;
        RuntimeUI.Box(canvas.transform, "Dim", Vector2.zero, Vector2.one, new Color(0.03f, 0.02f, 0.08f, 0.22f)).raycastTarget = false;
        BuildMain(canvas.transform);
        BuildBoard(canvas.transform);
        BuildAchievements(canvas.transform);
        ShowMain();
    }

    private void BuildMain(Transform parent)
    {
        mainPage = Page(parent, "Main");
        title = BuildTitle(mainPage.transform, "HERDING CATS", 128f, TitleY);
        RuntimeUI.Outline(RuntimeUI.Text(mainPage.transform, "Music by Kevin MacLeod (incompetech.com)  -  CC BY 4.0", 16, Faint, new Vector2(0, -350), new Vector2(900, 24)));

        Transform list = mainPage.transform;
        nameLabel = RuntimeUI.Outline(RuntimeUI.Text(list, "NAME:", 30, ItemColor, new Vector2(-110, -5), new Vector2(160, 44), TextAlignmentOptions.MidlineRight));
        nameField = NameField(list, new Vector2(100, -5), new Vector2(240, 44));
        nameHint = RuntimeUI.Outline(RuntimeUI.Text(list, "", 16, new Color(1f, 0.45f, 0.4f), new Vector2(0, -35), new Vector2(480, 22)));

        startButton = TextButton(list, "Start", -80, TryStart);
        TextButton(list, "Leaderboard", -132, ShowBoard);
        TextButton(list, "Achievements", -184, ShowAchievements);
        Button settings = null;
        settings = TextButton(list, "Settings", -236, () => Settings.Open(settings.gameObject));
        TextButton(list, "Quit", -288, Quit);

        nameField.text = PlayerName != "" ? PlayerName : PlayerPrefs.GetString("playerName", "");
        Validate();
    }

    private static Color ItemColor => RuntimeUI.ItemColor;

    private const float TitleY = 190f;

    private static RectTransform BuildTitle(Transform parent, string text, float size, float y) => RuntimeUI.Title(parent, text, size, new Vector2(0f, y));
    private static void SetTitle(RectTransform holder, string text) => RuntimeUI.SetTitle(holder, text);

    private static Image Backdrop(Transform parent, Vector2 pos, Vector2 size, float alpha = 0.45f)
    {
        Image img = RuntimeUI.Box(parent, "Backdrop", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.03f, 0.02f, 0.08f, alpha));
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.sizeDelta = size;
        img.raycastTarget = false;
        return img;
    }

    private static RectTransform Container(Transform parent, Vector2 size)
    {
        var rt = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = size;
        return rt;
    }
    private TextMeshProUGUI nameLabel;

    private static Button TextButton(Transform parent, string label, float y, UnityEngine.Events.UnityAction onClick, float x = 0f, float size = 32f) =>
        RuntimeUI.TextButton(parent, label, new Vector2(x, y), size, onClick);

    private TMP_InputField NameField(Transform parent, Vector2 pos, Vector2 size)
    {
        GameObject go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.GetComponent<Image>().color = Color.clear;
        go.AddComponent<CardHover>();

        var field = go.GetComponent<TMP_InputField>();
        field.transition = Selectable.Transition.None;
        field.characterLimit = 16;
        field.textComponent.fontSize = 30;
        field.textComponent.color = Color.white;
        field.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
        RuntimeUI.Outline((TextMeshProUGUI)field.textComponent);
        var placeholder = (TextMeshProUGUI)field.placeholder;
        placeholder.text = "your name";
        placeholder.fontSize = 30;
        placeholder.color = new Color(1f, 1f, 1f, 0.3f);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        field.caretColor = Color.white;
        field.onValueChanged.AddListener(_ => Validate());
        field.onSubmit.AddListener(_ => TryStart());
        return field;
    }

    private string TypedName => nameField.text.Trim();

    private void Validate()
    {
        bool ok = TypedName.Length > 0;
        SetEnabled(startButton, ok);
        nameHint.text = ok ? "" : "A name is required to start";
    }

    private void TryStart()
    {
        if (TypedName.Length == 0) { Validate(); return; }
        PlayerName = TypedName;
        PlayerPrefs.SetString("playerName", PlayerName);
        PlayerPrefs.Save();
        Started = true;
        IsOpen = false;
        Time.timeScale = 1f;
        foreach (Canvas c in hiddenUi) if (c != null) c.enabled = true;
        hiddenUi.Clear();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        StartCoroutine(FadeOut());
    }

    private System.Collections.IEnumerator FadeOut()
    {
        const float duration = 0.6f;
        var group = root.AddComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = false;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = t / duration;
            group.alpha = 1f - k * k;
            mainPage.transform.localScale = Vector3.one * (1f + 0.08f * k);
            yield return null;
        }
        Destroy(root);
    }

    private readonly List<Canvas> hiddenUi = new List<Canvas>();

    private void HideGameUi()
    {
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas || !c.enabled || c.gameObject == root || c.GetComponentInParent<Settings>() != null) continue;
            c.enabled = false;
            hiddenUi.Add(c);
        }
    }

    private void Animate()
    {
        float t = Time.unscaledTime;

        if (title != null)
        {
            title.anchoredPosition = new Vector2(0f, TitleY + Mathf.Abs(Mathf.Sin(t * 2.2f)) * 14f);
            title.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(t * 4.4f));
            title.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.1f) * 2f);
        }

        if (nameField != null)
        {
            bool on = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == nameField.gameObject;
            nameLabel.color = on ? Gold : ItemColor;
            nameField.textComponent.color = on ? Gold : Color.white;
        }

        if (boardPage == null || !boardPage.activeSelf) return;
        foreach (Row row in rows)
        {
            if (row.shine.gameObject.activeSelf)
                row.shine.rectTransform.anchoredPosition = new Vector2(-700f + (t * 600f) % 1600f, 0f);
            if (row.medal.gameObject.activeSelf) row.medal.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(t * 3f));
            if (row.glow.gameObject.activeSelf)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4f);
                row.glow.color = new Color(1f, 0.85f, 0.3f, 0.25f + 0.3f * pulse);
                row.glow.rectTransform.sizeDelta = new Vector2(78f, 50f) * (1f + 0.1f * pulse);
            }
        }
    }

    private void ShowMain()
    {
        mainPage.SetActive(true);
        boardPage.SetActive(false);
        achPage.SetActive(false);
        Select(nameField.gameObject);
        nameField.ActivateInputField();
    }

    private void BuildBoard(Transform parent)
    {
        boardPage = Page(parent, "Leaderboard");
        RectTransform panel = Container(boardPage.transform, new Vector2(1200, 680));
        boardTitle = BuildTitle(panel, "LEADERBOARD", 60f, 292f);
        Backdrop(panel, new Vector2(0, 5), new Vector2(1170, 530), 0.78f);
        boardBack = ShowMain;
        const float headerY = 245f;
        Column(panel, "RANK", -555, 80, headerY, Faint, 20);
        Column(panel, "NAME", -470, 230, headerY, Faint, 20);
        Column(panel, "SCORE", -230, 150, headerY, Faint, 20);
        Column(panel, "TIME", -70, 100, headerY, Faint, 20);
        Column(panel, "LV", 35, 60, headerY, Faint, 20);
        Column(panel, "BUILD", 100, 460, headerY, Faint, 20);

        for (int i = 0; i < RowsPerPage; i++)
        {
            float y = 200f - i * 46f;
            var row = new Row();
            row.bg = RuntimeUI.Box(panel, "Row", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.clear);
            row.bg.rectTransform.anchoredPosition = new Vector2(0, y);
            row.bg.rectTransform.sizeDelta = new Vector2(1150, 42);
            row.bg.raycastTarget = false;
            row.bg.gameObject.AddComponent<RectMask2D>();
            row.shine = RuntimeUI.Box(row.bg.transform, "Shine", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(1f, 1f, 1f, 0.22f));
            row.shine.rectTransform.sizeDelta = new Vector2(60, 90);
            row.shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 25f);
            row.glow = RuntimeUI.Box(panel, "Glow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Gold);
            row.glow.sprite = Shapes.Circle;
            row.glow.raycastTarget = false;
            row.glow.rectTransform.anchoredPosition = new Vector2(-530, y);
            row.medal = RuntimeUI.Box(panel, "Medal", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Gold);
            row.medal.sprite = Shapes.Circle;
            row.medal.rectTransform.anchoredPosition = new Vector2(-530, y);
            row.medal.rectTransform.sizeDelta = new Vector2(38, 38);
            var rim = row.medal.gameObject.AddComponent<Outline>();
            rim.effectColor = new Color(0.04f, 0.03f, 0.1f, 0.9f);
            rim.effectDistance = new Vector2(2, -2);
            row.rank = RuntimeUI.Outline(RuntimeUI.Text(panel, "", 24, Color.white, new Vector2(-530, y), new Vector2(70, 40)));
            row.name = Column(panel, "", -470, 230, y, Color.white, 24);
            row.score = Column(panel, "", -230, 150, y, Gold, 24);
            row.time = Column(panel, "", -70, 100, y, Color.white, 24);
            row.level = Column(panel, "", 35, 60, y, Color.white, 24);
            row.build = new HUD.Slot[BuildIcons];
            for (int k = 0; k < BuildIcons; k++)
                row.build[k] = HUD.MakeSlot(row.bg.rectTransform, new Vector2(675 + k * 35f, -5), 32);
            rows[i] = row;
        }

        prevButton = TextButton(panel, "< Prev", -292, () => SetPage(page - 1), -210, 28);
        pageText = RuntimeUI.Outline(RuntimeUI.Text(panel, "", 24, ItemColor, new Vector2(0, -292), new Vector2(240, 44)));
        nextButton = TextButton(panel, "Next >", -292, () => SetPage(page + 1), 210, 28);
        backLabel = TextButton(panel, "Back", -292, () => boardBack(), -480, 28).GetComponent<TextMeshProUGUI>();
        RuntimeUI.Outline(RuntimeUI.Text(panel, "<color=#FF7AD9>pink time</color> = endless\n<- / -> or LB / RB pages   Esc / B back", 16, Faint, new Vector2(470, -292), new Vector2(240, 44)));
    }

    private static TextMeshProUGUI Column(Transform parent, string text, float left, float width, float y, Color color, float size)
    {
        TextMeshProUGUI t = RuntimeUI.Outline(RuntimeUI.Text(parent, text, size, color, new Vector2(left + width / 2f, y), new Vector2(width, 40),
                                                             TextAlignmentOptions.MidlineLeft));
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        return t;
    }

    private void ShowBoard()
    {
        entries = Leaderboard.Load();
        if (mainPage != null) mainPage.SetActive(false);
        boardPage.SetActive(true);
        SetPage(LastRank > 0 ? (LastRank - 1) / RowsPerPage : 0);
        Select(nextButton.gameObject);
    }

    private void SetPage(int p)
    {
        page = Mathf.Clamp(p, 0, Pages - 1);
        pageText.text = $"Page {page + 1} / {Pages}";
        SetEnabled(prevButton, page > 0);
        SetEnabled(nextButton, page < Pages - 1);

        for (int i = 0; i < RowsPerPage; i++)
        {
            int rank = page * RowsPerPage + i + 1;
            Row row = rows[i];
            Leaderboard.Entry e = rank <= entries.Count ? entries[rank - 1] : null;
            Color rankColor = rank == 1 ? Gold : rank == 2 ? new Color(0.85f, 0.88f, 0.95f) : rank == 3 ? new Color(0.85f, 0.55f, 0.3f) : Color.white;
            bool medal = rank <= 3 && e != null;
            row.medal.gameObject.SetActive(medal);
            row.medal.color = rankColor;
            row.rank.text = medal ? rank.ToString() : $"#{rank}";
            bool mine = Leaderboard.IsMine(e);
            row.glow.gameObject.SetActive(mine);
            if (mine) row.rank.color = medal ? Color.white : Gold;
            else row.rank.color = Color.white;
            row.bg.color = rank == LastRank ? new Color(1f, 0.85f, 0.3f, 0.18f) : i % 2 == 0 ? new Color(1f, 1f, 1f, 0.06f) : Color.clear;
            row.shine.gameObject.SetActive(rank == LastRank && e != null);

            row.name.text = e != null ? e.name : "<color=#5A6280>---</color>";
            row.score.text = e != null ? e.score.ToString("N0") : "";
            row.time.text = e != null ? $"{(e.endless ? "<color=#FF7AD9>" : "")}{e.seconds / 60:00}:{e.seconds % 60:00}" : "";
            row.level.text = e != null ? e.level.ToString() : "";
            for (int k = 0; k < BuildIcons; k++)
            {
                HUD.Slot slot = row.build[k];
                slot.frame.gameObject.SetActive(e != null);
                if (e == null) continue;
                if (k < e.build.Count) HUD.Fill(slot, e.build[k].icon, e.build[k].level == "MAX" ? "<color=#FFD24A>M</color>" : e.build[k].level, gold: e.build[k].level == "");
                else HUD.Clear(slot);
            }
        }
    }

    private Button achBack;

    private void BuildAchievements(Transform parent)
    {
        achPage = Page(parent, "Achievements");
        RectTransform panel = Container(achPage.transform, new Vector2(1200, 680));
        BuildTitle(panel, "ACHIEVEMENTS", 60f, 292f);
        Backdrop(panel, new Vector2(0, 0), new Vector2(1180, 540), 0.78f);
        achText = RuntimeUI.Outline(RuntimeUI.Text(panel, "", 20, Color.white, new Vector2(0, 0), new Vector2(1130, 510), TextAlignmentOptions.TopLeft));
        achText.textWrappingMode = TextWrappingModes.NoWrap;
        achText.enableAutoSizing = true;
        achText.fontSizeMin = 14;
        achText.fontSizeMax = 24;
        achBack = TextButton(panel, "Back", -292, ShowMain, 0, 28);
    }

    private static string RewardShort(string reward)
    {
        if (reward == "reroll") return "<color=#9FD8FF>+1 Reroll</color>";
        if (reward == "eliminate") return "<color=#9FD8FF>+1 Eliminate</color>";
        string id = reward.Substring(reward.IndexOf(':') + 1);
        return reward.StartsWith("weapon:") ? $"<color=#FFB36B>{PlayerWeapons.Find(id).name}</color>" : $"<color=#D7A6FF>{PlayerSkills.Find(id).name}</color>";
    }

    private void ShowAchievements()
    {
        var sb = new System.Text.StringBuilder();
        int done = 0;
        foreach (Achievements.Def d in Achievements.All)
        {
            bool on = Achievements.IsUnlocked(d);
            if (on) done++;
            string mark = on ? "<color=#7CFF9A>[X]</color>" : "<color=#5A6280>[ ]</color>";
            string name = on ? $"<color=#FFE08A>{d.name}</color>" : d.name;
            string progress = !on && d.progress != null ? $"  <color=#FFB36B>({d.progress()})</color>" : "";
            sb.Append($"{mark}<pos=4%><b>{name}</b><pos=27%><color=#C9D2F0>{d.goal}</color>{progress}<pos=74%>{(on ? "" : "<alpha=#88>")}{RewardShort(d.reward)}<alpha=#FF>\n");
        }
        sb.Append($"\n<color=#9AA3C0>{done} / {Achievements.All.Length} done.  <color=#FFB36B>Weapons</color> and <color=#D7A6FF>skills</color> join the level-up pool once earned; <color=#9FD8FF>bonuses</color> apply every run.</color>");
        achText.text = sb.ToString();
        mainPage.SetActive(false);
        achPage.SetActive(true);
        Select(achBack.gameObject);
    }

    private static GameObject Page(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }

    private static void SetEnabled(Button button, bool on)
    {
        button.interactable = on;
        CanvasGroup group = button.GetComponent<CanvasGroup>();
        if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
        group.alpha = on ? 1f : 0.4f;
    }

    private static void Select(GameObject go)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(go);
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
