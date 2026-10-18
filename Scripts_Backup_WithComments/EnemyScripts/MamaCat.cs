using UnityEngine;

// Mama Cat: big, slow and tanky. Every few seconds she stops and gives birth to a pair of kittens,
// up to a limit, until you take her down.
[RequireComponent(typeof(EnemyAI), typeof(EnemyHealth))]
public class MamaCat : MonoBehaviour, IPoolable
{
    public GameObject kittenPrefab;
    public float interval = 4.5f;
    public int perLitter = 2;
    public int maxKittens = 6; // alive at once from this mama

    private static EnemySpawner spawner;
    private EnemyAI ai;
    private EnemyHealth health;
    private readonly System.Collections.Generic.List<(EnemyHealth cat, int life)> kittens = new System.Collections.Generic.List<(EnemyHealth, int)>();
    private float timer;

    private void Awake()
    {
        ai = GetComponent<EnemyAI>();
        health = GetComponent<EnemyHealth>();
    }

    public void OnSpawn()
    {
        timer = interval * 0.6f;
        kittens.Clear();
    }

    private void Update()
    {
        if (health.IsDead || kittenPrefab == null || ai.IsAsleep || (timer -= Time.deltaTime) > 0f) return;
        timer = interval;
        kittens.RemoveAll(k => k.cat == null || k.cat.IsDead || k.cat.Life != k.life || !k.cat.isActiveAndEnabled);
        if (kittens.Count >= maxKittens) return;

        if (spawner == null) spawner = FindFirstObjectByType<EnemySpawner>(); // rare: fine to look it up
        Vector2 at = transform.position;
        ai.Stun(0.5f); // pauses to give birth
        DamagePopup.SpawnText(at + Vector2.up * 0.5f, "mrrp!", new Color(1f, 0.75f, 0.85f), 3f);
        Juice.CatPuff(at, new Color(1f, 0.85f, 0.9f));
        for (int i = 0; i < perLitter && kittens.Count < maxKittens; i++)
        {
            GameObject kid = Pool.Spawn(kittenPrefab, at + Random.insideUnitCircle * 0.6f, Quaternion.identity);
            if (!kid.TryGetComponent(out EnemyHealth h)) continue;
            if (spawner != null) h.ScaleHealth(spawner.HealthMultiplier); // as tough as kittens spawned normally
            kittens.Add((h, h.Life));
        }
    }
}
