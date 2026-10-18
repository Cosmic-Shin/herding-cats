using System.Collections.Generic;
using UnityEngine;

public class AbsoluteZeroWeapon : BlackHoleWeapon
{
    private const float FreezeTick = 0.1f;
    private static readonly Color Ice = new Color(0.6f, 0.9f, 1f);

    private readonly List<BlackHole> holes = new List<BlackHole>();
    private static readonly List<Collider2D> hits = new List<Collider2D>(64);
    private float freezeTimer;

    protected override void OnOpened(BlackHole hole)
    {
        hole.Tint(new Color(0.75f, 0.92f, 1f));
        holes.Add(hole);
    }

    protected override void Update()
    {
        base.Update();
        holes.RemoveAll(h => h == null);

        if ((freezeTimer -= Time.deltaTime) > 0f) return;
        freezeTimer = FreezeTick;
        foreach (BlackHole hole in holes)
        {
            Vector2 center = hole.transform.position;
            foreach (Collider2D c in Bomb.OverlapEnemies(center, hole.PullRadius, hits))
                if (c.TryGetComponent(out EnemyAI cat)) cat.Freeze(0.5f);
            if (Random.value < 0.5f) Juice.Sparks(center + Random.insideUnitCircle * hole.PullRadius, Ice, 1, 0.5f, 0.12f);
        }
    }

    public override void Pop(Vector2 at, float radius, float damage)
    {
        base.Pop(at, radius, damage);
        FrostNovaWeapon.ShardBurst(at, radius * 1.5f, 14);
        StompFx.Spawn(at, radius * 1.5f, ice: true);
        Sfx.Play(Sfx.Id.Shatter, 0.6f, 0.8f);
    }
}
