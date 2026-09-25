using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AlgoTrading.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.Json;
using AlgoTrading.DataAccess.Calendar;


namespace AlgoTrading
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        
        public static IServiceProvider? _serviceProvider;

        public static IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("Service provider is not initialized.");

        protected override void OnStartup(StartupEventArgs e)
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
                    var val = cs["MsSqlDatabase"];
                    if (!string.IsNullOrEmpty(val)) appSettings.MsSqlDatabase = val;
                }
            }
            catch { }
            services.AddSingleton(appSettings);

            // Register ViewModels and Views
            services.AddSingleton<ViewModels.WebViewViewModel>();
            services.AddTransient<MainWindow>();

            string connectionString = configuration.GetConnectionString("MsSqlDatabase");
            //// Data access registrations
            try
            {
                services.AddDbContext<AlgoTrading.DataAccess.Data.AlgoTradingDbContext>(opts =>
                {
                    var cs = configuration.GetSection("ConnectionStrings")["MsSqlDatabase"];
                    if (!string.IsNullOrEmpty(cs))
                    {
                        opts.UseSqlServer(cs);
                    }
                });
            }
            catch (Exception ex)
            {

            }
            services.AddScoped<AlgoTrading.DataAccess.Repositories.ICandleRepository, AlgoTrading.DataAccess.Repositories.CandleRepository>();
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                services.AddSingleton<ITradingCalendarRepository>(
                    new SqlTradingCalendarRepository(connectionString, commandTimeoutSeconds: 30));
                services.AddSingleton<ITradingCalendarProvider, RepositoryTradingCalendarProvider>();

                IConfigurationSection calendar = configuration.GetSection("TradingCalendar");
                services.AddSingleton(new TradingCalendarSelection(
                    calendar["ExchangeCode"] ?? "NSE",
                    calendar["SegmentCode"] ?? "CM",
                    calendar["Revision"] ?? throw new InvalidOperationException("TradingCalendar:Revision is required.")));
            }

            _serviceProvider = services.BuildServiceProvider();

            // Create and show MainWindow from DI
            var main = _serviceProvider.GetRequiredService<MainWindow>();
            main.Show();
        }
    }

}
