using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Pooled floating damage numbers. Builds its own TextMesh pool at runtime rather than needing
    /// a prefab, and recycles a fixed budget - on a screen with 200 zombies, uncapped damage text
    /// is its own performance problem.
    /// </summary>
    public class DamageNumbers : MonoBehaviour
    {
        static DamageNumbers _instance;

        [SerializeField] int poolSize = 48;
        [SerializeField] float lifetime = 0.65f;
        [SerializeField] float riseDistance = 0.9f;
        [SerializeField] float characterSize = 0.075f;
        [SerializeField] Color normalColor = new Color(1f, 0.96f, 0.88f);
        [SerializeField] Color critColor = new Color(1f, 0.78f, 0.25f);
        [SerializeField] Color playerColor = new Color(1f, 0.42f, 0.42f);
        [Tooltip("Damage below this is not worth a number.")]
        [SerializeField] float minimumToShow = 1f;

        class Entry
        {
            public Transform transform;
            public TextMesh text;
            public MeshRenderer renderer;
            public float elapsed;
            public Vector3 origin;
            public Vector3 drift;
            public Color color;
            public bool active;
        }

        readonly List<Entry> _pool = new List<Entry>();
        int _cursor;

        void Awake()
        {
            _instance = this;
            BuildPool();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void BuildPool()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("DamageNumber");
                go.transform.SetParent(transform, false);

                var text = go.AddComponent<TextMesh>();
                text.font = font;
                text.fontSize = 64;
                text.characterSize = characterSize;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontStyle = FontStyle.Bold;

                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = font.material;
                mr.sortingLayerName = "Default";
                mr.sortingOrder = SortingBands.DamageNumber;   // always above the characters
                mr.enabled = false;

                _pool.Add(new Entry { transform = go.transform, text = text, renderer = mr });
            }
        }

        public static void Show(Vector3 worldPosition, float amount, bool crit)
        {
            if (_instance == null) return;
            _instance.Spawn(worldPosition, amount, crit ? _instance.critColor : _instance.normalColor, crit);
        }

        /// <summary>Damage the player took - coloured differently so it cannot be misread as output.</summary>
        public static void ShowPlayerDamage(Vector3 worldPosition, float amount)
        {
            if (_instance == null) return;
            _instance.Spawn(worldPosition, amount, _instance.playerColor, false);
        }

        void Spawn(Vector3 worldPosition, float amount, Color color, bool crit)
        {
            if (amount < minimumToShow) return;

            // Round-robin: the oldest number gets recycled once the budget is used up.
            var entry = _pool[_cursor];
            _cursor = (_cursor + 1) % _pool.Count;

            entry.elapsed = 0f;
            entry.active = true;
            entry.origin = worldPosition + new Vector3(Random.Range(-0.15f, 0.15f), 0f, 0f);
            entry.drift = new Vector3(Random.Range(-0.35f, 0.35f), 0f, 0f);
            entry.color = color;

            entry.text.text = Mathf.Max(1f, amount) < 10f
                ? amount.ToString("0.#")
                : Mathf.RoundToInt(amount).ToString();
            entry.text.characterSize = crit ? characterSize * 1.45f : characterSize;

            entry.transform.position = entry.origin;
            entry.renderer.enabled = true;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            var camera = Camera.main;

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (!e.active) continue;

                e.elapsed += dt;
                float t = e.elapsed / lifetime;

                if (t >= 1f)
                {
                    e.active = false;
                    e.renderer.enabled = false;
                    continue;
                }

                // Rise fast then ease out, fading over the back half.
                float rise = 1f - (1f - t) * (1f - t);
                e.transform.position = e.origin + Vector3.up * (rise * riseDistance) + e.drift * t;

                var c = e.color;
                c.a = t < 0.5f ? 1f : 1f - (t - 0.5f) * 2f;
                e.text.color = c;

                if (camera != null) e.transform.rotation = camera.transform.rotation;
            }
        }
    }
}
