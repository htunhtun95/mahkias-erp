using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects.Data.Result;

namespace Mahkias.Core.Modules.Projects
{
    public class LookupValue : EntityBase
    {
    }

    public interface ILookupRepository : IRepository<LookupValue>
    {
        Task<IReadOnlyList<LookupValueResult>> ListUnitsAsync();

        Task<IReadOnlyList<LookupValueResult>> ListConditionsAsync();
    }
}
