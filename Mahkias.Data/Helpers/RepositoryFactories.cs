using Mahkias.Core.Modules.Projects;
using Mahkias.Data.Modules.Projects.Repositories;

namespace Mahkias.Data.Helpers
{
    public class RepositoryFactories
    {
        public RepositoryFactories()
        {
            _adoRepositoryFactories = GetAdoFactories();
        }

        private IDictionary<Type, Func<string, object>> GetAdoFactories()
        {
            return new Dictionary<Type, Func<string, object>>
            {
                { typeof(IProjectRepository), connectionString => new ProjectRepository(connectionString) },
                { typeof(IActivityRepository), connectionString => new ActivityRepository(connectionString) },
                { typeof(ISupplierRepository), connectionString => new SupplierRepository(connectionString) },
                { typeof(IQuotationRepository), connectionString => new QuotationRepository(connectionString) },
                { typeof(IActivityGroupRepository), connectionString => new ActivityGroupRepository(connectionString) },
            };
        }

        private IDictionary<Type, Func<string, object>> _adoRepositoryFactories;

        public Func<string, object> GetAdoRepositoryFactory<T>()
        {
            Func<string, object> factory;
            _adoRepositoryFactories.TryGetValue(typeof(T), out factory);
            return factory;
        }
    }
}
