using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// HoloCure-style collabs: once both halves of a combo are maxed, the player can combine them from the Combine menu (C).
// Each weapon/skill can go into one combination only, merged weapons can't be picked up again, and a run allows MaxCollabs.
//   Merge - two Lv 7 weapons fuse into one collab weapon (frees a weapon slot)
//   Boost - a Lv 7 weapon + a maxed skill: the weapon gains an extra effect (the skill is kept)
//   Bomb  - max bombs per throw + a maxed explosion skill: upgrades the main bombs
// Weapons/skills check their boost with PlayerCollabs.Has(id).
[RequireComponent(typeof(PlayerStats))]
public class PlayerCollabs : MonoBehaviour
{
    public enum Kind { Merge, Boost, Bomb, Super }

    public class Def
    {
        public string id, name, text;
        public string a; // weapon id (unused for Bomb)
        public string b; // weapon id (Merge) or skill id (Boost / Bomb); Super: a and b are combo ids
        public string icon; // Super: whose art the card / slot shows
        public Kind kind;
    }

    public const int MaxBombsPerThrow = 6; // matches the "+1 bomb" upgrade cap
    public const int MaxCollabs = 4;

    public static readonly Def[] All =
    {
        new Def { id = "chakram", kind = Kind.Merge, a = "boomerang", b = "shuriken", name = "Chakram Storm",
                  text = "4 giant blades bounce between 5 enemies, then fly back through everything" },
        new Def { id = "thunderstorm", kind = Kind.Merge, a = "molotov", b = "lightning", name = "Thunderstorm",
                  text = "Storm clouds drift over the crowd, raining lightning that sets the ground on fire" },
        new Def { id = "barrage", kind = Kind.Merge, a = "missiles", b = "mines", name = "Cluster Barrage",
                  text = "8 homing missiles; every blast scatters 3 armed mines" },
        new Def { id = "glacier", kind = Kind.Merge, a = "frost", b = "stomp", name = "Glacier Quake",
                  text = "A huge slam that freezes cats solid; frozen cats take 50% more damage" },
        new Def { id = "horizon", kind = Kind.Merge, a = "laser", b = "blackhole", name = "Event Horizon",
                  text = "2 black holes that spin lasers outward while they pull cats in" },
        new Def { id = "tarpit", kind = Kind.Merge, a = "glue", b = "molotov", name = "Tar Pit",
                  text = "4 huge pools of burning tar: 75% slow and heavy fire damage" },
        new Def { id = "tesla", kind = Kind.Merge, a = "lightning", b = "orbs", name = "Tesla Coils",
                  text = "8 orbs linked by electric arcs that zap everything nearby" },
        new Def { id = "gravitywheel", kind = Kind.Merge, a = "boomerang", b = "blackhole", name = "Gravity Wheel",
                  text = "Huge boomerangs drag cats along their path and pop at the far end" },

        new Def { id = "halo", kind = Kind.Merge, a = "orbs", b = "aura", name = "Halo",
                  text = "A ring of 8 light orbs that sends out a damaging pulse every second" },
        new Def { id = "thunderstar", kind = Kind.Merge, a = "shuriken", b = "lightning", name = "Thunder Star",
                  text = "Ricocheting stars that call down lightning on every bounce" },
        new Def { id = "absolutezero", kind = Kind.Merge, a = "blackhole", b = "frost", name = "Absolute Zero",
                  text = "2 black holes that freeze everything they pull in, then shatter it" },
        new Def { id = "stickymines", kind = Kind.Merge, a = "mines", b = "glue", name = "Sticky Mines",
                  text = "Mines fly onto cats, glue them in place, then explode" },
        new Def { id = "seismic", kind = Kind.Merge, a = "stomp", b = "aura", name = "Seismic Field",
                  text = "A wide damaging field that cracks the ground every 2.5s, stunning and shoving cats" },
        new Def { id = "bladeorbit", kind = Kind.Merge, a = "orbs", b = "shuriken", name = "Blade Orbit",
                  text = "6 spinning blades orbit you and fling themselves at cats, then come back" },
        new Def { id = "cryobeam", kind = Kind.Merge, a = "laser", b = "frost", name = "Cryo Beam",
                  text = "6 icy beams sweep around you, slowing cats, then freeze everything they touched" },
        new Def { id = "phoenix", kind = Kind.Merge, a = "boomerang", b = "molotov", name = "Phoenix Rang",
                  text = "3 flaming boomerangs set cats on fire, leave a trail of flames and burst into a fireball where they turn" },

        new Def { id = "missilechain", kind = Kind.Boost, a = "missiles", b = "chain", name = "Chain Missiles",
                  text = "Every missile blast sets off a chain reaction" },
        new Def { id = "auraguard", kind = Kind.Boost, a = "aura", b = "thickskin", name = "Guardian Aura",
                  text = "Cats touching you inside the aura deal 30% less damage" },
        new Def { id = "blinkmines", kind = Kind.Boost, a = "mines", b = "blink", name = "Parting Gift",
                  text = "Blinking leaves 3 mines where you were" },
        new Def { id = "gluemagnet", kind = Kind.Boost, a = "glue", b = "magnet", name = "Sticky Magnet",
                  text = "Glue puddles drag cats to their center and pull XP gems to you" },

        new Def { id = "sanctuary", kind = Kind.Boost, a = "aura", b = "regen", name = "Sanctuary",
                  text = "Heal 0.3 HP for every cat inside your aura each tick (up to 3 HP)" },
        new Def { id = "campfire", kind = Kind.Boost, a = "molotov", b = "regen", name = "Campfire",
                  text = "Standing in your own fire heals you 2 HP per tick" },
        new Def { id = "supernova", kind = Kind.Boost, a = "blackhole", b = "cluster", name = "Supernova",
                  text = "Black holes pop in a wider blast and scatter 8 mini bombs" },
        new Def { id = "repulsor", kind = Kind.Boost, a = "orbs", b = "shockwave", name = "Repulsor",
                  text = "Orbs hurl cats away and stun them for 0.4s" },

        new Def { id = "concussion", kind = Kind.Bomb, b = "shockwave", name = "Concussion Bombs",
                  text = "Bomb blasts stun everything they hit for 0.6s" },
        new Def { id = "magnetbomb", kind = Kind.Bomb, b = "magnet", name = "Magnet Bombs",
                  text = "Stuck bombs drag nearby cats in before they go off" },
        new Def { id = "furyfuse", kind = Kind.Bomb, b = "berserk", name = "Fury Fuse",
                  text = "The lower your HP, the faster you throw (up to 2x) and the bigger the blasts (up to +50%)" },
        new Def { id = "carpet", kind = Kind.Bomb, b = "cluster", name = "Carpet Bomb",
                  text = "Every throw also sprays a line of mini bombs" },
        new Def { id = "firebomb", kind = Kind.Bomb, b = "napalm", name = "Firebomb",
                  text = "Napalm fires are bigger, longer and hotter; burning cats explode when they die" },
    };

