using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects.Data;
using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Core.Modules.Projects.Data.Result;

namespace Mahkias.Core.Modules.Projects
{
    public interface IProjectRepository : IRepository<Project>
    {
        Task<SearchProjectResult> SearchAsync(SearchProjectArgs args);
        Task<GetProjectResult> GetAsync(int id);
        Task<int> AddAsync(UpsertProjectArgs args);
        Task<int> UpdateAsync(int id, UpsertProjectArgs args);
        Task<bool> DeleteAsync(int id);
    }
}
