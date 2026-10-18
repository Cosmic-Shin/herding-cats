using System;
using System.Collections.Generic;
using UnityEngine;

public class Shot : MonoBehaviour
{
    public Vector2 velocity;
    public float life;
    public float hitRadius;
    public int pierce = -1;
    public float spin;
    public float age;
    public string source;
    public Color trail;
    public float trailSize;
    private float trailTimer;
    public Color ghost;
    private float ghostTimer;

    public Action<Shot> onUpdate;
    public Action<Shot, EnemyHealth> onHit;
    public Action<Shot> onExpire;

    public SpriteRenderer Renderer { get; private set; }
    public bool Alive => gameObject.activeSelf;
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
        while (free.Count > 0 && s == null) s = free.Pop();
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
            AfterImage.Spawn(Renderer, ghost);
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
