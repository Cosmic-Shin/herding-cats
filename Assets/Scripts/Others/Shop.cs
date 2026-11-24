using UnityEngine;

// Fish coins: earned at the end of every run, spent in the main-menu shop on permanent upgrades and buddy looks.
public static class Shop
{
    private const string CoinKey = "fish_coins", LookKey = "buddy_look";

    public static int Coins
    {
        get => PlayerPrefs.GetInt(CoinKey, 0);
        private set { PlayerPrefs.SetInt(CoinKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    // ---------- Upgrades ----------

    public class Upgrade
    {
        public string id, name, icon, format; // format: "{0}" = total bonus at a tier
        public float perTier;
        public int max, baseCost;
        public float growth;
        public string Describe(int tier) => string.Format(format, tier * perTier);
    }

    public static readonly Upgrade[] Upgrades =
    {
        new Upgrade { id = "hp",         name = "Max HP",       icon = "stat:MaxHealth",           format = "+{0} max HP",            perTier = 10, max = 10, baseCost = 50,   growth = 1.3f },
        new Upgrade { id = "damage",     name = "Damage",       icon = "stat:AttackDamagePercent", format = "+{0}% damage",           perTier = 5,  max = 5,  baseCost = 120,  growth = 1.5f },
        new Upgrade { id = "speed",      name = "Move Speed",   icon = "stat:MoveSpeedPercent",    format = "+{0}% move speed",       perTier = 4,  max = 5,  baseCost = 80,   growth = 1.45f },
        new Upgrade { id = "pickup",     name = "Pickup Range", icon = "skill:magnet",             format = "+{0}% pickup range",     perTier = 15, max = 3,  baseCost = 60,   growth = 1.5f },
        new Upgrade { id = "xp",         name = "XP Gain",      icon = "skill:scholar",          format = "+{0}% XP",               perTier = 5,  max = 4,  baseCost = 100,  growth = 1.5f },
        new Upgrade { id = "reroll",     name = "Rerolls",      icon = "chest",                   format = "+{0} rerolls every run", perTier = 1,  max = 5,  baseCost = 100,  growth = 1.4f },
        new Upgrade { id = "startlevel", name = "Head Start",   icon = "skill:overdrive",     format = "+{0} starting levels", perTier = 1, max = 5, baseCost = 150,  growth = 1.5f },
        new Upgrade { id = "revive",     name = "Nine Lives",   icon = "skill:secondwind",         format = "Revive once per run",    perTier = 1,  max = 1,  baseCost = 2500, growth = 1f },
    };

    public static Upgrade Find(string id) => System.Array.Find(Upgrades, u => u.id == id);
    public static int Tier(Upgrade u) => Mathf.Clamp(PlayerPrefs.GetInt("shop_" + u.id, 0), 0, u.max);
    public static bool Maxed(Upgrade u) => Tier(u) >= u.max;
    public static int Cost(Upgrade u, int tier) => Mathf.RoundToInt(u.baseCost * Mathf.Pow(u.growth, tier) / 5f) * 5;
    public static int NextCost(Upgrade u) => Maxed(u) ? 0 : Cost(u, Tier(u));
    private static float Bonus(string id) { Upgrade u = Find(id); return Tier(u) * u.perTier; }

    public static bool Buy(Upgrade u)
    {
        int cost = NextCost(u);
        if (Maxed(u) || Coins < cost) return false;
        PlayerPrefs.SetInt("shop_" + u.id, Tier(u) + 1);
        Coins -= cost;
        return true;
    }

    // What every bought upgrade tier cost; Refund hands it all back (looks are not refunded).
    public static int Spent
    {
        get
        {
            int total = 0;
            foreach (Upgrade u in Upgrades) for (int t = 0; t < Tier(u); t++) total += Cost(u, t);
            return total;
        }
    }

    public static void Refund()
    {
        int back = Spent;
        foreach (Upgrade u in Upgrades) PlayerPrefs.DeleteKey("shop_" + u.id);
        Coins += back;
    }

    // ---------- Buddy looks: a coat (color or breed) plus an optional hat, see BuddySprites ----------

    public enum LookKind { Color, Breed, Hat }

    public class Look { public string id, name, trick; public LookKind kind; public int cost; public bool IsHat => kind == LookKind.Hat; }

    public static readonly Look[] Looks =
    {
        // Colors (ids kept from the old recolors, so anything already bought stays owned)
        new Look { id = "ginger",   name = "Ginger",   kind = LookKind.Color, cost = 0 },
        new Look { id = "midnight", name = "Midnight", kind = LookKind.Color, cost = 300 },
        new Look { id = "snowball", name = "Snowball", kind = LookKind.Color, cost = 300 },
        new Look { id = "smoky",    name = "Smoky",    kind = LookKind.Color, cost = 400 },
        new Look { id = "sakura",   name = "Sakura",   kind = LookKind.Color, cost = 500 },
        new Look { id = "mint",     name = "Mint",     kind = LookKind.Color, cost = 500 },
        new Look { id = "lavender", name = "Lavender", kind = LookKind.Color, cost = 600 },
        new Look { id = "golden",   name = "Golden",   kind = LookKind.Color, cost = 1000 },
        // Breeds
        // Breeds also know a small trick, active every run while equipped (ApplyToRun / PayOut).
        new Look { id = "tuxedo",   name = "Tuxedo",   kind = LookKind.Breed, cost = 400, trick = "+5% crit chance" },
        new Look { id = "tabby",    name = "Tabby",    kind = LookKind.Breed, cost = 400, trick = "+15% pickup range" },
        new Look { id = "cow",      name = "Cow Cat",  kind = LookKind.Breed, cost = 500, trick = "+15 max HP" },
        new Look { id = "calico",   name = "Calico",   kind = LookKind.Breed, cost = 700, trick = "+5% XP" },
        new Look { id = "siamese",  name = "Siamese",  kind = LookKind.Breed, cost = 700, trick = "+15% fish coins" },
        // Hats (worn on top of any coat)
        new Look { id = "none",     name = "No Hat",   kind = LookKind.Hat, cost = 0 },
        new Look { id = "party",    name = "Party Hat", kind = LookKind.Hat, cost = 300 },
        new Look { id = "chef",     name = "Chef Hat", kind = LookKind.Hat, cost = 400 },
        new Look { id = "maid",     name = "Maid Band", kind = LookKind.Hat, cost = 500 },
        new Look { id = "wizard",   name = "Wizard",   kind = LookKind.Hat, cost = 600 },
        new Look { id = "frog",     name = "Frog Hood", kind = LookKind.Hat, cost = 600 },
        new Look { id = "knight",   name = "Knight",   kind = LookKind.Hat, cost = 900 },
        new Look { id = "crown",    name = "Crown",    kind = LookKind.Hat, cost = 1500 },
    };

    private const string HatKey = "buddy_hat";

    public static Look[] OfKind(LookKind kind) => System.Array.FindAll(Looks, l => l.kind == kind);
    public static bool Owns(Look l) => l.cost == 0 || PlayerPrefs.GetInt("look_" + l.id, 0) == 1;

    public static Look Equipped => EquippedCoat;
    public static Look EquippedCoat => System.Array.Find(Looks, l => !l.IsHat && l.id == PlayerPrefs.GetString(LookKey, "ginger") && Owns(l)) ?? Looks[0];
    public static Look EquippedHat => System.Array.Find(Looks, l => l.IsHat && l.id == PlayerPrefs.GetString(HatKey, "none") && Owns(l));
    public static bool IsEquipped(Look l) => l.IsHat ? (EquippedHat ?? FindLook("none")) == l : EquippedCoat == l;
    private static Look FindLook(string id) => System.Array.Find(Looks, l => l.id == id);

    public static bool BuyLook(Look l)
    {
        if (Owns(l) || Coins < l.cost) return false;
        PlayerPrefs.SetInt("look_" + l.id, 1);
        Coins -= l.cost;
        Equip(l);
        return true;
    }

    public static void Equip(Look l)
    {
        if (!Owns(l)) return;
        PlayerPrefs.SetString(l.IsHat ? HatKey : LookKey, l.id);
        PlayerPrefs.Save();
    }

    // ---------- Weapons: locked out of the level-up pool until bought (one-time, not refunded) ----------

    public class ShopWeapon { public string id; public int cost; }

    public static readonly ShopWeapon[] Weapons =
    {
        new ShopWeapon { id = "hairball", cost = 600 },
        new ShopWeapon { id = "sardine",  cost = 800 },
    };

    public static bool OwnsWeapon(ShopWeapon w) => PlayerPrefs.GetInt("weapon_" + w.id, 0) == 1;

    public static bool BuyWeapon(ShopWeapon w)
    {
        if (OwnsWeapon(w) || Coins < w.cost) return false;
        PlayerPrefs.SetInt("weapon_" + w.id, 1);
        Coins -= w.cost;
        return true;
    }

    public static ShopWeapon WeaponFor(string reward) =>
        reward != null && reward.StartsWith("weapon:") ? System.Array.Find(Weapons, w => "weapon:" + w.id == reward) : null;

    // Used by Achievements.IsLocked, so the level-up pool and wiki treat unbought shop weapons as locked.
    public static bool WeaponLocked(string reward) => WeaponFor(reward) is ShopWeapon w && !OwnsWeapon(w);

    // ---------- In a run ----------

    private static bool inRun, reviveUsed, paid;
    public static int LastPayout { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { inRun = reviveUsed = paid = false; LastPayout = 0; }

    // Called once when a run actually starts playing (PlayerStats waits for that).
    public static void ApplyToRun(PlayerStats s)
    {
        inRun = true;
        reviveUsed = paid = false;
        LastPayout = 0;

        s.IncreaseMaxHealth(Bonus("hp"));
        s.attackDamageMultiplier += Bonus("damage") / 100f;
        s.moveSpeedMultiplier += Bonus("speed") / 100f;
        s.pickupRange *= 1f + Bonus("pickup") / 100f;
        s.expMultiplier += Bonus("xp") / 100f;
        if (s.TryGetComponent(out LevelUpUIController levelUp)) levelUp.rerolls += Mathf.RoundToInt(Bonus("reroll"));
        ApplyBuddyTrick(s);
        s.GrantLevels(Mathf.RoundToInt(Bonus("startlevel")));
    }

    private static string Breed => EquippedCoat.kind == LookKind.Breed ? EquippedCoat.id : null;

    private static void ApplyBuddyTrick(PlayerStats s)
    {
        switch (Breed)
        {
            case "tuxedo": s.critChance += 0.05f; break;
            case "tabby": s.pickupRange *= 1.15f; break;
            case "cow": s.IncreaseMaxHealth(15f); break;
            case "calico": s.expMultiplier += 0.05f; break;
        }
    }

    public static bool TryRevive()
    {
        if (!inRun || reviveUsed || Tier(Find("revive")) == 0) return false;
        reviveUsed = true;
        return true;
    }

    // Fish for the run that just ended. Safe to call more than once: only the first call pays.
    public static int PayOut()
    {
        if (!inRun || paid) return LastPayout;
        paid = true;
        inRun = false;
        if (AdminTool.GodMode) return LastPayout = 0;

        HUD hud = Object.FindFirstObjectByType<HUD>();
        EnemySpawner spawner = Object.FindFirstObjectByType<EnemySpawner>();
        float seconds = hud != null ? hud.RunTime : 0f;
        float endless = spawner != null ? spawner.EndlessMinutes : 0f;
        LastPayout = Earned(seconds, EnemyHealth.Kills, EnemyHealth.BossKills, endless);
        if (Breed == "siamese") LastPayout = Mathf.RoundToInt(LastPayout * 1.15f);
        Coins += LastPayout;
        return LastPayout;
    }

    // 1 per 10 s survived, 1 per 40 cats, 20 per boss, +5 per endless minute. A full 20-minute clear is roughly 250-300.
    public static int Earned(float seconds, int kills, int bosses, float endlessMinutes) =>
        Mathf.FloorToInt(seconds / 10f) + kills / 40 + bosses * 20 + Mathf.FloorToInt(endlessMinutes * 5f);
}
