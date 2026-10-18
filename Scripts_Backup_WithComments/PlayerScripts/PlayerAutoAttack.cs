using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// HoloCure-style auto attack: no button press needed. On a cooldown based on
// AttackSpeed, throws one bomb per ProjectileCount at the nearest enemies.
// While the ultimate is active, bombs hit harder, blow up bigger, and come in larger volleys.
[RequireComponent(typeof(PlayerStats))]
public class PlayerAutoAttack : MonoBehaviour
{
    private PlayerStats stats;
    private PlayerAnimator anim;
    private PlayerUltimate ult;

    [Header("Attack Settings")]
    public float attackRange = 7f;
    [FormerlySerializedAs("projectilePrefab")] public GameObject bombPrefab;
    public Transform firePoint;

    private float attackTimer;

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        anim = GetComponent<PlayerAnimator>();
        ult = GetComponent<PlayerUltimate>();
        if (firePoint == null) firePoint = transform;
    }

    // Fury Fuse (Berserk bomb upgrade): 0 at full HP, 1 at no HP
    private float Fury => PlayerCollabs.Has("furyfuse") && stats.maxHealth > 0f ? 1f - Mathf.Clamp01(stats.currentHealth / stats.maxHealth) : 0f;

    private void Update()
    {
        attackTimer += Time.deltaTime;
        if (attackTimer < 1f / (stats.AttackSpeed * (1f + Fury))) return; // Fury Fuse: up to 2x as fast at low HP

        List<Collider2D> hits = FindTargets(attackRange);
        if (hits.Count == 0) return;

        int count = Mathf.Max(1, stats.ProjectileCount) + (ult != null ? ult.ExtraBombs : 0);
        for (int i = 0; i < count; i++)
        {
            // more bombs than enemies in range -> double up on the closest ones
            Throw(hits[i % hits.Count].transform, Vector2.zero);
        }

        if (PlayerCollabs.Has("carpet")) CarpetBomb(hits[0].transform.position - firePoint.position);
        if (SuperWeapon.Active != null) SuperWeapon.Active.OnThrow(hits[0].transform.position); // Armageddon airstrikes
        if (anim != null) anim.PlayThrow(hits[0].transform.position - firePoint.position);
        ThrowKick();
        attackTimer = 0f;
    }

    // Carpet Bomb collab: a line of mini bombs toward the main target with every throw.
    private void CarpetBomb(Vector2 dir)
    {
        if (bombPrefab == null) return;
        dir = dir.normalized;
        float damage = stats.AttackDamage * 0.35f * (ult != null ? ult.DamageMultiplier : 1f);
        float radius = stats.BombRadius * 0.6f * (ult != null ? ult.RadiusMultiplier : 1f) * (1f + 0.5f * Fury);
        for (int i = 0; i < 5; i++)
        {
            DamageStats.Source = DamageStats.Bombs;
            GameObject go = Pool.Spawn(bombPrefab, firePoint.position, Quaternion.identity);
            go.transform.localScale *= 0.6f;
            if (!go.TryGetComponent(out Bomb mini)) continue;
            mini.Initialize(null, dir, damage, radius, stats.critChance, stats.critDamage);
            mini.isSecondary = true;              // no skill procs off the minis, so it can't snowball
            mini.maxFlightTime = 0.06f + i * 0.06f; // one lands every ~0.85 units along the line
            mini.fuseTime = 0.25f;
        }
    }

    // Ultimate opener: a volley at everything nearby; with nothing in range, a ring of bombs.
    public void Burst(int bombCount)
    {
        List<Collider2D> hits = FindTargets(attackRange * 1.3f);
        for (int i = 0; i < bombCount; i++)
        {
            if (hits.Count > 0) Throw(hits[i % hits.Count].transform, Vector2.zero);
            else
            {
                float a = i * Mathf.PI * 2f / bombCount;
                Throw(null, new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
            }
        }
        if (anim != null) anim.PlayThrow(hits.Count > 0 ? hits[0].transform.position - firePoint.position : Vector3.down);
        ThrowKick();
        attackTimer = 0f;
    }

    // Shared buffer + cached comparer: targeting runs several times a second, so no per-call garbage.
    private static readonly List<Collider2D> targets = new List<Collider2D>(128);
    private static Vector2 sortOrigin;
    private static readonly System.Comparison<Collider2D> byDistance = (a, b) =>
        ((Vector2)a.transform.position - sortOrigin).sqrMagnitude.CompareTo(((Vector2)b.transform.position - sortOrigin).sqrMagnitude);

    // Nearest first. Sleeping cats in range count too: a bomb wakes them up.
    private List<Collider2D> FindTargets(float range)
    {
        Bomb.OverlapEnemies(transform.position, range, targets);
        sortOrigin = transform.position;
        targets.Sort(byDistance);
        return targets;
    }

    // target == null: fly straight along dir until the bomb's max flight time, then explode
    // a little punch on every throw: Mira squashes and the fuse spits sparks
    private void ThrowKick()
    {
        if (anim != null) anim.Squash(new Vector2(1.15f, 0.88f), 0.1f);
        Juice.Sparks(firePoint.position, new Color(1f, 0.75f, 0.3f), 4, 3f, 0.1f);
    }

    private void Throw(Transform target, Vector2 dir)
    {
        if (bombPrefab == null) return;

        if (target != null) dir = (target.position - firePoint.position).normalized;
        float damage = stats.AttackDamage * (ult != null ? ult.DamageMultiplier : 1f);
        float radius = stats.BombRadius * (ult != null ? ult.RadiusMultiplier : 1f) * (1f + 0.5f * Fury);

        DamageStats.Source = DamageStats.Bombs;
        GameObject go = Pool.Spawn(bombPrefab, firePoint.position, Quaternion.identity);
        if (go.TryGetComponent(out Bomb bomb))
        {
            bomb.Initialize(target, dir, damage, radius, stats.critChance, stats.critDamage);
            if (target == null) bomb.maxFlightTime = 0.25f; // untargeted ring: pop ~3.5 units out, on screen
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
