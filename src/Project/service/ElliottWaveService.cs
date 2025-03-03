// src/Project/service/ElliottWaveService.cs

using System.Threading.Tasks;
using Project.model;

namespace Project.service
{
    public interface ElliottWaveService
    {
        Task<ElliottWaveResponse> CalculateElliottWaves(ElliottWaveRequest request);
    }
}