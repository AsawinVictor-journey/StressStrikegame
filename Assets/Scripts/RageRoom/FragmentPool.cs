using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reuses fragment GameObjects instead of Instantiate()/Destroy()'ing them every time a
/// DestructibleObject breaks. Rage Room can break up to 8 objects (5-15 fragments each) in
/// quick succession, and without pooling every break spawned a fresh batch of GameObjects
/// (plus a matching Destroy() call fragmentLifetime later) — a real GC/instantiation cost
/// concentrated right at the moment things shatter, which is exactly when a hitch is most
/// noticeable. See the perf diagnosis this was built from: fragment pooling was identified
/// as the top unconfirmed suspect after mesh colliders, particle systems, and physics
/// settings were all ruled out by direct inspection.
///
/// ONE POOL, KEYED BY PREFAB — not five separate pool classes (RedChair/Glass/Monitor/Chair/
/// Desk). A dictionary keyed by prefab means this file needs zero changes if a fragment
/// prefab is renamed, swapped, or a new one added later — it doesn't need to know the
/// fragment roster up front. The cost is one dictionary lookup per Get/Release call, which
/// is irrelevant next to the Instantiate/Destroy cost it replaces. Five hand-written pools
/// would only pay for themselves if each fragment type needed genuinely different pooling
/// behavior; none of them do — every fragment is just "a Rigidbody + BoxCollider mesh",
/// only the mesh differs.
///
/// Lazily pre-warms per prefab on first request (not all five up front at scene load), so a
/// play session that only breaks chairs never pays to instantiate 20 idle glass shards it'll
/// never use. If a pool ever does run dry (more simultaneous fragments of one type than
/// prewarmSizePerPrefab), Get() falls back to a plain Instantiate() rather than blocking or
/// erroring — a safety net for an unpredictable destruction chain, not a resizing strategy;
/// the pool doesn't grow its retained size afterward.
/// </summary>
public class FragmentPool : MonoBehaviour
{
    public static FragmentPool Instance { get; private set; }

    [Tooltip("Fragments pre-warmed per prefab the first time that prefab is requested. " +
             "DestructibleObject.maxPieces defaults to 15 per break, so this covers one " +
             "full break of a single fragment type with some headroom for a second object " +
             "of the same type breaking shortly after.")]
    public int prewarmSizePerPrefab = 20;

    private readonly Dictionary<GameObject, Stack<GameObject>> pools = new Dictionary<GameObject, Stack<GameObject>>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>Takes a fragment instance from the pool (instantiating fresh only if the pool is empty), positioned and rotated in place, ready to use.</summary>
    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        Stack<GameObject> pool = GetOrCreatePool(prefab);
        GameObject instance = pool.Count > 0 ? pool.Pop() : Instantiate(prefab);

        Transform t = instance.transform;
        t.SetParent(null);
        t.SetPositionAndRotation(position, rotation);
        t.localScale = prefab.transform.localScale;

        Rigidbody rb = instance.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Clear any velocity left over from this instance's previous life as debris —
            // otherwise a reused fragment could launch off already moving.
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        instance.SetActive(true);
        return instance;
    }

    /// <summary>Deactivates the fragment and returns it to its pool after `delay` seconds, instead of destroying it.</summary>
    public void Release(GameObject instance, GameObject prefab, float delay)
    {
        StartCoroutine(ReleaseAfterDelay(instance, prefab, delay));
    }

    private IEnumerator ReleaseAfterDelay(GameObject instance, GameObject prefab, float delay)
    {
        yield return new WaitForSeconds(delay);

        // The instance (or this pool, on scene teardown) may already be gone by the time
        // this fires — nothing to return in that case.
        if (instance == null) yield break;

        instance.SetActive(false);
        instance.transform.SetParent(transform);
        GetOrCreatePool(prefab).Push(instance);
    }

    private Stack<GameObject> GetOrCreatePool(GameObject prefab)
    {
        if (pools.TryGetValue(prefab, out Stack<GameObject> pool))
            return pool;

        pool = new Stack<GameObject>(prewarmSizePerPrefab);
        pools[prefab] = pool;

        for (int i = 0; i < prewarmSizePerPrefab; i++)
        {
            GameObject instance = Instantiate(prefab, transform);
            instance.SetActive(false);
            pool.Push(instance);
        }

        return pool;
    }
}
