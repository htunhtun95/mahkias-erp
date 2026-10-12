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

        public async Task<ProjectDocumentResult> AddAsync(CreateProjectDocumentArgs args)
        {
            using var command = new SqlCommand("dbo.CreateProjectDocument");
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddInt("ProjectId", args.ProjectId);
            command.Parameters.AddNVarChar("FileName", args.FileName, 260, false);
            command.Parameters.AddNVarChar("FileType", args.FileType, 100, false);
            command.Parameters.AddNVarChar("GoogleDriveFileId", args.GoogleDriveFileId, 128, false);
            command.Parameters.AddNVarChar("WebViewLink", args.WebViewLink, 1000, false);
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

        public async Task<QuotationDocumentResult> AddQuotationDocumentAsync(CreateQuotationDocumentArgs args)
        {
            using var command = new SqlCommand("dbo.CreateQuotationDocument");
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddInt("ProjectId", args.ProjectId);
            command.Parameters.AddInt("QuotationId", args.QuotationId);
            command.Parameters.AddNVarChar("FileName", args.FileName, 260, false);
            command.Parameters.AddNVarChar("FileType", args.FileType, 100, false);
            command.Parameters.AddNVarChar("GoogleDriveFileId", args.GoogleDriveFileId, 128, false);
            command.Parameters.AddNVarChar("WebViewLink", args.WebViewLink, 1000, false);
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
            command.Parameters.AddNVarChar("GoogleDriveFileId", args.GoogleDriveFileId, 128, false);
            command.Parameters.AddNVarChar("WebViewLink", args.WebViewLink, 1000, false);
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
            };
        }
    }
}
