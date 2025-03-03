using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Linq;
using Project.model;
using Project.service;
using Binance.Net.Clients;
using Binance.Net.Enums;

namespace Project.service.impl
{
    public class CryptoServiceImpl : CryptoService
    {
        private readonly HttpClient _httpClient;
        private const int LEN = 7; 
        private const bool BREAKER_CANDLE_ONLY_BODY = false;
        private const bool BREAKER_CANDLE_2_LAST = false;
        private const bool TILL_FIRST_BREAK = true;
        
        private const int LIQUIDITY_LEN = 7;
        private const decimal LIQUIDITY_MARGIN = 10m / 6.9m;
        private const decimal BUYSIDE_MARGIN = 2.3m; 
        private const decimal SELLSIDE_MARGIN = 2.3m; 
        private const int VISIBLE_LEVELS = 3; 

        private readonly HashSet<decimal> _detectedBuysideLevels = new HashSet<decimal>();
        private readonly HashSet<decimal> _detectedSellsideLevels = new HashSet<decimal>();
        private readonly HashSet<decimal> _breachedBuysideLevels = new HashSet<decimal>();
        private readonly HashSet<decimal> _breachedSellsideLevels = new HashSet<decimal>();

        public CryptoServiceImpl(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<OHLCResponse> FetchOHLCAsync(OHLCRequest request)
        {
            var binanceClient = new BinanceRestClient();
            var interval = ToKlineInterval(request.Timeframe);
            var symbol = request.Symbol.ToUpperInvariant();
            int limit = request.Limit;
            var startTime = request.Since;

            var response = new OHLCResponse
            {
                Symbol = request.Symbol,
                Timeframe = request.Timeframe,
                Exchange = "binance",
                Candles = new List<Candle>()
            };

            try
            {
                var result = await binanceClient.SpotApi.ExchangeData.GetKlinesAsync(
                    symbol + "USDT",
                    interval,
                    startTime: startTime,
                    endTime: null,
                    limit: limit
                );

                if (result?.Data != null && result.Data.Any())
                {
                    foreach (var kline in result.Data)
                    {
                        response.Candles.Add(new Candle
                        {
                            Timestamp = new DateTimeOffset(kline.OpenTime).ToUnixTimeMilliseconds(),
                            DateTime = kline.OpenTime,
                            Open = kline.OpenPrice,
                            High = kline.HighPrice,
                            Low = kline.LowPrice,
                            Close = kline.ClosePrice,
                            Volume = kline.Volume
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Binance API hatası: {ex.Message}");
            }

            return response;
        }

        public async Task<BreakerBlocksResponse> FetchBreakerBlocksAsync(OHLCRequest request)
        {
            var ohlcResponse = await FetchOHLCAsync(request);
            
            var response = new BreakerBlocksResponse
            {
                Symbol = request.Symbol,
                Timeframe = request.Timeframe,
                Exchange = "binance",
                BreakerBlocks = new List<BreakerBlock>()
            };
            
            if (ohlcResponse.Candles == null || !ohlcResponse.Candles.Any())
                return response;
                
            CalculateBreakerBlocksImproved(ohlcResponse.Candles, response.BreakerBlocks);
            
            return response;
        }

        public async Task<LiquidityResponse> FetchLiquidityZonesAsync(OHLCRequest request)
        {
            var ohlcResponse = await FetchOHLCAsync(request);
            
            var response = new LiquidityResponse
            {
                Symbol = request.Symbol,
                Timeframe = request.Timeframe,
                Exchange = "binance",
                LiquidityZones = new List<LiquidityZone>()
            };
            
            if (ohlcResponse.Candles == null || !ohlcResponse.Candles.Any())
                return response;
                
            CalculateLiquidityZones(ohlcResponse.Candles, response.LiquidityZones);
            
            return response;
        }
        
        private void CalculateLiquidityZones(List<Candle> candles, List<LiquidityZone> liquidityZones)
        {
            if (candles.Count < LIQUIDITY_LEN + 1)
                return;

            var zigZag = new ZigZag(500);
            var atr = CalculateATR(candles, 10);
            
            var buysideLiquidity = new List<LiquidityZone>();
            var sellsideLiquidity = new List<LiquidityZone>();

            for (int n = LIQUIDITY_LEN; n < candles.Count; n++)
            {
                bool isPivotHigh = IsPivotPoint(candles, n, LIQUIDITY_LEN, true);
                bool isPivotLow = IsPivotPoint(candles, n, LIQUIDITY_LEN, false);
                
                if (isPivotHigh)
                {
                    int dir = zigZag.GetDirection(0);
                    int x1 = zigZag.GetX(0);
                    decimal y1 = zigZag.GetY(0);
                    int x2 = n;
                    decimal y2 = candles[n].High;
                    
                    if (dir < 1) 
                    {
                        zigZag.AddPoint(1, x1, y1, x2, y2, true);
                    }
                    else if (dir == 1 && y2 > y1) 
                    {
                        zigZag.UpdatePoint(0, x2, y2);
                    }
                    
                    var pivotHigh = candles[n].High;
                    var liquidityATR = atr[n] / LIQUIDITY_MARGIN;
                    
                    int count = 0;
                    int startBarIndex = 0;
                    decimal minPrice = 0m;  
                    decimal maxPrice = 10000000m;  
                    
                    for (int i = 0; i < Math.Min(zigZag.Count, 50); i++)
                    {
                        if (zigZag.GetDirection(i) == 1)
                        {
                            var pivotPrice = zigZag.GetY(i);
                            if (pivotPrice > pivotHigh + liquidityATR)
                                break;
                                
                            if (pivotPrice > pivotHigh - liquidityATR && pivotPrice < pivotHigh + liquidityATR)
                            {
                                count++;
                                startBarIndex = zigZag.GetX(i);
                                
                                if (pivotPrice > minPrice)
                                    minPrice = pivotPrice;
                                if (pivotPrice < maxPrice)
                                    maxPrice = pivotPrice;
                            }
                        }
                    }
                    
                    if (count > 2)
                    {
                        decimal avgPrice = (minPrice + maxPrice) / 2;
                        bool updateExisting = false;
                        
                        decimal roundedLevel = Math.Round(zigZag.GetY(0), 4);
                        
                        if (!_detectedBuysideLevels.Contains(roundedLevel))
                        {
                            if (buysideLiquidity.Count > 0)
                            {
                                var existing = buysideLiquidity[0];
                                
                                if (existing.PivotIndex == startBarIndex)
                                {
                                    existing.TopPrice = avgPrice + liquidityATR;
                                    existing.BottomPrice = avgPrice - liquidityATR;
                                    existing.AveragePrice = avgPrice;
                                    updateExisting = true;
                                }
                            }
                            
                            if (!updateExisting)
                            {
                                buysideLiquidity.Insert(0, new LiquidityZone
                                {
                                    Type = "Buyside",
                                    Date = candles[n].DateTime,
                                    Index = n,
                                    PivotIndex = startBarIndex,
                                    PivotPrice = zigZag.GetY(0),
                                    TopPrice = avgPrice + liquidityATR,
                                    BottomPrice = avgPrice - liquidityATR,
                                    AveragePrice = avgPrice,
                                    IsBreached = false,
                                    IsActive = true
                                });
                                
                                _detectedBuysideLevels.Add(roundedLevel);
                                
                                if (buysideLiquidity.Count > VISIBLE_LEVELS)
                                {
                                    var oldest = buysideLiquidity[buysideLiquidity.Count - 1];
                                    _detectedBuysideLevels.Remove(Math.Round(oldest.PivotPrice, 4));
                                    buysideLiquidity.RemoveAt(buysideLiquidity.Count - 1);
                                }
                            }
                        }
                    }
                }
                
                if (isPivotLow)
                {
                    int dir = zigZag.GetDirection(0);
                    int x1 = zigZag.GetX(0);
                    decimal y1 = zigZag.GetY(0);
                    int x2 = n;
                    decimal y2 = candles[n].Low;
                    
                    if (dir > -1) 
                    {
                        zigZag.AddPoint(-1, x1, y1, x2, y2, true);
                    }
                    else if (dir == -1 && y2 < y1) 
                    {
                        zigZag.UpdatePoint(0, x2, y2);
                    }
                    
                    var pivotLow = candles[n].Low;
                    var liquidityATR = atr[n] / LIQUIDITY_MARGIN;
                    
                    int count = 0;
                    int startBarIndex = 0;
                    decimal minPrice = 0m;  
                    decimal maxPrice = 10000000m;  
                    
                    for (int i = 0; i < Math.Min(zigZag.Count, 50); i++)
                    {
                        if (zigZag.GetDirection(i) == -1)
                        {
                            var pivotPrice = zigZag.GetY(i);
                            if (pivotPrice < pivotLow - liquidityATR)
                                break;
                                
                            if (pivotPrice > pivotLow - liquidityATR && pivotPrice < pivotLow + liquidityATR)
                            {
                                count++;
                                startBarIndex = zigZag.GetX(i);
                                
                                if (pivotPrice > minPrice)
                                    minPrice = pivotPrice;
                                if (pivotPrice < maxPrice)
                                    maxPrice = pivotPrice;
                            }
                        }
                    }
                    
                    if (count > 2)
                    {
                        decimal avgPrice = (minPrice + maxPrice) / 2;
                        bool updateExisting = false;
                        
                        decimal roundedLevel = Math.Round(zigZag.GetY(0), 4);
                        
                        if (!_detectedSellsideLevels.Contains(roundedLevel))
                        {
                            if (sellsideLiquidity.Count > 0)
                            {
                                var existing = sellsideLiquidity[0];
                                
                                if (existing.PivotIndex == startBarIndex)
                                {
                                    existing.TopPrice = avgPrice + liquidityATR;
                                    existing.BottomPrice = avgPrice - liquidityATR;
                                    existing.AveragePrice = avgPrice;
                                    updateExisting = true;
                                }
                            }
                            
                            if (!updateExisting)
                            {
                                sellsideLiquidity.Insert(0, new LiquidityZone
                                {
                                    Type = "Sellside",
                                    Date = candles[n].DateTime,
                                    Index = n,
                                    PivotIndex = startBarIndex,
                                    PivotPrice = zigZag.GetY(0),
                                    TopPrice = avgPrice + liquidityATR,
                                    BottomPrice = avgPrice - liquidityATR,
                                    AveragePrice = avgPrice,
                                    IsBreached = false,
                                    IsActive = true
                                });
                                
                                _detectedSellsideLevels.Add(roundedLevel);
                                
                                if (sellsideLiquidity.Count > VISIBLE_LEVELS)
                                {
                                    var oldest = sellsideLiquidity[sellsideLiquidity.Count - 1];
                                    _detectedSellsideLevels.Remove(Math.Round(oldest.PivotPrice, 4));
                                    sellsideLiquidity.RemoveAt(sellsideLiquidity.Count - 1);
                                }
                            }
                        }
                    }
                }
                
                CheckLiquidityBreaches(candles[n], buysideLiquidity, atr[n], true);
                CheckLiquidityBreaches(candles[n], sellsideLiquidity, atr[n], false);
            }
            
            liquidityZones.AddRange(buysideLiquidity);
            liquidityZones.AddRange(sellsideLiquidity);
        }
        
        private void CheckLiquidityBreaches(Candle candle, List<LiquidityZone> liquidityZones, decimal atr, bool isBuyside)
        {
            foreach (var zone in liquidityZones)
            {
                if (!zone.IsBreached)
                {
                    if (isBuyside && candle.High > zone.TopPrice)
                    {
                        decimal roundedLevel = Math.Round(zone.PivotPrice, 4);
                        if (!_breachedBuysideLevels.Contains(roundedLevel))
                        {
                            zone.IsBreached = true;
                            zone.IsActive = true;
                            _breachedBuysideLevels.Add(roundedLevel);
                        }
                    }
                    else if (!isBuyside && candle.Low < zone.BottomPrice)
                    {
                        decimal roundedLevel = Math.Round(zone.PivotPrice, 4);
                        if (!_breachedSellsideLevels.Contains(roundedLevel))
                        {
                            zone.IsBreached = true;
                            zone.IsActive = true;
                            _breachedSellsideLevels.Add(roundedLevel);
                        }
                    }
                }
                else if (zone.IsActive)
                {
                    decimal margin = isBuyside ? BUYSIDE_MARGIN : SELLSIDE_MARGIN;
                    
                    if (candle.Low > zone.AveragePrice - margin * atr && 
                        candle.High < zone.AveragePrice + margin * atr)
                    {
                        if (isBuyside)
                            zone.TopPrice = Math.Max(candle.High, zone.TopPrice);
                        else
                            zone.BottomPrice = Math.Min(candle.Low, zone.BottomPrice);
                    }
                    else
                    {
                        zone.IsActive = false;
                    }
                }
            }
        }
        
        private bool IsPivotPoint(List<Candle> candles, int index, int length, bool isHigh)
        {
            if (index < length || index >= candles.Count - 1)
                return false;
            
            decimal valueToCheck = isHigh ? candles[index].High : candles[index].Low;
            
            for (int i = index - length; i < index; i++)
            {
                if (i < 0) continue;
                
                decimal compareValue = isHigh ? candles[i].High : candles[i].Low;
                if ((isHigh && compareValue >= valueToCheck) || (!isHigh && compareValue <= valueToCheck))
                    return false;
            }
            
            int rightPos = index + 1;
            if (rightPos < candles.Count)
            {
                decimal compareValue = isHigh ? candles[rightPos].High : candles[rightPos].Low;
                if ((isHigh && compareValue >= valueToCheck) || (!isHigh && compareValue <= valueToCheck))
                    return false;
            }
            
            return true;
        }
        
        private List<decimal> CalculateATR(List<Candle> candles, int period)
        {
            var atrValues = new List<decimal>();
            
            decimal sum = 0;
            for (int i = 1; i <= period && i < candles.Count; i++)
            {
                decimal tr = Math.Max(
                    candles[i].High - candles[i].Low,
                    Math.Max(
                        Math.Abs(candles[i].High - candles[i-1].Close),
                        Math.Abs(candles[i].Low - candles[i-1].Close)
                    )
                );
                sum += tr;
            }
            
            if (period > 0 && candles.Count > period)
                atrValues.Add(sum / period);
            else
                atrValues.Add(0);
                
            for (int i = period + 1; i < candles.Count; i++)
            {
                decimal tr = Math.Max(
                    candles[i].High - candles[i].Low,
                    Math.Max(
                        Math.Abs(candles[i].High - candles[i-1].Close),
                        Math.Abs(candles[i].Low - candles[i-1].Close)
                    )
                );
                
                decimal prevATR = atrValues[atrValues.Count - 1];
                decimal newATR = ((period - 1) * prevATR + tr) / period;
                atrValues.Add(newATR);
            }
            
            while (atrValues.Count < candles.Count)
                atrValues.Insert(0, atrValues[0]);
                
            return atrValues;
        }
        
        private void CalculateBreakerBlocksImproved(List<Candle> candles, List<BreakerBlock> breakerBlocks)
        {
            if (candles.Count < LEN + 1)
                return;

            var zigZag = new ZigZag(500);
            
            int MSS_dir = 0;
            
            for (int n = LEN; n < candles.Count; n++)
            {
                bool isPivotHigh = true;
                bool isPivotLow = true;
                
                for (int j = Math.Max(0, n - LEN); j <= Math.Min(candles.Count - 1, n + 1); j++)
                {
                    if (j != n)
                    {
                        if (candles[j].High > candles[n].High)
                            isPivotHigh = false;
                        
                        if (candles[j].Low < candles[n].Low)
                            isPivotLow = false;
                    }
                }
                
                if (isPivotHigh)
                {
                    int dir = zigZag.GetDirection(0);
                    int x1 = zigZag.GetX(0);
                    decimal y1 = zigZag.GetY(0);
                    int x2 = n;
                    decimal y2 = candles[n].High;
                    
                    if (dir < 1)  
                    {
                        zigZag.AddPoint(1, x1, y1, x2, y2, true);
                    }
                    else if (dir == 1 && y2 > y1) 
                    {
                        zigZag.UpdatePoint(0, x2, y2);
                    }
                }
                
                if (isPivotLow)
                {
                    int dir = zigZag.GetDirection(0);
                    int x1 = zigZag.GetX(0);
                    decimal y1 = zigZag.GetY(0);
                    int x2 = n;
                    decimal y2 = candles[n].Low;
                    
                    if (dir > -1)  
                    {
                        zigZag.AddPoint(-1, x1, y1, x2, y2, true);
                    }
                    else if (dir == -1 && y2 < y1) 
                    {
                        zigZag.UpdatePoint(0, x2, y2);
                    }
                }
                
                if (zigZag.Count < 5)
                    continue;
                
                int iH = zigZag.GetDirection(2) == 1 ? 2 : 1;
                
                if (candles[n].Close > zigZag.GetY(iH) && zigZag.GetDirection(iH) == 1 && MSS_dir < 1)
                {
                    if (iH + 3 < zigZag.Count)
                    {
                        int Ex = zigZag.GetX(iH - 1);
                        decimal Ey = zigZag.GetY(iH - 1);
                        int Dx = zigZag.GetX(iH);
                        decimal Dy = zigZag.GetY(iH);
                        int Cx = zigZag.GetX(iH + 1);
                        decimal Cy = zigZag.GetY(iH + 1);
                        int Bx = zigZag.GetX(iH + 2);
                        decimal By = zigZag.GetY(iH + 2);
                        int Ax = zigZag.GetX(iH + 3);
                        decimal Ay = zigZag.GetY(iH + 3);
                        
                        decimal _y = Math.Max(By, Dy);
                        decimal mid = Ay + ((_y - Ay) / 2);
                        
                        bool isOK = true;
                        
                        if (Ey < Cy && Cx != Dx && isOK)
                        {
                            for (int i = Dx; i > Cx; i--)
                            {
                                if (i >= 0 && i < candles.Count && candles[i].Close > candles[i].Open)
                                {
                                    decimal green1prT = BREAKER_CANDLE_ONLY_BODY ? 
                                        Math.Max(candles[i].Open, candles[i].Close) : candles[i].High;
                                    decimal green1prB = BREAKER_CANDLE_ONLY_BODY ? 
                                        Math.Min(candles[i].Open, candles[i].Close) : candles[i].Low;
                                    
                                    if (BREAKER_CANDLE_2_LAST && i + 1 < candles.Count)
                                    {
                                        if (candles[i + 1].Close > candles[i + 1].Open)
                                        {
                                            decimal green2prT = BREAKER_CANDLE_ONLY_BODY ? 
                                                Math.Max(candles[i + 1].Open, candles[i + 1].Close) : candles[i + 1].High;
                                            decimal green2prB = BREAKER_CANDLE_ONLY_BODY ? 
                                                Math.Min(candles[i + 1].Open, candles[i + 1].Close) : candles[i + 1].Low;
                                            
                                            if (green2prT > green1prT || green2prB < green1prB)
                                            {
                                                green1prT = Math.Max(green1prT, green2prT);
                                                green1prB = Math.Min(green1prB, green2prB);
                                            }
                                        }
                                    }
                                    
                                    decimal avg = (green1prB + green1prT) / 2;
                                    
                                    breakerBlocks.Add(new BreakerBlock
                                    {
                                        Type = "+BB",
                                        Date = candles[n].DateTime,
                                        Index = n,
                                        CandleIndex = i,
                                        High = green1prT,
                                        Low = green1prB,
                                        Average = avg,
                                        IsBroken = false,
                                        IsMitigated = false
                                    });
                                    
                                    if (TILL_FIRST_BREAK)
                                        break;
                                }
                            }
                        }
                    }
                    
                    MSS_dir = 1;
                }
                
                int iL = zigZag.GetDirection(2) == -1 ? 2 : 1;
                
                if (candles[n].Close < zigZag.GetY(iL) && zigZag.GetDirection(iL) == -1 && MSS_dir > -1)
                {
                    if (iL + 3 < zigZag.Count)
                    {
                        int Ex = zigZag.GetX(iL - 1);
                        decimal Ey = zigZag.GetY(iL - 1);
                        int Dx = zigZag.GetX(iL);
                        decimal Dy = zigZag.GetY(iL);
                        int Cx = zigZag.GetX(iL + 1);
                        decimal Cy = zigZag.GetY(iL + 1);
                        int Bx = zigZag.GetX(iL + 2);
                        decimal By = zigZag.GetY(iL + 2);
                        int Ax = zigZag.GetX(iL + 3);
                        decimal Ay = zigZag.GetY(iL + 3);
                        
                        decimal _y = Math.Min(By, Dy);
                        decimal mid = Ay - ((Ay - _y) / 2);
                        
                        bool isOK = true;
                        
                        if (Ey > Cy && Cx != Dx && isOK)
                        {
                            for (int i = Dx; i > Cx; i--)
                            {
                                if (i >= 0 && i < candles.Count && candles[i].Close < candles[i].Open)
                                {
                                    decimal red1prT = BREAKER_CANDLE_ONLY_BODY ? 
                                        Math.Max(candles[i].Open, candles[i].Close) : candles[i].High;
                                    decimal red1prB = BREAKER_CANDLE_ONLY_BODY ? 
                                        Math.Min(candles[i].Open, candles[i].Close) : candles[i].Low;
                                    
                                    if (BREAKER_CANDLE_2_LAST && i + 1 < candles.Count)
                                    {
                                        if (candles[i + 1].Close < candles[i + 1].Open)
                                        {
                                            decimal red2prT = BREAKER_CANDLE_ONLY_BODY ? 
                                                Math.Max(candles[i + 1].Open, candles[i + 1].Close) : candles[i + 1].High;
                                            decimal red2prB = BREAKER_CANDLE_ONLY_BODY ? 
                                                Math.Min(candles[i + 1].Open, candles[i + 1].Close) : candles[i + 1].Low;
                                            
                                            if (red2prT > red1prT || red2prB < red1prB)
                                            {
                                                red1prT = Math.Max(red1prT, red2prT);
                                                red1prB = Math.Min(red1prB, red2prB);
                                            }
                                        }
                                    }
                                    
                                    decimal avg = (red1prB + red1prT) / 2;
                                    
                                    breakerBlocks.Add(new BreakerBlock
                                    {
                                        Type = "-BB",
                                        Date = candles[n].DateTime,
                                        Index = n,
                                        CandleIndex = i,
                                        High = red1prT,
                                        Low = red1prB,
                                        Average = avg,
                                        IsBroken = false,
                                        IsMitigated = false
                                    });
                                    
                                    if (TILL_FIRST_BREAK)
                                        break;
                                }
                            }
                        }
                    }
                    
                    MSS_dir = -1;
                }
            }
        }
        
        private KlineInterval ToKlineInterval(string timeframe)
        {
            return timeframe switch
            {
                "1m" => KlineInterval.OneMinute,
                "3m" => KlineInterval.ThreeMinutes,
                "5m" => KlineInterval.FiveMinutes,
                "15m" => KlineInterval.FifteenMinutes,
                "30m" => KlineInterval.ThirtyMinutes,
                "1h" => KlineInterval.OneHour,
                "2h" => KlineInterval.TwoHour,
                "4h" => KlineInterval.FourHour,
                "6h" => KlineInterval.SixHour,
                "8h" => KlineInterval.EightHour,
                "12h" => KlineInterval.TwelveHour,
                "1d" => KlineInterval.OneDay,
                "3d" => KlineInterval.ThreeDay,
                "1w" => KlineInterval.OneWeek,
                "1M" => KlineInterval.OneMonth,
                _ => KlineInterval.OneDay
            };
        }
    }
    
    public class ZigZag
    {
        private readonly List<int> _direction;
        private readonly List<int> _x;
        private readonly List<decimal> _y;
        private readonly List<bool> _boolean;
        private readonly int _maxSize;

        public int Count => _direction.Count;

        public ZigZag(int maxSize)
        {
            _direction = new List<int>();
            _x = new List<int>();
            _y = new List<decimal>();
            _boolean = new List<bool>();
            _maxSize = maxSize;
        }

        public int GetDirection(int index)
        {
            return index < _direction.Count ? _direction[index] : 0;
        }

        public int GetX(int index)
        {
            return index < _x.Count ? _x[index] : 0;
        }

        public decimal GetY(int index)
        {
            return index < _y.Count ? _y[index] : 0m;
        }

        public bool GetBoolean(int index)
        {
            return index < _boolean.Count ? _boolean[index] : false;
        }

        public void AddPoint(int dir, int x1, decimal y1, int x2, decimal y2, bool b)
        {
            _direction.Insert(0, dir);
            _x.Insert(0, x2);
            _y.Insert(0, y2);
            _boolean.Insert(0, b);
            
            if (_direction.Count > _maxSize)
            {
                _direction.RemoveAt(_direction.Count - 1);
                _x.RemoveAt(_x.Count - 1);
                _y.RemoveAt(_y.Count - 1);
                _boolean.RemoveAt(_boolean.Count - 1);
            }
        }

        public void UpdatePoint(int index, int x, decimal y)
        {
            if (index < _x.Count)
            {
                _x[index] = x;
                _y[index] = y;
            }
        }
        
    }
}