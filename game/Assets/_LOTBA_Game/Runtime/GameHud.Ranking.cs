using TMPro;
using UnityEngine;

namespace Lotba.Game
{
    public sealed partial class GameHud
    {
        [SerializeField] private Color rankingAccent = new Color32(255, 210, 102, 255);
        [SerializeField] private Color savedRankingRow = new Color32(33, 99, 81, 255);
        private Color[] rankingRowColors, nicknameSlotColors;
        private Color rankingOfferColor;
        [SerializeField] private TMP_Text[] rankingLetters = new TMP_Text[LocalHighScores.Capacity * LocalHighScores.NicknameLength];
        [SerializeField] private TMP_Text[] rankingScores = new TMP_Text[LocalHighScores.Capacity];
        [SerializeField] private UnityEngine.UI.Image[] rankingRows = new UnityEngine.UI.Image[LocalHighScores.Capacity];
        [SerializeField] private TMP_Text[] nicknameLetters = new TMP_Text[LocalHighScores.NicknameLength];
        [SerializeField] private UnityEngine.UI.Image[] nicknameSlots = new UnityEngine.UI.Image[LocalHighScores.NicknameLength];
        [SerializeField] private UnityEngine.UI.Button[] nicknameKeys = new UnityEngine.UI.Button[LocalHighScores.Alphabet.Length];
        private LocalHighScores highScores;
        [SerializeField] private RectTransform rankingOverlay, rankingCard, rankingList, rankingEditor, rankingActions;
        [SerializeField] private UnityEngine.UI.Button rankingButton, rankingOffer, rankingCloseButton, saveRankingButton, eraseNicknameButton;
        [SerializeField] private TMP_Text rankingOfferLabel, rankingCloseLabel, rankingEntryLabel;
        [Header("Récords · Anclas en modo consulta")]
        [SerializeField] private Rect rankingReadCard = Rect.MinMaxRect(.05f, .20f, .95f, .80f);
        [SerializeField] private Rect rankingReadList = Rect.MinMaxRect(.055f, .20f, .945f, .88f);
        [SerializeField] private Rect rankingReadActions = Rect.MinMaxRect(.055f, .045f, .945f, .16f);
        [SerializeField] private Rect rankingReadClose = Rect.MinMaxRect(.20f, 0f, .80f, 1f);
        private RankingRect[] rankingEntryLayout;
        private long? rankingCandidate;
        private string rankingNickname = string.Empty;
        private int highlightedRank = -1;

        public bool RankingOpen => rankingOverlay != null && rankingOverlay.gameObject.activeSelf;

        private readonly struct RankingRect
        {
            private readonly RectTransform target;
            private readonly Vector2 min, max, insetMin, insetMax;

            public RankingRect(RectTransform target)
            {
                this.target = target;
                min = target.anchorMin;
                max = target.anchorMax;
                insetMin = target.offsetMin;
                insetMax = target.offsetMax;
            }

            public void Apply(bool editing, Rect readAnchors)
            {
                target.anchorMin = editing ? min : readAnchors.min;
                target.anchorMax = editing ? max : readAnchors.max;
                target.offsetMin = insetMin;
                target.offsetMax = insetMax;
            }
        }

        private void BindRanking()
        {
            highScores = new LocalHighScores();
            rankingRowColors = System.Array.ConvertAll(rankingRows, row => row.color);
            nicknameSlotColors = System.Array.ConvertAll(nicknameSlots, slot => slot.color);
            rankingOfferColor = rankingOfferLabel.color;
            rankingEntryLayout = new[]
            {
                new RankingRect(rankingCard), new RankingRect(rankingList), new RankingRect(rankingActions),
                new RankingRect((RectTransform)rankingCloseButton.transform)
            };
            rankingButton.onClick.AddListener(() => OpenRanking(false));
            rankingOffer.onClick.AddListener(() => OpenRanking(true));
            rankingCloseButton.onClick.AddListener(CloseRanking);
            eraseNicknameButton.onClick.AddListener(EraseNicknameLetter);
            saveRankingButton.onClick.AddListener(SaveRanking);
            for (int i = 0; i < nicknameKeys.Length; i++)
            {
                char letter = LocalHighScores.Alphabet[i];
                nicknameKeys[i].onClick.AddListener(() => AddNicknameLetter(letter));
            }
        }

