using AlgoTrading.Helpers;
using AlgoTrading.ViewModels;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AlgoTrading.Models
{
    internal class MarketWatchModel
    {
        public event EventHandler MarketWatchDataUpdated;

        private Dictionary<string, int> _marketWatchData = new Dictionary<string, int>();
        public Dictionary<string, int> ActiveSubscriptions { get => _marketWatchData; }
        public string BaseDirectotry { get => System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "MarketWatch"); }

        public string[] MarketWatchFiles { get => System.IO.Directory.GetFiles(BaseDirectotry, "*.json"); }
        internal StatusMessage ProcessWebResourceResponseReceived(CoreWebView2WebResourceRequest request, CoreWebView2WebResourceResponseView response)
        {
            StatusMessage msg = new StatusMessage("Market Watch", "Processing web resource response received.", StatusMessageType.Info);
            if (request.Uri.Contains("items?uid=marketwatch"))
            {
                // Match one or more digits
                var match = Regex.Match(request.Uri, @"\d+");
                int number = 0;
                if (match.Success)
                {
                    number = int.Parse(match.Value); // Result: 10
                }
                try
                {
                    var streamTask = response.GetContentAsync();
                    // 2. Chain the continuation block
                    var readTask = streamTask.ContinueWith(io =>
                    {
                        if (io.IsCompletedSuccessfully) // Modern replacement for IsCompleted
                        {
                            // 3. Keep disposables inside the callback boundary
                            using (var stream = io.Result)
                                if (stream is not null)
                                    using (var reader = new System.IO.StreamReader(stream))
                                    {
                                        string jsonResponse = reader.ReadToEnd();// Return it out of the task scope

                                        using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                                        {

                                            var root = doc.RootElement;
                                            string name = doc.RootElement.GetProperty("data").GetProperty("name").GetString();
                                            var tradingItems = root.GetProperty("data").GetProperty("groups")[0].GetProperty("items");
                                            if (tradingItems.GetArrayLength() >= 0)
                                            FileIO.SaveJsonToFileAsync(tradingItems.ToString(), BaseDirectotry, $"marketwatch{number}.json").Wait();
                                            msg = new StatusMessage("Market Watch", "Successfully saved market watch data.", StatusMessageType.Info);
                                        }
                                    }
                        }
                        else if (io.IsFaulted)
                        {

                            msg = new StatusMessage("Market Watch Exception", io.Exception.Message, StatusMessageType.Error);
                            //Application.Current?.Dispatcher?.Invoke(() => StatusMessages.Add(msg));

                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.StackTrace);
                }
            }
            OnMarketWatchDataUpdated();
            return msg;
        }

        public async Task GetOfflineMarketWatchDataAsync(int watchListNumber = -1)
        {
            //_marketWatchData.Clear(); -- One time clear is not needed as we are adding new data to the existing dictionary. If you want to clear the data, you can uncomment this line.
            try
            {
                string[] filePath = watchListNumber == -1 ?
                Directory.GetFiles(BaseDirectotry, "*.json")
                : Directory.GetFiles(System.IO.Path.Combine(BaseDirectotry, $"marketwatch{watchListNumber}.json"));

                foreach (var file in filePath)
                {
                    if (System.IO.File.Exists(file))
                    {
                        string fileContent = await System.IO.File.ReadAllTextAsync(file);
                        if (!string.IsNullOrEmpty(fileContent))
                        {

                            using (JsonDocument doc = JsonDocument.Parse(fileContent))
                            {
                                var marketWatchItems = doc.RootElement.EnumerateArray().Select(x => new { Tradingsymbol = x.GetProperty("tradingsymbol").GetString(), InstrumentToken = x.GetProperty("instrument_token").GetInt32() });
                                foreach (var item in marketWatchItems)
                                {
                                    _marketWatchData.Add(item.Tradingsymbol, item.InstrumentToken);
                                }
                            }

                        }
                    }
                }
                if (_marketWatchData.Count > 0)
                {
                    OnMarketWatchDataUpdated();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.StackTrace);
            }
        }

        protected virtual void OnMarketWatchDataUpdated()
        {
            MarketWatchDataUpdated?.Invoke(this, EventArgs.Empty);
        }

    }
}
