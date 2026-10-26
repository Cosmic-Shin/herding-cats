using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    public PlayerStats stats;

    internal class Slot
    {
        public Image frame, icon;
        public TextMeshProUGUI badge, level;
        public bool themed; // cat-theme frames (the HUD); the leaderboard keeps the old ones
    }

    private const float SlotSize = 64f, SlotGap = 8f, Margin = 20f;
    private static readonly Color HpHigh = new Color(0.96f, 0.46f, 0.63f), HpLow = new Color(0.95f, 0.25f, 0.3f);
    private static readonly Color ComboGlow = new Color(1f, 0.8f, 0.3f);

    private RectTransform hpFill, xpFill, hpChunk, playerPanel;
    private Image hpFillImage, xpFillImage, ultFill, specialFrame;
    private TextMeshProUGUI specialBadge;
    private static readonly Color XpColor = new Color(0.36f, 0.75f, 0.96f);
    private Vector2 panelPos;
    private float lastHp = 1f, chunkHp = 1f, chunkDelay, panelShake;
    private float xpShown, xpPulse;
    private bool ultWasReady = true;
    private int lastKills;
    private float killPop;
    private readonly int[] slotLevels = new int[PlayerWeapons.MaxWeapons];
    private readonly float[] levelPop = new float[PlayerWeapons.MaxWeapons];
    private float ultPing;
    private TextMeshProUGUI hpText, levelText, timerText, killText, ultText, scoreText;
    private LevelUpUIController levelUp;
    private Slot[] weaponSlots, skillSlots;

    private float runTime;
    public float RunTime => runTime;
    public void Advance(float seconds) => runTime += seconds;
    private int skillsSignature = -1;
    private int weaponsSignature = -1;
    private PlayerUltimate ult;
    private PlayerSkills skills;
    private PlayerWeapons weapons;
    private PlayerCollabs collabs;

    private readonly HashSet<string> combinable = new HashSet<string>();
    private int combinableSignature;
    private float nextCombineCheck;

    private void Start()
    {
        if (stats == null) stats = FindFirstObjectByType<PlayerStats>();
        if (stats != null)
        {
            ult = stats.GetComponent<PlayerUltimate>();
            skills = stats.GetComponent<PlayerSkills>();
            weapons = stats.GetComponent<PlayerWeapons>();
            collabs = stats.GetComponent<PlayerCollabs>();
        }
        Build();
    }

    private void Build()
    {
        var root = (RectTransform)transform;

        RectTransform panel = Place(RuntimeUI.Frame(root, "PlayerPanel", Vector2.zero, Vector2.zero).rectTransform, new Vector2(0, 1), new Vector2(Margin, -Margin), new Vector2(500, 124));
        CatTheme.Frame(panel.GetComponent<Image>(), CatTheme.Style.Panel);
        Slot portrait = MakeSlot(panel, new Vector2(12, -12), 100f, themed: true);
        CatTheme.Frame(portrait.frame, CatTheme.Style.Slime);
        portrait.icon.sprite = stats != null && stats.TryGetComponent(out PlayerAnimator anim) && anim.spriteRenderer != null ? anim.spriteRenderer.sprite : null;
        portrait.icon.enabled = portrait.icon.sprite != null;
        portrait.frame.gameObject.AddComponent<RectMask2D>().padding = Vector4.one * 6f;
        Place(portrait.icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 34f), new Vector2(230f, 230f));
        portraitIcon = portrait.icon;
        heroLabel = Label(panel, "", 26, Color.white, new Vector2(124, -10), new Vector2(200, 30), TextAlignmentOptions.TopLeft);
        levelText = Label(panel, "LV 1", 26, new Color(1f, 0.85f, 0.35f), new Vector2(124, -10), new Vector2(364, 30), TextAlignmentOptions.TopRight);

        (hpFill, hpFillImage) = Bar(panel, new Vector2(124, -44), new Vector2(364, 32), new Color(0.21f, 0.07f, 0.16f));
        hpChunk = RuntimeUI.Box(hpFill.parent, "Chunk", Vector2.zero, Vector2.one, new Color(1f, 0.92f, 0.85f)).rectTransform;
        hpChunk.SetSiblingIndex(0);
        hpText = Label(hpFill.parent, "100 / 100", 22, Color.white, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, stretch: true);
        (xpFill, xpFillImage) = Bar(panel, new Vector2(124, -84), new Vector2(364, 26), new Color(0.06f, 0.1f, 0.22f));
        xpFillImage.color = XpColor;
        playerPanel = panel;
        panelPos = panel.anchoredPosition;

        float row1 = -Margin - 124 - 12, row2 = row1 - SlotSize - SlotGap, row3 = row2 - SlotSize - SlotGap;
        weaponSlots = new Slot[PlayerWeapons.MaxWeapons + 1 + PlayerCollabs.MaxCollabs]; // weapons, buddy, main-attack combos
        for (int i = 0; i < weaponSlots.Length; i++) weaponSlots[i] = MakeSlot(root, new Vector2(Margin + i * (SlotSize + SlotGap), row1), SlotSize, themed: true);
        skillSlots = new Slot[PlayerSkills.MaxSkills];
        for (int i = 0; i < skillSlots.Length; i++) skillSlots[i] = MakeSlot(root, new Vector2(Margin + i * (SlotSize + SlotGap), row2), SlotSize, themed: true);

        Slot special = MakeSlot(root, new Vector2(Margin, row3), SlotSize, themed: true);
        specialFrame = special.frame;
        CatTheme.Frame(specialFrame, CatTheme.Style.Pink);
        ultFill = special.icon;
        ultFill.sprite = ExplosionFx.GetCircle();
        ultFill.type = Image.Type.Filled;
        ultFill.fillMethod = Image.FillMethod.Radial360;
        ultFill.fillOrigin = (int)Image.Origin360.Top;
        ultFill.enabled = true;
        special.badge.gameObject.SetActive(true);
        specialBadge = special.badge;
        specialBadge.text = Keybinds.Prompt(Keybinds.Action.Special);
        special.badge.color = Color.white;
        ultText = Label(root, "", 24, Color.white, new Vector2(Margin + SlotSize + 12, row3 - 6), new Vector2(360, 56), TextAlignmentOptions.TopLeft);
        BuildTips(root, row3 - SlotSize - 16);

        timerText = Label(root, "00:00", 60, Color.white, Vector2.zero, new Vector2(320, 70), TextAlignmentOptions.Center, anchor: new Vector2(0.5f, 1f));
        ((RectTransform)timerText.transform).anchoredPosition = new Vector2(0, -16);
        timerText.outlineWidth = 0.28f;
        timerPlate = CatTheme.PlateBehind((RectTransform)timerText.transform, new Vector2(230f, 82f));
        RectTransform killBox = Place(RuntimeUI.Frame(root, "Kills", Vector2.zero, Vector2.zero).rectTransform, new Vector2(1, 1), new Vector2(-Margin, -Margin), new Vector2(300, 56));
        CatTheme.Frame(killBox.GetComponent<Image>(), CatTheme.Style.Panel);
        killText = Label(killBox, "DEFEATED  0", 24, Color.white, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, stretch: true);
        scoreText = Label(root, "SCORE  0", 24, Color.white, Vector2.zero, new Vector2(320, 30), TextAlignmentOptions.Center, anchor: new Vector2(0.5f, 1f));
        // Below the timer's plate (which reaches y -92), on its own smaller plate so the two don't run together.
        ((RectTransform)scoreText.transform).anchoredPosition = new Vector2(0, -102);
        scorePlate = CatTheme.PlateBehind((RectTransform)scoreText.transform, new Vector2(250f, 36f));
    }

    private static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, Vector2 pos, Vector2 box,
                                         TextAlignmentOptions align, bool stretch = false, Vector2? anchor = null)
    {
        TextMeshProUGUI t = RuntimeUI.Outline(RuntimeUI.Text(parent, text, size, color, Vector2.zero, box, align));
        var rt = (RectTransform)t.transform;
        if (stretch)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        else Place(rt, anchor ?? new Vector2(0, 1), pos, box);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    private static (RectTransform, Image) Bar(RectTransform parent, Vector2 pos, Vector2 size, Color back)
    {
        Image frame = RuntimeUI.Box(parent, "Bar", Vector2.zero, Vector2.zero, Color.white);
        CatTheme.BarFrame(frame);
        Place(frame.rectTransform, new Vector2(0, 1), pos, size);
        Image bg = RuntimeUI.Box(frame.transform, "Back", Vector2.zero, Vector2.one, back);
        bg.rectTransform.offsetMin = new Vector2(6, 6);
        bg.rectTransform.offsetMax = new Vector2(-6, -6);
        Image fill = RuntimeUI.Box(bg.transform, "Fill", Vector2.zero, Vector2.one, Color.white);
        CatTheme.BarFill(fill, Color.white);
        return (fill.rectTransform, fill);
    }

    internal static Slot MakeSlot(RectTransform parent, Vector2 pos, float size, bool themed = false)
    {
        var s = new Slot { themed = themed };
        s.frame = RuntimeUI.Box(parent, "Slot", Vector2.zero, Vector2.zero, Color.white);
        UiIcons.Frame(s.frame, UiIcons.Slot);
        if (themed) CatTheme.Frame(s.frame, CatTheme.Style.Slot);
        Place(s.frame.rectTransform, new Vector2(0, 1), pos, new Vector2(size, size));
        s.icon = RuntimeUI.Box(s.frame.transform, "Icon", Vector2.zero, Vector2.one, Color.white);
        s.icon.rectTransform.offsetMin = Vector2.one * size * 0.14f;
        s.icon.rectTransform.offsetMax = -Vector2.one * size * 0.14f;
        s.icon.preserveAspect = true;
        s.icon.enabled = false;
        s.badge = Label(s.frame.transform, "", size * 0.4f, Color.white, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, stretch: true);
        s.badge.gameObject.SetActive(false);
        s.level = Label(s.frame.transform, "", size * 0.3f, Color.white, new Vector2(-3, 2), new Vector2(size, size * 0.4f), TextAlignmentOptions.BottomRight, anchor: new Vector2(1, 0));
        return s;
    }

    internal static void Fill(Slot s, string iconId, string level, bool gold)
    {
        UiIcons.Apply(iconId, s.icon, s.badge);
        s.level.text = level;
        s.frame.sprite = SlotSprite(s, gold);
        if (s.frame.TryGetComponent(out Button b)) b.interactable = true;
        s.frame.color = Color.white;
    }

    internal static void Clear(Slot s)
    {
        s.icon.enabled = false;
        s.badge.gameObject.SetActive(false);
        s.level.text = "";
        s.frame.sprite = SlotSprite(s, false);
        s.frame.color = new Color(1f, 1f, 1f, 0.45f);
        if (s.frame.TryGetComponent(out Button b)) b.interactable = false;
    }

    private static Sprite SlotSprite(Slot s, bool gold)
    {
        Sprite themed = s.themed ? CatTheme.Get(gold ? CatTheme.Style.SlotGold : CatTheme.Style.Slot) : null;
        return themed != null ? themed : gold ? UiIcons.BoxGold : UiIcons.Slot;
    }

    private static string LevelTag(int level, int max) => level >= max ? "<color=#FFD24A>MAX</color>" : level.ToString();

    private Image portraitIcon, timerPlate, scorePlate;
    private TextMeshProUGUI heroLabel;
    private bool gearNavLive = true;

    // The gear slots are selectable so the pause menu can show their details. Anywhere else (level-up, rewards,
    // combine) they'd just be dead stops for the d-pad, so they're taken out of navigation unless paused.
    private void UpdateGearNavigation()
    {
        bool live = Paused;
        if (live == gearNavLive) return;
        gearNavLive = live;
        foreach (Slot[] set in new[] { weaponSlots, skillSlots })
            foreach (Slot s in set)
                if (s != null && s.frame.TryGetComponent(out Button b))
                    b.navigation = new Navigation { mode = live ? Navigation.Mode.Automatic : Navigation.Mode.None };
        if (!live && EventSystem.current != null && EventSystem.current.currentSelectedGameObject is GameObject sel && sel.GetComponent<SlotTip>() != null)
            EventSystem.current.SetSelectedGameObject(null); // don't leave the cursor parked on a slot
    }

    private void Update()
    {
        if (stats == null || hpFill == null) return;
        UpdateGearNavigation();
        string hero = Hero.Current.name.ToUpper(); // the menu can switch heroes after the HUD is built
        if (heroLabel.text != hero)
        {
            heroLabel.text = hero;
            portraitIcon.sprite = Hero.Portrait(Hero.Current.id); // always the front-facing frame, so the face shows
            portraitIcon.rectTransform.anchoredPosition = new Vector2(0f, 34f + Hero.Current.portraitDrop * 230f / 32f);
            portraitIcon.enabled = portraitIcon.sprite != null;
        }
        string special = Keybinds.Prompt(Keybinds.Action.Special); // may be rebound, or a pad plugged in
        if (specialBadge.text != special) specialBadge.text = special;

        if (stats.currentHealth > 0f) runTime += Time.deltaTime;

        float hp = Mathf.Clamp01(stats.currentHealth / stats.maxHealth);
        hpFill.anchorMax = new Vector2(hp, 1f);
        hpFillImage.color = Color.Lerp(HpLow, HpHigh, hp * 1.4f - 0.2f);
        UpdateHpChunk(hp);
        UpdateXp(Mathf.Clamp01((float)stats.currentExp / stats.expToNextLevel));

        hpText.text = $"{Mathf.CeilToInt(stats.currentHealth)} / {Mathf.CeilToInt(stats.maxHealth)}";
        levelText.text = $"LV {stats.level}";
        int seconds = (int)runTime;
        timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
        killText.text = $"DEFEATED  <color=#FFD24A>{EnemyHealth.Kills:N0}</color>";
        scoreText.text = $"SCORE  <color=#FFE08A>{Leaderboard.Score(runTime, stats.level):N0}</color>";
        // The level-up title sits where the timer and score are, so hide them while it's open.
        if (levelUp == null) levelUp = FindAnyObjectByType<LevelUpUIController>();
        timerText.enabled = scoreText.enabled = levelUp == null || !levelUp.IsOpen;
        if (timerPlate != null) timerPlate.enabled = timerText.enabled;
        if (scorePlate != null) scorePlate.enabled = scoreText.enabled;

        UpdateUltimate();
        RefreshCombinable();
        UpdateSkills();
        UpdateWeapons();
        GlowCombinable();
        PulseHud();
        if (tipFor != null && !Paused) HideTip(tipFor);
    }

    private void PulseHud()
    {
        float dt = Time.unscaledDeltaTime;
        if (EnemyHealth.Kills != lastKills) { lastKills = EnemyHealth.Kills; killPop = 1f; }
        killPop = Mathf.Max(0f, killPop - dt * 6f);
        killText.rectTransform.localScale = Vector3.one * (1f + 0.2f * killPop);

        if (weapons == null) return;
        IReadOnlyList<string> owned = weapons.OwnedInOrder;
        for (int i = 0; i < owned.Count && i < PlayerWeapons.MaxWeapons; i++)
        {
            Weapon w = weapons.Get(owned[i]);
            if (w == null) continue;
            int level = w.Level * 10 + w.Enhance;
            if (level > slotLevels[i] && slotLevels[i] > 0) levelPop[i] = 1f;
            slotLevels[i] = level;
            levelPop[i] = Mathf.Max(0f, levelPop[i] - dt * 2.5f);
            float fire = Mathf.Clamp01(1f - (Time.time - w.LastFired) / 0.2f);
            weaponSlots[i].icon.rectTransform.localScale = Vector3.one * (1f + 0.15f * fire + 0.5f * levelPop[i] * Mathf.Sin(levelPop[i] * Mathf.PI * 3f));
        }
    }

    private void UpdateWeapons()
    {
        if (weapons == null) return;

        IReadOnlyList<string> owned = weapons.OwnedInOrder;
        Companion buddy = Companion.Instance;
        List<string> mainCombos = collabs != null ? collabs.MainCombos : new List<string>();
        int signature = (buddy != null ? buddy.Level : 0) + combinableSignature + mainCombos.Count * 7919;
        foreach (string id in owned) signature = signature * 31 + id.GetHashCode() + weapons.LevelOf(id) + 100 * weapons.Get(id).Enhance;
        if (signature == weaponsSignature) return;
        weaponsSignature = signature;

        for (int i = 0; i < PlayerWeapons.MaxWeapons; i++)
        {
            if (i >= owned.Count) { Clear(weaponSlots[i]); continue; }
            string id = owned[i];
            bool collab = PlayerWeapons.IsCollab(id);
            int enhance = weapons.Get(id).Enhance;
            bool super = PlayerWeapons.IsSuper(id);
            string level = enhance > 0 ? $"<color=#FFD24A>+{enhance}</color>" : super ? "<color=#FF7AD9>S</color>" : collab ? "" : LevelTag(weapons.LevelOf(id), Weapon.MaxLevel);
            Fill(weaponSlots[i], id, level, gold: collab);
            if (super) weaponSlots[i].frame.color = new Color(1f, 0.6f, 0.95f);
        }

        // After the weapons: the buddy, then combos on the main attack, packed left with no gaps.
        int next = PlayerWeapons.MaxWeapons;
        extraCombos.Clear();
        if (buddy != null) { extraCombos.Add(null); Fill(weaponSlots[next++], "buddy", LevelTag(buddy.Level, Companion.MaxLevel), gold: true); }
        foreach (string id in mainCombos)
        {
            if (next >= weaponSlots.Length) break;
            extraCombos.Add(id);
            Fill(weaponSlots[next++], id, "", gold: true);
        }
        for (int i = PlayerWeapons.MaxWeapons; i < weaponSlots.Length; i++) weaponSlots[i].frame.gameObject.SetActive(i < next);
    }

    private void UpdateSkills()
    {
        if (skills == null) return;

        IReadOnlyList<string> owned = skills.OwnedInOrder;
        int signature = (skills.ReviveUsed ? 1 : 0) + combinableSignature;
        foreach (string id in owned) signature = signature * 31 + id.GetHashCode() + skills.LevelOf(id);
        if (signature == skillsSignature) return;
        skillsSignature = signature;

        for (int i = 0; i < skillSlots.Length; i++)
        {
            if (i >= owned.Count) { Clear(skillSlots[i]); continue; }
            PlayerSkills.Skill s = PlayerSkills.Find(owned[i]);
            string level = s.id == "secondwind" && skills.ReviveUsed ? "<color=#8890A8>USED</color>" : LevelTag(skills.LevelOf(s.id), s.levels.Length);
            Fill(skillSlots[i], "skill:" + s.id, level, gold: false);
        }
    }

    private void UpdateHpChunk(float hp)
    {
        float dt = Time.unscaledDeltaTime;
        if (hp < lastHp - 0.001f) { chunkDelay = 0.4f; panelShake = 0.25f; }
        lastHp = hp;
        if (hp >= chunkHp) chunkHp = hp;
        else if ((chunkDelay -= dt) <= 0f) chunkHp = Mathf.MoveTowards(chunkHp, hp, dt * 0.8f);
        hpChunk.anchorMax = new Vector2(chunkHp, 1f);

        panelShake = Mathf.Max(0f, panelShake - dt);
        playerPanel.anchoredPosition = panelPos + (panelShake > 0f ? Random.insideUnitCircle * 24f * panelShake : Vector2.zero);
    }

    private void UpdateXp(float xp)
    {
        float dt = Time.unscaledDeltaTime;
        if (xp > xpShown + 0.001f) xpPulse = 1f;
        xpShown = xp < xpShown - 0.2f ? 0f : Mathf.MoveTowards(xpShown, xp, dt * 1.5f);
        xpPulse = Mathf.Max(0f, xpPulse - dt * 3f);
        xpFill.anchorMax = new Vector2(xpShown, 1f);
        xpFillImage.color = Color.Lerp(XpColor, Color.white, 0.5f * xpPulse);
    }

    private void UpdateUltimate()
    {
        if (ult == null || ultFill == null) return;

        bool ready = ult.IsReady && !ult.IsActive;
        if (ready && !ultWasReady)
        {
            Sfx.Play(Sfx.Id.Ready, 0.6f);
            DamagePopup.SpawnText((Vector2)stats.transform.position + Vector2.up * 1f, $"{ult.Name} READY!  [{Keybinds.Prompt(Keybinds.Action.Special)}]", new Color(1f, 0.85f, 0.35f), 5f);
            ultPing = 1f;
        }
        ultWasReady = ready;
        ultPing = Mathf.Max(0f, ultPing - Time.unscaledDeltaTime * 2.5f);
        specialFrame.sprite = CatTheme.Get(ready ? CatTheme.Style.SlotGold : CatTheme.Style.Pink) ?? (ready ? UiIcons.BoxGold : UiIcons.Slot);
        specialFrame.rectTransform.localScale = Vector3.one * (1f + 0.35f * ultPing + (ready ? 0.05f * Mathf.Sin(Time.unscaledTime * 6f) : 0f));

        if (ult.IsActive)
        {
            ultFill.fillAmount = ult.ActiveRemaining / ult.duration;
            ultFill.color = new Color(1f, 0.55f, 0.15f);
            ultText.text = $"{ult.Name}\n<color=#FFB040>ACTIVE {Mathf.CeilToInt(ult.ActiveRemaining)}s</color>";
        }
        else if (ult.IsReady)
        {
            ultFill.fillAmount = 1f;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
            ultFill.color = new Color(1f, 0.82f, 0.3f) * pulse + new Color(0, 0, 0, 1f - pulse);
            ultText.text = $"{ult.Name}\n<color=#FFE08A>READY! press [{Keybinds.Prompt(Keybinds.Action.Special)}]</color>";
        }
        else
        {
            ultFill.fillAmount = 1f - ult.CooldownRemaining / ult.cooldown;
            ultFill.color = new Color(0.45f, 0.5f, 0.65f);
            ultText.text = $"{ult.Name}\n<color=#9AA3C0>{Mathf.CeilToInt(ult.CooldownRemaining)}s</color>";
        }
    }

    private void RefreshCombinable()
    {
        if (collabs == null || Time.unscaledTime < nextCombineCheck) return;
        nextCombineCheck = Time.unscaledTime + 0.25f;

        combinable.Clear();
        foreach (PlayerCollabs.Def d in collabs.Offerable())
        {
            if (d.kind != PlayerCollabs.Kind.Bomb) combinable.Add(d.a);
            combinable.Add(d.b);
        }
        int signature = combinable.Count;
        foreach (string id in combinable) signature += id.GetHashCode();
        combinableSignature = signature;
    }

    private void GlowCombinable()
    {
        if (combinable.Count == 0) return;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
        Glow(weapons != null ? weapons.OwnedInOrder : null, weaponSlots, pulse);
        Glow(skills != null ? skills.OwnedInOrder : null, skillSlots, pulse);
    }

    private void Glow(IReadOnlyList<string> owned, Slot[] slots, float pulse)
    {
        if (owned == null) return;
        for (int i = 0; i < slots.Length && i < owned.Count; i++)
        {
            if (!combinable.Contains(owned[i])) continue;
            slots[i].frame.sprite = SlotSprite(slots[i], true);
            slots[i].frame.color = Color.Lerp(Color.white, ComboGlow, pulse);
        }
    }

    // ---- Pause tooltips: hover (or gamepad-select) a weapon/skill slot for its details ----
    private RectTransform tipBox;
    private TextMeshProUGUI tipText;
    private SlotTip tipFor;
    private PauseMenu pause;
    private bool Paused => pause != null && pause.IsPaused && !Settings.IsOpen && !ComboBook.IsOpen;

    private class SlotTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public HUD hud;
        public System.Func<string> text;
        public void OnPointerEnter(PointerEventData _) => hud.ShowTip(this);
        public void OnSelect(BaseEventData _) => hud.ShowTip(this);
        public void OnPointerExit(PointerEventData _) => hud.HideTip(this);
        public void OnDeselect(BaseEventData _) => hud.HideTip(this);
    }

    private void BuildTips(RectTransform root, float top)
    {
        pause = FindFirstObjectByType<PauseMenu>();
        tipBox = Place(RuntimeUI.Frame(root, "Tooltip", Vector2.zero, Vector2.zero, gold: true).rectTransform, new Vector2(0, 1), new Vector2(Margin, top), new Vector2(380, 100));
        CatTheme.Frame(tipBox.GetComponent<Image>(), CatTheme.Style.PanelGold);
        tipText = RuntimeUI.Text(tipBox, "", 24, Color.white, Vector2.zero, Vector2.zero, TextAlignmentOptions.TopLeft);
        var rt = tipText.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(14, 12); rt.offsetMax = new Vector2(-14, -12);
        tipText.raycastTarget = false;
        tipBox.GetComponent<Image>().raycastTarget = false;
        tipBox.gameObject.SetActive(false);

        for (int i = 0; i < PlayerWeapons.MaxWeapons; i++) { int k = i; AddTip(weaponSlots[i], () => weapons != null && k < weapons.OwnedInOrder.Count ? WeaponTip(weapons.OwnedInOrder[k]) : ""); }
        for (int i = 0; i < skillSlots.Length; i++) { int k = i; AddTip(skillSlots[i], () => skills != null && k < skills.OwnedInOrder.Count ? SkillTip(skills.OwnedInOrder[k]) : ""); }
        for (int i = PlayerWeapons.MaxWeapons; i < weaponSlots.Length; i++)
        {
            int k = i - PlayerWeapons.MaxWeapons;
            AddTip(weaponSlots[i], () => k < extraCombos.Count && extraCombos[k] != null ? ComboTip(extraCombos[k], "") : "");
        }
    }

    internal Selectable FirstGearSlot { get { Button b = weaponSlots[0].frame.GetComponent<Button>(); return b != null && b.interactable ? b : null; } }

    private void AddTip(Slot s, System.Func<string> text)
    {
        s.frame.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // makes it gamepad-selectable
        SlotTip tip = s.frame.gameObject.AddComponent<SlotTip>();
        tip.hud = this;
        tip.text = text;
    }

    private void ShowTip(SlotTip from)
    {
        if (!Paused) return;
        string text = from.text();
        if (string.IsNullOrEmpty(text)) { HideTip(tipFor); return; }
        tipFor = from;
        tipText.text = text;
        tipBox.gameObject.SetActive(true);
        tipBox.SetParent(tipBox.GetComponentInParent<Canvas>().transform, true);
        tipBox.SetAsLastSibling(); // above the pause panel, which shares this canvas
        tipText.ForceMeshUpdate();
        tipBox.sizeDelta = new Vector2(tipBox.sizeDelta.x, tipText.preferredHeight + 26f);
    }

    private void HideTip(SlotTip from)
    {
        if (from == null || from != tipFor) return;
        tipFor = null;
        tipBox.gameObject.SetActive(false);
    }

    private static string Tip(string name, string tag, string body, string next, string source)
    {
        var sb = new System.Text.StringBuilder($"<size=115%><b><color=#FFE08A>{name}</color></b></size>  <color=#9AA3C0>{tag}</color>\n<color=#C9D2F0>{body}</color>");
        if (next != null) sb.Append($"\n\n<color=#7FD6FF>Next:</color> <color=#C9D2F0>{next}</color>");
        float dmg = source != null ? DamageStats.Of(source) : 0f;
        if (dmg > 0f) sb.Append($"\n\n<color=#9AA3C0>Damage dealt</color>  {DamageStats.Short(dmg)}");
        return sb.ToString();
    }

    private string WeaponTip(string id)
    {
        Weapon w = weapons.Get(id);
        string enhance = w != null && w.Enhance > 0 ? $"  <color=#FFD24A>+{w.Enhance}</color>" : "";
        if (PlayerCollabs.Find(id) != null) return ComboTip(id, enhance);
        PlayerWeapons.Def def = PlayerWeapons.Find(id);
        if (def == null || def.levels == null) return "";
        int level = weapons.LevelOf(id);
        string lv = level >= def.levels.Length ? "<color=#FFD24A>MAX</color>" : $"Lv {level}/{def.levels.Length}";
        string body = level > 1 ? $"{def.levels[0]}\n<color=#FFD24A>Latest:</color> {def.levels[level - 1]}" : def.levels[0];
        return Tip(def.name, lv + enhance, body + PairLine(id), level < def.levels.Length ? def.levels[level] : null, def.name);
    }

    private readonly List<string> extraCombos = new List<string>(); // what each slot after the weapons shows (null = buddy)

    private static string ComboTip(string id, string enhance)
    {
        PlayerCollabs.Def combo = PlayerCollabs.Find(id);
        PlayerWeapons.Def d = PlayerWeapons.Find(id);
        string tag = combo.kind == PlayerCollabs.Kind.Super ? "<color=#FF7AD9>SUPER</color>" : "<color=#FFD24A>COMBO</color>";
        string body = $"<color=#FFB36B>{PlayerCollabs.Recipe(combo)}</color>\n{combo.text}";
        return Tip(combo.name, tag + enhance, body, null, d != null ? d.name : null);
    }

    private static string PairLine(string id) =>
        PlayerCollabs.PairHint(id, 3) is string hint ? $"\n\n<color=#FFD24A>Pairs with:</color> {hint}" : "";

    private string SkillTip(string id)
    {
        PlayerSkills.Skill s = PlayerSkills.Find(id);
        if (s == null) return "";
        int level = skills.LevelOf(id);
        string lv = level >= s.levels.Length ? "<color=#FFD24A>MAX</color>" : $"Lv {level}/{s.levels.Length}";
        // Each level line after the first is an upgrade; show everything earned so far.
        string body = string.Join("\n", s.levels, 0, Mathf.Clamp(level, 1, s.levels.Length));
        return Tip(s.name, lv, body + PairLine(id), level < s.levels.Length ? s.levels[level] : null, null);
    }
}
