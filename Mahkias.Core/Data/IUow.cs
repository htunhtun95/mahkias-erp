using Mahkias.Core.Modules.Projects;

namespace Mahkias.Core.Data
{
    public interface IUow
    {
        IProjectRepository Projects { get; }
        IProjectHistoryRepository ProjectHistory { get; }
        IActivityRepository Activities { get; }
        ISupplierRepository Suppliers { get; }
        IQuotationRepository Quotations { get; }
        IPurchaseOrderRepository PurchaseOrders { get; }
        IActivityGroupRepository ActivityGroups { get; }
        IActivityLabelRepository ActivityLabels { get; }
        ICurrencyRepository Currencies { get; }
        ILookupRepository Lookups { get; }
        IProjectDocumentRepository ProjectDocuments { get; }
    }
}
