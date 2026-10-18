using System;
using System.Collections.Generic;
using UnityEngine;

// One pooled projectile type shared by all weapons. A weapon spawns it, sets velocity/life/
// hit radius, and plugs in behavior with callbacks (steering, on-hit, on-expire).
// Each shot hits a given enemy at most once (ClearHits lets e.g. a returning boomerang re-hit).
public class Shot : MonoBehaviour
{
    public Vector2 velocity;
    public float life;
    public float hitRadius;  // 0 = never hits enemies directly
    public int pierce = -1;  // hits before it's spent; -1 = unlimited
    public float spin;       // degrees / sec
    public float age;
    public string source; // DamageStats label, captured at spawn
    public Color trail;      // alpha 0 = no trail; otherwise drops a fading dot behind it
    public float trailSize;
    private float trailTimer;
    public Color ghost;      // alpha 0 = none; otherwise leaves fading copies of itself (motion blur)
    private float ghostTimer;

    public Action<Shot> onUpdate;              // steering etc. (may Release)
    public Action<Shot, EnemyHealth> onHit;    // may Release
    public Action<Shot> onExpire;              // life ran out

    public SpriteRenderer Renderer { get; private set; }
    public bool Alive => gameObject.activeSelf;
    // Bumps on every Spawn. Code that keeps a Shot reference across frames should store this
    // too: a released shot can be reused by another weapon, and IsSame tells them apart.
    public int Generation { get; private set; }
    public bool IsSame(int generation) => Alive && Generation == generation;
    public Vector2 Position => transform.position;

    private readonly HashSet<EnemyHealth> hitAlready = new HashSet<EnemyHealth>();
    private static readonly List<Collider2D> hits = new List<Collider2D>(32);
    private static readonly Stack<Shot> free = new Stack<Shot>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => free.Clear();

    public static Shot Spawn(Sprite sprite, Color color, float size, Vector2 pos, int sortingOrder = 20)
    {
        Shot s = null;
        while (free.Count > 0 && s == null) s = free.Pop(); // skip ones destroyed with an old scene
        if (s != null)
        {
            s.gameObject.SetActive(true);
        }
        else
        {
            var go = new GameObject("Shot");
            s = go.AddComponent<Shot>();
            s.Renderer = go.AddComponent<SpriteRenderer>();
            Lighting.Glow(s.Renderer);
        }

        s.transform.SetPositionAndRotation(pos, Quaternion.identity);
        s.transform.localScale = Vector3.one * size;
        s.Renderer.sprite = sprite;
        s.Renderer.color = color;
        s.Renderer.sortingOrder = sortingOrder;
        s.velocity = Vector2.zero;
        s.life = 5f;
        s.hitRadius = 0f;
        s.pierce = -1;
        s.spin = 0f;
        s.trail = Color.clear;
        s.trailSize = 0.14f;
        s.ghost = Color.clear;
        s.age = 0f;
        s.source = DamageStats.Source;
        s.Generation++;
        return s;
    }

    public bool HasHit(EnemyHealth e) => hitAlready.Contains(e);
    public void ClearHits() => hitAlready.Clear();

    public void Release()
    {
        if (!gameObject.activeSelf) return;
        onUpdate = null;
        onHit = null;
        onExpire = null;
        hitAlready.Clear();
        gameObject.SetActive(false);
        free.Push(this);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        DamageStats.Source = source;
        age += dt;

        onUpdate?.Invoke(this);
        if (!Alive) return;

        transform.position += (Vector3)(velocity * dt);
        if (spin != 0f) transform.Rotate(0f, 0f, spin * dt);
        if (trail.a > 0f && (trailTimer -= dt) <= 0f)
        {
            trailTimer = 0.03f;
            Juice.Sparks(transform.position, trail, 1, 0.4f, trailSize);
        }
        if (ghost.a > 0f && !Settings.LowParticles && (ghostTimer -= dt) <= 0f)
        {
            ghostTimer = 0.04f;
            AfterImage.Spawn(Renderer, ghost); // pooled
        }

        if (hitRadius > 0f && onHit != null)
        {
            foreach (Collider2D c in Bomb.OverlapEnemies(transform.position, hitRadius, hits))
            {
                if (!c.TryGetComponent(out EnemyHealth e) || e.IsDead || !hitAlready.Add(e)) continue;
                onHit(this, e);
                if (!Alive) return;
                if (pierce > 0 && --pierce == 0) { Release(); return; }
            }
        }

        if (age >= life)
        {
            onExpire?.Invoke(this);
            Release();
        }
    }
}
