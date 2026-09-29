using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AlgoTrading.Models.Rules;
using AlgoTrading.RuleEditor.Models;

namespace AlgoTrading.RuleEditor.Services
{
    public sealed class StrategyDocumentService
    {
        private readonly RuleDocumentAdapter _rules = new RuleDocumentAdapter();
        private static readonly JsonSerializerOptions Options = CreateOptions();

        private static JsonSerializerOptions CreateOptions()
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            };
            options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
            return options;
        }

        public string Serialize(StrategyDocument document)
        {
            return JsonSerializer.Serialize(document, Options);
        }

        public StrategyDocument Deserialize(string json, out bool importedLegacy)
        {
            using JsonDocument probe = JsonDocument.Parse(json);
            if (probe.RootElement.TryGetProperty("SchemaVersion", out JsonElement versionElement))
            {
                int version = versionElement.GetInt32();
                if (version != 1)
                {
                    throw new NotSupportedException("Strategy schema version " + version + " is not supported. This editor supports version 1.");
                }
                StrategyDocument? document = JsonSerializer.Deserialize<StrategyDocument>(json, Options);
                importedLegacy = false;
                return document ?? throw new InvalidDataException("The strategy file is empty.");
            }

            RuleDefinition legacy = RuleDefinitionJson.Deserialize(json);
            EditorDocument legacyEditor = _rules.FromDefinition(legacy);
            StrategyDocument imported = CreateBlank();
            imported.Name = legacyEditor.Name;
            imported.EvaluationTimeframe = legacyEditor.EvaluationTimeframe;
            imported.Entry = legacyEditor.Root;
            imported.Description = "Imported from a legacy single-rule file. Instrument, protection, and exits require review.";
            importedLegacy = true;
            return imported;
        }

        public StrategyDocument CreateBlank()
        {
            StrategyDocument document = new StrategyDocument();
            document.Entry.Children.Add(EditorExamples.NewComparison(document.EvaluationTimeframe));
            document.FullExit.Children.Add(EditorExamples.NewComparison(document.EvaluationTimeframe));
            return document;
        }

        public RuleDefinition ToEntryRule(StrategyDocument document)
        {
            return _rules.ToDefinition(new EditorDocument
            {
                Name = document.Name + " - entry",
                EvaluationTimeframe = document.EvaluationTimeframe,
                Root = document.Entry
            });
        }

        public RuleDefinition ToFullExitRule(StrategyDocument document)
        {
            return _rules.ToDefinition(new EditorDocument
            {
                Name = document.Name + " - full exit",
                EvaluationTimeframe = document.EvaluationTimeframe,
                Root = document.FullExit
            });
        }

        public IReadOnlyList<string> Validate(StrategyDocument document)
        {
            List<string> errors = new List<string>();
            if (string.IsNullOrWhiteSpace(document.Name)) errors.Add("Name: enter a strategy name.");
            if (string.IsNullOrWhiteSpace(document.Instrument)) errors.Add("Instrument: enter the tradable symbol supplied by the runtime.");
            if (document.RetestRequired && document.LevelSelection == "None") errors.Add("Entry.LevelSelection: a same-level retest needs a pivot or critical level.");
            if (document.RetestRequired && string.IsNullOrWhiteSpace(document.LevelBinding)) errors.Add("Entry.LevelBinding: give the selected level a stable binding name.");
            if (document.RetestRequired && document.RetestWindowCandles < 1) errors.Add("Entry.RetestWindowCandles: use at least one completed candle.");
            if (document.Protection.Enabled && document.Protection.Value <= 0) errors.Add("Protection.Value: enter a positive stop distance.");
            for (int index = 0; index < document.PartialExits.Count; index++)
            {
                PartialExitStage stage = document.PartialExits[index];
                if (stage.Enabled && (stage.QuantityPercent <= 0 || stage.QuantityPercent > 100))
                {
                    errors.Add("PartialExits[" + index + "].QuantityPercent: use a value above 0 and at most 100.");
                }
                if (stage.Enabled && string.IsNullOrWhiteSpace(stage.Target)) errors.Add("PartialExits[" + index + "].Target: enter a target or reference.");
            }
            decimal originalTotal = document.PartialExits.Where(stage => stage.Enabled && stage.QuantityBasis == "Original").Sum(stage => stage.QuantityPercent);
            if (originalTotal > 100) errors.Add("PartialExits: stages based on original quantity total more than 100%.");
            TryBind("Entry", () => RuleBinder.Bind(ToEntryRule(document)), errors);
            TryBind("FullExit", () => RuleBinder.Bind(ToFullExitRule(document)), errors);
            return errors;
        }

        private static void TryBind(string path, Action bind, List<string> errors)
        {
            try { bind(); }
            catch (Exception exception) { errors.Add(path + ": " + exception.Message); }
        }

        public string Summary(StrategyDocument document)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine(document.Name + " (schema 1)");
            text.AppendLine(document.Direction + " · " + document.Exchange + " / " + document.Segment + " / " + ValueOrMissing(document.Instrument));
            text.AppendLine(document.EvaluationTimeframe + " · " + document.Session + " session · completed candles only");
            text.AppendLine();
            text.AppendLine("ENTRY");
            text.AppendLine(EditorLabels.Condition(document.Entry, 0));
            if (document.LevelSelection != "None")
            {
                text.AppendLine("Level: " + document.LevelSelection + " bound as '" + document.LevelBinding + "'. Equal-priced levels retain this identity.");
            }
            if (document.RetestRequired)
            {
                text.AppendLine("Sequence: cross, then retest the same bound level within " + document.RetestWindowCandles + " completed candles" + (document.InvalidateOnOppositeCross ? "; opposite cross invalidates the setup." : "."));
            }
            text.AppendLine();
            text.AppendLine("PROTECTION");
            text.AppendLine(document.Protection.Enabled ? document.Protection.Mode + ": " + document.Protection.Value + " from " + document.Protection.Reference + ". Existing positions keep their last valid stop if reference data is missing." : "Disabled");
            text.AppendLine();
            text.AppendLine("PARTIAL EXITS");
            List<PartialExitStage> stages = document.PartialExits.Where(stage => stage.Enabled).ToList();
            if (stages.Count == 0) text.AppendLine("None");
            for (int index = 0; index < stages.Count; index++)
            {
                PartialExitStage stage = stages[index];
                text.AppendLine("Stage " + (index + 1) + ": close " + stage.QuantityPercent + "% of " + stage.QuantityBasis.ToLowerInvariant() + " quantity at " + stage.Target + " (" + stage.Label + ").");
            }
            text.AppendLine();
            text.AppendLine("FULL EXIT");
            text.AppendLine(EditorLabels.Condition(document.FullExit, 0));
            text.AppendLine("A full exit always closes all remaining quantity.");
            text.AppendLine();
            text.AppendLine("EXECUTION");
            text.AppendLine(document.Execution.Evaluation + " · " + document.Execution.StopTargetPriority + " · " + document.Execution.SameCandleActions + " · re-entry: " + document.Execution.ReEntry);
            return text.ToString().TrimEnd();
        }

        private static string ValueOrMissing(string value) => string.IsNullOrWhiteSpace(value) ? "instrument not selected" : value;
    }
}
