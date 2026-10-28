using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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

    private float Fury => PlayerCollabs.Has("furyfuse") && stats.maxHealth > 0f ? 1f - Mathf.Clamp01(stats.currentHealth / stats.maxHealth) : 0f;

    private void Update()
    {
        attackTimer += Time.deltaTime;
        if (attackTimer < 1f / (stats.AttackSpeed * (1f + Fury))) return;

        List<Collider2D> hits = FindTargets(attackRange);
        if (hits.Count == 0) return;

        int count = Mathf.Max(1, stats.ProjectileCount) + (ult != null ? ult.ExtraBombs : 0);
        for (int i = 0; i < count; i++)
        {
            Throw(hits[i % hits.Count].transform, Vector2.zero);
        }

        if (PlayerCollabs.Has("carpet")) CarpetBomb(hits[0].transform.position - firePoint.position);
        if (SuperWeapon.Active != null) SuperWeapon.Active.OnThrow(hits[0].transform.position);
        if (anim != null) anim.PlayThrow(hits[0].transform.position - firePoint.position);
        ThrowKick();
        attackTimer = 0f;
    }

    private void CarpetBomb(Vector2 dir)
    {
        if (bombPrefab == null) return;
        dir = dir.normalized;
        float damage = stats.AttackDamage * 0.35f * (ult != null ? ult.DamageMultiplier : 1f);
        float radius = stats.BombRadius * 0.6f * (ult != null ? ult.RadiusMultiplier : 1f) * (1f + 0.5f * Fury);
        Juice.SparksDir(firePoint.position, dir, 15f, new Color(1f, 0.8f, 0.35f), 10, 9f, 0.14f);
        for (int i = 0; i < 5; i++)
        {
            DamageStats.Source = DamageStats.Bombs;
            GameObject go = Pool.Spawn(bombPrefab, firePoint.position, Quaternion.identity);
            go.transform.localScale *= 0.6f;
            if (!go.TryGetComponent(out Bomb mini)) continue;
            mini.Initialize(null, dir, damage, radius, stats.critChance, stats.critDamage);
            mini.isSecondary = true;
            mini.maxFlightTime = 0.06f + i * 0.06f;
            mini.fuseTime = 0.25f;
        }
    }

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

    private static readonly List<Collider2D> targets = new List<Collider2D>(128);
    private static Vector2 sortOrigin;
    private static readonly System.Comparison<Collider2D> byDistance = (a, b) =>
        ((Vector2)a.transform.position - sortOrigin).sqrMagnitude.CompareTo(((Vector2)b.transform.position - sortOrigin).sqrMagnitude);

    private List<Collider2D> FindTargets(float range)
    {
        Bomb.OverlapEnemies(transform.position, range, targets);
        sortOrigin = transform.position;
        targets.Sort(byDistance);
        return targets;
    }

    private void ThrowKick()
    {
        if (anim != null) anim.Squash(new Vector2(1.15f, 0.88f), 0.1f);
        float fury = Fury;
        Juice.Sparks(firePoint.position, Color.Lerp(new Color(1f, 0.75f, 0.3f), new Color(1f, 0.2f, 0.15f), fury), 4 + Mathf.RoundToInt(fury * 8f), 3f + fury * 3f, 0.1f);
        if (fury > 0.4f) ExplosionFx.Ring(firePoint.position, 0.8f + fury, new Color(1f, 0.25f, 0.2f, 0.7f), 0.2f);
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
            if (target == null) bomb.maxFlightTime = 0.25f;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
