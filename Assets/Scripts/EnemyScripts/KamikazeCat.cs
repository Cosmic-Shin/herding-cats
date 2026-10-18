using UnityEngine;

[RequireComponent(typeof(EnemyAI), typeof(EnemyHealth))]
public class KamikazeCat : MonoBehaviour, IPoolable
{
    public float triggerRange = 1.6f;
    public float fuse = 0.9f;
    public float blastRadius = 1.8f;
    public float blastMultiplier = 2f;

    private EnemyAI ai;
    private EnemyHealth health;
    private SpriteRenderer sr;
    private Transform player;
    private float fuseLeft;
    private bool primed;

    private void Awake()
    {
        ai = GetComponent<EnemyAI>();
        health = GetComponent<EnemyHealth>();
        sr = GetComponent<SpriteRenderer>();
    }

    public void OnSpawn() => primed = false;

    private void Update()
    {
        if (health.IsDead) return;
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            player = p.transform;
        }

        if (!primed)
        {
            if (ai.IsAsleep || Vector2.Distance(player.position, transform.position) > triggerRange) return;
            primed = true;
            fuseLeft = fuse;
            ai.Stun(fuse + 0.1f);
            AoeWarning.Spawn(transform.position, blastRadius, fuse);
            DamagePopup.SpawnText((Vector2)transform.position + Vector2.up * 0.4f, "!!", new Color(1f, 0.35f, 0.2f), 5f);
            Sfx.Play(Sfx.Id.Alert, 0.3f, 1.6f);
            return;
        }

        fuseLeft -= Time.deltaTime;
        float rate = Mathf.Lerp(8f, 30f, 1f - fuseLeft / fuse);
        sr.color = Mathf.Repeat(Time.time * rate, 1f) > 0.5f ? Color.white : new Color(1f, 0.35f, 0.3f);
        if (Random.value < 0.5f) Juice.SparksDir((Vector2)transform.position + Vector2.up * 0.3f, Vector2.up, 40f, new Color(1f, 0.8f, 0.3f), 1, 3f, 0.1f);
        if (fuseLeft > 0f) return;

        Vector2 at = transform.position;
        ExplosionFx.Spawn(at, blastRadius);
        CameraFollow.Shake(0.12f, 0.15f);
        Sfx.Play(Sfx.Id.Boom, 0.6f);
        if (Vector2.Distance(player.position, at) <= blastRadius && player.TryGetComponent(out PlayerHealth hp)) hp.TakeDamage(ai.contactDamage * blastMultiplier);
        sr.color = health.BaseColor;
        Pool.Despawn(gameObject);
    }
}
