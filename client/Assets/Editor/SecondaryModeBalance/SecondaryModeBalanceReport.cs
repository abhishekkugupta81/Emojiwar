using System;
using System.IO;
using UnityEngine;

public static class SecondaryModeBalanceReport
{
    public static string ResolveRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
    }

    public static string ResolveOutputDirectory(SecondaryModeBalanceSimulationConfig config)
    {
        var outputRoot = string.IsNullOrWhiteSpace(config.OutputRoot)
            ? Path.Combine(ResolveRepositoryRoot(), "SecondaryModeBalanceReports")
            : Path.GetFullPath(config.OutputRoot);

        return Path.Combine(outputRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
    }

    public static string BuildBatchCommand()
    {
        return "Unity -batchmode -projectPath <project> -executeMethod SecondaryModeBalanceSimulationRunner.RunDefaultBatchAndExit -quit";
    }

    public static void ValidateOutputDirectory(string outputDirectory)
    {
        foreach (var fileName in SecondaryModeBalanceStrategies.ExpectedReportFiles)
        {
            var path = Path.Combine(outputDirectory, fileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Expected report file was not created: {path}", path);
            }
        }
    }
}
