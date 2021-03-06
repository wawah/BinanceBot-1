﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

using Binance.Net;
using Binance.Net.Objects;
using CryptoExchange.Net.Authentication;

using BinanceBot.Domain;

using BinanceBot.Common;

using BinanceBot.Settings;
//WebcallTimeout
namespace BinanceBot.Application
{
    public class BinanceWebCall
    {
        //shared variables
        public BinanceClient client;
        private string symbol;
        private TimeSpan? TimeZoneDiff;
        private List<OHLCKandle> kandleCache;
        private string timeframeCache;
        private long WebcallTimeout;


        /// <summary>
        /// constructor
        /// </summary>
        public BinanceWebCall()
        {
            WebcallTimeout = BinanceBotSettings.settings.WebcallTimeout;
        }

        /// <summary>
        /// Assign variables common to this class to all functions
        /// </summary>
        /// <param name="client"></param>
        /// <param name="symbol"></param>
        public void AssignBinanceWebCallFeatures(string symbol)
        {
            this.symbol = symbol;
        }
        /// <summary>
        /// Authentication Information to access Binance API
        /// </summary>
        /// <param name="ApiKey"></param>
        /// <param name="ApiSecret"></param>
        public void AddAuthenticationInformation(string ApiKey, string ApiSecret)
        {
            BinanceClient.SetDefaultOptions(new BinanceClientOptions()
            {
                ApiCredentials = new ApiCredentials(ApiKey, ApiSecret),
            });

            BinanceSocketClient.SetDefaultOptions(new BinanceSocketClientOptions()
            {
                ApiCredentials = new ApiCredentials(ApiKey, ApiSecret),
            });
        }
        /// <summary>
        /// Place Sell Order of Given Quantity
        /// </summary>
        /// <param name="quantity"></param>
        /// <param name="price"></param>
        /// <param name="futures"></param>
        /// <returns></returns>
        public BinancePlacedOrder PlaceSellOrder(decimal quantity, decimal price, bool futures)
        {
            var sellOrder = client.PlaceOrder(symbol, OrderSide.Sell, OrderType.Market, quantity: quantity, futures: true);

            if (sellOrder.Success)
            {
                return sellOrder.Data;
            }
            else
            {
                return null;
            }

        }
        /// <summary>
        /// Place Buy Order with given Quantity
        /// </summary>
        /// <param name="quantity"></param>
        /// <param name="price"></param>
        /// <param name="futures"></param>
        /// <returns></returns>
        public BinancePlacedOrder PlaceBuyOrder(decimal quantity, decimal price, bool futures)
        {
            var buyOrder = client.PlaceOrder(symbol, OrderSide.Buy, OrderType.Market, quantity: quantity, futures: true);

            if (buyOrder.Success)
            {
                return buyOrder.Data;
            }
            else
            {
                return null;
            }
        }
        /// <summary>
        /// Get all Positions for the given symbol 
        /// </summary>
        /// <param name="robotInput"></param>
        /// <param name="strategyData"></param>
        /// <param name="position"></param>
        /// <param name="isLive"></param>
        public void GetOpenPosition(RobotInput robotInput, ref StrategyData strategyData, ref SimplePosition position, bool isLive)
        {
            if (!isLive)
            {
                return;
            }
        getpositions:

            position = new SimplePosition
            {
                PositionType = PositionType.None,

                EntryPrice = -1,

                Quantity = robotInput.quantity
            };

            var positions = client.GetPositions(default, WebcallTimeout, true);

            var currentPosition = positions.Data.Where(x => x.symbol == symbol && x.positionSide == "BOTH").Single();

            if (currentPosition.entryPrice != 0)
            {
                position.EntryPrice = currentPosition.entryPrice;

                position.Quantity = robotInput.quantity;

                if (currentPosition.entryPrice > currentPosition.markPrice && currentPosition.unRealizedProfit > 0)
                {
                    position.PositionType = PositionType.Sell;
                }
                else if (currentPosition.entryPrice < currentPosition.markPrice && currentPosition.unRealizedProfit < 0)
                {
                    position.PositionType = PositionType.Sell;
                }
                else if (currentPosition.entryPrice < currentPosition.markPrice && currentPosition.unRealizedProfit > 0)
                {
                    position.PositionType = PositionType.Buy;
                }
                else if (currentPosition.entryPrice > currentPosition.markPrice && currentPosition.unRealizedProfit < 0)
                {
                    position.PositionType = PositionType.Buy;
                }
                else
                {
                    Thread.Sleep(1000);
                    goto getpositions;
                }
            }
            else
            {
                strategyData.profitFactor = 1;

                position.PositionType = PositionType.None;

                position.EntryPrice = -1;

                position.Quantity = robotInput.quantity;
            }

            if (position.PositionType != PositionType.None)
            {
                if (position.PositionType == PositionType.Sell)
                {
                    strategyData.Percentage = robotInput.leverage * ((position.EntryPrice - strategyData.currentClose) / position.EntryPrice) * 100;
                }

                if (position.PositionType == PositionType.Buy)
                {
                    strategyData.Percentage = robotInput.leverage * ((strategyData.currentClose - position.EntryPrice) / position.EntryPrice) * 100;
                }

                if (strategyData.Percentage < 0)
                {
                    strategyData.profitFactor = robotInput.decreaseOnNegative;
                }
            }
        }
        /// <summary>
        /// Get Candle Data From the Servers
        /// </summary>
        /// <param name="timeframe"></param>
        /// <param name="candleCount"></param>
        /// <param name="currentClose"></param>
        /// <param name="ohlckandles"></param>
        public void GetKLinesData(string timeframe, int candleCount, ref List<OHLCKandle> ohlckandles)
        {
            #region -candle interval for the request-

            var timeframe_ = KlineInterval.ThirtyMinutes;

            if (timeframe == "1m")
            {
                timeframe_ = KlineInterval.OneMinute;
            }
            else if (timeframe == "5m")
            {
                timeframe_ = KlineInterval.FiveMinutes;
            }
            else if (timeframe == "15m")
            {
                timeframe_ = KlineInterval.FifteenMinutes;
            }
            else if (timeframe == "30m")
            {
                timeframe_ = KlineInterval.ThirtyMinutes;
            }
            else if (timeframe == "1h")
            {
                timeframe_ = KlineInterval.OneHour;
            }
            else if (timeframe == "2h")
            {
                timeframe_ = KlineInterval.TwoHour;
            }
            else if (timeframe == "4h")
            {
                timeframe_ = KlineInterval.FourHour;
            }
            else if (timeframe == "6h")
            {
                timeframe_ = KlineInterval.SixHour;
            }
            else if (timeframe == "1d")
            {
                timeframe_ = KlineInterval.OneDay;
            }
            else
            {
                timeframe_ = KlineInterval.ThirtyMinutes;
            }

            #endregion

            var startTime = GetStartTime(candleCount, timeframe);

            var klines = client.GetKlines(symbol, timeframe_, startTime: startTime, ct: default, futures: true);

            ohlckandles = ConvertToOHLCKandles(klines.Data.ToList());
        }
        /// <summary>
        /// Convert Binance Klines to OHLC Kandles
        /// </summary>
        /// <param name="klines"></param>
        /// <returns></returns>
        private List<OHLCKandle> ConvertToOHLCKandles(List<BinanceKline> klines)
        {
            List<OHLCKandle> candles = new List<OHLCKandle>();

            foreach (var item in klines)
            {
                candles.Add(
                    new OHLCKandle
                    {
                        Open = item.Open,
                        Close = item.Close,
                        High = item.High,
                        Low = item.Low,
                        OpenTime = item.OpenTime,
                        CloseTime = item.CloseTime
                    });
            }


            return candles;
        }
        /// <summary>
        /// Get Candle Data From the Servers with local caching (faster performance)
        /// </summary>
        /// <param name="timeframe"></param>
        /// <param name="candleCount"></param>
        /// <param name="currentClose"></param>
        /// <param name="ohlckandles"></param>
        public void GetKLinesDataCached(string timeframe, int candleCount, ref StrategyData strategyData)
        {
            List<OHLCKandle> ohlckandles = new List<OHLCKandle>();

            //insert data in cache
            if (string.IsNullOrEmpty(timeframeCache) || timeframe != timeframeCache || kandleCache == null || kandleCache.Count == 0)
            {
                //get all the kandles from server
                GetKLinesData(timeframe, candleCount, ref ohlckandles);

                //synchronize cache
                kandleCache = ohlckandles.Select(x => new OHLCKandle
                {
                    Open = x.Open,

                    Close = x.Close,

                    High = x.High,

                    Low = x.Low,

                    OpenTime = x.OpenTime,

                    CloseTime = x.CloseTime

                }).ToList();

                //synchronize timeframe
                timeframeCache = timeframe.ToString();
            }
            //update existing cache
            else
            {
                #region -candle interval for the request-

                KlineInterval timeframe_;

                if (timeframe == "1m")
                {
                    timeframe_ = KlineInterval.OneMinute;
                }
                else if (timeframe == "5m")
                {
                    timeframe_ = KlineInterval.FiveMinutes;
                }
                else if (timeframe == "15m")
                {
                    timeframe_ = KlineInterval.FifteenMinutes;
                }
                else if (timeframe == "30m")
                {
                    timeframe_ = KlineInterval.ThirtyMinutes;
                }
                else if (timeframe == "1h")
                {
                    timeframe_ = KlineInterval.OneHour;
                }
                else if (timeframe == "2h")
                {
                    timeframe_ = KlineInterval.TwoHour;
                }
                else if (timeframe == "4h")
                {
                    timeframe_ = KlineInterval.FourHour;
                }
                else if (timeframe == "6h")
                {
                    timeframe_ = KlineInterval.SixHour;
                }
                else if (timeframe == "1d")
                {
                    timeframe_ = KlineInterval.OneDay;
                }
                else
                {
                    timeframe_ = KlineInterval.ThirtyMinutes;
                }

                #endregion

                //get 2 latest kandles from server
                var klines = client.GetKlines(symbol, timeframe_, limit: 2, futures: true);

                //convert 2 latest kandles to ohlc
                ohlckandles = ConvertToOHLCKandles(klines.Data.ToList());

                //cache count
                int countToMaintain = kandleCache.Count;

                //synchronize cache with latest values
                foreach (var kandle in ohlckandles)
                {
                    var k = kandleCache.Where(x => x.OpenTime == kandle.OpenTime).SingleOrDefault();

                    if (k == null)
                    {
                        #region -Add to Cache-
                        kandleCache.Add(
                            new OHLCKandle
                            {
                                Open = kandle.Open,

                                Close = kandle.Close,

                                High = kandle.High,

                                Low = kandle.Low,

                                OpenTime = kandle.OpenTime,

                                CloseTime = kandle.CloseTime
                            });
                        #endregion
                    }
                    else
                    {
                        #region -Update Cache-
                        k.Close = kandle.Close;
                        k.Open = kandle.Open;
                        k.High = kandle.High;
                        k.Low = kandle.Low;
                        #endregion
                    }
                }

                while (kandleCache.Count != countToMaintain)
                {
                    kandleCache.RemoveAt(0);
                }

                //output values
                ohlckandles = kandleCache.Select(x => new OHLCKandle
                {
                    Open = x.Open,

                    Close = x.Close,

                    High = x.High,

                    Low = x.Low,

                    OpenTime = x.OpenTime,

                    CloseTime = x.CloseTime

                }).ToList();
            }


            //save values in strategydata variable
            strategyData.currentOpen = ohlckandles[ohlckandles.Count - 1].Open;

            strategyData.currentClose = ohlckandles[ohlckandles.Count - 1].Close;

            strategyData.PrevOpen = ohlckandles[ohlckandles.Count - 2].Open;

            strategyData.PrevClose = ohlckandles[ohlckandles.Count - 2].Close;

            strategyData.kandles =

            ohlckandles.Select(x => new OHLCKandle
            {
                Close = x.Close,
                CloseTime = x.CloseTime,
                High = x.High,
                Low = x.Low,
                Open = x.Open,
                OpenTime = x.OpenTime

            }).ToList();
        }

