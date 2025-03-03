using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Project.model;
using Project.service;

namespace Project.service.impl
{
    public class ElliottWaveServiceImpl : ElliottWaveService
    {
        private readonly CryptoService _cryptoService;
        
        public ElliottWaveServiceImpl(CryptoService cryptoService)
        {
            _cryptoService = cryptoService;
        }
        
        public async Task<ElliottWaveResponse> CalculateElliottWaves(ElliottWaveRequest request)
        {
            var ohlcRequest = new OHLCRequest
            {
                Symbol = request.Symbol,
                Timeframe = request.Timeframe,
                Limit = request.Limit,
                Since = request.Since
            };
            
            var ohlcResponse = await _cryptoService.FetchOHLCAsync(ohlcRequest);
            
            var response = new ElliottWaveResponse
            {
                Symbol = request.Symbol,
                Timeframe = request.Timeframe,
                Exchange = ohlcResponse.Exchange,
                WavePatterns = new List<WavePattern>(),
                ZigZagPoints = new List<ZigZagPoint>()
            };
            
            if (ohlcResponse.Candles == null || !ohlcResponse.Candles.Any())
                return response;
            
            if (request.UseLength1)
                CalculateZigZagAndWaves(ohlcResponse.Candles, response.ZigZagPoints, response.WavePatterns, request.Length1);
            
            if (request.UseLength2)
                CalculateZigZagAndWaves(ohlcResponse.Candles, response.ZigZagPoints, response.WavePatterns, request.Length2);
            
            if (request.UseLength3)
                CalculateZigZagAndWaves(ohlcResponse.Candles, response.ZigZagPoints, response.WavePatterns, request.Length3);
            
            CalculateFibonacciLevels(response.WavePatterns, request);
            
            return response;
        }
        
        private void CalculateZigZagAndWaves(List<Candle> candles, List<ZigZagPoint> zigZagPoints, 
                                           List<WavePattern> wavePatterns, int length)
        {
            var zigZag = new ZigZag(500);
            
            for (int i = length; i < candles.Count; i++)
            {
                bool isPivotHigh = IsPivotPoint(candles, i, length, true);
                bool isPivotLow = IsPivotPoint(candles, i, length, false);
                
                if (isPivotHigh)
                {
                    int dir = zigZag.GetDirection(0);
                    int x1 = zigZag.GetX(0);
                    decimal y1 = zigZag.GetY(0);
                    int x2 = i;
                    decimal y2 = candles[i].High;
                    
                    if (dir < 1)
                    {
                        zigZag.AddPoint(1, x1, y1, x2, y2, true);
                        zigZagPoints.Add(new ZigZagPoint
                        {
                            Direction = 1,
                            Index = x2,
                            Price = y2,
                            Date = candles[i].DateTime
                        });
                    }
                    else if (dir == 1 && y2 > y1)
                    {
                        zigZag.UpdatePoint(0, x2, y2);
                        if (zigZagPoints.Any() && zigZagPoints.Last().Direction == 1)
                        {
                            zigZagPoints.Last().Index = x2;
                            zigZagPoints.Last().Price = y2;
                            zigZagPoints.Last().Date = candles[i].DateTime;
                        }
                    }
                    
                    DetectElliottWavePatterns(zigZag, candles, wavePatterns, i);
                }
                
                if (isPivotLow)
                {
                    int dir = zigZag.GetDirection(0);
                    int x1 = zigZag.GetX(0);
                    decimal y1 = zigZag.GetY(0);
                    int x2 = i;
                    decimal y2 = candles[i].Low;
                    
                    if (dir > -1)
                    {
                        zigZag.AddPoint(-1, x1, y1, x2, y2, true);
                        zigZagPoints.Add(new ZigZagPoint
                        {
                            Direction = -1,
                            Index = x2,
                            Price = y2,
                            Date = candles[i].DateTime
                        });
                    }
                    else if (dir == -1 && y2 < y1)
                    {
                        zigZag.UpdatePoint(0, x2, y2);
                        if (zigZagPoints.Any() && zigZagPoints.Last().Direction == -1)
                        {
                            zigZagPoints.Last().Index = x2;
                            zigZagPoints.Last().Price = y2;
                            zigZagPoints.Last().Date = candles[i].DateTime;
                        }
                    }
                    
                    DetectElliottWavePatterns(zigZag, candles, wavePatterns, i);
                }
            }
        }
        
