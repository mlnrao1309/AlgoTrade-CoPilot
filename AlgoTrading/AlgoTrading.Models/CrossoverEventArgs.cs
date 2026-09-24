namespace AlgoTrading.Models.Rules;

public sealed class CrossoverEventArgs : EventArgs
{
    public CrossoverOccurrence Occurrence { get; }

    public CrossoverEventArgs(CrossoverOccurrence occurrence)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        Occurrence = occurrence;
    }
}
