using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerStats))]
public class PlayerCollabs : MonoBehaviour
{
    public enum Kind { Merge, Boost, Bomb, Super }

    public class Def
    {
        public string id, name, text;
        public string a;
        public string b;
        public string icon;
        public Kind kind;
    }

    public static int MaxBombsPerThrow => Hero.Current.id == "Kai" || Hero.Current.id == "Nyx" ? 4 : 6;
    public const int MaxCollabs = 4;

    public static readonly Def[] All =
    {
        new Def { id = "chakram", kind = Kind.Merge, a = "boomerang", b = "shuriken", name = "Claw Disc",
                  text = "4 big blades bounce between 5 cats, then come back" },
        new Def { id = "thunderstorm", kind = Kind.Merge, a = "molotov", b = "lightning", name = "Hiss Storm",
                  text = "Storm clouds follow the crowd and strike lightning that leaves fire" },
        new Def { id = "barrage", kind = Kind.Merge, a = "missiles", b = "mines", name = "Pounce Barrage",
                  text = "8 homing missiles. Each blast drops 3 mines" },
        new Def { id = "glacier", kind = Kind.Merge, a = "frost", b = "stomp", name = "Frosty Paws",
                  text = "Big slam that freezes cats. Frozen cats take 50% more damage" },
        new Def { id = "horizon", kind = Kind.Merge, a = "laser", b = "blackhole", name = "Litter Box Void",
                  text = "2 black holes that spin lasers while pulling cats in" },
        new Def { id = "tarpit", kind = Kind.Merge, a = "glue", b = "molotov", name = "Tar Pit",
                  text = "4 big pools of burning tar. 75% slow, heavy fire damage" },
        new Def { id = "tesla", kind = Kind.Merge, a = "lightning", b = "orbs", name = "Static Fur",
                  text = "8 orbs linked by arcs that zap anything nearby" },
        new Def { id = "gravitywheel", kind = Kind.Merge, a = "boomerang", b = "blackhole", name = "Yarn Ball",
                  text = "Big boomerangs drag cats along and pop at the far end" },

        new Def { id = "halo", kind = Kind.Merge, a = "orbs", b = "aura", name = "Halo",
                  text = "8 light orbs, damaging pulse every second" },
        new Def { id = "thunderstar", kind = Kind.Merge, a = "shuriken", b = "lightning", name = "Thunder Star",
                  text = "Bouncing stars, lightning on every bounce" },
        new Def { id = "absolutezero", kind = Kind.Merge, a = "blackhole", b = "frost", name = "Cold Nose",
                  text = "2 frost vortexes that freeze what they pull in, then shatter it" },
        new Def { id = "stickymines", kind = Kind.Merge, a = "mines", b = "glue", name = "Sticky Mines",
                  text = "Mines fly onto cats, glue them down, then explode" },
        new Def { id = "seismic", kind = Kind.Merge, a = "stomp", b = "aura", name = "Kneading Field",
                  text = "Wide field. Every 2.5s the ground cracks, stunning and shoving cats" },
        new Def { id = "bladeorbit", kind = Kind.Merge, a = "orbs", b = "shuriken", name = "Claw Orbit",
                  text = "6 blades circle you, fling at cats, then come back" },
        new Def { id = "cryobeam", kind = Kind.Merge, a = "laser", b = "frost", name = "Brain Freeze",
                  text = "6 icy beams sweep around you, slow cats, then freeze them" },
        new Def { id = "phoenix", kind = Kind.Merge, a = "boomerang", b = "molotov", name = "Fire Rang",
                  text = "3 flaming boomerangs. Leave fire behind, explode where they turn" },

        new Def { id = "missilechain", kind = Kind.Boost, a = "missiles", b = "chain", name = "Chain Missiles",
                  text = "Every missile blast sets off a chain reaction" },
        new Def { id = "auraguard", kind = Kind.Boost, a = "aura", b = "thickskin", name = "Thick Coat",
                  text = "Steel aura. Cats touching you inside it deal 30% less damage" },
        new Def { id = "blinkmines", kind = Kind.Boost, a = "mines", b = "blink", name = "Parting Gift",
                  text = "Blinking leaves 5 mines where you were" },
        new Def { id = "gluemagnet", kind = Kind.Boost, a = "glue", b = "magnet", name = "Sticky Magnet",
                  text = "Glue puddles drag cats to their center and pull XP gems to you" },

        new Def { id = "sanctuary", kind = Kind.Boost, a = "aura", b = "regen", name = "Purr Therapy",
                  text = "Green healing aura. Heal 0.3 HP for every cat inside it each tick (up to 3 HP)" },
        new Def { id = "campfire", kind = Kind.Boost, a = "molotov", b = "regen", name = "Campfire",
                  text = "Every throw also lights a healing campfire at your feet. Standing in your fire heals 2 HP per tick" },
        new Def { id = "supernova", kind = Kind.Boost, a = "blackhole", b = "cluster", name = "Supernova",
                  text = "Black holes pop in a wider blast and scatter 8 mini bombs" },
        new Def { id = "repulsor", kind = Kind.Boost, a = "orbs", b = "shockwave", name = "Shoo!",
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
                  text = "Napalm fires are bigger, longer and hotter. Burning cats explode" },
    };

