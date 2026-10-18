using System.Collections.Generic;
using UnityEngine;

public class LightningWeapon : Weapon
{
    private static readonly int[] Count = { 2, 3, 3, 4, 4, 5, 7 };
    private static readonly float[] Damage = { 20, 20, 25, 25, 30, 34, 40 };
    private const float StrikeRadius = 1.4f;

    private readonly List<Vector2> targets = new List<Vector2>(8);

    private void Update()
    {
        if (!Ready(2f)) return;

        targets.Clear();
        List<Collider2D> pool = EnemiesNear(Pos, 9f);
        for (int i = 0; i < Count[L] && pool.Count > 0; i++)
        {
            int pick = Random.Range(0, pool.Count);
            targets.Add(pool[pick].transform.position);
            pool.RemoveAt(pick);
        }

        float damage = Dmg(Damage[L]);
        float radius = Area(StrikeRadius);
        foreach (Vector2 at in targets)
        {
            LightningFx.Spawn(at);
            StompFx.Spawn(at, radius * 0.7f);
            Lighting.Flash(at, radius * 2.5f, new Color(1f, 0.95f, 0.6f), 0.15f);
            ExplosionFx.Ring(at, radius, new Color(1f, 0.9f, 0.35f), 0.25f);
            foreach (Collider2D c in EnemiesNear(at, radius))
            {
                if (c.TryGetComponent(out EnemyHealth e)) Hit(e, damage, at);
            }
        }
        if (targets.Count > 0) CameraFollow.Shake(0.04f, 0.08f);
        if (targets.Count > 0) Sfx.Play(Sfx.Id.Crackle, 0.5f);
    }
}
