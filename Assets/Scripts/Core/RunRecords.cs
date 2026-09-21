using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieShooter
{
    /// <summary>Everything a leaderboard needs to know about one finished run.</summary>
    [Serializable]
    public class RunSummary
    {
        public int score;
        public int wave;
        public int kills;
        public int level;
        public float seconds;
        public string finishedUtc;

        /// <summary>Filled in after submission: 1-based rank on the local board, or 0 if unranked.</summary>
        [NonSerialized] public int localRank;
        [NonSerialized] public bool isNewBest;
    }

    /// <summary>
    /// Where finished runs are reported. The local board is the only implementation today; an online
    /// leaderboard (Unity Gaming Services, Play Games) is a second implementation registered next to
    /// it - GameManager does not change.
    /// </summary>
    public interface ILeaderboardSink
    {
        void Submit(RunSummary run);
    }

    /// <summary>Top-10 runs on this device, persisted in PlayerPrefs as JSON.</summary>
    public class LocalLeaderboard : ILeaderboardSink
    {
        const string Key = "zws_local_leaderboard_v1";
        public const int Capacity = 10;

        [Serializable]
        class Board
        {
            public List<RunSummary> runs = new List<RunSummary>();
        }

        public static IReadOnlyList<RunSummary> Load()
        {
            return LoadBoard().runs;
        }

        public static int BestScore
        {
            get
            {
                var runs = LoadBoard().runs;
                return runs.Count > 0 ? runs[0].score : 0;
            }
        }

        public void Submit(RunSummary run)
        {
            if (run == null) return;

            var board = LoadBoard();
            int previousBest = board.runs.Count > 0 ? board.runs[0].score : 0;

            board.runs.Add(run);
            board.runs.Sort((a, b) => b.score.CompareTo(a.score));
            if (board.runs.Count > Capacity) board.runs.RemoveRange(Capacity, board.runs.Count - Capacity);

            int index = board.runs.IndexOf(run);
            run.localRank = index >= 0 ? index + 1 : 0;
            run.isNewBest = run.score > previousBest;

            try
            {
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(board));
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                // A full or unavailable prefs store must not break the game-over screen.
                Debug.LogWarning("[LocalLeaderboard] could not save: " + e.Message);
            }
        }

        static Board LoadBoard()
        {
            try
            {
                var json = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(json))
                {
                    var board = JsonUtility.FromJson<Board>(json);
                    if (board != null && board.runs != null) return board;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LocalLeaderboard] could not read, starting fresh: " + e.Message);
            }
            return new Board();
        }
    }
}