    public static readonly Def[] Supers =
    {
        new Def { id = "tempest", kind = Kind.Super, a = "chakram", b = "thunderstorm", icon = "boomerang", name = "Cat-egory 5",
                  text = "6 storm blades spiral out. Lightning and fire wherever they go" },
        new Def { id = "cryo", kind = Kind.Super, a = "glacier", b = "horizon", icon = "frost", name = "Snow Globe",
                  text = "Pulls the crowd in and freezes it, then fires 12 ice lasers. Frozen cats shatter for 2x damage" },
        new Def { id = "stormnet", kind = Kind.Super, a = "tesla", b = "barrage", icon = "lightning", name = "Static Cling",
                  text = "5 pylons link into a shocking web, then fire homing missiles" },
        new Def { id = "magmarift", kind = Kind.Super, a = "tarpit", b = "seismic", icon = "molotov", name = "Hot Floor",
                  text = "Your slam opens 6 lava cracks that burn and stun, then erupt" },
        new Def { id = "judgment", kind = Kind.Super, a = "halo", b = "thunderstar", icon = "aura", name = "Holy Hiss",
                  text = "Lightning pillar on the toughest cat, then 8 bouncing stars fly out" },
        new Def { id = "sawgalaxy", kind = Kind.Super, a = "bladeorbit", b = "gravitywheel", icon = "orbs", name = "Fur Shredder",
                  text = "3 spiral arms of saws spin around you while a vortex pulls cats in" },
        new Def { id = "permafrost", kind = Kind.Super, a = "stickymines", b = "glacier", icon = "mines", name = "Cold Feet",
                  text = "Slam scatters 8 frost mines that freeze everything nearby" },
        new Def { id = "laserdiscs", kind = Kind.Super, a = "chakram", b = "horizon", icon = "laser", name = "Laser Pointer",
                  text = "4 discs fly out and back, each spinning 2 lasers" },
        new Def { id = "blackice", kind = Kind.Super, a = "absolutezero", b = "thunderstorm", icon = "blackhole", name = "Cold Snap",
                  text = "A dark cloud rains ice lightning that freezes cats. Frozen cats shatter" },
        new Def { id = "hellfire", kind = Kind.Super, a = "firebomb", b = "thunderstorm", icon = "bomb", name = "Spicy Bombs",
                  text = "Every bomb blast calls red lightning and leaves a ring of fire" },
        new Def { id = "earthshaker", kind = Kind.Super, a = "concussion", b = "seismic", icon = "stomp", name = "Catquake",
                  text = "Every bomb blast cracks the ground 3 ways, stunning cats on the cracks" },
        new Def { id = "armageddon", kind = Kind.Super, a = "carpet", b = "barrage", icon = "missiles", name = "Catastrophe",
                  text = "Every throw marks 5 spots for falling meteors" },
        new Def { id = "frostfire", kind = Kind.Super, a = "cryobeam", b = "phoenix", icon = "frost", name = "Hot & Cold",
                  text = "Ice and fire beams spin opposite ways. Cats hit by both take big damage" },
        new Def { id = "infernowheel", kind = Kind.Super, a = "phoenix", b = "horizon", icon = "boomerang", name = "Spin Cycle",
                  text = "2 burning wheels fly out and back, pulling cats in and trailing fire" },
    };

