using Project.model;
using System.Threading.Tasks;

namespace Project.service
{
    public interface IMarketStructureService
    {
        Task<MarketStructureResponse> CalculateMarketStructureAsync(MarketStructureRequest request);
    }
} 