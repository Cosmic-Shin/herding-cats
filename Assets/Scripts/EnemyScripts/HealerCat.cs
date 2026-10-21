using UnityEngine;

[RequireComponent(typeof(EnemyAI), typeof(EnemyHealth))]
public class HealerCat : MonoBehaviour, IPoolable
{
    public float interval = 3f;
    public float radius = 3.5f;
    [Range(0f, 1f)] public float healFraction = 0.25f;

    private static readonly Color Green = new Color(0.45f, 1f, 0.5f);
    private static readonly System.Collections.Generic.List<Collider2D> hits = new System.Collections.Generic.List<Collider2D>(64);
    private EnemyAI ai;
    private EnemyHealth health;
    private Transform mark;
    private float timer;

    private void Awake()
    {
        ai = GetComponent<EnemyAI>();
        health = GetComponent<EnemyHealth>();
        var sr = new GameObject("HealMark").AddComponent<SpriteRenderer>();
        sr.sprite = Shapes.Plus;
        sr.color = Green;
        sr.sortingOrder = GetComponent<SpriteRenderer>().sortingOrder + 2;
        mark = sr.transform;
        mark.SetParent(transform, false);
        mark.localScale = Vector3.one * 0.3f;
    }

    public void OnSpawn() => timer = interval * Random.Range(0.5f, 1f);

    private void Update()
    {
        if (health.IsDead) { mark.gameObject.SetActive(false); return; }
        mark.gameObject.SetActive(true);
        mark.localPosition = new Vector3(0f, 0.75f + 0.06f * Mathf.Sin(Time.time * 4f), 0f);
        if (ai.IsAsleep || (timer -= Time.deltaTime) > 0f) return;
        timer = interval;

        Vector2 at = transform.position;
        ExplosionFx.Ring(at, radius, Green, 0.45f);
        ExplosionFx.Crosses(at, radius, Green, 7);
        Juice.Sparks(at, Green, 6, 3f, 0.12f);
        foreach (Collider2D c in Bomb.OverlapEnemies(at, radius, hits))
        {
            if (!c.TryGetComponent(out EnemyHealth cat) || cat.IsDead || c.GetComponent<BossAI>() != null) continue;
            float healed = cat.Heal(cat.maxHealth * healFraction);
            if (healed < 0.5f) continue;
            DamagePopup.SpawnText((Vector2)c.transform.position + Vector2.up * 0.3f, "+" + Mathf.RoundToInt(healed), Green, 2.5f);
            Juice.SparksDir(c.transform.position, Vector2.up, 20f, Green, 2, 1.5f, 0.1f);
        }
    }
}
