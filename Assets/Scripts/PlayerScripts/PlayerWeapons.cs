using System.Collections.Generic;
using UnityEngine;

public class PlayerWeapons : MonoBehaviour
{
    public const int MaxWeapons = 7;

    public class Def
    {
        public string id;
        public string name;
        public System.Type type;
        public string[] levels;
    }

    public static readonly Def[] All =
    {
        new Def { id = "boomerang", name = "Boomerang", type = typeof(BoomerangWeapon), levels = new[] {
            "Throw 2 big boomerangs that pierce enemies and fly back (10 dmg)",
            "Damage 10 -> 13",
            "+1 boomerang",
            "Boomerangs 25% bigger",
            "Damage 13 -> 16, longer range",
            "+1 boomerang",
            "5 giant boomerangs, 22 damage, even longer range" } },
        new Def { id = "shuriken", name = "Shuriken", type = typeof(ShurikenWeapon), levels = new[] {
            "Throw 2 shurikens that each bounce to 2 more enemies (7 dmg)",
            "Damage 7 -> 8",
            "+1 bounce",
            "+1 shuriken, damage 9",
            "+1 bounce",
            "Damage 9 -> 10",
            "4 shurikens, 6 bounces, 12 damage" } },
        new Def { id = "missiles", name = "Homing Missiles", type = typeof(MissileWeapon), levels = new[] {
            "Fire 2 homing missiles at the strongest enemy nearby, healers first (16 dmg blast)",
            "+1 missile",
            "Damage 16 -> 20",
            "+1 missile",
            "Damage 20 -> 25, bigger blasts",
            "+1 missile",
            "8 missiles, 32 damage, huge blasts" } },
        new Def { id = "molotov", name = "Molotov", type = typeof(MolotovWeapon), levels = new[] {
            "Throw 2 molotovs that leave fire for 4s (6 dmg per tick)",
            "Fire damage 6 -> 7",
            "+1 molotov",
            "Bigger, longer fire, 8 damage",
            "Fire damage 8 -> 9",
            "+1 molotov, bigger fire",
            "5 molotovs, huge 6s fires, 12 damage per tick" } },
        new Def { id = "orbs", name = "Orbiting Orbs", type = typeof(OrbsWeapon), levels = new[] {
            "3 big orbs circle you and hit enemies they touch (7 dmg)",
            "Damage 7 -> 8",
            "+1 orb",
            "Wider orbit, damage 10",
            "+1 orb, faster spin",
            "Damage 10 -> 12, wider orbit",
            "8 orbs, 15 damage, fast and wide" } },
        new Def { id = "aura", name = "Damage Aura", type = typeof(AuraWeapon), levels = new[] {
            "A big aura that damages and slows enemies 15% (5 dmg every 0.5s)",
            "Bigger aura",
            "Damage 5 -> 6",
            "Bigger aura, slow 20%",
            "Bigger aura, damage 8",
            "Damage 8 -> 9, slow 25%",
            "Huge aura, 12 damage, slow 35%" } },
        new Def { id = "frost", name = "Frost Nova", type = typeof(FrostNovaWeapon), levels = new[] {
            "Every 3s a big icy pulse damages and slows nearby enemies 40% (12 dmg)",
            "Damage 12 -> 15",
            "Bigger pulse, more often",
            "Slow 50%, damage 19",
            "Bigger pulse, more often",
            "Damage 19 -> 24",
            "Huge pulse every 2s that freezes cats for 1.5s, 30 damage" } },
        new Def { id = "stomp", name = "Shockwave Stomp", type = typeof(StompWeapon), levels = new[] {
            "Every 3s slam the ground, knocking back and stunning nearby enemies (16 dmg)",
            "Bigger slam, 20 damage",
            "Damage 23, slams faster",
            "Bigger, faster slam",
            "Bigger slam, 28 damage",
            "Damage 28 -> 33, faster",
            "Huge slam every 2s, 42 damage" } },
        new Def { id = "lightning", name = "Lightning Strike", type = typeof(LightningWeapon), levels = new[] {
            "Lightning strikes 2 random enemies every 2s (20 dmg)",
            "+1 strike",
            "Damage 20 -> 25",
            "+1 strike",
            "Damage 25 -> 30",
            "+1 strike, 34 damage",
            "7 strikes, 40 damage" } },
        new Def { id = "laser", name = "Laser Beam", type = typeof(LaserWeapon), levels = new[] {
            "Fire 3 piercing lasers in a fan at the nearest enemy (6 dmg x 5 ticks)",
            "Damage per tick 6 -> 7",
            "Wider beams",
            "+1 laser, longer, fires faster, 9 damage",
            "Wider beams",
            "+1 laser, longer, faster, 11 damage",
            "8 lasers in every direction, 14 damage" } },
        new Def { id = "blackhole", name = "Black Hole", type = typeof(BlackHoleWeapon), levels = new[] {
            "Open a big black hole that pulls cats in, then pops (30 dmg blast)",
            "Pop damage 30 -> 36",
            "Wider pull, opens more often",
            "Lasts longer, 44 damage",
            "Wider pull, more often, 50 damage",
            "Lasts longer, 58 damage",
            "2 huge black holes, 75 damage mega pops every 4s" } },
        new Def { id = "mines", name = "Proximity Mines", type = typeof(MineWeapon), levels = new[] {
            "Drop mines as you move; they blast when cats get near (20 dmg)",
            "Drop faster, 23 damage",
            "More mines, 26 damage",
            "Drop faster, bigger blasts, 29 damage",
            "Bigger blasts, 33 damage",
            "Faster, more mines, 37 damage",
            "Up to 16 mines, 45 damage, huge blasts" } },
        new Def { id = "glue", name = "Glue Bomb", type = typeof(GlueWeapon), levels = new[] {
            "Throw 2 sticky glue puddles that slow enemies 50% and sting (2 dmg)",
            "Slow 55%, lasts longer",
            "Bigger puddles, 3 damage",
            "+1 glue bomb, slow 60%",
            "Bigger puddles, 4 damage",
            "Slow 65%, lasts longer",
            "4 huge puddles, 75% slow, 6 damage" } },
    };