        private void SetRankingOffer(long score)
        {
            int rank = highScores.RankFor(score);
            rankingCandidate = rank >= 0 ? score : (long?)null;
            rankingOfferLabel.text = rank == 0 ? "NUEVO RÉCORD" : "GUARDAR MARCA";
            rankingOfferLabel.color = rank == 0 ? rankingAccent : rankingOfferColor;
            rankingOffer.gameObject.SetActive(rankingCandidate.HasValue);
        }

        private void ClearRankingOffer()
        {
            CloseRanking();
            rankingCandidate = null;
            rankingOffer.gameObject.SetActive(false);
        }

        private void OpenRanking(bool fromResults)
        {
            if (fromResults && !rankingCandidate.HasValue) return;
            rankingNickname = string.Empty;
            highlightedRank = -1;
            rankingOverlay.SetAsLastSibling();
            rankingOverlay.gameObject.SetActive(true);
            ShowRanking(fromResults);
        }

        public void CloseRanking() => rankingOverlay.gameObject.SetActive(false);

        private void ShowRanking(bool editing)
        {
            rankingEntryLayout[0].Apply(editing, rankingReadCard);
            rankingEntryLayout[1].Apply(editing, rankingReadList);
            rankingEntryLayout[2].Apply(editing, rankingReadActions);
            rankingEntryLayout[3].Apply(editing, rankingReadClose);
            rankingCloseLabel.text = editing ? "VOLVER" : "CERRAR";
            rankingEditor.gameObject.SetActive(editing);
            eraseNicknameButton.gameObject.SetActive(editing);
            saveRankingButton.gameObject.SetActive(editing);
            for (int row = 0; row < LocalHighScores.Capacity; row++)
            {
                var entry = row < highScores.Entries.Count ? highScores.Entries[row] : null;
                rankingRows[row].color = row == highlightedRank ? savedRankingRow : rankingRowColors[row];
                rankingScores[row].text = entry != null ? entry.score.ToString("N0") : "—";
                for (int i = 0; i < LocalHighScores.NicknameLength; i++)
                    rankingLetters[row * LocalHighScores.NicknameLength + i].text = entry != null && i < entry.nickname.Length
                        ? entry.nickname[i].ToString() : string.Empty;
            }
            if (editing)
            {
                rankingEntryLabel.text = $"APODO · {rankingCandidate.GetValueOrDefault():N0} PUNTOS";
                UpdateNickname();
            }
        }

        private void AddNicknameLetter(char letter)
        {
            if (rankingNickname.Length >= LocalHighScores.NicknameLength) return;
            rankingNickname += letter;
            UpdateNickname();
        }

        private void EraseNicknameLetter()
        {
            if (rankingNickname.Length == 0) return;
            rankingNickname = rankingNickname.Substring(0, rankingNickname.Length - 1);
            UpdateNickname();
        }

        private void UpdateNickname()
        {
            for (int i = 0; i < nicknameLetters.Length; i++)
            {
                nicknameLetters[i].text = i < rankingNickname.Length ? rankingNickname[i].ToString() : string.Empty;
                nicknameSlots[i].color = i == rankingNickname.Length ? rankingAccent : nicknameSlotColors[i];
            }
            eraseNicknameButton.interactable = saveRankingButton.interactable = rankingNickname.Length > 0;
        }

        private void SaveRanking()
        {
            if (!rankingCandidate.HasValue) return;
            if (!highScores.TrySave(rankingNickname, rankingCandidate.Value, out highlightedRank))
            {
                rankingEntryLabel.text = "NO SE PUDO GUARDAR";
                return;
            }
            rankingCandidate = null;
            rankingOffer.gameObject.SetActive(false);
            ShowRanking(false);
        }
    }
}
