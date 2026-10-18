using System.Collections;
using UnityEngine;

// Super: Absolute Zero + Thunderstorm. Signature: a frozen storm cloud rolls over the crowd in a flurry of snow,
// raining blue lightning that freezes cats solid; a cat that's struck while already frozen shatters for big damage.
public class BlackIceStormWeapon : SuperWeapon
{
    private static readonly Color IceBolt = new Color(0.45f, 0.75f, 1f);

    private void Update()
    {
        if (Ready(4f)) StartCoroutine(Blizzard(Crowd(10f)));
    }

    private IEnumerator Blizzard(Vector2 at)
    {
        float reach = Area(3f), damage = Dmg(30f);
        Shot cloud = Shot.Spawn(Shapes.Art("cloud"), new Color(0.35f, 0.45f, 0.7f, 0f), reach * 1.4f, at + Vector2.up * 1.2f, 55);
        cloud.life = 10f;
        int gen = cloud.Generation;
        Sfx.Play(Sfx.Id.Whoosh, 0.6f, 0.6f);

        const float duration = 3.2f;
        float strike = 0.35f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            // drift with the crowd
            Transform near = NearestTo(at, reach * 2.5f);
            if (near != null) at = Vector2.MoveTowards(at, near.position, 1.5f * Time.deltaTime);
            bool flash = strike > 0.27f;
            if (cloud.IsSame(gen))
            {
                cloud.transform.position = at + Vector2.up * 1.2f;
                Color c = flash ? new Color(0.85f, 0.95f, 1f) : new Color(0.35f, 0.45f, 0.7f);
                c.a = 0.75f * Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((duration - t) / 0.4f);
                cloud.Renderer.color = c;
            }

            // snow
            if (Random.value < 0.6f)
                Juice.SparksDir(at + new Vector2(Random.Range(-reach, reach), 1.2f), new Vector2(-0.3f, -1f), 10f, new Color(0.9f, 0.97f, 1f), 1, 2.5f, 0.09f);

            if ((strike -= Time.deltaTime) <= 0f)
            {
                strike = 0.3f;
                Vector2 p = at + Random.insideUnitCircle * reach;
                Transform target = RandomNear(at, reach);
                if (target != null) p = target.position;
                LightningFx.Spawn(p, IceBolt, 1.4f);
                Lighting.Flash(p, 3f, IceBolt, 0.15f);
                StompFx.Spawn(p, Area(1.3f), ice: true); // frost where it struck
                Sfx.Play(Sfx.Id.Zap, 0.4f, Random.Range(1.1f, 1.4f));
                HitArea(p, Area(1.3f), damage, e =>
                {
                    EnemyAI cat = Cat(e);
                    if (cat == null) return;
                    if (cat.IsFrozen && !e.IsDead) // shatter
                    {
                        Hit(e, damage * 1.5f, p, knockback: false);
                        Shatter(e.transform.position, 1f);
                    }
                    cat.Freeze(1.6f);
                });
            }
            yield return null;
        }
        if (cloud.IsSame(gen)) cloud.Release();
    }

    private static Transform RandomNear(Vector2 at, float radius)
    {
        var list = EnemiesNear(at, radius);
        return list.Count > 0 ? list[Random.Range(0, list.Count)].transform : null;
    }
}
