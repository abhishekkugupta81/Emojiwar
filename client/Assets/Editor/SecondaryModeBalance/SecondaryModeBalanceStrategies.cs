public static class SecondaryModeBalanceStrategies
{
    public const string RelativeScriptPath = "supabase/functions/_shared/secondary-mode-balance-sim.ts";

    public static readonly string[] ScenarioNames =
    {
        "RandomSquadVsRandomSquad",
        "HeuristicSquadVsRandomSquad",
        "HeuristicBanVsRandomBan",
        "AutoFillVsRandomFormation",
        "AutoFillVsHeuristicFormation",
        "HeuristicSquadBanFormationMirror"
    };

    public static readonly string[] ExpectedReportFiles =
    {
        "secondary_balance_summary.md",
        "squad_win_rates.csv",
        "unit_post_ban_stats.csv",
        "ban_stats.csv",
        "formation_slot_stats.csv",
        "left_right_slot_symmetry.csv",
        "formation_shape_stats.csv",
        "auto_fill_vs_manual_heuristic.csv",
        "matchup_core_stats.csv",
        "core_shell_frequency.csv",
        "flagged_findings.csv"
    };
}
