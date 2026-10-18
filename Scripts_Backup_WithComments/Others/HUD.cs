using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HoloCure-style HUD, built in code at startup (frames/icons from Tools > Build UI Assets):
//   top-left:   portrait, name + level, HP bar, EXP bar; under it the weapon icons (+ Cat Buddy),
//               the skill badges and the Special (ultimate) gauge
//   top-center: run timer          top-right: cats defeated
// Slots holding a piece of a ready combination pulse gold.
public class HUD : MonoBehaviour
{
    public PlayerStats stats; // auto-found if empty

    internal class Slot
    {
        public Image frame, icon;
        public TextMeshProUGUI badge, level;
    }

    private const float SlotSize = 64f, SlotGap = 8f, Margin = 20f;
    private static readonly Color HpHigh = new Color(0.35f, 0.9f, 0.4f), HpLow = new Color(0.95f, 0.25f, 0.25f);
    private static readonly Color ComboGlow = new Color(1f, 0.8f, 0.3f);

    private RectTransform hpFill, xpFill, hpChunk, playerPanel;
    private Image hpFillImage, xpFillImage, ultFill, specialFrame;
    private static readonly Color XpColor = new Color(0.3f, 0.7f, 1f);
    private Vector2 panelPos;
    private float lastHp = 1f, chunkHp = 1f, chunkDelay, panelShake;
    private float xpShown, xpPulse;
    private bool ultWasReady = true; // no ping for a special that starts ready
    private int lastKills;
    private float killPop;
    private readonly int[] slotLevels = new int[PlayerWeapons.MaxWeapons];
    private readonly float[] levelPop = new float[PlayerWeapons.MaxWeapons];
    private float ultPing;
    private TextMeshProUGUI hpText, levelText, timerText, killText, ultText, scoreText;
    private Slot[] weaponSlots, skillSlots;

    private float runTime;
    public float RunTime => runTime;
    public void Advance(float seconds) => runTime += seconds; // admin tool
    private int skillsSignature = -1; // only rebuild slots when something changed
    private int weaponsSignature = -1;
    private PlayerUltimate ult;
    private PlayerSkills skills;
    private PlayerWeapons weapons;
    private PlayerCollabs collabs;

    // pieces that are part of a combination ready right now: their slots glow gold
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

    // ---------- layout ----------

    private void Build()
    {
        var root = (RectTransform)transform;

        // player panel
        RectTransform panel = Place(RuntimeUI.Frame(root, "PlayerPanel", Vector2.zero, Vector2.zero).rectTransform, new Vector2(0, 1), new Vector2(Margin, -Margin), new Vector2(500, 124));
        Slot portrait = MakeSlot(panel, new Vector2(12, -12), 100f);
        portrait.icon.sprite = stats != null && stats.TryGetComponent(out PlayerAnimator anim) && anim.spriteRenderer != null ? anim.spriteRenderer.sprite : null;
        portrait.icon.enabled = portrait.icon.sprite != null;
        // zoom in on the head and shoulders (the sprite cell has lots of empty space), clipped to the frame
        portrait.frame.gameObject.AddComponent<RectMask2D>().padding = Vector4.one * 6f;
        Place(portrait.icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 34f), new Vector2(230f, 230f));
        Label(panel, "MIRA", 26, Color.white, new Vector2(124, -10), new Vector2(200, 30), TextAlignmentOptions.TopLeft);
        levelText = Label(panel, "LV 1", 26, new Color(1f, 0.85f, 0.35f), new Vector2(124, -10), new Vector2(364, 30), TextAlignmentOptions.TopRight);

        (hpFill, hpFillImage) = Bar(panel, new Vector2(124, -44), new Vector2(364, 32), new Color(0.2f, 0.04f, 0.07f));
        hpChunk = RuntimeUI.Box(hpFill.parent, "Chunk", Vector2.zero, Vector2.one, new Color(1f, 0.92f, 0.85f)).rectTransform;
        hpChunk.SetSiblingIndex(0); // under the fill: shows the HP just lost, then drains
        hpText = Label(hpFill.parent, "100 / 100", 22, Color.white, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, stretch: true);
        (xpFill, xpFillImage) = Bar(panel, new Vector2(124, -84), new Vector2(364, 26), new Color(0.04f, 0.07f, 0.2f));
        xpFillImage.color = XpColor;
        playerPanel = panel;
        panelPos = panel.anchoredPosition;

