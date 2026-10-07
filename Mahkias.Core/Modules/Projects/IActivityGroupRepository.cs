using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects.Data.Result;

namespace Mahkias.Core.Modules.Projects
{
    public class ActivityGroupEntity : EntityBase
    {
    }

    public interface IActivityGroupRepository : IRepository<ActivityGroupEntity>
    {
        Task<IReadOnlyList<ActivityGroupResult>> ListAsync(int projectId);

        Task<int> CreateAsync(int projectId, string name, string description);

        Task<bool> UpdateAsync(int id, int projectId, string name, string description);

        Task<bool> DeleteAsync(int id, int projectId);

        Task<ActivityGroupAssignResult> AssignAsync(int projectId, int activityId, int? activityGroupId);

        Task<ActivityGroupAssignResult> AssignManyAsync(int projectId, IReadOnlyList<int> activityIds, int? activityGroupId);
    }
}
