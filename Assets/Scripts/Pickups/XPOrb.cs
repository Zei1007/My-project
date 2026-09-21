using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Pooled XP drop. Sits still until the player's pickup radius reaches it, then homes in -
    /// the magnet feel is what makes PickupRadius a desirable buff.
    /// </summary>
    public class XPOrb : MonoBehaviour, IPoolable
    {
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] OrbVisual visual;
        [SerializeField] float collectDistance = 0.35f;
        [SerializeField] float homingSpeed = 11f;
        [SerializeField] float acceleration = 22f;
        [SerializeField] float lifetime = 45f;

        // Live gems, so a magnet pickup can pull the whole floor in at once.
        static readonly System.Collections.Generic.List<XPOrb> _live = new System.Collections.Generic.List<XPOrb>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() { _live.Clear(); }

        /// <summary>Starts every gem on the floor homing to the player, wherever it is.</summary>
        public static void AttractAll()
        {
            for (int i = 0; i < _live.Count; i++)
            {
                var orb = _live[i];
                if (orb == null || orb._homing) continue;
                orb._homing = true;
                if (orb.visual != null) orb.visual.SetAttracting(true);
            }
        }

        float _value = 1f;
        float _speed;
        float _age;
        bool _homing;

        public void Configure(float value)
        {
            _value = Mathf.Max(0.01f, value);
            _speed = 0f;
            _homing = false;
            _age = 0f;
        }

        public void OnSpawned()
        {
            _speed = 0f;
            _homing = false;
            _age = 0f;
            if (visual != null) visual.SetAttracting(false);
            if (!_live.Contains(this)) _live.Add(this);
        }

        public void OnDespawned()
        {
            _live.Remove(this);
        }

        void Update()
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null) return;

            _age += Time.deltaTime;
            if (_age >= lifetime)
            {
                PoolManager.Despawn(gameObject);
                return;
            }

            Vector2 toPlayer = (Vector2)(player.transform.position - transform.position);
            float distance = toPlayer.magnitude;

            if (!_homing)
            {
                float radius = player.Stats.Get(StatType.PickupRadius);
                if (distance > radius) return;

                _homing = true;
                if (visual != null) visual.SetAttracting(true);
            }

            if (distance <= collectDistance)
            {
                Collect(player);
                return;
            }

            _speed = Mathf.Min(homingSpeed, _speed + acceleration * Time.deltaTime);
            transform.position += (Vector3)(toPlayer.normalized * (_speed * Time.deltaTime));
        }

        void Collect(PlayerController player)
        {
            if (player.Experience != null) player.Experience.AddXP(_value);
            PoolManager.Despawn(gameObject);
        }
    }
}
