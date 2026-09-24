using AlgorithmicEngine.Data;
using System;
using System.Collections.Generic;
using System.Text.Json;

public class StrategySnapshot
{
    public string StrategyName { get; set; } = "Sensex_Bearish_Retest";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Strategy Parameters
    public double RetestTolerancePct { get; set; } = 0.25; // 0.25% tolerance
    public double RsiUpperThreshold { get; set; } = 60.0;
    public double RsiLowerThreshold { get; set; } = 40.0;
    public int EmaPeriod { get; set; } = 5;
    public int DemaPeriod { get; set; } = 5;

    // Static Reference Lines (77 Pivots / SR Levels)
    public List<decimal> ReferenceLines { get; set; } = new List<decimal>();

    // Candle Dataset
    public List<ExtendedCandle> Candles { get; set; } = new List<ExtendedCandle>();
}

public class CandleSnapshot
{
    public DateTime Time { get; set; }
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public long Volume { get; set; }
}


public static class SnapshotManager
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void ExportSnapshot(StrategySnapshot snapshot, string filePath)
    {
        string jsonString = JsonSerializer.Serialize(snapshot, Options);
        File.WriteAllText(filePath, jsonString);
        Console.WriteLine($"[SNAPSHOT] Strategy snapshot saved to: {Path.GetFullPath(filePath)}");
    }

    public static StrategySnapshot ImportSnapshot(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Snapshot file not found: {filePath}");
        }

        string jsonString = File.ReadAllText(filePath);
        var snapshot = JsonSerializer.Deserialize<StrategySnapshot>(jsonString, Options);
        Console.WriteLine($"[SNAPSHOT] Strategy snapshot loaded successfully from: {Path.GetFullPath(filePath)}");
        return snapshot;
    }
}