    public static readonly Def[] CollabDefs =
    {
        new Def { id = "chakram", name = "Claw Disc", type = typeof(ChakramStormWeapon) },
        new Def { id = "thunderstorm", name = "Hiss Storm", type = typeof(ThunderstormWeapon) },
        new Def { id = "barrage", name = "Pounce Barrage", type = typeof(ClusterBarrageWeapon) },
        new Def { id = "glacier", name = "Frosty Paws", type = typeof(GlacierQuakeWeapon) },
        new Def { id = "horizon", name = "Litter Box Void", type = typeof(EventHorizonWeapon) },
        new Def { id = "tarpit", name = "Tar Pit", type = typeof(TarPitWeapon) },
        new Def { id = "tesla", name = "Static Fur", type = typeof(TeslaCoilsWeapon) },
        new Def { id = "gravitywheel", name = "Yarn Ball", type = typeof(GravityWheelWeapon) },
        new Def { id = "halo", name = "Halo", type = typeof(HaloWeapon) },
        new Def { id = "thunderstar", name = "Thunder Star", type = typeof(ThunderStarWeapon) },
        new Def { id = "absolutezero", name = "Cold Nose", type = typeof(AbsoluteZeroWeapon) },
        new Def { id = "stickymines", name = "Sticky Mines", type = typeof(StickyMinesWeapon) },
        new Def { id = "seismic", name = "Kneading Field", type = typeof(SeismicFieldWeapon) },
        new Def { id = "bladeorbit", name = "Claw Orbit", type = typeof(BladeOrbitWeapon) },
        new Def { id = "cryobeam", name = "Brain Freeze", type = typeof(CryoBeamWeapon) },
        new Def { id = "phoenix", name = "Fire Rang", type = typeof(PhoenixRangWeapon) },

        // Weapon + skill combos: the same weapon, renamed, with the combo effect switched on (see PlayerCollabs.Has).
        new Def { id = "missilechain", name = "Chain Missiles", type = typeof(MissileWeapon) },
        new Def { id = "auraguard", name = "Thick Coat", type = typeof(AuraWeapon) },
        new Def { id = "blinkmines", name = "Parting Gift", type = typeof(MineWeapon) },
        new Def { id = "gluemagnet", name = "Sticky Magnet", type = typeof(GlueWeapon) },
        new Def { id = "sanctuary", name = "Purr Therapy", type = typeof(AuraWeapon) },
        new Def { id = "campfire", name = "Campfire", type = typeof(MolotovWeapon) },
        new Def { id = "supernova", name = "Supernova", type = typeof(BlackHoleWeapon) },
        new Def { id = "repulsor", name = "Shoo!", type = typeof(OrbsWeapon) },

        new Def { id = "tempest", name = "Cat-egory 5", type = typeof(TempestBladesWeapon) },
        new Def { id = "cryo", name = "Snow Globe", type = typeof(CryoSingularityWeapon) },
        new Def { id = "stormnet", name = "Static Cling", type = typeof(StormNetworkWeapon) },
        new Def { id = "magmarift", name = "Hot Floor", type = typeof(MagmaRiftWeapon) },
        new Def { id = "judgment", name = "Holy Hiss", type = typeof(DivineJudgmentWeapon) },
        new Def { id = "sawgalaxy", name = "Fur Shredder", type = typeof(SawGalaxyWeapon) },
        new Def { id = "permafrost", name = "Cold Feet", type = typeof(PermafrostMinefieldWeapon) },
        new Def { id = "laserdiscs", name = "Laser Pointer", type = typeof(LaserDiscsWeapon) },
        new Def { id = "blackice", name = "Cold Snap", type = typeof(BlackIceStormWeapon) },
        new Def { id = "hellfire", name = "Spicy Bombs", type = typeof(HellfireBombsWeapon) },
        new Def { id = "earthshaker", name = "Catquake", type = typeof(EarthshakerWeapon) },
        new Def { id = "armageddon", name = "Catastrophe", type = typeof(ArmageddonWeapon) },
        new Def { id = "frostfire", name = "Hot & Cold", type = typeof(FrostfireEclipseWeapon) },
        new Def { id = "infernowheel", name = "Spin Cycle", type = typeof(InfernoWheelWeapon) },
    };

