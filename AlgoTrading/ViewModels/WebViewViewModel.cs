using AlgoTrading.Helpers;
using AlgoTrading.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Identity.Client;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace AlgoTrading.ViewModels
{
    public class WebViewViewModel : INotifyPropertyChanged
    {
        private readonly AlgoTrading.DataAccess.Repositories.ICandleRepository _candleRepository;
        private readonly Models.AppSettings _appSettings;
        
        private bool _isProcessWebResourceResponseReceivedEnabled = false;
        public bool IsProcessWebResourceResponseReceivedEnabled
        {
            get => _isProcessWebResourceResponseReceivedEnabled;
            set
            {
                if (_isProcessWebResourceResponseReceivedEnabled == value) return;
                _isProcessWebResourceResponseReceivedEnabled = value;
                OnPropertyChanged();
            }
        }

        public string ConnectionString => _appSettings?.MsSqlDatabase ?? string.Empty;

        private string _uri = "https://www.bing.com";

        private string? _lastResourceUri;
        private string? _lastResponseStatus;

        private readonly System.Net.Http.HttpClient _client = new System.Net.Http.HttpClient();
        public string? LastResourceUri
        {
            get => _lastResourceUri;
            private set
            {
                if (_lastResourceUri == value) return;
                _lastResourceUri = value;
                OnPropertyChanged();
            }

        }
        private string? _accessToken;
        public string? AccessToken
        {
            set
            {
                if (!Equals(_accessToken, value))
                {
                    _accessToken = value;
                    OnPropertyChanged();
                }
            }
            get => _accessToken;
        }

        readonly MarketWatchModel? marketWatchModel = null;
        private HistoricalDataModel? historicalDataModel = null;
        public WebViewViewModel(AlgoTrading.DataAccess.Repositories.ICandleRepository candleRepository, Models.AppSettings appSettings)
        {
            _candleRepository = candleRepository ?? throw new ArgumentNullException(nameof(candleRepository));
            _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
            marketWatchModel = new MarketWatchModel();

            marketWatchModel.MarketWatchDataUpdated += (s, e) =>
            { 
                // Handle the event when market watch data is updated
                // For example, you can update the UI or perform other actions
                // You can access the updated data through marketWatchModel.ActiveSubscriptions
                marketWatchModel.ActiveSubscriptions.ToList().ForEach(kvp =>
                {
                    Debug.WriteLine($"Trading Symbol: {kvp.Key}, Instrument Token: {kvp.Value}");
                });
            };
            //Reads offline market watch data from the local file and populates the ActiveSubscriptions dictionary
            //var marketData = marketWatchModel.GetOfflineMarketWatchDataAsync();


            //var data =  _candleRepository.GetCandlesAsync(256265,"minute",limit:100000).ContinueWith( e => {
            //    if (e.IsCompletedSuccessfully)
            //    {
            //        var candles = e.Result;
            //        // Process the retrieved candles as needed
            //        foreach (var candle in candles)
            //        {
            //            // Example: Log or display candle information
            //            Console.WriteLine($"Timestamp: {candle.Timestamp}, Open: {candle.Open}, Close: {candle.Close}");
            //        }
            //    }
            //    else if (e.IsFaulted)
            //    {
            //        // Handle any exceptions that occurred during the async operation
            //        Console.WriteLine($"Error retrieving candles: {e.Exception?.GetBaseException().Message}");
            //    }
            //}); 
        }
        public ObservableCollection<StatusMessage> StatusMessages { get; } = new ObservableCollection<StatusMessage>();

        // Selected items (multi-selection) bound from the ListBox via behavior
        public ObservableCollection<StatusMessage> SelectedStatusMessages { get; } = new ObservableCollection<StatusMessage>();

        public string? LastResponseStatus
        {
            get => _lastResponseStatus;
            private set
            {
                if (_lastResponseStatus == value) return;
                _lastResponseStatus = value;
                OnPropertyChanged();
            }
        }

        public string Uri
        {
            get => _uri;
            set
            {
                if (_uri == value) return;
                _uri = value;
                OnPropertyChanged();
            }
        }

        private string? _navigationSource;
        public string? NavigationSource
        {
            get => _navigationSource;
            private set
            {
                if (_navigationSource == value) return;
                _navigationSource = value;
                OnPropertyChanged();
            }
        }

        private string? _errorMessage;
        public string? ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (_errorMessage == value) return;
                _errorMessage = value;
                OnPropertyChanged();
            }
        }

        private ICommand? _navigateCommand;
        public ICommand NavigateCommand => _navigateCommand ??= new RelayCommand(_ => ExecuteNavigate());

        private ICommand? _webResourceResponseReceivedCommand;
        public ICommand WebResourceResponseReceivedCommand => _webResourceResponseReceivedCommand ??= new RelayCommand(p =>
        {
            // Expect the CoreWebView2WebResourceResponseReceivedEventArgs from the behavior
            if (p is CoreWebView2WebResourceResponseReceivedEventArgs args)
            {
                OnWebResourceResponseReceived(null, args);
            }
        });

        private ICommand? _processAccessTokenCommand;
        public ICommand ProcessAccessTokenCommand => _processAccessTokenCommand ??= new RelayCommand(p =>
        {
            if (p is string token)
            {
                AccessToken = token;
                var msg = new StatusMessage("Access Token Captured", $"Access Token: {token}", StatusMessageType.Info);
                Application.Current?.Dispatcher?.Invoke(() => StatusMessages.Add(msg));
            }
        });
        private ICommand? _fetchHistoricalDataCommand;
        public ICommand FetchHistoricalDataCommand => _fetchHistoricalDataCommand ??= new RelayCommand(p =>
        {
            marketWatchModel?.ActiveSubscriptions.Clear();
            marketWatchModel?.GetOfflineMarketWatchDataAsync().ContinueWith(e =>
            {
                if (e.IsCompletedSuccessfully)
                {
                    var msg = new StatusMessage("Market Watch Data", "Successfully read offline market watch data.", StatusMessageType.Info);
                    Application.Current?.Dispatcher?.Invoke(() => StatusMessages.Add(msg));
                }
                else if (e.IsFaulted)
                {
                    var msg = new StatusMessage("Market Watch Data Exception", e.Exception?.GetBaseException().Message ?? "Unknown error", StatusMessageType.Error);
                    Application.Current?.Dispatcher?.Invoke(() => StatusMessages.Add(msg));
                }
            });

            if (_accessToken is not null)
            {
                _client.DefaultRequestHeaders.Add("Authorization", $"enctoken {AccessToken}");
                historicalDataModel = new HistoricalDataModel(_client, _accessToken);
                historicalDataModel.ConnectionString = ConnectionString;
                historicalDataModel.GetHistoricalDataFromApiAsync(marketWatchModel.ActiveSubscriptions.Values.ToArray(), HistoryCandleInterval.AllIntervals, years: 5).ContinueWith(e =>
                {
                    if (e.IsCompletedSuccessfully)
                    {
                        var msg = new StatusMessage("Historical Data Fetch", "Successfully fetched historical data.", StatusMessageType.Info);
                        Application.Current?.Dispatcher?.Invoke(() => StatusMessages.Add(msg));
                    }
                    else if (e.IsFaulted)
                    {
                        var msg = new StatusMessage("Historical Data Fetch Exception", e.Exception?.GetBaseException().Message ?? "Unknown error", StatusMessageType.Error);
                        Application.Current?.Dispatcher?.Invoke(() => StatusMessages.Add(msg));
                    }
                });
            }
        });
        //},(_) => _accessToken is not null);

        private ICommand? _readCurrentMarketWatchCommand;
        public ICommand ReadCurrentMarketWatchCommand => _readCurrentMarketWatchCommand ??= new RelayCommand(p =>
        {
            try
            {
                IsProcessWebResourceResponseReceivedEnabled = true;
            }
            catch
            {
                // swallow any errors to avoid crashing the UI thread
            }

            //// Reads offline market watch data from the local file and populates the ActiveSubscriptions dictionary
            //var marketData = marketWatchModel?.GetOfflineMarketWatchDataAsync();
            //if (marketData != null)
            //{
            //    var msg = new StatusMessage("Market Watch Data", "Successfully read offline market watch data.", StatusMessageType.Info);
            //    Application.Current?.Dispatcher?.Invoke(() => StatusMessages.Add(msg));
            //}
            //else
            //{
            //    var msg = new StatusMessage("Market Watch Data", "No offline market watch data found.", StatusMessageType.Warning);
            //    Application.Current?.Dispatcher?.Invoke(() => StatusMessages.Add(msg));
            //}
        });

        private ICommand? _webSocketFrameReceivedCommand;
        public ICommand WebSocketFrameReceivedCommand => _webSocketFrameReceivedCommand ??= new RelayCommand(p =>
        {
            ////// Expect the DevTools protocol event payload as JSON string
            ////if (p is string json)
            ////{
            ////    try
            ////    {
            ////        // Parse JSON and extract friendly fields
            ////        using var doc = JsonDocument.Parse(json);
            ////        var root = doc.RootElement;

            ////        // Default values
            ////        var direction = "Received";
            ////        int opcode = -1;
            ////        string opcodeName = "Unknown";
            ////        string payloadPreview = string.Empty;

            ////        if (root.TryGetProperty("response", out var response))
            ////        {
            ////            if (response.TryGetProperty("opcode", out var opEl) && opEl.ValueKind == JsonValueKind.Number)
            ////            {
            ////                opcode = opEl.GetInt32();
            ////                opcodeName = opcode switch
            ////                {
            ////                    0 => "Continuation",
            ////                    1 => "Text",
            ////                    2 => "Binary",
            ////                    8 => "Connection Close",
            ////                    9 => "Ping",
            ////                    10 => "Pong",
            ////                    _ => $"Opcode {opcode}"
            ////                };
            ////            }

            ////            // Only extract payload for text (1) or binary (2) frames
            ////            if ((opcode == 1 || opcode == 2) && response.TryGetProperty("payloadData", out var payloadEl) && payloadEl.ValueKind == JsonValueKind.String)
            ////            {
            ////                var payload = payloadEl.GetString() ?? string.Empty;
            ////                if (payload.Equals("AA=="))
            ////                    return;

            ////                if (opcode == 1)
            ////                {
            ////                    // Text frame
            ////                    payloadPreview = payload;
            ////                }
            ////                else if (opcode == 2)
            ////                {
            ////                    // Binary frame - payloadData is expected (base64). Use payloadData property explicitly.
            ////                    try
            ////                    {
            ////                        // payloadData is typically base64 for binary frames in CDP
            ////                        var bytes = Convert.FromBase64String(payload);
            ////                        var len = bytes.Length;
            ////                        var previewLen = Math.Min(64, len);
            ////                        var sb = new StringBuilder();
            ////                        for (int i = 0; i < previewLen; i++)
            ////                        {
            ////                            sb.Append(bytes[i].ToString("X2"));
            ////                            if (i < previewLen - 1) sb.Append(' ');
            ////                        }
            ////                        payloadPreview = $"<binary {len} bytes> {sb}";
            ////                    }
            ////                    catch
            ////                    {
            ////                        // not base64 or decode failed - show raw payloadData
            ////                        payloadPreview = $"<binary?> {payload}";
            ////                    }
            ////                }
            ////            }
            ////        }

            ////        var title = $"WebSocket {direction} - {opcodeName}";
            ////        var details = new StringBuilder();
            ////        details.AppendLine($"Direction: {direction}");
            ////        details.AppendLine($"Opcode: {opcodeName} ({opcode})");
            ////        if (!string.IsNullOrEmpty(payloadPreview))
            ////        {
            ////            details.AppendLine("Payload:");
            ////            details.AppendLine(payloadPreview.Length > 1000 ? payloadPreview.Substring(0, 1000) + "..." : payloadPreview);
            ////        }

            ////        //var msg = new StatusMessage(title, details.ToString(), StatusMessageType.Info);
            ////        //Application.Current?.Dispatcher?.Invoke(() => StatusMessages.Add(msg));
            ////    }
            ////    catch
            ////    {
            ////        // fallback: show raw json
            ////        try
            ////        {
            ////            //var msg = new StatusMessage("WebSocket Frame", json, StatusMessageType.Info);
            ////            //Application.Current?.Dispatcher?.Invoke(() => StatusMessages.Add(msg));
            ////        }
            ////        catch { }
            ////    }
            ////}
        });

        private ICommand? _showMessageCommand;
        public ICommand ShowMessageCommand => _showMessageCommand ??= new RelayCommand(p =>
        {
            if (p is StatusMessage msg)
            {
                // Show a simple popup window with details (View created here for simplicity)
                var win = new Views.StatusPopupWindow(msg.Title, msg.Details);
                try { win.ShowDialog(); } catch { try { win.Show(); } catch { } }
            }
        });

        private ICommand? _clearAllCommand;
        public ICommand ClearAllCommand => _clearAllCommand ??= new RelayCommand(_ =>
        {
            try
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    StatusMessages.Clear();
                    SelectedStatusMessages.Clear();
                });
            }
            catch { }
        });

        private ICommand? _clearSelectedCommand;
        public ICommand ClearSelectedCommand => _clearSelectedCommand ??= new RelayCommand(_ =>
        {
            try
            {
                // make a copy to avoid modifying collection while enumerating
                var toRemove = new StatusMessage[SelectedStatusMessages.Count];
                SelectedStatusMessages.CopyTo(toRemove, 0);
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    foreach (var m in toRemove)
                    {
                        StatusMessages.Remove(m);
                    }
                    SelectedStatusMessages.Clear();
                });
            }
            catch { }
        }, _ => SelectedStatusMessages.Count > 0);

        private void ExecuteNavigate()
        {
            // Normalize the user-entered Uri and set NavigationSource which the View binds to
            var normalized = NormalizeUri(Uri);
            if (!string.IsNullOrEmpty(normalized))
            {
                ErrorMessage = null;
                NavigationSource = normalized;
            }
            else
            {
                // Provide validation feedback
                ErrorMessage = string.IsNullOrWhiteSpace(Uri) ? "Please enter a URL." : "Invalid URL.";
            }
        }

        private string? NormalizeUri(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            var trimmed = input.Trim();

            // If user entered a scheme, trust it if it's a well-formed absolute URI
            if (System.Uri.TryCreate(trimmed, System.UriKind.Absolute, out var tmp) && !string.IsNullOrEmpty(tmp.Scheme))
                return tmp.ToString();

            // Otherwise assume https
            if (System.Uri.TryCreate("https://" + trimmed, System.UriKind.Absolute, out var httpsUri))
                return httpsUri.ToString();

            return null;
        }

        private async Task ExtractInstrumentIdsAsync(string json)
        {
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;
                var tradingItems = root.GetProperty("data").GetProperty("groups")[0].GetProperty("items");
                FileIO.SaveJsonToFileAsync(tradingItems.ToString(), System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data"), "marketwatch.json").Wait();
                //var newList = new List<int>();
                //int arraylength = tradingItems.GetArrayLength();
                //StringBuilder sb = new StringBuilder();
                ////instrumentData = new Dictionary<string, JsonDocument>();
                //Dictionary<string, int> activeSubscriptions = new Dictionary<string, int>();
                //for (int i = 0; i < arraylength; i++)
                //{
                //    string tradingsymbol = tradingItems[i].GetProperty("tradingsymbol").ToString();
                //    int instrumentToken = tradingItems[i].GetProperty("instrument_token").GetInt32();
                //    if (Equals(activeSubscriptions, null))
                //        activeSubscriptions = new Dictionary<string, int>();
                //    activeSubscriptions.TryAdd(tradingsymbol, (int)instrumentToken);
                //    //sb.AppendLine(tradingItems[i].GetProperty("instrument_token").ToString());
                //    //activeSubscriptions.Add(tradingsymbol, instrumentToken);
                //    //JsonDocument? document = OpenInstrumentJson(tradingsymbol);
                //    //if (document is not null) instrumentData.Add(tradingsymbol, document);
                //}
                //string instrumentList = string.Join(",", activeSubscriptions.Values.Select(x => x));
                //string instrumentList = string.Join(",", sb.ToString().Split("\r\n"));
                //await InjectModeSwitch(instrumentList);
            }
        }

        // Called from the view when WebView2 raises WebResourceResponseReceived
        public void OnWebResourceResponseReceived(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs args)
        {
            StatusMessage msg = new StatusMessage("WebResourceResponseReceived", $"Request: {args.Request.Uri}, Response: {args.Response.StatusCode} {args.Response.ReasonPhrase}", StatusMessageType.Info);
            try
            {
                if (IsProcessWebResourceResponseReceivedEnabled)
                {
                    msg = marketWatchModel?.ProcessWebResourceResponseReceived(args.Request, args.Response);
                    IsProcessWebResourceResponseReceivedEnabled = false; // Reset the flag after processing
                }
            }
            catch (Exception ex)
            {
                msg = new StatusMessage("WebResourceResponseReceived Exception", $"Request: {args.Request.Uri}, Response: {args.Response.StatusCode} {args.Response.ReasonPhrase}, Exception: {ex.Message}", StatusMessageType.Error);
                // swallow any errors to avoid crashing the UI thread
            }
            Application.Current?.Dispatcher?.Invoke(() => StatusMessages.Add(msg));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public WebViewViewModel(Models.AppSettings appSettings)
        {
            _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
            // Re-evaluate commands when selection changes
            SelectedStatusMessages.CollectionChanged += (s, e) => System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }


    }

    // Minimal RelayCommand implementation to avoid third-party dependencies
    internal class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
