using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZombieShooter
{
    public enum GameState
    {
        Running,
        LevelUpPaused,
        Paused,
        GameOver,
    }

    /// <summary>Owns run state: what is paused, when the run ends, and how it restarts.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Scene References")]
        [SerializeField] PlayerController player;
        [SerializeField] WaveManager waveManager;

        [Header("Run")]
        [Tooltip("Kills earned this run. Meta-currency would be derived from this between runs.")]
        [SerializeField] int killCount;
        [SerializeField] int score;

        [Header("Scoring")]
        [Tooltip("Each wave adds this fraction to kill scores, so surviving longer is worth more per kill.")]
        [SerializeField] float scorePerWave = 0.05f;

        GameState _state = GameState.Running;
        float _runSeconds;
        readonly System.Collections.Generic.List<ILeaderboardSink> _leaderboards =
            new System.Collections.Generic.List<ILeaderboardSink> { new LocalLeaderboard() };

        public GameState State { get { return _state; } }
        public PlayerController Player { get { return player; } }
        public WaveManager Waves { get { return waveManager; } }
        public int KillCount { get { return killCount; } }
        public int Score { get { return score; } }
        public bool IsPaused { get { return _state == GameState.Paused; } }

        /// <summary>The run that just ended - read by the game-over screen.</summary>
        public RunSummary LastRun { get; private set; }

        /// <summary>Register an extra leaderboard (an online one, later). Local is always present.</summary>
        public void AddLeaderboard(ILeaderboardSink sink)
        {
            if (sink != null && !_leaderboards.Contains(sink)) _leaderboards.Add(sink);
        }

        /// <summary>Adds score from anything - kills, and XP earned after the level cap.</summary>
        public void AddScore(int amount)
        {
            if (amount <= 0 || _state == GameState.GameOver) return;
            score += amount;
            GameEvents.RaiseScoreChanged(score);
        }

        public static Vector3 PlayerPosition
        {
            get
            {
                var inst = Instance;
                return (inst != null && inst.player != null) ? inst.player.transform.position : Vector3.zero;
            }
        }

        void Awake()
        {
            Instance = this;

            // Static state is reset in GameEvents/EnemyRegistry before any scene object wakes,
            // so nothing is cleared here - peers have already subscribed by this point.
            Time.timeScale = 1f;
            killCount = 0;
            score = 0;
            _runSeconds = 0f;

            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (waveManager == null) waveManager = FindAnyObjectByType<WaveManager>();
        }

        void OnEnable()
        {
            GameEvents.EnemyKilled += OnEnemyKilled;
            GameEvents.PlayerDied += OnPlayerDied;
            GameEvents.LevelUp += OnLevelUp;
            GameEvents.BonusChoice += OnBonusChoice;
        }

        void OnDisable()
        {
            GameEvents.EnemyKilled -= OnEnemyKilled;
            GameEvents.PlayerDied -= OnPlayerDied;
            GameEvents.LevelUp -= OnLevelUp;
            GameEvents.BonusChoice -= OnBonusChoice;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        void Update()
        {
            if (_state == GameState.Running) _runSeconds += Time.unscaledDeltaTime;

            // Esc - which is also the Android back button in the Input System - or P toggles pause.
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame))
                TogglePause();

            // R restarts once the run is over; the level-up panel drives its own resume.
            if (_state == GameState.GameOver && UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame)
            {
                Restart();
            }
        }

        void OnEnemyKilled(ZombieController zombie, Vector3 position)
        {
            killCount++;
            GameEvents.RaiseKillCountChanged(killCount);

            // Tougher mobs are worth more, and every kill is worth a little more each wave.
            var definition = zombie != null ? zombie.Definition : null;
            if (definition == null) return;
            int wave = waveManager != null ? Mathf.Max(1, waveManager.CurrentWave) : 1;
            AddScore(Mathf.RoundToInt(definition.scoreValue * (1f + scorePerWave * (wave - 1))));
        }

        void OnLevelUp(int level)
        {
            // The level-up panel calls Resume() once a choice is taken.
            SetState(GameState.LevelUpPaused);
            Time.timeScale = 0f;
        }

        /// <summary>A blessing pauses exactly like a level-up; the panel resumes when the choice is taken.</summary>
        void OnBonusChoice()
        {
            if (_state == GameState.GameOver) return;
            SetState(GameState.LevelUpPaused);
            Time.timeScale = 0f;
        }

        /// <summary>Mobile pause button. Ignored while a level-up choice or the run's end is showing.</summary>
        public void TogglePause()
        {
            if (_state == GameState.GameOver || _state == GameState.LevelUpPaused) return;

            if (_state == GameState.Paused)
            {
                SetState(GameState.Running);
                Time.timeScale = 1f;
                GameEvents.RaisePauseChanged(false);
            }
            else
            {
                SetState(GameState.Paused);
                Time.timeScale = 0f;
                GameEvents.RaisePauseChanged(true);
            }
        }

        public void Resume()
        {
            if (_state == GameState.GameOver) return;
            SetState(GameState.Running);
            Time.timeScale = 1f;
        }

        void OnPlayerDied()
        {
            SetState(GameState.GameOver);
            Time.timeScale = 0f;

            int wave = waveManager != null ? waveManager.CurrentWave : 0;
            LastRun = new RunSummary
            {
                score = score,
                wave = wave,
                kills = killCount,
                level = player != null && player.Experience != null ? player.Experience.Level : 1,
                seconds = _runSeconds,
                finishedUtc = System.DateTime.UtcNow.ToString("o"),
            };
            for (int i = 0; i < _leaderboards.Count; i++) _leaderboards[i].Submit(LastRun);

            GameEvents.RaiseRunEnded(wave);
        }

        /// <summary>A call or notification backgrounds the app on a phone - pause rather than die.</summary>
        void OnApplicationPause(bool paused)
        {
            if (paused && _state == GameState.Running) TogglePause();
        }

        void SetState(GameState next)
        {
            _state = next;
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            GameEvents.Clear();
            EnemyRegistry.Clear();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void QuitToDesktop()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
