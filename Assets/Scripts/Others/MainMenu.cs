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

    private GameObject root, mainPage, boardPage, achPage, creditsPage;
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
        public Image bg, medal, shine, glow, hero, mapDot;
        public TextMeshProUGUI rank, name, score, time, level;
        public HUD.Slot[] build;
    }
    private readonly Row[] rows = new Row[RowsPerPage];

    private RectTransform title;

    private void OnDestroy() => Leaderboard.Changed -= OnBoardChanged;

    // Online scores can land while the board is open; redraw in place.
    private void OnBoardChanged()
    {
        if (boardPage == null || !boardPage.activeSelf) return;
        entries = Leaderboard.Load(HeroFilter, MapFilter);
        SetPage(page);
    }

    private void Awake()
    {
        Leaderboard.Changed += OnBoardChanged;
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
        if (preview != null) preview.gameObject.SetActive(mainPage != null && mainPage.activeSelf && !Settings.IsOpen);

        if (mainPage != null && mainPage.activeSelf && !Settings.IsOpen && nameField.isFocused &&
            (GamepadUI.Move().sqrMagnitude > 0.25f || GamepadUI.Pressed(g => g.buttonSouth)))
        {
            if (TypedName == "") nameField.text = "Cat" + Random.Range(1000, 10000); // can't type on a pad; unique-ish so pad players don't share one leaderboard row
            nameField.DeactivateInputField();
            Select(startButton.gameObject);
            return;
        }

        Keyboard kb = Keyboard.current;
        bool back = (kb != null && kb.escapeKey.wasPressedThisFrame) || GamepadUI.Back;
        if (Settings.IsOpen) { if (back && !Settings.Busy) Settings.Back(); return; }
        if ((achPage != null && achPage.activeSelf) || (creditsPage != null && creditsPage.activeSelf)) { if (back) ShowMain(); return; }
        if (!boardPage.activeSelf)
        {
            // Left/right on the hero or map row flips through the options (click/A goes forward).
            GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            int step = (kb != null && kb.leftArrowKey.wasPressedThisFrame) || GamepadUI.Pressed(g => g.dpad.left) ? -1
                     : (kb != null && kb.rightArrowKey.wasPressedThisFrame) || GamepadUI.Pressed(g => g.dpad.right) ? 1 : 0;
            if (step != 0 && heroButton != null && sel == heroButton.gameObject) CycleHero(step);
            else if (step != 0 && mapButton != null && sel == mapButton.gameObject) CycleMap(step);
            return;
        }
        if (back) { boardBack(); return; }
        // Left/right on a filter button changes the filter; otherwise it turns the page.
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        int dir = (kb != null && kb.leftArrowKey.wasPressedThisFrame) || GamepadUI.Pressed(g => g.dpad.left) ? -1
                : (kb != null && kb.rightArrowKey.wasPressedThisFrame) || GamepadUI.Pressed(g => g.dpad.right) ? 1 : 0;
        if (dir != 0 && heroFilterButton != null && selected == heroFilterButton.gameObject) CycleFilter(ref heroFilter, Hero.All.Length, dir);
        else if (dir != 0 && mapFilterButton != null && selected == mapFilterButton.gameObject) CycleFilter(ref mapFilter, MapGenerator.Names.Length, dir);
        else if ((kb != null && kb.leftArrowKey.wasPressedThisFrame) || GamepadUI.Pressed(g => g.leftShoulder)) SetPage(page - 1);
        else if ((kb != null && kb.rightArrowKey.wasPressedThisFrame) || GamepadUI.Pressed(g => g.rightShoulder)) SetPage(page + 1);
    }

    // Leaderboard filters: -1 = all, otherwise an index into Hero.All / MapGenerator.Names.
    private int heroFilter = -1, mapFilter = -1;
    private Button heroFilterButton, mapFilterButton;
    private string HeroFilter => heroFilter < 0 ? null : Hero.All[heroFilter].id;
    private string MapFilter => mapFilter < 0 ? null : MapGenerator.Names[mapFilter];
    private bool Filtered => heroFilter >= 0 || mapFilter >= 0;

    private void CycleFilter(ref int value, int count, int step)
    {
        value = (value + 1 + step + count + 1) % (count + 1) - 1;
        Sfx.Play(Sfx.Id.Click, 0.5f);
        RefreshFilterLabels();
        entries = Leaderboard.Load(HeroFilter, MapFilter);
        SetPage(0);
    }

    private void RefreshFilterLabels()
    {
        if (heroFilterButton == null) return;
        heroFilterButton.GetComponent<TextMeshProUGUI>().text = $"<  HERO: {(HeroFilter ?? "All").ToUpper()}  >";
        mapFilterButton.GetComponent<TextMeshProUGUI>().text = $"<  MAP: {(MapFilter ?? "All").ToUpper()}  >";
    }

    private static Color MapColor(string map) =>
        map == "Winter" ? new Color(0.7f, 0.9f, 1f) : map == "Volcano" ? new Color(1f, 0.45f, 0.15f) : new Color(0.45f, 0.85f, 0.35f);

    public void ShowRanking(int rank, System.Action onContinue)
    {
        IsOpen = true;
        Canvas canvas = RuntimeUI.Canvas("RankingCanvas", 300);
        root = canvas.gameObject;
        RuntimeUI.Box(canvas.transform, "Dim", Vector2.zero, Vector2.one, new Color(0.03f, 0.02f, 0.08f, 0.85f));
        BuildBoard(canvas.transform);
        heroFilter = mapFilter = -1; // the rank is on the full board
        heroFilterButton.gameObject.SetActive(false);
        mapFilterButton.gameObject.SetActive(false);
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
        RuntimeUI.Box(canvas.transform, "Dim", Vector2.zero, Vector2.one, new Color(0.03f, 0.02f, 0.08f, MapGenerator.Winter ? 0.42f : 0.22f)).raycastTarget = false;
        BuildMain(canvas.transform);
        BuildBoard(canvas.transform);
        BuildAchievements(canvas.transform);
        BuildCredits(canvas.transform);
        ShowMain();
    }

    private void BuildMain(Transform parent)
    {
        mainPage = Page(parent, "Main");
        title = BuildTitle(mainPage.transform, "HERDING CATS", 128f, TitleY);
        RectTransform version = RuntimeUI.Outline(RuntimeUI.Text(mainPage.transform, "v" + Application.version, 16, Faint, Vector2.zero, new Vector2(160, 24), TextAlignmentOptions.MidlineRight)).rectTransform;
        version.anchorMin = version.anchorMax = version.pivot = new Vector2(1f, 0f);
        version.anchoredPosition = new Vector2(-14f, 8f);

        Transform list = mainPage.transform;
        Backdrop(list, new Vector2(0, 66), new Vector2(800, 112));
        heroButton = TextButton(list, "Hero", 100, () => CycleHero(1), size: 26f);
        mapButton = TextButton(list, "Map", 62, () => CycleMap(1), size: 26f);
        mapButton.GetComponent<TextMeshProUGUI>().text = $"<  MAP: {MapGenerator.Names[MapGenerator.Choice].ToUpper()}  >";
        heroBlurb = RuntimeUI.Outline(RuntimeUI.Text(list, "", 16, new Color(0.79f, 0.82f, 0.94f), new Vector2(0, 30), new Vector2(760, 24)));
        CycleHero(0);
        nameLabel = RuntimeUI.Outline(RuntimeUI.Text(list, "NAME:", 30, ItemColor, new Vector2(-110, -5), new Vector2(160, 44), TextAlignmentOptions.MidlineRight));
        nameField = NameField(list, new Vector2(100, -5), new Vector2(240, 44));
        nameHint = RuntimeUI.Outline(RuntimeUI.Text(list, "", 16, new Color(1f, 0.45f, 0.4f), new Vector2(0, -35), new Vector2(480, 22)));

        startButton = TextButton(list, "Start", -80, TryStart);
        TextButton(list, "Leaderboard", -126, ShowBoard);
        TextButton(list, "Achievements", -172, ShowAchievements);
        Button settings = null;
        settings = TextButton(list, "Settings", -218, () => Settings.Open(settings.gameObject));
        TextButton(list, "Credits", -264, ShowCredits);
        TextButton(list, "Quit", -310, Quit);

        nameField.text = PlayerName != "" ? PlayerName : PlayerPrefs.GetString("playerName", "");
        Validate();
    }

    private static Color ItemColor => RuntimeUI.ItemColor;
    private static string Pad(UnityEngine.InputSystem.LowLevel.GamepadButton b) => Keybinds.PadLabel(b);

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
    private TextMeshProUGUI nameLabel, heroBlurb;
    private Button heroButton, mapButton;
    private HeroPreview preview;
    private static bool reselectMap;

    // The map is built when the scene loads, so switching reloads the scene (the menu backdrop shows the new map).
    private void CycleMap(int step)
    {
        int n = MapGenerator.Names.Length;
        PlayerPrefs.SetInt("map", ((MapGenerator.Choice + step) % n + n) % n);
        PlayerPrefs.Save();
        if (TypedName != "") PlayerName = TypedName; // keep an unsaved name across the reload
        reselectMap = true;
        Sfx.Play(Sfx.Id.Click, 0.5f);
        BackToMenu();
    }

    private void CycleHero(int step)
    {
        if (step != 0) { Hero.Cycle(step); Sfx.Play(Sfx.Id.Click, 0.5f); }
        heroButton.GetComponent<TextMeshProUGUI>().text = $"<  HERO: {Hero.Current.name.ToUpper()}  >";
        heroBlurb.text = Hero.Current.blurb;
        if (preview == null) preview = HeroPreview.Create();
        preview.Show(Hero.Current.id);
    }

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
        // Acts like a button: Enter starts editing, Enter again confirms. It never starts the run.
        field.shouldActivateOnSelect = false;
        return field;
    }

    private string TypedName => nameField.text.Trim();

    private void Validate()
    {
        bool blocked = NameFilter.IsBlocked(TypedName) || TypedName.IndexOfAny(new[] { '<', '>' }) >= 0;
        bool ok = TypedName.Length > 0 && !blocked;
        SetEnabled(startButton, ok);
        nameHint.text = ok ? "" : blocked ? "Please pick a different name" : "A name is required to start";
    }

    private void TryStart()
    {
        Validate();
        if (!startButton.interactable) return;
        PlayerName = TypedName;
        if (preview != null) Destroy(preview.gameObject);
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
            if (!c.isRootCanvas || !c.enabled || c.gameObject == root || c.GetComponentInParent<Settings>() != null || c.GetComponentInParent<ClickFx>() != null || c.GetComponentInParent<GamepadUI>() != null) continue;
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
            if (startButton.interactable) // valid name, so the hint line is free (errors keep it otherwise)
                nameHint.text = nameField.isFocused ? "<color=#C9D2F0>Press Enter to confirm</color>" : "";
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
        creditsPage.SetActive(false);
        if (reselectMap) { reselectMap = false; Select(mapButton.gameObject); return; }
        Select(nameField.gameObject);
        if (TypedName == "") nameField.ActivateInputField(); // first run: let them type straight away
    }

    private void BuildBoard(Transform parent)
    {
        boardPage = Page(parent, "Leaderboard");
        RectTransform panel = Container(boardPage.transform, new Vector2(1200, 680));
        boardTitle = BuildTitle(panel, "LEADERBOARD", 60f, 292f);
        Backdrop(panel, new Vector2(0, 5), new Vector2(1170, 530), 0.78f);
        boardBack = ShowMain;
        heroFilterButton = TextButton(panel, "Hero", 292, () => CycleFilter(ref heroFilter, Hero.All.Length, 1), -430, 22);
        mapFilterButton = TextButton(panel, "Map", 292, () => CycleFilter(ref mapFilter, MapGenerator.Names.Length, 1), 430, 22);
        RefreshFilterLabels();
        const float headerY = 245f;
        Column(panel, "RANK", -555, 80, headerY, Faint, 20);
        Column(panel, "NAME", -436, 196, headerY, Faint, 20);
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
            // Hero portrait with a little map badge, then the name.
            row.hero = RuntimeUI.Box(panel, "Hero", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.white);
            row.hero.rectTransform.anchoredPosition = new Vector2(-452, y + 2);
            row.hero.rectTransform.sizeDelta = new Vector2(40, 40);
            row.hero.preserveAspect = true;
            row.hero.raycastTarget = false;
            row.mapDot = RuntimeUI.Box(panel, "Map", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.white);
            row.mapDot.sprite = Shapes.Circle;
            row.mapDot.rectTransform.anchoredPosition = new Vector2(-438, y - 12);
            row.mapDot.rectTransform.sizeDelta = new Vector2(12, 12);
            row.mapDot.raycastTarget = false;
            var dotRim = row.mapDot.gameObject.AddComponent<Outline>();
            dotRim.effectColor = new Color(0.04f, 0.03f, 0.1f, 0.9f);
            dotRim.effectDistance = new Vector2(1, -1);
            row.name = Column(panel, "", -428, 188, y, Color.white, 24);
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
        RuntimeUI.Outline(RuntimeUI.Text(panel, $"<color=#FF7AD9>pink time</color> = endless  dot: <color=#73D959>meadow</color> <color=#B3E6FF>winter</color> <color=#FF7326>volcano</color>\n<- / -> or {Pad(UnityEngine.InputSystem.LowLevel.GamepadButton.LeftShoulder)} / {Pad(UnityEngine.InputSystem.LowLevel.GamepadButton.RightShoulder)} pages   Esc / {Pad(UnityEngine.InputSystem.LowLevel.GamepadButton.East)} back", 16, Faint, new Vector2(470, -292), new Vector2(240, 44)));
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
        entries = Leaderboard.Load(HeroFilter, MapFilter);
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
            bool justMade = rank == LastRank && !Filtered;
            row.bg.color = justMade ? new Color(1f, 0.85f, 0.3f, 0.18f) : i % 2 == 0 ? new Color(1f, 1f, 1f, 0.06f) : Color.clear;
            row.shine.gameObject.SetActive(justMade && e != null);

            row.name.text = e != null ? NameFilter.Clean(e.name) : "<color=#5A6280>---</color>";
            row.hero.gameObject.SetActive(e != null);
            row.mapDot.gameObject.SetActive(e != null);
            if (e != null)
            {
                row.hero.sprite = Hero.Portrait(Leaderboard.HeroOf(e));
                row.mapDot.color = MapColor(Leaderboard.MapOf(e));
            }
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

    private static string RewardShort(string reward, bool earned)
    {
        if (reward == "reroll") return "<color=#9FD8FF>+1 Reroll</color>";
        if (reward == "eliminate") return "<color=#9FD8FF>+1 Eliminate</color>";
        if (reward == Achievements.MeowlReward) return earned ? "<color=#BF8CFF>Meowl</color>" : "<color=#BF8CFF>???</color>";
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
            sb.Append($"{mark}<pos=4%><b>{name}</b><pos=27%><color=#C9D2F0>{d.goal}</color>{progress}<pos=74%>{(on ? "" : "<alpha=#88>")}{RewardShort(d.reward, on)}<alpha=#FF>\n");
        }
        sb.Append($"\n<color=#9AA3C0>{done} / {Achievements.All.Length} done.  <color=#FFB36B>Weapons</color> and <color=#D7A6FF>skills</color> join the level-up pool once earned; <color=#9FD8FF>bonuses</color> apply every run.</color>");
        achText.text = sb.ToString();
        mainPage.SetActive(false);
        achPage.SetActive(true);
        Select(achBack.gameObject);
    }

    private Button creditsBack;

    // Keep in sync with CREDITS.md.
    private const string CreditsText =
        "<color=#FFE08A><b>Music</b></color>\n" +
        "Kevin MacLeod (incompetech.com)\n" +
        "Licensed under Creative Commons: By Attribution 4.0\n" +
        "<color=#9AA3C0>Itty Bitty 8 Bit, Pixelland, Pixel Peeker Polka,\nVolatile Reaction, Exit the Premises</color>\n\n" +
        "<color=#FFE08A><b>Art</b></color>\n" +
        "Cats  -  Slime Cat Mega Pack by ToffeeCraft  <color=#9AA3C0>(toffeecraft.itch.io)</color>\n" +
        "Beholder bosses  -  CraftPix.net\n" +
        "Map tiles, trees and rocks  -  Topdown RPG 32x32 Nature by Mixel\n\n" +
        "<color=#FFE08A><b>Font</b></color>\n" +
        "Perfect DOS VGA 437 by Zeh Fernando\n\n" +
        "<color=#9AA3C0>Mira, special cat outfits, icons and effects made for Herding Cats.</color>";

    private void BuildCredits(Transform parent)
    {
        creditsPage = Page(parent, "Credits");
        RectTransform panel = Container(creditsPage.transform, new Vector2(1200, 680));
        BuildTitle(panel, "CREDITS", 60f, 292f);
        Backdrop(panel, Vector2.zero, new Vector2(1180, 540), 0.78f);
        TextMeshProUGUI text = RuntimeUI.Outline(RuntimeUI.Text(panel, CreditsText, 22, Color.white, Vector2.zero, new Vector2(1100, 500), TextAlignmentOptions.Center));
        text.enableAutoSizing = true;
        text.fontSizeMin = 14;
        text.fontSizeMax = 24;
        creditsBack = TextButton(panel, "Back", -292, ShowMain, 0, 28);
        creditsPage.SetActive(false);
    }

    private void ShowCredits()
    {
        mainPage.SetActive(false);
        creditsPage.SetActive(true);
        Select(creditsBack.gameObject);
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
