namespace Mahkias.Core.Modules.Projects.Data.Result
{
    public class ProjectActivityResult
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public string PartNo { get; set; }
        public string MainPartNo { get; set; }
        public List<string> AlternativePartNos { get; set; } = new();
        public decimal? Budget { get; set; }
        public string Description { get; set; }
        public string DSNNo { get; set; }
        public decimal? Quantity { get; set; }
        public int? TypeId { get; set; }
        public string Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public int? ActivityGroupId { get; set; }
        public string ActivityGroupName { get; set; }
        public int QuoteReceived { get; set; }
        public int DeliveryProgress { get; set; }
    }
}
