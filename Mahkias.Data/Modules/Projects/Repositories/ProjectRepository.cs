using Mahkias.Core.Helpers;
using Mahkias.Core.Modules.Projects;
using Mahkias.Core.Modules.Projects.Data;
using Mahkias.Core.Modules.Projects.Data.Args;
using Mahkias.Core.Modules.Projects.Data.Result;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Mahkias.Data.Modules.Projects.Repositories
{
    public class ProjectRepository : AdoRepository<Project>, IProjectRepository
    {
        public ProjectRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task<SearchProjectResult> SearchAsync(SearchProjectArgs args)
        {
            var result = new SearchProjectResult { };
            var projects = new List<SearchProject_ProjectResult>();
            var pageNumber = args.PageNumber < 1 ? 1 : args.PageNumber;
            var pageSize = args.PageSize < 1 ? 50 : args.PageSize;
            var searchTerm = string.IsNullOrWhiteSpace(args.SearchTerm) ? args.Keywords : args.SearchTerm;

            using (SqlCommand command = new SqlCommand("dbo.SearchProjects"))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddNVarChar("SearchTerm", searchTerm, 200);
                command.Parameters.AddInt("PageNumber", pageNumber);
                command.Parameters.AddInt("PageSize", pageSize);
                if (!string.IsNullOrWhiteSpace(args.SortBy))
                {
                    command.Parameters.AddNVarChar("SortBy", args.SortBy.Trim(), 50, false);
                }
                if (!string.IsNullOrWhiteSpace(args.SortDirection))
                {
                    command.Parameters.AddNVarChar("SortDirection", args.SortDirection.Trim(), 4, false);
                }
                command.Connection = _connection;
                _connection.Open();

                try
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (reader.Read())
                        {
                            projects.Add(new SearchProject_ProjectResult
                            {
                                Id = reader.ReadIntValue("Id"),
                                Name = reader.ReadStringValue("Name"),
                                Reference = reader.ReadStringValue("Reference"),
                                Description = reader.ReadStringValue("Description"),
                                CreatedAt = reader.ReadDateTimeValue("CreatedAt"),
                                ModifiedAt = reader.ReadNullableDateTimeValue("ModifiedAt"),
                            });
                        }

                        reader.NextResult();
                        if (reader.Read())
                        {
                            result.TotalResults = reader.ReadIntValue("Total_Results");
                        }
                    }
                }
                finally
                {
                    _connection.Close();
                }
            }

            result.Items = projects;
            return result;
        }

        public async Task<GetProjectResult> GetAsync(int id)
        {
            var result = new GetProjectResult();
            using (SqlCommand command = new SqlCommand("GetProject"))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddInt("Id", id);
                command.Connection = _connection;
                _connection.Open();

                try
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (reader.Read())
                        {
                            result.Id = reader.ReadIntValue("Id");
                            result.Reference = reader.ReadStringValue("Reference");
                            result.Name = reader.ReadStringValue("Name");
                            result.Description = reader.ReadStringValue("Description");
                            result.FiscalYear = reader.ReadIntValue("FiscalYear");
                            result.Deadline = reader.ReadNullableDateTimeValue("Deadline");
                            result.ProjectStatusId = reader.ReadIntValue("ProjectStatusId");
                            result.ProjectStatus = reader.ReadStringValue("ProjectStatus");
                            result.ProjectStatusSlug = reader.ReadStringValue("ProjectStatusSlug");
                            result.ProjectTypeId = reader.ReadIntValue("ProjectTypeId");
                            result.ProjectType = reader.ReadStringValue("ProjectType");
                        }
                    }
                }
                finally
                {
                    _connection.Close();
                }
            }
            return result;
        }

        public async Task<int> AddAsync(UpsertProjectArgs args)
        {
            using (SqlCommand command = new SqlCommand("dbo.CreateProject"))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddNVarChar("Name", args.Name, 250);
                command.Parameters.AddNVarChar("Reference", args.Reference, 100);
                command.Parameters.AddNVarCharMax("Description", args.Description);
                command.Connection = _connection;
                _connection.Open();
                try
                {
                    var id = await command.ExecuteScalarAsync();
                    return Convert.ToInt32(id);
                }
                finally
                {
                    _connection.Close();
                }
            }
        }

        public async Task<int> UpdateAsync(int id, UpsertProjectArgs args)
        {
            using (SqlCommand command = new SqlCommand("dbo.UpdateProject"))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddInt("Id", id);
                command.Parameters.AddNVarChar("Name", args.Name, 250);
                command.Parameters.AddNVarChar("Reference", args.Reference, 100);
                command.Parameters.AddNVarCharMax("Description", args.Description);
                command.Connection = _connection;
                _connection.Open();

                try
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (!reader.Read())
                        {
                            return 0;
                        }

                        return reader.ReadIntValue("StatusCode");
                    }
                }
                finally
                {
                    _connection.Close();
                }
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var result = new SqlExecutionResult();
            using (SqlCommand command = new SqlCommand("dbo.DeleteProject").WithSuccessParameter())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddInt("Id", id);

                command.Connection = _connection;
                _connection.Open();
                try
                {
                    result = await command.ExecuteNonQueryWithResultAsync();
                }
                finally
                {
                    _connection.Close();
                }
            }

            return result.Successful;
        }

        public override Project PopulateRecord(SqlDataReader reader)
        {
            throw new NotImplementedException();
        }
    }
}
