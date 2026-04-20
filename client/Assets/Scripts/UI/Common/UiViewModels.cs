using System;

namespace EmojiWar.Client.UI.Common
{
    [Flags]
    public enum UnitCardState
    {
        Default = 0,
        Selected = 1 << 0,
        Banned = 1 << 1,
        Disabled = 1 << 2
    }

    public enum PhaseStep
    {
        Squad = 1,
        Ban = 2,
        Formation = 3,
        Result = 4
    }

    public enum MatchUiPanelState
    {
        Queue,
        Ban,
        Formation,
        Waiting,
        Result,
        Error
    }

    [Serializable]
    public sealed class ReplayMoment
    {
        public string Caption = string.Empty;
        public string ReasonCode = string.Empty;
    }
}

