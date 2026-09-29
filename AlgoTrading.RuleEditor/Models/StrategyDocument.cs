using System.Collections.Generic;

namespace AlgoTrading.RuleEditor.Models
{
    public sealed class StrategyDocument
    {
        public int SchemaVersion { get; set; } = 1;
        public string Name { get; set; } = "Untitled strategy";
        public string Description { get; set; } = string.Empty;
        public string Exchange { get; set; } = "NSE";
        public string Segment { get; set; } = "Equity";
        public string Instrument { get; set; } = string.Empty;
        public string Session { get; set; } = "Regular";
        public string EvaluationTimeframe { get; set; } = "15minute";
        public string Direction { get; set; } = "Long";
        public string LevelSelection { get; set; } = "None";
        public string LevelBinding { get; set; } = "entry-level";
        public bool RetestRequired { get; set; }
        public int RetestWindowCandles { get; set; } = 5;
        public bool InvalidateOnOppositeCross { get; set; } = true;
        public EditorCondition Entry { get; set; } = new EditorCondition();
        public InitialProtection Protection { get; set; } = new InitialProtection();
        public List<PartialExitStage> PartialExits { get; set; } = new List<PartialExitStage>();
        public EditorCondition FullExit { get; set; } = new EditorCondition();
        public ExecutionPolicy Execution { get; set; } = new ExecutionPolicy();
    }

    public sealed class InitialProtection
    {
        public bool Enabled { get; set; } = true;
        public string Mode { get; set; } = "Fixed percent";
        public decimal Value { get; set; } = 1m;
        public string Reference { get; set; } = "Entry price";
    }

    public sealed class PartialExitStage
    {
        public bool Enabled { get; set; } = true;
        public string Label { get; set; } = "Target";
        public decimal QuantityPercent { get; set; } = 50m;
        public string QuantityBasis { get; set; } = "Original";
        public string Target { get; set; } = "1R";
    }

    public sealed class ExecutionPolicy
    {
        public string Evaluation { get; set; } = "Completed candle";
        public string StopTargetPriority { get; set; } = "Stop first";
        public string SameCandleActions { get; set; } = "One exit action";
        public string ReEntry { get; set; } = "Next setup";
    }
}
