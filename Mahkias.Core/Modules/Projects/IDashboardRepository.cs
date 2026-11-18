using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Core.Modules.Projects.Data.Result;

namespace Mahkias.Core.Modules.Projects
{
    public class DashboardEntity : EntityBase
    {
    }

    public interface IDashboardRepository : IRepository<DashboardEntity>
    {
        Task<DashboardSearchResult> SearchAsync(DashboardSearchArgs args);
    }
}
