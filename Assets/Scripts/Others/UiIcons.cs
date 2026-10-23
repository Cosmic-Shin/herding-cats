using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class UiIcons
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Sprite Box => Load("UI/box");
    public static Sprite BoxGold => Load("UI/box_gold");
    public static Sprite Slot => Load("UI/slot");

    private static readonly Dictionary<string, (string text, Color color)> Badges = new Dictionary<string, (string, Color)>
    {
        { "skill:blink", ("BL", new Color(0.5f, 0.85f, 1f)) },
        { "skill:chain", ("CH", new Color(1f, 0.6f, 0.2f)) },
        { "skill:cluster", ("CL", new Color(1f, 0.75f, 0.3f)) },
        { "skill:napalm", ("NP", new Color(1f, 0.4f, 0.2f)) },
        { "skill:sharpshooter", ("SS", new Color(1f, 0.9f, 0.3f)) },
        { "skill:berserk", ("BK", new Color(1f, 0.3f, 0.3f)) },
        { "skill:thickskin", ("TS", new Color(0.8f, 0.83f, 0.92f)) },
        { "skill:regen", ("RG", new Color(0.45f, 0.95f, 0.45f)) },
        { "skill:dodge", ("DG", new Color(0.7f, 0.92f, 1f)) },
        { "skill:shockwave", ("SW", new Color(0.4f, 0.65f, 1f)) },
        { "skill:secondwind", ("2W", new Color(0.55f, 1f, 0.7f)) },
        { "skill:magnet", ("MG", new Color(1f, 0.5f, 0.8f)) },
        { "skill:scholar", ("SC", new Color(0.75f, 0.55f, 1f)) },
        { "skill:gourmet", ("GM", new Color(1f, 0.7f, 0.45f)) },
        { "skill:overdrive", ("OD", new Color(1f, 0.85f, 0.25f)) },
        { "skill:blacksmith", ("BS", new Color(0.85f, 0.88f, 0.95f)) },
        { "passive:blade", ("BD", new Color(1f, 0.55f, 0.75f)) },
        { "passive:weakspot", ("WS", new Color(1f, 0.85f, 0.35f)) },
        { "passive:shield", ("SH", new Color(0.55f, 0.85f, 1f)) },
        { "stat:AttackDamagePercent", ("ATK", new Color(1f, 0.4f, 0.35f)) },
        { "stat:MaxHealth", ("HP", new Color(0.45f, 0.95f, 0.45f)) },
        { "stat:AttackSpeedPercent", ("HST", new Color(1f, 0.85f, 0.3f)) },
        { "stat:MoveSpeedPercent", ("SPD", new Color(0.5f, 0.85f, 1f)) },
        { "stat:Heal", ("+", new Color(0.45f, 0.95f, 0.45f)) },
        { "stat:AreaPercent", ("AOE", new Color(1f, 0.6f, 0.25f)) },
        { "stat:CritChance", ("CRT", new Color(1f, 0.9f, 0.3f)) },
    };

    public static void Apply(string id, Image icon, TextMeshProUGUI badge)
    {
        Sprite sprite = Icon(id);
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        bool hasBadge = sprite == null && id != null && Badges.TryGetValue(id, out _);
        badge.gameObject.SetActive(hasBadge);
        if (hasBadge)
        {
            (string text, Color color) = Badges[id];
            badge.text = text;
            badge.color = color;
        }
    }

    public static Sprite Icon(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        // Kai: "+1 swing" shows his sword, and the bomb skills use their sword-version icons.
        if (!Hero.UsesBombs && id == "stat:ProjectileCount") return Load("Icons/" + Hero.Current.weapon);
        if (Hero.Current.iconPrefix != null && id.StartsWith("skill:") && Load("Icons/" + Hero.Current.iconPrefix + id.Substring(6)) is Sprite heroSkill) return heroSkill;
        if (id.StartsWith("passive:")) return Load("Icons/passive_" + id.Substring(8));
        if (id == "stat:MoveSpeedPercent") return Load("Icons/speed");
        if (id.StartsWith("stat:") && id != "stat:ProjectileCount") return Load("Icons/stat_" + id.Substring(5));
        if (id.StartsWith("skill:")) return Load("Icons/skill_" + id.Substring(6)) ?? Load("Icons/" + id.Substring(6));
        if (id == "stat:ProjectileCount") id = "bomb";
        PlayerCollabs.Def collab = PlayerCollabs.Find(id);
        if (collab != null && (collab.kind == PlayerCollabs.Kind.Merge || collab.kind == PlayerCollabs.Kind.Super) && Load("Icons/" + id) is Sprite own) return own;
        if (collab != null && collab.kind == PlayerCollabs.Kind.Super) return Load("Icons/" + collab.icon);
        if (collab != null && collab.kind == PlayerCollabs.Kind.Merge) id = collab.a;
        return Load("Icons/" + id);
    }

    private static Sprite Load(string path)
    {
        if (!cache.TryGetValue(path, out Sprite s) || s == null) cache[path] = s = Resources.Load<Sprite>(path);
        return s;
    }

    public static void Frame(Image image, Sprite sprite)
    {
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f / 3f;
        image.color = Color.white;
    }
}
