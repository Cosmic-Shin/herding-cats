using System.Collections;
using UnityEngine;

// Super: Glacier Quake + Event Horizon. Signature: a frozen singularity opens in the crowd, dragging cats in
// and freezing them solid, then collapses into 12 ice lasers that shatter everything frozen for double damage.
public class CryoSingularityWeapon : SuperWeapon
{
    private static readonly Color Ice = new Color(0.6f, 0.9f, 1f);

    private void Update()
    {
        if (Ready(6f)) StartCoroutine(Singularity(Crowd(10f)));
    }

    private IEnumerator Singularity(Vector2 at)
    {
        float pullRadius = Area(6.5f), freezeRadius = Area(2.6f);
        Shot core = Shot.Spawn(Shapes.Circle, new Color(0.05f, 0.1f, 0.25f, 0.95f), 0.2f, at, 30);
        core.life = 10f;
        int coreGen = core.Generation;
        Shot halo = Shot.Spawn(Shapes.Ring, new Color(0.6f, 0.9f, 1f, 0.8f), 1f, at, 31);
        halo.life = 10f;
        halo.spin = -360f;
        int haloGen = halo.Generation;
        Sfx.Play(Sfx.Id.Whoosh, 0.8f, 0.5f);

        const float charge = 1.4f;
        for (float t = 0f; t < charge; t += Time.deltaTime)
        {
            float k = t / charge;
            if (core.IsSame(coreGen)) core.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.6f, k) * (1f + 0.08f * Mathf.Sin(t * 30f));
            if (halo.IsSame(haloGen)) halo.transform.localScale = Vector3.one * Mathf.Lerp(pullRadius * 2f, 2.2f, k);

            foreach (Collider2D c in EnemiesNear(at, pullRadius))
            {
                Vector2 to = at - (Vector2)c.transform.position;
                if (!c.TryGetComponent(out EnemyAI cat)) continue;
                if (to.magnitude < freezeRadius) cat.Freeze(2f);
                else cat.Pull(to.normalized * 7f);
            }
            if (Random.value < 0.5f) // frost streaming inward
            {
                Vector2 from = at + Random.insideUnitCircle.normalized * pullRadius;
                Juice.SparksDir(from, at - from, 5f, Ice, 1, (at - from).magnitude * 2.2f, 0.15f);
            }
            yield return null;
        }

        // collapse: 12 ice lasers; frozen cats shatter
        float damage = Dmg(42f), length = Area(9f);
        float spinOffset = Random.Range(0f, 30f);
        for (int i = 0; i < 12; i++)
        {
            Vector2 dir = Rotate(Vector2.right, spinOffset + i * 30f);
            GlowBeam(at, dir, length, 0.3f, Ice, 0.4f);
            HitLine(at, dir, length, 0.6f, damage, e =>
            {
                EnemyAI cat = Cat(e);
                if (cat == null || !cat.IsFrozen || e.IsDead) return;
                Hit(e, damage, at, knockback: false); // shatter: frozen cats take the hit twice
                Shatter(e.transform.position, 0.8f);
            });
        }
        HitArea(at, freezeRadius, damage * 1.5f, e => Shatter(e.transform.position, 1f));
        Fx(Shapes.Ring, Ice, 1f, at, 0.5f, Area(14f), 47);
        StompFx.Spawn(at, Area(4.5f), ice: true); // the ground under the crowd freezes over
        Lighting.Flash(at, 9f, Ice, 0.5f);
        Sfx.Play(Sfx.Id.Shatter, 1f, 0.8f);
        CameraFollow.Shake(0.2f, 0.25f);
        if (core.IsSame(coreGen)) core.Release();
        if (halo.IsSame(haloGen)) halo.Release();
    }
}
