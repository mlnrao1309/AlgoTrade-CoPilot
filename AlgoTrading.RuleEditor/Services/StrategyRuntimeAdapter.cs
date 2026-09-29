using System;
using AlgoTrading.Models.Rules;
using AlgoTrading.RuleEditor.Models;

namespace AlgoTrading.RuleEditor.Services
{
    /// <summary>
    /// Small, stable boundary used by the non-UI host. The snapshot owns a deep copy,
    /// so later editor changes cannot alter a running strategy.
    /// </summary>
    public sealed class StrategyRuntimeAdapter
    {
        private readonly StrategyDocumentService _documents = new StrategyDocumentService();

        public StrategyRuntimeSnapshot Load(string json)
        {
            StrategyDocument document = _documents.Deserialize(json, out bool importedLegacy);
            if (importedLegacy)
            {
                throw new ArgumentException("Import the legacy rule in the editor and complete its instrument, protection, and exit settings before runtime use.");
            }
            var errors = _documents.Validate(document);
            if (errors.Count > 0)
            {
                throw new ArgumentException(string.Join(Environment.NewLine, errors));
            }
            StrategyDocument immutableCopy = EditorCopyService.Copy(document);
            return new StrategyRuntimeSnapshot(
                immutableCopy,
                RuleBinder.Bind(_documents.ToEntryRule(immutableCopy)),
                RuleBinder.Bind(_documents.ToFullExitRule(immutableCopy)));
        }
    }

    public sealed class StrategyRuntimeSnapshot
    {
        public StrategyRuntimeSnapshot(StrategyDocument document, BoundRule entry, BoundRule fullExit)
        {
            Document = document;
            Entry = entry;
            FullExit = fullExit;
        }

        public StrategyDocument Document { get; }
        public BoundRule Entry { get; }
        public BoundRule FullExit { get; }
    }
}
