using UnityEngine;

// Volatile elite's death blast: a red warning disc, then a boom that hurts Mira if she's still inside.
public class DelayedBlast : MonoBehaviour
{
    private float radius, timer, damage;

    public static void Spawn(Vector2 at, float radius, float delay, float damage)
    {
        AoeWarning.Spawn(at, radius, delay);
        var b = new GameObject("DelayedBlast").AddComponent<DelayedBlast>();
        b.transform.position = at;
        b.radius = radius;
        b.timer = delay;
        b.damage = damage;
    }

    private void Update()
    {
        if ((timer -= Time.deltaTime) > 0f) return;
        ExplosionFx.Spawn(transform.position, radius, new Color(1f, 0.55f, 0.2f));
        CameraFollow.Shake(0.15f, 0.2f);
        Sfx.Play(Sfx.Id.Boom, 0.6f);
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && Vector2.Distance(player.transform.position, transform.position) <= radius &&
            player.TryGetComponent(out PlayerHealth health)) health.TakeDamage(damage);
        Destroy(gameObject);
    }
}
