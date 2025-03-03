using Project.model;
using System.Threading.Tasks;

namespace Project.service
{
    public interface CryptoService
    {
        Task<OHLCResponse> FetchOHLCAsync(OHLCRequest request);
        Task<BreakerBlocksResponse> FetchBreakerBlocksAsync(OHLCRequest request);

        Task<LiquidityResponse> FetchLiquidityZonesAsync(OHLCRequest request);
    }
}