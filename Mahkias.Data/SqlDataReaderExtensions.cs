using System;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Linq;

namespace Mahkias.Data
{
    public static class ExtensionMethods
    {
        public static int ReadIntValue(this SqlDataReader reader, string field)
        {
            int intValue = 0;

            if (reader.GetSchemaTable().Select($"ColumnName = '{field}'").Any())
            {
                var rawValue = reader[field];

                if (rawValue != DBNull.Value && rawValue != null)
                {
                    int.TryParse(rawValue.ToString(), out intValue);
                }
            }

            return intValue;
        }

        public static long ReadBigIntValue(this SqlDataReader reader, string field)
        {
            long longValue = 0;

            if (reader.GetSchemaTable().Select($"ColumnName = '{field}'").Any())
            {
                var rawValue = reader[field];

                if (rawValue != DBNull.Value && rawValue != null)
                {
                    long.TryParse(rawValue.ToString(), out longValue);
                }
            }

            return longValue;
        }

        public static short ReadSmallIntValue(this SqlDataReader reader, string field)
        {
            short shortValue = 0;

            if (reader.GetSchemaTable().Select($"ColumnName = '{field}'").Any())
            {
                var rawValue = reader[field];

                if (rawValue != DBNull.Value && rawValue != null)
                {
                    short.TryParse(rawValue.ToString(), out shortValue);
                }
            }

            return shortValue;
        }

        public static short? ReadNullableSmallIntValue(this SqlDataReader reader, string field)
        {
            short shortValue = 0;
            if (reader.GetSchemaTable().Select($"ColumnName = '{field}'").Any())
            {
                var rawValue = reader[field];

                if (rawValue != DBNull.Value && rawValue != null)
                {
                    short.TryParse(rawValue.ToString(), out shortValue);
                    return shortValue;
                }
            }

            return null;
        }

        public static string ReadIntValueToString(this SqlDataReader reader, string field)
        {
            int intValue = 0;

            if (reader.GetSchemaTable().Select($"ColumnName = '{field}'").Any())
            {
                var rawValue = reader[field];

                if (rawValue != DBNull.Value && rawValue != null)
                {
                    int.TryParse(rawValue.ToString(), out intValue);
                }
            }

            if (intValue == 0) return string.Empty;
            return intValue.ToString();
        }

        public static int ReadByteValue(this SqlDataReader reader, string field)
        {
            byte value = 0;

            if (reader.GetSchemaTable().Select($"ColumnName = '{field}'").Any())
            {
                var rawValue = reader[field];

                if (rawValue != DBNull.Value && rawValue != null)
                {
                    byte.TryParse(rawValue.ToString(), out value);
                }
            }

            return value;
        }

        public static decimal ReadDecimalValue(this SqlDataReader reader, string field)
        {
            decimal decValue = 0;

            if (reader.GetSchemaTable().Select($"ColumnName = '{field}'").Any())
            {
                var rawValue = reader[field];

                if (rawValue != DBNull.Value && rawValue != null)
                {
                    decimal.TryParse(rawValue.ToString(), out decValue);
                }
            }

            return decValue;
        }

        public static decimal ReadAbsoluteDecimalValue(this SqlDataReader reader, string field)
        {
            decimal decValue = 0;

            if (reader.GetSchemaTable().Select($"ColumnName = '{field}'").Any())
            {
                var rawValue = reader[field];

                if (rawValue != DBNull.Value && rawValue != null)
                {
                    decimal.TryParse(rawValue.ToString(), out decValue);
                }
            }

            return Math.Abs(decValue);
        }

        public static bool ReadBooleanValue(this SqlDataReader reader, string field)
        {
            bool booValue = false;

            if (reader.GetSchemaTable().Select($"ColumnName = '{field}'").Any())
            {
                var rawValue = reader[field];

                if (rawValue != DBNull.Value && rawValue != null)
                {
                    bool.TryParse(rawValue.ToString(), out booValue);
                }
            }

            return booValue;
        }

        public static TimeSpan? ReadNullableTimeSpanValue(this SqlDataReader reader, string field)
        {
            TimeSpan value = TimeSpan.Zero;
            if (reader.GetSchemaTable().Select($"ColumnName = '{field}'").Any())
            {
                var rawValue = reader[field];

                if (rawValue != DBNull.Value && rawValue != null)
                {
                    if (TimeSpan.TryParse(rawValue.ToString(), out value))
                        return value;
                }
            }

            return null;
        }

        public static TimeSpan ReadTimeSpanValue(this SqlDataReader reader, string field)
        {
            TimeSpan value = TimeSpan.Zero;
            if (reader.GetSchemaTable().Select($"ColumnName = '{field}'").Any())
            {
                var rawValue = reader[field];

                if (rawValue != DBNull.Value && rawValue != null)
                {
                    if (TimeSpan.TryParse(rawValue.ToString(), out value))
                        return value;
                }

            }

            return value;
        }


