using System;
using System.Collections.Generic;
using System.Linq;
using EmojiWar.Client.Gameplay.Clash;

public enum EmojiClashBalanceSide
{
    Player,
    Opponent
}

public interface IEmojiClashBalancePickStrategy
{
    string Name { get; }
    string PickUnit(EmojiClashBalanceMatchView view, EmojiClashBalanceSide side);
}

public sealed class EmojiClashBalanceMatchView
{
    public int TurnIndex;
    public int PlayerScore;
    public int OpponentScore;
    public int MatchSeed;
    public IReadOnlyCollection<string> PlayerUsedUnitKeys = Array.Empty<string>();
    public IReadOnlyCollection<string> OpponentUsedUnitKeys = Array.Empty<string>();
    public IReadOnlyList<string> PlayerAvailableUnitKeys = Array.Empty<string>();
    public IReadOnlyList<string> OpponentAvailableUnitKeys = Array.Empty<string>();
    public Random Random;
}

public sealed class EmojiClashUniformRandomStrategy : IEmojiClashBalancePickStrategy
{
    public string Name => "UniformRandom";

    public string PickUnit(EmojiClashBalanceMatchView view, EmojiClashBalanceSide side)
    {
        var available = side == EmojiClashBalanceSide.Player
            ? view.PlayerAvailableUnitKeys
            : view.OpponentAvailableUnitKeys;
        if (available == null || available.Count == 0)
        {
            return string.Empty;
        }

        return available[view.Random.Next(available.Count)];
    }
}

public sealed class EmojiClashCurrentBotStrategy : IEmojiClashBalancePickStrategy
{
    private readonly EmojiClashBotOpponent bot = new();

    public string Name => "CurrentBot";

    public string PickUnit(EmojiClashBalanceMatchView view, EmojiClashBalanceSide side)
    {
        if (side == EmojiClashBalanceSide.Opponent)
        {
            var state = new EmojiClashMatchState
            {
                CurrentTurnIndex = view.TurnIndex,
                PlayerScore = view.PlayerScore,
                OpponentScore = view.OpponentScore,
                MatchSeed = view.MatchSeed ^ 0x41A7,
                PlayerUsedUnitKeys = new HashSet<string>(view.PlayerUsedUnitKeys ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase),
                OpponentUsedUnitKeys = new HashSet<string>(view.OpponentUsedUnitKeys ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase)
            };
            return bot.PickUnit(state, view.OpponentAvailableUnitKeys);
        }

        var mirroredState = new EmojiClashMatchState
        {
            CurrentTurnIndex = view.TurnIndex,
            PlayerScore = view.OpponentScore,
            OpponentScore = view.PlayerScore,
            MatchSeed = view.MatchSeed ^ 0x6C8E,
            PlayerUsedUnitKeys = new HashSet<string>(view.OpponentUsedUnitKeys ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase),
            OpponentUsedUnitKeys = new HashSet<string>(view.PlayerUsedUnitKeys ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase)
        };
        return bot.PickUnit(mirroredState, view.PlayerAvailableUnitKeys);
    }
}

public sealed class EmojiClashFixedOrderStrategy : IEmojiClashBalancePickStrategy
{
    private readonly string[] order;

    public EmojiClashFixedOrderStrategy(IEnumerable<string> unitOrder)
    {
        order = (unitOrder ?? Array.Empty<string>())
            .Select(EmojiClashRules.NormalizeUnitKey)
            .Where(unitKey => !string.IsNullOrWhiteSpace(unitKey))
            .ToArray();
    }

    public string Name => "FixedOrder";

    public IReadOnlyList<string> Order => order;

    public string PickUnit(EmojiClashBalanceMatchView view, EmojiClashBalanceSide side)
    {
        var available = side == EmojiClashBalanceSide.Player
            ? view.PlayerAvailableUnitKeys
            : view.OpponentAvailableUnitKeys;
        if (available == null || available.Count == 0)
        {
            return string.Empty;
        }

        foreach (var unitKey in order)
        {
            if (available.Contains(unitKey, StringComparer.OrdinalIgnoreCase))
            {
                return unitKey;
            }
        }

        return available[0];
    }
}
