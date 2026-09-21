using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// XP accumulation and leveling. A level-up raises the event; GameManager pauses and the
    /// level-up panel presents the choices, so this component never knows about UI.
    /// </summary>
    public class PlayerExperience : MonoBehaviour
    {
        [SerializeField] int level = 1;
        [SerializeField] float current;

        PlayerController _player;
        PlayerDefinition _definition;
        float _required = 5f;

        public int Level { get { return level; } }
        public float Current { get { return current; } }
        public float Required { get { return _required; } }
        public float Normalized { get { return _required <= 0f ? 0f : Mathf.Clamp01(current / _required); } }
        public int MaxLevel { get { return _definition != null ? Mathf.Max(1, _definition.maxLevel) : 30; } }
        public bool IsMaxLevel { get { return level >= MaxLevel; } }

        public void Initialize(PlayerController player)
        {
            _player = player;
            _definition = player.Definition;
            level = 1;
            current = 0f;
            _required = _definition != null ? _definition.XPRequiredFor(1) : 5f;
            GameEvents.RaiseXPChanged(level, current, _required);
        }

        public void AddXP(float amount)
        {
            if (amount <= 0f || _player == null) return;

            float gained = amount * _player.Stats.Get(StatType.XPGain);

            // At the cap, soul gems still matter: they turn into score instead.
            if (IsMaxLevel)
            {
                ConvertToScore(gained);
                GameEvents.RaiseXPChanged(level, _required, _required);
                return;
            }

            current += gained;

            // A single large orb can clear more than one level - but never past the cap.
            int guard = 0;
            while (current >= _required && !IsMaxLevel && guard++ < 50)
            {
                current -= _required;
                level++;
                _required = _definition != null ? _definition.XPRequiredFor(level) : _required * 1.25f;
                GameEvents.RaiseLevelUp(level);
            }

            if (IsMaxLevel)
            {
                ConvertToScore(current);
                current = _required;   // full bar
            }

            GameEvents.RaiseXPChanged(level, current, _required);
        }

        void ConvertToScore(float xp)
        {
            if (xp <= 0f || GameManager.Instance == null) return;
            float rate = _definition != null ? _definition.scorePerOverflowXP : 5f;
            GameManager.Instance.AddScore(Mathf.RoundToInt(xp * rate));
        }
    }
}
