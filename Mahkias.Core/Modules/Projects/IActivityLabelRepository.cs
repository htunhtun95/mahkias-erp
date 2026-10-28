using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects.Data.Result;

namespace Mahkias.Core.Modules.Projects
{
    public class ActivityLabelEntity : EntityBase
    {
    }

    public interface IActivityLabelRepository : IRepository<ActivityLabelEntity>
    {
        Task<IReadOnlyList<ActivityLabelResult>> ListAsync(int projectId);

        Task<ActivityGroupAssignResult> CreateAsync(int projectId, string name, int typeId);

        Task<ActivityGroupAssignResult> AssignActivityAsync(int projectId, int activityId, int typeId, string name);

        Task<bool> DeleteAsync(int id, int projectId);

        Task<ActivityGroupAssignResult> SetActivitiesAsync(int projectId, IReadOnlyList<int> activityIds, IReadOnlyList<int> labelIds);

        Task<ActivityGroupAssignResult> SetGroupsAsync(int projectId, IReadOnlyList<int> activityGroupIds, IReadOnlyList<int> labelIds);

        Task ClearMismatchedAsync(int projectId);

        Task<ActivityGroupAssignResult> RemoveActivityAsync(int projectId, int activityId, int? labelId);

        Task<ActivityGroupAssignResult> RemoveMembersAsync(int projectId, IReadOnlyList<int> activityIds, IReadOnlyList<int> activityGroupIds, int? labelId);

        Task DeleteEmptyAsync(int projectId);

        Task DeleteEmptyForActivityAsync(int activityId);
    }
}
