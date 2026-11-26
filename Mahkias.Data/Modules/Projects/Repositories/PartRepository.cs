using System.Data;
using System.Text.Json;
using Mahkias.Core.Helpers;
using Mahkias.Core.Modules.Projects;
using Microsoft.Data.SqlClient;

namespace Mahkias.Data.Modules.Projects.Repositories
{
    public class PartRepository : AdoRepository<PartSuggestion>, IPartRepository
    {
        public PartRepository(string connectionString) : base(connectionString)
        {
        }

        public override PartSuggestion PopulateRecord(SqlDataReader reader)
        {
            return new PartSuggestion
            {
                PartId = reader.ReadIntValue("PartId"),
                PartNo = reader.ReadStringValue("PartNo"),
                Description = reader.ReadStringValue("Description"),
            };
        }

        public async Task<IReadOnlyList<PartSuggestion>> SearchAsync(string query)
        {
            var matches = new List<PartSuggestion>();
            if (string.IsNullOrWhiteSpace(query))
            {
                return matches;
            }

            using (SqlCommand command = new SqlCommand("dbo.SearchParts"))
            {
                command.CommandType = System.Data.CommandType.StoredProcedure;
                command.Parameters.AddNVarChar("Query", query.Trim(), 200, false);
                command.Connection = _connection;
                _connection.Open();
                try
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (reader.Read())
                        {
                            matches.Add(PopulateRecord(reader));
                        }
                    }
                }
                finally
                {
                    _connection.Close();
                }
            }

