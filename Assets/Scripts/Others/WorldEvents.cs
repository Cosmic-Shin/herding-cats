using System.Collections;
using UnityEngine;

// Friendly map events on top of the spawner's hostile ones:
// Golden Hour (double XP), Meteor Shower (meteors smash cats), and buff shrines you stand in to activate.
public class WorldEvents : MonoBehaviour
{
    public float firstEventTime = 120f;
    public Vector2 eventGap = new Vector2(100f, 140f);
    public float firstShrineTime = 60f;
    public float shrineInterval = 70f;

    private static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
    private Transform player;
    private PlayerStats stats;
    private BossTimeline timeline;
    private EnemySpawner spawner;
    private float eventTimer, shrineTimer, weatherTimer = 150f;
    private bool goldenNext = true;
    private Shrine shrine;

    private void Start()
    {
        spawner = GetComponent<EnemySpawner>();
        timeline = FindFirstObjectByType<BossTimeline>();
        stats = FindFirstObjectByType<PlayerStats>();
        if (stats != null) player = stats.transform;
        eventTimer = firstEventTime;
        shrineTimer = firstShrineTime;
    }

    private void Update()
    {
        if (player == null || stats.currentHealth <= 0f || !MainMenu.Started) return;
        if ((eventTimer -= Time.deltaTime) <= 0f)
        {
            eventTimer = Random.Range(eventGap.x, eventGap.y);
            StartCoroutine(goldenNext ? GoldenHour() : MeteorShower());
            goldenNext = !goldenNext;
        }
        if ((weatherTimer -= Time.deltaTime) <= 0f && !EnemySpawner.Endless)
        {
            weatherTimer = Random.Range(150f, 210f);
            StartCoroutine(Weather());
        }
        if (shrine == null && (shrineTimer -= Time.deltaTime) <= 0f)
        {
            shrineTimer = shrineInterval;
            SpawnShrine();
        }
    }

    private void Announce(string title, string subtitle, Color color)
    {
        if (timeline != null) timeline.Announce(title, subtitle);
        else DamagePopup.SpawnText((Vector2)player.position + Vector2.up * 1.6f, title, color, 8f);
    }

    // ---- Golden Hour: 30s of double XP, with gold sparkles around you ----
    private IEnumerator GoldenHour()
    {
        Announce("GOLDEN HOUR", "double fish for 30 seconds", Gold);
        Juice.Flash(new Color(1f, 0.85f, 0.4f, 0.3f), 0.5f);
        Sfx.Play(Sfx.Id.Fanfare, 0.5f, 1.2f);
        stats.expMultiplier *= 2f;
        Lighting.Mood = new Color(1f, 0.92f, 0.72f);
        for (float t = 0f; t < 30f; t += 0.25f)
        {
            for (int i = 0; i < 3; i++)
                Juice.SparksDir((Vector2)player.position + Random.insideUnitCircle * 3.5f, Vector2.up, 25f, Gold, 1, 0.8f, 0.12f);
            yield return new WaitForSeconds(0.25f);
        }
        stats.expMultiplier /= 2f;
        Lighting.Mood = null;
        DamagePopup.SpawnText((Vector2)player.position + Vector2.up * 1.2f, "golden hour over", Gold, 4f);
    }

    // ---- Weather: a ~40s storm that suits the map (rainstorm / blizzard / ash storm). Just looks, no gameplay change ----
    private IEnumerator Weather()
    {
        string sub = MapGenerator.Winter ? "you can barely see a thing" : MapGenerator.Volcano ? "the sky is raining ash" : "the cats hate getting wet";
        Color color = MapGenerator.Winter ? new Color(0.8f, 0.92f, 1f) : MapGenerator.Volcano ? new Color(1f, 0.5f, 0.3f) : new Color(0.6f, 0.75f, 1f);
        Announce(Ambience.WeatherName, sub, color);
        Sfx.Play(Sfx.Id.Whoosh, 0.5f, 0.5f);
        for (float t = 0f; t < 4f; t += Time.deltaTime) { Ambience.WeatherLevel = t / 4f; yield return null; }
        Ambience.WeatherLevel = 1f;
        yield return new WaitForSeconds(32f);
        for (float t = 0f; t < 5f; t += Time.deltaTime) { Ambience.WeatherLevel = 1f - t / 5f; yield return null; }
        Ambience.WeatherLevel = 0f;
    }

