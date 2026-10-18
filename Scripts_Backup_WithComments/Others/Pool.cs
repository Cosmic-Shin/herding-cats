using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Implemented by pooled prefabs' scripts: reset per-life state here (called on every spawn,
// after the object is active), instead of relying on Start, which only runs once.
public interface IPoolable
{
    void OnSpawn();
}

// Prefab pooling: reuse deactivated instances instead of Instantiate/Destroy every time.
// Spawn/Despawn are drop-in replacements; Despawn on a non-pooled object just destroys it.
public static class Pool
{
    private static readonly Dictionary<GameObject, Stack<GameObject>> free = new Dictionary<GameObject, Stack<GameObject>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        free.Clear();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => free.Clear(); // old instances died with the scene

    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject go = null;
        if (free.TryGetValue(prefab, out Stack<GameObject> stack))
        {
            while (stack.Count > 0 && go == null) go = stack.Pop(); // skip anything destroyed externally
        }

        if (go != null)
        {
            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
        }
        else
        {
            go = Object.Instantiate(prefab, position, rotation);
            go.AddComponent<Pooled>().Init(prefab);
        }

        go.GetComponent<Pooled>().NotifySpawn();
        return go;
    }

    public static void Despawn(GameObject go)
    {
        if (go == null) return;
        if (!go.TryGetComponent(out Pooled pooled))
        {
            Object.Destroy(go); // not from the pool (e.g. bosses, test spawns)
            return;
        }
        if (!go.activeSelf) return; // already back in the pool

        go.SetActive(false);
        if (!free.TryGetValue(pooled.Prefab, out Stack<GameObject> stack))
        {
            stack = new Stack<GameObject>();
            free[pooled.Prefab] = stack;
        }
        stack.Push(go);
    }

    // Create instances up front (e.g. at scene start) so the first waves don't hitch.
    public static void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null) return;
        var spawned = new List<GameObject>(count);
        for (int i = 0; i < count; i++) spawned.Add(Spawn(prefab, new Vector3(9999f, 9999f), Quaternion.identity));
        foreach (GameObject go in spawned) Despawn(go);
    }
}

// Tags a pooled instance with its source prefab and caches its IPoolable components.
public class Pooled : MonoBehaviour
{
    public GameObject Prefab { get; private set; }
    private IPoolable[] poolables;

    public void Init(GameObject prefab)
    {
        Prefab = prefab;
        poolables = GetComponentsInChildren<IPoolable>(true);
    }

    public void NotifySpawn()
    {
        foreach (IPoolable p in poolables) p.OnSpawn();
    }
}
