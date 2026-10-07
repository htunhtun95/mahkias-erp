using Mahkias.Core.DataTableTypes;
using Mahkias.Core.Helpers;
using Mahkias.Core.Modules.Projects;
using Mahkias.Core.Modules.Projects.Data;
using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Core.Modules.Projects.Data.Result;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Mahkias.Data.Modules.Projects.Repositories
{
    public class ActivityRepository : AdoRepository<Activity>, IActivityRepository
    {
        public ActivityRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task<IReadOnlyList<ProjectActivityResult>> GetByProjectAsync(int projectId, string sortBy = null, string sortDirection = null)
        {
            var activities = new List<ProjectActivityResult>();
            using (SqlCommand command = new SqlCommand("dbo.GetActivitiesByProject"))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddInt("ProjectId", projectId);
                if (!string.IsNullOrWhiteSpace(sortBy))
                {
                    command.Parameters.AddNVarChar("SortBy", sortBy.Trim(), 50, false);
                }
                if (!string.IsNullOrWhiteSpace(sortDirection))
                {
                    command.Parameters.AddNVarChar("SortDirection", sortDirection.Trim(), 4, false);
                }
                command.Connection = _connection;
                _connection.Open();

                try
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (reader.Read())
                        {
                            activities.Add(new ProjectActivityResult
                            {
                                Id = reader.ReadIntValue("Id"),
                                ProjectId = reader.ReadIntValue("ProjectId"),
                                PartNo = reader.ReadStringValue("PartNo"),
                                Budget = reader.ReadNullableDecimalValue("Budget"),
                                Description = reader.ReadStringValue("Description"),
                                DSNNo = reader.ReadStringValue("DSNNo"),
                                Quantity = reader.ReadNullableDecimalValue("Quantity"),
                                TypeId = reader.ReadNullableIntValue("TypeId"),
                                Type = reader.ReadStringValue("Type"),
                                CreatedAt = reader.ReadDateTimeValue("CreatedAt"),
                                ModifiedAt = reader.ReadNullableDateTimeValue("ModifiedAt"),
                                ActivityGroupId = reader.ReadNullableIntValue("ActivityGroupId"),
                                ActivityGroupName = reader.ReadStringValue("ActivityGroupName"),
                                QuoteReceived = reader.ReadIntValue("QuoteReceived"),
                                DeliveryProgress = reader.ReadIntValue("DeliveryProgress"),
                                MainPartNo = reader.ReadStringValue("MainPartNo"),
                                AlternativePartNos = new List<string>()
                            });
                        }

                        if (await reader.NextResultAsync())
                        {
                            var byId = activities.ToDictionary(activity => activity.Id);
                            while (reader.Read())
                            {
                                var activityId = reader.ReadIntValue("ActivityId");
                                if (byId.TryGetValue(activityId, out var activity))
                                {
                                    var alternative = reader.ReadStringValue("AlternativePartNo");
                                    if (!string.IsNullOrWhiteSpace(alternative))
                                    {
                                        activity.AlternativePartNos.Add(alternative);
                                    }
                                }
                            }
                        }
                    }
                }
                finally
                {
                    _connection.Close();
                }
            }

            return activities;
        }

        public async Task<int> CreateAsync(CreateActivityArgs args)
        {
            if (args == null || string.IsNullOrWhiteSpace(args.PartNo))
            {
                return 0;
            }

            using (SqlCommand command = new SqlCommand("dbo.CreateActivity"))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddInt("ProjectId", args.ProjectId);
                command.Parameters.AddNVarCharMax("PartNo", args.PartNo, false);
                command.Parameters.AddNullableDecimal("Budget", args.Budget, 18, 2);
                command.Parameters.AddNVarCharMax("Description", string.IsNullOrWhiteSpace(args.Description) ? null : args.Description, false);
                command.Parameters.AddNVarChar("DSNNo", string.IsNullOrWhiteSpace(args.DSNNo) ? null : args.DSNNo, 100, false);
                command.Parameters.AddNullableInt("Quantity", args.Quantity);
                command.Parameters.AddNullableInt("TypeId", args.TypeId);
                command.Connection = _connection;
                _connection.Open();

                try
                {
                    var id = await command.ExecuteScalarAsync();
                    if (id == null || id == DBNull.Value)
                    {
                        return 0;
                    }

                    return Convert.ToInt32(id);
                }
                finally
                {
                    _connection.Close();
                }
            }
        }

        public async Task<IReadOnlyList<ActivityImportIssue>> ValidateImportAsync(int projectId, IEnumerable<ActivityImportCandidate> rows)
        {
            var pending = (rows ?? Array.Empty<ActivityImportCandidate>()).Where(row => row != null).ToList();
            var typeIds = await ReadActivityTypeIdsAsync();
            var issues = new List<ActivityImportIssue>();
            foreach (var row in pending)
            {
                if (string.IsNullOrWhiteSpace(row.DSNNo))
                {
                    issues.Add(Issue(row.RowNumber, "DSN No", "Field is missing."));
                }

                if (string.IsNullOrWhiteSpace(row.PartNo))
                {
                    issues.Add(Issue(row.RowNumber, "Part No", "Field is missing."));
                }

                if (row.TypeId == null || row.TypeId <= 0)
                {
                    issues.Add(Issue(row.RowNumber, "Type", "Field is missing."));
                }
                else if (!typeIds.Contains(row.TypeId.Value))
                {
                    issues.Add(Issue(row.RowNumber, "Type", $"'{row.TypeId}' is not a valid type."));
                }

                if (!row.Quantity.HasValue || row.Quantity.Value < 0)
                {
                    issues.Add(Issue(row.RowNumber, "Qty", row.Quantity.HasValue
                        ? $"'{row.Quantity.Value}' is not a valid number."
                        : "Quantity must be a numeric integer."));
                }
            }

            return issues;
        }

        public async Task<int> BulkInsertAsync(int projectId, IEnumerable<CreateActivityArgs> activities)
        {
            var source = (activities ?? Array.Empty<CreateActivityArgs>()).Where(activity => activity != null).ToList();
            var issues = await ValidateImportAsync(projectId, source.Select(activity => new ActivityImportCandidate
            {
                RowNumber = 0,
                PartNo = activity.PartNo,
                DSNNo = activity.DSNNo,
                Description = activity.Description,
                Budget = activity.Budget,
                Quantity = activity.Quantity,
                TypeId = activity.TypeId
            }));
            if (issues.Count > 0)
            {
                throw new InvalidOperationException("Activity import failed validation.");
            }

            var rows = source
                .Where(activity => !string.IsNullOrWhiteSpace(activity.PartNo))
                .Select(activity => new DefaultGenericTableType
                {
                    TextValue1 = activity.PartNo,
                    TextValue2 = string.IsNullOrWhiteSpace(activity.Description) ? null : activity.Description,
                    TextValue3 = string.IsNullOrWhiteSpace(activity.DSNNo) ? null : activity.DSNNo,
                    DecimalValue1 = activity.Budget,
                    NumericValue1 = activity.Quantity,
                    NumericValue2 = activity.TypeId.HasValue && activity.TypeId.Value > 0 ? activity.TypeId : null
                })
                .ToList();

            if (rows.Count == 0)
            {
                return 0;
            }

            using (SqlCommand command = new SqlCommand("dbo.BulkInsertActivities"))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddInt("ProjectId", projectId);
                command.Parameters.AddTable("Activities", rows.ToGenericDatatableNullableArgs(), "dbo.DefaultGenericTableType");
                command.Connection = _connection;
                _connection.Open();

                try
                {
                    var count = await command.ExecuteScalarAsync();
                    if (count == null || count == DBNull.Value)
                    {
                        return 0;
                    }

                    return Convert.ToInt32(count);
                }
                finally
                {
                    _connection.Close();
                }
            }
        }

        private async Task<HashSet<int>> ReadActivityTypeIdsAsync()
        {
            var ids = new HashSet<int>();
            using (SqlCommand command = new SqlCommand("SELECT [Id] FROM [projects].[ActivityTypes]"))
            {
                command.Connection = _connection;
                _connection.Open();
                try
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            ids.Add(reader.GetInt32(0));
                        }
                    }
                }
                finally
                {
                    _connection.Close();
                }
            }

            return ids;
        }

        private static ActivityImportIssue Issue(int row, string field, string message)
        {
            return new ActivityImportIssue
            {
                Row = row,
                Field = field,
                Message = message
            };
        }

        public async Task<int> UpdateBatchAsync(IReadOnlyList<ActivityBatchUpdate> activities)
        {
            var pending = (activities ?? Array.Empty<ActivityBatchUpdate>())
                .Where(activity => activity != null && activity.Args != null)
                .ToList();
            if (pending.Count == 0)
            {
                return 0;
            }

            if (_connection.State != ConnectionState.Closed)
            {
                _connection.Close();
            }

            _connection.Open();
            var transaction = _connection.BeginTransaction();
            try
            {
                foreach (var activity in pending)
                {
                    using var command = new SqlCommand("dbo.UpdateActivity");
                    BindUpdateCommand(command, activity.Id, activity.Args);
                    command.Connection = _connection;
                    command.Transaction = transaction;
                    var updated = await command.ExecuteScalarAsync();
                    if (updated == null || updated == DBNull.Value || Convert.ToInt32(updated) <= 0)
                    {
                        throw new InvalidOperationException("Activity batch update failed.");
                    }
                }

                transaction.Commit();
                return pending.Count;
            }
            catch (InvalidOperationException)
            {
                transaction.Rollback();
                return 0;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            finally
            {
                transaction.Dispose();
                _connection.Close();
            }
        }

        public async Task<int> UpdateAsync(int id, CreateActivityArgs args)
        {
            if (args == null || string.IsNullOrWhiteSpace(args.PartNo))
            {
                return 0;
            }

            using (SqlCommand command = new SqlCommand("dbo.UpdateActivity"))
            {
                BindUpdateCommand(command, id, args);
                command.Connection = _connection;
                _connection.Open();

                try
                {
                    var updated = await command.ExecuteScalarAsync();
                    if (updated == null || updated == DBNull.Value)
                    {
                        return 0;
                    }

                    return Convert.ToInt32(updated);
                }
                finally
                {
                    _connection.Close();
                }
            }
        }

        public async Task<int> DeleteAsync(int id)
        {
            using (SqlCommand command = new SqlCommand("dbo.DeleteActivity"))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddInt("Id", id);
                command.Connection = _connection;
                _connection.Open();

                try
                {
                    var deleted = await command.ExecuteScalarAsync();
                    if (deleted == null || deleted == DBNull.Value)
                    {
                        return 0;
                    }

                    return Convert.ToInt32(deleted);
                }
                finally
                {
                    _connection.Close();
                }
            }
        }

        private static void BindUpdateCommand(SqlCommand command, int id, CreateActivityArgs args)
        {
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddInt("Id", id);
            command.Parameters.AddInt("ProjectId", args.ProjectId);
            var dsnNo = command.Parameters.AddNVarChar("DSNNo", args.DSNNo, 100, false);
            if (dsnNo.Value == null)
            {
                dsnNo.Value = DBNull.Value;
            }
            command.Parameters.AddNVarCharMax("PartNo", args.PartNo, false);
            var typeId = command.Parameters.AddNullableInt("TypeId", args.TypeId);
            if (typeId.Value == null)
            {
                typeId.Value = DBNull.Value;
            }
            var quantity = command.Parameters.AddNullableInt("Quantity", args.Quantity);
            if (quantity.Value == null)
            {
                quantity.Value = DBNull.Value;
            }
            command.Parameters.AddNullableDecimal("Budget", args.Budget, 18, 2);
            var description = command.Parameters.AddNVarCharMax("Description", args.Description, false);
            if (description.Value == null)
            {
                description.Value = DBNull.Value;
            }
        }

        public override Activity PopulateRecord(SqlDataReader reader)
        {
            throw new NotImplementedException();
        }

    }
}