        // weapons (+ Cat Buddy in the slot after them), skills, special
        float row1 = -Margin - 124 - 12, row2 = row1 - SlotSize - SlotGap, row3 = row2 - SlotSize - SlotGap;
        weaponSlots = new Slot[PlayerWeapons.MaxWeapons + 1];
        for (int i = 0; i < weaponSlots.Length; i++) weaponSlots[i] = MakeSlot(root, new Vector2(Margin + i * (SlotSize + SlotGap), row1), SlotSize);
        skillSlots = new Slot[PlayerSkills.MaxSkills];
        for (int i = 0; i < skillSlots.Length; i++) skillSlots[i] = MakeSlot(root, new Vector2(Margin + i * (SlotSize + SlotGap), row2), SlotSize);

        Slot special = MakeSlot(root, new Vector2(Margin, row3), SlotSize);
        specialFrame = special.frame;
        ultFill = special.icon;
        ultFill.sprite = ExplosionFx.GetCircle();
        ultFill.type = Image.Type.Filled;
        ultFill.fillMethod = Image.FillMethod.Radial360;
        ultFill.fillOrigin = (int)Image.Origin360.Top;
        ultFill.enabled = true;
        special.badge.gameObject.SetActive(true);
        special.badge.text = "F";
        special.badge.color = Color.white;
        ultText = Label(root, "", 24, Color.white, new Vector2(Margin + SlotSize + 12, row3 - 6), new Vector2(360, 56), TextAlignmentOptions.TopLeft);

        // timer + kills
        timerText = Label(root, "00:00", 60, Color.white, Vector2.zero, new Vector2(320, 70), TextAlignmentOptions.Center, anchor: new Vector2(0.5f, 1f));
        ((RectTransform)timerText.transform).anchoredPosition = new Vector2(0, -16);
        timerText.outlineWidth = 0.28f;
        RectTransform killBox = Place(RuntimeUI.Frame(root, "Kills", Vector2.zero, Vector2.zero).rectTransform, new Vector2(1, 1), new Vector2(-Margin, -Margin), new Vector2(300, 56));
        killText = Label(killBox, "DEFEATED  0", 24, Color.white, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, stretch: true);
        scoreText = Label(root, "SCORE  0", 24, Color.white, Vector2.zero, new Vector2(320, 30), TextAlignmentOptions.Center, anchor: new Vector2(0.5f, 1f));
        ((RectTransform)scoreText.transform).anchoredPosition = new Vector2(0, -84);
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

    // framed bar: returns the fill (width driven by anchorMax.x) and its image
    private static (RectTransform, Image) Bar(RectTransform parent, Vector2 pos, Vector2 size, Color back)
    {
        Image frame = RuntimeUI.Box(parent, "Bar", Vector2.zero, Vector2.zero, Color.white);
        UiIcons.Frame(frame, UiIcons.Slot);
        Place(frame.rectTransform, new Vector2(0, 1), pos, size);
        Image bg = RuntimeUI.Box(frame.transform, "Back", Vector2.zero, Vector2.one, back);
        bg.rectTransform.offsetMin = new Vector2(4, 4);
        bg.rectTransform.offsetMax = new Vector2(-4, -4);
        Image fill = RuntimeUI.Box(bg.transform, "Fill", Vector2.zero, Vector2.one, Color.white);
        return (fill.rectTransform, fill);
    }

    internal static Slot MakeSlot(RectTransform parent, Vector2 pos, float size)
    {
        var s = new Slot();
        s.frame = RuntimeUI.Box(parent, "Slot", Vector2.zero, Vector2.zero, Color.white);
        UiIcons.Frame(s.frame, UiIcons.Slot);
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
        s.frame.sprite = gold ? UiIcons.BoxGold : UiIcons.Slot;
        s.frame.color = Color.white;
    }

    internal static void Clear(Slot s)
    {
        s.icon.enabled = false;
        s.badge.gameObject.SetActive(false);
        s.level.text = "";
        s.frame.sprite = UiIcons.Slot;
        s.frame.color = new Color(1f, 1f, 1f, 0.45f); // empty slots stay faint
    }

