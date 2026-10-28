using UnityEngine;

public class DelayedBlast : MonoBehaviour
{
    private float radius, timer, damage;
    private bool eruption;
    private static readonly System.Collections.Generic.List<Collider2D> hits = new System.Collections.Generic.List<Collider2D>(64);

    public static void Spawn(Vector2 at, float radius, float delay, float damage, bool eruption = false)
    {
        AoeWarning.Spawn(at, radius, delay);
        var b = new GameObject("DelayedBlast").AddComponent<DelayedBlast>();
        b.transform.position = at;
        b.radius = radius;
        b.timer = delay;
        b.damage = damage;
        b.eruption = eruption;
    }

    private void Update()
    {
        if (eruption && Random.value < Time.deltaTime * 12f)
            Juice.Sparks((Vector2)transform.position + Random.insideUnitCircle * radius * 0.7f, new Color(1f, 0.5f, 0.15f), 1, 2f, 0.1f);
        if ((timer -= Time.deltaTime) > 0f) return;
        Vector2 at = transform.position;
        ExplosionFx.Spawn(at, radius, new Color(1f, 0.55f, 0.2f));
        CameraFollow.Shake(0.15f, 0.2f);
        Sfx.Play(Sfx.Id.Boom, 0.6f);
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && Vector2.Distance(player.transform.position, at) <= radius &&
            player.TryGetComponent(out PlayerHealth health)) health.TakeDamage(damage);
        if (eruption)
        {
            DamageStats.Source = "Eruption";
            foreach (Collider2D c in Bomb.OverlapEnemies(at, radius, hits))
                if (c.TryGetComponent(out EnemyHealth e) && !e.IsDead) e.TakeDamage(e.IsBoss ? 30f : 60f, at);
            BurningGround.Spawn(at, radius * 0.8f, 4f, 6f, burns: true);
            Juice.SparksDir(at, Vector2.up, 35f, new Color(1f, 0.6f, 0.15f), 14, 10f, 0.16f);
            Juice.Smoke(at, radius, 5);
        }
        Destroy(gameObject);
    }
}
