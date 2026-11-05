using Mahkias.Core.Modules.Projects;
using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Core.Modules.Projects.Data.Result;
using Microsoft.Data.SqlClient;

namespace Mahkias.Data.Modules.Projects.Repositories
{
    public class ProjectDocumentRepository : AdoRepository<ProjectDocument>, IProjectDocumentRepository
    {
        public ProjectDocumentRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task<ProjectDriveFolderResult> GetDriveFolderAsync(int projectId)
        {
            using var command = new SqlCommand("dbo.GetProjectDriveFolder");
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddInt("ProjectId", projectId);
            command.Connection = _connection;
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return new ProjectDriveFolderResult();
                }

                return new ProjectDriveFolderResult
                {
                    Exists = true,
                    Name = reader.ReadStringValue("Name"),
                    Reference = reader.ReadNullableStringValue("Reference"),
                    GoogleDriveFolderId = reader.ReadNullableStringValue("GoogleDriveFolderId"),
                    ProcurementFolderId = reader.ReadNullableStringValue("ProcurementFolderId"),
                    DocumentsFolderId = reader.ReadNullableStringValue("DocumentsFolderId"),
                };
            }
            finally
            {
                _connection.Close();
            }
        }

        public async Task<string> SetDriveFolderAsync(int projectId, string folderId)
        {
            using var command = new SqlCommand("dbo.SetProjectDriveFolder");
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddInt("ProjectId", projectId);
            command.Parameters.AddNVarChar("GoogleDriveFolderId", folderId, 128, false);
            command.Connection = _connection;
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return null;
                }

                return reader.ReadNullableStringValue("GoogleDriveFolderId");
            }
            finally
            {
                _connection.Close();
            }
        }

        public async Task<string> SetProcurementFolderAsync(int projectId, string folderId)
        {
            using var command = new SqlCommand("dbo.SetProjectProcurementFolder");
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddInt("ProjectId", projectId);
            command.Parameters.AddNVarChar("ProcurementFolderId", folderId, 128, false);
            command.Connection = _connection;
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return null;
                }

                return reader.ReadNullableStringValue("ProcurementFolderId");
            }
            finally
            {
                _connection.Close();
            }
        }

        public async Task<string> SetDocumentsFolderAsync(int projectId, string folderId)
        {
            using var command = new SqlCommand("dbo.SetProjectDocumentsFolder");
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddInt("ProjectId", projectId);
            command.Parameters.AddNVarChar("DocumentsFolderId", folderId, 128, false);
            command.Connection = _connection;
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return null;
                }

                return reader.ReadNullableStringValue("DocumentsFolderId");
            }
            finally
            {
                _connection.Close();
            }
        }

        public async Task<ProjectDocumentResult> AddAsync(CreateProjectDocumentArgs args)
        {
            using var command = new SqlCommand("dbo.CreateProjectDocument");
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddInt("ProjectId", args.ProjectId);
            command.Parameters.AddNVarChar("FileName", args.FileName, 260, false);
            command.Parameters.AddNVarChar("FileType", args.FileType, 100, false);
            command.Parameters.AddNVarChar("GoogleDriveFileId", args.GoogleDriveFileId, 128, false);
            command.Parameters.AddNVarChar("WebViewLink", args.WebViewLink, 1000, false);
            command.Parameters.AddBigInt("FileSize", args.FileSize);
            command.Parameters.AddBit("IsOriginal", args.IsOriginal);
            command.Parameters.AddNullableInt("SortOrder", args.SortOrder);
            command.Connection = _connection;
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return null;
                }

                return ReadDocument(reader);
            }
            finally
            {
                _connection.Close();
            }
        }

        public async Task<IReadOnlyList<ProjectDocumentResult>> ListByProjectAsync(int projectId)
        {
            var documents = new List<ProjectDocumentResult>();
            using var command = new SqlCommand("dbo.ListProjectDocuments");
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddInt("ProjectId", projectId);
            command.Connection = _connection;
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    documents.Add(ReadDocument(reader));
                }
            }
            finally
            {
                _connection.Close();
            }

            return documents;
        }

        public async Task<bool> RemoveAsync(int projectId, int documentId)
        {
            using var command = new SqlCommand("dbo.DeleteProjectDocument");
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddInt("ProjectId", projectId);
            command.Parameters.AddInt("DocumentId", documentId);
            command.Connection = _connection;
            _connection.Open();
            try
            {
                var deleted = await command.ExecuteScalarAsync();
                return Convert.ToInt32(deleted ?? 0) > 0;
            }
            finally
            {
                _connection.Close();
            }
        }

        public async Task OrderAsync(int projectId, IReadOnlyList<DocumentOrderItem> documents, int? originalDocumentId)
        {
            await _connection.OpenAsync();
            using var transaction = _connection.BeginTransaction();
            try
            {
                foreach (var item in documents ?? Array.Empty<DocumentOrderItem>())
                {
                    using var command = new SqlCommand(@"
UPDATE A
SET A.sort_order = @SortOrder
FROM media.assets AS A
INNER JOIN projects.ProjectDocuments AS D ON D.AssetId = A.id
INNER JOIN projects.Projects AS P ON P.Id = D.ProjectId
WHERE D.DocumentId = @DocumentId
  AND D.ProjectId = @ProjectId
  AND P.IsDeleted = 0;", _connection, transaction);
                    command.Parameters.AddInt("ProjectId", projectId);
                    command.Parameters.AddInt("DocumentId", item.DocumentId);
                    command.Parameters.AddInt("SortOrder", item.SortOrder);
                    await command.ExecuteNonQueryAsync();
                }

                if (originalDocumentId is > 0)
                {
                    using var mark = new SqlCommand(@"
UPDATE D
SET D.IsOriginal = CASE WHEN D.DocumentId = @OriginalDocumentId THEN 1 ELSE 0 END
FROM projects.ProjectDocuments AS D
INNER JOIN projects.Projects AS P ON P.Id = D.ProjectId
WHERE D.ProjectId = @ProjectId
  AND P.IsDeleted = 0;", _connection, transaction);
                    mark.Parameters.AddInt("ProjectId", projectId);
                    mark.Parameters.AddInt("OriginalDocumentId", originalDocumentId.Value);
                    await mark.ExecuteNonQueryAsync();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            finally
            {
                _connection.Close();
            }
        }

        public async Task<QuotationDocumentResult> AddQuotationDocumentAsync(CreateQuotationDocumentArgs args)
        {
            using var command = new SqlCommand("dbo.CreateQuotationDocument");
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddInt("ProjectId", args.ProjectId);
            command.Parameters.AddInt("QuotationId", args.QuotationId);
            command.Parameters.AddNVarChar("FileName", args.FileName, 260, false);
            command.Parameters.AddNVarChar("FileType", args.FileType, 100, false);
            BindAsset(command, args);
            command.Connection = _connection;
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return null;
                }

                return ReadQuotationDocument(reader);
            }
            finally
            {
                _connection.Close();
            }
        }

        public async Task<IReadOnlyList<QuotationDocumentResult>> ListByQuotationAsync(int projectId, int quotationId)
        {
            var documents = new List<QuotationDocumentResult>();
            using var command = new SqlCommand("dbo.ListQuotationDocuments");
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddInt("ProjectId", projectId);
            command.Parameters.AddInt("QuotationId", quotationId);
            command.Connection = _connection;
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    documents.Add(ReadQuotationDocument(reader));
                }
            }
            finally
            {
                _connection.Close();
            }

            return documents;
        }

        public async Task<QuotationDocumentResult> UpdateQuotationDocumentAsync(UpdateQuotationDocumentArgs args)
        {
            using var command = new SqlCommand("dbo.UpdateQuotationDocument");
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddInt("ProjectId", args.ProjectId);
            command.Parameters.AddInt("QuotationId", args.QuotationId);
            command.Parameters.AddInt("DocumentId", args.DocumentId);
            command.Parameters.AddNVarChar("FileName", args.FileName, 260, false);
            command.Parameters.AddNVarChar("FileType", args.FileType, 100, false);
            BindAsset(command, args);
            command.Connection = _connection;
            _connection.Open();
            try
            {
                using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return null;
                }

                return ReadQuotationDocument(reader);
            }
            catch (SqlException)
            {
                return null;
            }
            finally
            {
                _connection.Close();
            }
        }

        public async Task<bool> RemoveQuotationDocumentAsync(int projectId, int quotationId, int documentId)
        {
            using var command = new SqlCommand("dbo.DeleteQuotationDocument");
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddInt("ProjectId", projectId);
            command.Parameters.AddInt("QuotationId", quotationId);
            command.Parameters.AddInt("DocumentId", documentId);
            command.Connection = _connection;
            _connection.Open();
            try
            {
                var deleted = await command.ExecuteScalarAsync();
                return deleted != null && deleted != DBNull.Value && Convert.ToInt32(deleted) > 0;
            }
            catch (SqlException)
            {
                return false;
            }
            finally
            {
                _connection.Close();
            }
        }

        public override ProjectDocument PopulateRecord(SqlDataReader reader)
        {
            throw new NotImplementedException();
        }

        private static ProjectDocumentResult ReadDocument(SqlDataReader reader)
        {
            return new ProjectDocumentResult
            {
                DocumentId = reader.ReadIntValue("DocumentId"),
                ProjectId = reader.ReadIntValue("ProjectId"),
                FileName = reader.ReadStringValue("FileName"),
                FileType = reader.ReadStringValue("FileType"),
                GoogleDriveFileId = reader.ReadStringValue("GoogleDriveFileId"),
                WebViewLink = reader.ReadStringValue("WebViewLink"),
                UploadedAt = reader.ReadDateTimeValue("UploadedAt"),
                AssetId = reader.ReadGuidValue("AssetId"),
                FileSize = reader["FileSize"] == DBNull.Value ? 0 : Convert.ToInt64(reader["FileSize"]),
                SortOrder = reader.ReadIntValue("SortOrder"),
                IsOriginal = reader.ReadBooleanValue("IsOriginal"),
            };
        }

        private static QuotationDocumentResult ReadQuotationDocument(SqlDataReader reader)
        {
            return new QuotationDocumentResult
            {
                DocumentId = reader.ReadIntValue("DocumentId"),
                QuotationId = reader.ReadIntValue("QuotationId"),
                FileName = reader.ReadStringValue("FileName"),
                FileType = reader.ReadStringValue("FileType"),
                GoogleDriveFileId = reader.ReadStringValue("GoogleDriveFileId"),
                WebViewLink = reader.ReadStringValue("WebViewLink"),
                UploadedAt = reader.ReadDateTimeValue("UploadedAt"),
                AssetId = reader.ReadGuidValue("AssetId"),
                FileSize = reader["FileSize"] == DBNull.Value ? 0 : Convert.ToInt64(reader["FileSize"]),
                SortOrder = reader.ReadIntValue("SortOrder"),
                IsOriginal = reader.ReadBooleanValue("IsOriginal"),
            };
        }

        private static void BindAsset(SqlCommand command, CreateQuotationDocumentArgs args)
        {
            command.Parameters.AddNVarChar("GoogleDriveFileId", args.GoogleDriveFileId, 128, false);
            command.Parameters.AddNVarChar("WebViewLink", args.WebViewLink, 1000, false);
            command.Parameters.AddBigInt("FileSize", args.FileSize);
            command.Parameters.AddBit("IsOriginal", args.IsOriginal);
            command.Parameters.AddNullableInt("SortOrder", args.SortOrder);
        }

        public async Task OrderQuotationDocumentsAsync(int projectId, int quotationId, IReadOnlyList<DocumentOrderItem> documents, int? originalDocumentId)
        {
            await _connection.OpenAsync();
            using var transaction = _connection.BeginTransaction();
            try
            {
                foreach (var item in documents ?? Array.Empty<DocumentOrderItem>())
                {
                    using var command = new SqlCommand(@"
UPDATE A
SET A.sort_order = @SortOrder
FROM media.assets AS A
INNER JOIN projects.QuotationDocuments AS D ON D.AssetId = A.id
INNER JOIN projects.Quotations AS Q ON Q.Id = D.QuotationId
INNER JOIN projects.QuotationProjects AS QP ON QP.QuotationId = Q.Id
WHERE D.DocumentId = @DocumentId
  AND D.QuotationId = @QuotationId
  AND QP.ProjectId = @ProjectId
  AND Q.IsDeleted = 0;", _connection, transaction);
                    command.Parameters.AddInt("ProjectId", projectId);
                    command.Parameters.AddInt("QuotationId", quotationId);
                    command.Parameters.AddInt("DocumentId", item.DocumentId);
                    command.Parameters.AddInt("SortOrder", item.SortOrder);
                    await command.ExecuteNonQueryAsync();
                }

                if (originalDocumentId is > 0)
                {
                    using var mark = new SqlCommand(@"
UPDATE D
SET D.IsOriginal = CASE WHEN D.DocumentId = @OriginalDocumentId THEN 1 ELSE 0 END
FROM projects.QuotationDocuments AS D
INNER JOIN projects.Quotations AS Q ON Q.Id = D.QuotationId
INNER JOIN projects.QuotationProjects AS QP ON QP.QuotationId = Q.Id
WHERE D.QuotationId = @QuotationId
  AND QP.ProjectId = @ProjectId
  AND Q.IsDeleted = 0;

UPDATE Q
SET Q.DriveFileLink = A.file_url
FROM projects.Quotations AS Q
INNER JOIN projects.QuotationDocuments AS D ON D.QuotationId = Q.Id AND D.DocumentId = @OriginalDocumentId
INNER JOIN media.assets AS A ON A.id = D.AssetId
WHERE Q.Id = @QuotationId;", _connection, transaction);
                    mark.Parameters.AddInt("ProjectId", projectId);
                    mark.Parameters.AddInt("QuotationId", quotationId);
                    mark.Parameters.AddInt("OriginalDocumentId", originalDocumentId.Value);
                    await mark.ExecuteNonQueryAsync();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            finally
            {
                _connection.Close();
            }
        }
    }
}