        private void DetectElliottWavePatterns(ZigZag zigZag, List<Candle> candles, 
                                             List<WavePattern> wavePatterns, int currentIndex)
        {
            if (zigZag.Count < 6)
                return;
                
            int _6x = zigZag.GetX(0), _6y = (int)zigZag.GetY(0);
            int _5x = zigZag.GetX(1), _5y = (int)zigZag.GetY(1);
            int _4x = zigZag.GetX(2), _4y = (int)zigZag.GetY(2);
            int _3x = zigZag.GetX(3), _3y = (int)zigZag.GetY(3);
            int _2x = zigZag.GetX(4), _2y = (int)zigZag.GetY(4);
            int _1x = zigZag.GetX(5), _1y = (int)zigZag.GetY(5);
            
            if (zigZag.GetDirection(0) == 1)
            {
                decimal _W5 = zigZag.GetY(0) - zigZag.GetY(1);
                decimal _W3 = zigZag.GetY(2) - zigZag.GetY(3);
                decimal _W1 = zigZag.GetY(4) - zigZag.GetY(5);
                
                decimal min = Math.Min(Math.Min(_W1, _W3), _W5);
                
                bool isWave = _W3 != min &&
                              zigZag.GetY(0) > zigZag.GetY(2) &&
                              zigZag.GetY(3) > zigZag.GetY(5) &&
                              zigZag.GetY(1) > zigZag.GetY(4);
                              
                if (isWave)
                {
                    var wavePattern = new WavePattern
                    {
                        Type = "Motive",
                        Direction = "Bullish",
                        Date = candles[currentIndex].DateTime,
                        Index = currentIndex,
                        IsValid = true,
                        Points = new List<WavePoint>()
                    };
                    
                    for (int i = 0; i < 6; i++)
                    {
                        int idx = zigZag.GetX(i);
                        decimal price = zigZag.GetY(i);
                        
                        wavePattern.Points.Add(new WavePoint
                        {
                            Label = (i == 0) ? "5" : (i == 1) ? "4" : (i == 2) ? "3" : (i == 3) ? "2" : (i == 4) ? "1" : "0",
                            Index = idx,
                            Price = price,
                            Date = idx < candles.Count ? candles[idx].DateTime : DateTime.MinValue
                        });
                    }
                    
                    wavePatterns.Add(wavePattern);
                }
                
                if (wavePatterns.Any() && wavePatterns.Last().Type == "Motive" && 
                    wavePatterns.Last().Direction == "Bearish")
                {
                    var lastMotiveWave = wavePatterns.Last();
                    decimal waveDiff = Math.Abs(lastMotiveWave.Points[0].Price - lastMotiveWave.Points[5].Price);
                    
                    bool isValidCorrectiveWave = 
                        zigZag.GetX(3) == lastMotiveWave.Points[0].Index &&
                        zigZag.GetY(0) < lastMotiveWave.Points[0].Price + (waveDiff * 0.854m) &&
                        zigZag.GetY(2) < lastMotiveWave.Points[0].Price + (waveDiff * 0.854m) &&
                        zigZag.GetY(1) > lastMotiveWave.Points[0].Price;
                    
                    if (isValidCorrectiveWave)
                    {
                        var correctiveWave = new WavePattern
                        {
                            Type = "Corrective",
                            Direction = "Bullish",
                            Date = candles[currentIndex].DateTime,
                            Index = currentIndex,
                            IsValid = true,
                            Points = new List<WavePoint>()
                        };
                        
                        correctiveWave.Points.Add(new WavePoint
                        {
                            Label = "A",
                            Index = zigZag.GetX(2),
                            Price = zigZag.GetY(2),
                            Date = zigZag.GetX(2) < candles.Count ? candles[zigZag.GetX(2)].DateTime : DateTime.MinValue
                        });
                        
                        correctiveWave.Points.Add(new WavePoint
                        {
                            Label = "B",
                            Index = zigZag.GetX(1),
                            Price = zigZag.GetY(1),
                            Date = zigZag.GetX(1) < candles.Count ? candles[zigZag.GetX(1)].DateTime : DateTime.MinValue
                        });
                        
                        correctiveWave.Points.Add(new WavePoint
                        {
                            Label = "C",
                            Index = zigZag.GetX(0),
                            Price = zigZag.GetY(0),
                            Date = zigZag.GetX(0) < candles.Count ? candles[zigZag.GetX(0)].DateTime : DateTime.MinValue
                        });
                        
                        wavePatterns.Add(correctiveWave);
                    }
                }
            }
            
            if (zigZag.GetDirection(0) == -1)
            {
                decimal _W5 = zigZag.GetY(1) - zigZag.GetY(0);
                decimal _W3 = zigZag.GetY(3) - zigZag.GetY(2);
                decimal _W1 = zigZag.GetY(5) - zigZag.GetY(4);
                
                decimal min = Math.Min(Math.Min(_W1, _W3), _W5);
                
                bool isWave = _W3 != min &&
                              zigZag.GetY(2) > zigZag.GetY(0) &&
                              zigZag.GetY(5) > zigZag.GetY(3) &&
                              zigZag.GetY(4) > zigZag.GetY(1);
                              
                if (isWave)
                {
                    var wavePattern = new WavePattern
                    {
                        Type = "Motive",
                        Direction = "Bearish",
                        Date = candles[currentIndex].DateTime,
                        Index = currentIndex,
                        IsValid = true,
                        Points = new List<WavePoint>()
                    };
                    
                    for (int i = 0; i < 6; i++)
                    {
                        int idx = zigZag.GetX(i);
                        decimal price = zigZag.GetY(i);
                        
                        wavePattern.Points.Add(new WavePoint
                        {
                            Label = (i == 0) ? "5" : (i == 1) ? "4" : (i == 2) ? "3" : (i == 3) ? "2" : (i == 4) ? "1" : "0",
                            Index = idx,
                            Price = price,
                            Date = idx < candles.Count ? candles[idx].DateTime : DateTime.MinValue
                        });
                    }
                    
                    wavePatterns.Add(wavePattern);
                }
                
                if (wavePatterns.Any() && wavePatterns.Last().Type == "Motive" && 
                    wavePatterns.Last().Direction == "Bullish")
                {
                    var lastMotiveWave = wavePatterns.Last();
                    decimal waveDiff = Math.Abs(lastMotiveWave.Points[0].Price - lastMotiveWave.Points[5].Price);
                    
                    bool isValidCorrectiveWave = 
                        zigZag.GetX(3) == lastMotiveWave.Points[0].Index &&
                        zigZag.GetY(0) > lastMotiveWave.Points[0].Price - (waveDiff * 0.854m) &&
                        zigZag.GetY(2) > lastMotiveWave.Points[0].Price - (waveDiff * 0.854m) &&
                        zigZag.GetY(1) < lastMotiveWave.Points[0].Price;
                    
                    if (isValidCorrectiveWave)
                    {
                        var correctiveWave = new WavePattern
                        {
                            Type = "Corrective",
                            Direction = "Bearish",
                            Date = candles[currentIndex].DateTime,
                            Index = currentIndex,
                            IsValid = true,
                            Points = new List<WavePoint>()
                        };
                        
                        correctiveWave.Points.Add(new WavePoint
                        {
                            Label = "A",
                            Index = zigZag.GetX(2),
                            Price = zigZag.GetY(2),
                            Date = zigZag.GetX(2) < candles.Count ? candles[zigZag.GetX(2)].DateTime : DateTime.MinValue
                        });
                        
                        correctiveWave.Points.Add(new WavePoint
                        {
                            Label = "B",
                            Index = zigZag.GetX(1),
                            Price = zigZag.GetY(1),
                            Date = zigZag.GetX(1) < candles.Count ? candles[zigZag.GetX(1)].DateTime : DateTime.MinValue
                        });
                        
                        correctiveWave.Points.Add(new WavePoint
                        {
                            Label = "C",
                            Index = zigZag.GetX(0),
                            Price = zigZag.GetY(0),
                            Date = zigZag.GetX(0) < candles.Count ? candles[zigZag.GetX(0)].DateTime : DateTime.MinValue
                        });
                        
                        wavePatterns.Add(correctiveWave);
                    }
                }
            }
        }
        
