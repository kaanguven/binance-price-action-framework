using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Project.model;
using Project.service;

namespace Project.service.impl
{
    public class MarketStructureServiceImpl : IMarketStructureService
    {
        private readonly CryptoService _cryptoService;

        public MarketStructureServiceImpl(CryptoService cryptoService)
        {
            _cryptoService = cryptoService;
        }

        public async Task<MarketStructureResponse> CalculateMarketStructureAsync(MarketStructureRequest request)
        {
            var ohlcRequest = new OHLCRequest
            {
                Symbol = request.Symbol,
                Timeframe = request.Timeframe,
                Limit = request.Limit,
                Since = request.Since
            };
            var ohlcResponse = await _cryptoService.FetchOHLCAsync(ohlcRequest);

            var response = new MarketStructureResponse
            {
                Symbol = request.Symbol,
                Timeframe = request.Timeframe,
                Exchange = ohlcResponse.Exchange ?? "binance" // Default if not provided by crypto service
            };

            if (ohlcResponse.Candles == null || ohlcResponse.Candles.Count < request.ZigZagLength * 2) // Need enough data
            {
                return response; // Not enough data
            }

            var candles = ohlcResponse.Candles;
            int candleCount = candles.Count;

            // PineScript state variables
            var highPoints = new List<SwingPoint>();
            var lowPoints = new List<SwingPoint>();
            
            int currentTrend = 1; // 1 for up, -1 for down
            int prevTrend = 1;

            var toUpHistory = new List<bool>(candleCount);
            var toDownHistory = new List<bool>(candleCount);
            for(int k=0; k<candleCount; k++) { toUpHistory.Add(false); toDownHistory.Add(false); }

            int marketStatus = 1; // 1 for bullish structure, -1 for bearish structure
            int prevMarketStatus = 1;
            SwingPoint lastH0WhenMarketChanged = null;
            SwingPoint lastL0WhenMarketChanged = null;
            
            // Store all identified swing points for visualization/debugging
            var allSwings = new List<SwingPoint>();

            // Loop through candles to calculate indicators and detect structures
            for (int i = 0; i < candleCount; i++)
            {
                // Ensure enough lookback for ZigZagLength
                if (i < request.ZigZagLength -1) continue;

                // Calculate to_up and to_down
                decimal currentHigh = candles[i].High;
                decimal currentLow = candles[i].Low;
                decimal highestInZigZag = GetHighestOrLowestInRange(candles, i, request.ZigZagLength, c => c.High, true);
                decimal lowestInZigZag = GetHighestOrLowestInRange(candles, i, request.ZigZagLength, c => c.Low, false);

                bool isToUp = currentHigh >= highestInZigZag;
                bool isToDown = currentLow <= lowestInZigZag;
                toUpHistory[i] = isToUp;
                toDownHistory[i] = isToDown;

                // Update trend
                prevTrend = currentTrend;
                if (currentTrend == 1 && isToDown) currentTrend = -1;
                else if (currentTrend == -1 && isToUp) currentTrend = 1;

                bool trendChanged = currentTrend != prevTrend;

                if (trendChanged)
                {
                    if (currentTrend == 1) // Trend changed to UP, a LOW was made
                    {
                        int lastToUpSignalBar = i - 1 - BarsSince(toUpHistory, i - 1, true);
                        var (lowVal, lowIndex) = FindLowestLow(candles, lastToUpSignalBar, i);
                        if(lowIndex != -1)
                        {
                            var newLowPoint = new SwingPoint { Price = lowVal, Index = lowIndex, Date = candles[lowIndex].DateTime, Type = "Low" };
                            lowPoints.Add(newLowPoint);
                            allSwings.Add(newLowPoint);
                            if (lowPoints.Count > 5) lowPoints.RemoveAt(0);
                        }
                    }
                    else // Trend changed to DOWN, a HIGH was made
                    {
                        int lastToDownSignalBar = i - 1 - BarsSince(toDownHistory, i - 1, true);
                        var (highVal, highIndex) = FindHighestHigh(candles, lastToDownSignalBar, i);
                        if(highIndex != -1)
                        {
                            var newHighPoint = new SwingPoint { Price = highVal, Index = highIndex, Date = candles[highIndex].DateTime, Type = "High" };
                            highPoints.Add(newHighPoint);
                            allSwings.Add(newHighPoint);
                            if (highPoints.Count > 5) highPoints.RemoveAt(0);
                        }
                    }
                }

                // Get H0, H1, L0, L1
                SwingPoint h0 = GetSwing(highPoints, 0);
                SwingPoint h1 = GetSwing(highPoints, 1);
                SwingPoint l0 = GetSwing(lowPoints, 0);
                SwingPoint l1 = GetSwing(lowPoints, 1);

                if (h0 == null || l0 == null || h1 == null || l1 == null) continue; // Not enough swings yet

                // Update market status (MSB)
                prevMarketStatus = marketStatus;
                bool marketCanChange = true;
                if (lastH0WhenMarketChanged != null && lastL0WhenMarketChanged != null) {
                    if (lastH0WhenMarketChanged.Index == h0.Index && lastH0WhenMarketChanged.Price == h0.Price && 
                        lastL0WhenMarketChanged.Index == l0.Index && lastL0WhenMarketChanged.Price == l0.Price) {
                        marketCanChange = false; // Avoid re-triggering on the same H0/L0
                    }
                }

                if (marketCanChange) {
                    if (marketStatus == 1 && l0.Price < l1.Price && l0.Price < (l1.Price - Math.Abs(h0.Price - l1.Price) * (decimal)request.FibFactor))
                    {
                        marketStatus = -1; // Bearish MSB
                    }
                    else if (marketStatus == -1 && h0.Price > h1.Price && h0.Price > (h1.Price + Math.Abs(h1.Price - l0.Price) * (decimal)request.FibFactor))
                    {
                        marketStatus = 1; // Bullish MSB
                    }
                }

                bool marketStatusChanged = marketStatus != prevMarketStatus;

                if (marketStatusChanged)
                {
                    lastH0WhenMarketChanged = h0;
                    lastL0WhenMarketChanged = l0;

                    var msb = new MarketStructureBreak
                    {
                        Index = i,
                        Date = candles[i].DateTime,
                        Type = marketStatus == 1 ? "Bullish" : "Bearish",
                        H0_at_break = h0, L0_at_break = l0,
                        H1_at_break = h1, L1_at_break = l1
                    };

                    if (marketStatus == 1) // Bullish MSB (broke H1)
                    {
                        msb.BrokenSwingPoint = h1;
                        msb.PrecedingSwingPoint = l0; // L0 was before breaking H1
                        
                        // Bullish Order Block (Bu-OB): last bearish candle in range [h1i, l0i]
                        int buObCandleIndex = FindBlockCandle(candles, h1.Index, l0.Index, true, false);
                        if (buObCandleIndex != -1)
                        {
                            response.OrderBlocks.Add(new IdentifiedOrderBlock 
                            {
                                OrderBlockType = "Bu-OB",
                                High = candles[buObCandleIndex].High, Low = candles[buObCandleIndex].Low,
                                CandleIndex = buObCandleIndex, CandleDate = candles[buObCandleIndex].DateTime,
                                MsbIndex = i, MsbDate = candles[i].DateTime, MsbDirection = "Bullish"
                            });
                        }

                        // Bullish Breaker/Mitigation Block (Bu-BB / Bu-MB): last bullish candle in range [l1i - len, h1i]
                        int buBbCandleIndex = FindBlockCandle(candles, Math.Max(0, l1.Index - request.ZigZagLength), h1.Index, false, true);
                        if (buBbCandleIndex != -1)
                        {
                            response.BreakerBlocks.Add(new IdentifiedBreakerBlock
                            {
                                BreakerBlockType = l0.Price < l1.Price ? "Bu-BB" : "Bu-MB",
                                High = candles[buBbCandleIndex].High, Low = candles[buBbCandleIndex].Low,
                                CandleIndex = buBbCandleIndex, CandleDate = candles[buBbCandleIndex].DateTime,
                                MsbIndex = i, MsbDate = candles[i].DateTime, MsbDirection = "Bullish"
                            });
                        }
                    }
                    else // Bearish MSB (broke L1)
                    {
                        msb.BrokenSwingPoint = l1;
                        msb.PrecedingSwingPoint = h0; // H0 was before breaking L1

                        // Bearish Order Block (Be-OB): last bullish candle in range [l1i, h0i]
                        int beObCandleIndex = FindBlockCandle(candles, l1.Index, h0.Index, false, true);
                         if (beObCandleIndex != -1)
                        {
                            response.OrderBlocks.Add(new IdentifiedOrderBlock 
                            {
                                OrderBlockType = "Be-OB",
                                High = candles[beObCandleIndex].High, Low = candles[beObCandleIndex].Low,
                                CandleIndex = beObCandleIndex, CandleDate = candles[beObCandleIndex].DateTime,
                                MsbIndex = i, MsbDate = candles[i].DateTime, MsbDirection = "Bearish"
                            });
                        }

                        // Bearish Breaker/Mitigation Block (Be-BB / Be-MB): last bearish candle in range [h1i - len, l1i]
                        int beBbCandleIndex = FindBlockCandle(candles, Math.Max(0, h1.Index - request.ZigZagLength), l1.Index, true, false);
                        if (beBbCandleIndex != -1)
                        {
                            response.BreakerBlocks.Add(new IdentifiedBreakerBlock
                            {
                                BreakerBlockType = h0.Price > h1.Price ? "Be-BB" : "Be-MB",
                                High = candles[beBbCandleIndex].High, Low = candles[beBbCandleIndex].Low,
                                CandleIndex = beBbCandleIndex, CandleDate = candles[beBbCandleIndex].DateTime,
                                MsbIndex = i, MsbDate = candles[i].DateTime, MsbDirection = "Bearish"
                            });
                        }
                    }
                    response.MarketStructureBreaks.Add(msb);
                }
                
                // Mitigate existing blocks (simplified: check against current close)
                // PineScript deletes boxes. Here we just mark them as mitigated.
                // This check should ideally happen for blocks formed on previous bars against current bar i.
                // For simplicity in a stateless call, we can check blocks formed *within this same processing run*.
                foreach(var ob in response.OrderBlocks.Where(x => !x.IsMitigated))
                {
                    if (ob.OrderBlockType == "Bu-OB" && candles[i].Close < ob.Low) ob.IsMitigated = true;
                    if (ob.OrderBlockType == "Be-OB" && candles[i].Close > ob.High) ob.IsMitigated = true;
                }
                foreach(var bb in response.BreakerBlocks.Where(x => !x.IsMitigated))
                {
                    if ((bb.BreakerBlockType == "Bu-BB" || bb.BreakerBlockType == "Bu-MB") && candles[i].Close < bb.Low) bb.IsMitigated = true;
                    if ((bb.BreakerBlockType == "Be-BB" || bb.BreakerBlockType == "Be-MB") && candles[i].Close > bb.High) bb.IsMitigated = true;
                }
            }
            response.AllSwingPoints = allSwings.OrderBy(s => s.Index).ToList();
            return response;
        }

