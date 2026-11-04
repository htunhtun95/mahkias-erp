using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Core.Modules.Projects.Data.Result;

namespace Mahkias.Core.Modules.Projects
{
    public class PurchaseOrderEntity : EntityBase
    {
    }

    public interface IPurchaseOrderRepository : IRepository<PurchaseOrderEntity>
    {
        Task<IReadOnlyList<PurchaseOrderListItem>> GetByProjectAsync(int projectId);

        Task<IReadOnlyList<PurchaseOrderTraceLine>> GetLinesByProjectAsync(int projectId);

        Task<PurchaseOrderDetailResult> GetDetailAsync(int purchaseOrderId);

        Task<IReadOnlyList<PurchaseOrderActivityMatch>> GetProjectActivitiesAsync(int projectId);

        Task<int> CreateAsync(CreatePurchaseOrderArgs args);

        Task<int> ImportAsync(CreatePurchaseOrderArgs args, Func<int, CancellationToken, Task<CreatePurchaseOrderDocumentArgs>> attachFile, CancellationToken cancellationToken = default);

        Task SaveExistingAsync(int purchaseOrderId, CreatePurchaseOrderArgs args, Func<int, CancellationToken, Task<CreatePurchaseOrderDocumentArgs>> attachFile, CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(int purchaseOrderId);

        Task<string> NextCodeAsync();

        Task<IReadOnlyList<PurchaseOrderDocumentResult>> ListDocumentsAsync(int projectId, int purchaseOrderId);

        Task<PurchaseOrderDocumentResult> AddDocumentAsync(CreatePurchaseOrderDocumentArgs args);

        Task<bool> RemoveDocumentAsync(int projectId, int purchaseOrderId, int documentId);
    }
}
