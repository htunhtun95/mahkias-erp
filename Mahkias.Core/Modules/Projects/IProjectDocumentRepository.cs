using Mahkias.Core.Data;
using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Core.Modules.Projects.Data.Result;

namespace Mahkias.Core.Modules.Projects
{
    public class ProjectDocument : EntityBase
    {
    }

    public interface IProjectDocumentRepository : IRepository<ProjectDocument>
    {
        Task<ProjectDriveFolderResult> GetDriveFolderAsync(int projectId);

        Task<string> SetDriveFolderAsync(int projectId, string folderId);

        Task<string> SetProcurementFolderAsync(int projectId, string folderId);

        Task<ProjectDocumentResult> AddAsync(CreateProjectDocumentArgs args);

        Task<IReadOnlyList<ProjectDocumentResult>> ListByProjectAsync(int projectId);

        Task<QuotationDocumentResult> AddQuotationDocumentAsync(CreateQuotationDocumentArgs args);

        Task<QuotationDocumentResult> UpdateQuotationDocumentAsync(UpdateQuotationDocumentArgs args);

        Task<bool> RemoveQuotationDocumentAsync(int projectId, int quotationId, int documentId);

        Task<IReadOnlyList<QuotationDocumentResult>> ListByQuotationAsync(int projectId, int quotationId);
    }
}
