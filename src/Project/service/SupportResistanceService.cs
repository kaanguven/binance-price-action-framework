using Project.model;
using System.Threading.Tasks;

namespace Project.service
{
    public interface SupportResistanceService
    {
        Task<SupportResistanceResponse> CalculateSupportResistance(
            string symbol, 
            string interval, 
            float multiplicativeFactor = 8.0f, 
            int atrLength = 50, 
            int extendLast = 4,
            int limit = 500);
    }
} 