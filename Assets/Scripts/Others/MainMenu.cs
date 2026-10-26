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
        SceneFade.Reload(); // paw wipe closes, reloads, opens
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
        public Image bg, medal, shine, hero, mapDot;
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
        if (autoStart) { autoStart = false; TryStart(true); } // came back from a reload for the chosen map (still behind the wipe)
    }

    private void Update()
    {
        if (root == null) return;
        Animate();
        if (!IsOpen) return;
        Time.timeScale = 0f;
        if (!Started) HideGameUi();
        if (preview != null) preview.gameObject.SetActive(selectPage != null && selectPage.activeSelf && !Settings.IsOpen);

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
        if (selectPage != null && selectPage.activeSelf && back) { ShowMain(); return; }
        // Arrows only move the cursor. Selectors (hero, map, filters) change with Enter / A / a click.
        if (!boardPage.activeSelf) return;
        if (back) { boardBack(); return; }
        if (GamepadUI.Pressed(g => g.leftShoulder)) SetPage(page - 1);
        else if (GamepadUI.Pressed(g => g.rightShoulder)) SetPage(page + 1);
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
        BuildSelect(canvas.transform);
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
        nameLabel = RuntimeUI.Outline(RuntimeUI.Text(list, "NAME:", 30, ItemColor, new Vector2(-110, -5), new Vector2(160, 44), TextAlignmentOptions.MidlineRight));
        nameField = NameField(list, new Vector2(100, -5), new Vector2(240, 44));
        nameHint = RuntimeUI.Outline(RuntimeUI.Text(list, "", 16, new Color(1f, 0.45f, 0.4f), new Vector2(0, -35), new Vector2(480, 22)));

        startButton = TextButton(list, "Start", -80, ShowSelect);
        Button board = TextButton(list, "Leaderboard", -126, ShowBoard);
        TextButton(list, "Achievements", -172, ShowAchievements);
        Button settings = null;
        settings = TextButton(list, "Settings", -218, () => Settings.Open(settings.gameObject));
        TextButton(list, "Credits", -264, ShowCredits);
#if !UNITY_WEBGL || UNITY_EDITOR
        TextButton(list, "Quit", -310, Quit);
#endif
        // The name field sits off-center, so automatic nav skipped Start.
        Navigation startNav = startButton.navigation; startNav.mode = Navigation.Mode.Explicit;
        startNav.selectOnUp = nameField; startNav.selectOnDown = board;
        startButton.navigation = startNav;
        RuntimeUI.Nav(nameField, down: startButton);

        nameField.text = PlayerName != "" ? PlayerName : PlayerPrefs.GetString("playerName", "");
        Validate();
    }

    private static Color ItemColor => RuntimeUI.ItemColor;
    private static string Pad(UnityEngine.InputSystem.LowLevel.GamepadButton b) => Keybinds.PadLabel(b);

    private const float TitleY = 190f;

    private static RectTransform BuildTitle(Transform parent, string text, float size, float y) => RuntimeUI.Title(parent, text, size, new Vector2(0f, y));
    private static void SetTitle(RectTransform holder, string text) => RuntimeUI.SetTitle(holder, text);

    private static RectTransform Container(Transform parent, Vector2 size)
    {
        var rt = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = size;
        return rt;
    }
    private TextMeshProUGUI nameLabel;

    // ---- Run select: Start opens this; pick a hero and a map, read what they do, then Play. ----
    private GameObject selectPage;
    private Button heroButton, mapButton, playButton;
    private TextMeshProUGUI heroText, mapText;
    private Image mapSwatch, previewFrame;
    private MapPreviewUI mapPreview;
    private HeroPreview preview;
    private int pendingMap;
    private static bool autoStart; // set when Play has to reload the scene for a different map

    private static readonly Color[] MapSwatches = { new Color(0.45f, 0.75f, 0.35f), new Color(0.75f, 0.88f, 1f), new Color(0.95f, 0.4f, 0.12f) };

    // Cat theme: one card per hero (the picked one turns gold and gets ears), the kit underneath, the map on its own panel.
    private Image[] heroCards, heroSlots;
    private TextMeshProUGUI[] heroNames;

    private void BuildSelect(Transform parent)
    {
        selectPage = Page(parent, "Select");
        RectTransform panel = Container(selectPage.transform, new Vector2(1200, 680));
        BuildTitle(panel, "CHOOSE YOUR RUN", 56f, 296f);

        // Left: a card per hero. Click one, or Enter / A on the hero row.
        int n = Hero.All.Length;
        heroCards = new Image[n];
        heroSlots = new Image[n];
        heroNames = new TextMeshProUGUI[n];
        const float cardW = 230f, gap = 20f, left = -570f;
        for (int i = 0; i < n; i++)
        {
            Hero.Def h = Hero.All[i];
            float x = left + cardW / 2f + i * (cardW + gap);
            Image card = CatTheme.Panel(panel, "HeroCard", CatTheme.Style.Panel, new Vector2(x, 52f), new Vector2(cardW, 336f));
            CatTheme.Ears(card.rectTransform, "ear_gold"); // only shown on the picked hero
            card.raycastTarget = true;
            int index = i;
            Button pick = card.gameObject.AddComponent<Button>();
            pick.transition = Selectable.Transition.None;
            pick.navigation = new Navigation { mode = Navigation.Mode.None };
            pick.onClick.AddListener(() => PickHero(index));
            heroCards[i] = card;

            heroSlots[i] = CatTheme.Panel(card.transform, "Portrait", CatTheme.Style.Slot, new Vector2(0f, 53f), new Vector2(190f, 190f));
            CatTheme.Img(heroSlots[i].transform, "Hero", Hero.Portrait(h.id), Vector2.zero, new Vector2(160f, 160f));

            heroNames[i] = RuntimeUI.Outline(RuntimeUI.Text(card.transform, h.name.ToUpper(), 30, CatTheme.Cream, new Vector2(0f, -62f), new Vector2(210f, 36f)));
            int colon = h.blurb.IndexOf(':');
            string role = colon > 0 ? h.blurb.Substring(0, colon).ToUpper() : "";
            string desc = colon > 0 ? h.blurb.Substring(colon + 1).Trim() : h.blurb;
            RuntimeUI.Outline(RuntimeUI.Text(card.transform, role, 16, RoleColor(i), new Vector2(0f, -92f), new Vector2(210f, 22f)));
            TextMeshProUGUI blurb = RuntimeUI.Outline(RuntimeUI.Text(card.transform, desc, 14, CatTheme.LilacHi, new Vector2(0f, -132f), new Vector2(204f, 60f)));
            blurb.textWrappingMode = TextWrappingModes.Normal;
            blurb.enableAutoSizing = true;
            blurb.fontSizeMin = 10;
            blurb.fontSizeMax = 14;
        }
        heroButton = TextButton(panel, "Hero", -142, () => CycleHero(1), -195, 26);
        CatTheme.Button(heroButton, 14f);

        // The picked hero's kit.
        CatTheme.Panel(panel, "Kit", CatTheme.Style.Dim, new Vector2(-210, -232), new Vector2(720, 124));
        heroText = RuntimeUI.Outline(RuntimeUI.Text(panel, "", 16, Color.white, new Vector2(-210, -232), new Vector2(690, 104), TextAlignmentOptions.TopLeft));
        heroText.textWrappingMode = TextWrappingModes.Normal;
        heroText.enableAutoSizing = true;
        heroText.fontSizeMin = 10;
        heroText.fontSizeMax = 16;

        // Right: map.
        // Top level with the hero cards, bottom level with the hero row button.
        CatTheme.Panel(panel, "MapPanel", CatTheme.Style.Panel, new Vector2(380, 27.5f), new Vector2(380, 385));
        mapButton = TextButton(panel, "Map", 180, () => CycleMap(1), 380, 28);
        CatTheme.Button(mapButton, 10f);
        mapSwatch = RuntimeUI.Box(panel, "Accent", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.white);
        mapSwatch.rectTransform.anchoredPosition = new Vector2(380, 146);
        mapSwatch.rectTransform.sizeDelta = new Vector2(300, 4);
        mapSwatch.raycastTarget = false;
        previewFrame = RuntimeUI.Box(panel, "PreviewFrame", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.white);
        previewFrame.rectTransform.anchoredPosition = new Vector2(380, 58);
        previewFrame.rectTransform.sizeDelta = new Vector2(348, 150);
        previewFrame.raycastTarget = false;
        CatTheme.Frame(previewFrame, CatTheme.Style.Slot);
        mapPreview = MapPreviewUI.Create(panel, new Vector2(380, 58), new Vector2(326, 128));
        mapText = RuntimeUI.Outline(RuntimeUI.Text(panel, "", 17, Color.white, new Vector2(380, -95), new Vector2(340, 120), TextAlignmentOptions.Top));
        mapText.textWrappingMode = TextWrappingModes.Normal;
        mapText.lineSpacing = 4f;
        mapText.enableAutoSizing = true;
        mapText.fontSizeMin = 11;
        mapText.fontSizeMax = 17;

        // Same width as the map panel, same height as the kit box beside it (the frame adds 48 x 14).
        playButton = TextButton(panel, "Play", -232, Play, 380, 38);
        ((RectTransform)playButton.transform).sizeDelta = new Vector2(332f, 110f);
        CatTheme.Button(playButton, 24f);
        Button back = TextButton(panel, "Back", -322, ShowMain, -505, 22);
        CatTheme.Button(back, 8f);
        // Arrows follow the layout: Hero row (bottom left), Map (top right), Play (bottom right), Back (corner).
        // Enter / A on Hero or Map changes it; arrows never do.
        RuntimeUI.Nav(heroButton, up: mapButton, down: back, left: back, right: playButton);
        RuntimeUI.Nav(mapButton, up: playButton, down: playButton, left: heroButton);
        RuntimeUI.Nav(playButton, up: mapButton, down: mapButton, left: heroButton);
        RuntimeUI.Nav(back, up: heroButton, right: heroButton);
        RuntimeUI.Outline(RuntimeUI.Text(panel, $"Enter / {Pad(UnityEngine.InputSystem.LowLevel.GamepadButton.South)} on Hero or Map to change it   Esc / {Pad(UnityEngine.InputSystem.LowLevel.GamepadButton.East)} back", 15, CatTheme.Faint, new Vector2(-60, -322), new Vector2(620, 30)));
    }

    private static Color RoleColor(int i) => (i % 3) switch { 0 => CatTheme.Pink, 1 => CatTheme.Gold, _ => CatTheme.Slime };

    private void PickHero(int index)
    {
        int step = index - Hero.Index;
        if (step != 0) CycleHero(step);
        Select(heroButton.gameObject);
    }

    // Gold frame, ears and blue portrait slot on the picked hero's card.
    private void RefreshHeroCards()
    {
        if (heroCards == null) return;
        for (int i = 0; i < heroCards.Length; i++)
        {
            bool on = i == Hero.Index;
            heroCards[i].sprite = CatTheme.Get(on ? CatTheme.Style.PanelGold : CatTheme.Style.Panel);
            foreach (Transform child in heroCards[i].transform)
                if (child.name.StartsWith("Ear")) child.gameObject.SetActive(on);
            heroSlots[i].sprite = CatTheme.Get(on ? CatTheme.Style.Slime : CatTheme.Style.Slot);
            heroNames[i].color = on ? CatTheme.GoldHi : CatTheme.Cream;
        }
    }

    // Start: check the name, then on to picking the hero and map.
    private void ShowSelect()
    {
        Validate();
        if (!startButton.interactable) return;
        PlayerName = TypedName;
        PlayerPrefs.SetString("playerName", PlayerName);
        PlayerPrefs.Save();
        mainPage.SetActive(false);
        selectPage.SetActive(true);
        pendingMap = MapGenerator.Choice;
        CycleHero(0);
        CycleMap(0);
        Select(playButton.gameObject);
    }

    private void CycleHero(int step)
    {
        if (step != 0) { Hero.Cycle(step); Sfx.Play(Sfx.Id.Click, 0.5f); }
        heroButton.GetComponent<TextMeshProUGUI>().text = $"<  {Hero.Current.name.ToUpper()}  >";
        heroText.text = $"<color=#FFF0AA>{Hero.Current.name.ToUpper()} STARTS WITH</color>\n{Hero.Current.kit}";
        RefreshHeroCards(); // the cards show the heroes now, so no world-drawn preview here
    }

    // Just a choice here; the map is built when the scene loads, so Play reloads first if it changed.
    private void CycleMap(int step)
    {
        int n = MapGenerator.Names.Length;
        pendingMap = ((pendingMap + step) % n + n) % n;
        if (step != 0) Sfx.Play(Sfx.Id.Click, 0.5f);
        mapButton.GetComponent<TextMeshProUGUI>().text = $"<  {MapGenerator.Names[pendingMap].ToUpper()}  >";
        mapText.text = MapGenerator.Blurbs[pendingMap];
        mapSwatch.color = MapSwatches[pendingMap];
        previewFrame.color = MapSwatches[pendingMap];
        mapPreview.Show(pendingMap);
    }

    private void Play()
    {
        PlayerPrefs.SetInt("map", pendingMap);
        PlayerPrefs.Save();
        if (pendingMap != MapGenerator.Biome) { autoStart = true; BackToMenu(); return; } // rebuild the world for that map, then go
        SceneFade.Close(() => { TryStart(true); SceneFade.Open(); }); // paw wipe into the run
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

    private void TryStart(bool instant = false)
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
        if (instant) { Destroy(root); root = null; } // hidden behind the paw wipe: just go
        else StartCoroutine(FadeOut());
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
            if (!c.isRootCanvas || !c.enabled || c.gameObject == root || c.GetComponentInParent<Settings>() != null || c.GetComponentInParent<ClickFx>() != null || c.GetComponentInParent<GamepadUI>() != null || c.GetComponent<SceneFade>() != null) continue;
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
        }
    }

    private void ShowMain()
    {
        mainPage.SetActive(true);
        boardPage.SetActive(false);
        achPage.SetActive(false);
        creditsPage.SetActive(false);
        if (selectPage != null) selectPage.SetActive(false);
        Select(nameField.gameObject);
        if (TypedName == "") nameField.ActivateInputField(); // first run: let them type straight away
    }

    private void BuildBoard(Transform parent)
    {
        boardPage = Page(parent, "Leaderboard");
        RectTransform panel = Container(boardPage.transform, new Vector2(1200, 680));
        boardTitle = BuildTitle(panel, "LEADERBOARD", 60f, 292f);
        CatTheme.Panel(panel, "Panel", CatTheme.Style.Panel, new Vector2(0, 5), new Vector2(1170, 530));
        boardBack = ShowMain;
        heroFilterButton = TextButton(panel, "Hero", 292, () => CycleFilter(ref heroFilter, Hero.All.Length, 1), -430, 22);
        mapFilterButton = TextButton(panel, "Map", 292, () => CycleFilter(ref mapFilter, MapGenerator.Names.Length, 1), 430, 22);
        Width(heroFilterButton, 250f);
        Width(mapFilterButton, 250f);
        CatTheme.Button(heroFilterButton, 10f);
        CatTheme.Button(mapFilterButton, 10f);
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
        foreach (Button b in new[] { prevButton, nextButton, backLabel.GetComponent<Button>() }) { Width(b, 170f); CatTheme.Button(b, 10f); }
        // One legend line under the buttons (it used to be squeezed into the corner).
        TextMeshProUGUI legend = RuntimeUI.Outline(RuntimeUI.Text(panel, $"<color=#FF7AD9>pink time</color> = endless    dot = map: <color=#73D959>meadow</color> <color=#B3E6FF>winter</color> <color=#FF7326>volcano</color>    {Pad(UnityEngine.InputSystem.LowLevel.GamepadButton.LeftShoulder)} / {Pad(UnityEngine.InputSystem.LowLevel.GamepadButton.RightShoulder)} pages    Esc / {Pad(UnityEngine.InputSystem.LowLevel.GamepadButton.East)} back", 16, CatTheme.Faint, new Vector2(0, -334), new Vector2(1150, 24)));
        CatTheme.PlateBehind(legend.rectTransform, new Vector2(1000f, 30f)); // readable over the grass
    }

    // Text buttons default to 11 characters wide; themed frames follow the rect, so narrow it to fit.
    private static void Width(Button b, float w)
    {
        var rt = (RectTransform)b.transform;
        rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
    }

    // Leaderboard names come from other players: drop characters the pixel font can't draw (they showed as boxes).
    private static string Printable(TextMeshProUGUI t, string s)
    {
        if (t.font == null) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s) if (t.font.HasCharacter(c, true, true)) sb.Append(c); // tryAdd: dynamic fonts add glyphs on first use
        return sb.Length > 0 ? sb.ToString() : "?";
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
        int pages = Mathf.Clamp((entries.Count + RowsPerPage - 1) / RowsPerPage, 1, Pages);
        page = Mathf.Clamp(p, 0, pages - 1);
        pageText.text = $"Page {page + 1} / {pages}";
        SetEnabled(prevButton, page > 0);
        SetEnabled(nextButton, page < pages - 1);
        // Arrows follow the layout: filters on the top row, Back / Prev / Next on the bottom row (disabled ones skipped).
        // Enter / A on a filter changes it; shoulder buttons also turn pages.
        var bottom = new List<Selectable> { backLabel.GetComponent<Button>() };
        if (prevButton.interactable) bottom.Add(prevButton);
        if (nextButton.interactable) bottom.Add(nextButton);
        for (int i = 0; i < bottom.Count; i++)
            RuntimeUI.Nav(bottom[i], up: bottom[i] == nextButton ? mapFilterButton : heroFilterButton,
                          left: i > 0 ? bottom[i - 1] : null, right: i < bottom.Count - 1 ? bottom[i + 1] : null);
        RuntimeUI.Nav(heroFilterButton, down: bottom[0], right: mapFilterButton);
        RuntimeUI.Nav(mapFilterButton, down: bottom[bottom.Count - 1], left: heroFilterButton);

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
            row.rank.color = mine && !medal ? Gold : Color.white;
            row.name.color = mine ? CatTheme.GoldHi : Color.white;
            bool justMade = rank == LastRank && !Filtered;
            // Your rows get a soft gold tint (stronger for the run you just finished); the rest alternate stripes.
            row.bg.color = justMade ? new Color(1f, 0.85f, 0.3f, 0.22f) : mine ? new Color(1f, 0.85f, 0.3f, 0.1f)
                         : i % 2 == 0 ? new Color(1f, 1f, 1f, 0.05f) : Color.clear;
            row.shine.gameObject.SetActive(justMade && e != null);

            row.name.text = e != null ? Printable(row.name, NameFilter.Clean(e.name)) : "<color=#5A6280>---</color>";
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
        CatTheme.Panel(panel, "Panel", CatTheme.Style.Panel, new Vector2(0, 0), new Vector2(1180, 550));

        // A table: status dot, name, goal (with progress), reward. One striped row per achievement.
        int n = Achievements.All.Length;
        const float top = 205f, bottom = -215f, headerY = 240f;
        float rowH = (top - bottom) / Mathf.Max(1, n - 1);
        Column(panel, "ACHIEVEMENT", -540, 260, headerY, CatTheme.Faint, 18);
        Column(panel, "GOAL", -270, 520, headerY, CatTheme.Faint, 18);
        Column(panel, "REWARD", 285, 270, headerY, CatTheme.Faint, 18);
        achRows = new AchRow[n];
        for (int i = 0; i < n; i++)
        {
            float y = top - i * rowH;
            var r = new AchRow();
            r.bg = RuntimeUI.Box(panel, "Row", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), i % 2 == 0 ? new Color(1f, 1f, 1f, 0.05f) : Color.clear);
            r.bg.rectTransform.anchoredPosition = new Vector2(0, y);
            r.bg.rectTransform.sizeDelta = new Vector2(1140, rowH - 1f);
            r.bg.raycastTarget = false;
            r.dot = RuntimeUI.Box(panel, "Dot", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.white);
            r.dot.rectTransform.anchoredPosition = new Vector2(-553, y);
            r.dot.rectTransform.sizeDelta = new Vector2(14, 14);
            r.dot.raycastTarget = false;
            r.name = Column(panel, "", -540, 260, y, Color.white, 18);
            r.goal = Column(panel, "", -270, 545, y, new Color(0.79f, 0.82f, 0.94f), 18);
            r.reward = Column(panel, "", 285, 270, y, Color.white, 18);
            achRows[i] = r;
        }
        achText = RuntimeUI.Outline(RuntimeUI.Text(panel, "", 16, CatTheme.Faint, new Vector2(0, -250), new Vector2(1140, 24)));
        achBack = TextButton(panel, "Back", -305, ShowMain, 0, 28);
        Width(achBack, 170f);
        CatTheme.Button(achBack, 10f);
    }

    private class AchRow { public Image bg, dot; public TextMeshProUGUI name, goal, reward; }
    private AchRow[] achRows;

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
        int done = 0;
        for (int i = 0; i < achRows.Length; i++)
        {
            Achievements.Def d = Achievements.All[i];
            AchRow r = achRows[i];
            bool on = Achievements.IsUnlocked(d);
            if (on) done++;
            // Earned: green dot, gold name. Locked: dim dot, faded reward, progress if there is any.
            r.dot.sprite = Shapes.Circle;
            r.dot.color = on ? CatTheme.Mint : new Color(0.35f, 0.3f, 0.5f);
            r.name.text = d.name;
            r.name.color = on ? CatTheme.GoldHi : CatTheme.Cream;
            r.goal.text = d.goal + (!on && d.progress != null ? $"  <color=#FFB36B>({d.progress()})</color>" : "");
            r.reward.text = RewardShort(d.reward, on);
            r.reward.alpha = on ? 1f : 0.55f;
        }
        achText.text = $"{done} / {Achievements.All.Length} done.   <color=#FFB36B>Weapons</color> and <color=#D7A6FF>skills</color> join the level-up pool once earned; <color=#9FD8FF>bonuses</color> apply every run.";
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
        "Kai  -  8-direction Top Down Character by Gamekrazzy  <color=#9AA3C0>(gamekrazzy.itch.io)</color>\n" +
        "Beholder bosses  -  CraftPix.net\n" +
        "Map tiles, trees and rocks  -  Topdown RPG 32x32 Nature by Mixel\n\n" +
        "<color=#FFE08A><b>Font</b></color>\n" +
        "Perfect DOS VGA 437 by Zeh Fernando\n\n" +
        "<color=#9AA3C0>Mira, Nyx, special cat outfits, icons and effects made for Herding Cats.</color>";

    private void BuildCredits(Transform parent)
    {
        creditsPage = Page(parent, "Credits");
        RectTransform panel = Container(creditsPage.transform, new Vector2(1200, 680));
        BuildTitle(panel, "CREDITS", 60f, 292f);
        CatTheme.Panel(panel, "Panel", CatTheme.Style.Panel, Vector2.zero, new Vector2(1180, 540));
        Image cat = CatTheme.Cat(panel, "black", new Vector2(470f, 264f), 72f); // a cat sitting on the panel's edge
        cat.rectTransform.pivot = new Vector2(0.5f, 0f);
        TextMeshProUGUI text = RuntimeUI.Outline(RuntimeUI.Text(panel, CreditsText, 22, Color.white, Vector2.zero, new Vector2(1100, 500), TextAlignmentOptions.Center));
        text.enableAutoSizing = true;
        text.fontSizeMin = 14;
        text.fontSizeMax = 24;
        creditsBack = TextButton(panel, "Back", -305, ShowMain, 0, 28);
        Width(creditsBack, 170f);
        CatTheme.Button(creditsBack, 10f);
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
