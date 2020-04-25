﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Diagnostics;


using Binance.Net;

using Binance.Net.Objects;

using BinanceBot.Strategy;

using BinanceBot.Domain;



namespace BinanceBot.Application
{
    public class BinanceCommand
    {
        private BinanceWebCall webCall;

        public BinanceCommand()
        {
            webCall = new BinanceWebCall();
        }

        public void ConnectFuturesBot(string symbol, decimal quantity, string ApiKey, string ApiSecret, decimal risk, decimal reward, decimal leverage, int signalStrength, string timeframe, int candleCount, bool isLive, decimal decreaseOnNegative)
        {
            #region -strategy and function level variables-
            var openclosestrategy = new OpenCloseStrategy();

            var profitFactor = (decimal)1;

            var errorCount = 0;
            #endregion

            webCall.AddAuthenticationInformation(ApiKey, ApiSecret);

            using (var client = new BinanceClient())
            {
                webCall.AssignBinanceWebCallFeatures(client, symbol);

                while (true)
                {
                    try
                    {
                        #region -variables refreshed every cycle-
                        Stopwatch sw = new Stopwatch();

                        sw.Start();

                        var isBuy = default(bool);

                        var isSell = default(bool);

                        var mood = default(string);

                        var trend = default(string);

                        var shortPercentage = default(decimal);

                        var longPercentage = default(decimal);

                        List<OHLCKandle> ohlckandles = new List<OHLCKandle>();

                        var currentClose = default(decimal);

                        var currentPosition

                         = new SimplePosition
                         {
                             OrderID = -1,

                             OrderType = "",

                             EntryPrice = -1,

                             Quantity = quantity,

                             Trend = "",

                             Mood = ""
                         };

                        var histdata = default(string);

                        var strategyOutput = StrategyOutput.None;

                        Thread.Sleep(1500);
                        #endregion

                        if (isLive)
                        {
                            webCall.GetCurrentPosition(ref currentPosition, quantity, ref profitFactor);
                        }

                        webCall.GetKLinesDataCached(timeframe, candleCount, ref currentClose, ref ohlckandles);

                        openclosestrategy.RunStrategy(ohlckandles, ref isBuy, ref isSell, ref trend, ref mood, ref histdata, ref currentPosition, currentClose, risk, reward, leverage, ref shortPercentage, ref longPercentage, ref profitFactor, signalStrength, ref strategyOutput, decreaseOnNegative);

                        if (isLive && strategyOutput != StrategyOutput.None)
                        {
                            PlaceOrders(quantity, currentClose, strategyOutput);
                        }

                        sw.Stop();

                        DumpToConsole(isBuy, isSell, mood, trend, currentClose, currentPosition, shortPercentage,
                        longPercentage, reward, risk, profitFactor, leverage,
                        symbol, sw.ElapsedMilliseconds, signalStrength, histdata, openclosestrategy.BuyCounter, openclosestrategy.SellCounter);
                    }
                    catch (Exception)
                    {
                        ++errorCount;
                    }

                    if (errorCount >= 5)
                    {
                        break;
                    }
                }
            }
        }

        public void PlaceOrders(decimal quantity, decimal currrentClose, StrategyOutput strategyOutput)
        {
            BinancePlacedOrder placedOrder = null;

            if (strategyOutput == StrategyOutput.OpenPositionWithBuy || strategyOutput == StrategyOutput.ExitPositionWithBuy || strategyOutput == StrategyOutput.BookProfitWithBuy || strategyOutput == StrategyOutput.MissedPositionBuy)
            {
                placedOrder = webCall.PlaceBuyOrder(quantity, -1, true);
            }
            else if (strategyOutput == StrategyOutput.OpenPositionWithSell || strategyOutput == StrategyOutput.ExitPositionWithSell || strategyOutput == StrategyOutput.BookProfitWithSell || strategyOutput == StrategyOutput.MissedPositionSell)
            {
                placedOrder = webCall.PlaceSellOrder(quantity, -1, true);
            }
            else if (strategyOutput == StrategyOutput.EscapeTrapWithBuy)
            {
                placedOrder = webCall.PlaceBuyOrder(quantity * 2, -1, true);
            }
            else if (strategyOutput == StrategyOutput.EscapeTrapWithSell)
            {
                placedOrder = webCall.PlaceSellOrder(quantity * 2, -1, true);
            }
            else
            {
                //no action
            }

            if (placedOrder != null)
            {
                DumpToLog(currrentClose, currrentClose, strategyOutput.ToString(), strategyOutput.ToString(), -1, -1);
            }

        }

