using System.Text.Json.Serialization;

namespace Mahkias.Api.Modules.Projects
{
    public class CreateProjectRequest
    {
        public string Name { get; set; }
        public string Reference { get; set; }
        public string Description { get; set; }
    }

    public class ProjectResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Reference { get; set; }
        public string Description { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public List<ActivityResponse> Activities { get; set; } = new();
    }

    public class CreateActivityRequest
    {
        public string PartNo { get; set; }
        public decimal? Budget { get; set; }
        public string Description { get; set; }
        [JsonPropertyName("dsnNo")]
        public string DSNNo { get; set; }
        public int? Quantity { get; set; }
        public int? TypeId { get; set; }
    }

    public class UpdateActivityRequest : CreateActivityRequest
    {
        public int ProjectId { get; set; }
    }

    public class BatchActivityUpdate : UpdateActivityRequest
    {
        public int Id { get; set; }
    }

    public class BatchUpdateActivitiesRequest
    {
        public List<BatchActivityUpdate> Activities { get; set; } = new();
    }

    public class BulkActivitiesRequest
    {
        public List<CreateActivityRequest> Activities { get; set; } = new();
    }

    public class ActivityExcelUploadForm
    {
        public IFormFile File { get; set; }
        public string DsnNoColumn { get; set; }
        public string PartNoColumn { get; set; }
        public string DescriptionColumn { get; set; }
        public string TypeColumn { get; set; }
        public string QuantityColumn { get; set; }
        public string BudgetColumn { get; set; }
        public string DefaultDsnNo { get; set; }
        public string DefaultPartNo { get; set; }
        public string DefaultTypeId { get; set; }
        public string DefaultQuantity { get; set; }
        public string DefaultBudget { get; set; }
    }

    public class ActivityResponse
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string PartNo { get; set; }
        public string MainPartNo { get; set; }
        public List<string> AlternativePartNos { get; set; } = new();
        public decimal? Budget { get; set; }
        public string Description { get; set; }
        [JsonPropertyName("dsnNo")]
        public string DSNNo { get; set; }
        public decimal? Quantity { get; set; }
        public int? TypeId { get; set; }
        public string Type { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public int? ActivityGroupId { get; set; }
        public string ActivityGroupName { get; set; }
        public int QuoteReceived { get; set; }
        public int DeliveryProgress { get; set; }
    }

    public class ActivityTypeResponse
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
    }
}
