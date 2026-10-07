using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects.Data;
using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Core.Modules.Projects.Data.Result;

namespace Mahkias.Core.Modules.Projects
{
    public class QuotationEntity : EntityBase
    {
    }

    public interface IQuotationRepository : IRepository<QuotationEntity>
    {
        Task<IReadOnlyList<QuotationMapActivity>> GetActivitiesForMappingAsync(IReadOnlyList<int> projectIds);

        Task<int> CreateAsync(CreateQuotationArgs args);

        Task<string> NextCodeAsync();

        Task<IReadOnlyList<QuotationListItem>> GetByProjectAsync(int projectId);

        Task<QuotationDetailResult> GetDetailAsync(int quotationId);

        Task<IReadOnlyList<ActivityQuotationLink>> GetActivityLinksAsync(int projectId);

        Task<bool> UpdateAsync(int quotationId, CreateQuotationArgs args);

        Task<bool> DeleteAsync(int quotationId);

        Task<IReadOnlyList<PartPriceHistoryItem>> GetPriceHistoryAsync(string partNo);
    }
}