        private decimal GetHighestOrLowestInRange(List<Candle> candles, int currentIndex, int length, Func<Candle, decimal> selector, bool findHighest)
        {
            decimal val = findHighest ? decimal.MinValue : decimal.MaxValue;
            for (int k = Math.Max(0, currentIndex - length + 1); k <= currentIndex; k++)
            {
                if (findHighest) { if (selector(candles[k]) > val) val = selector(candles[k]); }
                else { if (selector(candles[k]) < val) val = selector(candles[k]); }
            }
            return val;
        }

        private int BarsSince(List<bool> history, int currentIndex, bool valueToFind)
        {
            if (currentIndex < 0) return history.Count +1; // effectively infinity if checking before start
            for (int k = currentIndex; k >= 0; k--)
            {
                if (history[k] == valueToFind) return currentIndex - k;
            }
            return currentIndex + 1; // Or a large number indicating not found recently
        }

        private (decimal price, int index) FindLowestLow(List<Candle> candles, int startIndex, int endIndex)
        {
            if (startIndex < 0) startIndex = 0;
            if (endIndex >= candles.Count) endIndex = candles.Count -1;
            if (startIndex > endIndex) return (0, -1);

            decimal lowest = decimal.MaxValue;
            int lowestIdx = -1;
            for (int k = startIndex; k <= endIndex; k++)
            {
                if (candles[k].Low < lowest)
                {
                    lowest = candles[k].Low;
                    lowestIdx = k;
                }
            }
            return (lowest, lowestIdx);
        }

