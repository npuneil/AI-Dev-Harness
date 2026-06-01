using System;
using System.Management;
using System.Runtime.InteropServices;

namespace LocalAiDemos.Shared.AI;

public enum Silicon
{
    Unknown = 0,
    IntelCoreUltra,
    QualcommSnapdragonX,
    AmdRyzenAi,
    IntelGeneric,
    AmdGeneric,
}

/// <summary>
/// Detects the host silicon family. Uses WMI <c>Win32_Processor.Name</c> as the
/// authoritative source because <see cref="RuntimeInformation.ProcessArchitecture"/>
/// lies on Windows-on-ARM under x64 emulation.
/// </summary>
public static class SiliconDetector
{
    private static readonly Lazy<DetectionResult> _cached = new(Detect);

    public static Silicon Current => _cached.Value.Silicon;
    public static string CpuName => _cached.Value.CpuName;
    public static Architecture ProcessArchitecture => RuntimeInformation.ProcessArchitecture;
    public static Architecture OsArchitecture => RuntimeInformation.OSArchitecture;

    private record DetectionResult(Silicon Silicon, string CpuName);

    private static DetectionResult Detect()
    {
        var cpuName = QueryCpuName();
        var upper = cpuName.ToUpperInvariant();

        Silicon silicon;
        if (upper.Contains("SNAPDRAGON") || upper.Contains("QUALCOMM") || upper.Contains("ORYON"))
        {
            silicon = Silicon.QualcommSnapdragonX;
        }
        else if (upper.Contains("CORE(TM) ULTRA") || upper.Contains("CORE ULTRA"))
        {
            silicon = Silicon.IntelCoreUltra;
        }
        else if (upper.Contains("RYZEN AI"))
        {
            silicon = Silicon.AmdRyzenAi;
        }
        else if (upper.Contains("INTEL"))
        {
            silicon = Silicon.IntelGeneric;
        }
        else if (upper.Contains("AMD"))
        {
            silicon = Silicon.AmdGeneric;
        }
        else
        {
            silicon = Silicon.Unknown;
        }

        return new DetectionResult(silicon, cpuName);
    }

    private static string QueryCpuName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(name)) return name.Trim();
            }
        }
        catch
        {
            // WMI unavailable; fall through.
        }
        return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown CPU";
    }
}
