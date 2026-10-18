using UnityEngine;

public class SawGalaxyWeapon : SuperWeapon
{
    private const int Arms = 3, SawsPerArm = 6;
    private static readonly Color Violet = new Color(0.8f, 0.55f, 1f);

    private readonly Shot[] saws = new Shot[Arms * SawsPerArm];
    private readonly int[] gens = new int[Arms * SawsPerArm];
    private Shot core;
    private int coreGen;
    private float spin;

    private void Update()
    {
        bool regrind = Ready(0.35f);
        float t = Time.time;
        spin += Time.deltaTime * 110f;
        float breath = 0.75f + 0.25f * Mathf.Sin(t * 1.4f);
        float step = Area(1.15f);

        for (int arm = 0; arm < Arms; arm++)
        {
            for (int j = 0; j < SawsPerArm; j++)
            {
                int i = arm * SawsPerArm + j;
                if (saws[i] == null || !saws[i].IsSame(gens[i])) Make(i);
                float radius = (1.3f + j * step) * breath;
                float angle = spin + arm * 360f / Arms + j * 24f;
                saws[i].transform.position = Pos + Rotate(Vector2.right, angle) * radius;
                saws[i].transform.localScale = Vector3.one * Area(0.75f + j * 0.08f);
                saws[i].hitRadius = Area(0.45f);
                if (regrind) saws[i].ClearHits();
            }
        }

        if (core == null || !core.IsSame(coreGen))
        {
            core = Shot.Spawn(Shapes.Ring, new Color(0.75f, 0.5f, 1f, 0.14f), 1f, Pos, 4);
            core.spin = -40f;
            core.life = float.MaxValue;
            coreGen = core.Generation;
        }
        core.transform.position = Pos;
        core.transform.localScale = Vector3.one * (1.3f + (SawsPerArm - 1) * step) * breath * 2.2f;

        foreach (Collider2D c in EnemiesNear(Pos, Area(8.5f)))
        {
            Vector2 to = Pos - (Vector2)c.transform.position;
            if (to.sqrMagnitude < 0.5f || !c.TryGetComponent(out EnemyAI cat)) continue;
            cat.Pull(Vector2.Perpendicular(to.normalized) * 2.2f + to.normalized * 1.6f);
        }
        if (Random.value < 0.3f) Juice.Sparks(Pos + Random.insideUnitCircle * Area(7f), Violet, 1, 1.5f, 0.1f);
    }

    private void Make(int i)
    {
        Shot s = Shot.Spawn(Shapes.Art("Icons/shuriken"), Color.Lerp(Violet, Color.white, (i % SawsPerArm) / (float)SawsPerArm * 0.35f), 0.6f, Pos, 24);
        s.life = float.MaxValue;
        s.spin = 1500f;
        s.hitRadius = 0.45f;
        if (i % SawsPerArm >= SawsPerArm - 2) { s.trail = new Color(0.8f, 0.6f, 1f, 0.45f); s.trailSize = 0.18f; }
        s.onHit = (shot, e) => Hit(e, Dmg(11f), Pos);
        saws[i] = s;
        gens[i] = s.Generation;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        for (int i = 0; i < saws.Length; i++) if (saws[i] != null && saws[i].IsSame(gens[i])) saws[i].Release();
        if (core != null && core.IsSame(coreGen)) core.Release();
    }
}
