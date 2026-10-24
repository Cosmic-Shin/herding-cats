using System.Collections.Generic;
using UnityEngine;

// Playable heroes. All share the same rig (walk/roll/slash sheets in Resources/Heroes/<id>),
// so switching is a sprite swap by frame name plus turning on that hero's weapon and passives.
// Only Mira throws bombs; the others get their own versions of the bomb-flavoured upgrades (text here,
// behaviour in KaiSword / NyxDrones).
public static class Hero
{
    public class Def
    {
        public string id, name, blurb;
        public string kit;               // the run-select screen: weapon, passives, special
        public string weapon = "bomb";   // icon of the main weapon
        public string iconPrefix;        // their skill icons, e.g. "kai_" -> Icons/kai_cluster
        public string maxPiece = "Max Bombs", upgradeKind = "Bomb upgrade";
        public string[] stats = { "Bomb damage", "Throws/sec", "Bombs/throw", "Blast radius" };
        public float portraitDrop;       // HUD portrait: art pixels to shift the crop down so the face shows (big hair)
        public Dictionary<string, (string title, string text)> upgrades; // by upgrade type
        public Dictionary<string, (string name, string[] levels)> skills;
        public Dictionary<string, (string name, string text)> combos;    // combos and supers
    }

    public static readonly Def[] All =
    {
        new Def
        {
            id = "Mira", name = "Mira", blurb = "Blade dancer: slashes close cats, and a bubble shield blocks a hit.",
            kit = Kit("Bombs: lobs bombs that stick to cats and blow up the crowd around them.",
                      "Blade Dancer: slashes any cat that gets too close", "Weak Spot: extra-strong crits on slowed or stunned cats", "Bubble Shield: blocks a whole hit, then recharges",
                      "SPECIAL: a burst of bombs all around her, then bigger, stronger bombs"),
        },
        new Def
        {
            id = "Kai", name = "Kai", blurb = "Swordsman: a huge slab of a sword that stuns, executes, and cuts through dashes.",
            kit = Kit("Greatsword: slow, huge wide cleaves that shove cats back. Strong up close.",
                      "Heavy Blade: every hit stuns", "Execute: cats left under 15% HP die on the spot", "Dash Slash: dashing cuts through every cat in the way",
                      "RAGE: a full spin, then faster, bigger swings that heal him on every hit"),
            weapon = "sword", iconPrefix = "kai_", maxPiece = "Max Swings", upgradeKind = "Sword upgrade",
            portraitDrop = 5f,
            stats = new[] { "Damage", "Attack speed", "Swings/attack", "Sword reach" },
            upgrades = new Dictionary<string, (string, string)>
            {
                { "AttackDamagePercent", ("Attack Up", "+15% sword damage") },
                { "AttackSpeedPercent", ("Haste", "Swing 20% faster") },
                { "ProjectileCount", ("Extra Swing", "+1 swing per attack") },
                { "AreaPercent", ("Big Blade", "+15% sword reach") },
                { "CritChance", ("Sharp Edge", "+5% crit chance (crits deal 1.5x)") },
            },
            skills = new Dictionary<string, (string, string[])>
            {
                { "blink", ("Blink", new[] {
                    "Dash becomes a teleport to a safe spot nearby (4s cooldown)",
                    "Blink cooldown 4s -> 3s, 0.6s invulnerability after landing",
                    "Leave a spinning sword slash where you blinked from (200% damage)" }) },
                { "chain", ("Echo Slash", new[] {
                    "Swings have a 15% chance to slash again (50% damage)",
                    "25% chance, the echo deals 60% damage",
                    "35% chance, the echo deals 75% damage" }) },
                { "cluster", ("Shrapnel", new[] {
                    "Swings fling 2 steel shards at nearby cats (25% damage)",
                    "3 shards (30% damage)",
                    "4 shards (35% damage)" }) },
                { "napalm", ("Scorched Arc", new[] {
                    "Swings leave fire along the arc for 2s (8% damage every 0.5s)",
                    "Fire lasts 2.5s, 12% damage per tick",
                    "Fire lasts 3s, 16% damage per tick" }) },
            },
            combos = new Dictionary<string, (string, string)>
            {
                { "concussion", ("Concussive Edge", "Every swing stuns what it hits for 0.6s and knocks it much further") },
                { "magnetbomb", ("Undertow", "Before each swing, nearby cats get dragged into the blade's path") },
                { "furyfuse", ("Fury Blade", "The lower your HP, the faster you swing (up to 2x) and the longer your reach (up to +50%)") },
                { "carpet", ("Blade Rain", "Every attack also drops a line of 5 phantom swords ahead of you") },
                { "firebomb", ("Burning Edge", "Scorched Arc fires are bigger, longer and hotter. Burning cats explode") },
                { "hellfire", ("Spicy Slash", "Every swing calls red lightning and leaves a ring of fire") },
                { "earthshaker", ("Catquake", "Every swing cracks the ground 3 ways, stunning cats on the cracks") },
                { "armageddon", ("Catastrophe", "Every attack marks 5 spots for falling meteors") },
            },
        },
        new Def
        {
            id = "Nyx", name = "Nyx", blurb = "Hacker: drones that hack cats, mark targets and run exploits.",
            kit = Kit("Drones: a swarm of zapper drones darts out at cats. She attacks from range.",
                      "Hack: zapped cats sometimes freeze and zap their friends", "Mark Target: the toughest cat nearby takes +50% damage", "Backdoor: every 12th zap is a big guaranteed crit",
                      "SYSTEM OVERRIDE: an EMP that hacks every cat on screen, then faster drones"),
            weapon = "drone", iconPrefix = "nyx_", maxPiece = "Max Drones", upgradeKind = "Drone upgrade",
            stats = new[] { "Damage", "Fire rate", "Drones", "Zap radius" },
            upgrades = new Dictionary<string, (string, string)>
            {
                { "AttackDamagePercent", ("Attack Up", "+15% drone damage") },
                { "AttackSpeedPercent", ("Overclock", "Drones fire 20% faster") },
                { "ProjectileCount", ("Extra Drones", "+2 drones") },
                { "AreaPercent", ("Wide Zap", "+15% zap radius") },
                { "CritChance", ("Lucky Byte", "+5% crit chance (crits deal 1.5x)") },
            },
            skills = new Dictionary<string, (string, string[])>
            {
                { "blink", ("Blink", new[] {
                    "Dash becomes a teleport to a safe spot nearby (4s cooldown)",
                    "Blink cooldown 4s -> 3s, 0.6s invulnerability after landing",
                    "Leave an EMP blast where you blinked from (200% damage, stuns)" }) },
                { "chain", ("Chain Zap", new[] {
                    "Zaps have a 15% chance to arc to another cat (50% damage)",
                    "25% chance, the arc deals 60% damage",
                    "35% chance, the arc deals 75% damage" }) },
                { "cluster", ("Packet Split", new[] {
                    "Zaps split into 2 data packets that hit nearby cats (25% damage)",
                    "3 packets (30% damage)",
                    "4 packets (35% damage)" }) },
                { "napalm", ("Static Field", new[] {
                    "Zaps leave crackling static for 2s (8% damage every 0.5s)",
                    "Static lasts 2.5s, 12% damage per tick",
                    "Static lasts 3s, 16% damage per tick" }) },
            },
            combos = new Dictionary<string, (string, string)>
            {
                { "concussion", ("Stun Gun", "Every zap stuns what it hits for 0.6s") },
                { "magnetbomb", ("Tractor Drones", "Drones drag nearby cats onto their target before zapping") },
                { "furyfuse", ("Overdrive Mode", "The lower your HP, the faster your drones fire (up to 2x) and the bigger the zaps (up to +50%)") },
                { "carpet", ("Drone Strike", "Every volley also calls a line of 5 zaps from the sky ahead of you") },
                { "firebomb", ("Overload", "Static fields are bigger, longer and hotter. Shocked cats explode") },
                { "hellfire", ("Spicy Zap", "Every zap calls red lightning and leaves a ring of fire") },
                { "earthshaker", ("Catquake", "Every zap cracks the ground 3 ways, stunning cats on the cracks") },
                { "armageddon", ("Catastrophe", "Every volley marks 5 spots for falling meteors") },
            },
        },
    };

