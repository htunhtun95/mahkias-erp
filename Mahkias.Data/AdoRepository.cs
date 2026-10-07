using Mahkias.Core;
using Mahkias.Core.Attributes;
using Mahkias.Core.Data;
using Mahkias.Core.DataTableTypes;
using Mahkias.Core.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Mahkias.Data
{
    public abstract class AdoRepository<T> : AdoRepository, IRepository<T> where T : class
    {
        public AdoRepository(string connectionString) : base(connectionString)
        {
        }

        public abstract T PopulateRecord(SqlDataReader reader);

        protected async Task ReadMultipleSets(SqlCommand command, IEnumerable<Action<SqlDataReader>> setActions)
        {
            var list = new List<T>();

            command.Connection = _connection;
            command.CommandType = CommandType.StoredProcedure;
            _connection.Open();
            try
            {
                var reader = await command.ExecuteReaderAsync();

                try
                {
                    foreach (var item in setActions)
                    {
                        item(reader);

                        reader.NextResult();
                    }
                }
                finally
                {
                    reader.Close();
                }
            }
            finally
            {
                _connection.Close();
            }
        }

        protected async Task<T> GetRecordAsync(SqlCommand command)
        {
            T record = null;
            command.Connection = _connection;
            command.CommandType = CommandType.StoredProcedure;
            _connection.Open();
            try
            {
                var reader = await command.ExecuteReaderAsync();
                try
                {
                    while (reader.Read())
                    {
                        record = PopulateRecord(reader);
                        break;
                    }
                }
                finally
                {
                    // Always call Close when done reading.
                    reader.Close();
                }
            }
            finally
            {
                _connection.Close();
            }
            return record;
        }

        protected async Task<T> GetRecordCustomAsync(SqlCommand command, Action<SqlDataReader> configure)
        {
            T record = null;
            command.Connection = _connection;
            command.CommandType = CommandType.StoredProcedure;
            _connection.Open();
            try
            {
                var reader = await command.ExecuteReaderAsync();
                try
                {
                    while (reader.Read())
                    {
                        configure(reader);
                        break;
                    }
                }
                finally
                {
                    // Always call Close when done reading.
                    reader.Close();
                }
            }
            finally
            {
                _connection.Close();
            }
            return record;
        }

        protected async Task<IEnumerable<T>> GetRecordsAsync(SqlCommand command)
        {
            var list = new List<T>();
            command.Connection = _connection;
            command.CommandType = CommandType.StoredProcedure;
            _connection.Open();
            try
            {
                var reader = await command.ExecuteReaderAsync();
                try
                {
                    while (reader.Read())
                    {
                        var record = PopulateRecord(reader);
                        if (record != null) list.Add(record);
                    }
                }
                finally
                {
                    // Always call Close when done reading.
                    reader.Close();
                }
            }
            finally
            {
                _connection.Close();
            }
            return list;
        }

        protected async Task ExecuteNonQueryAsync(SqlCommand command)
        {
            command.Connection = _connection;
            command.CommandType = CommandType.StoredProcedure;
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

        public virtual Task<T> GetByIdAsync(string id)
        {
            var command = new SqlCommand($"Get{typeof(T).Name}ById");
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.AddWithValue("Id", id);

            return GetRecordAsync(command);
        }

        public virtual Task AddAsync(T entity)
        {
            var command = new SqlCommand($"Add{typeof(T).Name}");
            command.CommandType = CommandType.StoredProcedure;

            foreach (PropertyInfo prop in typeof(T).GetProperties())
            {
                command.Parameters.AddWithValue($"{prop.Name}", prop.GetValue(entity, null));
            }

            return ExecuteNonQueryAsync(command);
        }

        public Task Update(T entity)
        {
            throw new NotImplementedException();
        }

        public Task DeleteAsync(string id)
        {
            throw new NotImplementedException();
        }
    }

    public abstract class AdoRepository
    {
        public SqlConnection _connection;
        public string _connectionString;

        public AdoRepository(string connectionString)
        {
            _connectionString = connectionString;
            _connection = new SqlConnection(connectionString);
        }

        protected static void AddParameters<X>(X value, SqlParameterCollection collection) where X : class
        {
            foreach (var prop in typeof(X).GetProperties().Where(p => !p.IsMarkedWith<NotAParamAttribute>() && !p.IsMarkedWith<IsOutputParamAttribute>()))
            {
                var propValue = prop.GetValue(value);

                if (prop.PropertyType == typeof(string))
                {
                    //SD191124: I found it annoying that if the value was null, we turned it to empty string
                    //I was going to change it, but then I realised it may mess up some SPs where we expect empty strings,
                    //because AddNVarCharMax turns NULL strings to empty strings
                    //so let's re-use KeepNull custom attribute

                    bool keepNull = prop.IsMarkedWith<KeepNullValuesAttribute>();

                    if (keepNull && propValue == null)
                    {
                        collection.AddNullableStringValue(prop.Name, null);
                    }
                    else
                    {
                        if (prop.IsMarkedWith<MaxLengthAttribute>())
                        {
                            int maxLength = prop.GetAttributes<MaxLengthAttribute>().First().Length;
                            collection.AddNVarChar(prop.Name, (string)propValue, maxLength);
                        }
                        else
                        {
                            collection.AddNVarCharMax(prop.Name, (string)propValue);
                        }
                    }
                }
                else if(prop.PropertyType == typeof(int))
                {
                    collection.AddInt(prop.Name, (int)propValue);
                }
                else if (prop.PropertyType == typeof(int?))
                {
                    collection.AddNullableInt(prop.Name, (int?)propValue);
                }                
                else if (prop.PropertyType == typeof(short))
                {
                    collection.AddSmallInt(prop.Name, (short)propValue);
                }
                else if (prop.PropertyType == typeof(short?))
                {
                    collection.AddNullableSmallInt(prop.Name, (short?)propValue);
                }
                else if (prop.PropertyType == typeof(decimal?))
                {
                    collection.AddNullableMoney(prop.Name, (decimal?)propValue);
                }
                else if (prop.PropertyType == typeof(bool))
                {
                    collection.AddBit(prop.Name, (bool)propValue);
                }
                else if (prop.PropertyType == typeof(bool?))
                {
                    if (propValue != null)
                    {
                        collection.AddBit(prop.Name, (bool)propValue);
                    }
                    else if (prop.IsMarkedWith<DefaultIfNullAttribute>())
                    {
                        bool defaultIfNull = (bool)prop.GetAttributes<DefaultIfNullAttribute>().First().DefaultValue;
                        collection.AddBitWithDefault(prop.Name, (bool?)propValue, defaultIfNull);
                    }
                    else
                    {
                        collection.AddWithValue("@" + prop.Name, DBNull.Value);
                    }
                }
                else if (prop.PropertyType == typeof(DateTime?))
                {
                    collection.AddNullableDateTime(prop.Name, (DateTime?)propValue);
                }
                else if (prop.PropertyType == typeof(DateTime))
                {
                    collection.AddNullableDateTime(prop.Name, (DateTime)propValue);
                }

                else if (prop.PropertyType == typeof(TimeSpan?))
                {
                    collection.AddNullableTime(prop.Name, (TimeSpan?)propValue);
                }
                else if (prop.PropertyType == typeof(TimeSpan))
                {
                    collection.AddNullableTime(prop.Name, (TimeSpan)propValue);
                }
                else if (prop.PropertyType == typeof(Time))
                {
                    collection.AddNullableTime(prop.Name, (Time)propValue);
                }
                else if (prop.PropertyType == typeof(Guid?))
                {
                    collection.AddNullableUniqueIdentifier(prop.Name, (Guid?)propValue);
                }
                else if (prop.PropertyType == typeof(Guid))
                {
                    collection.AddUniqueIdentifier(prop.Name, (Guid)propValue);
                }
                else if (prop.PropertyType == typeof(List<GenericTableType>))
                {
                    bool keepNull = prop.IsMarkedWith<KeepNullValuesAttribute>();
                    collection.AddTable(prop.Name, ((List<GenericTableType>)(propValue)).ToGenericDatatableNullableArgs(), "dbo.GenericTableType");
                }
                else if (prop.PropertyType == typeof(List<GenericTableTypeWithDecimal>))
                {
                    collection.AddTable(prop.Name, ((List<GenericTableTypeWithDecimal>)(propValue)).ToGenericDatatableWithDecimalNullableArgs());
                }
                else if (prop.PropertyType == typeof(List<AddressTableType>))
                {
                    collection.AddTable(prop.Name, ((List<AddressTableType>)(propValue)).ToAddressDatatableArgs());
                }
                else if (prop.PropertyType == typeof(List<BigGenericTableType>))
                {
                    collection.AddTable(prop.Name, ((List<BigGenericTableType>)(propValue)).ToGenericDatatableNullableArgs(), "dbo.BigGenericTableType");
                }
                else if (prop.PropertyType == typeof(List<BigMoneyGenericTableType>))
                {
                    collection.AddTable(prop.Name, ((List<BigMoneyGenericTableType>)(propValue)).ToGenericDatatableNullableArgs());
                }
                else if (prop.PropertyType == typeof(List<BigTextTwentyGenericTableType>))
                {
                    collection.AddTable(prop.Name, ((List<BigTextTwentyGenericTableType>)(propValue)).ToGenericDatatableNullableArgs());
                }
                else if (prop.PropertyType == typeof(List<TextBlockTableType>))
                {
                    collection.AddTable(prop.Name, ((List<TextBlockTableType>)(propValue)).ToTextBlockDatatableArgs());
                }
                else if(prop.PropertyType == typeof(List<GenericDateAndTimeTableType>))
                {
                    collection.AddTable(prop.Name, ((List<GenericDateAndTimeTableType>)(propValue)).ToGenericDateAndTimeTableTypeArgs());
                }
                else if (prop.PropertyType == typeof(int?[]))
                {
                    collection.AddTable(prop.Name, ((int?[])(propValue)).ToNumericDatatableNullableArgs(), "dbo.NumberTableType");
                }
                else if (prop.PropertyType == typeof(int[]))
                {
                    collection.AddTable(prop.Name, ((int[])(propValue)).ToNumericDatatableArgs(), "dbo.NumberTableType");
                }
                else if (prop.PropertyType == typeof(string[]))
                {
                    collection.AddTable(prop.Name, ((string[])(propValue)).ToTextDatatableArgs(), "dbo.TextTableType");
                }
                else
                {
                    if (propValue != null)
                    {
                        collection.AddWithValue("@" + prop.Name, propValue);
                    }
                }
            }
        }

        protected static X PopulateSingleResult<X>(SqlDataReader reader) where X : new()
        {
            var returnObject = new X();
            foreach (var prop in typeof(X).GetProperties().Where(p => !p.IsMarkedWith<NotAParamAttribute>() && !p.IsMarkedWith<IsOutputParamAttribute>()))
            {
                dynamic value = null;
                bool canSetValue = true;

                var exists = Enumerable.Range(0, reader.FieldCount).Any(i =>
                        string.Equals(
                                reader.GetName(i),
                                prop.Name,
                                StringComparison.OrdinalIgnoreCase
                          ));
                if (!exists) continue;

                if (prop.PropertyType == typeof(int))
                {
                    value = reader.ReadIntValue(prop.Name);
                }
                else if (prop.PropertyType == typeof(int?))
                {
                    value = reader.ReadNullableIntValue(prop.Name);
                }
                else if (prop.PropertyType == typeof(short))
                {
                    value = reader.ReadSmallIntValue(prop.Name);
                }
                else if (prop.PropertyType == typeof(short?))
                {
                    value = reader.ReadNullableSmallIntValue(prop.Name);
                }
                else if (prop.PropertyType == typeof(DateTime?))
                {
                    value = reader.ReadNullableDateTimeValue(prop.Name);
                }
                else if (prop.PropertyType == typeof(DateTime))
                {
                    value = reader.ReadDateTimeValue(prop.Name);
                }
                else if (prop.PropertyType == typeof(TimeSpan?))
                {
                    value = reader.ReadNullableTimeSpanValue(prop.Name);
                }
                else if (prop.PropertyType == typeof(TimeSpan))
                {
                    value = reader.ReadTimeSpanValue(prop.Name);
                }
                else if (prop.PropertyType == typeof(decimal?))
                {
                    value = reader.ReadNullableDecimalValue(prop.Name);
                }
                else if (prop.PropertyType == typeof(decimal))
                {
                    value = reader.ReadDecimalValue(prop.Name);
                }
                else if (prop.PropertyType == typeof(bool))
                {
                    value = reader.ReadBooleanValue(prop.Name);
                }
                else if (prop.PropertyType == typeof(bool?))
                {
                    value = reader.ReadNullableBoolValue(prop.Name);
                }
                else if (prop.PropertyType == typeof(Guid))
                {
                    value = reader.ReadNullableGuidValue(prop.Name) ?? Guid.Empty;
                }
                else if (prop.PropertyType == typeof(Guid?))
                {
                    value = reader.ReadNullableGuidValue(prop.Name);
                }
                else if (prop.PropertyType == typeof(string))
                {
                    value = reader.ReadStringValue(prop.Name);
                }
                else
                {
                    canSetValue = false;
                }

                if (canSetValue)
                {
                    prop.SetValue(returnObject, value);
                }
            }

            return returnObject;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        protected SqlCommand CreateSpCommandWithParams<X>(X args, string spName) where X : class
        {
            using (SqlCommand command = new SqlCommand(spName)
            {
                CommandType = CommandType.StoredProcedure
            })
            {
                AddParameters(args, command.Parameters);
                command.Connection = _connection;
                return command;
            }
        }
        protected SqlCommand CreateSpCommandWithoutParams(string spName)
        {
            using (SqlCommand command = new SqlCommand(spName)
            {
                CommandType = CommandType.StoredProcedure
            })
            {
                command.Connection = _connection;
                return command;
            }
        }

        /// <summary>
        /// Does while loop reader.read for ExecuteReaderAsync
        /// </summary>
        /// <typeparam name="X">Args</typeparam>
        /// <typeparam name="Y">Return type</typeparam>
        /// <param name="args">args</param>
        /// <param name="spName">Name of the SP</param>
        /// <returns>List<Y></returns>

        protected async Task<List<Y>> CallSpWithListReturnTypeAsync<X, Y>(X args, string spName) where X : class where Y : class, new()
        {
            var command = CreateSpCommandWithParams(args, spName);
            _connection.Open();
            try
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    var returnObject = new List<Y>();
                    while (reader.Read())
                    {
                        returnObject.Add(PopulateSingleResult<Y>(reader));
                    }
                    return returnObject;
                }
            }
            catch (Exception ex)
            {                
                _connection.Close();
                throw;
            }
            finally
            {
                _connection.Close();
            }
        }

        private class DummyEmptyClass
        {

        }

        protected async Task<List<Y>> CallSpWithListReturnTypeAsync<Y>(string spName) where Y : class, new()
        {
            var command = CreateSpCommandWithParams(new DummyEmptyClass(), spName);
            _connection.Open();
            try
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    var returnObject = new List<Y>();
                    while (reader.Read())
                    {
                        returnObject.Add(PopulateSingleResult<Y>(reader));
                    }
                    return returnObject;
                }
            }
            catch (Exception ex)
            {
                _connection.Close();
                throw;
            }
            finally
            {
                _connection.Close();
            }
        }

        protected async Task<Y> CallSpReturnFirstRowAsync<X, Y>(X args, string spName) where X : class where Y : class, new()
        {
            var command = CreateSpCommandWithParams(args, spName);
            _connection.Open();
            try
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (reader.Read())
                    {
                        return PopulateSingleResult<Y>(reader);
                    }
                }
            }
            catch (Exception ex)
            {
                _connection.Close();
                throw ex;
            }
            finally
            {
                _connection.Close();
            }
            return null;
        }

        protected async Task<Y> CallSpReturnScalarAsync<X, Y>(X args, string spName) where X : class where Y : struct
        {
            var command = CreateSpCommandWithParams(args, spName);
            _connection.Open();
            try
            {
                return (Y)await command.ExecuteScalarAsync();
            }
            catch (Exception ex)
            {
                _connection.Close();
                throw ex;
            }
            finally
            {
                _connection.Close();
            }
        }

        protected async Task<(bool, int)> CallSpNonQueryWithOutputBoolIntParamAsync<X>(X args, string spName) where X : class
        {
            var command = CreateSpCommandWithParams(args, spName);
            var outputProps = typeof(X).GetProperties().Where(o => o.IsMarkedWith<IsOutputParamAttribute>());

            (bool, int) retVal = (false, 0);

            SqlParameter boolOutputParam = new SqlParameter("@" + outputProps.First(o => o.PropertyType == typeof(bool)).Name, 0)
            {
                SqlDbType = SqlDbType.Bit,
                Direction = ParameterDirection.Output
            };
            command.Parameters.Add(boolOutputParam);
            SqlParameter intOutputParam = new SqlParameter("@" + outputProps.First(o => o.PropertyType == typeof(int)).Name, 0)
            {
                SqlDbType = SqlDbType.Int,
                Direction = ParameterDirection.Output
            };
            command.Parameters.Add(intOutputParam);
            _connection.Open();
            try
            {
                await command.ExecuteNonQueryAsync();
                retVal.Item1 = bool.Parse(boolOutputParam.Value.ToString());
                if (retVal.Item1)
                {
                    retVal.Item2 = int.Parse(intOutputParam.Value.ToString());
                }
            }
            catch (Exception ex)
            {
                _connection.Close();
                throw ex;
            }
            finally
            {
                _connection.Close();
            }

            return retVal;
        }


        protected async Task<bool> CallSpNonQueryWithOutputBoolParamAsync<X>(X args, string spName) where X : class
        {
            var command = CreateSpCommandWithParams(args, spName);
            var outputProp = typeof(X).GetProperties().First(o => o.IsMarkedWith<IsOutputParamAttribute>());
            bool retval = false;
            SqlParameter outputParam = new SqlParameter("@" + outputProp.Name, 0)
            {
                SqlDbType = SqlDbType.Bit,
                Direction = ParameterDirection.Output
            };
            command.Parameters.Add(outputParam);
            _connection.Open();
            try
            {
                await command.ExecuteNonQueryAsync();
                retval = bool.Parse(outputParam.Value.ToString());
            }
            catch (Exception ex)
            {
                _connection.Close();
                throw ex;
            }
            finally
            {
                _connection.Close();
            }

            return retval;
        }

        protected async Task<string> CallSpNonQueryWithOutputStringParamAsync<X>(X args, string spName) where X : class
        {
            var command = CreateSpCommandWithParams(args, spName);
            var outputProp = typeof(X).GetProperties().First(o => o.IsMarkedWith<IsOutputParamAttribute>());
            string retval = null;
            SqlParameter outputParam = new SqlParameter("@" + outputProp.Name, SqlDbType.NVarChar)
            {
                Direction = ParameterDirection.Output,
                Size = -1
            };
            command.Parameters.Add(outputParam);
            _connection.Open();
            try
            {
                await command.ExecuteNonQueryAsync();
                if (outputParam.Value != DBNull.Value)
                {
                    retval = outputParam.Value.ToString();
                }
            }
            catch (Exception ex)
            {
                _connection.Close();
                throw ex;
            }
            finally
            {
                _connection.Close();
            }

            return retval;
        }


        protected async Task<int> CallSpNonQueryWithOutputIntParamAsync<X>(X args, string spName) where X : class
        {
            var command = CreateSpCommandWithParams(args, spName);
            var outputProp = typeof(X).GetProperties().First(o => o.IsMarkedWith<IsOutputParamAttribute>());
            int retval = 0;
            SqlParameter outputParam = new SqlParameter("@" + outputProp.Name, 0)
            {
                SqlDbType = SqlDbType.Int,
                Direction = ParameterDirection.Output
            };
            command.Parameters.Add(outputParam);
            _connection.Open();
            try
            {
                await command.ExecuteNonQueryAsync();
                retval = int.Parse(outputParam.Value.ToString());
            }
            catch (Exception ex)
            {
                _connection.Close();
                throw ex;
            }
            finally
            {
                _connection.Close();
            }

            return retval;
        }

        //INT/? + INT/? + STRING/? (SD150124: redone)
        protected async Task<(int?, int?, string)> CallSpNonQueryWithOutputIntIntStringParamAsync<X>(X args, string spName) where X : class
        {
            var command = CreateSpCommandWithParams(args, spName);
            var outputProps = typeof(X).GetProperties().Where(o => o.IsMarkedWith<IsOutputParamAttribute>() || o.IsMarkedWith<IsInputOutputParamAttribute>());

            (int?, int?, string) retVal = (null, null, null);  //(false, 0, string.Empty);

            var prop1 = outputProps.FirstOrDefault(o => o.PropertyType == typeof(int));
            if (prop1 == null)
            {
                prop1 = outputProps.FirstOrDefault(o => o.PropertyType == typeof(int?));
            }
            bool isInputOutput1 = prop1.IsMarkedWith<IsInputOutputParamAttribute>();
            SqlParameter intOutputParam1 = new SqlParameter("@" + prop1.Name, SqlDbType.Int)
            {
                Direction = isInputOutput1 ? ParameterDirection.InputOutput : ParameterDirection.Output
            };
            if (isInputOutput1)
            {
                var propValue1 = prop1.GetValue(args);
                if (propValue1 == null)
                {
                    propValue1 = DBNull.Value;
                }
                intOutputParam1.Value = propValue1;
            }
            command.Parameters.Add(intOutputParam1);

            var prop2 = outputProps.Where(o => o.PropertyType == typeof(int)).ElementAtOrDefault(1);    //0-based, ie 2nd element
            if (prop2 == null)
            {
                prop2 = outputProps.Where(o => o.PropertyType == typeof(int?)).ElementAtOrDefault(1);   //0-based, ie 2nd element
            }
            bool isInputOutput2 = prop2.IsMarkedWith<IsInputOutputParamAttribute>();
            SqlParameter intOutputParam2 = new SqlParameter("@" + prop2.Name, SqlDbType.Int)
            {
                Direction = isInputOutput2 ? ParameterDirection.InputOutput : ParameterDirection.Output
            };
            if (isInputOutput2)
            {
                var propValue2 = prop2.GetValue(args);
                if (propValue2 == null)
                {
                    propValue2 = DBNull.Value;
                }
                intOutputParam2.Value = propValue2;
            }
            command.Parameters.Add(intOutputParam2);

            //typeof(string?) doesn't work, but typeof(string) covers both
            var prop3 = outputProps.FirstOrDefault(o => o.PropertyType == typeof(string));
            bool isInputOutput3 = prop3.IsMarkedWith<IsInputOutputParamAttribute>();
            SqlParameter stringOutputParam = new SqlParameter("@" + prop3.Name, SqlDbType.NVarChar)
            {
                Direction = isInputOutput3 ? ParameterDirection.InputOutput : ParameterDirection.Output,
                Size = -1
            };
            if (isInputOutput3)
            {
                var propValue3 = prop3.GetValue(args);
                if (propValue3 == null)
                {
                    propValue3 = DBNull.Value;
                }
                stringOutputParam.Value = propValue3;
            }
            command.Parameters.Add(stringOutputParam);

            _connection.Open();
            try
            {
                await command.ExecuteNonQueryAsync();

                int? i1 = null;
                if (intOutputParam1.Value != DBNull.Value)
                    i1 = int.Parse(intOutputParam1.Value.ToString());
                retVal.Item1 = i1;

                int? i2 = null;
                if (intOutputParam2.Value != DBNull.Value)
                    i2 = int.Parse(intOutputParam2.Value.ToString());
                retVal.Item2 = i2;

                retVal.Item3 = stringOutputParam.Value != DBNull.Value ? stringOutputParam.Value.ToString() : null;
            }
            catch (Exception ex)
            {
                _connection.Close();
                throw ex;
            }
            finally
            {
                _connection.Close();
            }
            return retVal;
        }

        protected async Task CallSpNonQueryAsync<X>(X args, string spName) where X : class
        {
            var command = CreateSpCommandWithParams(args, spName);
            _connection.Open();
            try
            {
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _connection.Close();
                throw ex;
            }
            finally
            {
                _connection.Close();
            }
        }
    }
}