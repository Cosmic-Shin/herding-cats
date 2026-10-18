using UnityEngine;

// Healer Cat: hangs back behind the crowd (EnemyAI kiteRange on the prefab) and every few seconds sends out
// a green pulse that heals the cats around it. A floating green plus marks it as a priority target.
[RequireComponent(typeof(EnemyAI), typeof(EnemyHealth))]
public class HealerCat : MonoBehaviour, IPoolable
{
    public float interval = 3f;
    public float radius = 3.5f;
    [Range(0f, 1f)] public float healFraction = 0.25f; // of each cat's max HP per pulse

    private static readonly Color Green = new Color(0.45f, 1f, 0.5f);
    private static readonly System.Collections.Generic.List<Collider2D> hits = new System.Collections.Generic.List<Collider2D>(64);
    private static Sprite plus;
    private EnemyAI ai;
    private EnemyHealth health;
    private Transform mark;
    private float timer;

    private void Awake()
    {
        ai = GetComponent<EnemyAI>();
        health = GetComponent<EnemyHealth>();
        var sr = new GameObject("HealMark").AddComponent<SpriteRenderer>();
        sr.sprite = Plus;
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
        mark.localPosition = new Vector3(0f, 0.75f + 0.06f * Mathf.Sin(Time.time * 4f), 0f); // bobs over its head
        if (ai.IsAsleep || (timer -= Time.deltaTime) > 0f) return;
        timer = interval;

        Vector2 at = transform.position;
        ExplosionFx.Ring(at, radius, Green, 0.45f);
        Juice.Sparks(at, Green, 6, 3f, 0.12f);
        foreach (Collider2D c in Bomb.OverlapEnemies(at, radius, hits))
        {
            if (!c.TryGetComponent(out EnemyHealth cat) || cat.IsDead || c.GetComponent<BossAI>() != null) continue; // bosses don't get healed
            float healed = cat.Heal(cat.maxHealth * healFraction);
            if (healed < 0.5f) continue;
            DamagePopup.SpawnText((Vector2)c.transform.position + Vector2.up * 0.3f, "+" + Mathf.RoundToInt(healed), Green, 2.5f);
            Juice.SparksDir(c.transform.position, Vector2.up, 20f, Green, 2, 1.5f, 0.1f);
        }
    }

    // A chunky 7px plus, drawn once.
    private static Sprite Plus
    {
        get
        {
            if (plus != null) return plus;
            const int s = 7;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                    tex.SetPixel(x, y, (x >= 2 && x <= 4) || (y >= 2 && y <= 4) ? Color.white : Color.clear);
            tex.Apply();
            return plus = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }
    }
}