        #region -timezone and server time functions-
        /// <summary>
        /// Get live server time of Binance Server
        /// </summary>
        /// <returns></returns>
        private DateTime GetServerTime()
        {
            DateTime dtServer = client.GetServerTime().Data;

            return dtServer;
        }
        /// <summary>
        /// Get cached/calculated server time of Binance Server
        /// </summary>
        /// <returns></returns>
        private DateTime GetServerTimeCached()
        {
            if (TimeZoneDiff == null)
            {
                TimeZoneDiff = GetTimeZoneDifference();
            }

            return DateTime.Now.Add((TimeSpan)TimeZoneDiff);
        }
        /// <summary>
        /// Get timezone difference between local machine and Binance Server
        /// </summary>
        /// <returns></returns>
        private TimeSpan GetTimeZoneDifference()
        {
            var serverTime = GetServerTime();

            var dtLocal = DateTime.Now;

            var tdiff = Math.Round((serverTime - dtLocal).TotalMinutes, 0, MidpointRounding.AwayFromZero);

            return TimeSpan.FromMinutes(tdiff);
        }
        /// <summary>
        /// Calculate max start-time of candle to request from Binance Server
        /// </summary>
        /// <param name="candleCount"></param>
        /// <param name="timeframe"></param>
        /// <returns></returns>
        public DateTime GetStartTime(int candleCount, string timeframe)
        {
            DateTime serverDateTime = GetServerTimeCached();

            int minutesRequested = 0;

            #region -set multiplier-
            if (timeframe == "1m")
            {
                minutesRequested = 1;
            }
            else if (timeframe == "5m")
            {
                minutesRequested = 5;
            }
            else if (timeframe == "15m")
            {
                minutesRequested = 15;
            }
            else if (timeframe == "30m")
            {
                minutesRequested = 30;
            }
            else if (timeframe == "1h")
            {
                minutesRequested = 60;
            }
            else if (timeframe == "2h")
            {
                minutesRequested = 120;
            }
            else if (timeframe == "4h")
            {
                minutesRequested = 240;
            }
            else if (timeframe == "6h")
            {
                minutesRequested = 360;
            }
            else if (timeframe == "1d")
            {
                minutesRequested = 24 * 60;
            }
            else
            {
                minutesRequested = 30;
            }
            #endregion

            int totalRequestedminutes = minutesRequested * candleCount;

            var dtTemp = serverDateTime.AddMinutes(-1 * totalRequestedminutes);

            if (dtTemp.Minute > 30)
            {
                dtTemp = dtTemp.AddMinutes((dtTemp.Minute - 30) * -1);
            }
            if (dtTemp.Minute < 30)
            {
                dtTemp = dtTemp.AddMinutes((dtTemp.Minute + 30) * -1);
            }

            return new DateTime(dtTemp.Year, dtTemp.Month, dtTemp.Day, dtTemp.Hour, dtTemp.Minute, 0);
        }
        #endregion

