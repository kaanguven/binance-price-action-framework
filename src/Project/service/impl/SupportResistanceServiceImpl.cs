using Project.model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Project.service.impl
{
    public class SupportResistanceServiceImpl : SupportResistanceService
    {
        private readonly CryptoService _cryptoService;

        public SupportResistanceServiceImpl(CryptoService cryptoService)
        {
            _cryptoService = cryptoService;
        }

        public async Task<SupportResistanceResponse> CalculateSupportResistance(
            string symbol, 
            string interval, 
            float multiplicativeFactor = 8.0f, 
            int atrLength = 50, 
            int extendLast = 4,
            int limit = 500)
        {
            // Make sure we have enough data for ATR calculation
            int requiredCandles = Math.Max(limit, atrLength * 2);
            
            // Fetch price data using the existing CryptoService
            var request = new OHLCRequest 
            { 
                Symbol = symbol,
                Timeframe = interval,
                Limit = requiredCandles
            };
            
            var ohlcResponse = await _cryptoService.FetchOHLCAsync(request);
            var candles = ohlcResponse.Candles;

            if (candles.Count < atrLength + 1)
            {
                throw new Exception($"Not enough data to calculate ATR. Minimum required: {atrLength + 1}, but got {candles.Count}");
            }

            // Calculate the ATR
            decimal[] atr = CalculateATR(candles, atrLength);

            // Initialize variables
            List<SrLevel> srLevels = new List<SrLevel>();
            decimal avg = candles[0].Close; // Start with the first close price
            decimal holdAtr = 0;
            int os = 0; // Oscillator: 1 = uptrend (support), 0 = downtrend (resistance)
            
            // Variables to track previous avg value
            decimal prevAvg = avg;

            // Main calculation loop
            for (int i = 0; i < candles.Count; i++)
            {
                decimal close = candles[i].Close;
                long timestamp = candles[i].Timestamp;
                
                // Skip if we don't have an ATR value yet
                if (i < atrLength)
                    continue;

                decimal breakoutAtr = atr[i] * (decimal)multiplicativeFactor;
                bool isBreakoutPoint = false;
                
                // This mimics Pine Script's behavior:
                // avg := math.abs(close - avg) > breakout_atr ? close : avg
                prevAvg = avg;
                if (Math.Abs((double)(close - avg)) > (double)breakoutAtr)
                {
                    avg = close;
                    holdAtr = breakoutAtr;
                    isBreakoutPoint = true;
                }

                // This mimics: os := avg > avg[1] ? 1 : avg < avg[1] ? 0 : os
                if (avg > prevAvg)
                    os = 1;
                else if (avg < prevAvg)
                    os = 0;
                // else os remains unchanged

                // Calculate support and resistance levels
                decimal? upperRes = os == 0 ? avg + holdAtr / (decimal)multiplicativeFactor : (decimal?)null;
                decimal? lowerRes = os == 0 ? avg + holdAtr / (decimal)multiplicativeFactor / 2 : (decimal?)null;
                decimal? upperSup = os == 1 ? avg - holdAtr / (decimal)multiplicativeFactor / 2 : (decimal?)null;
                decimal? lowerSup = os == 1 ? avg - holdAtr / (decimal)multiplicativeFactor : (decimal?)null;

                // Record support/resistance levels at breakout points (when avg changes)
                if (isBreakoutPoint)
                {
                    if (os == 1)
                    {
                        srLevels.Insert(0, new SrLevel
                        {
                            Y = (float)(decimal)lowerSup.Value,
                            Area = (float)(decimal)upperSup.Value,
                            X = timestamp,
                            IsSupport = true
                        });
                    }
                    else
                    {
                        srLevels.Insert(0, new SrLevel
                        {
                            Y = (float)(decimal)upperRes.Value,
                            Area = (float)(decimal)lowerRes.Value,
                            X = timestamp,
                            IsSupport = false
                        });
                    }
                }
            }

            // Return only the most recent levels based on extendLast
            var response = new SupportResistanceResponse
            {
                Symbol = symbol,
                Timeframe = interval,
                Levels = srLevels.Take(Math.Min(extendLast, srLevels.Count)).ToList()
            };

            return response;
        }

        private decimal[] CalculateATR(List<Candle> candles, int period)
        {
            int len = candles.Count;
            decimal[] atr = new decimal[len];
            decimal[] tr = new decimal[len];

            // Calculate True Range
            for (int i = 0; i < len; i++)
            {
                decimal high = candles[i].High;
                decimal low = candles[i].Low;
                decimal prevClose = i > 0 ? candles[i - 1].Close : candles[i].Open;

                decimal tr1 = high - low;
                decimal tr2 = Math.Abs(high - prevClose);
                decimal tr3 = Math.Abs(low - prevClose);

                tr[i] = Math.Max(Math.Max(tr1, tr2), tr3);
            }

            // Calculate ATR using RMA (smoothed moving average like in TradingView)
            decimal sum = 0;
            
            // First ATR is a simple average of TR
            for (int i = 0; i < period; i++)
            {
                sum += tr[i];
            }
            
            atr[period - 1] = sum / period;
            
            // Subsequent ATRs use the RMA formula (Pine uses this by default in ta.atr)
            decimal alpha = 1.0m / period;
            for (int i = period; i < len; i++)
            {
                atr[i] = tr[i] * alpha + atr[i - 1] * (1 - alpha);
            }

            return atr;
        }
    }
} 