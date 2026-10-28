using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    private const float Lifetime = 4f;

    private Vector2 velocity;
    private float damage;
    private float hitRadius;
    private PlayerHealth target;

    private bool toxic, needle;
    private SpriteRenderer sr;
    private float trailTimer;
    private static int trailFrame, trailCount;
    private static bool TrailBudget()
    {
        if (trailFrame != Time.frameCount) { trailFrame = Time.frameCount; trailCount = 0; }
        return trailCount++ < 12;
    }

    public static void SpawnNeedle(Vector2 pos, Vector2 velocity, float damage, PlayerHealth target)
    {
        EnemyProjectile p = Spawn(pos, velocity, damage, target, 0.25f, Color.white);
        p.needle = true;
        p.sr.sprite = Shapes.FishBone;
        p.transform.localScale = Vector3.one * 0.6f;
        p.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
    }
    private float age;
    private const float ToxicRange = 1.4f;

    public static void SpawnToxic(Vector2 pos, Vector2 velocity, float damage, PlayerHealth target) =>
        Spawn(pos, velocity, damage, target, 0.3f, new Color(0.5f, 0.95f, 0.3f)).toxic = true;

    public static EnemyProjectile Spawn(Vector2 pos, Vector2 velocity, float damage, PlayerHealth target, float size = 0.25f, Color? color = null)
    {
        var go = new GameObject("CatSpit");
        go.transform.position = pos;
        go.transform.localScale = new Vector3(size, size, 1f) * 1.35f;
        var sr = go.AddComponent<SpriteRenderer>();
        Lighting.Glow(sr);
        sr.sprite = Shapes.Glob;
        sr.color = color ?? new Color(1f, 0.6f, 0.15f);
        sr.sortingOrder = 15;

        var p = go.AddComponent<EnemyProjectile>();
        p.sr = sr;
        p.velocity = velocity;
        p.damage = damage;
        p.hitRadius = 0.1f + size;
        p.target = target;
        Destroy(go, Lifetime);
        return p;
    }

    private const float HairballRadius = 0.9f;
    private bool lob;
    private Vector2 from, landAt;
    private float flightTime, t, splitDamage;
    private int split;

    public static void SpawnHairball(Vector2 pos, Vector2 target, float flightTime, float damage, int split, float splitDamage, PlayerHealth player)
    {
        var go = new GameObject("Hairball");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        Lighting.Glow(sr);
        sr.sprite = Shapes.Hairball;
        sr.sortingOrder = 16;

        var p = go.AddComponent<EnemyProjectile>();
        p.sr = sr;
        p.lob = true;
        p.from = pos;
        p.landAt = target;
        p.flightTime = flightTime;
        p.damage = damage;
        p.split = split;
        p.splitDamage = splitDamage;
        p.target = player;
        AoeWarning.Spawn(target, HairballRadius, flightTime);
    }

    private void Splat()
    {
        EnemyPuddle.Spawn(transform.position, 1f, 4f, damage * 0.6f, target);
        enabled = false;
        Destroy(gameObject);
    }

    private void Land()
    {
        ExplosionFx.Spawn(landAt, HairballRadius, new Color(0.7f, 0.55f, 0.45f));
        if (target != null && Vector2.Distance(target.transform.position, landAt) <= HairballRadius) target.TakeDamage(damage);
        float offset = Random.value * 360f;
        for (int i = 0; i < split; i++)
        {
            float a = (offset + i * 360f / split) * Mathf.Deg2Rad;
            Spawn(landAt, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 4.5f, splitDamage, target);
        }
        enabled = false;
        Destroy(gameObject);
    }

    private void Update()
    {
        if (lob)
        {
            t += Time.deltaTime / flightTime;
            float arc = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            transform.position = Vector2.Lerp(from, landAt, t) + Vector2.up * arc * 1.5f;
            transform.localScale = Vector3.one * (0.5f + 0.25f * arc);
            transform.Rotate(0f, 0f, -540f * Time.deltaTime);
            if (t >= 1f) Land();
            return;
        }

        transform.position += (Vector3)(velocity * Time.deltaTime);
        age += Time.deltaTime;
        if (!needle) transform.Rotate(0f, 0f, 360f * Time.deltaTime);
        if ((trailTimer -= Time.deltaTime) <= 0f && !Settings.LowParticles && TrailBudget())
        {
            trailTimer = toxic ? 0.05f : 0.08f;
            Color c = needle ? new Color(1f, 0.95f, 0.8f, 0.6f) : new Color(sr.color.r, sr.color.g, sr.color.b, 0.6f);
            if (toxic) Juice.SparksDir(transform.position, Vector2.down, 30f, c, 1, 1.5f, 0.12f);
            else Juice.Sparks(transform.position, c, 1, 0.3f, needle ? 0.08f : 0.1f);
        }
        if (toxic && age >= ToxicRange) { Splat(); return; }

        if (target != null && Vector2.Distance(transform.position, target.transform.position) <= hitRadius)
        {
            target.TakeDamage(damage);
            if (toxic) { Splat(); return; }
            enabled = false;
            Destroy(gameObject);
        }
    }
}
