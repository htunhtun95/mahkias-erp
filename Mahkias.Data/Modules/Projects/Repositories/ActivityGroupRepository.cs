using Mahkias.Core.Helpers;
using Mahkias.Core.Modules.Projects;
using Mahkias.Core.Modules.Projects.Data.Result;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Mahkias.Data.Modules.Projects.Repositories
{
    public class ActivityGroupRepository : AdoRepository<ActivityGroupEntity>, IActivityGroupRepository
    {
        public ActivityGroupRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task<IReadOnlyList<ActivityGroupResult>> ListAsync(int projectId)
        {
            var groups = new List<ActivityGroupResult>();
            using var command = new SqlCommand(@"
SELECT Id, ProjectId, Name, Description, ActivityTypeId, CreatedAt
FROM projects.ActivityGroups
WHERE ProjectId = @ProjectId
ORDER BY Name, Id", _connection);
            command.Parameters.AddInt("ProjectId", projectId);
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    groups.Add(ReadGroup(reader));
                }
            }
            finally
            {
                _connection.Close();
            }

            return groups;
        }

        public async Task<int> CreateAsync(int projectId, string name, string description)
        {
            using var command = new SqlCommand(@"
INSERT INTO projects.ActivityGroups (ProjectId, Name, Description)
OUTPUT INSERTED.Id
VALUES (@ProjectId, @Name, @Description)", _connection);
            command.Parameters.AddInt("ProjectId", projectId);
            command.Parameters.AddNVarChar("Name", name.Trim(), 200);
            command.Parameters.Add(OptionalText("Description", NullIfBlank(description)));
            _connection.Open();
            try
            {
                return Convert.ToInt32(await command.ExecuteScalarAsync());
            }
            finally
            {
                _connection.Close();
            }
        }

        public async Task<bool> UpdateAsync(int id, int projectId, string name, string description)
        {
            using var command = new SqlCommand(@"
UPDATE projects.ActivityGroups
SET Name = @Name,
    Description = @Description
WHERE Id = @Id
  AND ProjectId = @ProjectId", _connection);
            command.Parameters.AddInt("Id", id);
            command.Parameters.AddInt("ProjectId", projectId);
            command.Parameters.AddNVarChar("Name", name.Trim(), 200);
            command.Parameters.Add(OptionalText("Description", NullIfBlank(description)));
            _connection.Open();
            try
            {
                return await command.ExecuteNonQueryAsync() > 0;
            }
            finally
            {
                _connection.Close();
            }
        }

        public async Task<bool> DeleteAsync(int id, int projectId)
        {
            _connection.Open();
            SqlTransaction transaction = null;
            try
            {
                transaction = _connection.BeginTransaction();
                using (var clearItems = new SqlCommand(@"
UPDATE projects.QuotationItems
SET ActivityGroupId = NULL
WHERE ActivityGroupId = @Id", _connection, transaction))
                {
                    clearItems.Parameters.AddInt("Id", id);
                    await clearItems.ExecuteNonQueryAsync();
                }

                using (var clearActivities = new SqlCommand(@"
UPDATE projects.Activities
SET ActivityGroupId = NULL
WHERE ActivityGroupId = @Id
  AND ProjectId = @ProjectId", _connection, transaction))
                {
                    clearActivities.Parameters.AddInt("Id", id);
                    clearActivities.Parameters.AddInt("ProjectId", projectId);
                    await clearActivities.ExecuteNonQueryAsync();
                }

                int deleted;
                using (var remove = new SqlCommand(@"
DELETE FROM projects.ActivityGroups
WHERE Id = @Id AND ProjectId = @ProjectId", _connection, transaction))
                {
                    remove.Parameters.AddInt("Id", id);
                    remove.Parameters.AddInt("ProjectId", projectId);
                    deleted = await remove.ExecuteNonQueryAsync();
                }

                if (deleted == 0)
                {
                    transaction.Rollback();
                    return false;
                }

                transaction.Commit();
                return true;
            }
            catch
            {
                transaction?.Rollback();
                throw;
            }
            finally
            {
                transaction?.Dispose();
                _connection.Close();
            }
        }

        public async Task<ActivityGroupAssignResult> AssignManyAsync(int projectId, IReadOnlyList<int> activityIds, int? activityGroupId)
        {
            var ids = (activityIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToArray();
            if (ids.Length == 0)
            {
                return new ActivityGroupAssignResult { Error = "Select at least one activity." };
            }

            return await AssignCoreAsync(projectId, ids, activityGroupId);
        }

        public async Task<ActivityGroupAssignResult> AssignAsync(int projectId, int activityId, int? activityGroupId)
        {
            if (activityId <= 0)
            {
                return new ActivityGroupAssignResult { Missing = true };
            }

            return await AssignCoreAsync(projectId, new[] { activityId }, activityGroupId);
        }

        private async Task<ActivityGroupAssignResult> AssignCoreAsync(int projectId, int[] ids, int? activityGroupId)
        {
            var result = new ActivityGroupAssignResult();
            _connection.Open();
            try
            {
                if (activityGroupId.HasValue)
                {
                    var locked = await LockGroupTypeAsync(projectId, ids, activityGroupId.Value);
                    if (locked.Missing)
                    {
                        result.Missing = true;
                        return result;
                    }

                    if (!string.IsNullOrEmpty(locked.Error))
                    {
                        result.Error = locked.Error;
                        return result;
                    }
                }

                using var command = new SqlCommand();
                var names = new string[ids.Length];
                for (var i = 0; i < ids.Length; i++)
                {
                    names[i] = "@A" + i;
                    command.Parameters.AddInt("A" + i, ids[i]);
                }

                command.CommandText = $@"
UPDATE projects.Activities
SET ActivityGroupId = @ActivityGroupId,
    ModifiedAt = GETDATE()
WHERE ProjectId = @ProjectId
  AND IsDeleted = 0
  AND Id IN ({string.Join(", ", names)})";
                command.Connection = _connection;
                command.Parameters.AddInt("ProjectId", projectId);
                command.Parameters.Add(new SqlParameter("@ActivityGroupId", SqlDbType.Int)
                {
                    Value = (object)activityGroupId ?? DBNull.Value,
                });
                result.Updated = await command.ExecuteNonQueryAsync();
                result.Missing = result.Updated == 0;
                return result;
            }
            finally
            {
                _connection.Close();
            }
        }

        private async Task<ActivityGroupAssignResult> LockGroupTypeAsync(int projectId, int[] ids, int groupId)
        {
            var result = new ActivityGroupAssignResult();
            int? groupType = null;
            using (var group = new SqlCommand(@"
SELECT ActivityTypeId
FROM projects.ActivityGroups
WHERE Id = @Id AND ProjectId = @ProjectId", _connection))
            {
                group.Parameters.AddInt("Id", groupId);
                group.Parameters.AddInt("ProjectId", projectId);
                using var reader = await group.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    result.Missing = true;
                    return result;
                }

                groupType = reader.ReadNullableIntValue("ActivityTypeId");
            }

            var types = new List<int?>();
            using (var activities = new SqlCommand())
            {
                var names = new string[ids.Length];
                for (var i = 0; i < ids.Length; i++)
                {
                    names[i] = "@T" + i;
                    activities.Parameters.AddInt("T" + i, ids[i]);
                }

                activities.CommandText = $@"
SELECT ActivityTypeId
FROM projects.Activities
WHERE ProjectId = @ProjectId
  AND IsDeleted = 0
  AND Id IN ({string.Join(", ", names)})";
                activities.Connection = _connection;
                activities.Parameters.AddInt("ProjectId", projectId);
                using var reader = await activities.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    types.Add(reader.ReadNullableIntValue("ActivityTypeId"));
                }
            }

            if (types.Count != ids.Length)
            {
                result.Missing = true;
                return result;
            }

            var shared = types.Where(type => type.HasValue).Select(type => type.Value).Distinct().ToList();
            if (shared.Count != 1 || types.Any(type => !type.HasValue))
            {
                result.Error = "Activities in a group must share the same type.";
                return result;
            }

            if (groupType.HasValue && groupType.Value != shared[0])
            {
                result.Error = "Activities in a group must share the same type.";
                return result;
            }

            if (!groupType.HasValue)
            {
                using var lockType = new SqlCommand(@"
UPDATE projects.ActivityGroups
SET ActivityTypeId = @TypeId
WHERE Id = @Id
  AND ProjectId = @ProjectId
  AND ActivityTypeId IS NULL", _connection);
                lockType.Parameters.AddInt("TypeId", shared[0]);
                lockType.Parameters.AddInt("Id", groupId);
                lockType.Parameters.AddInt("ProjectId", projectId);
                await lockType.ExecuteNonQueryAsync();
            }

            return result;
        }

        public override ActivityGroupEntity PopulateRecord(SqlDataReader reader)
        {
            throw new NotImplementedException();
        }

        private static ActivityGroupResult ReadGroup(SqlDataReader reader)
        {
            return new ActivityGroupResult
            {
                Id = reader.ReadIntValue("Id"),
                ProjectId = reader.ReadIntValue("ProjectId"),
                Name = reader.ReadStringValue("Name"),
                Description = reader.ReadStringValue("Description"),
                TypeId = reader.ReadNullableIntValue("ActivityTypeId"),
                CreatedAt = reader.ReadDateTimeValue("CreatedAt"),
            };
        }

        private static SqlParameter OptionalText(string name, string value)
        {
            return new SqlParameter("@" + name, SqlDbType.NVarChar, -1)
            {
                Value = (object)value ?? DBNull.Value,
            };
        }

        private static string NullIfBlank(string value)
        {
            var text = value?.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }
    }
}