        private void CalculateFibonacciLevels(List<WavePattern> wavePatterns, ElliottWaveRequest request)
        {
            foreach (var pattern in wavePatterns)
            {
                if (pattern.Type == "Motive" && pattern.IsValid && pattern.Points.Count >= 6)
                {
                    var start = pattern.Points.First(p => p.Label == "0");
                    var end = pattern.Points.First(p => p.Label == "5");
                    
                    decimal diff = Math.Abs(end.Price - start.Price);
                    bool isBullish = pattern.Direction == "Bullish";
                    
                    pattern.FibonacciLevels = new List<FibonacciLevel>();
                    
                    pattern.FibonacciLevels.Add(new FibonacciLevel
                    {
                        Level = 1,
                        Ratio = request.FibLevel1,
                        Price = end.Price + ((isBullish ? -1 : 1) * diff * request.FibLevel1)
                    });
                    
                    pattern.FibonacciLevels.Add(new FibonacciLevel
                    {
                        Level = 2,
                        Ratio = request.FibLevel2,
                        Price = end.Price + ((isBullish ? -1 : 1) * diff * request.FibLevel2)
                    });
                    
                    pattern.FibonacciLevels.Add(new FibonacciLevel
                    {
                        Level = 3,
                        Ratio = request.FibLevel3,
                        Price = end.Price + ((isBullish ? -1 : 1) * diff * request.FibLevel3)
                    });
                    
                    pattern.FibonacciLevels.Add(new FibonacciLevel
                    {
                        Level = 4,
                        Ratio = request.FibLevel4,
                        Price = end.Price + ((isBullish ? -1 : 1) * diff * request.FibLevel4)
                    });
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
    }
}