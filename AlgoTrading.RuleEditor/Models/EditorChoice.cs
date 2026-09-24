namespace AlgoTrading.RuleEditor.Models
{
    public sealed class EditorChoice
    {
        private readonly string _key;
        private readonly string _label;

        public EditorChoice(string key, string label)
        {
            _key = key;
            _label = label;
        }

        public string Key
        {
            get
            {
                return _key;
            }
        }

        public string Label
        {
            get
            {
                return _label;
            }
        }
    }
}
