using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects;

namespace Mahkias.Data
{
    public class Uow : IUow, IDisposable
    {
        public Uow(IRepositoryProvider repositoryProvider, string coreConnectionString)
        {
            RepositoryProvider = repositoryProvider;
            repositoryProvider.CoreConnectionString = coreConnectionString;
        }

        protected IRepositoryProvider RepositoryProvider { get; set; }

        protected T GetAdoRepo<T>() where T : class
        {
            return RepositoryProvider.GetAdoRepository<T>();
        }

        public IProjectRepository Projects { get { return GetAdoRepo<IProjectRepository>(); } }
        public IActivityRepository Activities { get { return GetAdoRepo<IActivityRepository>(); } }
        public ISupplierRepository Suppliers { get { return GetAdoRepo<ISupplierRepository>(); } }
        public IQuotationRepository Quotations { get { return GetAdoRepo<IQuotationRepository>(); } }
        public IActivityGroupRepository ActivityGroups { get { return GetAdoRepo<IActivityGroupRepository>(); } }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
