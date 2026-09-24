internal static class Program
{
    private static async Task Main(string[] arguments)
    {
        if (await DatabaseTestCommand.TryRunAsync(arguments))
        {
            return;
        }

        RuleRegressionTests tests = new RuleRegressionTests();
        tests.Run();
    }
}

