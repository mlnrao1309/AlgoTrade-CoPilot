namespace AlgoTrading.Services
{
    /// <summary>
    /// Application-wide configuration values.
    /// Reads appsettings.json (if present) and exposes readonly properties.
    /// </summary>
    public static class AppConfiguration
    {
        public static string MsSqlDatabase { get; }

        static AppConfiguration()
        {
            string? conn = null;
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var path = System.IO.Path.Combine(baseDir, "appsettings.json");
                if (System.IO.File.Exists(path))
                {
                    using var stream = System.IO.File.OpenRead(path);
                    var doc = System.Text.Json.JsonDocument.Parse(stream);
                    var root = doc.RootElement;

                    // Prefer ConnectionStrings:MsSqlDatabase
                    if (root.TryGetProperty("ConnectionStrings", out var cs) && cs.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        if (cs.TryGetProperty("MsSqlDatabase", out var ms) && ms.ValueKind == System.Text.Json.JsonValueKind.String)
                            conn = ms.GetString();
                    }

                    // Fallback to top-level MsSqlDatabase
                    if (string.IsNullOrEmpty(conn) && root.TryGetProperty("MsSqlDatabase", out var top) && top.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        conn = top.GetString();
                    }
                }
            }
            catch
            {
                // ignore and fallback to default
            }

            MsSqlDatabase = string.IsNullOrEmpty(conn)
                ? @"Server=MANCHIKANTI\MANCHIKANTI;Database=Algo_Trading_CFCore;Trusted_Connection=True;TrustServerCertificate=True;"
                : conn!;
        }
    }
}