        //method to place orders
        public void PlaceOrders(RobotInput robotInput, StrategyData strategyData, bool isLive)
        {
            BinancePlacedOrder placedOrder = null;

            if (!isLive || strategyData.Decision == StrategyDecision.None)
            {
                return;
            }

            if (strategyData.Decision == StrategyDecision.Open || strategyData.Decision == StrategyDecision.OpenMissed || strategyData.Decision == StrategyDecision.TakeProfit ||
                strategyData.Decision == StrategyDecision.Exit || strategyData.Decision == StrategyDecision.ExitHeavy || strategyData.Decision == StrategyDecision.Escape)
            {
                if (strategyData.DecisionType == StrategyDecision.Buy)
                {
                    placedOrder = PlaceBuyOrder(robotInput.quantity, -1, true);
                }
                if (strategyData.DecisionType == StrategyDecision.Sell)
                {
                    placedOrder = PlaceSellOrder(robotInput.quantity, -1, true);
                }
            }

            #region -old commented code retained for historical sakes-
            /*old code */
            /*
            if (strategyData.Decision == StrategyDecision.OpenPositionWithBuy || strategyData.Decision == StrategyDecision.ExitPositionWithBuy ||
                strategyData.Decision == StrategyDecision.BookProfitWithBuy || strategyData.Decision == StrategyDecision.MissedPositionBuy ||
                strategyData.Decision == StrategyDecision.ExitPositionHeavyLossWithBuy)
            {
                placedOrder = PlaceBuyOrder(robotInput.quantity, -1, true);
            }

            else if (strategyData.Decision == StrategyDecision.OpenPositionWithSell || strategyData.Decision == StrategyDecision.ExitPositionWithSell ||
                     strategyData.Decision == StrategyDecision.BookProfitWithSell || strategyData.Decision == StrategyDecision.MissedPositionSell ||
                     strategyData.Decision == StrategyDecision.ExitPositionHeavyLossWithSell)
            {
                placedOrder = PlaceSellOrder(robotInput.quantity, -1, true);
            }

            else if (strategyData.Decision == StrategyDecision.EscapeTrapWithBuy)
            {
                if (BinanceBotSettings.settings.ReOpenOnEscape)
                {
                    placedOrder = PlaceBuyOrder(robotInput.quantity * 2, -1, true);
                }
                else
                {
                    placedOrder = PlaceBuyOrder(robotInput.quantity, -1, true);
                }
            }

            else if (strategyData.Decision == StrategyDecision.EscapeTrapWithSell)
            {
                if (BinanceBotSettings.settings.ReOpenOnEscape)
                {
                    placedOrder = PlaceSellOrder(robotInput.quantity * 2, -1, true);
                }
                else
                {
                    placedOrder = PlaceSellOrder(robotInput.quantity, -1, true);
                }
            }
            else
            {
                //no action
            }
            */
            #endregion

            if (placedOrder != null || strategyData.Decision.ToString().ToLower().Contains("skip"))
            {
                Utility.DumpToLog(robotInput, strategyData);
            }
        }
    }
}
