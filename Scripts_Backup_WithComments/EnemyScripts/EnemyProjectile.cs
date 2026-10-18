using UnityEngine;

// Cat spit. Flies straight; hits the player by distance check (no physics layers needed).
public class EnemyProjectile : MonoBehaviour
{
    private const float Lifetime = 4f;

    private Vector2 velocity;
    private float damage;
    private float hitRadius;
    private PlayerHealth target;

    // size/color default to cat spit; bosses fire bigger, differently colored orbs
    // Toxic Spitter: green spit that leaves a poison puddle where it lands (or hits).
    private bool toxic;
    private float age;
    private const float ToxicRange = 1.4f; // seconds of flight before it splats on the ground

    public static void SpawnToxic(Vector2 pos, Vector2 velocity, float damage, PlayerHealth target) =>
        Spawn(pos, velocity, damage, target, 0.3f, new Color(0.5f, 0.95f, 0.3f)).toxic = true;

    public static EnemyProjectile Spawn(Vector2 pos, Vector2 velocity, float damage, PlayerHealth target, float size = 0.25f, Color? color = null)
    {
        var go = new GameObject("CatSpit");
        go.transform.position = pos;
        go.transform.localScale = new Vector3(size, size, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        Lighting.Glow(sr);
        sr.sprite = ExplosionFx.GetCircle();
        sr.color = color ?? new Color(1f, 0.6f, 0.15f);
        sr.sortingOrder = 15;

        var p = go.AddComponent<EnemyProjectile>();
        p.velocity = velocity;
        p.damage = damage;
        p.hitRadius = 0.1f + size; // 0.35 for spit
        p.target = target;
        Destroy(go, Lifetime);
        return p;
    }

    // Hairball Launcher: lobbed in an arc at where the player is standing (red circle shows the spot),
    // hits on landing, then bursts into `split` spits flying outward.
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
        sr.sprite = ExplosionFx.GetCircle();
        sr.color = new Color(0.6f, 0.5f, 0.45f);
        sr.sortingOrder = 16;

        var p = go.AddComponent<EnemyProjectile>();
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
        enabled = false; // Destroy happens at the end of the frame; never land twice
        Destroy(gameObject);
    }

    private void Update()
    {
        if (lob)
        {
            t += Time.deltaTime / flightTime;
            float arc = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
            transform.position = Vector2.Lerp(from, landAt, t) + Vector2.up * arc * 1.5f;
            transform.localScale = Vector3.one * (0.5f + 0.25f * arc); // bigger at the top of the arc
            if (t >= 1f) Land();
            return;
        }

        transform.position += (Vector3)(velocity * Time.deltaTime);
        age += Time.deltaTime;
        if (toxic && age >= ToxicRange) { Splat(); return; }

        if (target != null && Vector2.Distance(transform.position, target.transform.position) <= hitRadius)
        {
            target.TakeDamage(damage);
            if (toxic) { Splat(); return; }
            enabled = false; // never hit twice before the Destroy goes through
            Destroy(gameObject);
        }
    }
}
