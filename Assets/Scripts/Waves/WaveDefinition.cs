using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>
    /// One authored wave. Waves past the end of the authored list are generated procedurally by
    /// WaveManager from the same building blocks, so the table only needs to cover the hand-tuned
    /// opening of a run.
    /// </summary>
    [CreateAssetMenu(fileName = "WAV_New", menuName = "Zombie Shooter/Wave Definition")]
    public class WaveDefinition : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public ZombieDefinition zombie;
            [Min(1)] public int count = 8;
            [Tooltip("Seconds into the wave before this group starts spawning.")]
            public float startDelay;
        }

        [Header("Identity")]
        public string label = "Wave";

        [Header("Composition")]
        public List<Entry> entries = new List<Entry>();

        [Header("Pacing")]
        [Tooltip("Seconds between individual spawns.")]
        public float spawnInterval = 0.45f;
        [Tooltip("Wave ends after this many seconds even if mobs remain. 0 means clear-to-advance only.")]
        public float duration;
        [Tooltip("Wave ends once every spawned mob is dead.")]
        public bool clearToAdvance = true;
        [Tooltip("Breather before the next wave starts.")]
        public float intermission = 3f;

        public int TotalCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < entries.Count; i++) total += entries[i].count;
                return total;
            }
        }
    }
}