    // Super combines: two finished combos fused into one. One per run, unlocked by killing the 15:00 boss
    // (BossTimeline). The super takes over the combo weapons (freeing a slot) and adds a signature move
    // on top: see Weapons/SuperWeapon.cs. Each pair here shares no pieces, so both combos can exist in one run.
    public static readonly Def[] Supers =
    {
        new Def { id = "tempest", kind = Kind.Super, a = "chakram", b = "thunderstorm", icon = "boomerang", name = "Tempest Blades",
                  text = "A storm gathers around you, then 6 giant storm blades spiral outward while lightning strikes and sets fires wherever they are" },
        new Def { id = "cryo", kind = Kind.Super, a = "glacier", b = "horizon", icon = "frost", name = "Cryo Singularity",
                  text = "A frozen singularity drags the crowd in and freezes it, then collapses into 12 ice lasers; frozen cats shatter for double damage" },
        new Def { id = "stormnet", kind = Kind.Super, a = "tesla", b = "barrage", icon = "lightning", name = "Storm Network",
                  text = "5 tesla pylons link into a crackling web that shocks and stuns anything crossing it, then fire a volley of homing missiles" },
        new Def { id = "magmarift", kind = Kind.Super, a = "tarpit", b = "seismic", icon = "molotov", name = "Magma Rift",
                  text = "Your slam tears 6 lava fissures through the ground, burning and stunning everything on them, and each erupts at its end" },
        new Def { id = "judgment", kind = Kind.Super, a = "halo", b = "thunderstar", icon = "aura", name = "Divine Judgment",
                  text = "A golden sigil marks the toughest cat, a pillar of holy lightning smites it, and 8 ricocheting stars burst out" },
        new Def { id = "sawgalaxy", kind = Kind.Super, a = "bladeorbit", b = "gravitywheel", icon = "orbs", name = "Saw Galaxy",
                  text = "3 spiral arms of saws turn around you like a galaxy while a gravity vortex sweeps cats into the blades" },
        new Def { id = "permafrost", kind = Kind.Super, a = "stickymines", b = "glacier", icon = "mines", name = "Permafrost Minefield",
                  text = "An icy slam scatters 8 frost mines; when a cat comes close they burst and freeze everything nearby" },
        new Def { id = "laserdiscs", kind = Kind.Super, a = "chakram", b = "horizon", icon = "laser", name = "Laser Discs",
                  text = "4 energy discs fly out and back, each sweeping two spinning lasers that slice through everything" },
        new Def { id = "blackice", kind = Kind.Super, a = "absolutezero", b = "thunderstorm", icon = "blackhole", name = "Black Ice Storm",
                  text = "A frozen storm cloud rains blue lightning that freezes cats; cats struck while frozen shatter for big damage" },
        new Def { id = "hellfire", kind = Kind.Super, a = "firebomb", b = "thunderstorm", icon = "bomb", name = "Hellfire Bombs",
                  text = "Every bomb blast calls down crimson lightning and leaves a ring of hellfire" },
        new Def { id = "earthshaker", kind = Kind.Super, a = "concussion", b = "seismic", icon = "stomp", name = "Earthshaker",
                  text = "Every bomb blast cracks the ground into 3 fissures that stun and damage every cat along them" },
        new Def { id = "armageddon", kind = Kind.Super, a = "carpet", b = "barrage", icon = "missiles", name = "Armageddon",
                  text = "Every throw calls in an airstrike: 5 marked spots, then missiles scream down from the sky" },
        new Def { id = "frostfire", kind = Kind.Super, a = "cryobeam", b = "phoenix", icon = "frost", name = "Frostfire Eclipse",
                  text = "4 ice beams and 4 fire beams sweep in opposite directions; a cat caught by both cracks from thermal shock for big damage" },
        new Def { id = "infernowheel", kind = Kind.Super, a = "phoenix", b = "horizon", icon = "boomerang", name = "Inferno Wheel",
                  text = "2 huge burning wheels fly out and back, dragging cats in, spinning fire lasers and leaving a trail of flames" },
    };

