using System;
using System.Windows;
using AlgoTrading.RuleEditor.Windows;

namespace AlgoTrading.RuleEditor
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs arguments)
        {
            base.OnStartup(arguments);
            if (arguments.Args.Length == 2 && arguments.Args[0] == "--verify")
            {
                try
                {
                    Verification.EditorVerification verification = new Verification.EditorVerification();
                    verification.Run(arguments.Args[1]);
                    Shutdown(0);
                }
                catch (Exception exception)
                {
                    System.IO.File.WriteAllText(arguments.Args[1], exception.ToString());
                    Shutdown(1);
                }
                return;
            }
            MainWindow window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
    }
}