    private static string Kit(string weapon, string p1, string p2, string p3, string special)
    {
        int colon = special.IndexOf(':');
        return $"<color=#FFB36B>WEAPON</color>  {weapon}\n\n<color=#7FE3FF>PASSIVES</color>\n- {p1}\n- {p2}\n- {p3}\n\n" +
               $"<color=#FFD24A>{special.Substring(0, colon)}</color>  {special.Substring(colon + 2)}";
    }

    public static int Index => Mathf.Clamp(PlayerPrefs.GetInt("hero", 0), 0, All.Length - 1);
    public static Def Current => All[Index];
    public static bool UsesBombs => Current.id == "Mira";

    public static (string title, string text)? Upgrade(string type) =>
        Current.upgrades != null && Current.upgrades.TryGetValue(type, out var u) ? u : ((string, string)?)null;

    // Size stat for the build sheet: blast radius, sword reach or zap radius.
    public static float Range(PlayerStats stats) =>
        KaiSword.Active != null ? KaiSword.Active.Reach : NyxDrones.Active != null ? NyxDrones.Active.ZapRadius : stats.BombRadius;

    private static Dictionary<string, (string, string[])> bombSkills;
    private static Dictionary<string, (string, string)> bombCombos;

    // Skill and combo defs are shared statics, so swap their text to the current hero's versions.
    private static void ApplyTexts()
    {
        if (bombSkills == null)
        {
            bombSkills = new Dictionary<string, (string, string[])>();
            bombCombos = new Dictionary<string, (string, string)>();
            foreach (PlayerSkills.Skill s in PlayerSkills.All) bombSkills[s.id] = (s.name, s.levels);
            foreach (PlayerCollabs.Def[] defs in new[] { PlayerCollabs.All, PlayerCollabs.Supers })
                foreach (PlayerCollabs.Def d in defs) bombCombos[d.id] = (d.name, d.text);
        }
        Def h = Current;
        foreach (PlayerSkills.Skill s in PlayerSkills.All)
            (s.name, s.levels) = h.skills != null && h.skills.TryGetValue(s.id, out var mine) ? mine : bombSkills[s.id];
        foreach (PlayerCollabs.Def[] defs in new[] { PlayerCollabs.All, PlayerCollabs.Supers })
            foreach (PlayerCollabs.Def d in defs)
                (d.name, d.text) = h.combos != null && h.combos.TryGetValue(d.id, out var mine) ? mine : bombCombos[d.id];
        foreach (PlayerWeapons.Def w in PlayerWeapons.CollabDefs)
            if (PlayerCollabs.Find(w.id) is PlayerCollabs.Def combo) w.name = combo.name; // weapon names match their combo
    }

