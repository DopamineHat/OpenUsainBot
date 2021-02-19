using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Objects.Spot;
using Binance.Net.Objects.Spot.MarketData;
using Binance.Net.SubClients.Spot;
using Binance.Net.Objects.Spot.SpotData;
using Binance.Net.SocketSubClients;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Logging;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace OpenCryptShot
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            Console.Title = "UsainBot Alpha";
            Config config = LoadOrCreateConfig();
            if (config == null)
            {
                Console.Read();
                return;
            }
         /*   using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri("https://usainbot.com/api/");
                Task<HttpResponseMessage> response = client.GetAsync("?key=" + config.apiKey);
                HttpResponseMessage result = response.Result;
                response.Wait();
                if (result.IsSuccessStatusCode)
                {
                    Task<string> task = result.Content.ReadAsStringAsync();
                    string[] res = task.Result.Split(':');
                    config.name = res[0];
                    config.expiry = res[1];
                }
                else if (result.StatusCode == HttpStatusCode.Unauthorized)
                {
                    Console.WriteLine("No license for this API key.");
                    Thread.Sleep(3000);
                    return;
                }
                else if (result.StatusCode == HttpStatusCode.Forbidden)
                {
                    Console.WriteLine("Your license has expired.");
                    Thread.Sleep(3000);
                    return;
                }
                else
                {
                    Console.WriteLine("Unknown error while querying API.");
                    Thread.Sleep(3000);
                    return;
                }
            }
            Console.WriteLine("Hello " + config.name + ", your license will expire on " + config.expiry); */

            try
            {
                BinanceClient.SetDefaultOptions(new BinanceClientOptions
                {
                    ApiCredentials = new ApiCredentials(config.apiKey, config.apiSecret),
                    LogVerbosity = LogVerbosity.None,
                    LogWriters = new List<TextWriter> {Console.Out}
                });
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR! Could not set Binance options. Error message: {ex.Message}");
                Console.Read();
                return;
            }

            Utilities.Write(ConsoleColor.Green, "Successfully logged in.");

            while (true)
            {
                string symbol;
                if (config.channel_id.Length > 0 && config.discord_token.Length > 0)
                {
                    symbol = null;
                    Utilities.Write(ConsoleColor.Yellow, "Looking for ticker...");
                    while (null == symbol)
                    {
                        symbol = FindTicker(config.discord_token, config.channel_id);
                        Thread.Sleep(1000);
                    }
                    Utilities.Write(ConsoleColor.Green, "Found ticker " + symbol);
                    Console.ForegroundColor = ConsoleColor.White;
                    symbol = symbol.Remove(0, 1);
                }
                else
                {
                    //Wait for symbol input
                    Utilities.Write(ConsoleColor.Yellow, "Input symbol: ");
                    Console.ForegroundColor = ConsoleColor.White;
                    symbol = Console.ReadLine();
                }

                //Exit the program if nothing was entered
                if (string.IsNullOrEmpty(symbol))
                    return;
                //Try to execute the order
                ExecuteOrder(symbol, config.quantity, config.strategyrisk, config.sellStrategy, config.maxsecondsbeforesell);
            }
        }

        private static Config LoadOrCreateConfig()
        {
            if (!File.Exists("config.json"))
            {
                string json = JsonConvert.SerializeObject(new Config
                {
                    apiKey = "",
                    apiSecret = "",
                    quantity = (decimal)0.00025,
                    strategyrisk = (decimal)30.0,
                    sellStrategy = (decimal).8,
                    maxsecondsbeforesell = (decimal)20.0,
                    discord_token = "",
                    channel_id = ""
    });
                File.WriteAllText("config.json", json);
                Utilities.Write(ConsoleColor.Red, "config.json was missing and has been created. Please edit the file and restart the application.");
                return null;
            }

            return JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));
        }

        private static string FindTicker(string discord_token, string channel_id)
        {
            Regex regex = new Regex(@"(\$)[a-zA-Z]{1,5}");
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://discord.com/api/v8/channels/" + channel_id +"/messages?limit=1");
            req.Headers.Add("Authorization", discord_token);
            req.Accept = "*/*";
            req.ContentType = "application/json";
            req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) discord/0.0.309 Chrome/83.0.4103.122 Electron/9.3.5 Safari/537.36";
            WebResponse res = req.GetResponse();
            Stream dataStream = res.GetResponseStream();
            StreamReader reader = new StreamReader(dataStream);
            string resJson = reader.ReadToEnd();
            JsonSerializerOptions options = new JsonSerializerOptions
            { IncludeFields = true };
            Message[] msg = JsonSerializer.Deserialize<Message[]>(resJson, options);
            Match match = regex.Match(msg[0].content);
            reader.Close();
            dataStream.Close();
            if (match.Success)
            {
                return match.Value;
            }
            else
            {
                return null;
            }
        }

        private static void ExecuteOrder(string symbol, decimal quantity, decimal strategyrisk, decimal sellStrategy, decimal maxsecondsbeforesell)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            using (var client = new BinanceClient())
            {
                string pair = symbol.ToUpper() + "BTC";
                           WebCallResult<BinancePlacedOrder> order = client.Spot.Order.PlaceOrder(pair, OrderSide.Buy, OrderType.Market, null, quantity);
                           if (!order.Success)
                           {
                               Utilities.Write(ConsoleColor.Red, $"ERROR! Could not place the Market order. Error code: " + order.Error?.Message);
                               return;
                           }
                            stopWatch.Stop();
                            var timestamp = DateTime.Now.ToFileTime();
                            TimeSpan ts = stopWatch.Elapsed;

                            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                                ts.Hours, ts.Minutes, ts.Seconds,
                                ts.Milliseconds / 10);
                            Console.WriteLine("RunTime " + elapsedTime);
                            WebCallResult<BinanceExchangeInfo> exchangeInfo = client.Spot.System.GetExchangeInfo();
                            if (!exchangeInfo.Success)
                            {
                                Utilities.Write(ConsoleColor.Red, $"ERROR! Could not exchange informations. Error code: " + exchangeInfo.Error?.Message);
                                return;
                            }

                            BinanceSymbol symbolInfo = exchangeInfo.Data.Symbols.FirstOrDefault(s => s.QuoteAsset == "BTC" && s.BaseAsset == symbol.ToUpper());
                            if (symbolInfo == null)
                            {
                                Utilities.Write(ConsoleColor.Red, $"ERROR! Could not get symbol informations.");
                                return;
                            }
                            int symbolPrecision = 1;
                            decimal ticksize = symbolInfo.PriceFilter.TickSize;
                            while ((ticksize = ticksize * 10) < 1)
                                ++symbolPrecision;


                decimal paidPrice = 0;
                           if (order.Data.Fills != null)
                            {
                                paidPrice = order.Data.Fills.Average(trade => trade.Price);
                            }
                    decimal OrderQuantity = order.Data.QuantityFilled;

                    Utilities.Write(ConsoleColor.Green, $"Order submitted, Got: {OrderQuantity} coins from {pair} at {paidPrice}");
                    decimal sellPriceRiskRatio = (decimal).95;
                    decimal StartSellStrategy = sellStrategy;
                    decimal MaxSellStrategy = 1 - ((1 - sellStrategy) / 5);
                    decimal volasellmax = (decimal)1.0;
                    decimal currentstoploss = 0;
                    List<decimal> tab = new List<decimal>();
                    int count = -1; 
                    int x = 0;
                    int usainsell = 0;
                    int imincharge = 0;
                    Thread t = new Thread(NewThread);
                    t.Start();
                    WebCallResult<BinanceBookPrice> priceResult3 = client.Spot.Market.GetBookPrice(pair);
                    Thread t2 = new Thread(NewThread2);
                    t2.Start();
                    WebCallResult<BinanceBookPrice> priceResult2 = client.Spot.Market.GetBookPrice(pair);
                             void NewThread()
                             {
                                 while ((timestamp + maxsecondsbeforesell * 10000000) > DateTime.Now.ToFileTime() && x != 2)
                                 {
                                     count++;
                                     priceResult2 = client.Spot.Market.GetBookPrice(pair);
                                     if (priceResult2.Success)
                                        tab.Add(priceResult2.Data.BestBidPrice);
                                     else
                                        return;
                                    if (count % 10 == 0)
                                    {
                                        int countca = count;
                                        decimal espa = 0;
                                        int x2a = -1;
                                        while (--countca > 0 && ++x2a < 10)
                                        {
                                            espa += (tab[countca] - tab[countca - 1]) / 10;
                                        }
                                        decimal esp2a = espa / 3;
                                        while (--countca > 0 && ++x2a < 30)
                                        {
                                            esp2a += (tab[countca] - tab[countca - 1]) / 30;
                                        }
                                        Utilities.Write(ConsoleColor.Green, $" {Math.Round(espa / priceResult2.Data.BestBidPrice * 100000, 2)}");
                                        Utilities.Write(ConsoleColor.Red, $" {Math.Round(esp2a / priceResult2.Data.BestBidPrice * 100000, 2)}");
                                        decimal volasell = (esp2a - espa) / priceResult2.Data.BestBidPrice * 100000;
                                        if (volasell > strategyrisk / 4)
                                        {
                                            Utilities.Write(ConsoleColor.Red, $" negative volatility detected at a {Math.Round(volasell, 2)} ratio");
                                    if (volasell < strategyrisk)
                                    {
                                        if (volasell > volasellmax)
                                        {
                                            volasellmax = volasell;
                                            sellStrategy = Math.Round(StartSellStrategy + (decimal)Math.Pow((double)volasell / (double)strategyrisk, 1.5) * (MaxSellStrategy - StartSellStrategy), 3);
                                        }
                                    }
                                    else if (usainsell == 0 && imincharge == 0)
                                    {
                                        imincharge = 1;
                                        WebCallResult<IEnumerable<BinanceCancelledId>> orderspanic = client.Spot.Order.CancelAllOpenOrders(symbol: pair);
                                        WebCallResult<BinancePlacedOrder> ordersell = client.Spot.Order.PlaceOrder(pair, OrderSide.Sell, OrderType.Limit, OrderQuantity, price: Math.Round(priceResult2.Data.BestBidPrice * sellPriceRiskRatio, symbolPrecision), timeInForce: TimeInForce.GoodTillCancel);
                                        while (!ordersell.Success)
                                        {
                                            Utilities.Write(ConsoleColor.Red, $"ERROR! Could not place the Market order sell, trying another time. Error code: " + ordersell.Error?.Message);
                                            ordersell = client.Spot.Order.PlaceOrder(pair, OrderSide.Sell, OrderType.Limit, OrderQuantity, price: Math.Round(priceResult2.Data.BestBidPrice * sellPriceRiskRatio, symbolPrecision), timeInForce: TimeInForce.GoodTillCancel);
                                        }
                                        usainsell = 1;
                                        Utilities.Write(ConsoleColor.Green, "UsainBot PANIC SOLD successfully  " + OrderQuantity + " " + ordersell.Data.Symbol + $" sold at " + priceResult2.Data.BestBidPrice);
                                        return;
                                            }
                                            else
                                                return;
                                        }
                                    }
                                    Console.Title = $"Price for {pair} is {priceResult2.Data.BestBidPrice} to {priceResult2.Data.BestAskPrice} in iteration  " + count + "  negative volatility ratio is " + Math.Round(volasellmax, 2) + " stop limit is placed at " + currentstoploss;
                                 }
                             }
                    void NewThread2()
                    {
                        while ((timestamp + maxsecondsbeforesell * 10000000) > DateTime.Now.ToFileTime() && x != 2)
                        {
                            count++;
                            try
                            {
                                priceResult3 = client.Spot.Market.GetBookPrice(pair);
                                tab.Add(priceResult3.Data.BestBidPrice);
                            }
                            catch (Exception e)
                            {
                                tab.Add(tab[count - 1]);
                            }
                            Console.Title = $"Price for {pair} is {priceResult3.Data.BestBidPrice} to {priceResult3.Data.BestAskPrice} in iteration  " + count + "  negative volatility ratio is " + Math.Round(volasellmax , 2) + " stop limit is placed at " + currentstoploss;
                            if (count % 10 == 0)
                            {
                                int countc = count;
                                decimal esp = 0;
                                int x2 = -1;
                                while (--countc > 0 && ++x2 < 10)
                                {
                                    esp += (tab[countc] - tab[countc - 1]) / 10;
                                }
                                decimal esp2 = esp / 3;
                                while (--countc > 0 && ++x2 < 30)
                                {
                                    esp2 += (tab[countc] - tab[countc - 1]) / 30;
                                }
                                Utilities.Write(ConsoleColor.Green, $" {Math.Round(esp / priceResult3.Data.BestBidPrice * 100000, 2)}");
                                Utilities.Write(ConsoleColor.Red, $" {Math.Round(esp2 / priceResult3.Data.BestBidPrice * 100000, 2)}");
                                decimal volasell = (esp2 - esp) / priceResult3.Data.BestBidPrice * 100000;
                                if (volasell > strategyrisk / 4)
                                {
                                    Utilities.Write(ConsoleColor.Red, $" negative volatility detected at a {Math.Round(volasell, 2)} ratio");
                                    if (volasell < strategyrisk)
                                    {
                                        if (volasell > volasellmax)
                                        {
                                            volasellmax = volasell;
                                            sellStrategy = Math.Round(StartSellStrategy + (decimal)Math.Pow((double)volasell / (double)strategyrisk, 1.5) * (MaxSellStrategy - StartSellStrategy), 3);
                                        }
                                    }
                                    else if (usainsell == 0 && imincharge == 0)
                                    {
                                        imincharge = 1;
                                        WebCallResult<IEnumerable<BinanceCancelledId>> orderspanic2 = client.Spot.Order.CancelAllOpenOrders(symbol: pair);
                                        WebCallResult<BinancePlacedOrder> ordersell2 = client.Spot.Order.PlaceOrder(pair, OrderSide.Sell, OrderType.Limit, OrderQuantity, price: Math.Round(priceResult3.Data.BestBidPrice * sellPriceRiskRatio, symbolPrecision), timeInForce: TimeInForce.GoodTillCancel);
                                        while (!ordersell2.Success)
                                        {
                                            Utilities.Write(ConsoleColor.Red, $"ERROR! Could not place the Market order sell, trying another time. Error code: " + ordersell2.Error?.Message);
                                            ordersell2 = client.Spot.Order.PlaceOrder(pair, OrderSide.Sell, OrderType.Limit, OrderQuantity, price: Math.Round(priceResult3.Data.BestBidPrice * sellPriceRiskRatio, symbolPrecision), timeInForce: TimeInForce.GoodTillCancel);
                                        }
                                        usainsell = 1;
                                        Utilities.Write(ConsoleColor.Green, "UsainBot PANIC SOLD successfully  " + OrderQuantity + " " + ordersell2.Data.Symbol + $" sold at " + priceResult3.Data.BestBidPrice);
                                        return;
                                    }
                                    else
                                        return;
                                }
                            }

                        }
                    }
                    while (sellStrategy <= MaxSellStrategy && sellStrategy >= StartSellStrategy && usainsell == 0)
                    {
                        if ((timestamp + (decimal)1.0 * 10000000 * maxsecondsbeforesell) > DateTime.Now.ToFileTime())
                        {
                            priceResult2 = client.Spot.Market.GetBookPrice(pair);
                            decimal stopPrice = Math.Round(priceResult2.Data.BestBidPrice * sellStrategy, symbolPrecision);
                            if (stopPrice > currentstoploss)
                            {
                                currentstoploss = stopPrice;
                                decimal sellPrice = Math.Round(stopPrice * sellPriceRiskRatio, symbolPrecision);
                                WebCallResult<IEnumerable<BinanceCancelledId>> orders = client.Spot.Order.CancelAllOpenOrders(symbol: pair);
                                if (!orders.Success)
                                {
                                    if (x == 0)
                                    {
                                        x = 1;
                                        Utilities.Write(ConsoleColor.Red, $"ERROR! Could not remove orders. Error code: " + orders.Error?.Message);
                                    }
                                    else
                                    {
                                        x = 2;
                                        Utilities.Write(ConsoleColor.Red, "StopLoss executed : " + orders.Error?.Message);
                                        return;
                                    }
                                }
                                else
                                    Utilities.Write(ConsoleColor.Blue, $"Orders successfully removed.");
                                //     WebCallResult<BinanceCanceledOrder> cancel = client.Spot.Order.CancelOrder(pair);
                                if (usainsell == 0)
                                {
                                    WebCallResult<BinancePlacedOrder> panicsell = client.Spot.Order.PlaceOrder(pair, OrderSide.Sell, OrderType.StopLossLimit, OrderQuantity, price: sellPrice, timeInForce: TimeInForce.GoodTillCancel, stopPrice: stopPrice);
                                    if (!panicsell.Success)
                                    {
                                        Utilities.Write(ConsoleColor.Red, $"ERROR! Could not place the StopLimit order. Error code: " + panicsell.Error?.Message);
                                        return;
                                    }
                                    else
                                        Utilities.Write(ConsoleColor.Blue, $"StopLimit Order submitted, stop limit price: {stopPrice}, sell price: {sellPrice}");
                                }
                            }
                        }
                        else
                        {
                            imincharge = 1;
                            WebCallResult<IEnumerable<BinanceCancelledId>> orderspanic2 = client.Spot.Order.CancelAllOpenOrders(symbol: pair);
                            WebCallResult<BinancePlacedOrder> ordersell2 = client.Spot.Order.PlaceOrder(pair, OrderSide.Sell, OrderType.Limit, OrderQuantity, price: Math.Round(priceResult3.Data.BestBidPrice * sellPriceRiskRatio, symbolPrecision), timeInForce: TimeInForce.GoodTillCancel);
                            while (!ordersell2.Success)
                            {
                                Utilities.Write(ConsoleColor.Red, $"ERROR! Could not place the Market order sell, trying another time. Error code: " + ordersell2.Error?.Message);
                                ordersell2 = client.Spot.Order.PlaceOrder(pair, OrderSide.Sell, OrderType.Limit, OrderQuantity, price: Math.Round(priceResult3.Data.BestBidPrice * sellPriceRiskRatio, symbolPrecision), timeInForce: TimeInForce.GoodTillCancel);
                            }
                            usainsell = 1;
                            Utilities.Write(ConsoleColor.Green, "UsainBot TIME SOLD successfully  " + OrderQuantity + " " + ordersell2.Data.Symbol + $" sold at " + priceResult3.Data.BestBidPrice);
                            return;
                        }
                    }
            }
        }
    }
}