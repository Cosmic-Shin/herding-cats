using System.Collections;
using UnityEngine;

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
        OffscreenArrows.Track(shrine.transform, BoonColors[(int)boon], Shapes.BoonIcon((int)boon));
    }

    private void Claim(Boon boon, Vector2 at)
    {
        Color c = BoonColors[(int)boon];
        Announce($"<color=#{ColorUtility.ToHtmlStringRGB(c)}>SHRINE OF {BoonNames[(int)boon]}", BoonText[(int)boon], c);
        ExplosionFx.Ring(at, 3f, c, 0.4f);
        Juice.SparksDir(at, Vector2.up, 30f, c, 20, 10f, 0.16f);
        Lighting.Flash(at, 6f, c, 0.5f);
        Sfx.Play(Sfx.Id.Fanfare, 0.5f);
        Vector2 me = player.position;
        switch (boon)
        {
            case Boon.Fury:
                foreach (Collider2D col in Bomb.OverlapEnemies(me, 4f, hits))
                    if (col.TryGetComponent(out IEnemyBrain brain)) brain.Knockback(((Vector2)col.transform.position - me).normalized * 14f);
                ExplosionFx.Ring(me, 4f, c, 0.35f);
                ExplosionFx.Ring(me, 2.5f, Color.Lerp(c, Color.white, 0.4f), 0.25f);
                CameraFollow.Shake(0.15f, 0.2f);
                StartCoroutine(Buff(boon, v => stats.attackDamageMultiplier += v, 0.4f, c));
                break;
            case Boon.Swift:
                Juice.SparksDir(me, Vector2.up, 180f, c, 24, 9f, 0.1f);
                Sfx.Play(Sfx.Id.Whoosh, 0.6f, 1.2f);
                StartCoroutine(Buff(boon, v => stats.moveSpeedMultiplier += v, 0.3f, c));
                break;
            case Boon.Magnet:
                for (int i = 0; i < 20; i++)
                {
                    Vector2 dir = Random.insideUnitCircle.normalized;
                    Juice.SparksDir(me + dir * Random.Range(3.5f, 5f), -dir, 4f, c, 1, 12f, 0.12f);
                }
                Sfx.Play(Sfx.Id.Whoosh, 0.6f, 0.8f);
                if (XPCollectorManager.Instance != null) XPCollectorManager.Instance.MagnetizeNear(player.position, 999f);
                break;
            case Boon.Heal:
                ExplosionFx.Crosses(me, 1.4f, c, 10, 0.9f);
                ExplosionFx.Ring(me, 1.8f, c, 0.4f);
                Juice.Flash(new Color(c.r, c.g, c.b, 0.2f), 0.35f);
                Sfx.Play(Sfx.Id.Sparkle, 0.6f, 1.1f);
                stats.Heal(stats.maxHealth * 0.4f);
                break;
        }
    }

    private readonly System.Collections.Generic.List<Collider2D> hits = new System.Collections.Generic.List<Collider2D>();
    private const float BuffTime = 30f;

    private IEnumerator Buff(Boon boon, System.Action<float> apply, float amount, Color c)
    {
        apply(amount);
        HUD.ShowBuff(Shapes.BoonIcon((int)boon), c, BuffTime);
        var paws = new SpriteRenderer[5];
        for (int i = 0; i < paws.Length; i++)
        {
            paws[i] = new GameObject("BuffPaw").AddComponent<SpriteRenderer>();
            paws[i].sprite = Shapes.PawPrint;
            paws[i].sortingOrder = 1;
            Lighting.Glow(paws[i]);
        }
        Vector2 last = player != null ? (Vector2)player.position : Vector2.zero;
        for (float t = 0f; t < BuffTime; t += Time.deltaTime)
        {
            if (player != null)
            {
                Vector2 pos = player.position;
                float left = BuffTime - t;
                float alpha = left > 3f || Mathf.Repeat(left, 0.4f) > 0.15f ? 0.9f : 0.25f;
                for (int i = 0; i < paws.Length; i++)
                {
                    float a = t * 1.2f + i * Mathf.PI * 2f / paws.Length;
                    paws[i].transform.position = pos + new Vector2(Mathf.Cos(a) * 0.75f, Mathf.Sin(a) * 0.3f - 0.4f);
                    paws[i].color = new Color(c.r, c.g, c.b, alpha * (Mathf.Sin(a) > 0f ? 0.55f : 1f));
                }
                Vector2 moved = pos - last;
                last = pos;
                if (boon == Boon.Swift && moved.sqrMagnitude > 0.0001f && Random.value < 0.6f)
                    Juice.SparksDir(pos + Random.insideUnitCircle * 0.35f, -moved.normalized, 8f, Color.Lerp(c, Color.white, 0.4f), 2, 7f, 0.13f);
                else if (boon == Boon.Fury && Random.value < Time.deltaTime * 12f)
                    Juice.SparksDir(pos + Random.insideUnitCircle * 0.4f, Vector2.up, 25f, Color.Lerp(c, new Color(1f, 0.7f, 0.2f), Random.value), 1, 2f, 0.1f);
            }
            yield return null;
        }
        foreach (SpriteRenderer p in paws) Destroy(p.gameObject);
        apply(-amount);
    }

    private class Shrine : MonoBehaviour
    {
        private const float Radius = 1.3f, ClaimTime = 3f, Lifetime = 45f, StatueY = 1.75f, IconY = 3f, BlinkFrom = 5f, BurstTime = 0.6f;
        private const int Stones = 7;
        private WorldEvents owner;
        private Boon boon;
        private Color color;
        private SpriteRenderer statue, carving, icon, shadow;
        private readonly SpriteRenderer[] pavers = new SpriteRenderer[Stones], runes = new SpriteRenderer[Stones];
        private UnityEngine.Rendering.Universal.Light2D glowLight;
        private AudioSource hum;
        private readonly System.Collections.Generic.List<SpriteRenderer> all = new System.Collections.Generic.List<SpriteRenderer>();
        private float progress, age, claimedAt = -1f;
        private int lit;

        private Color Tint(float a) { Color l = Color.Lerp(color, Color.white, 0.4f); l.a = a; return l; }
        private Vector2 Pos => transform.position;

        public void Init(WorldEvents owner, Boon boon, Color color)
        {
            this.owner = owner;
            this.boon = boon;
            this.color = color;
            int biome = MapGenerator.Biome;
            for (int i = 0; i < Stones; i++)
            {
                float a = Mathf.PI / 2f - (i + 1) * Mathf.PI / 4f;
                Vector2 at = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Radius;
                pavers[i] = Layer("Paver", Shapes.ShrinePaver(biome), Color.white, 1, at, 1f, false);
                runes[i] = Layer("PaverRune", Shapes.ShrinePaverRune, Tint(0.25f), 2, at, 1f, true);
            }
            statue = Layer("Statue", Shapes.ShrineStatue(biome), Color.white, 4, new Vector2(0f, StatueY), 1f, false);
            shadow = SpriteFx.AddShadow(statue, 1.1f);
            all.Add(shadow);
            carving = Layer("Carving", Shapes.ShrineStatueRune, color, 5, new Vector2(0f, StatueY), 1f, true);
            icon = Layer("Icon", Shapes.BoonIcon((int)boon), Color.Lerp(color, Color.white, 0.35f), 6, new Vector2(0f, IconY), 0.6f, true);
            glowLight = Lighting.MakeLight("ShrineLight", statue.transform, 2.5f, 0.5f, color);
            hum = gameObject.AddComponent<AudioSource>();
            hum.clip = Sfx.GetClip(Sfx.Id.Hum);
            hum.loop = true;
            hum.volume = 0f;
            hum.Play();
            transform.localScale = Vector3.zero;

            Juice.Smoke(Pos, 1.6f, 8);
            Juice.SparksDir(Pos, Vector2.up, 40f, color, 12, 6f, 0.14f);
            Sfx.Play(Sfx.Id.Ready, 0.5f, 0.9f);
        }

        private SpriteRenderer Layer(string name, Sprite sprite, Color c, int order, Vector2 offset, float scale, bool lit)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.transform.localPosition = offset;
            sr.transform.localScale = Vector3.one * scale;
            sr.sprite = sprite;
            sr.color = c;
            sr.sortingOrder = order;
            if (lit) Lighting.Glow(sr);
            all.Add(sr);
            return sr;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (claimedAt >= 0f) { Burst((age - claimedAt) / BurstTime); return; }
            if (age > Lifetime)
            {
                Juice.Smoke(Pos, 1.4f, 6);
                Sfx.Play(Sfx.Id.Blink, 0.4f, 0.7f);
                Destroy(gameObject);
                return;
            }

            float r = Mathf.Clamp01(age / 0.45f) - 1f;
            transform.localScale = Vector3.one * (1f + 2.7f * r * r * r + 1.7f * r * r);

            float left = Lifetime - age;
            bool show = left > BlinkFrom || Mathf.Repeat(age, left > 2f ? 0.3f : 0.14f) < (left > 2f ? 0.2f : 0.08f);
            foreach (SpriteRenderer sr in all) sr.enabled = show;

            bool inside = owner.player != null && Vector2.Distance(owner.player.position, Pos) <= Radius;
            progress = Mathf.Clamp(progress + (inside ? Time.deltaTime : -Time.deltaTime * 2f), 0f, ClaimTime);
            float k = progress / ClaimTime;

            int n = Mathf.FloorToInt(k * Stones);
            if (n > lit && n < Stones)
            {
                Sfx.Play(Sfx.Id.Tick, 0.7f, 1f + 0.08f * n);
                Juice.SparksDir((Vector2)runes[n - 1].transform.position, Vector2.up, 40f, color, 3, 3f, 0.1f);
            }
            lit = n;

            for (int i = 0; i < Stones; i++)
            {
                float idle = 0.65f + 0.35f * Mathf.Max(0f, Mathf.Sin(age * 3f - i * 0.8f));
                float fill = Mathf.Clamp01(k * Stones - i);
                runes[i].color = Color.Lerp(Tint(idle), Color.Lerp(color, Color.white, 0.8f), fill);
                pavers[i].color = Color.Lerp(Color.white, Color.Lerp(color, Color.white, 0.45f), fill);
            }
            carving.color = Tint(0.8f + 0.2f * Mathf.Sin(age * 2.2f));
            icon.transform.localPosition = new Vector2(0f, IconY + 0.07f * Mathf.Sin(age * 2.5f)) + Random.insideUnitCircle * 0.04f * k * k;
            glowLight.intensity = (0.45f + 0.1f * Mathf.Sin(age * 3f) + 0.9f * k) * (show ? 1f : 0.3f);
            float near = owner.player != null ? Mathf.Clamp01(1f - Vector2.Distance(owner.player.position, Pos) / 8f) : 0f;
            hum.volume = Sfx.Volume * 0.35f * near * near * (0.6f + 0.6f * k) * Mathf.Clamp01(left);
            hum.pitch = 1f + 0.6f * k;

            if (inside && Random.value < Time.deltaTime * 20f)
                Juice.SparksDir(Pos + Random.insideUnitCircle.normalized * Radius, Vector2.up, 15f, color, 1, 2.5f, 0.1f);

            if (progress >= ClaimTime)
            {
                owner.Claim(boon, Pos);
                claimedAt = age;
                foreach (SpriteRenderer sr in all) sr.enabled = true;
                transform.localScale = Vector3.one;
                foreach (SpriteRenderer rune in runes) Juice.SparksDir((Vector2)rune.transform.position, Vector2.up, 50f, color, 3, 5f, 0.12f);
                CameraFollow.Shake(0.08f, 0.15f);
                CameraFollow.ZoomPunch(0.05f);
            }
        }

        private void Burst(float t)
        {
            if (t >= 1f) { Destroy(gameObject); return; }
            float fade = 1f - t;
            foreach (SpriteRenderer sr in all)
            {
                if (sr == carving || System.Array.IndexOf(runes, sr) >= 0) sr.color = Color.Lerp(Color.white, Tint(0f), t);
                else sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, sr == shadow ? 0.3f * fade : fade);
            }
            icon.transform.localPosition = new Vector2(0f, IconY + 2.5f * t * t);
            glowLight.intensity = 3f * fade * fade;
            hum.volume *= 0.85f;
        }
    }
}
