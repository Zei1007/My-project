using System;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// Lightweight static event bus. Everything gameplay-significant flows through here so UI and
    /// systems never hold direct references to each other. GameManager calls Clear() when a run
    /// starts, which keeps subscriptions sane if Enter Play Mode options disable domain reload.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<float, float> PlayerHealthChanged;   // current, max
        public static event Action<float, float> PlayerArmorChanged;    // current, max
        public static event Action PlayerDied;

        public static event Action<int, float, float> XPChanged;        // level, current, toNext
        public static event Action<int> LevelUp;                        // new level

        public static event Action<ZombieController, Vector3> EnemyKilled;
        public static event Action<int> KillCountChanged;

        public static event Action<int> WaveStarted;
        public static event Action<int> WaveCompleted;
        public static event Action<float> WaveTimerChanged;

        public static event Action<BuffDefinition> BuffApplied;
        public static event Action<BuffDefinition> BuffExpired;

        public static event Action<int> RunEnded;                       // final wave reached

        public static event Action<ZombieController, BossDefinition> BossSpawned;
        public static event Action<int, int, string> BossPhaseChanged;  // index, total, phase name
        public static event Action<BossDefinition> BossDefeated;

        /// <summary>A blessing was picked up: offer a free buffs-only choice.</summary>
        public static event Action BonusChoice;
        public static event Action<string, Color> LootCollected;        // label, colour
        public static event Action<int> ScoreChanged;                   // total score
        public static event Action<bool> PauseChanged;                  // paused?

        public static void RaisePlayerHealthChanged(float cur, float max)
        {
            if (PlayerHealthChanged != null) PlayerHealthChanged(cur, max);
        }

        public static void RaisePlayerArmorChanged(float cur, float max)
        {
            if (PlayerArmorChanged != null) PlayerArmorChanged(cur, max);
        }

        public static void RaisePlayerDied()
        {
            if (PlayerDied != null) PlayerDied();
        }

        public static void RaiseXPChanged(int level, float cur, float toNext)
        {
            if (XPChanged != null) XPChanged(level, cur, toNext);
        }

        public static void RaiseLevelUp(int level)
        {
            if (LevelUp != null) LevelUp(level);
        }

        public static void RaiseEnemyKilled(ZombieController z, Vector3 pos)
        {
            if (EnemyKilled != null) EnemyKilled(z, pos);
        }

        public static void RaiseKillCountChanged(int count)
        {
            if (KillCountChanged != null) KillCountChanged(count);
        }

        public static void RaiseWaveStarted(int wave)
        {
            if (WaveStarted != null) WaveStarted(wave);
        }

        public static void RaiseWaveCompleted(int wave)
        {
            if (WaveCompleted != null) WaveCompleted(wave);
        }

        public static void RaiseWaveTimerChanged(float t)
        {
            if (WaveTimerChanged != null) WaveTimerChanged(t);
        }

        public static void RaiseBuffApplied(BuffDefinition b)
        {
            if (BuffApplied != null) BuffApplied(b);
        }

        public static void RaiseBuffExpired(BuffDefinition b)
        {
            if (BuffExpired != null) BuffExpired(b);
        }

        public static void RaiseRunEnded(int wave)
        {
            if (RunEnded != null) RunEnded(wave);
        }

        /// <summary>
        /// Wipes every subscription before any scene object wakes up. This must not live in a
        /// MonoBehaviour's Awake: peers on the same object subscribe during their own Awake/OnEnable,
        /// and clearing from there silently unsubscribes whoever got there first.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Clear();
        }

        public static void RaiseBossSpawned(ZombieController boss, BossDefinition definition)
        {
            if (BossSpawned != null) BossSpawned(boss, definition);
        }

        public static void RaiseBossPhaseChanged(int index, int total, string phaseName)
        {
            if (BossPhaseChanged != null) BossPhaseChanged(index, total, phaseName);
        }

        public static void RaiseBossDefeated(BossDefinition definition)
        {
            if (BossDefeated != null) BossDefeated(definition);
        }

        public static void RaiseBonusChoice()
        {
            if (BonusChoice != null) BonusChoice();
        }

        public static void RaiseLootCollected(string label, Color color)
        {
            if (LootCollected != null) LootCollected(label, color);
        }

        public static void RaiseScoreChanged(int score)
        {
            if (ScoreChanged != null) ScoreChanged(score);
        }

        public static void RaisePauseChanged(bool paused)
        {
            if (PauseChanged != null) PauseChanged(paused);
        }

        public static void Clear()
        {
            PlayerHealthChanged = null;
            PlayerArmorChanged = null;
            PlayerDied = null;
            XPChanged = null;
            LevelUp = null;
            EnemyKilled = null;
            KillCountChanged = null;
            WaveStarted = null;
            WaveCompleted = null;
            WaveTimerChanged = null;
            BuffApplied = null;
            BuffExpired = null;
            RunEnded = null;
            BossSpawned = null;
            BossPhaseChanged = null;
            BossDefeated = null;
            BonusChoice = null;
            LootCollected = null;
            ScoreChanged = null;
            PauseChanged = null;
        }
    }
}