    // ---- Meteor Shower: marked meteors rain around you for 12s; they only hurt cats ----
    private IEnumerator MeteorShower()
    {
        Announce("METEOR SHOWER", "rocks from the sky. cats only, relax", new Color(1f, 0.5f, 0.25f));
        Sfx.Play(Sfx.Id.Alert, 0.6f, 0.8f);
        for (float t = 0f; t < 12f; t += 0.35f)
        {
            Vector2 at = (Vector2)player.position + Random.insideUnitCircle * 7f;
            StartCoroutine(Meteor(at));
            yield return new WaitForSeconds(0.35f);
        }
    }

    private IEnumerator Meteor(Vector2 at)
    {
        const float fall = 0.9f, radius = 1.4f;
        AoeWarning.Spawn(at, radius, fall);
        var rock = new GameObject("Meteor").AddComponent<SpriteRenderer>();
        rock.sprite = Shapes.Projectile("Icons/armageddon");
        rock.sortingOrder = 60;
        rock.transform.localScale = Vector3.one * 0.9f;
        Lighting.Glow(rock);
        Vector2 from = at + new Vector2(-3f, 7f);
        Vector2 fallDir = at - from;
        rock.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(fallDir.y, fallDir.x) * Mathf.Rad2Deg - 45f);
        for (float t = 0f; t < fall; t += Time.deltaTime)
        {
            rock.transform.position = Vector2.Lerp(from, at, t / fall);
            Juice.Sparks(rock.transform.position, new Color(1f, 0.7f, 0.3f, 0.8f), 1, 0.5f, 0.14f);
            yield return null;
        }
        Destroy(rock.gameObject);
        ExplosionFx.Spawn(at, radius, new Color(1f, 0.55f, 0.25f));
        Lighting.Flash(at, radius * 3f, new Color(1f, 0.6f, 0.3f), 0.3f);
        CameraFollow.Shake(0.06f, 0.08f);
        Sfx.Play(Sfx.Id.Boom, 0.35f, 1.2f);
        DamageStats.Source = "Meteor Shower";
        Bomb.Blast(at, radius, stats.AttackDamage * 3f, stats.critChance, stats.critDamage, true);
    }

    // ---- Shrines: stand inside for 3s to claim a 30s buff (or an instant one) ----
    private enum Boon { Fury, Swift, Magnet, Heal }
    private static readonly Color[] BoonColors = { new Color(1f, 0.35f, 0.3f), new Color(0.4f, 0.8f, 1f), new Color(0.8f, 0.45f, 1f), new Color(0.45f, 1f, 0.5f) };
    private static readonly string[] BoonNames = { "FURY", "SWIFTNESS", "MAGNET", "HEALING" };
    private static readonly string[] BoonText = { "+40% damage for 30s", "+30% move speed for 30s", "every fish flies to you", "restores 40% HP" };

    private void SpawnShrine()
    {
        Vector2 at = player.position;
        for (int i = 0; i < 12; i++)
        {
            Vector2 p = (Vector2)player.position + Random.insideUnitCircle.normalized * Random.Range(5f, 7f);
            if (Physics2D.OverlapCircle(p, 0.8f, MapGenerator.ObstacleMask) == null) { at = p; break; }
        }
        var boon = (Boon)Random.Range(0, 4);
        if (boon == Boon.Heal && stats.currentHealth > stats.maxHealth * 0.8f) boon = Boon.Fury;
        shrine = new GameObject("Shrine").AddComponent<Shrine>();
        shrine.transform.position = at;
        shrine.Init(this, boon, BoonColors[(int)boon]);
        OffscreenArrows.Track(shrine.transform, BoonColors[(int)boon]);
    }

    private void Claim(Boon boon, Vector2 at)
    {
        Color c = BoonColors[(int)boon];
        Announce("SHRINE OF " + BoonNames[(int)boon], BoonText[(int)boon], c);
        ExplosionFx.Ring(at, 3f, c, 0.4f);
        Juice.SparksDir(at, Vector2.up, 30f, c, 20, 10f, 0.16f);
        Lighting.Flash(at, 6f, c, 0.5f);
        Sfx.Play(Sfx.Id.Fanfare, 0.5f);
        switch (boon)
        {
            case Boon.Fury: StartCoroutine(Buff(v => stats.attackDamageMultiplier += v, 0.4f, c)); break;
            case Boon.Swift: StartCoroutine(Buff(v => stats.moveSpeedMultiplier += v, 0.3f, c)); break;
            case Boon.Magnet: if (XPCollectorManager.Instance != null) XPCollectorManager.Instance.MagnetizeNear(player.position, 999f); break;
            case Boon.Heal: stats.Heal(stats.maxHealth * 0.4f); break;
        }
    }

    // Adds a stat bonus for 30s, with a trickle of coloured sparks so you can tell it's active.
    private IEnumerator Buff(System.Action<float> apply, float amount, Color c)
    {
        apply(amount);
        for (float t = 0f; t < 30f; t += 0.3f)
        {
            if (player != null) Juice.Sparks((Vector2)player.position + Random.insideUnitCircle * 0.4f, c, 1, 1f, 0.1f);
            yield return new WaitForSeconds(0.3f);
        }
        apply(-amount);
    }

    private class Shrine : MonoBehaviour
    {
        private const float Radius = 1.3f, ClaimTime = 3f, Lifetime = 45f;
        private WorldEvents owner;
        private Boon boon;
        private Color color;
        private SpriteRenderer orb, ring, fill;
        private float progress, age;

        public void Init(WorldEvents owner, Boon boon, Color color)
        {
            this.owner = owner;
            this.boon = boon;
            this.color = color;
            Layer("Stone", Shapes.ShrineStone, Color.white, 4, Vector2.zero, 1f);
            ring = Layer("Ring", Shapes.Ring, color, 3, Vector2.zero, Radius * 2f);
            fill = Layer("Fill", Shapes.Circle, color, 2, Vector2.zero, 0f);
            orb = Layer("Orb", Shapes.Circle, color, 5, new Vector2(0f, 0.75f), 0.35f);
            Lighting.Glow(orb);
            Juice.Sparks(transform.position, color, 12, 6f, 0.14f);
            Sfx.Play(Sfx.Id.Ready, 0.5f, 0.9f);
        }

        private SpriteRenderer Layer(string name, Sprite sprite, Color c, int order, Vector2 offset, float scale)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.transform.localPosition = offset;
            sr.transform.localScale = Vector3.one * scale;
            sr.sprite = sprite;
            sr.color = c;
            sr.sortingOrder = order;
            return sr;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age > Lifetime) { Juice.Sparks(transform.position, color, 8, 3f, 0.12f); Destroy(gameObject); return; }

            bool inside = owner.player != null && Vector2.Distance(owner.player.position, transform.position) <= Radius;
            progress = Mathf.Clamp(progress + (inside ? Time.deltaTime : -Time.deltaTime * 2f), 0f, ClaimTime);
            float k = progress / ClaimTime;
            fill.transform.localScale = Vector3.one * Radius * 2f * k;
            fill.color = new Color(color.r, color.g, color.b, 0.25f);
            ring.color = new Color(color.r, color.g, color.b, 0.5f + 0.4f * Mathf.Sin(age * 4f));
            orb.transform.localPosition = new Vector2(0f, 0.75f + 0.08f * Mathf.Sin(age * 3f));
            orb.transform.localScale = Vector3.one * (0.35f + 0.15f * k);
            if (inside && Random.value < 0.3f) Juice.SparksDir((Vector2)transform.position + Random.insideUnitCircle * Radius, Vector2.up, 20f, color, 1, 2f, 0.1f);

            if (progress >= ClaimTime)
            {
                owner.Claim(boon, transform.position);
                Destroy(gameObject);
            }
        }
    }
}
