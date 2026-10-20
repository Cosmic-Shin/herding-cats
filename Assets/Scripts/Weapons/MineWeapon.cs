using System.Collections.Generic;
using UnityEngine;

public class MineWeapon : Weapon
{
    private static readonly float[] DropInterval = { 0.9f, 0.85f, 0.8f, 0.7f, 0.6f, 0.55f, 0.45f };
    private static readonly int[] MaxMines = { 6, 7, 8, 9, 10, 12, 16 };
    private static readonly float[] Damage = { 20, 23, 26, 29, 33, 37, 45 };
    private static readonly float[] Radius = { 1.7f, 1.7f, 1.7f, 1.9f, 2.1f, 2.1f, 2.5f };
    private const float ArmTime = 0.4f;
    private const float TriggerRadius = 0.9f;
    private const float MineLife = 15f;
    private const float MinMoveBetweenDrops = 0.8f;

    private readonly List<(Shot shot, int gen)> mines = new List<(Shot, int)>();
    private Vector2 lastDrop;
    private float dropTimer;

    private void Update()
    {
        mines.RemoveAll(m => !m.shot.IsSame(m.gen));
        dropTimer -= Time.deltaTime;
        if (dropTimer > 0f || (Pos - lastDrop).sqrMagnitude < MinMoveBetweenDrops * MinMoveBetweenDrops) return;

        dropTimer = DropInterval[L] / Mathf.Max(0.1f, stats.attackSpeedMultiplier);
        lastDrop = Pos;
        if (mines.Count >= MaxMines[L]) mines[0].shot.Release();
        Shot mine = Drop();
        mines.Add((mine, mine.Generation));
    }

    private Shot Drop() => SpawnMine(Pos, Dmg(Damage[L]), Area(Radius[L]), Explode);

    public void DropBurst(Vector2 at)
    {
        for (int i = 0; i < 3; i++)
        {
            Shot mine = SpawnMine(at + Random.insideUnitCircle * 0.8f, Dmg(Damage[L]), Area(Radius[L]), Explode);
            mines.Add((mine, mine.Generation));
        }
    }

    public static Shot SpawnMine(Vector2 pos, float damage, float radius, System.Action<Vector2, float, float> explode)
    {
        Shot s = Shot.Spawn(Shapes.Art("landmine"), Color.white, 0.8f, pos, sortingOrder: 5);
        s.life = MineLife;
        Juice.Sparks(pos, new Color(0.6f, 0.55f, 0.5f), 3, 1.5f, 0.1f);
        bool beeped = false;

        s.onUpdate = mine =>
        {
            bool armed = mine.age >= ArmTime;
            if (armed && !beeped)
            {
                beeped = true;
                Sfx.Play(Sfx.Id.Tick, 0.2f, 0.8f);
                ExplosionFx.Ring(mine.Position, TriggerRadius, new Color(1f, 0.35f, 0.3f, 0.7f), 0.3f);
            }
            mine.Renderer.color = !armed ? new Color(0.55f, 0.55f, 0.55f) : (Mathf.Repeat(mine.age * 2f, 1f) > 0.5f ? new Color(1f, 0.45f, 0.45f) : Color.white);
            if (!armed || EnemiesNear(mine.Position, TriggerRadius).Count == 0) return;

            explode(mine.Position, radius, damage);
            mine.Release();
        };
        return s;
    }
}
