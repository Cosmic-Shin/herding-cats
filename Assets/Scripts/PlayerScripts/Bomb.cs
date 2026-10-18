using System.Collections.Generic;
using UnityEngine;

public class Bomb : MonoBehaviour, IPoolable
{
    public float speed = 14f;
    public float fuseTime = 0.8f;
    public float maxFlightTime = 1.2f;
    public float spinSpeed = 900f;
    public Vector2 fuseTip = new Vector2(0.04f, 0.07f);
    private float nextSpark;

    private static int enemyMask;
    public static int EnemyMask => enemyMask != 0 ? enemyMask : (enemyMask = LayerMask.GetMask("Enemy"));
    private static readonly List<Collider2D> hits = new List<Collider2D>(64);

    private EnemyHealth target;
    private int targetLife;
    private Vector2 direction;
    private Vector3 stickOffset;
    private bool stuck;
    private float timer;
    private float damage;
    private float radius;
    private float critChance;
    private float critMultiplier;
    private SpriteRenderer sr;
    private string source;

    private float defaultFuse, defaultFlight;
    private Vector3 defaultScale;
    private Vector3 stickScale;

    [HideInInspector] public bool isSecondary;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        defaultFuse = fuseTime;
        defaultFlight = maxFlightTime;
        defaultScale = transform.localScale;
    }

    public void OnSpawn()
    {
        stuck = false;
        timer = 0f;
        isSecondary = false;
        fuseTime = defaultFuse;
        maxFlightTime = defaultFlight;
        transform.localScale = defaultScale;
        transform.rotation = Quaternion.identity;
        if (sr != null) sr.color = Color.white;
    }

    public void Initialize(Transform target, Vector2 dir, float damage, float radius, float critChance, float critMultiplier = 1.5f)
    {
        this.target = target != null ? target.GetComponent<EnemyHealth>() : null;
        targetLife = this.target != null ? this.target.Life : 0;
        direction = dir;
        this.damage = damage;
        this.radius = radius;
        this.critChance = critChance;
        this.critMultiplier = critMultiplier;
        source = DamageStats.Source;
    }

    private bool TargetValid => target != null && target.Life == targetLife && !target.IsDead;

    private void Update()
    {
        timer += Time.deltaTime;
        if (Time.time >= nextSpark)
        {
            nextSpark = Time.time + 0.06f;
            Juice.Sparks(transform.TransformPoint(fuseTip), new Color(1f, 0.7f, 0.2f), 1, 1.5f, 0.06f);
        }

        if (stuck)
        {
            if (TargetValid) transform.position = target.transform.position + stickOffset;
            if (!isSecondary && source == DamageStats.Bombs && PlayerCollabs.Has("magnetbomb")) PullCats();
            float pop = Mathf.Clamp01(1f - timer / 0.15f);
            transform.localScale = stickScale * (1f + 0.6f * pop * Mathf.Sin(pop * Mathf.PI));
            if (sr != null) sr.color = Mathf.Repeat(timer * (4f + timer * 20f), 1f) > 0.5f ? Color.red : Color.white;
            if (timer >= fuseTime) Explode();
            return;
        }

        if (TargetValid) direction = ((Vector2)(target.transform.position - transform.position)).normalized;
        transform.position += (Vector3)(direction * speed * Time.deltaTime);
        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

        if (timer >= maxFlightTime) Explode();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (stuck || !other.TryGetComponent(out EnemyHealth enemy) || enemy.IsDead) return;

        stuck = true;
        timer = 0f;
        target = enemy;
        targetLife = enemy.Life;
        stickOffset = transform.position - enemy.transform.position;
        stickScale = transform.localScale;
        Juice.Smoke(transform.position, 0.45f, 2);
        Juice.Sparks(transform.position, new Color(0.9f, 0.8f, 0.6f), 3, 2.5f, 0.08f);
    }

    private void PullCats()
    {
        Vector2 at = transform.position;
        foreach (Collider2D c in OverlapEnemies(at, radius * 1.6f, hits))
        {
            Vector2 to = at - (Vector2)c.transform.position;
            if (to.sqrMagnitude > 0.04f && c.TryGetComponent(out EnemyAI cat)) cat.Pull(to.normalized * 4f);
        }
        if (Random.value < 0.3f) Juice.Sparks(at + Random.insideUnitCircle * radius, new Color(1f, 0.55f, 0.6f), 1, 0.5f, 0.1f);
    }

    private void Explode()
    {
        DamageStats.Source = source;
        Blast(transform.position, radius, damage, critChance, critMultiplier, isSecondary);
        Pool.Despawn(gameObject);
    }

    private static float nextCritStop;

    public static void Blast(Vector2 pos, float radius, float damage, float critChance, float critMultiplier, bool secondary)
    {
        int crits = 0;
        bool ownBomb = DamageStats.Source == DamageStats.Bombs;
        bool concussion = ownBomb && PlayerCollabs.Has("concussion");
        foreach (Collider2D hit in OverlapEnemies(pos, radius, hits))
        {
            if (!hit.TryGetComponent(out EnemyHealth enemy)) continue;
            bool crit = Random.value < critChance || MiraPassives.AutoCrit(enemy);
            if (crit) crits++;
            enemy.TakeDamage(damage * (crit ? critMultiplier * MiraPassives.CritBonus(enemy) : 1f) * Random.Range(0.9f, 1.1f), pos, crit);
            if (concussion && hit.TryGetComponent(out EnemyAI stunned)) stunned.Stun(0.6f);
        }

        ExplosionFx.Spawn(pos, radius);
        Sfx.Play(Sfx.Id.Boom, secondary ? 0.45f : 0.7f, Mathf.Clamp(1.4f - radius * 0.2f, 0.7f, 1.3f));
        CameraFollow.Shake(secondary ? 0.04f : 0.08f, 0.1f);
        if (crits >= 3 && Time.unscaledTime >= nextCritStop)
        {
            nextCritStop = Time.unscaledTime + 0.5f;
            Juice.HitStop(0.05f);
            CameraFollow.Shake(0.15f, 0.12f);
        }

        if (!secondary && PlayerSkills.Instance != null) PlayerSkills.Instance.OnExplosion(pos, radius, damage);
        if (!secondary && ownBomb && SuperWeapon.Active != null) SuperWeapon.Active.OnBombBlast(pos, radius, damage);
    }

    public static List<Collider2D> OverlapEnemies(Vector2 pos, float radius, List<Collider2D> results)
    {
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = EnemyMask, useTriggers = true };
        Physics2D.OverlapCircle(pos, radius, filter, results);
        return results;
    }
}
