using System.Windows;

namespace AlgoTrading.RuleEditor.Windows
{
    public partial class CommentEditorWindow : Window
    {
        public CommentEditorWindow(string comment)
        {
            InitializeComponent();
            CommentBox.Text = comment;
        }

        public string EditedComment
        {
            get
            {
                return CommentBox.Text;
            }
        }

        private void OnApply(object sender, RoutedEventArgs arguments)
        {
            DialogResult = true;
        }
    }
}
