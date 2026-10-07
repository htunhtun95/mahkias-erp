using Mahkias.Core;
using Mahkias.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace Mahkias.Data
{
    public static class SqlParameterCollectionExtensions
    {
        public static SqlCommand WithSuccessParameter(this SqlCommand command)
        {
            SqlCommand newCommand = command.Clone();

                var successParameter = new SqlParameter("Success", 0)
                {
                    SqlDbType = System.Data.SqlDbType.Bit,
                    Direction = System.Data.ParameterDirection.Output
                };

            newCommand.Parameters.Add(successParameter);

            command.Dispose();

            return newCommand;
        }

        /// <summary>
        /// a single int param
        /// </summary>
        /// <param name="command"></param>
        /// <param name="paramName"></param>
        /// <returns></returns>
        public static SqlCommand WithIntOutputParameter(this SqlCommand command, string paramName)
        {
            SqlCommand newCommand = command.Clone();

            var successParameter = new SqlParameter(paramName, 0)
            {
                SqlDbType = System.Data.SqlDbType.Int,
                Direction = System.Data.ParameterDirection.Output
            };

            newCommand.Parameters.Add(successParameter);

            command.Dispose();

            return newCommand;
        }

        /// <summary>
        /// multiple int params
        /// </summary>
        /// <param name="command"></param>
        /// <param name="paramNames"></param>
        /// <returns></returns>
        public static SqlCommand WithIntOutputParameter(this SqlCommand command, List<string> paramNames)
        {
            SqlCommand newCommand = command.Clone();

            foreach(var param in paramNames)
            {
                var successParameter = new SqlParameter(param, 0)
                {
                    SqlDbType = System.Data.SqlDbType.Int,
                    Direction = System.Data.ParameterDirection.Output
                };

                newCommand.Parameters.Add(successParameter);
            }

            command.Dispose();

            return newCommand;
        }



        public static async Task<SqlExecutionResult> ExecuteNonQueryWithResultAsync(this SqlCommand command)
        {
            await command.ExecuteNonQueryAsync();

            SqlExecutionResult result = new SqlExecutionResult();

            if (command.Parameters.Contains("Success"))
            {
                result.Successful = (bool)command.Parameters["Success"].Value;
            }

            return result;
        }

        /// <summary>
        /// a single int output
        /// </summary>
        /// <param name="command"></param>
        /// <param name="paramName"></param>
        /// <returns></returns>
        public static async Task<SqlExecutionResult> ExecuteNonQueryWithIntOutputAsync(this SqlCommand command, string paramName)
        {
            await command.ExecuteNonQueryAsync();

            SqlExecutionResult result = new SqlExecutionResult();

            if (command.Parameters.Contains(paramName))
            {
                result.IntResult = (int)command.Parameters[paramName].Value;
            }

            return result;
        }

        /// <summary>
        /// multiple int output values, each referred to by a different key equal to the param name
        /// </summary>
        /// <param name="command"></param>
        /// <param name="paramNames"></param>
        /// <returns></returns>
        public static async Task<SqlExecutionResult> ExecuteNonQueryWithIntOutputAsync(this SqlCommand command, List<string> paramNames)
        {
            await command.ExecuteNonQueryAsync();

            SqlExecutionResult result = new SqlExecutionResult();

            result.IntResults = new List<KeyValuePair<string, int>>();
            foreach (var param in paramNames)
            {
                if (command.Parameters.Contains(param))
                {
                    result.IntResults.Add(new KeyValuePair<string, int>(param, (int)command.Parameters[param].Value ));
                }
            }

            return result;
        }


        public static SqlParameter AddNullableUniqueIdentifier(this SqlParameterCollection collection, string name, Guid? value)
        {
            object dbValue = DBNull.Value;
            if (value != null)
                dbValue = value;
            return collection.Add(new SqlParameter("@" + name, SqlDbType.UniqueIdentifier, -1, ParameterDirection.Input, false, 0, 0, name, DataRowVersion.Current, dbValue));
        }

        public static SqlParameter AddUniqueIdentifier(this SqlParameterCollection collection, string name, Guid value)
        {
            /*Helper.AddSqlFtsMarkup(args.SearchText)*/
            return collection.Add(new SqlParameter("@" + name, SqlDbType.UniqueIdentifier, 0, ParameterDirection.Input, false, 0, 0, name, DataRowVersion.Current, value));
        }

        public static SqlParameter AddNVarChar(this SqlParameterCollection collection, string name, string value, int size, bool valueToEmptyWhenNull = true)
        {
            /*Helper.AddSqlFtsMarkup(args.SearchText)*/
            return collection.Add(new SqlParameter("@" + name, SqlDbType.NVarChar, size, ParameterDirection.Input, false, 0, 0, name, DataRowVersion.Current, valueToEmptyWhenNull ? value.ToEmptyWhenNull() : value));
        }
        public static SqlParameter AddNVarCharMax(this SqlParameterCollection collection, string name, string value)
        {
            /*Helper.AddSqlFtsMarkup(args.SearchText)*/
            return collection.Add(new SqlParameter("@" + name, SqlDbType.NVarChar, -1, ParameterDirection.Input, false, 0, 0, name, DataRowVersion.Current, value.ToEmptyWhenNull()));
        }
        public static SqlParameter AddNVarCharMax(this SqlParameterCollection collection, string name, string value, bool valueToEmptyWhenNull)
        {
            /*Helper.AddSqlFtsMarkup(args.SearchText)*/
            return collection.Add(new SqlParameter("@" + name, SqlDbType.NVarChar, -1, ParameterDirection.Input, false, 0, 0, name, DataRowVersion.Current, valueToEmptyWhenNull ? value.ToEmptyWhenNull() : value));
        }
        public static SqlParameter AddNullableStringValue(this SqlParameterCollection collection, string name, string value)
        {
            return collection.AddWithValue(name, string.IsNullOrEmpty(value) ? DBNull.Value : (object)value);
        }

        public static SqlParameter AddNullableDateTime(this SqlParameterCollection collection, string name, DateTime? value)
        {
            SqlParameter dateParameter = new SqlParameter("@" + name, value);
            dateParameter.IsNullable = true;
            if (dateParameter.Value == null)
                dateParameter.Value = DBNull.Value;
            dateParameter.Direction = ParameterDirection.Input;
            dateParameter.SqlDbType = SqlDbType.DateTime;
            return collection.Add(dateParameter);
             
        } 

        public static SqlParameter AddNullableTime(this SqlParameterCollection collection, string name, TimeSpan? value)
        {
            SqlParameter timeParameter = new SqlParameter("@" + name, value);
            timeParameter.IsNullable = true;
            if (timeParameter.Value == null)
                timeParameter.Value = DBNull.Value;
            timeParameter.Direction = ParameterDirection.Input;
            timeParameter.SqlDbType = SqlDbType.Time;
            return collection.Add(timeParameter);
        }

        public static SqlParameter AddNullableTime(this SqlParameterCollection collection, string name, Time value)
        {
            SqlParameter timeParameter = new SqlParameter("@" + name, DBNull.Value);
            timeParameter.IsNullable = true;

            if (value != null)
                timeParameter.Value = value.ToString();

            timeParameter.Direction = ParameterDirection.Input;
            timeParameter.SqlDbType = SqlDbType.Time;
            return collection.Add(timeParameter);
        }

        public static SqlParameter AddSmallInt(this SqlParameterCollection collection, string name, int value)
        {
            return collection.Add(new SqlParameter("@" + name, SqlDbType.SmallInt, 2, ParameterDirection.Input, false, 0, 0, name, DataRowVersion.Current, value));
        }

        public static SqlParameter AddNullableSmallInt(this SqlParameterCollection collection, string name, short? value)
        {
            object dbValue = DBNull.Value;
            if (value.HasValue)
                dbValue = value.Value;
            return collection.Add(new SqlParameter("@" + name, SqlDbType.SmallInt, 2, ParameterDirection.Input, false, 0, 0, name, DataRowVersion.Current, dbValue));
        }
        public static SqlParameter AddNullableTinyInt(this SqlParameterCollection collection, string name, int? value)
        {
            object dbValue = DBNull.Value;
            if (value.HasValue)
                dbValue = value.Value;
            return collection.Add(new SqlParameter("@" + name, SqlDbType.TinyInt, 2, ParameterDirection.Input, false, 0, 0, name, DataRowVersion.Current, dbValue));
        }
        public static SqlParameter AddInt(this SqlParameterCollection collection, string name, int value)
        {
            return collection.Add(new SqlParameter("@" + name, SqlDbType.Int, 4, ParameterDirection.Input, false, 0, 0, name, DataRowVersion.Current, value));
        }
        public static SqlParameter AddInt(this SqlParameterCollection collection, string name, string value)
        {
            return collection.Add(new SqlParameter("@" + name, SqlDbType.Int, 4, ParameterDirection.Input, false, 0, 0, name, DataRowVersion.Current, value));
        }
        public static SqlParameter AddNullableInt(this SqlParameterCollection collection, string name, int? value)
        {
            object dbValue = DBNull.Value;
            if (value.HasValue)
                dbValue = value.Value;
            return collection.Add(new SqlParameter("@" + name, SqlDbType.Int, 4, ParameterDirection.Input, false, 0, 0, name, DataRowVersion.Current, dbValue));
        }
        public static SqlParameter AddBigInt(this SqlParameterCollection collection, string name, long value)
        {
            return collection.Add(new SqlParameter("@" + name, SqlDbType.BigInt, 8, ParameterDirection.Input, false, 0, 0, name, DataRowVersion.Current, value));
        }
        public static SqlParameter AddNullableMoney(this SqlParameterCollection collection, string name, decimal? value)
        {
            object dbValue = DBNull.Value;
            if (value.HasValue)
                dbValue = value.Value;
            return collection.Add(new SqlParameter("@" + name, SqlDbType.Money, 8, ParameterDirection.Input, true, 0, 0, name, DataRowVersion.Current, dbValue));
        }
        public static SqlParameter AddNullableDecimal(this SqlParameterCollection collection, string name, decimal? value, byte precision, byte scale)
        {
            object dbValue = DBNull.Value;
            if (value.HasValue)
                dbValue = value.Value;
            return collection.Add(new SqlParameter("@" + name, SqlDbType.Decimal, precision, ParameterDirection.Input, true, precision, scale, name, DataRowVersion.Current, dbValue));
        }
        public static SqlParameter AddNullableBit(this SqlParameterCollection collection, string name, bool? value)
        {
            object dbValue = DBNull.Value;
            if (value.HasValue)
                dbValue = value.Value;
            //SD301221: I don't know what the 4th "isNullable" param actually means, but whether it's passed as true or false, it works in both cases
            return collection.Add(new SqlParameter("@" + name, SqlDbType.Bit, 1, ParameterDirection.Input, true, 0, 0, name, DataRowVersion.Current, dbValue));
        }

        public static SqlParameter AddBit(this SqlParameterCollection collection, string name, bool value)
        {
            return collection.Add(new SqlParameter("@" + name, SqlDbType.Bit, 1, ParameterDirection.Input, false, 0, 0, name, DataRowVersion.Current, value));
        }

        public static SqlParameter AddBitWithDefault(this SqlParameterCollection collection, string name, bool? value, bool defaultIfNull)
        {
            return collection.Add(new SqlParameter("@" + name, SqlDbType.Bit, 1, ParameterDirection.Input, false, 0, 0, name, DataRowVersion.Current, value.HasValue ? value.Value : defaultIfNull));
        }

        public static SqlParameter AddTable(this SqlParameterCollection collection, string name, DataTable value, string typeName = null)
        {
            var parameter = new SqlParameter("@" + name, SqlDbType.Structured, -1, ParameterDirection.Input, false, 0, 0, name, DataRowVersion.Current, value);
            if (!string.IsNullOrWhiteSpace(typeName))
            {
                parameter.TypeName = typeName;
            }

            return collection.Add(parameter);
        }

    }
}
