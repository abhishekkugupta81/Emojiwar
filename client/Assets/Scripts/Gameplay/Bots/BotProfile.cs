using UnityEngine;

namespace EmojiWar.Client.Gameplay.Bots
{
    public enum BotDifficulty
    {
        Practice,
        Smart
    }

    [CreateAssetMenu(fileName = "BotProfile", menuName = "EmojiWar/Gameplay/Bot Profile")]
    public sealed class BotProfile : ScriptableObject
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private BotDifficulty difficulty;
        [SerializeField] private float aggression = 0.5f;
        [SerializeField] private float defenseBias = 0.5f;
        [SerializeField] private float comboBias = 0.5f;

        public string Id => id;
        public BotDifficulty Difficulty => difficulty;
        public float Aggression => aggression;
        public float DefenseBias => defenseBias;
        public float ComboBias => comboBias;
    }
}