            return matches;
        }

        public async Task<IReadOnlyList<PartDetail>> ListAsync(string search)
        {
            var parts = new List<PartDetail>();
            using (SqlCommand command = new SqlCommand("dbo.ListParts"))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddNVarChar("Search", string.IsNullOrWhiteSpace(search) ? null : search.Trim(), 200, false);
                command.Connection = _connection;
                _connection.Open();
                try
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (reader.Read())
                        {
                            parts.Add(ReadPart(reader));
                        }
                    }
                }
                finally
                {
                    _connection.Close();
                }
            }

            return parts;
        }

        public async Task<PartDetail> GetAsync(int id)
        {
            using (SqlCommand command = new SqlCommand("dbo.GetPart"))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddInt("Id", id);
                command.Connection = _connection;
                _connection.Open();
                try
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return ReadPart(reader);
                        }
                    }
                }
                finally
                {
                    _connection.Close();
                }
            }

            return null;
        }

        public Task<PartSaveResult> CreateAsync(string partNo, IReadOnlyList<PartDescriptionWrite> descriptions)
        {
            return SaveAsync("dbo.CreatePart", null, partNo, descriptions);
        }

        public Task<PartSaveResult> UpdateAsync(int id, string partNo, IReadOnlyList<PartDescriptionWrite> descriptions)
        {
            return SaveAsync("dbo.UpdatePart", id, partNo, descriptions);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using (SqlCommand command = new SqlCommand("dbo.DeletePart"))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddInt("Id", id);
                command.Connection = _connection;
                _connection.Open();
                try
                {
                    var deleted = await command.ExecuteScalarAsync();
                    return deleted != null && deleted != DBNull.Value && Convert.ToInt32(deleted) > 0;
                }
                finally
                {
                    _connection.Close();
                }
            }
        }

        public async Task<IReadOnlyList<PartAlternate>> ListAlternatesAsync(int partId)
        {
            var result = await ReadAlternatesAsync("dbo.ListPartAlternates", partId, null, null, null, null);
            return result.Alternates;
        }

        public Task<PartAlternateSaveResult> SaveAlternateAsync(int partId, int? linkId, string partNo, string description, int? selectedPartId)
        {
            return ReadAlternatesAsync("dbo.SavePartAlternate", partId, linkId, partNo, description, selectedPartId);
        }

        public async Task<bool> RemoveAlternateAsync(int partId, int linkId)
        {
            using (SqlCommand command = new SqlCommand("dbo.RemovePartAlternate"))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddInt("PartId", partId);
                command.Parameters.AddInt("LinkId", linkId);
                command.Connection = _connection;
                _connection.Open();
                try
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        return reader.Read() && reader.ReadIntValue("Deleted") > 0;
                    }
                }
                finally
                {
                    _connection.Close();
                }
            }
        }

        public async Task<IReadOnlyList<PartQuotationLine>> ListQuotationsAsync(int partId)
        {
            var lines = new List<PartQuotationLine>();
            using (SqlCommand command = new SqlCommand("dbo.ListPartQuotations"))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddInt("PartId", partId);
                command.Connection = _connection;
                _connection.Open();
                try
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (reader.Read())
                        {
                            lines.Add(new PartQuotationLine
                            {
                                QuotationItemId = reader.ReadIntValue("QuotationItemId"),
                                QuotationId = reader.ReadIntValue("QuotationId"),
                                Code = reader.ReadStringValue("Code"),
                                SupplierId = reader.ReadIntValue("SupplierId"),
                                SupplierName = reader.ReadStringValue("SupplierName"),
                                SupplierReference = reader.ReadNullableStringValue("SupplierReference"),
                                PartNo = reader.ReadStringValue("PartNo"),
                                Quantity = reader.ReadIntValue("Quantity"),
                                Unit = reader.ReadNullableStringValue("Unit"),
                                Condition = reader.ReadNullableStringValue("Condition"),
                                UnitPrice = reader.ReadNullableDecimalValue("UnitPrice"),
                                TotalPrice = reader.ReadNullableDecimalValue("TotalPrice"),
                                CurrencyCode = reader.ReadNullableStringValue("CurrencyCode"),
                                CurrencySymbol = reader.ReadNullableStringValue("CurrencySymbol"),
                                QuotedDate = reader.ReadNullableDateTimeValue("QuotedDate"),
                                ValidUntil = reader.ReadNullableDateTimeValue("ValidUntil"),
                                Match = reader.ReadStringValue("Match"),
                                Projects = ReadProjects(reader.ReadNullableStringValue("Projects")),
                            });
                        }
                    }
                }
                finally
                {
                    _connection.Close();
                }
            }

            return lines;
        }

        private async Task<PartAlternateSaveResult> ReadAlternatesAsync(
            string procedure,
            int partId,
            int? linkId,
            string partNo,
            string description,
            int? selectedPartId)
        {
            var result = new PartAlternateSaveResult();
            using (SqlCommand command = new SqlCommand(procedure))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddInt("PartId", partId);
                if (procedure.EndsWith("SavePartAlternate", StringComparison.Ordinal))
                {
                    command.Parameters.Add("@LinkId", SqlDbType.Int).Value = (object)linkId ?? DBNull.Value;
                    command.Parameters.AddNVarCharMax("PartNo", partNo ?? string.Empty, false);
                    command.Parameters.AddNVarCharMax("Description", description, true);
                    command.Parameters.Add("@SelectedPartId", SqlDbType.Int).Value = (object)selectedPartId ?? DBNull.Value;
                }

                command.Connection = _connection;
                _connection.Open();
                try
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        var alternates = new List<PartAlternate>();
                        while (reader.Read())
                        {
                            var message = reader.ReadNullableStringValue("Message");
                            if (!string.IsNullOrWhiteSpace(message))
                            {
                                result.Error = message;
                                return result;
                            }

                            if (reader.IsDBNull(reader.GetOrdinal("Id")))
                            {
                                continue;
                            }

                            alternates.Add(new PartAlternate
                            {
                                Id = reader.ReadIntValue("Id"),
                                PartId = reader.ReadIntValue("PartId"),
                                PartNo = reader.ReadStringValue("PartNo"),
                                Descriptions = ReadDescriptions(reader.ReadNullableStringValue("Descriptions")),
                            });
                        }

                        result.Alternates = alternates;
                    }
                }
                finally
                {
                    _connection.Close();
                }
            }

            return result;
        }

        private static List<PartProjectLink> ReadProjects(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<PartProjectLink>();
            }

            return JsonSerializer.Deserialize<List<PartProjectLink>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }) ?? new List<PartProjectLink>();
        }

        private async Task<PartSaveResult> SaveAsync(string procedure, int? id, string partNo, IReadOnlyList<PartDescriptionWrite> descriptions)
        {
            using (SqlCommand command = new SqlCommand(procedure))
            {
                command.CommandType = CommandType.StoredProcedure;
                if (id.HasValue)
                {
                    command.Parameters.AddInt("Id", id.Value);
                }
                command.Parameters.AddNVarCharMax("PartNo", partNo, false);
                command.Parameters.AddNVarCharMax("Descriptions", DescriptionsJson(descriptions), false);
                command.Connection = _connection;
                _connection.Open();
                try
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                        {
                            return new PartSaveResult { Error = "The part could not be saved." };
                        }

                        var message = reader.ReadStringValue("Message");
                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            return new PartSaveResult { Error = message };
                        }

                        if (reader.IsDBNull(reader.GetOrdinal("Id")))
                        {
                            return new PartSaveResult { Error = "The part could not be saved." };
                        }

                        return new PartSaveResult { Part = ReadPart(reader) };
                    }
                }
                finally
                {
                    _connection.Close();
                }
            }
        }

        private static PartDetail ReadPart(SqlDataReader reader)
        {
            return new PartDetail
            {
                Id = reader.ReadIntValue("Id"),
                PartNo = reader.ReadStringValue("PartNo"),
                CreatedAt = reader.ReadDateTimeValue("CreatedAt"),
                Descriptions = ReadDescriptions(reader.ReadStringValue("Descriptions")),
            };
        }

        private static List<PartDescriptionItem> ReadDescriptions(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<PartDescriptionItem>();
            }

            return JsonSerializer.Deserialize<List<PartDescriptionItem>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }) ?? new List<PartDescriptionItem>();
        }

        private static string DescriptionsJson(IReadOnlyList<PartDescriptionWrite> descriptions)
        {
            var rows = (descriptions ?? Array.Empty<PartDescriptionWrite>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Description))
                .Select(item => new { id = item.Id, description = item.Description.Trim() });
            return JsonSerializer.Serialize(rows);
        }
    }
}
