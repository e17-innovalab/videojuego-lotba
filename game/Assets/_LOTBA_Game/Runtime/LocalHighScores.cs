using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lotba.Game
{
    internal sealed class LocalHighScores
    {
        internal const int Capacity = 5;
        internal const int NicknameLength = 5;
        internal const string Alphabet = "ABCDEFGHIJKLMNÑOPQRSTUVWXYZ";
        private const string PreferenceKey = "Lotba.CityRush.HighScores";

        [Serializable]
        internal sealed class Entry
        {
            public string nickname;
            public long score;
            public bool example;
        }

        [Serializable]
        private sealed class SavedRanking
        {
            public List<Entry> entries = new List<Entry>();
        }

        private SavedRanking ranking = new SavedRanking();
        public IReadOnlyList<Entry> Entries => ranking.entries;

        public LocalHighScores()
        {
            string json = PlayerPrefs.GetString(PreferenceKey);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var loaded = JsonUtility.FromJson<SavedRanking>(json);
                    if (loaded?.entries != null)
                    {
                        foreach (Entry entry in loaded.entries)
                            if (entry != null && IsNicknameValid(entry.nickname) && entry.score >= 0)
                                Insert(entry.example && entry.nickname == "MAN" && entry.score == 1337
                                    ? CreateExample() : entry);
                    }
                }
                catch (ArgumentException exception)
                {
                    Debug.LogWarning($"No se pudo leer el ranking local. {exception.Message}");
                }
            }
            if (Entries.Count == 0)
                Insert(CreateExample());
        }

        private static Entry CreateExample() => new Entry { nickname = "MANNY", score = 9999, example = true };

        public int RankFor(long score)
        {
            if (score < 0) return -1;
            int rank = 0;
            while (rank < Entries.Count && Entries[rank].score >= score) rank++;
            return rank < Capacity ? rank : -1;
        }

        public bool TrySave(string nickname, long score, out int rank)
        {
            rank = -1;
            if (!IsNicknameValid(nickname)) return false;
            int position = RankFor(score);
            if (position < 0) return false;
            var next = new SavedRanking { entries = new List<Entry>(Entries) };
            next.entries.Insert(position, new Entry { nickname = nickname, score = score });
            if (next.entries.Count > Capacity) next.entries.RemoveAt(Capacity);
            try
            {
                PlayerPrefs.SetString(PreferenceKey, JsonUtility.ToJson(next));
                PlayerPrefs.Save();
            }
            catch (PlayerPrefsException exception)
            {
                Debug.LogWarning($"No se pudo guardar el ranking local. {exception.Message}");
                return false;
            }
            ranking = next;
            rank = position;
            return true;
        }

        private int Insert(Entry entry)
        {
            int rank = RankFor(entry.score);
            if (rank < 0) return -1;
            ranking.entries.Insert(rank, entry);
            if (Entries.Count > Capacity) ranking.entries.RemoveAt(Capacity);
            return rank;
        }

        private static bool IsNicknameValid(string nickname)
        {
            if (string.IsNullOrEmpty(nickname) || nickname.Length > NicknameLength) return false;
            foreach (char letter in nickname)
                if (Alphabet.IndexOf(letter) < 0) return false;
            return true;
        }
    }
}
