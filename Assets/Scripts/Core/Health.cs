using System;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// HP pool with an optional armor buffer in front of it. Incoming damage is scaled by the
    /// DamageTaken stat, which is where "/2 damage taken" style buffs land.
    /// </summary>
    public class Health : MonoBehaviour
    {
        [SerializeField] bool isPlayer;
        [SerializeField] float armorRegenPerSecond;

        StatSheet _stats;
        float _current;
        float _currentArmor;
        float _lastMax;
        float _lastMaxArmor;
        bool _dead;

        public event Action<float, float> Changed;        // current, max
        public event Action<float, GameObject> Damaged;   // amount actually dealt, source
        public event Action<GameObject> Died;             // killer

        public float Current { get { return _current; } }
        public float Max { get { return _stats != null ? _stats.Get(StatType.MaxHealth) : 1f; } }
        public float Armor { get { return _currentArmor; } }
        public float MaxArmor { get { return _stats != null ? _stats.Get(StatType.Armor) : 0f; } }
        public bool IsDead { get { return _dead; } }
        public float Normalized { get { return Max <= 0f ? 0f : Mathf.Clamp01(_current / Max); } }

        /// <summary>Binds the sheet and fills HP + armor. Called on spawn, and again on respawn.</summary>
        public void Init(StatSheet stats)
        {
            if (_stats != null) _stats.Changed -= OnStatsChanged;
            _stats = stats;
            if (_stats != null) _stats.Changed += OnStatsChanged;

            _dead = false;
            _current = Max;
            _currentArmor = MaxArmor;
            _lastMax = Max;
            _lastMaxArmor = MaxArmor;
            Broadcast();
        }

        void OnDestroy()
        {
            if (_stats != null) _stats.Changed -= OnStatsChanged;
        }

        void Update()
        {
            if (_dead || armorRegenPerSecond <= 0f) return;
            float max = MaxArmor;
            if (_currentArmor < max)
            {
                _currentArmor = Mathf.Min(max, _currentArmor + armorRegenPerSecond * Time.deltaTime);
                if (isPlayer) GameEvents.RaisePlayerArmorChanged(_currentArmor, max);
            }
        }

        /// <summary>
        /// A MaxHealth increase grants the same amount of current HP, so +25 max takes 100/100 to
        /// 125/125 rather than leaving an empty 25 on the bar. Decreases only clamp - losing a buff
        /// should not deal damage. Armor follows the same rule, which is what fills a shield pickup.
        /// </summary>
        void OnStatsChanged()
        {
            float max = Max;
            if (!_dead && max > _lastMax) _current += max - _lastMax;
            _lastMax = max;
            _current = Mathf.Min(_current, max);

            float maxArmor = MaxArmor;
            if (!_dead && maxArmor > _lastMaxArmor) _currentArmor += maxArmor - _lastMaxArmor;
            _lastMaxArmor = maxArmor;
            _currentArmor = Mathf.Min(_currentArmor, maxArmor);

            Broadcast();
        }

        public void TakeDamage(float rawAmount, GameObject source = null)
        {
            if (_dead || rawAmount <= 0f) return;

            float amount = rawAmount * (_stats != null ? _stats.Get(StatType.DamageTaken) : 1f);
            if (amount <= 0f) return;

            // Armor soaks first, then the remainder hits HP.
            if (_currentArmor > 0f)
            {
                float absorbed = Mathf.Min(_currentArmor, amount);
                _currentArmor -= absorbed;
                amount -= absorbed;
                if (isPlayer) GameEvents.RaisePlayerArmorChanged(_currentArmor, MaxArmor);
            }

            if (amount > 0f)
                _current = Mathf.Max(0f, _current - amount);

            if (Damaged != null) Damaged(rawAmount, source);
            Broadcast();

            if (_current <= 0f) Kill(source);
        }

        public void Heal(float amount)
        {
            if (_dead || amount <= 0f) return;
            _current = Mathf.Min(Max, _current + amount);
            Broadcast();
        }

        public void RefillArmor()
        {
            _currentArmor = MaxArmor;
            if (isPlayer) GameEvents.RaisePlayerArmorChanged(_currentArmor, MaxArmor);
        }

        public void Kill(GameObject source = null)
        {
            if (_dead) return;
            _dead = true;
            _current = 0f;
            Broadcast();
            if (Died != null) Died(source);
            if (isPlayer) GameEvents.RaisePlayerDied();
        }

        void Broadcast()
        {
            if (Changed != null) Changed(_current, Max);
            if (!isPlayer) return;

            GameEvents.RaisePlayerHealthChanged(_current, Max);
            GameEvents.RaisePlayerArmorChanged(_currentArmor, MaxArmor);
        }
    }
}