    public static Def FindSuper(string id) => System.Array.Find(Supers, d => d.id == id);

    private static PlayerCollabs instance;
    private string superTaken; // the run's one super combine
    public bool SuperUnlocked { get; private set; }
    public string SuperTaken => superTaken;
    private readonly List<string> taken = new List<string>();
    private readonly HashSet<string> used = new HashSet<string>(); // ingredient ids already combined ("bombs" for the main bombs)
    private PlayerStats stats;
    private PlayerWeapons weapons;
    private PlayerSkills skills;

    public IReadOnlyList<string> Taken => taken;

    public static bool Has(string id) => instance != null && (instance.taken.Contains(id) || instance.superTaken == id);

    // BossTimeline: the 15:00 boss died.
    public static void UnlockSupers()
    {
        if (instance == null || instance.SuperUnlocked) return;
        instance.SuperUnlocked = true;
        DamagePopup.SpawnText((Vector2)instance.transform.position + Vector2.up * 1.2f, "SUPER COMBINES UNLOCKED!", SuperWeapon.SuperPink, 5f);
        Sfx.Play(Sfx.Id.Ready, 0.9f, 0.8f);
    }

    // A weapon fused into a collab: never offered on level-up again.
    public static bool IsConsumed(string weaponId) => instance != null && instance.used.Contains(weaponId);

