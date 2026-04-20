using UnityEngine;

namespace EmojiWar.Client.Content
{
    [CreateAssetMenu(fileName = "EmojiDefinition", menuName = "EmojiWar/Content/Emoji Definition")]
    public sealed class EmojiDefinition : ScriptableObject
    {
        [System.Serializable]
        public struct EmojiBattleStats
        {
            public int hp;
            public int attack;
            public int speed;
            public PreferredRow preferredRow;
        }

        [SerializeField] private EmojiId id;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private EmojiRole role;
        [SerializeField] private string primaryVerb = string.Empty;
        [SerializeField] private string strengths = string.Empty;
        [SerializeField] private string weaknesses = string.Empty;
        [SerializeField] private string whySummary = string.Empty;
        [SerializeField] private EmojiBattleStats battleStats;

        public EmojiId Id => id;
        public string DisplayName => displayName;
        public EmojiRole Role => role;
        public string PrimaryVerb => primaryVerb;
        public string Strengths => strengths;
        public string Weaknesses => weaknesses;
        public string WhySummary => whySummary;
        public EmojiBattleStats BattleStats => battleStats;
    }
}
