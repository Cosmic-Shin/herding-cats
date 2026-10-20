using System.Collections.Generic;
using UnityEngine;

public class BurningGround : MonoBehaviour
{
    private const float TickInterval = 0.5f;
    private const float FadeTime = 0.3f;

    private static readonly Stack<BurningGround> free = new Stack<BurningGround>();
    private static readonly List<Collider2D> hits = new List<Collider2D>(32);

    private SpriteRenderer sr;
    private float radius;
    private float tickDamage;
    private float life;
    private float tickTimer;
    private Color baseColor;
    private float slow;
    private bool isFire;
    private float pull;
    private bool magnetGems;
    private float healPlayer;
    private static PlayerStats player;
    private string source;
    private bool lava;
    private Material litMat, glowMat;
    private bool glue;
    private int palette;
    private int lavaVariant;
    private float lavaTime, age, scale;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => free.Clear();

    public static void Spawn(Vector2 pos, float radius, float duration, float tickDamage, Color? color = null, float slow = 0f,
                             bool burns = false, float pull = 0f, bool magnetGems = false, float healPlayer = 0f)
    {
        BurningGround fire = null;
        while (free.Count > 0 && fire == null) fire = free.Pop();
        if (fire != null)
        {
            fire.gameObject.SetActive(true);
        }
        else
        {
            var go = new GameObject("Napalm");
            fire = go.AddComponent<BurningGround>();
            fire.sr = go.AddComponent<SpriteRenderer>();
            fire.litMat = fire.sr.sharedMaterial;
            Lighting.Glow(fire.sr);
            fire.glowMat = fire.sr.sharedMaterial;
            fire.sr.sprite = ExplosionFx.GetCircle();
        }

        fire.transform.position = pos;
        fire.glue = color != null && slow > 0f && !burns;
        bool tar = color != null && slow > 0f && burns;
        fire.palette = fire.glue ? LavaArt.GluePal : tar ? LavaArt.TarPal : LavaArt.LavaPal;
        fire.lava = color == null || fire.glue || tar;
        fire.sr.sharedMaterial = fire.glue ? fire.litMat : fire.glowMat;
        fire.age = 0f;
        if (fire.lava)
        {
            fire.lavaVariant = Random.Range(0, LavaArt.Variants);
            fire.lavaTime = Random.value * 10f;
            fire.transform.rotation = Quaternion.Euler(0f, 0f, 90f * Random.Range(0, 4));
            fire.scale = radius * 2f * 1.2f;
            fire.sr.sortingOrder = -85;
        }
        else
        {
            fire.sr.sprite = ExplosionFx.GetCircle();
            fire.sr.sortingOrder = 2;
            fire.transform.rotation = Quaternion.identity;
            fire.scale = radius * 2f;
        }
        fire.transform.localScale = Vector3.one * fire.scale;
        fire.radius = radius;
        fire.tickDamage = tickDamage;
        fire.life = duration;
        fire.tickTimer = slow > 0f ? 0f : TickInterval;
        fire.baseColor = color ?? new Color(1f, 0.45f, 0.1f);
        fire.slow = slow;
        fire.isFire = color == null || burns;
        fire.pull = pull;
        fire.magnetGems = magnetGems;
        fire.healPlayer = healPlayer;
        fire.source = DamageStats.Source;
    }

    private void UpdateLava(float fade)
    {
        age += Time.deltaTime;
        lavaTime += Time.deltaTime;
        sr.sprite = LavaArt.Get(lavaVariant, (int)(lavaTime * (palette == LavaArt.LavaPal ? 9f : 5f)), radius, palette);
        float grow = Mathf.Clamp01(age / 0.15f);
        transform.localScale = Vector3.one * scale * Mathf.Lerp(0.55f, 1f, 1f - (1f - grow) * (1f - grow)) * Mathf.Lerp(0.7f, 1f, fade);
        sr.color = glue ? new Color(1f, 1f, 1f, 0.9f * fade * Settings.EffectAlpha)
                        : new Color(1f, Mathf.Lerp(0.55f, 1f, fade), Mathf.Lerp(0.5f, 1f, fade), 0.92f * fade * Settings.EffectAlpha);
        if (glue || Settings.LowParticles || Random.value > Time.deltaTime * (2f + radius * 2f)) return;
        Vector2 at = (Vector2)transform.position + Random.insideUnitCircle * radius * 0.8f;
        Juice.SparksDir(at, Vector2.up, 25f, Random.value < 0.5f ? new Color(1f, 0.6f, 0.15f) : new Color(1f, 0.85f, 0.35f), 1, 1.2f, 0.08f);
    }

    private void Update()
    {
        life -= Time.deltaTime;
        tickTimer -= Time.deltaTime;

        float fade = Mathf.Clamp01(life / FadeTime);
        if (lava) UpdateLava(fade);
        else
        {
            float flicker = 0.8f + 0.2f * Mathf.Sin(Time.time * 25f + transform.position.x);
            sr.color = new Color(baseColor.r, baseColor.g * flicker, baseColor.b, 0.35f * flicker * fade * Settings.EffectAlpha);
        }

        if (pull > 0f)
        {
            foreach (Collider2D hit in Bomb.OverlapEnemies(transform.position, radius * 1.3f, hits))
            {
                Vector2 to = (Vector2)transform.position - (Vector2)hit.transform.position;
                if (to.sqrMagnitude > 0.04f && hit.TryGetComponent(out EnemyAI cat)) cat.Pull(to.normalized * pull);
            }
        }

        if (tickTimer <= 0f)
        {
            tickTimer = TickInterval;
            DamageStats.Source = source;
            Vector2 pos = transform.position;
            if (magnetGems && XPCollectorManager.Instance != null) XPCollectorManager.Instance.MagnetizeNear(pos, radius * 1.5f);
            if (healPlayer > 0f)
            {
                if (player == null) player = FindFirstObjectByType<PlayerStats>();
                if (player != null && player.currentHealth > 0f && Vector2.Distance(player.transform.position, pos) <= radius)
                {
                    player.Heal(healPlayer);
                    Juice.Sparks(player.transform.position, new Color(0.5f, 1f, 0.5f), 2, 1.5f, 0.12f);
                }
            }
            foreach (Collider2D hit in Bomb.OverlapEnemies(pos, radius, hits))
            {
                if (hit.TryGetComponent(out EnemyHealth enemy))
                {
                    if (isFire) enemy.MarkBurning();
                    if (tickDamage > 0f) enemy.TakeDamage(tickDamage, pos, false, knockback: false);
                }
                if (slow > 0f && hit.TryGetComponent(out IEnemyBrain brain)) brain.Slow(slow, TickInterval + 0.15f);
            }
        }

        if (life <= 0f)
        {
            gameObject.SetActive(false);
            free.Push(this);
        }
    }
}
