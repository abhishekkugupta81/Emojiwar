using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class SecondaryModeBalanceSimulationRunner
{
    private const int DefaultSeed = 12345;

    [MenuItem("Emoji War/Secondary Mode/Run Balance Simulation")]
    public static void RunDefaultFromMenu()
    {
        try
        {
            var manifest = RunDefaultSimulation();
            EditorUtility.DisplayDialog(
                "Secondary Mode Balance Simulation",
                $"Reports written to:\n{manifest.OutputDirectory}",
                "OK");
            EditorUtility.RevealInFinder(manifest.OutputDirectory);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Secondary Mode Balance Simulation Failed", exception.Message, "OK");
        }
    }

    public static void RunDefaultBatch()
    {
        var manifest = RunDefaultSimulation();
        Debug.Log($"Secondary mode balance reports written to: {manifest.OutputDirectory}");
    }

    public static void RunDefaultBatchAndExit()
    {
        try
        {
            var manifest = RunDefaultSimulation();
            Debug.Log($"Secondary mode balance reports written to: {manifest.OutputDirectory}");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static SecondaryModeBalanceSimulationConfig CreateDefaultConfig()
    {
        return new SecondaryModeBalanceSimulationConfig
        {
            Seed = DefaultSeed
        };
    }

    public static SecondaryModeBalanceSimulationManifest RunDefaultSimulation()
    {
        return RunSimulation(CreateDefaultConfig());
    }

    public static SecondaryModeBalanceSimulationManifest RunSimulation(SecondaryModeBalanceSimulationConfig config)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        var repositoryRoot = SecondaryModeBalanceReport.ResolveRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, SecondaryModeBalanceStrategies.RelativeScriptPath);
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Secondary mode balance script was not found: {scriptPath}", scriptPath);
        }

        var outputDirectory = string.IsNullOrWhiteSpace(config.OutputDirectory)
            ? SecondaryModeBalanceReport.ResolveOutputDirectory(config)
            : Path.GetFullPath(config.OutputDirectory);

        Directory.CreateDirectory(outputDirectory);
        config.OutputDirectory = outputDirectory;

        var configPath = Path.Combine(outputDirectory, "secondary_mode_balance_config.json");
        File.WriteAllText(configPath, JsonUtility.ToJson(config, true));

        var processStartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = ResolveDenoExecutable(),
            Arguments = $"run --allow-read --allow-write \"{scriptPath}\" \"{configPath}\"",
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new System.Diagnostics.Process { StartInfo = processStartInfo };
        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            Debug.Log(stdout.Trim());
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            Debug.LogWarning(stderr.Trim());
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Secondary mode balance script failed with exit code {process.ExitCode}.");
        }

        SecondaryModeBalanceReport.ValidateOutputDirectory(outputDirectory);

        var manifest = new SecondaryModeBalanceSimulationManifest
        {
            OutputDirectory = outputDirectory,
            Seed = config.Seed
        };

        foreach (var fileName in SecondaryModeBalanceStrategies.ExpectedReportFiles)
        {
            manifest.GeneratedFiles.Add(fileName);
        }

        return manifest;
    }

    private static string ResolveDenoExecutable()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            var defaultInstall = Path.Combine(userProfile, ".deno", "bin", "deno.exe");
            if (File.Exists(defaultInstall))
            {
                return defaultInstall;
            }
        }

        return "deno";
    }
}
