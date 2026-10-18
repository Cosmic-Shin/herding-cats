using UnityEngine;

// Collab: Molotov + Lightning. Storm clouds drift toward the crowd and rain lightning on
// whatever is under them; every strike sets the ground on fire.
public class ThunderstormWeapon : Weapon
{
    private const int Clouds = 3;
    private static readonly Color StormGrey = new Color(0.55f, 0.55f, 0.68f);
    private const float CloudLife = 5f;
    private const float CloudRadius = 2.5f;   // strikes land on enemies this close to the cloud
    private const float DriftSpeed = 1.5f;
    private const float StrikeEvery = 0.5f;
    private const float StrikeDamage = 40f;
    private const float StrikeRadius = 1.4f;
    private const float FireTick = 8f;

    private void Update()
    {
        if (!Ready(2.2f)) return;

        for (int i = 0; i < Clouds; i++)
        {
            Transform t = RandomEnemy(9f);
            Vector2 at = t != null ? (Vector2)t.position : Pos + Random.insideUnitCircle * 4f;
            SpawnCloud(at);
        }
    }

    private void SpawnCloud(Vector2 at)
    {
        float reach = Area(CloudRadius);
        Shot cloud = Shot.Spawn(Shapes.Art("cloud"), StormGrey, reach * 1.2f, at, sortingOrder: 55);
        cloud.life = CloudLife;

        float damage = Dmg(StrikeDamage), tick = Dmg(FireTick), strikeRadius = Area(StrikeRadius);
        float nextStrike = 0.3f;

        cloud.onUpdate = c =>
        {
            // drift toward the nearest enemy, rumbling
            Transform near = NearestTo(c.Position, reach * 2.5f);
            c.velocity = near != null ? ((Vector2)near.position - c.Position).normalized * DriftSpeed : Vector2.zero;
            bool flash = c.age < nextStrike - StrikeEvery + 0.08f; // lights up white right after a strike
            Color tint = flash ? Color.white : StormGrey;
            tint.a = 0.55f * Mathf.Clamp01((CloudLife - c.age) / 0.4f) * Mathf.Clamp01(c.age / 0.25f); // fades in and out
            c.Renderer.color = tint;
            c.transform.localScale = Vector3.one * reach * 1.2f * (1f + 0.04f * Mathf.Sin(c.age * 5f)); // gentle bob
            if (Random.value < 0.5f) Juice.Sparks(c.Position + Random.insideUnitCircle * reach * 0.6f, new Color(0.6f, 0.75f, 1f, 0.6f), 1, 0.5f, 0.08f); // rain

            if (c.age < nextStrike) return;
            nextStrike += StrikeEvery;

            var under = EnemiesNear(c.Position, reach);
            if (under.Count == 0) return;
            Vector2 p = under[Random.Range(0, under.Count)].transform.position;

            LightningFx.Spawn(p);
            Sfx.Play(Sfx.Id.Crackle, 0.4f);
            foreach (Collider2D hit in EnemiesNear(p, strikeRadius))
                if (hit.TryGetComponent(out EnemyHealth e)) Hit(e, damage, p);
            BurningGround.Spawn(p, strikeRadius * 0.8f, 2f, tick);
            CameraFollow.Shake(0.03f, 0.06f);
        };
    }
}