    public static Def FindSuper(string id) => System.Array.Find(Supers, d => d.id == id);

    private static PlayerCollabs instance;
    private string superTaken;
    public bool SuperUnlocked { get; private set; }
    public string SuperTaken => superTaken;
    private readonly List<string> taken = new List<string>();
    private readonly HashSet<string> used = new HashSet<string>();
    private PlayerStats stats;
    private PlayerWeapons weapons;
    private PlayerSkills skills;

    public IReadOnlyList<string> Taken => taken;
    public List<string> MainCombos => taken.FindAll(id => Find(id).kind == Kind.Bomb);

    public static bool Has(string id) => instance != null && (instance.taken.Contains(id) || instance.superTaken == id);

    public static void UnlockSupers()
    {
        if (instance == null || instance.SuperUnlocked) return;
        instance.SuperUnlocked = true;
        DamagePopup.SpawnText((Vector2)instance.transform.position + Vector2.up * 1.2f, "SUPER COMBINES UNLOCKED!", SuperWeapon.SuperPink, 5f);
        Sfx.Play(Sfx.Id.Ready, 0.9f, 0.8f);
    }

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

    public static string Recipe(Def d)
    {
        string Weapon(string id) => PlayerWeapons.Find(id).name;
        string Skill(string id) => PlayerSkills.Find(id).name;
        return d.kind == Kind.Super ? $"{Find(d.a).name} + {Find(d.b).name}"
             : d.kind == Kind.Merge ? $"{Weapon(d.a)} + {Weapon(d.b)}"
             : d.kind == Kind.Boost ? $"{Weapon(d.a)} + {Skill(d.b)}"
             : $"{PieceName("bombs", false)} + {Skill(d.b)}";
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

    public Status SuperStatus(Def s)
    {
        if (superTaken == s.id) return Status.Done;
        if (superTaken != null) return Status.Blocked;
        bool a = taken.Contains(s.a), b = taken.Contains(s.b);
        if (a && b) return SuperUnlocked ? Status.Ready : Status.Locked;
        if ((!a && StatusOf(Find(s.a)) == Status.Blocked) || (!b && StatusOf(Find(s.b)) == Status.Blocked)) return Status.Blocked;
        return a || b ? Status.InProgress : Status.NotStarted;
    }

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
        if (d.kind != Kind.Bomb) weapons.Remove(d.id);
        if (d.kind != Kind.Merge) skills.Unabsorb(d.b);
    }

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
            if (Find(combo).kind == Kind.Merge) parts.Add(combo);
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
        Juice.SparksDir(at, Vector2.up, 30f, Color.white, 24, 14f, 0.2f);
        Sfx.Play(Sfx.Id.Fanfare, 0.8f, 0.9f);
    }

    private bool Ready(Def d)
    {
        bool WeaponMaxed(string id) => weapons != null && weapons.LevelOf(id) >= Weapon.MaxLevel;
        bool SkillMaxed(string id) => skills != null && skills.LevelOf(id) >= PlayerSkills.Find(id).levels.Length;

        if (used.Contains(d.kind == Kind.Bomb ? "bombs" : d.a) || used.Contains(d.b)) return false;

        switch (d.kind)
        {
            case Kind.Merge: return WeaponMaxed(d.a) && WeaponMaxed(d.b);
            case Kind.Boost: return WeaponMaxed(d.a) && SkillMaxed(d.b);
            default: return stats.projectileCount >= MaxBombsPerThrow && SkillMaxed(d.b);
        }
    }

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

    public float Progress(Def d)
    {
        float Part(string id, bool isSkill)
        {
            int level = PieceLevel(id, isSkill), max = PieceMax(id, isSkill);
            return id == "bombs" ? (level - 1f) / (max - 1f) : level / (float)max;
        }
        return Part(FirstPiece(d), false) + Part(d.b, SecondIsSkill(d));
    }

    public int PieceLevel(string id, bool isSkill) =>
        id == "bombs" ? stats.projectileCount : isSkill ? (skills != null ? skills.LevelOf(id) : 0) : (weapons != null ? weapons.LevelOf(id) : 0);
    public static int PieceMax(string id, bool isSkill) =>
        id == "bombs" ? MaxBombsPerThrow : isSkill ? PlayerSkills.Find(id).levels.Length : Weapon.MaxLevel;
    public static string PieceName(string id, bool isSkill) =>
        id == "bombs" ? Hero.Current.maxPiece : isSkill ? PlayerSkills.Find(id).name : PlayerWeapons.Find(id).name;
    public bool PieceUsed(string id) => used.Contains(id);

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
            if (partner == "bombs" ? level <= 1 : level <= 0) continue;
            result.Add((d, partner, partnerIsSkill, level >= PieceMax(partner, partnerIsSkill) ? 3 : 2));
        }
        return result;
    }

    public static string PairHint(string pieceId, int max = 2)
    {
        if (instance == null) return null;
        var parts = new List<string>();
        foreach (Def d in All)
        {
            Status s = instance.StatusOf(d);
            if (s == Status.Done || s == Status.Blocked) continue;
            string first = FirstPiece(d);
            if (pieceId == first) parts.Add($"{PieceName(d.b, SecondIsSkill(d))} -> {d.name}");
            else if (pieceId == d.b) parts.Add($"{PieceName(first, false)} -> {d.name}");
        }
        if (parts.Count == 0) return null;
        string more = parts.Count > max ? $" +{parts.Count - max} more" : "";
        return string.Join(", ", parts.GetRange(0, Mathf.Min(max, parts.Count))) + more;
    }

    public static void MaxHint(string pieceId, Vector2 at)
    {
        string hint = PairHint(pieceId);
        if (hint != null) DamagePopup.SpawnText(at + Vector2.up * 0.5f, "Pairs with " + hint, new Color(1f, 0.82f, 0.3f), 3.2f);
    }

    public void Take(Def d)
    {
        Achievements.MarkTried(d.kind == Kind.Super ? "x" : "c", d.id);
        if (d.kind == Kind.Super) { TakeSuper(d); return; }
        taken.Add(d.id);
        used.Add(d.kind == Kind.Bomb ? "bombs" : d.a);
        used.Add(d.b);
        if (d.kind != Kind.Bomb) weapons.Merge(d.a, d.b, PlayerWeapons.Find(d.id));
        if (d.kind != Kind.Merge) skills.Absorb(d.b);

        Sfx.Play(Sfx.Id.Whoosh, 0.6f, 1.2f);
        FlyIn(IconPath(d.a), Vector2.left, null);
        FlyIn(IconPath(d.b), Vector2.right, () => FuseBurst(d));
    }

    private const float FlyTime = 0.35f;

    private static string IconPath(string id) =>
        id == null ? "Icons/" + Hero.Current.weapon
        : Resources.Load<Texture2D>("Icons/" + id) != null ? "Icons/" + id
        : Hero.Current.iconPrefix != null && Resources.Load<Texture2D>("Icons/" + Hero.Current.iconPrefix + id) != null ? "Icons/" + Hero.Current.iconPrefix + id : "Icons/skill_" + id;

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
            shot.transform.position = (Vector2)transform.position + offset * (1f - k * k);
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
        Juice.SparksDir(at, Vector2.up, 25f, Color.white, 16, 12f, 0.18f);
        Sfx.Play(Sfx.Id.Fanfare, 0.7f);
    }

    public static void BurningDeath(Vector2 pos)
    {
        if (!Has("firebomb")) return;
        instance.StartCoroutine(instance.DelayedBlast(pos));
    }

    private IEnumerator DelayedBlast(Vector2 pos)
    {
        yield return new WaitForSeconds(0.1f);
        float r = 1.3f * stats.areaMultiplier;
        ExplosionFx.Ring(pos, r * 1.3f, new Color(1f, 0.45f, 0.1f, 0.9f), 0.3f);
        Juice.SparksDir(pos, Vector2.up, 40f, new Color(1f, 0.6f, 0.15f), 8, 6f, 0.15f);
        Lighting.Flash(pos, r * 2f, new Color(1f, 0.5f, 0.15f), 0.25f);
        Bomb.Blast(pos, 1.3f * stats.areaMultiplier, stats.AttackDamage * 0.6f, stats.critChance, stats.critDamage, secondary: true);
    }
}