    public int Remaining => MaxCollabs - taken.Count;
    public static Def Find(string id) => System.Array.Find(All, d => d.id == id) ?? FindSuper(id);

    private void Awake()
    {
        instance = this;
        stats = GetComponent<PlayerStats>();
        weapons = GetComponent<PlayerWeapons>();
        skills = GetComponent<PlayerSkills>();
    }

    // "Boomerang + Shuriken", for cards and the game over screen
    public static string Recipe(Def d)
    {
        string Weapon(string id) => PlayerWeapons.Find(id).name;
        string Skill(string id) => PlayerSkills.Find(id).name;
        return d.kind == Kind.Super ? $"{Find(d.a).name} + {Find(d.b).name}"
             : d.kind == Kind.Merge ? $"{Weapon(d.a)} + {Weapon(d.b)}"
             : d.kind == Kind.Boost ? $"{Weapon(d.a)} + {Skill(d.b)}"
             : $"Max Bombs + {Skill(d.b)}";
    }

    public List<Def> Offerable()
    {
        var result = new List<Def>();
        foreach (Def d in All)
            if (!taken.Contains(d.id) && Ready(d)) result.Add(d);
        if (Remaining <= 0) result.Clear();
        foreach (Def s in Supers) if (SuperStatus(s) == Status.Ready) result.Add(s);
        return result;
    }

    // Super combine card state: Locked = both combos done but the 15:00 boss is still alive.
    public Status SuperStatus(Def s)
    {
        if (superTaken == s.id) return Status.Done;
        if (superTaken != null) return Status.Blocked;
        bool a = taken.Contains(s.a), b = taken.Contains(s.b);
        if (a && b) return SuperUnlocked ? Status.Ready : Status.Locked;
        if ((!a && StatusOf(Find(s.a)) == Status.Blocked) || (!b && StatusOf(Find(s.b)) == Status.Blocked)) return Status.Blocked;
        return a || b ? Status.InProgress : Status.NotStarted;
    }

    // Admin tool: undo a combination. Its weapon goes away and its pieces are free to combine again.
    // Removing a super also undoes the two combos that went into it.
    public void Remove(Def d)
    {
        if (d.kind == Kind.Super)
        {
            if (superTaken != d.id) return;
            superTaken = null;
            weapons.Remove(d.id);
            Remove(Find(d.a));
            Remove(Find(d.b));
            return;
        }
        if (!taken.Remove(d.id)) return;
        used.Remove(FirstPiece(d));
        used.Remove(d.b);
        if (d.kind == Kind.Merge) weapons.Remove(d.id);
    }

    // The taken super is built from this combo (so the combo can't be removed on its own).
    public bool InSuper(string comboId)
    {
        Def s = superTaken != null ? FindSuper(superTaken) : null;
        return s != null && (s.a == comboId || s.b == comboId);
    }

    private void TakeSuper(Def d)
    {
        superTaken = d.id;
        var parts = new List<string>();
        foreach (string combo in new[] { d.a, d.b })
            if (Find(combo).kind == Kind.Merge) parts.Add(combo); // combo weapons the super takes over
        weapons.Super(parts, PlayerWeapons.Find(d.id));

        Vector2 at = transform.position;
        DamagePopup.SpawnText(at + Vector2.up * 0.9f, "SUPER COMBINE!\n" + d.name.ToUpper(), SuperWeapon.SuperPink, 7f);
        Juice.Flash(new Color(1f, 0.5f, 0.9f, 0.6f), 0.6f);
        Juice.SlowMo(0.25f, 0.6f);
        ExplosionFx.Ring(at, 10f, SuperWeapon.SuperPink, 0.7f);
        ExplosionFx.Ring(at, 6f, Color.white, 0.5f);
        Juice.Sparks(at, SuperWeapon.SuperPink, 24, 12f, 0.2f);
        Lighting.Flash(at, 12f, SuperWeapon.SuperPink, 1f);
        CameraFollow.Shake(0.25f, 0.4f);
        CameraFollow.ZoomPunch(0.6f);
        Sfx.Play(Sfx.Id.LevelUp, 1f, 0.8f);
    }

