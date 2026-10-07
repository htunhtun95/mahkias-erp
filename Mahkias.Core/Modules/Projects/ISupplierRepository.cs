using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects.Data;
using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Core.Modules.Projects.Data.Result;

namespace Mahkias.Core.Modules.Projects
{
    public interface ISupplierRepository : IRepository<Supplier>
    {
        Task<IReadOnlyList<SupplierResult>> SearchAsync(string search);

        Task<SupplierDetailResult> GetAsync(int id);

        Task<int> CreateAsync(CreateSupplierArgs args);

        Task<bool> UpdateAsync(int id, CreateSupplierArgs args);

        Task<SupplierDeleteResult> DeleteAsync(int id);
    }
}
