using System.Collections.Generic;

namespace EmojiWar.Client.Gameplay.Clash
{
    public interface IEmojiClashOpponent
    {
        string PickUnit(EmojiClashMatchState state, IReadOnlyList<string> availableUnitKeys);
    }
}