    private static string LevelTag(int level, int max) => level >= max ? "<color=#FFD24A>MAX</color>" : level.ToString();

    // ---------- per frame ----------

    private void Update()
    {
        if (stats == null || hpFill == null) return;

        if (stats.currentHealth > 0f) runTime += Time.deltaTime; // stops on death and during level-up pause

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

        UpdateUltimate();
        RefreshCombinable();
        UpdateSkills();
        UpdateWeapons();
        GlowCombinable();
        PulseHud();
    }

    // Kill counter bounces when it ticks; weapon icons pulse when they fire and bounce when they level up.
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
        int signature = (buddy != null ? buddy.Level : 0) + combinableSignature;
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
            if (super) weaponSlots[i].frame.color = new Color(1f, 0.6f, 0.95f); // super combine: pink-gold frame
        }

        Slot buddySlot = weaponSlots[PlayerWeapons.MaxWeapons];
        buddySlot.frame.gameObject.SetActive(buddy != null);
        if (buddy != null) Fill(buddySlot, "buddy", LevelTag(buddy.Level, Companion.MaxLevel), gold: true);
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

    // HoloCure-style damage feedback: the lost HP stays as a pale chunk for a moment, then drains;
    // the panel shakes on each hit. Unscaled time so it still plays during hit-stop.
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

    // XP fills smoothly and brightens when it grows; on level-up it empties and starts over.
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

        // ping once when the special finishes charging; the frame glows gold while it's ready
        bool ready = ult.IsReady && !ult.IsActive;
        if (ready && !ultWasReady)
        {
            Sfx.Play(Sfx.Id.Ready, 0.6f);
            DamagePopup.SpawnText((Vector2)stats.transform.position + Vector2.up * 1f, "SPECIAL READY!", new Color(1f, 0.85f, 0.35f), 5f);
            ultPing = 1f;
        }
        ultWasReady = ready;
        ultPing = Mathf.Max(0f, ultPing - Time.unscaledDeltaTime * 2.5f);
        specialFrame.sprite = ready ? UiIcons.BoxGold : UiIcons.Slot;
        specialFrame.rectTransform.localScale = Vector3.one * (1f + 0.35f * ultPing + (ready ? 0.05f * Mathf.Sin(Time.unscaledTime * 6f) : 0f));

        if (ult.IsActive)
        {
            ultFill.fillAmount = ult.ActiveRemaining / ult.duration; // drains while active
            ultFill.color = new Color(1f, 0.55f, 0.15f);
            ultText.text = $"SPECIAL\n<color=#FFB040>ACTIVE {Mathf.CeilToInt(ult.ActiveRemaining)}s</color>";
        }
        else if (ult.IsReady)
        {
            ultFill.fillAmount = 1f;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
            ultFill.color = new Color(1f, 0.82f, 0.3f) * pulse + new Color(0, 0, 0, 1f - pulse);
            ultText.text = "SPECIAL\n<color=#FFE08A>READY!</color>";
        }
        else
        {
            ultFill.fillAmount = 1f - ult.CooldownRemaining / ult.cooldown; // refills while charging
            ultFill.color = new Color(0.45f, 0.5f, 0.65f);
            ultText.text = $"SPECIAL\n<color=#9AA3C0>{Mathf.CeilToInt(ult.CooldownRemaining)}s</color>";
        }
    }

    private void RefreshCombinable()
    {
        if (collabs == null || Time.unscaledTime < nextCombineCheck) return;
        nextCombineCheck = Time.unscaledTime + 0.25f; // Offerable() builds a list; a few checks a second is plenty

        combinable.Clear();
        foreach (PlayerCollabs.Def d in collabs.Offerable())
        {
            if (d.kind != PlayerCollabs.Kind.Bomb) combinable.Add(d.a);
            combinable.Add(d.b);
        }
        int signature = combinable.Count;
        foreach (string id in combinable) signature += id.GetHashCode(); // order-independent
        combinableSignature = signature; // folded into the slot signatures, so slots refresh when it changes
    }

    // Pulse the frame of every slot holding a combinable piece (other slots keep what the rebuild set).
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
            slots[i].frame.sprite = UiIcons.BoxGold;
            slots[i].frame.color = Color.Lerp(Color.white, ComboGlow, pulse);
        }
    }
}