    public static bool IsSuper(string id) => PlayerCollabs.FindSuper(id) != null;

    private readonly Dictionary<string, Weapon> owned = new Dictionary<string, Weapon>();
    private readonly List<string> order = new List<string>();

    public IReadOnlyList<string> OwnedInOrder => order;

    public static Def Find(string id) => System.Array.Find(All, d => d.id == id) ?? System.Array.Find(CollabDefs, d => d.id == id);
    public static bool IsCollab(string id) => System.Array.Exists(CollabDefs, d => d.id == id);

    public int LevelOf(string id) => owned.TryGetValue(id, out Weapon w) ? w.Level : 0;
    public Weapon Get(string id) => owned.TryGetValue(id, out Weapon w) ? w : null;

    public List<Def> Offerable()
    {
        var result = new List<Def>();
        foreach (Def d in All)
        {
            if (PlayerCollabs.IsConsumed(d.id)) continue;
            if (LevelOf(d.id) == 0 && Achievements.IsLocked("weapon:" + d.id)) continue;
            int level = LevelOf(d.id);
            if (level > 0 ? level < Weapon.MaxLevel : owned.Count < MaxWeapons) result.Add(d);
        }
        return result;
    }

    public void LevelUp(Def def)
    {
        Achievements.MarkTried("w", def.id);
        if (!owned.TryGetValue(def.id, out Weapon weapon))
        {
            weapon = (Weapon)gameObject.AddComponent(def.type);
            weapon.DisplayName = def.name;
            owned[def.id] = weapon;
            order.Add(def.id);
        }
        int before = weapon.Level;
        weapon.SetLevel(weapon.Level + 1);
        if (before < Weapon.MaxLevel && weapon.Level == Weapon.MaxLevel) { MaxBurst(def.name); PlayerCollabs.MaxHint(def.id, transform.position); }
    }

    private void MaxBurst(string name)
    {
        Vector2 at = transform.position;
        Color gold = new Color(1f, 0.82f, 0.3f);
        DamagePopup.SpawnText(at + Vector2.up * 1.3f, name.ToUpper() + "\nMAX!", gold, 6f);
        ExplosionFx.Ring(at, 5f, gold, 0.5f);
        Juice.SparksDir(at, Vector2.up, 25f, gold, 20, 11f, 0.18f);
        Lighting.Flash(at, 7f, gold, 0.6f);
        CameraFollow.ZoomPunch(0.25f);
        Sfx.Play(Sfx.Id.Fanfare, 0.55f, 1.15f);
    }

    public void Merge(string a, string b, Def collab)
    {
        int slot = order.IndexOf(a);
        Weapon carried = Get(a);
        foreach (string id in new[] { a, b })
        {
            if (owned.TryGetValue(id, out Weapon w)) Destroy(w);
            owned.Remove(id);
            order.Remove(id);
        }

        var weapon = (Weapon)gameObject.AddComponent(collab.type);
        weapon.DisplayName = collab.name;
        owned[collab.id] = weapon;
        order.Insert(Mathf.Clamp(slot, 0, order.Count), collab.id);
        weapon.SetLevel(Weapon.MaxLevel);
        if (carried != null) weapon.CopyEnhance(carried); // anvil work on the first piece isn't lost
    }

    public void Remove(string id)
    {
        if (!owned.TryGetValue(id, out Weapon w)) return;
        Destroy(w);
        owned.Remove(id);
        order.Remove(id);
    }

    public void Super(List<string> partIds, Def super)
    {
        var parts = new List<Weapon>();
        int slot = int.MaxValue;
        foreach (string id in partIds)
        {
            if (!owned.TryGetValue(id, out Weapon w)) continue;
            slot = Mathf.Min(slot, order.IndexOf(id));
            parts.Add(w);
            owned.Remove(id);
            order.Remove(id);
        }

        var weapon = (SuperWeapon)gameObject.AddComponent(super.type);
        weapon.DisplayName = super.name;
        owned[super.id] = weapon;
        order.Insert(Mathf.Clamp(slot, 0, order.Count), super.id);
        weapon.SetLevel(Weapon.MaxLevel);
        weapon.Adopt(parts);
    }
}
