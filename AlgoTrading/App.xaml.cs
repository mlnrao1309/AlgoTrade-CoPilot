using AlgoTrading.DataAccess.Calendar;
using AlgoTrading.DataAccess.Infrastructure;
using AlgoTrading.DataAccess.Infrastructure.DatabaseContext;
using AlgoTrading.Models;
using AlgoTrading.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;


namespace AlgoTrading
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        
        public static IServiceProvider? _serviceProvider;

        public static IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("Service provider is not initialized.");

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = new ServiceCollection();

            // Configuration
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

            IConfiguration configuration = builder.Build();
            services.AddSingleton(configuration);

            // Bind settings (ConnectionStrings section)
            var appSettings = new AppSettings();
            try
            {
                var cs = configuration.GetSection("ConnectionStrings");
                if (cs.Exists())
                {
                    var val = Environment.GetEnvironmentVariable("ALGOTRADING_SQL_CONNECTION_STRING") ?? cs["MsSqlDatabase"];
                    if (!string.IsNullOrEmpty(val)) appSettings.MsSqlDatabase = val;
                }
            }
            catch { }
            services.AddSingleton(appSettings);

            // Register ViewModels and Views
            services.AddSingleton<ViewModels.WebViewViewModel>();
            services.AddTransient<MainWindow>();

            string? connectionString = Environment.GetEnvironmentVariable("ALGOTRADING_SQL_CONNECTION_STRING")
                ?? configuration.GetConnectionString("MsSqlDatabase");
            //// Data access registrations

            if (!string.IsNullOrWhiteSpace(connectionString))
            {

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlServer(connectionString));

                services.AddScoped<DataAccess.Infrastructure.Repositories.IInstrumentEqRepository, DataAccess.Infrastructure.Repositories.InstrumentEqRepository>();
                services.AddScoped<DataAccess.Infrastructure.Repositories.IInstrumentFoRepository, DataAccess.Infrastructure.Repositories.InstrumentFoRepository>();
                services.AddScoped<DataAccess.Infrastructure.Repositories.IUnitOfWork, DataAccess.Infrastructure.Repositories.UnitOfWork>();
                services.AddScoped<IPlatformConfigurationProvider, PlatformConfigurationProvider>();
                services.AddSingleton<IPlatformConfigurationRuntime, PlatformConfigurationRuntime>();

                services.AddSingleton<ITradingCalendarRepository>(
                    new SqlTradingCalendarRepository(connectionString, commandTimeoutSeconds: 30));
                services.AddSingleton<ITradingCalendarProvider, RepositoryTradingCalendarProvider>();

                IConfigurationSection calendar = configuration.GetSection("TradingCalendar");
                services.AddSingleton(new TradingCalendarSelection(
                    calendar["ExchangeCode"] ?? "NSE",
                    calendar["SegmentCode"] ?? "CM",
                    calendar["Revision"] ?? throw new InvalidOperationException("TradingCalendar:Revision is required.")));
            }
            services.AddScoped<IInstrumentImportService, InstrumentImportService>();
            _serviceProvider = services.BuildServiceProvider();

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                try
                {
                    using (IServiceScope scope = _serviceProvider.CreateScope())
                    {
                        IPlatformConfigurationProvider provider = scope.ServiceProvider
                            .GetRequiredService<IPlatformConfigurationProvider>();
                        IPlatformConfigurationRuntime runtime = _serviceProvider
                            .GetRequiredService<IPlatformConfigurationRuntime>();
                        await runtime.InitializeAsync(provider);
                    }
                }
                catch (Exception exception)
                {
                    MessageBox.Show("Background market-data configuration is invalid or unavailable. "
                        + "Processing was not started.\n\n" + exception.Message,
                        "Market-data startup failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown(1);
                    return;
                }
            }

            // Create and show MainWindow from DI
            var main = _serviceProvider.GetRequiredService<MainWindow>();
            main.Show();
        }
    }

}
