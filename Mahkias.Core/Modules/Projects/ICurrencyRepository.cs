using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects.Data.Result;

namespace Mahkias.Core.Modules.Projects
{
    public class Currency : EntityBase
    {
    }

    public interface ICurrencyRepository : IRepository<Currency>
    {
        Task<IReadOnlyList<CurrencyResult>> ListActiveAsync();
    }
}