        #region -dump code-
        private void DumpToConsole(bool isBuy, bool isSell, string mood, string trend, decimal currentClose, SimplePosition order, decimal shortPercentage, decimal longPercentage, decimal reward, decimal risk, decimal profitFactor, decimal leverage, string symbol, long
            cycleTime, int signalStrength, string histdata, int BuyCounter, int SellCounter)
        {
            Console.Clear();

            Console.WriteLine();

            Console.WriteLine("\n--------------------------------------------------------------------------");

            Console.WriteLine("\nMARKET DETAILS: \n");

            //latest price
            Console.WriteLine("{0} : {1}\n", symbol, currentClose);

            //mood
            if (mood == "BULLISH")
            {//\u02C4
                Console.WriteLine("MOOD    : {0}\n", "UP");
            }
            else if (mood == "BEARISH")
            {
                Console.WriteLine("MOOD    : {0}\n", "DOWN");
            }
            else
            {
                Console.WriteLine("MOOD : {0}\n", "");
            }

            //trend
            if (trend == "BULLISH")
            {
                Console.WriteLine("TREND   : {0}\n", "UP");
            }
            else if (trend == "BEARISH")
            {
                Console.WriteLine("TREND   : {0}\n", "DOWN");
            }
            else
            {
                Console.WriteLine("TREND : {0}\n", "");
            }

            //signal
            if (isBuy)
            {
                Console.WriteLine("SIGNAL  : {0}\n", "BUY");
                Console.WriteLine("SIGNA%  : {0}%\n", 100 * BuyCounter / signalStrength);
            }
            else if (isSell)
            {
                Console.WriteLine("SIGNAL  : {0}\n", "SELL");
                Console.WriteLine("SIGNA%  : {0}%\n", 100 * SellCounter / signalStrength);
            }
            else
            {
                Console.WriteLine("SIGNAL  : {0}\n", "NO SIGNAL");
            }

            Console.WriteLine("HISTORY : {0}", histdata);

            Console.WriteLine("\n--------------------------------------------------------------------------");

            Console.WriteLine("\nORDER DETAILS: \n");

            Console.WriteLine("ID {0}\n", order?.OrderID);

            Console.WriteLine("TYPE {0} \n", order?.OrderType);

            Console.WriteLine("ENTRY PRICE {0} \n", order?.EntryPrice);

            if (order?.OrderID != -1 && order?.OrderType == "SELL")
            {
                Console.WriteLine("PERCENTAGE {0} \n", Math.Round(shortPercentage, 3));
            }
            if (order?.OrderID != -1 && order?.OrderType == "BUY")
            {
                Console.WriteLine("PERCENTAGE {0} \n", Math.Round(longPercentage, 3));
            }

            Console.WriteLine("ADJUSTED PROFIT LIMIT {0}% \n", reward * profitFactor);

            Console.WriteLine("CURRENT PROFIT LIMIT {0}% \n", reward);

            Console.WriteLine("CURRENT LOSS LIMIT {0}% \n", risk);

            Console.WriteLine("CURRENT LEVERAGE {0}x\n", leverage);

            Console.WriteLine("--------------------------------------------------------------------------\n");

            Console.WriteLine("Refresh Rate {0} milliseconds\n", cycleTime);
        }

        private void DumpToLog(decimal currentClose, decimal entryPrice, string OrderType, string decision, decimal percentage, decimal limitpercentage)
        {
            var debuginfo = string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}", DateTime.Now.ToUniversalTime().ToString(), currentClose, entryPrice
                        , OrderType, decision, percentage, limitpercentage);

            File.AppendAllLines("debug.logs", new[] { debuginfo });
        }
        #endregion

    }
}