using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects.Data;
using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Core.Modules.Projects.Data.Result;

namespace Mahkias.Core.Modules.Projects
{
    public interface IActivityRepository : IRepository<Activity>
    {
        Task<IReadOnlyList<ProjectActivityResult>> GetByProjectAsync(int projectId, string sortBy = null, string sortDirection = null);
        Task<int> CreateAsync(CreateActivityArgs args);
        Task<IReadOnlyList<ActivityImportIssue>> ValidateImportAsync(int projectId, IEnumerable<ActivityImportCandidate> rows);
        Task<int> BulkInsertAsync(int projectId, IEnumerable<CreateActivityArgs> activities);
        Task<int> UpdateAsync(int id, CreateActivityArgs args);
        Task<int> UpdateBatchAsync(IReadOnlyList<ActivityBatchUpdate> activities);
        Task<int> DeleteAsync(int id);
    }
}
