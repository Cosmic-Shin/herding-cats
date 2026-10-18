using UnityEngine;

// Gem Thief: harmless, but it goes for the XP gems lying around instead of you, scooping them up, and runs
// away when you get close. Kill it and it drops everything it stole, plus 50%.
[RequireComponent(typeof(EnemyAI), typeof(EnemyHealth))]
public class GemThief : MonoBehaviour, IPoolable
{
    public float seekRange = 9f;
    public float fleeRange = 4f;
    public float grabRange = 0.45f;

    private static readonly Color Gold = new Color(1f, 0.85f, 0.35f);
    private EnemyAI ai;
    private EnemyHealth health;
    private static Transform player;
    private XPGem target;
    private int stolen;
    private float retarget, glint;

    private void Awake()
    {
        ai = GetComponent<EnemyAI>();
        health = GetComponent<EnemyHealth>();
        ai.steer = Steer;
    }

    public void OnSpawn()
    {
        target = null;
        stolen = 0;
    }

    // dir = toward the player. Flee if they're close, else head for the nearest loose gem, else wander after them.
    private Vector2 Steer(Vector2 toPlayer)
    {
        if (player == null) { GameObject p = GameObject.FindGameObjectWithTag("Player"); if (p != null) player = p.transform; }
        if (player != null && Vector2.Distance(player.position, transform.position) < fleeRange) return -toPlayer;
        if (target != null && target.IsIdle && target.isActiveAndEnabled)
            return ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
        return toPlayer;
    }

    private void Update()
    {
        if (health.IsDead) return;
        XPCollectorManager gems = XPCollectorManager.Instance;
        if (gems != null && (retarget -= Time.deltaTime) <= 0f)
        {
            retarget = 0.3f;
            XPGem near = gems.NearestIdle(transform.position);
            target = near != null && Vector2.Distance(near.transform.position, transform.position) <= seekRange ? near : null;
        }

        if (target != null && target.IsIdle && target.isActiveAndEnabled &&
            Vector2.Distance(target.transform.position, transform.position) <= grabRange)
        {
            stolen += target.expValue;
            health.xpValue += Mathf.CeilToInt(target.expValue * 1.5f); // its loot, paid out with interest on death
            Juice.Sparks(target.transform.position, Gold, 4, 2f, 0.1f);
            DamagePopup.SpawnText((Vector2)transform.position + Vector2.up * 0.4f, "yoink!", Gold, 2.5f);
            Pool.Despawn(target.gameObject);
            target = null;
        }

        // the more it carries, the more it glitters
        if (stolen > 0 && (glint -= Time.deltaTime) <= 0f)
        {
            glint = Mathf.Lerp(0.6f, 0.1f, Mathf.Clamp01(stolen / 30f));
            Juice.Sparks((Vector2)transform.position + Random.insideUnitCircle * 0.3f, Gold, 1, 0.5f, 0.1f);
        }
    }
}
