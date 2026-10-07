using Mahkias.Core.Modules.Projects;

namespace Mahkias.Core.Data
{
    public interface IUow
    {
        IProjectRepository Projects { get; }
        IActivityRepository Activities { get; }
        ISupplierRepository Suppliers { get; }
        IQuotationRepository Quotations { get; }
        IActivityGroupRepository ActivityGroups { get; }
    }
}
