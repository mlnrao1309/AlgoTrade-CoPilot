namespace AlgoTrading.Models.Rules;

internal sealed class CrossoverOccurrenceComparer : IComparer<CrossoverOccurrence>
{
    internal static CrossoverOccurrenceComparer Instance { get; } = new();

    public int Compare(CrossoverOccurrence? first, CrossoverOccurrence? second)
    {
        if (ReferenceEquals(first, second)) return 0;
        if (first == null) return -1;
        if (second == null) return 1;
        int timestampOrder = first.OccurredAt.CompareTo(second.OccurredAt);
        return timestampOrder != 0 ? timestampOrder : StringComparer.Ordinal.Compare(first.ConditionPath, second.ConditionPath);
    }
}
