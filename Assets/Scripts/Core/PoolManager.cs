using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>Implement on pooled prefabs that need to reset state between uses.</summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

    /// <summary>
    /// Prefab-keyed object pool. Wave counts get large, so zombies, projectiles and pickups all
    /// come from here rather than Instantiate/Destroy.
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        static PoolManager _instance;

        readonly Dictionary<GameObject, Stack<GameObject>> _pools = new Dictionary<GameObject, Stack<GameObject>>();
        readonly Dictionary<GameObject, GameObject> _instanceToPrefab = new Dictionary<GameObject, GameObject>();
        readonly Dictionary<GameObject, Transform> _roots = new Dictionary<GameObject, Transform>();

        public static PoolManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<PoolManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("~PoolManager");
                        _instance = go.AddComponent<PoolManager>();
                    }
                }
                return _instance;
            }
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>Creates instances up front so the first wave does not hitch.</summary>
        public static void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null) return;
            var mgr = Instance;
            var pool = mgr.GetPool(prefab);
            for (int i = 0; i < count; i++)
            {
                var go = mgr.CreateInstance(prefab);
                go.SetActive(false);
                pool.Push(go);
            }
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;
            var mgr = Instance;
            var pool = mgr.GetPool(prefab);

            GameObject go = null;
            while (pool.Count > 0 && go == null)
                go = pool.Pop();

            if (go == null)
                go = mgr.CreateInstance(prefab);

            var t = go.transform;
            t.SetPositionAndRotation(position, rotation);
            go.SetActive(true);

            var poolables = go.GetComponentsInChildren<IPoolable>(true);
            for (int i = 0; i < poolables.Length; i++)
                poolables[i].OnSpawned();

            return go;
        }

        public static T Spawn<T>(GameObject prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            var go = Spawn(prefab, position, rotation);
            return go == null ? null : go.GetComponent<T>();
        }

        public static void Despawn(GameObject instance)
        {
            if (instance == null) return;
            var mgr = Instance;

            GameObject prefab;
            if (!mgr._instanceToPrefab.TryGetValue(instance, out prefab))
            {
                // Not pooled (hand-placed in the scene, say) - just destroy it.
                Destroy(instance);
                return;
            }

            var poolables = instance.GetComponentsInChildren<IPoolable>(true);
            for (int i = 0; i < poolables.Length; i++)
                poolables[i].OnDespawned();

            instance.SetActive(false);
            instance.transform.SetParent(mgr.GetRoot(prefab), false);
            mgr.GetPool(prefab).Push(instance);
        }

        /// <summary>Returns every live instance of every pooled prefab to its pool.</summary>
        public static void DespawnAll()
        {
            if (_instance == null) return;
            var live = new List<GameObject>(_instance._instanceToPrefab.Keys);
            for (int i = 0; i < live.Count; i++)
            {
                var go = live[i];
                if (go != null && go.activeSelf) Despawn(go);
            }
        }

        Stack<GameObject> GetPool(GameObject prefab)
        {
            Stack<GameObject> pool;
            if (!_pools.TryGetValue(prefab, out pool))
            {
                pool = new Stack<GameObject>();
                _pools[prefab] = pool;
            }
            return pool;
        }

        Transform GetRoot(GameObject prefab)
        {
            Transform root;
            if (!_roots.TryGetValue(prefab, out root) || root == null)
            {
                var go = new GameObject("Pool_" + prefab.name);
                root = go.transform;
                root.SetParent(transform, false);
                _roots[prefab] = root;
            }
            return root;
        }

        GameObject CreateInstance(GameObject prefab)
        {
            var go = Instantiate(prefab, GetRoot(prefab));
            go.name = prefab.name;
            _instanceToPrefab[go] = prefab;
            return go;
        }
    }
}
