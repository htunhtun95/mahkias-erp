namespace Mahkias.Core.Modules.Projects.Data.Result
{
    public class ProjectHistoryEntry
    {
        public int Id { get; set; }

        public string Source { get; set; }

        public string Action { get; set; }

        public string Summary { get; set; }

        public string ActorName { get; set; }

        public string ActorEmail { get; set; }

        public DateTime CreatedAt { get; set; }

        public int? PurchaseOrderId { get; set; }

        public string PurchaseOrderCode { get; set; }
    }
}
