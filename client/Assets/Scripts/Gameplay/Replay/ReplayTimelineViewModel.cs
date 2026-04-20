using System.Collections.Generic;
using EmojiWar.Client.Gameplay.Contracts;

namespace EmojiWar.Client.Gameplay.Replay
{
    public sealed class ReplayTimelineViewModel
    {
        public string WhyText { get; set; } = string.Empty;
        public IReadOnlyList<string> WhyChain { get; set; } = new List<string>();
        public IReadOnlyList<ReplayEventDto> ReplayEvents { get; set; } = new List<ReplayEventDto>();
    }
}