        public static DateTime ReadDateTimeValue(this SqlDataReader reader, string field)
        {
            /*
             * //SD1206323: it'd be good to know what the default culture is - maybe on localhost it's different to what's on staging...??
            //System.Globalization.CultureInfo cultureinfo = CultureInfo.DefaultThreadCurrentCulture;
            CultureInfo cultureinfo = new CultureInfo("en-GB");
            */

            DateTime value = DateTime.MinValue;

            if (reader.GetSchemaTable().Select($"ColumnName = '{field}'").Any())
            {
                var rawValue = reader[field];

                if (rawValue != DBNull.Value && rawValue != null)
                {
                    //try parse doesn't take culture info as a param, so let's do it via parse and try/catch
                    if (DateTime.TryParse(rawValue.ToString(), out value))
                        return value;
                    /*try
                    {
                        DateTime dt = DateTime.Parse(rawValue.ToString(), cultureinfo);
                        return dt;
                    }
                    catch (Exception ex)
                    {
                        //let it go silently
                    }*/
                }
            }

            return DateTime.MinValue;
        }     

        public static DateTime? ReadNullableDateTimeValue(this SqlDataReader reader, string field)
        {
            /*
             * //SD1206323: it'd be good to know what the default culture is - maybe on localhost it's different to what's on staging...??
            //System.Globalization.CultureInfo cultureinfo = CultureInfo.DefaultThreadCurrentCulture;
            CultureInfo cultureinfo = new CultureInfo("en-GB");
            */

            DateTime value = DateTime.MinValue;

            if (reader.GetSchemaTable().Select($"ColumnName = '{field}'").Any())
            {
                var rawValue = reader[field];

                if (rawValue != DBNull.Value && rawValue != null)
                {
                    //try parse doesn't take culture info as a param, so let's do it via parse and try/catch
                    if (DateTime.TryParse(rawValue.ToString(), out value))
                        return value;

                    /*try
                    {
                        DateTime dt = DateTime.Parse(rawValue.ToString(), cultureinfo);
                        return dt;
                    }
                    catch (Exception ex)
                    {
                        //let it go silently
                    }*/
                }
            }

            return null;
        }

        public static int? ReadNullableIntValue(this SqlDataReader reader, string field)
        {
            if (reader.GetSchemaTable().Select($"ColumnName = '{field}'").Any())
            {
                var rawValue = reader[field];

                if (rawValue != DBNull.Value && rawValue != null)
                {
                    if (int.TryParse(rawValue.ToString(), out int value))
                    {
                        return value;
                    }

                    return null;
                }

                return null;
            }

            return null;
        }

        public static decimal? ReadNullableDecimalValue(this SqlDataReader reader, string field)
        {
            if (reader.GetSchemaTable().Select($"ColumnName = '{field}'").Any())
            {
                var rawValue = reader[field];

                if (rawValue != DBNull.Value && rawValue != null)
                {
                    if (decimal.TryParse(rawValue.ToString(), out decimal value))
                    {
                        return value;
                    }

                    return null;
                }

                return null;
            }

            return null;
        }

        public static bool? ReadNullableBoolValue(this SqlDataReader reader, string field)
        {
            if (reader.GetSchemaTable().Select($"ColumnName = '{field}'").Any())
            {
                var rawValue = reader[field];

                if (rawValue != DBNull.Value && rawValue != null)
                {
                    if (bool.TryParse(rawValue.ToString(), out bool value))
                    {
                        return value;
                    }

                    return null;
                }

                return null;
            }

            return null;
        }

        public static Guid? ReadNullableGuidValue(this SqlDataReader reader, string field)
        {
            if (reader.GetSchemaTable().Select($"ColumnName = '{field}'").Any())
            {
                var rawValue = reader[field];

                if (rawValue != DBNull.Value && rawValue != null)
                {
                    if (Guid.TryParse(rawValue.ToString(), out Guid value))
                    {
                        return value;
                    }

                    return null;
                }

                return null;
            }

            return null;
        }

        /// <summary>
        /// Copied from ReadNullableGuidValue. Returning new Guid() when not possible to parse value to guid
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="field"></param>
        /// <returns>Guid value or new Guid value with all 0 values</returns>
        public static Guid ReadGuidValue(this SqlDataReader reader, string field)
        {
            if (reader.GetSchemaTable().Select($"ColumnName = '{field}'").Any())
            {
                var rawValue = reader[field];

                if (rawValue != DBNull.Value && rawValue != null)
                {
                    if (Guid.TryParse(rawValue.ToString(), out Guid value))
                    {
                        return value;
                    }
                    return new Guid();
                }
                return new Guid();
            }
            return new Guid();
        }


        public static string ReadStringValue(this SqlDataReader reader, string field)
        {
            if (reader.GetSchemaTable().Select($"ColumnName = '{field}'").Any())
            {
                var rawValue = reader[field];

                if (rawValue != DBNull.Value && rawValue != null)
                {
                    return rawValue.ToString();
                }
            }

            return string.Empty;
        }

        public static string ReadNullableStringValue(this SqlDataReader reader, string field)
        {
            if (reader.GetSchemaTable().Select($"ColumnName = '{field}'").Any())
            {
                var rawValue = reader[field];

                if (rawValue != DBNull.Value && rawValue != null)
                {
                    return rawValue.ToString();
                }
            }

            return null;
        }

        public static string ReadNullableTimeValueAsString(this SqlDataReader reader, string field, bool includeSeconds)
        {
            if (reader.GetSchemaTable().Select($"ColumnName = '{field}'").Any())
            {
                var rawValue = reader[field];
                if (rawValue != DBNull.Value && rawValue != null)
                {
                    return includeSeconds ? rawValue.ToString().Substring(0, 8) : rawValue.ToString().Substring(0, 5);
                }
            }
            return string.Empty;
        }
    }
}