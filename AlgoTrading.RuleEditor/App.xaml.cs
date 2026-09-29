using System;
using System.Windows;
using AlgoTrading.RuleEditor.Windows;
using AlgoTrading.RuleEditor.Services;

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
            if (arguments.Args.Length == 4 && arguments.Args[0] == "--backtest")
            {
                try
                {
                    StrategyRuntimeSnapshot snapshot = new StrategyRuntimeAdapter().Load(System.IO.File.ReadAllText(arguments.Args[1]));
                    Models.BacktestResult result = new SimpleBacktestService().RunCsv(snapshot, arguments.Args[2], 1m);
                    System.IO.File.WriteAllText(arguments.Args[3], result.ToReport());
                    Shutdown(0);
                }
                catch (Exception exception)
                {
                    System.IO.File.WriteAllText(arguments.Args[3], exception.ToString());
                    Shutdown(1);
                }
                return;
            }
            if (arguments.Args.Length > 0)
            {
                MessageBox.Show("Usage:\n  AlgoTrading.RuleEditor.exe\n  AlgoTrading.RuleEditor.exe --verify <report-path>\n  AlgoTrading.RuleEditor.exe --backtest <strategy.json> <candles.csv> <report-path>",
                    "Invalid command line", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown(2);
                return;
            }
            MainWindow window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
    }
}
