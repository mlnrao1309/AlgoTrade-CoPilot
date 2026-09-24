using AlgoTrading.Models.Rules;

internal sealed class CrossoverTestCollector
{
    internal List<CrossoverOccurrence> Detected { get; } = new();
    internal List<CrossoverOccurrence> Completed { get; } = new();

    internal void Attach(RuleCrossoverMonitor monitor)
    {
        monitor.CrossoverDetected += OnDetected;
        monitor.CrossoverNextCandleAvailable += OnNextCandle;
    }

    private void OnDetected(object? sender, CrossoverEventArgs arguments)
    {
        Detected.Add(arguments.Occurrence);
    }

    private void OnNextCandle(object? sender, CrossoverEventArgs arguments)
    {
        Completed.Add(arguments.Occurrence);
    }
}
