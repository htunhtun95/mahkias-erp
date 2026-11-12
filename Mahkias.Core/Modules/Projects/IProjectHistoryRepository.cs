using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Core.Modules.Projects.Data.Result;

namespace Mahkias.Core.Modules.Projects
{
    public interface IProjectHistoryRepository : IRepository<ProjectHistoryEntry>
    {
        Task AddAsync(ProjectHistoryWrite entry);

        // includePurchaseOrders stays false for the project History tab.
        // Pass true to append child purchase-order history for the same project.
        Task<IReadOnlyList<ProjectHistoryEntry>> ListAsync(int projectId, bool includePurchaseOrders);
    }
}
