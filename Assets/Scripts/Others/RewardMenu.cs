using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RewardMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    private static RewardMenu instance;
    private static RewardMenu I => instance != null ? instance : instance = new GameObject("RewardMenu").AddComponent<RewardMenu>();

    private Canvas canvas;
    private GameObject panel;
    private float timeScaleBefore = 1f;
    private PlayerStats stats;
    private PlayerWeapons weapons;
    private PlayerSkills skills;

    private void Awake()
    {
        IsOpen = false;
        canvas = RuntimeUI.Canvas("RewardCanvas", 160);
        canvas.transform.SetParent(transform, false);
        stats = FindFirstObjectByType<PlayerStats>();
        if (stats != null)
        {
            weapons = stats.GetComponent<PlayerWeapons>();
            skills = stats.GetComponent<PlayerSkills>();
        }
    }

    public static int ChestsOpened;

    public static void OpenHoloBox()
    {
        if (!I.Open()) return;
        Sfx.Play(Sfx.Id.Chest, 0.9f);
        ChestsOpened++;
        I.StartCoroutine(I.HoloBox());
    }

    private int anvilUses;
    private bool anvilSecondPick;

    public static void OpenAnvil()
    {
        if (!I.Open()) return;
        I.anvilUses = PlayerSkills.Instance != null && PlayerSkills.Instance.LevelOf("blacksmith") >= 3 ? 2 : 1;
        I.anvilSecondPick = false;
        I.BuildAnvil();
        Sfx.Play(Sfx.Id.Clang, 0.5f, 0.8f);
    }

    private bool Open()
    {
        if (IsOpen || stats == null) return false;
        IsOpen = true;
        Juice.CancelHitStop();
        timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        return true;
    }

    private void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        StopAllCoroutines();
        if (panel != null) Destroy(panel);
        Time.timeScale = timeScaleBefore;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private RectTransform MakePanel(string title, Vector2 size, Color titleColor)
    {
        Image dim = RuntimeUI.Box(canvas.transform, "RewardMenu", Vector2.zero, Vector2.one, new Color(0.03f, 0.02f, 0.08f, 0.75f));
        panel = dim.gameObject;
        RectTransform box = RuntimeUI.Frame(dim.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)).rectTransform;
        box.sizeDelta = size;
        RuntimeUI.Title(box, title, 44f, new Vector2(0, size.y / 2f - 40), size.x);
        return box;
    }

    private class Reel
    {
        public Image icon;
        public TextMeshProUGUI badge, caption;
    }

    private IEnumerator HoloBox()
    {
        RectTransform box = MakePanel("HOLO BOX!", new Vector2(620, 420), new Color(0.55f, 0.85f, 1f));

        var reels = new Reel[3];
        for (int i = 0; i < 3; i++)
        {
            Image frame = RuntimeUI.Box(box, "Reel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.white);
            UiIcons.Frame(frame, UiIcons.Slot);
            frame.rectTransform.anchoredPosition = new Vector2((i - 1) * 170, 30);
            frame.rectTransform.sizeDelta = new Vector2(124, 124);
            var r = new Reel();
            r.icon = RuntimeUI.Box(frame.transform, "Icon", Vector2.zero, Vector2.one, Color.white);
            r.icon.rectTransform.offsetMin = Vector2.one * 16;
            r.icon.rectTransform.offsetMax = -Vector2.one * 16;
            r.icon.preserveAspect = true;
            r.badge = RuntimeUI.Outline(RuntimeUI.Text(frame.transform, "", 40, Color.white, Vector2.zero, new Vector2(124, 124)));
            r.caption = RuntimeUI.Outline(RuntimeUI.Text(box, "", 20, Color.white, new Vector2((i - 1) * 170, -78), new Vector2(170, 56)));
            reels[i] = r;
        }

        float roll = Random.value;
        int prizes = roll < 0.6f ? 1 : roll < 0.9f ? 2 : 3;
        var faces = new List<string>();
        foreach (PlayerWeapons.Def d in PlayerWeapons.All) faces.Add(d.id);
        foreach (PlayerSkills.Skill s in PlayerSkills.All) faces.Add("skill:" + s.id);

        float[] stopAt = { 0.9f, 1.2f, 1.5f };
        bool[] stopped = new bool[3];
        float t = 0f, nextFace = 0f;
        while (!stopped[2])
        {
            t += Time.unscaledDeltaTime;
            bool flip = t >= nextFace;
            if (flip) nextFace = t + 0.07f;
            for (int i = 0; i < 3; i++)
            {
                if (stopped[i]) continue;
                if (t >= stopAt[i])
                {
                    stopped[i] = true;
                    if (i < prizes) GivePrize(reels[i]);
                    else { reels[i].icon.enabled = false; reels[i].badge.text = "-"; reels[i].badge.color = new Color(0.5f, 0.55f, 0.7f); }
                    continue;
                }
                if (flip) UiIcons.Apply(faces[Random.Range(0, faces.Count)], reels[i].icon, reels[i].badge);
                if (flip && i == 0) Sfx.Play(Sfx.Id.Tick, 0.6f);
            }
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.3f);
        Button ok = RuntimeUI.Button(box, "Continue", new Vector2(0, -164), new Vector2(220, 52), Close);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(ok.gameObject);
    }

    private (string, string) FullHeal()
    {
        stats.Heal(stats.maxHealth);
        return ("stat:Heal", "<color=#7CFF9A>Full heal</color>");
    }

    private void GivePrize(Reel reel)
    {
        var options = new List<System.Func<(string icon, string caption)>>();
        string Lv(int level) => $"<color=#FFD24A>Lv {level}</color>";
        if (weapons != null)
            foreach (string id in weapons.OwnedInOrder)
            {
                if (PlayerWeapons.IsCollab(id) || weapons.LevelOf(id) >= Weapon.MaxLevel) continue;
                string captured = id;
                options.Add(() => { weapons.LevelUp(PlayerWeapons.Find(captured)); return (captured, $"{PlayerWeapons.Find(captured).name}\n{Lv(weapons.LevelOf(captured))}"); });
            }
        if (skills != null)
            foreach (string id in skills.OwnedInOrder)
            {
                PlayerSkills.Skill s = PlayerSkills.Find(id);
                if (skills.LevelOf(id) >= s.levels.Length || (id == "secondwind" && skills.ReviveUsed)) continue;
                options.Add(() => { skills.LevelUp(s); return ("skill:" + s.id, $"{s.name}\n{Lv(skills.LevelOf(s.id))}"); });
            }
        MiraPassives mira = MiraPassives.Instance;
        if (mira != null)
            foreach (MiraPassives.Passive p in mira.Offerable())
                options.Add(() => { mira.LevelUp(p.id); return ("passive:" + p.id, $"{p.name}\n{Lv(mira.LevelOf(p.id))}"); });
        LevelUpUIController levelUp = stats.GetComponent<LevelUpUIController>();
        if (levelUp != null)
            for (int i = 0; i < 2; i++)
                options.Add(() => levelUp.GiveRandomStat(out string icon, out string title)
                    ? (icon, $"{title}\n<color=#7CFF9A>Stat up!</color>")
                    : FullHeal());

        (string icon, string caption) prize = options.Count == 0 ? FullHeal() : options[Random.Range(0, options.Count)]();
        UiIcons.Apply(prize.icon, reel.icon, reel.badge);
        Sfx.Play(Sfx.Id.Ready, 0.7f);
        reel.caption.text = prize.caption;
        Juice.Flash(new Color(0.6f, 0.9f, 1f, 0.35f), 0.25f);
    }

    private void BuildAnvil()
    {
        IReadOnlyList<string> owned = weapons != null ? weapons.OwnedInOrder : new List<string>();
        float rowHeight = 84f;
        float height = 200f + Mathf.Max(1, owned.Count) * rowHeight;
        RectTransform box = MakePanel(anvilSecondPick ? "ANVIL - SECOND PICK" : "ANVIL", new Vector2(860, height), new Color(0.85f, 0.88f, 0.95f));
        float top = height / 2f;
        RuntimeUI.Text(box, "Level up a weapon, or enhance a maxed one (up to +5).", 20, new Color(0.8f, 0.84f, 0.95f),
                       new Vector2(0, top - 88), new Vector2(800, 30));

        Button first = null;
        if (owned.Count == 0)
            RuntimeUI.Text(box, "No weapons yet.", 24, new Color(0.6f, 0.65f, 0.8f), new Vector2(0, top - 150), new Vector2(600, 40));

        for (int i = 0; i < owned.Count; i++)
        {
            string id = owned[i];
            Weapon weapon = weapons.Get(id);
            float y = top - 150f - i * rowHeight;

            Image slot = RuntimeUI.Box(box, "Slot", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Color.white);
            UiIcons.Frame(slot, UiIcons.Slot);
            slot.rectTransform.anchoredPosition = new Vector2(-370, y);
            slot.rectTransform.sizeDelta = new Vector2(70, 70);
            Image icon = RuntimeUI.Box(slot.transform, "Icon", Vector2.zero, Vector2.one, Color.white);
            icon.rectTransform.offsetMin = Vector2.one * 10;
            icon.rectTransform.offsetMax = -Vector2.one * 10;
            icon.preserveAspect = true;
            TextMeshProUGUI badge = RuntimeUI.Text(slot.transform, "", 24, Color.white, Vector2.zero, new Vector2(70, 70));
            UiIcons.Apply(id, icon, badge);

            bool maxed = weapon.Level >= Weapon.MaxLevel;
            string level = !maxed ? $"Lv {weapon.Level}" : weapon.Enhance > 0 ? $"MAX +{weapon.Enhance}" : "MAX";
            RuntimeUI.Outline(RuntimeUI.Text(box, $"{PlayerWeapons.Find(id).name}\n<color=#FFD24A>{level}</color>", 22, Color.white,
                                             new Vector2(-180, y), new Vector2(280, rowHeight - 8), TextAlignmentOptions.MidlineLeft));

            Button b = null;
            if (!maxed)
            {
                b = RuntimeUI.Button(box, "Level Up", new Vector2(230, y), new Vector2(300, 52), () => { weapons.LevelUp(PlayerWeapons.Find(id)); Done(id); });
            }
            else if (weapon.Enhance < Weapon.MaxEnhance)
            {
                b = RuntimeUI.Button(box, "+DMG", new Vector2(120, y), new Vector2(120, 52), () => { weapon.AddEnhance(Weapon.Boost.Damage); Done(id); });
                RuntimeUI.Button(box, "+AREA", new Vector2(240, y), new Vector2(120, 52), () => { weapon.AddEnhance(Weapon.Boost.Area); Done(id); });
                RuntimeUI.Button(box, "+SPD", new Vector2(360, y), new Vector2(120, 52), () => { weapon.AddEnhance(Weapon.Boost.Speed); Done(id); });
            }
            else
            {
                RuntimeUI.Text(box, "<color=#8890A8>Fully enhanced</color>", 22, Color.white, new Vector2(240, y), new Vector2(360, 40));
            }
            if (first == null) first = b;
        }

        Button notNow = RuntimeUI.Button(box, anvilSecondPick ? "Done" : "Not now", new Vector2(0, -height / 2f + 40f), new Vector2(200, 48), () =>
        {
            Close();
            if (!anvilSecondPick) Pickup.Spawn(Pickup.Kind.Anvil, (Vector2)stats.transform.position + Vector2.right * 2f);
        });
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject((first != null ? first : notNow).gameObject);
    }

    private void Done(string id)
    {
        Close();
        Sfx.Play(Sfx.Id.Clang, 0.9f);
        DamagePopup.SpawnText((Vector2)stats.transform.position + Vector2.up * 0.8f, $"{PlayerWeapons.Find(id).name.ToUpper()} UP!", new Color(0.85f, 0.88f, 0.95f), 5f);
        if (--anvilUses > 0 && Open()) { anvilSecondPick = true; BuildAnvil(); }
    }
}
