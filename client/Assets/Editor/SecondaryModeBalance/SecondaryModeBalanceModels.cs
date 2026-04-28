using System;
using System.Collections.Generic;

[Serializable]
public sealed class SecondaryModeBalanceSimulationConfig
{
    public int Seed = 12345;
    public int RandomSquadMatchCount = 2000;
    public int HeuristicSquadMatchCount = 2000;
    public int HeuristicBanMatchCount = 2000;
    public int AutoFillRandomFormationMatchCount = 2000;
    public int AutoFillHeuristicFormationMatchCount = 2000;
    public int HeuristicMirrorMatchCount = 2000;
    public int SampledTopCoreCount = 250;
    public int SampledTopCoreOpponentSamples = 40;
    public string OutputRoot = string.Empty;
    public string OutputDirectory = string.Empty;
}

[Serializable]
public sealed class SecondaryModeBalanceSimulationManifest
{
    public string OutputDirectory = string.Empty;
    public int Seed;
    public List<string> GeneratedFiles = new();
    public List<string> ScenarioSummaries = new();
}