    private bool Ready(Def d)
    {
        bool WeaponMaxed(string id) => weapons != null && weapons.LevelOf(id) >= Weapon.MaxLevel;
        bool SkillMaxed(string id) => skills != null && skills.LevelOf(id) >= PlayerSkills.Find(id).levels.Length;

        if (used.Contains(d.kind == Kind.Bomb ? "bombs" : d.a) || used.Contains(d.b)) return false; // each piece combines once

        switch (d.kind)
        {
            case Kind.Merge: return WeaponMaxed(d.a) && WeaponMaxed(d.b);
            case Kind.Boost: return WeaponMaxed(d.a) && SkillMaxed(d.b);
            default: return stats.projectileCount >= MaxBombsPerThrow && SkillMaxed(d.b);
        }
    }

    // Combos that become ready once piece id is maxed, because the other piece already is.
    // Pieces: weapon ids, skill ids, or "bombs" (main bombs at max per throw). Used for level-up card hints.
    public List<Def> UnlockedBy(string id)
    {
        var result = new List<Def>();
        if (Remaining <= 0) return result;
        foreach (Def d in All)
        {
            string first = d.kind == Kind.Bomb ? "bombs" : d.a;
            if (taken.Contains(d.id) || used.Contains(first) || used.Contains(d.b)) continue;
            bool firstIsSkill = false, secondIsSkill = d.kind != Kind.Merge;
            if (id == first && PieceMaxed(d.b, secondIsSkill)) result.Add(d);
            else if (id == d.b && PieceMaxed(first, firstIsSkill)) result.Add(d);
        }
        return result;
    }

    private bool PieceMaxed(string id, bool isSkill)
    {
        if (id == "bombs") return stats.projectileCount >= MaxBombsPerThrow;
        if (isSkill) return skills != null && skills.LevelOf(id) >= PlayerSkills.Find(id).levels.Length;
        return weapons != null && weapons.LevelOf(id) >= Weapon.MaxLevel;
    }

    // ---------- Combo Book / level-up suggestions ----------

    public enum Status { Done, Ready, InProgress, NotStarted, Blocked, Locked }

    public static string FirstPiece(Def d) => d.kind == Kind.Bomb ? "bombs" : d.a;
    public static bool SecondIsSkill(Def d) => d.kind != Kind.Merge;

    public Status StatusOf(Def d)
    {
        if (taken.Contains(d.id)) return Status.Done;
        if (used.Contains(FirstPiece(d)) || used.Contains(d.b) || Remaining <= 0) return Status.Blocked;
        if (Ready(d)) return Status.Ready;
        return Progress(d) > 0f ? Status.InProgress : Status.NotStarted;
    }

    // 0..2: how far along both pieces are (bombs count from the 1 you start with)
    public float Progress(Def d)
    {
        float Part(string id, bool isSkill)
        {
            int level = PieceLevel(id, isSkill), max = PieceMax(id, isSkill);
            return id == "bombs" ? (level - 1f) / (max - 1f) : level / (float)max;
        }
        return Part(FirstPiece(d), false) + Part(d.b, SecondIsSkill(d));
    }

    // Weapon/skill level, or bombs per throw for "bombs"; with its max.
    public int PieceLevel(string id, bool isSkill) =>
        id == "bombs" ? stats.projectileCount : isSkill ? (skills != null ? skills.LevelOf(id) : 0) : (weapons != null ? weapons.LevelOf(id) : 0);
    public static int PieceMax(string id, bool isSkill) =>
        id == "bombs" ? MaxBombsPerThrow : isSkill ? PlayerSkills.Find(id).levels.Length : Weapon.MaxLevel;
    public static string PieceName(string id, bool isSkill) =>
        id == "bombs" ? "Max Bombs" : isSkill ? PlayerSkills.Find(id).name : PlayerWeapons.Find(id).name;
    public bool PieceUsed(string id) => used.Contains(id);

