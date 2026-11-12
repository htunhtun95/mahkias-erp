using Mahkias.Core.Helpers;
using Mahkias.Core.Modules.Projects;
using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Core.Modules.Projects.Data.Result;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Mahkias.Data.Modules.Projects.Repositories
{
    public class ProjectHistoryRepository : AdoRepository<ProjectHistoryEntry>, IProjectHistoryRepository
    {
        public ProjectHistoryRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task AddAsync(ProjectHistoryWrite entry)
        {
            if (entry == null || entry.ProjectId <= 0 || string.IsNullOrWhiteSpace(entry.Action) || string.IsNullOrWhiteSpace(entry.Summary))
            {
                return;
            }

            using var command = new SqlCommand(@"
INSERT INTO projects.ProjectHistory
    (ProjectId, Action, Summary, ActorName, ActorEmail, UserId)
VALUES
    (@ProjectId, @Action, @Summary,
     NULLIF(LTRIM(RTRIM(@ActorName)), N''),
     NULLIF(LTRIM(RTRIM(@ActorEmail)), N''),
     @UserId);");
            command.Parameters.AddInt("ProjectId", entry.ProjectId);
            command.Parameters.AddNVarChar("Action", entry.Action.Trim(), 40);
            command.Parameters.AddNVarChar("Summary", TrimSummary(entry.Summary), 400);
            command.Parameters.AddNVarChar("ActorName", entry.ActorName ?? string.Empty, 200);
            command.Parameters.AddNVarChar("ActorEmail", entry.ActorEmail ?? string.Empty, 320);
            command.Parameters.AddNullableInt("UserId", entry.UserId);
            command.Connection = _connection;
            _connection.Open();
            try
            {
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                _connection.Close();
            }
        }

        public async Task<IReadOnlyList<ProjectHistoryEntry>> ListAsync(int projectId, bool includePurchaseOrders)
        {
            var entries = new List<ProjectHistoryEntry>();
            using var command = new SqlCommand(@"
SELECT Id, Source, Action, Summary, ActorName, ActorEmail, CreatedAt, PurchaseOrderId, PurchaseOrderCode
FROM (
    SELECT
        H.Id,
        Source = N'Project',
        H.Action,
        H.Summary,
        H.ActorName,
        H.ActorEmail,
        H.CreatedAt,
        PurchaseOrderId = CAST(NULL AS INT),
        PurchaseOrderCode = CAST(NULL AS NVARCHAR(50))
    FROM projects.ProjectHistory H
    WHERE H.ProjectId = @ProjectId

    UNION ALL

    SELECT
        H.Id,
        Source = N'PurchaseOrder',
        H.Action,
        H.Summary,
        H.ActorName,
        H.ActorEmail,
        H.CreatedAt,
        H.PurchaseOrderId,
        PO.Code
    FROM projects.PurchaseOrderHistory H
    INNER JOIN projects.PurchaseOrders PO ON PO.Id = H.PurchaseOrderId
    INNER JOIN projects.PurchaseOrderProjects PP ON PP.PurchaseOrderId = PO.Id
    WHERE @IncludePurchaseOrders = 1
      AND PP.ProjectId = @ProjectId
) AS History
ORDER BY CreatedAt DESC, Id DESC;");
            command.Parameters.AddInt("ProjectId", projectId);
            command.Parameters.Add(new SqlParameter("@IncludePurchaseOrders", SqlDbType.Bit) { Value = includePurchaseOrders });
            command.Connection = _connection;
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    entries.Add(new ProjectHistoryEntry
                    {
                        Id = reader.ReadIntValue("Id"),
                        Source = reader.ReadStringValue("Source"),
                        Action = reader.ReadStringValue("Action"),
                        Summary = reader.ReadStringValue("Summary"),
                        ActorName = reader.ReadNullableStringValue("ActorName"),
                        ActorEmail = reader.ReadNullableStringValue("ActorEmail"),
                        CreatedAt = reader.ReadDateTimeValue("CreatedAt"),
                        PurchaseOrderId = reader.ReadNullableIntValue("PurchaseOrderId"),
                        PurchaseOrderCode = reader.ReadNullableStringValue("PurchaseOrderCode"),
                    });
                }
            }
            finally
            {
                _connection.Close();
            }

            return entries;
        }

        public override ProjectHistoryEntry PopulateRecord(SqlDataReader reader)
        {
            throw new NotImplementedException();
        }

        private static string TrimSummary(string value)
        {
            var text = (value ?? string.Empty).Trim();
            return text.Length <= 400 ? text : text.Substring(0, 400);
        }
    }
}
