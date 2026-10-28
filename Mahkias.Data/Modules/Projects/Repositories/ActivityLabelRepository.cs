using Mahkias.Core.Helpers;
using Mahkias.Core.Modules.Projects;
using Mahkias.Core.Modules.Projects.Data.Result;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Mahkias.Data.Modules.Projects.Repositories
{
    public class ActivityLabelRepository : AdoRepository<ActivityLabelEntity>, IActivityLabelRepository
    {
        public ActivityLabelRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task<IReadOnlyList<ActivityLabelResult>> ListAsync(int projectId)
        {
            var labels = new List<ActivityLabelResult>();
            _connection.Open();
            try
            {
                await DeleteEmptyCoreAsync(projectId);
                using (var command = new SqlCommand(@"
SELECT Id, ProjectId, Name, ActivityTypeId, CreatedAt
FROM projects.ActivityLabels
WHERE ProjectId = @ProjectId
ORDER BY Name, Id", _connection))
                {
                    command.Parameters.AddInt("ProjectId", projectId);
                    using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        labels.Add(new ActivityLabelResult
                        {
                            Id = reader.ReadIntValue("Id"),
                            ProjectId = reader.ReadIntValue("ProjectId"),
                            Name = reader.ReadStringValue("Name"),
                            TypeId = reader.ReadIntValue("ActivityTypeId"),
                            CreatedAt = reader.ReadDateTimeValue("CreatedAt"),
                        });
                    }
                }

                var byId = labels.ToDictionary(label => label.Id);
                using (var activities = new SqlCommand(@"
SELECT L.ActivityLabelId, L.ActivityId
FROM projects.ActivityLabelActivities AS L
INNER JOIN projects.ActivityLabels AS AL ON AL.Id = L.ActivityLabelId
INNER JOIN projects.Activities AS A ON A.Id = L.ActivityId AND A.IsDeleted = 0
WHERE AL.ProjectId = @ProjectId", _connection))
                {
                    activities.Parameters.AddInt("ProjectId", projectId);
                    using var reader = await activities.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var labelId = reader.ReadIntValue("ActivityLabelId");
                        if (byId.TryGetValue(labelId, out var label))
                        {
                            label.ActivityIds.Add(reader.ReadIntValue("ActivityId"));
                        }
                    }
                }

                using (var groups = new SqlCommand(@"
SELECT L.ActivityLabelId, L.ActivityGroupId
FROM projects.ActivityLabelGroups AS L
INNER JOIN projects.ActivityLabels AS AL ON AL.Id = L.ActivityLabelId
INNER JOIN projects.ActivityGroups AS G ON G.Id = L.ActivityGroupId
WHERE AL.ProjectId = @ProjectId", _connection))
                {
                    groups.Parameters.AddInt("ProjectId", projectId);
                    using var reader = await groups.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var labelId = reader.ReadIntValue("ActivityLabelId");
                        if (byId.TryGetValue(labelId, out var label))
                        {
                            label.ActivityGroupIds.Add(reader.ReadIntValue("ActivityGroupId"));
                        }
                    }
                }
            }
            finally
            {
                _connection.Close();
            }

            return labels;
        }

        public async Task<ActivityGroupAssignResult> CreateAsync(int projectId, string name, int typeId)
        {
            var result = new ActivityGroupAssignResult();
            var labelName = (name ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(labelName) || typeId <= 0)
            {
                result.Error = "Label name and type are required.";
                return result;
            }

            if (labelName.Length > 200)
            {
                result.Error = "Label name must be 200 characters or fewer.";
                return result;
            }

            _connection.Open();
            try
            {
                result.Updated = await FindOrInsertLabelIdAsync(projectId, typeId, labelName);
                if (result.Updated <= 0)
                {
                    result.Error = "The label could not be created.";
                }

                return result;
            }
            finally
            {
                _connection.Close();
            }
        }

        public async Task<ActivityGroupAssignResult> AssignActivityAsync(int projectId, int activityId, int typeId, string name)
        {
            var result = new ActivityGroupAssignResult();
            var labelName = (name ?? string.Empty).Trim();
            if (activityId <= 0 || string.IsNullOrEmpty(labelName) || typeId <= 0)
            {
                result.Error = "Label name and type are required.";
                return result;
            }

            if (labelName.Length > 200)
            {
                result.Error = "Label name must be 200 characters or fewer.";
                return result;
            }

            _connection.Open();
            try
            {
                int activityType;
                using (var activity = new SqlCommand(@"
SELECT ActivityTypeId
FROM projects.Activities
WHERE Id = @ActivityId
  AND ProjectId = @ProjectId
  AND IsDeleted = 0", _connection))
                {
                    activity.Parameters.AddInt("ActivityId", activityId);
                    activity.Parameters.AddInt("ProjectId", projectId);
                    var value = await activity.ExecuteScalarAsync();
                    if (value == null || value == DBNull.Value)
                    {
                        result.Missing = true;
                        return result;
                    }

                    activityType = Convert.ToInt32(value);
                }

                if (activityType != typeId)
                {
                    result.Error = "A label can only be attached to activities of one type.";
                    return result;
                }

                var labelId = await FindOrInsertLabelIdAsync(projectId, typeId, labelName);
                if (labelId <= 0)
                {
                    result.Error = "The label could not be created.";
                    return result;
                }

                using var link = new SqlCommand(@"
IF NOT EXISTS (
    SELECT 1
    FROM projects.ActivityLabelActivities
    WHERE ActivityLabelId = @LabelId
      AND ActivityId = @ActivityId
)
INSERT INTO projects.ActivityLabelActivities (ActivityLabelId, ActivityId)
VALUES (@LabelId, @ActivityId)", _connection);
                link.Parameters.AddInt("LabelId", labelId);
                link.Parameters.AddInt("ActivityId", activityId);
                await link.ExecuteNonQueryAsync();
                result.Updated = labelId;
                return result;
            }
            finally
            {
                _connection.Close();
            }
        }

        private async Task<int> FindOrInsertLabelIdAsync(int projectId, int typeId, string labelName)
        {
            var existingId = await FindLabelIdAsync(projectId, typeId, labelName);
            if (existingId > 0)
            {
                return existingId;
            }

            try
            {
                using var command = new SqlCommand(@"
INSERT INTO projects.ActivityLabels (ProjectId, Name, ActivityTypeId)
OUTPUT INSERTED.Id
VALUES (@ProjectId, @Name, @TypeId)", _connection);
                command.Parameters.AddInt("ProjectId", projectId);
                command.Parameters.AddNVarChar("Name", labelName, 200);
                command.Parameters.AddInt("TypeId", typeId);
                return Convert.ToInt32(await command.ExecuteScalarAsync());
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                return await FindLabelIdAsync(projectId, typeId, labelName);
            }
        }

        private async Task<int> FindLabelIdAsync(int projectId, int typeId, string labelName)
        {
            using var existing = new SqlCommand(@"
SELECT TOP (1) Id
FROM projects.ActivityLabels
WHERE ProjectId = @ProjectId
  AND ActivityTypeId = @TypeId
  AND LOWER(Name) = LOWER(@Name)
ORDER BY Id", _connection);
            existing.Parameters.AddInt("ProjectId", projectId);
            existing.Parameters.AddInt("TypeId", typeId);
            existing.Parameters.AddNVarChar("Name", labelName, 200);
            var found = await existing.ExecuteScalarAsync();
            return found == null || found == DBNull.Value ? 0 : Convert.ToInt32(found);
        }

        public async Task<bool> DeleteAsync(int id, int projectId)
        {
            using var command = new SqlCommand(@"
DELETE FROM projects.ActivityLabels
WHERE Id = @Id AND ProjectId = @ProjectId", _connection);
            command.Parameters.AddInt("Id", id);
            command.Parameters.AddInt("ProjectId", projectId);
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

        public Task<ActivityGroupAssignResult> SetActivitiesAsync(int projectId, IReadOnlyList<int> activityIds, IReadOnlyList<int> labelIds)
        {
            return SetMembersAsync(projectId, activityIds, labelIds, forGroups: false);
        }

        public Task<ActivityGroupAssignResult> SetGroupsAsync(int projectId, IReadOnlyList<int> activityGroupIds, IReadOnlyList<int> labelIds)
        {
            return SetMembersAsync(projectId, activityGroupIds, labelIds, forGroups: true);
        }

        public async Task<ActivityGroupAssignResult> RemoveActivityAsync(int projectId, int activityId, int? labelId)
        {
            var result = new ActivityGroupAssignResult();
            using var command = new SqlCommand(@"
IF NOT EXISTS (
    SELECT 1
    FROM projects.Activities
    WHERE Id = @ActivityId
      AND ProjectId = @ProjectId
      AND IsDeleted = 0
)
BEGIN
    SELECT Updated = 0, Missing = 1;
    RETURN;
END

DELETE L
FROM projects.ActivityLabelActivities AS L
INNER JOIN projects.ActivityLabels AS AL ON AL.Id = L.ActivityLabelId
WHERE L.ActivityId = @ActivityId
  AND AL.ProjectId = @ProjectId
  AND (@LabelId IS NULL OR L.ActivityLabelId = @LabelId);

SELECT Updated = @@ROWCOUNT, Missing = 0;", _connection);
            command.Parameters.AddInt("ProjectId", projectId);
            command.Parameters.AddInt("ActivityId", activityId);
            command.Parameters.Add(new SqlParameter("@LabelId", SqlDbType.Int)
            {
                Value = (object)labelId ?? DBNull.Value,
            });
            _connection.Open();
            try
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        result.Updated = reader.ReadIntValue("Updated");
                        result.Missing = reader.ReadIntValue("Missing") == 1;
                    }
                }

                if (!result.Missing)
                {
                    await DeleteEmptyCoreAsync(projectId);
                }
            }
            finally
            {
                _connection.Close();
            }

            return result;
        }

        public async Task<ActivityGroupAssignResult> RemoveMembersAsync(int projectId, IReadOnlyList<int> activityIds, IReadOnlyList<int> activityGroupIds, int? labelId)
        {
            var result = new ActivityGroupAssignResult();
            var activities = (activityIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToArray();
            var groups = (activityGroupIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToArray();
            if (activities.Length == 0 && groups.Length == 0)
            {
                result.Error = "Select at least one activity or group.";
                return result;
            }

            _connection.Open();
            try
            {
                var updated = 0;
                if (activities.Length > 0)
                {
                    updated += await DeleteLinksAsync(projectId, activities, labelId, forGroups: false);
                }

                if (groups.Length > 0)
                {
                    updated += await DeleteLinksAsync(projectId, groups, labelId, forGroups: true);
                }

                await DeleteEmptyCoreAsync(projectId);
                result.Updated = updated;
                return result;
            }
            finally
            {
                _connection.Close();
            }
        }

        public async Task ClearMismatchedAsync(int projectId)
        {
            using var command = new SqlCommand(@"
DELETE L
FROM projects.ActivityLabelActivities AS L
INNER JOIN projects.ActivityLabels AS AL ON AL.Id = L.ActivityLabelId
INNER JOIN projects.Activities AS A ON A.Id = L.ActivityId
WHERE AL.ProjectId = @ProjectId
  AND (A.ActivityTypeId IS NULL OR A.ActivityTypeId <> AL.ActivityTypeId);

DELETE L
FROM projects.ActivityLabelGroups AS L
INNER JOIN projects.ActivityLabels AS AL ON AL.Id = L.ActivityLabelId
INNER JOIN projects.ActivityGroups AS G ON G.Id = L.ActivityGroupId
WHERE AL.ProjectId = @ProjectId
  AND (G.ActivityTypeId IS NULL OR G.ActivityTypeId <> AL.ActivityTypeId);", _connection);
            command.Parameters.AddInt("ProjectId", projectId);
            _connection.Open();
            try
            {
                await command.ExecuteNonQueryAsync();
                await DeleteEmptyCoreAsync(projectId);
            }
            finally
            {
                _connection.Close();
            }
        }

        public override ActivityLabelEntity PopulateRecord(SqlDataReader reader)
        {
            throw new NotImplementedException();
        }

        private async Task<ActivityGroupAssignResult> SetMembersAsync(int projectId, IReadOnlyList<int> memberIds, IReadOnlyList<int> labelIds, bool forGroups)
        {
            var result = new ActivityGroupAssignResult();
            var members = (memberIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToArray();
            var labels = (labelIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToArray();
            if (members.Length == 0)
            {
                result.Error = forGroups ? "Select at least one activity group." : "Select at least one activity.";
                return result;
            }

            _connection.Open();
            SqlTransaction transaction = null;
            try
            {
                var typeId = forGroups
                    ? await SharedGroupTypeAsync(projectId, members)
                    : await SharedActivityTypeAsync(projectId, members);
                if (typeId.Missing)
                {
                    result.Missing = true;
                    return result;
                }

                if (!string.IsNullOrEmpty(typeId.Error))
                {
                    result.Error = typeId.Error;
                    return result;
                }

                if (labels.Length > 0)
                {
                    var labelError = await ValidateLabelsAsync(projectId, typeId.Updated, labels);
                    if (!string.IsNullOrEmpty(labelError))
                    {
                        result.Error = labelError;
                        return result;
                    }
                }

                transaction = _connection.BeginTransaction();
                var memberNames = ParameterNames(members, "M");
                var deleteSql = forGroups
                    ? $@"DELETE FROM projects.ActivityLabelGroups WHERE ActivityGroupId IN ({memberNames})"
                    : $@"DELETE FROM projects.ActivityLabelActivities WHERE ActivityId IN ({memberNames})";
                using (var delete = new SqlCommand(deleteSql, _connection, transaction))
                {
                    AddIds(delete, members, "M");
                    await delete.ExecuteNonQueryAsync();
                }

                if (labels.Length > 0)
                {
                    var insertSql = forGroups
                        ? @"INSERT INTO projects.ActivityLabelGroups (ActivityLabelId, ActivityGroupId) VALUES (@LabelId, @MemberId)"
                        : @"INSERT INTO projects.ActivityLabelActivities (ActivityLabelId, ActivityId) VALUES (@LabelId, @MemberId)";
                    foreach (var labelId in labels)
                    {
                        foreach (var memberId in members)
                        {
                            using var insert = new SqlCommand(insertSql, _connection, transaction);
                            insert.Parameters.AddInt("LabelId", labelId);
                            insert.Parameters.AddInt("MemberId", memberId);
                            await insert.ExecuteNonQueryAsync();
                        }
                    }
                }

                transaction.Commit();
                transaction.Dispose();
                transaction = null;
                await DeleteEmptyCoreAsync(projectId);
                result.Updated = members.Length;
                return result;
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

        private async Task<ActivityGroupAssignResult> SharedActivityTypeAsync(int projectId, int[] ids)
        {
            var result = new ActivityGroupAssignResult();
            var types = new List<int?>();
            using var command = new SqlCommand();
            var names = ParameterNames(ids, "A");
            command.CommandText = $@"
SELECT ActivityTypeId
FROM projects.Activities
WHERE ProjectId = @ProjectId
  AND IsDeleted = 0
  AND Id IN ({names})";
            command.Connection = _connection;
            command.Parameters.AddInt("ProjectId", projectId);
            AddIds(command, ids, "A");
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                types.Add(reader.ReadNullableIntValue("ActivityTypeId"));
            }

            if (types.Count != ids.Length || types.Any(type => !type.HasValue) || types.Select(type => type.Value).Distinct().Count() != 1)
            {
                result.Error = types.Count != ids.Length
                    ? null
                    : "A label can only be attached to activities of one type.";
                result.Missing = types.Count != ids.Length;
                if (!result.Missing && string.IsNullOrEmpty(result.Error))
                {
                    result.Error = "A label can only be attached to activities of one type.";
                }

                return result;
            }

            result.Updated = types[0].Value;
            return result;
        }

        private async Task<ActivityGroupAssignResult> SharedGroupTypeAsync(int projectId, int[] ids)
        {
            var result = new ActivityGroupAssignResult();
            var types = new List<int?>();
            using var command = new SqlCommand();
            var names = ParameterNames(ids, "G");
            command.CommandText = $@"
SELECT ActivityTypeId
FROM projects.ActivityGroups
WHERE ProjectId = @ProjectId
  AND Id IN ({names})";
            command.Connection = _connection;
            command.Parameters.AddInt("ProjectId", projectId);
            AddIds(command, ids, "G");
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                types.Add(reader.ReadNullableIntValue("ActivityTypeId"));
            }

            if (types.Count != ids.Length)
            {
                result.Missing = true;
                return result;
            }

            if (types.Any(type => !type.HasValue) || types.Select(type => type.Value).Distinct().Count() != 1)
            {
                result.Error = "A label can only be attached to groups of one type.";
                return result;
            }

            result.Updated = types[0].Value;
            return result;
        }

        private async Task<string> ValidateLabelsAsync(int projectId, int typeId, int[] labelIds)
        {
            using var command = new SqlCommand();
            var names = ParameterNames(labelIds, "L");
            command.CommandText = $@"
SELECT COUNT(*)
FROM projects.ActivityLabels
WHERE ProjectId = @ProjectId
  AND ActivityTypeId = @TypeId
  AND Id IN ({names})";
            command.Connection = _connection;
            command.Parameters.AddInt("ProjectId", projectId);
            command.Parameters.AddInt("TypeId", typeId);
            AddIds(command, labelIds, "L");
            var count = Convert.ToInt32(await command.ExecuteScalarAsync());
            return count == labelIds.Length ? null : "Each label must belong to the same type as the activities or groups it covers.";
        }

        public async Task DeleteEmptyForActivityAsync(int activityId)
        {
            _connection.Open();
            try
            {
                int projectId;
                using (var lookup = new SqlCommand("SELECT ProjectId FROM projects.Activities WHERE Id = @Id", _connection))
                {
                    lookup.Parameters.AddInt("Id", activityId);
                    var value = await lookup.ExecuteScalarAsync();
                    if (value == null || value == DBNull.Value)
                    {
                        return;
                    }

                    projectId = Convert.ToInt32(value);
                }

                await DeleteEmptyCoreAsync(projectId);
            }
            finally
            {
                _connection.Close();
            }
        }

        public async Task DeleteEmptyAsync(int projectId)
        {
            _connection.Open();
            try
            {
                await DeleteEmptyCoreAsync(projectId);
            }
            finally
            {
                _connection.Close();
            }
        }

        private async Task DeleteEmptyCoreAsync(int projectId)
        {
            using var command = new SqlCommand(@"
DELETE AL
FROM projects.ActivityLabels AS AL
WHERE AL.ProjectId = @ProjectId
  AND NOT EXISTS (
        SELECT 1
        FROM projects.ActivityLabelActivities AS X
        INNER JOIN projects.Activities AS A ON A.Id = X.ActivityId AND A.IsDeleted = 0
        WHERE X.ActivityLabelId = AL.Id)
  AND NOT EXISTS (
        SELECT 1
        FROM projects.ActivityLabelGroups AS G
        WHERE G.ActivityLabelId = AL.Id);", _connection);
            command.Parameters.AddInt("ProjectId", projectId);
            await command.ExecuteNonQueryAsync();
        }

        private async Task<int> DeleteLinksAsync(int projectId, int[] ids, int? labelId, bool forGroups)
        {
            using var command = new SqlCommand();
            var names = ParameterNames(ids, "R");
            command.CommandText = forGroups
                ? $@"DELETE L
FROM projects.ActivityLabelGroups AS L
INNER JOIN projects.ActivityLabels AS AL ON AL.Id = L.ActivityLabelId
WHERE AL.ProjectId = @ProjectId
  AND L.ActivityGroupId IN ({names})
  AND (@LabelId IS NULL OR L.ActivityLabelId = @LabelId)"
                : $@"DELETE L
FROM projects.ActivityLabelActivities AS L
INNER JOIN projects.ActivityLabels AS AL ON AL.Id = L.ActivityLabelId
WHERE AL.ProjectId = @ProjectId
  AND L.ActivityId IN ({names})
  AND (@LabelId IS NULL OR L.ActivityLabelId = @LabelId)";
            command.Connection = _connection;
            command.Parameters.AddInt("ProjectId", projectId);
            command.Parameters.Add(new SqlParameter("@LabelId", SqlDbType.Int)
            {
                Value = (object)labelId ?? DBNull.Value,
            });
            AddIds(command, ids, "R");
            return await command.ExecuteNonQueryAsync();
        }

        private static string ParameterNames(int[] ids, string prefix)
        {
            return string.Join(", ", ids.Select((_, index) => "@" + prefix + index));
        }

        private static void AddIds(SqlCommand command, int[] ids, string prefix)
        {
            for (var i = 0; i < ids.Length; i++)
            {
                command.Parameters.AddInt(prefix + i, ids[i]);
            }
        }
    }
}