    // Open combos that use this piece and whose other piece you already have (for level-up suggestions).
    // score: 2 = partner owned, 3 = partner already maxed.
    public List<(Def def, string partner, bool partnerIsSkill, int score)> CombosWithOwnedPartner(string pieceId)
    {
        var result = new List<(Def, string, bool, int)>();
        foreach (Def d in All)
        {
            Status s = StatusOf(d);
            if (s == Status.Done || s == Status.Blocked) continue;
            string first = FirstPiece(d);
            string partner; bool partnerIsSkill;
            if (pieceId == first) { partner = d.b; partnerIsSkill = SecondIsSkill(d); }
            else if (pieceId == d.b) { partner = first; partnerIsSkill = false; }
            else continue;
            int level = PieceLevel(partner, partnerIsSkill);
            if (partner == "bombs" ? level <= 1 : level <= 0) continue; // everyone starts with 1 bomb
            result.Add((d, partner, partnerIsSkill, level >= PieceMax(partner, partnerIsSkill) ? 3 : 2));
        }
        return result;
    }

    public void Take(Def d)
    {
        if (d.kind == Kind.Super) { TakeSuper(d); return; }
        taken.Add(d.id);
        used.Add(d.kind == Kind.Bomb ? "bombs" : d.a);
        used.Add(d.b);
        if (d.kind == Kind.Merge) weapons.Merge(d.a, d.b, PlayerWeapons.Find(d.id));

        // the two pieces' icons fly in from either side and slam together on Mira, then a golden burst
        Sfx.Play(Sfx.Id.Whoosh, 0.6f, 1.2f);
        FlyIn(IconPath(d.a), Vector2.left, null);
        FlyIn(IconPath(d.b), Vector2.right, () => FuseBurst(d));
    }

    private const float FlyTime = 0.35f;

    private static string IconPath(string id) =>
        id == null ? "Icons/bomb" : Resources.Load<Texture2D>("Icons/" + id) != null ? "Icons/" + id : "Icons/skill_" + id;

    private void FlyIn(string icon, Vector2 side, System.Action onArrive)
    {
        Vector2 offset = side * 2.2f + Vector2.up * 0.6f;
        Shot s = Shot.Spawn(Shapes.Art(icon), Color.white, 1f, (Vector2)transform.position + offset, 70);
        s.life = FlyTime;
        s.ghost = new Color(1f, 0.85f, 0.45f, 0.5f);
        Juice.Sparks(s.Position, new Color(1f, 0.85f, 0.4f), 6, 3f, 0.12f);
        s.onUpdate = shot =>
        {
            float k = shot.age / FlyTime;
            shot.transform.position = (Vector2)transform.position + offset * (1f - k * k); // speeds up into the slam
            shot.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, k);
        };
        if (onArrive != null) s.onExpire = _ => onArrive();
    }

    private void FuseBurst(Def d)
    {
        Vector2 at = transform.position;
        Color gold = new Color(1f, 0.82f, 0.3f);
        DamagePopup.SpawnText(at + Vector2.up * 0.8f, "COMBINE!\n" + d.name.ToUpper(), gold, 6f);
        Juice.Flash(new Color(1f, 0.85f, 0.4f, 0.5f), 0.4f);
        Juice.SlowMo(0.35f, 0.35f);
        ExplosionFx.Ring(at, 7f, gold, 0.6f);
        ExplosionFx.Ring(at, 4f, Color.white, 0.4f);
        Juice.Sparks(at, gold, 18, 10f, 0.18f);
        Lighting.Flash(at, 9f, gold, 0.8f);
        CameraFollow.Shake(0.15f, 0.3f);
        CameraFollow.ZoomPunch(0.35f);
        Sfx.Play(Sfx.Id.LevelUp, 0.8f, 1.1f);
    }

    // Firebomb: a burning cat that dies explodes. Delayed a beat: blasts can't nest inside a blast
    // (they share one hit buffer), and it reads better as a ripple.
    public static void BurningDeath(Vector2 pos)
    {
        if (!Has("firebomb")) return;
        instance.StartCoroutine(instance.DelayedBlast(pos));
    }

    private IEnumerator DelayedBlast(Vector2 pos)
    {
        yield return new WaitForSeconds(0.1f);
        Bomb.Blast(pos, 1.3f * stats.areaMultiplier, stats.AttackDamage * 0.6f, stats.critChance, stats.critDamage, secondary: true);
    }
}