        private (decimal price, int index) FindHighestHigh(List<Candle> candles, int startIndex, int endIndex)
        {
             if (startIndex < 0) startIndex = 0;
            if (endIndex >= candles.Count) endIndex = candles.Count -1;
            if (startIndex > endIndex) return (0, -1);

            decimal highest = decimal.MinValue;
            int highestIdx = -1;
            for (int k = startIndex; k <= endIndex; k++)
            {
                if (candles[k].High > highest)
                {
                    highest = candles[k].High;
                    highestIdx = k;
                }
            }
            return (highest, highestIdx);
        }

        private SwingPoint GetSwing(List<SwingPoint> swings, int indexFromEnd)
        {
            if (swings.Count > indexFromEnd) return swings[swings.Count - 1 - indexFromEnd];
            return null;
        }

        // Finds the last candle matching criteria in the range [startIndex, endIndex]
        private int FindBlockCandle(List<Candle> candles, int startIndex, int endIndex, bool findBearish, bool findBullish)
        {
            int blockCandleIndex = -1;
            if (startIndex < 0 || endIndex >= candles.Count || startIndex > endIndex) return -1;

            for (int k = startIndex; k <= endIndex; k++)
            {
                bool isBearish = candles[k].Open > candles[k].Close;
                bool isBullish = candles[k].Open < candles[k].Close;
                if (findBearish && isBearish) blockCandleIndex = k;
                if (findBullish && isBullish) blockCandleIndex = k;
            }
            return blockCandleIndex;
        }
    }
} 