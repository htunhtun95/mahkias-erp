using Mahkias.Core.Data;

namespace Mahkias.Core.Modules.Projects
{
    public class PartSuggestion
    {
        public int PartId { get; set; }
        public string PartNo { get; set; }
        public string Description { get; set; }
    }

    public class PartDescriptionItem
    {
        public int Id { get; set; }
        public string Description { get; set; }
    }

    public class PartDescriptionWrite
    {
        public int? Id { get; set; }
        public string Description { get; set; }
    }

    public class PartDetail
    {
        public int Id { get; set; }
        public string PartNo { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<PartDescriptionItem> Descriptions { get; set; } = new();
    }

    public class PartSaveResult
    {
        public PartDetail Part { get; set; }
        public string Error { get; set; }
    }

    public class PartAlternate
    {
        public int Id { get; set; }
        public int PartId { get; set; }
        public string PartNo { get; set; }
        public List<PartDescriptionItem> Descriptions { get; set; } = new();
    }

    public class PartAlternateSaveResult
    {
        public IReadOnlyList<PartAlternate> Alternates { get; set; } = new List<PartAlternate>();
        public string Error { get; set; }
    }

    public class PartProjectLink
    {
        public int Id { get; set; }
        public string Label { get; set; }
    }

    public class PartQuotationLine
    {
        public int QuotationItemId { get; set; }
        public int QuotationId { get; set; }
        public string Code { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; }
        public string SupplierReference { get; set; }
        public string PartNo { get; set; }
        public int Quantity { get; set; }
        public string Unit { get; set; }
        public string Condition { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? TotalPrice { get; set; }
        public string CurrencyCode { get; set; }
        public string CurrencySymbol { get; set; }
        public DateTime? QuotedDate { get; set; }
        public DateTime? ValidUntil { get; set; }
        public string Match { get; set; }
        public List<PartProjectLink> Projects { get; set; } = new();
    }

    public interface IPartRepository : IRepository<PartSuggestion>
    {
        Task<IReadOnlyList<PartSuggestion>> SearchAsync(string query);
        Task<IReadOnlyList<PartDetail>> ListAsync(string search);
        Task<PartDetail> GetAsync(int id);
        Task<PartSaveResult> CreateAsync(string partNo, IReadOnlyList<PartDescriptionWrite> descriptions);
        Task<PartSaveResult> UpdateAsync(int id, string partNo, IReadOnlyList<PartDescriptionWrite> descriptions);
        Task<bool> DeleteAsync(int id);
        Task<IReadOnlyList<PartAlternate>> ListAlternatesAsync(int partId);
        Task<PartAlternateSaveResult> SaveAlternateAsync(int partId, int? linkId, string partNo, string description, int? selectedPartId);
        Task<bool> RemoveAlternateAsync(int partId, int linkId);
        Task<IReadOnlyList<PartQuotationLine>> ListQuotationsAsync(int partId);
    }
}
