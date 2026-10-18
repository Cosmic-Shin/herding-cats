using UnityEngine;

public class XPGem : MonoBehaviour, IPoolable
{
    [Header("Value")]
    public int expValue = 1;

    [Header("Magnet Behavior")]
    public float flySpeed = 8f;
    public float collectDistance = 0.3f;

    private static readonly (int min, Color color, float size)[] Tiers =
    {
        (80, new Color(0.85f, 0.35f, 1f), 2.0f),
        (30, new Color(1f, 0.4f, 0.25f), 1.7f),
        (10, new Color(1f, 0.85f, 0.2f), 1.45f),
        (3, new Color(0.3f, 1f, 0.35f), 1.2f),
        (0, new Color(0.35f, 0.75f, 1f), 1f),
    };

    private const float HopTime = 0.12f;
    private static float lastCollect;
    private static int chain;
    private float pullTime;
    private bool isBeingCollected = false;
    private bool hasBeenCollected = false;

    private Vector3 baseScale;
    private Color tierColor;
    private Vector2 burstVelocity;
    private float burstTime;
    private TrailRenderer trail;

    private void Awake()
    {
        baseScale = transform.localScale;
        if (TryGetComponent(out SpriteRenderer sr)) Lighting.Glow(sr);
        trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.18f;
        trail.minVertexDistance = 0.05f;
        trail.emitting = false;
        trail.sortingOrder = 1;
        Lighting.Glow(trail);
        ApplyTier();
    }

    private void Start()
    {
        RegisterWithManager();
    }

    private void OnEnable()
    {
        RegisterWithManager();
    }

    private void OnDisable()
    {
        if (XPCollectorManager.Instance != null)
        {
            XPCollectorManager.Instance.Unregister(this);
        }
    }

    private void OnDestroy()
    {
        if (XPCollectorManager.Instance != null)
        {
            XPCollectorManager.Instance.Unregister(this);
        }
    }

    public bool IsIdle => !isBeingCollected && !hasBeenCollected && burstTime <= 0f;

    public void Magnetize()
    {
        if (IsIdle) isBeingCollected = true;
    }

    public void OnSpawn()
    {
        pullTime = 0f;
        isBeingCollected = false;
        hasBeenCollected = false;
        burstTime = 0f;
        burstVelocity = Vector2.zero;
        expValue = 1;
        ApplyTier();
        trail.Clear();
        trail.emitting = false;
    }

    public void SetValue(int value)
    {
        expValue = value;
        ApplyTier();
    }

    public void Launch(Vector2 velocity, float duration)
    {
        burstVelocity = velocity;
        burstTime = duration;
    }

    private void ApplyTier()
    {
        foreach (var tier in Tiers)
        {
            if (expValue < tier.min) continue;
            tierColor = tier.color;
            transform.localScale = baseScale * tier.size;
            if (trail != null)
            {
                trail.widthMultiplier = 0.14f * tier.size;
                trail.startColor = tier.color;
                trail.endColor = new Color(tier.color.r, tier.color.g, tier.color.b, 0f);
            }
            if (TryGetComponent(out SpriteRenderer sr))
            {
                sr.color = tier.color;
                sr.sortingOrder = 1;
            }
            return;
        }
    }

    public void HandleCollection(Transform playerTransform, PlayerStats playerStats)
    {
        if (playerTransform == null || playerStats == null || hasBeenCollected)
            return;

        if (burstTime > 0f)
        {
            burstTime -= Time.deltaTime;
            transform.position += (Vector3)(burstVelocity * Time.deltaTime);
            burstVelocity *= 1f - 6f * Time.deltaTime;
            return;
        }

        float distance = Vector2.Distance(transform.position, playerTransform.position);
        if (!isBeingCollected && Random.value < 0.004f)
            Juice.Sparks((Vector2)transform.position + Random.insideUnitCircle * 0.1f, Color.white, 1, 0.3f, 0.12f);

        if (isBeingCollected || distance <= playerStats.pickupRange)
        {
            isBeingCollected = true;
            pullTime += Time.deltaTime;

            Vector2 toPlayer = (Vector2)(playerTransform.position - transform.position);
            if (pullTime < HopTime)
            {
                transform.position -= (Vector3)(toPlayer.normalized * 3f * Time.deltaTime);
            }
            else
            {
                float speed = flySpeed + (pullTime - HopTime) * 30f;
                trail.emitting = true;
                transform.position = Vector2.MoveTowards(transform.position, playerTransform.position, speed * Time.deltaTime);
                if (Random.value < 0.4f) Juice.Sparks(transform.position, new Color(tierColor.r, tierColor.g, tierColor.b, 0.7f), 1, 0.2f, 0.1f);
            }

            if (Vector2.Distance(transform.position, playerTransform.position) <= collectDistance)
            {
                Collect(playerStats);
            }
        }
    }

    private void RegisterWithManager()
    {
        if (XPCollectorManager.Instance != null)
        {
            XPCollectorManager.Instance.Register(this);
        }
    }

    private void Collect(PlayerStats playerStats)
    {
        if (hasBeenCollected || playerStats == null)
            return;

        hasBeenCollected = true;
        playerStats.AddExp(expValue);
        chain = Time.time - lastCollect < 0.35f ? chain + 1 : 0;
        lastCollect = Time.time;
        Sfx.Play(Sfx.Id.Xp, 0.3f, 1f + Mathf.Min(chain, 24) * 0.03f + Mathf.Min(expValue, 20) * 0.02f);
        ExplosionFx.Spawn(playerStats.transform.position, 0.25f + 0.05f * Mathf.Min(expValue, 10), tierColor);

        if (XPCollectorManager.Instance != null)
        {
            XPCollectorManager.Instance.Unregister(this);
        }

        Pool.Despawn(gameObject);
    }
}
