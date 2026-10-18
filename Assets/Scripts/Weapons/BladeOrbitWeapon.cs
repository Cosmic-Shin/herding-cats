using UnityEngine;

public class BladeOrbitWeapon : OrbsWeapon
{
    private const float FlingEvery = 1.2f;
    private const float FlingRange = 6f;
    private const float FlingDamage = 20f;
    private const float Speed = 15f;
    private const float OutTime = 0.3f;

    private float flingTimer;
    private float[] outUntil = new float[0];

    protected override void OnLevelChanged()
    {
        base.OnLevelChanged();
        foreach (Transform orb in orbs)
        {
            var sr = orb.GetComponent<SpriteRenderer>();
            sr.sprite = Shapes.Art("Icons/shuriken");
            sr.color = Color.white;
            orb.localScale = Vector3.one * 0.75f;
            Transform glow = orb.Find("Glow");
            if (glow != null) glow.gameObject.SetActive(false);
        }
        outUntil = new float[orbs.Count];
    }

    protected override void Update()
    {
        base.Update();
        for (int i = 0; i < orbs.Count; i++)
        {
            orbs[i].Rotate(0f, 0f, 900f * Time.deltaTime);
            orbs[i].GetComponent<SpriteRenderer>().enabled = Time.time >= outUntil[i];
        }

        if ((flingTimer -= Time.deltaTime) > 0f) return;
        flingTimer = FlingEvery / Mathf.Max(0.1f, stats.attackSpeedMultiplier);
        Sfx.Play(Sfx.Id.Swish, 0.35f, 1.3f);
        for (int i = 0; i < orbs.Count; i++)
        {
            Transform target = NearestTo(orbs[i].position, Area(FlingRange));
            if (target == null) continue;
            Fling(orbs[i].position, ((Vector2)target.position - (Vector2)orbs[i].position).normalized);
            outUntil[i] = Time.time + 0.9f;
        }
    }

    private void Fling(Vector2 from, Vector2 dir)
    {
        Shot s = Shot.Spawn(Shapes.Art("Icons/shuriken"), Color.white, 0.75f, from);
        s.velocity = dir * Speed;
        s.life = 1.5f;
        s.hitRadius = 0.45f;
        s.spin = 1440f;
        s.trail = new Color(0.75f, 0.85f, 1f, 0.6f);
        s.trailSize = 0.1f;
        s.ghost = new Color(0.6f, 0.8f, 1f, 0.45f);

        float damage = Dmg(FlingDamage);
        bool returning = false;
        Transform player = transform;
        s.onUpdate = shot =>
        {
            if (!returning && shot.age >= OutTime) { returning = true; shot.ClearHits(); }
            if (!returning) return;
            Vector2 back = (Vector2)player.position - shot.Position;
            if (back.magnitude < 0.5f) { shot.Release(); return; }
            shot.velocity = back.normalized * (Speed + 3f);
        };
        s.onHit = (shot, e) =>
        {
            Hit(e, damage, shot.Position);
            Juice.SparksDir(e.transform.position, shot.velocity, 40f, new Color(1f, 0.95f, 0.75f), 4, 7f, 0.12f);
        };
    }
}
