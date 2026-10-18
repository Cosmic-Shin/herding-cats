using System.Collections.Generic;
using UnityEngine;

// Super: Sticky Mines + Glacier Quake. Signature: an icy slam scatters 8 frost mines around Mira. Each one
// glitters until a cat comes close, then bursts into a freezing blast that locks everything nearby in ice.
public class PermafrostMinefieldWeapon : SuperWeapon
{
    private const int MaxMines = 24;
    private static readonly Color Frost = new Color(0.55f, 0.9f, 1f);
    private readonly List<(Shot shot, int gen)> mines = new List<(Shot, int)>();

    private void Update()
    {
        if (!Ready(4f)) return;
        mines.RemoveAll(m => !m.shot.IsSame(m.gen));

        Vector2 at = Pos;
        Fx(Shapes.Ring, Frost, 1f, at, 0.35f, Area(6f), 44);
        StompFx.Spawn(at, Area(2.5f), ice: true);
        Juice.Sparks(at, new Color(0.8f, 0.97f, 1f), 12, 8f, 0.15f);
        HitArea(at, Area(2.5f), Dmg(20f), e => { EnemyAI cat = Cat(e); if (cat != null) cat.Freeze(1f); });
        Sfx.Play(Sfx.Id.Shatter, 0.6f, 1.3f);

        for (int i = 0; i < 8 && mines.Count < MaxMines; i++)
            Throw(at, at + Random.insideUnitCircle.normalized * Random.Range(2f, Area(6.5f)));
    }

    private void Throw(Vector2 from, Vector2 to)
    {
        Shot s = Shot.Spawn(Shapes.Art("Icons/frost"), Frost, 0.55f, from, 6); // snowflake mine
        s.life = 14f;
        s.spin = 120f;
        float armAt = 0.45f, blastRadius = Area(2.3f), damage = Dmg(40f);
        bool landed = false;
        s.onUpdate = shot =>
        {
            if (!landed) // skid out to its spot
            {
                shot.transform.position = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, shot.age / armAt));
                if (shot.age >= armAt) { landed = true; Juice.Sparks(to, Frost, 4, 3f, 0.1f); }
                return;
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(shot.age * 6f);
            shot.Renderer.color = Color.Lerp(Frost, Color.white, pulse * 0.6f);
            shot.transform.localScale = Vector3.one * (0.55f + 0.08f * pulse);
            if (EnemiesNear(shot.Position, 1f).Count == 0) return;

            Vector2 p = shot.Position;
            HitArea(p, blastRadius, damage, e => { EnemyAI cat = Cat(e); if (cat != null) cat.Freeze(2.5f); });
            StompFx.Spawn(p, blastRadius, ice: true); // frost bursts over the ground
            Fx(Shapes.Ring, Frost, 0.5f, p, 0.35f, blastRadius * 2.2f, 51);
            for (int k = 0; k < 6; k++) Juice.SparksDir(p, Rotate(Vector2.right, k * 60f), 10f, new Color(0.8f, 0.97f, 1f), 2, 9f, 0.14f); // ice spikes
            Shatter(p, 1.2f);
            Lighting.Flash(p, 3.5f, Frost, 0.3f);
            Sfx.Play(Sfx.Id.Shatter, 0.5f, Random.Range(0.9f, 1.1f));
            shot.Release();
        };
        mines.Add((s, s.Generation));
    }
}