    public static void Cycle(int step)
    {
        PlayerPrefs.SetInt("hero", ((Index + step) % All.Length + All.Length) % All.Length);
        PlayerPrefs.Save();
        PlayerStats player = Object.FindFirstObjectByType<PlayerStats>();
        if (player != null) Apply(player);
    }

    private static void Toggle<T>(PlayerStats player, bool on) where T : Behaviour
    {
        T c = player.GetComponent<T>();
        if (c == null && on) c = player.gameObject.AddComponent<T>();
        if (c != null) c.enabled = on;
    }

    public static void Apply(PlayerStats player)
    {
        string id = Current.id;
        if (player.TryGetComponent(out MiraPassives mira)) mira.enabled = id == "Mira";
        Toggle<KaiPassives>(player, id == "Kai");
        Toggle<NyxPassives>(player, id == "Nyx");
        if (player.TryGetComponent(out PlayerAutoAttack bombs)) bombs.enabled = id == "Mira";
        Toggle<KaiSword>(player, id == "Kai");
        Toggle<NyxDrones>(player, id == "Nyx");
        ApplyTexts();

        if (!player.TryGetComponent(out PlayerAnimator anim)) return;
        var frames = new Dictionary<string, Sprite>();
        foreach (Sprite s in Resources.LoadAll<Sprite>("Heroes/" + id)) frames[Suffix(s.name)] = s;
        foreach (PlayerAnimator.Clip[] clips in new[] { anim.walk, anim.roll, anim.throwClips })
            foreach (PlayerAnimator.Clip clip in clips)
                for (int i = 0; i < clip.frames.Length; i++)
                    if (clip.frames[i] != null && frames.TryGetValue(Suffix(clip.frames[i].name), out Sprite swap)) clip.frames[i] = swap;
        if (anim.spriteRenderer != null && anim.spriteRenderer.sprite != null && frames.TryGetValue(Suffix(anim.spriteRenderer.sprite.name), out Sprite now))
            anim.spriteRenderer.sprite = now;
    }

    // "Mira_DownLeft_2" -> "_DownLeft_2"
    private static string Suffix(string name) => name.Substring(name.IndexOf('_'));

    // Front-facing frame, for the HUD portrait and leaderboard rows.
    private static readonly Dictionary<string, Sprite> portraits = new Dictionary<string, Sprite>();
    public static Sprite Portrait(string id)
    {
        if (portraits.TryGetValue(id, out Sprite s) && s != null) return s;
        foreach (Sprite f in Resources.LoadAll<Sprite>("Heroes/" + id)) if (f.name.EndsWith("_Down_0")) s = f;
        return portraits[id] = s;
    }
}

// Shared leveling for a hero's three passives (level 1 at the start, up to 4 from level-ups).
public abstract class HeroPassives : MonoBehaviour
{
    public const int MaxPassiveLevel = 4;

    public class Passive
    {
        public string id, name;
        public string[] levels; // text for levels 2..4
    }

    public static HeroPassives Instance { get; private set; }

    protected abstract Passive[] Passives { get; }
    protected abstract void Apply(string id, int level);

    private readonly Dictionary<string, int> levels = new Dictionary<string, int>();

    public int LevelOf(string id) => levels.TryGetValue(id, out int l) ? l : 0;
    public List<Passive> Offerable() => new List<Passive>(System.Array.FindAll(Passives, p => LevelOf(p.id) < MaxPassiveLevel));

    public void LevelUp(string id)
    {
        int level = levels[id] = Mathf.Min(MaxPassiveLevel, LevelOf(id) + 1);
        Apply(id, level);
    }

    protected virtual void Awake()
    {
        foreach (Passive p in Passives) levels[p.id] = 1;
    }

    protected virtual void OnEnable() => Instance = this;
    protected virtual void OnDisable() { if (Instance == this) Instance = null; }
}
