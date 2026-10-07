using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Mahkias.Core.DataTableTypes;

namespace Mahkias.Core.Helpers
{
    public static class ExtensionMethods
    {
        private const string EmailAllowedPattern = @"([a-zA-Z0-9_\-\.\+]+)@([a-zA-Z0-9_\-\.]+)\.([a-zA-Z]{2,5})";

        #region "String extensions"

        //USE StringExtensions.cs !!

        #endregion  "String extensions"


        #region "Email extensions"

        public static bool IsEmailValid(this string email)
        {
            MatchCollection mc = Regex.Matches(email, EmailAllowedPattern);
            return mc != null && mc.Count == 1;
        }

        public static string GetValidEmail(this string email)
        {
            if (!email.IsEmailValid())
            {
                return email;
            }
            MatchCollection mc = Regex.Matches(email, EmailAllowedPattern);
            return mc[0].Value;
        }

        public static string GetMultipleValidEmail(this string email)
        {
            MatchCollection mc = Regex.Matches(email, EmailAllowedPattern);
            return mc != null ? mc.Cast<Match>().Select(o => o.Value).Aggregate((i, j) => i + ", " + j) : email;
        }

        public static IEnumerable<string> GetValidAdditionalEmails(this string email)
        {
            if (!email.IsEmailValid())
            {
                return null;
            }
            MatchCollection mc = Regex.Matches(email, EmailAllowedPattern);

            //not getting the first one
            return mc.Cast<Match>().SkipWhile(o => o.Index < 1).Select(o => o.Value).ToList();
        }

        #endregion  "Email extensions"


        #region "Numeric extensions"

        public static int RandomNumber(int noOfDigits)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < noOfDigits; i++)
            {
                sb.Append(new Random().Next(0, 9));
            }

            return int.Parse(sb.ToString());
        }

        public static string ShowLeastDecimal(this decimal value)
        {
            string strDecimal = value.ToString("N2");
            strDecimal = strDecimal.Replace(".00", "");
            strDecimal = strDecimal.EndsWith("0") && strDecimal.Contains(".") ? strDecimal.Substring(0, strDecimal
                .Length - 1) : strDecimal;

            return strDecimal;
        }

        public static string ToOccurrenceSuffix(this int integer)
        {
            switch (integer % 100)
            {
                case 11:
                case 12:
                case 13:
                    return "th";
            }
            switch (integer % 10)
            {
                case 1:
                    return "st";
                case 2:
                    return "nd";
                case 3:
                    return "rd";
                default:
                    return "th";
            }
        }
        public static string ToRound(this decimal number, bool round)
        {
            if (round)
            {
                return Math.Round(number, MidpointRounding.AwayFromZero).ToString("N0");
            }
            else
            {
                return number.ToString("N2");
            }
        }

        public static bool IsDecimal(this decimal number)
        {
            //for doubles, use this: Math.Abs(d % 1) <= (Double.Epsilon * 100)
            //https://stackoverflow.com/questions/2751593/how-to-determine-if-a-decimal-double-is-an-integer
            //for decimals, this should be enough
            return number % 1 > 0;
        }

        public static bool HasDecimal(this List<decimal> numbers)
        {
            int cntDecimals = numbers.Count(x => x.IsDecimal());
            return cntDecimals > 0;
        }
        public static bool HasDecimal(this decimal[] numbers)
        {
            int cntDecimals = numbers.Count(x => x.IsDecimal());
            return cntDecimals > 0;
        }

        public static bool HasAllDecimals(this List<decimal> numbers)
        {
            int cntDecimals = numbers.Count(x => x.IsDecimal());
            return cntDecimals == numbers.Count;
        }
        public static bool HasAllDecimals(this decimal[] numbers)
        {
            var cntDecimals = numbers.Count(x => x.IsDecimal());
            return cntDecimals == numbers.Length;
        }

        #endregion  "Numeric extensions"



        public static DateTime StartOfTheDay(this DateTime date)
        {
            //https://learn.microsoft.com/en-us/dotnet/api/system.datetime.date?view=net-7.0
            //return new DateTime(date.Year, date.Month, date.Day, 0, 0, 0);
            return date.Date;
        }

        public static DateTime EndOfTheDay(this DateTime date)
        {
            return new DateTime(date.Year, date.Month, date.Day, 23, 59, 59);
        }

        public static bool IsOnSameDate(this DateTime date1, DateTime date2)
        {
            //https://learn.microsoft.com/en-us/dotnet/api/system.datetime.date?view=net-7.0
            //return date1.StartOfTheDay() == date2.StartOfTheDay();
            return date1.Date == date2.Date;
        }

        public static string ConverDateTimeToStringWithTimeZone(this DateTime date, DateTime? utcDate)
        {
            var dateString = date.ToString("yyyy-MM-ddTHH:mm:ss+00:00");
            if (utcDate.HasValue)
            {
                var subDate = date.Subtract(utcDate.Value);
                var diffMin = subDate.Minutes;
                string plusOrMinus = "+";
                if (subDate.Hours < 0)
                {
                    subDate = utcDate.Value.Subtract(date);
                    plusOrMinus = "-";
                }

                if (subDate.Hours != 0)
                {
                    diffMin = subDate.Minutes % (((int)subDate.Hours) * 60);
                }

                dateString = date.ToString("yyyy-MM-ddTHH:mm:ss") + $"{plusOrMinus}{subDate.Hours.ToString().PadLeft(2, '0')}:{diffMin.ToString().PadLeft(2, '0')}";
            }
            return dateString;
        }
        public static int CalculateAge(this DateTime date)
        {
            // Save today's date.
            var today = DateTime.UtcNow;
            // Calculate the age.
            int age = today.Year - date.Year;
            // Go back to the year the person was born in case of a leap year
            if (date.Date > today.AddYears(-age)) age--;

            return age;
        }

        public static int? CalculateAge(this DateTime? date)
        {
            if (!date.HasValue)
            {
                return null;
            }



            DateTime dateTime = date.Value;
            // Save today's date.
            var today = DateTime.UtcNow;
            // Calculate the age.
            int age = today.Year - dateTime.Year;
            // Go back to the year the person was born in case of a leap year
            if (dateTime.Date > today.AddYears(-age)) age--;

            return age;
        }

        public static string RelativeTime(this DateTime myDate)
        {
            // myDate = myDate.ToUniversalTime();

            var gmtNow = DateTime.UtcNow.AddHours(1);


            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT").ToLower() == "development")
            {
                gmtNow = TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time"));
            }
            else
            {
                gmtNow = TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon"));
            }

            const int SECOND = 1;
            const int MINUTE = 60 * SECOND;
            const int HOUR = 60 * MINUTE;
            const int DAY = 24 * HOUR;
            const int MONTH = 30 * DAY;

            var ts = new TimeSpan(gmtNow.Ticks - myDate.Ticks);
            double delta = Math.Abs(ts.TotalSeconds);

            if (myDate > gmtNow)
            {
                if (delta < 1 * MINUTE)
                {
                    if (ts.Seconds == 0)
                        return "Just now";
                    else
                        return ts.Seconds == 1 ? "1 second from now" : ts.Seconds + " seconds from now";
                }
                if (delta < 2 * MINUTE)
                    return "1 minute from now";
                if (delta < 45 * MINUTE)
                    return ts.Minutes + " minutes from now";
                if (delta < 90 * MINUTE)
                    return "1 hour from now";
                if (delta < 24 * HOUR)
                    return ts.Hours + " hours from now";
                if (delta < 48 * HOUR)
                    return "Tomorrow";
                if (delta < 30 * DAY)
                    return ts.Days + " days from now";
                if (delta < 12 * MONTH)
                {
                    int months = Convert.ToInt32(Math.Floor((double)ts.Days / 30));
                    return months <= 1 ? "1 month from now" : months + " months from now";
                }
                else
                {
                    int years = Convert.ToInt32(Math.Floor((double)ts.Days / 365));
                    return years <= 1 ? "1 year from now" : years + " years from now";
                }
            }
            else
            {


                if (delta < 1 * MINUTE)
                {

                    if (ts.Seconds == 0)
                        return "Just now";
                    else
                        return ts.Seconds == 1 ? "1 second ago" : ts.Seconds + " seconds ago";
                }

                if (delta < 2 * MINUTE)
                    return "1 minute ago";

                if (delta < 45 * MINUTE)
                    return ts.Minutes + " minutes ago";

                if (delta < 90 * MINUTE)
                    return "1 hour ago";

                if (delta < 24 * HOUR)
                    return ts.Hours + " hours ago";

                if (delta < 48 * HOUR)
                    return "yesterday";

                if (delta < 30 * DAY)
                    return ts.Days + " days ago";

                if (delta < 12 * MONTH)
                {
                    int months = Convert.ToInt32(Math.Floor((double)ts.Days / 30));
                    return months <= 1 ? "1 month ago" : months + " months ago";
                }
                else
                {
                    int years = Convert.ToInt32(Math.Floor((double)ts.Days / 365));
                    return years <= 1 ? "1 year ago" : years + " years ago";
                }
            }
        }

        /// <summary>
        /// formats two dates combined into a string based on various options. Can be extended by adding a new format version
        /// </summary>
        /// <param name="dateFrom">non-nullable date</param>
        /// <param name="dateTo">non-nullable date</param>
        /// <param name="separator">e.g. "-", " - ", " to ", " "</param>
        /// <param name="formatVersion">
        /// (1) = 31 December 2022 - 2 January 2023 or 9 - 12 August 2023; 
        /// (2) = Dec 31st - Jan 2nd or Aug 9th - 12th;
        /// (3) = Thu 31st December - Sat 2nd January or Mon 9th - Thu 12th August; 
        /// </param>
        /// <param name="includeDaySuffix">whether st, nd, rd, th suffix is appended to the day number</param>
        /// <returns></returns>
        public static string TwoDatesFormatted(
            this DateTime dateFrom,
            DateTime dateTo,
            string separator,
            int formatVersion,
            bool includeDaySuffix)
        {
            string datesFormatted = "";
            string daySuffix = includeDaySuffix ? "nn" : "";

            switch (formatVersion)
            {
                case 1:
                    /// 31 December 2022 - 2 January 2023 
                    /// 2 January - 12 February 2023
                    /// 2 - 12 January 2023
                    /// 2 January 2023 -> if both dates are the same

                    if (!dateFrom.IsOnSameDate(dateTo))
                    {
                        if (dateFrom.Month == dateTo.Month && dateFrom.Year == dateTo.Year)
                        {
                            //dateFrom.ToString("d"); returns dd/mm/yyyy -> no idea why!
                            datesFormatted = dateFrom.Day.ToString() + (includeDaySuffix ? dateFrom.Day.ToOccurrenceSuffix() : "");
                        }
                        else if (dateFrom.Month != dateTo.Month)
                        {
                            datesFormatted = dateFrom.ToString($"d{daySuffix} MMMM", includeDaySuffix);
                        }
                        else
                        {
                            datesFormatted = dateFrom.ToString($"d{daySuffix} MMMM yyyy", includeDaySuffix);
                        }
                        datesFormatted += separator;
                    }

                    datesFormatted += dateTo.ToString($"d{daySuffix} MMMM yyyy", includeDaySuffix);

                    break;

                case 2:
                    /// Dec 31st - Jan 2nd 
                    /// Jan 2nd - Feb 12th
                    /// Jan 2nd - 12th
                    /// Jan 2nd -> if both dates are the same

                    datesFormatted = dateFrom.ToString($"MMM d{daySuffix}", includeDaySuffix);

                    if (!dateFrom.IsOnSameDate(dateTo))
                    {
                        datesFormatted += separator;

                        if (dateFrom.Month == dateTo.Month)
                        {
                            //dateTo.ToString("d"); returns dd/mm/yyyy -> no idea why!
                            datesFormatted += dateTo.Day.ToString() + (includeDaySuffix ? dateTo.Day.ToOccurrenceSuffix() : "");
                        }
                        else
                        {
                            datesFormatted += dateTo.ToString($"MMM d{daySuffix}", includeDaySuffix);
                        }
                    }

                    break;

                case 3:
                    /// Thu 31st December - Sat 2nd January 
                    /// Sat 2nd January - Fri 12th February
                    /// Sat 2nd - Tue 12th January
                    /// Sat 2nd January -> if both dates are the same

                    if (!dateFrom.IsOnSameDate(dateTo))
                    {
                        if (dateFrom.Month == dateTo.Month)
                        {
                            //dateFrom.ToString("d"); returns dd/mm/yyyy -> no idea why!
                            datesFormatted = dateFrom.ToString("ddd ")/*.DayOfWeek*/ + dateFrom.Day.ToString() + (includeDaySuffix ? dateFrom.Day.ToOccurrenceSuffix() : "");
                        }
                        else
                        {
                            datesFormatted = dateFrom.ToString($"ddd d{daySuffix} MMMM", includeDaySuffix);
                        }
                        datesFormatted += separator;
                    }

                    datesFormatted += dateTo.ToString($"ddd d{daySuffix} MMMM", includeDaySuffix);

                    break;

                default:
                    break;
            }

            return datesFormatted;
        }

        public static string ToString(this DateTime dateTime, string format, bool useExtendedSpecifiers)
        {
            return useExtendedSpecifiers
                ? dateTime.ToString(format)
                    .Replace("nn", dateTime.Day.ToOccurrenceSuffix().ToLower())
                    .Replace("NN", dateTime.Day.ToOccurrenceSuffix().ToUpper())
                : dateTime.ToString(format);
        }

        public static T? GetNullableFromReaderValue<T>(object value) where T : struct
        {
            if (!(value is DBNull))
            {
                return (T?)value;
            }

            return null;
        }

        public static T GetNullableFromReaderReference<T>(object value) where T : class
        {
            if (!(value is DBNull))
            {
                return (T)value;
            }

            return null;
        }

        public static DateTime AddHoursSkipWeekend(this DateTime start, double value)
        {
            if (start.DayOfWeek != DayOfWeek.Saturday && start.DayOfWeek != DayOfWeek.Sunday)
            {
                var hoursUntilNextSaturday = (6 - (int)start.DayOfWeek) * 24 - start.Hour;

                if (value > hoursUntilNextSaturday)
                {
                    start = start.AddDays(2);
                }
            }
            else
            {
                var extraDays = start.DayOfWeek != DayOfWeek.Saturday ? 1 : 2;
                var nextMonday = start.AddDays(extraDays);
                start = nextMonday.AddHours((-1) * start.Hour);
            }
            return start.AddHours(value);
        }

        public static DateTime AddHoursSkipWeekendv2(this DateTime start, double value)
        {
            const int hoursPerDay = 24;
            const int startHour = 0;
            // Don't start counting hours until start time is during working hours
            if (start.TimeOfDay.TotalHours > startHour + hoursPerDay)
                start = start.Date.AddDays(1).AddHours(startHour);
            if (start.TimeOfDay.TotalHours < startHour)
                start = start.Date.AddHours(startHour);
            if (start.DayOfWeek == DayOfWeek.Saturday)
                start.AddDays(2);
            else if (start.DayOfWeek == DayOfWeek.Sunday)
                start.AddDays(1);
            // Calculate how much working time already passed on the first day
            TimeSpan firstDayOffset = start.TimeOfDay.Subtract(TimeSpan.FromHours(startHour));
            // Calculate number of whole days to add
            int wholeDays = (int)((value + firstDayOffset.Hours) / hoursPerDay);
            // How many hours off the specified offset does this many whole days consume?
            TimeSpan wholeDaysHours = TimeSpan.FromHours(wholeDays * hoursPerDay);
            // Calculate the final time of day based on the number of whole days spanned and the specified offset
            TimeSpan remainder = TimeSpan.FromHours(value - wholeDaysHours.Hours);
            // How far into the week is the starting date?
            int weekOffset = ((int)(start.DayOfWeek + 7) - (int)DayOfWeek.Monday) % 7;
            // How many weekends are spanned?
            int weekends = (int)((wholeDays + weekOffset) / 5);
            // Calculate the final result using all the above calculated values
            return start.AddDays(wholeDays + weekends * 2).Add(remainder);
        }

        /// <summary>
        /// Treats the long value as a Unix Timestamp and converts to DateTime
        /// </summary>
        /// <param name="timestamp">long to convert</param>
        /// <returns>DateTime version of timestamp</returns>
        public static DateTime ToDateTime(this long timestamp)
        {
            return new DateTime(1970, 1, 1).AddSeconds(timestamp);
        }

        /// <summary>
        /// Converts the DateTime object to a long Unix Timestamp
        /// </summary>
        /// <param name="dateTime">DateTime to convert</param>
        /// <returns>long Unix Timestamp</returns>
        public static long ToUnixTimestamp(this DateTime dateTime)
        {
            return Convert.ToInt64(dateTime.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);
        }

        public static string ToReadableString(this TimeSpan span)
        {
            string formatted = string.Format("{0}{1}{2}{3}",
                span.Duration().Days > 0 ? string.Format("{0:0} day{1} ", span.Days, span.Days == 1 ? string.Empty : "s") : string.Empty,
                span.Duration().Hours > 0 ? string.Format("{0:0} hour{1} ", span.Hours, span.Hours == 1 ? string.Empty : "s") : string.Empty,
                span.Duration().Minutes > 0 ? string.Format("{0:0} minute{1} ", span.Minutes, span.Minutes == 1 ? string.Empty : "s") : string.Empty,
                span.Duration().Seconds > 0 ? string.Format("{0:0} second{1}", span.Seconds, span.Seconds == 1 ? string.Empty : "s") : string.Empty);

            if (formatted.EndsWith(", ")) formatted = formatted.Substring(0, formatted.Length - 2);

            if (string.IsNullOrEmpty(formatted)) formatted = "0 seconds";

            return formatted;
        }



        #region "DataTable extensions"

        private static object NullableValue(object nullableValue)
        {
            if (nullableValue == null)
                return DBNull.Value;
            else
                return nullableValue;
        }

        public static DataTable ToDatatableArgs(this List<string> args)
        {
            DataTable tbl = new DataTable();
            tbl.Columns.Add("TextValue", typeof(string));

            if (args != null && args.Any())
            {
                foreach (var val in args)
                {
                    tbl.Rows.Add(val);
                }
            }

            return tbl;
        }

        public static DataTable ToTextDatatableArgs(this string[] args)
        {
            DataTable tbl = new DataTable();
            tbl.Columns.Add("TextValue", typeof(string));

            if (args != null && args.Any())
            {
                foreach (var val in args)
                {
                    tbl.Rows.Add(val);
                }
            }

            return tbl;
        }

        public static DataTable ToNumericDatatableArgs(this int[] args)
        {
            DataTable tbl = new DataTable();
            tbl.Columns.Add("NumericID", typeof(int));

            if (args != null && args.Any())
            {
                foreach (var val in args)
                {
                    tbl.Rows.Add(val);
                }
            }
            return tbl;
        }

        /// <summary>
        /// allows NULL values to be passed on to the SP, instead of basic type default values
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static DataTable ToNumericDatatableNullableArgs(this int?[] args)
        {
            DataTable tbl = new DataTable();
            tbl.Columns.Add("NumericID", typeof(int));

            if (args != null && args.Any())
            {
                foreach (var val in args)
                {
                    tbl.Rows.Add(NullableValue(val));
                }
            }
            return tbl;
        }

        public static DataTable ToAddressDatatableArgs(this List<AddressTableType> args)
        {
            DataTable tbl = new DataTable();
            tbl.Columns.Add("Id", typeof(int));
            tbl.Columns.Add("Address1", typeof(string));
            tbl.Columns.Add("Address2", typeof(string));
            tbl.Columns.Add("Address3", typeof(string));
            tbl.Columns.Add("CityId", typeof(int));
            tbl.Columns.Add("CityFriendlyUrl", typeof(string));
            tbl.Columns.Add("CityName", typeof(string));
            tbl.Columns.Add("StateId", typeof(int));
            tbl.Columns.Add("StateFriendlyUrl", typeof(string));
            tbl.Columns.Add("StateName", typeof(string));
            tbl.Columns.Add("Postcode", typeof(string));
            tbl.Columns.Add("CountryId", typeof(int));
            tbl.Columns.Add("CountryCode", typeof(string));
            tbl.Columns.Add("TravellerAddressGuid", typeof(Guid));
            tbl.Columns.Add("NumericValue1", typeof(int));
            tbl.Columns.Add("NumericValue2", typeof(int));
            tbl.Columns.Add("NumericValue3", typeof(int));
            tbl.Columns.Add("NumericValue4", typeof(int));
            tbl.Columns.Add("TextValue1", typeof(string));
            tbl.Columns.Add("TextValue2", typeof(string));
            tbl.Columns.Add("TextValue3", typeof(string));
            tbl.Columns.Add("TextValue4", typeof(string));
            tbl.Columns.Add("BitValue1", typeof(bool));
            tbl.Columns.Add("BitValue2", typeof(bool));
            tbl.Columns.Add("BitValue3", typeof(bool));
            tbl.Columns.Add("BitValue4", typeof(bool));

            if (args != null && args.Any())
            {
                foreach (var val in args)
                {
                    DataRow row = tbl.NewRow();
                    row["Id"] = val.Id ?? 0;
                    row["Address1"] = val.Address1;
                    row["Address2"] = val.Address2;
                    row["Address3"] = val.Address3;
                    row["CityId"] = val.CityId ?? 0;
                    row["CityFriendlyUrl"] = val.CityFriendlyUrl;
                    row["CityName"] = val.CityName;
                    row["StateId"] = val.StateId ?? 0;
                    row["StateFriendlyUrl"] = val.StateFriendlyUrl;
                    row["StateName"] = val.StateName;
                    row["Postcode"] = val.Postcode;
                    row["CountryId"] = val.CountryId ?? 0;
                    row["CountryCode"] = val.CountryCode;
                    row["TravellerAddressGuid"] = val.TravellerAddressGuid ?? new Guid();
                    row["NumericValue1"] = val.NumericValue1 ?? 0;
                    row["NumericValue2"] = val.NumericValue2 ?? 0;
                    row["NumericValue3"] = val.NumericValue3 ?? 0;
                    row["NumericValue4"] = val.NumericValue4 ?? 0;
                    row["TextValue1"] = val.TextValue1;
                    row["TextValue2"] = val.TextValue2;
                    row["TextValue3"] = val.TextValue3;
                    row["TextValue4"] = val.TextValue4;
                    row["BitValue1"] = val.BitValue1 ?? false;
                    row["BitValue2"] = val.BitValue2 ?? false;
                    row["BitValue3"] = val.BitValue3 ?? false;
                    row["BitValue4"] = val.BitValue4 ?? false;
                    tbl.Rows.Add(row);
                }
            }
            return tbl;
        }

        public static DataTable ToTextBlockDatatableArgs(this List<TextBlockTableType> args)
        {
            DataTable tbl = new DataTable();
            tbl.Columns.Add("Id", typeof(int));
            tbl.Columns.Add("TextBlockTitle", typeof(string));
            tbl.Columns.Add("TextBlockDescription", typeof(string));
            tbl.Columns.Add("DisplayOrder", typeof(short));
            if (args != null && args.Any())
            {
                foreach (var val in args)
                {
                    DataRow row = tbl.NewRow();
                    row["Id"] = val.Id;
                    row["TextBlockTitle"] = val.TextBlockTitle ?? null;
                    row["TextBlockDescription"] = val.TextBlockDescription ?? null;
                    row["DisplayOrder"] = val.DisplayOrder;
                    tbl.Rows.Add(row);
                }
            }

            return tbl;
        }

        [Obsolete("Use ToGenericDatatableNullableArgs instead. Please check SP first, that is using NULL evaluation instead of a basic type default value evaluation!")]
        public static DataTable ToGenericDatatableArgs(this List<GenericTableType> args)
        {
            DataTable tbl = new DataTable();
            tbl.Columns.Add("NumericValue1", typeof(int));
            tbl.Columns.Add("NumericValue2", typeof(int));
            tbl.Columns.Add("NumericValue3", typeof(int));
            tbl.Columns.Add("NumericValue4", typeof(int));
            tbl.Columns.Add("TextValue1", typeof(string));
            tbl.Columns.Add("TextValue2", typeof(string));
            tbl.Columns.Add("TextValue3", typeof(string));
            tbl.Columns.Add("TextValue4", typeof(string));
            tbl.Columns.Add("BitValue1", typeof(bool));
            tbl.Columns.Add("BitValue2", typeof(bool));
            tbl.Columns.Add("BitValue3", typeof(bool));
            tbl.Columns.Add("BitValue4", typeof(bool));

            if (args != null && args.Any())
            {
                foreach (var val in args)
                {
                    DataRow row = tbl.NewRow();
                    row["NumericValue1"] = val.NumericValue1 ?? 0;
                    row["NumericValue2"] = val.NumericValue2 ?? 0;
                    row["NumericValue3"] = val.NumericValue3 ?? 0;
                    row["NumericValue4"] = val.NumericValue4 ?? 0;
                    row["TextValue1"] = val.TextValue1 ?? "";
                    row["TextValue2"] = val.TextValue2 ?? "";
                    row["TextValue3"] = val.TextValue3 ?? "";
                    row["TextValue4"] = val.TextValue4 ?? "";
                    row["BitValue1"] = val.BitValue1 ?? false;
                    row["BitValue2"] = val.BitValue2 ?? false;
                    row["BitValue3"] = val.BitValue3 ?? false;
                    row["BitValue4"] = val.BitValue4 ?? false;
                    tbl.Rows.Add(row);
                }
            }

            return tbl;
        }

        /// <summary>
        /// allows NULL values to be passed on to the SP, instead of basic type default values
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static DataTable ToGenericDatatableNullableArgs(this List<GenericTableType> args)
        {
            DataTable tbl = new DataTable();
            tbl.Columns.Add("NumericValue1", typeof(int));
            tbl.Columns.Add("NumericValue2", typeof(int));
            tbl.Columns.Add("NumericValue3", typeof(int));
            tbl.Columns.Add("NumericValue4", typeof(int));

            tbl.Columns.Add("TextValue1", typeof(string));
            tbl.Columns.Add("TextValue2", typeof(string));
            tbl.Columns.Add("TextValue3", typeof(string));
            tbl.Columns.Add("TextValue4", typeof(string));

            tbl.Columns.Add("BitValue1", typeof(bool));
            tbl.Columns.Add("BitValue2", typeof(bool));
            tbl.Columns.Add("BitValue3", typeof(bool));
            tbl.Columns.Add("BitValue4", typeof(bool));

            if (args != null && args.Any())
            {
                foreach (var val in args)
                {
                    DataRow row = tbl.NewRow();

                    //these syntaxes are not allowed
                    //row["NumericValue1"] = val.NumericValue1 ?? DBNull.Value;
                    //row["NumericValue1"] = val.NumericValue1 == null ? DBNull.Value : val.NumericValue1.Value;

                    //let's use a workaround
                    //if (val.NumericValue1 == null)
                    //    row["NumericValue1"] = DBNull.Value;
                    //else
                    //    row["NumericValue1"] = val.NumericValue1.Value;

                    row["NumericValue1"] = NullableValue(val.NumericValue1);
                    row["NumericValue2"] = NullableValue(val.NumericValue2);
                    row["NumericValue3"] = NullableValue(val.NumericValue3);
                    row["NumericValue4"] = NullableValue(val.NumericValue4);

                    //TEXTS (NVARCHARS)
                    row["TextValue1"] = NullableValue(val.TextValue1);
                    row["TextValue2"] = NullableValue(val.TextValue2);
                    row["TextValue3"] = NullableValue(val.TextValue3);
                    row["TextValue4"] = NullableValue(val.TextValue4);

                    //BOOLS (BITS)
                    row["BitValue1"] = NullableValue(val.BitValue1);
                    row["BitValue2"] = NullableValue(val.BitValue2);
                    row["BitValue3"] = NullableValue(val.BitValue3);
                    row["BitValue4"] = NullableValue(val.BitValue4);

                    tbl.Rows.Add(row);
                }
            }

            return tbl;
        }


        /// <summary>
        /// allows NULL values to be passed on to the SP, instead of basic type default values
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static DataTable ToGenericDatatableNullableArgs(this List<DefaultGenericTableType> args)
        {
            DataTable tbl = new DataTable();
            tbl.Columns.Add("NumericValue1", typeof(int));
            tbl.Columns.Add("NumericValue2", typeof(int));
            tbl.Columns.Add("NumericValue3", typeof(int));
            tbl.Columns.Add("NumericValue4", typeof(int));
            tbl.Columns.Add("NumericValue5", typeof(int));
            tbl.Columns.Add("NumericValue6", typeof(int));
            tbl.Columns.Add("NumericValue7", typeof(int));
            tbl.Columns.Add("NumericValue8", typeof(int));
            tbl.Columns.Add("NumericValue9", typeof(int));
            tbl.Columns.Add("NumericValue10", typeof(int));

            tbl.Columns.Add("TextValue1", typeof(string));
            tbl.Columns.Add("TextValue2", typeof(string));
            tbl.Columns.Add("TextValue3", typeof(string));
            tbl.Columns.Add("TextValue4", typeof(string));
            tbl.Columns.Add("TextValue5", typeof(string));
            tbl.Columns.Add("TextValue6", typeof(string));
            tbl.Columns.Add("TextValue7", typeof(string));
            tbl.Columns.Add("TextValue8", typeof(string));
            tbl.Columns.Add("TextValue9", typeof(string));
            tbl.Columns.Add("TextValue10", typeof(string));

            tbl.Columns.Add("BitValue1", typeof(bool));
            tbl.Columns.Add("BitValue2", typeof(bool));
            tbl.Columns.Add("BitValue3", typeof(bool));
            tbl.Columns.Add("BitValue4", typeof(bool));
            tbl.Columns.Add("BitValue5", typeof(bool));
            tbl.Columns.Add("BitValue6", typeof(bool));
            tbl.Columns.Add("BitValue7", typeof(bool));
            tbl.Columns.Add("BitValue8", typeof(bool));
            tbl.Columns.Add("BitValue9", typeof(bool));
            tbl.Columns.Add("BitValue10", typeof(bool));


            tbl.Columns.Add("MoneyValue1", typeof(decimal));
            tbl.Columns.Add("MoneyValue2", typeof(decimal));
            tbl.Columns.Add("MoneyValue3", typeof(decimal));
            tbl.Columns.Add("MoneyValue4", typeof(decimal));
            tbl.Columns.Add("MoneyValue5", typeof(decimal));
            tbl.Columns.Add("MoneyValue6", typeof(decimal));
            tbl.Columns.Add("MoneyValue7", typeof(decimal));
            tbl.Columns.Add("MoneyValue8", typeof(decimal));
            tbl.Columns.Add("MoneyValue9", typeof(decimal));
            tbl.Columns.Add("MoneyValue10", typeof(decimal));


            tbl.Columns.Add("DecimalValue1", typeof(decimal));
            tbl.Columns.Add("DecimalValue2", typeof(decimal));
            tbl.Columns.Add("DecimalValue3", typeof(decimal));
            tbl.Columns.Add("DecimalValue4", typeof(decimal));
            tbl.Columns.Add("DecimalValue5", typeof(decimal));
            tbl.Columns.Add("DecimalValue6", typeof(decimal));
            tbl.Columns.Add("DecimalValue7", typeof(decimal));
            tbl.Columns.Add("DecimalValue8", typeof(decimal));
            tbl.Columns.Add("DecimalValue9", typeof(decimal));
            tbl.Columns.Add("DecimalValue10", typeof(decimal));

            tbl.Columns.Add("DateValue1", typeof(DateTime));
            tbl.Columns.Add("DateValue2", typeof(DateTime));
            tbl.Columns.Add("DateValue3", typeof(DateTime));
            tbl.Columns.Add("DateValue4", typeof(DateTime));
            tbl.Columns.Add("DateValue5", typeof(DateTime));
            tbl.Columns.Add("DateValue6", typeof(DateTime));
            tbl.Columns.Add("DateValue7", typeof(DateTime));
            tbl.Columns.Add("DateValue8", typeof(DateTime));
            tbl.Columns.Add("DateValue9", typeof(DateTime));
            tbl.Columns.Add("DateValue10", typeof(DateTime));


            if (args != null && args.Any())
            {
                foreach (var val in args)
                {
                    DataRow row = tbl.NewRow();

                    //these syntaxes are not allowed
                    //row["NumericValue1"] = val.NumericValue1 ?? DBNull.Value;
                    //row["NumericValue1"] = val.NumericValue1 == null ? DBNull.Value : val.NumericValue1.Value;

                    //let's use a workaround
                    //if (val.NumericValue1 == null)
                    //    row["NumericValue1"] = DBNull.Value;
                    //else
                    //    row["NumericValue1"] = val.NumericValue1.Value;

                    row["NumericValue1"] = NullableValue(val.NumericValue1);
                    row["NumericValue2"] = NullableValue(val.NumericValue2);
                    row["NumericValue3"] = NullableValue(val.NumericValue3);
                    row["NumericValue4"] = NullableValue(val.NumericValue4);
                    row["NumericValue5"] = NullableValue(val.NumericValue5);
                    row["NumericValue6"] = NullableValue(val.NumericValue6);
                    row["NumericValue7"] = NullableValue(val.NumericValue7);
                    row["NumericValue8"] = NullableValue(val.NumericValue8);
                    row["NumericValue9"] = NullableValue(val.NumericValue9);
                    row["NumericValue10"] = NullableValue(val.NumericValue10);

                    //TEXTS (NVARCHARS)
                    row["TextValue1"] = NullableValue(val.TextValue1);
                    row["TextValue2"] = NullableValue(val.TextValue2);
                    row["TextValue3"] = NullableValue(val.TextValue3);
                    row["TextValue4"] = NullableValue(val.TextValue4);
                    row["TextValue5"] = NullableValue(val.TextValue5);
                    row["TextValue6"] = NullableValue(val.TextValue6);
                    row["TextValue7"] = NullableValue(val.TextValue7);
                    row["TextValue8"] = NullableValue(val.TextValue8);
                    row["TextValue9"] = NullableValue(val.TextValue9);
                    row["TextValue10"] = NullableValue(val.TextValue10);

                    //BOOLS (BITS)
                    row["BitValue1"] = NullableValue(val.BitValue1);
                    row["BitValue2"] = NullableValue(val.BitValue2);
                    row["BitValue3"] = NullableValue(val.BitValue3);
                    row["BitValue4"] = NullableValue(val.BitValue4);
                    row["BitValue5"] = NullableValue(val.BitValue5);
                    row["BitValue6"] = NullableValue(val.BitValue6);
                    row["BitValue7"] = NullableValue(val.BitValue7);
                    row["BitValue8"] = NullableValue(val.BitValue8);
                    row["BitValue9"] = NullableValue(val.BitValue9);
                    row["BitValue10"] = NullableValue(val.BitValue10);

                    //MONEY
                    row["MoneyValue1"] = NullableValue(val.MoneyValue1);
                    row["MoneyValue2"] = NullableValue(val.MoneyValue2);
                    row["MoneyValue3"] = NullableValue(val.MoneyValue3);
                    row["MoneyValue4"] = NullableValue(val.MoneyValue4);
                    row["MoneyValue5"] = NullableValue(val.MoneyValue5);
                    row["MoneyValue6"] = NullableValue(val.MoneyValue6);
                    row["MoneyValue7"] = NullableValue(val.MoneyValue7);
                    row["MoneyValue8"] = NullableValue(val.MoneyValue8);
                    row["MoneyValue9"] = NullableValue(val.MoneyValue9);
                    row["MoneyValue10"] = NullableValue(val.MoneyValue10);




                    //DECIMALS
                    row["DecimalValue1"] = NullableValue(val.DecimalValue1);
                    row["DecimalValue2"] = NullableValue(val.DecimalValue2);
                    row["DecimalValue3"] = NullableValue(val.DecimalValue3);
                    row["DecimalValue4"] = NullableValue(val.DecimalValue4);
                    row["DecimalValue5"] = NullableValue(val.DecimalValue5);
                    row["DecimalValue6"] = NullableValue(val.DecimalValue6);
                    row["DecimalValue7"] = NullableValue(val.DecimalValue7);
                    row["DecimalValue8"] = NullableValue(val.DecimalValue8);
                    row["DecimalValue9"] = NullableValue(val.DecimalValue9);
                    row["DecimalValue10"] = NullableValue(val.DecimalValue10);

                    //DATES
                    row["DateValue1"] = NullableValue(val.DateValue1);
                    row["DateValue2"] = NullableValue(val.DateValue2);
                    row["DateValue3"] = NullableValue(val.DateValue3);
                    row["DateValue4"] = NullableValue(val.DateValue4);
                    row["DateValue5"] = NullableValue(val.DateValue5);
                    row["DateValue6"] = NullableValue(val.DateValue6);
                    row["DateValue7"] = NullableValue(val.DateValue7);
                    row["DateValue8"] = NullableValue(val.DateValue8);
                    row["DateValue9"] = NullableValue(val.DateValue9);
                    row["DateValue10"] = NullableValue(val.DateValue10);


                    tbl.Rows.Add(row);
                }
            }

            return tbl;
        }

        [Obsolete("Use ToGenericDatatableNullableArgs instead. Please check SP first, that is using NULL evaluation instead of a basic type default value evaluation!")]
        public static DataTable ToGenericDatatableArgs(this List<GenericTableTypeWithMoney> args)
        {
            DataTable tbl = new DataTable();
            tbl.Columns.Add("NumericValue1", typeof(int));
            tbl.Columns.Add("NumericValue2", typeof(int));
            tbl.Columns.Add("NumericValue3", typeof(int));
            tbl.Columns.Add("NumericValue4", typeof(int));
            tbl.Columns.Add("TextValue1", typeof(string));
            tbl.Columns.Add("TextValue2", typeof(string));
            tbl.Columns.Add("TextValue3", typeof(string));
            tbl.Columns.Add("TextValue4", typeof(string));
            tbl.Columns.Add("BitValue1", typeof(bool));
            tbl.Columns.Add("BitValue2", typeof(bool));
            tbl.Columns.Add("BitValue3", typeof(bool));
            tbl.Columns.Add("BitValue4", typeof(bool));
            tbl.Columns.Add("MoneyValue1", typeof(decimal));
            tbl.Columns.Add("MoneyValue2", typeof(decimal));
            tbl.Columns.Add("MoneyValue3", typeof(decimal));
            tbl.Columns.Add("MoneyValue4", typeof(decimal));

            if (args != null && args.Any())
            {
                foreach (var val in args)
                {
                    DataRow row = tbl.NewRow();
                    row["NumericValue1"] = val.NumericValue1 ?? 0;
                    row["NumericValue2"] = val.NumericValue2 ?? 0;
                    row["NumericValue3"] = val.NumericValue3 ?? 0;
                    row["NumericValue4"] = val.NumericValue4 ?? 0;
                    row["TextValue1"] = val.TextValue1;
                    row["TextValue2"] = val.TextValue2;
                    row["TextValue3"] = val.TextValue3;
                    row["TextValue4"] = val.TextValue4;
                    row["BitValue1"] = val.BitValue1 ?? false;
                    row["BitValue2"] = val.BitValue2 ?? false;
                    row["BitValue3"] = val.BitValue3 ?? false;
                    row["BitValue4"] = val.BitValue4 ?? false;
                    row["MoneyValue1"] = val.MoneyValue1 ?? 0;
                    row["MoneyValue2"] = val.MoneyValue2 ?? 0;
                    row["MoneyValue3"] = val.MoneyValue3 ?? 0;
                    row["MoneyValue4"] = val.MoneyValue4 ?? 0;
                    tbl.Rows.Add(row);
                }
            }

            return tbl;
        }

        /// <summary>
        /// allows NULL values to be passed on to the SP, instead of basic type default values
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static DataTable ToGenericDatatableNullableArgs(this List<GenericTableTypeWithMoney> args)
        {
            DataTable tbl = new DataTable();

            tbl.Columns.Add("NumericValue1", typeof(int));
            tbl.Columns.Add("NumericValue2", typeof(int));
            tbl.Columns.Add("NumericValue3", typeof(int));
            tbl.Columns.Add("NumericValue4", typeof(int));

            tbl.Columns.Add("TextValue1", typeof(string));
            tbl.Columns.Add("TextValue2", typeof(string));
            tbl.Columns.Add("TextValue3", typeof(string));
            tbl.Columns.Add("TextValue4", typeof(string));

            tbl.Columns.Add("BitValue1", typeof(bool));
            tbl.Columns.Add("BitValue2", typeof(bool));
            tbl.Columns.Add("BitValue3", typeof(bool));
            tbl.Columns.Add("BitValue4", typeof(bool));

            tbl.Columns.Add("MoneyValue1", typeof(decimal));
            tbl.Columns.Add("MoneyValue2", typeof(decimal));
            tbl.Columns.Add("MoneyValue3", typeof(decimal));
            tbl.Columns.Add("MoneyValue4", typeof(decimal));

            if (args != null && args.Any())
            {
                foreach (var val in args)
                {
                    DataRow row = tbl.NewRow();

                    //NUMERIC (INTS)
                    row["NumericValue1"] = NullableValue(val.NumericValue1);
                    row["NumericValue2"] = NullableValue(val.NumericValue2);
                    row["NumericValue3"] = NullableValue(val.NumericValue3);
                    row["NumericValue4"] = NullableValue(val.NumericValue4);

                    //TEXTS (NVARCHARS)
                    row["TextValue1"] = NullableValue(val.TextValue1);
                    row["TextValue2"] = NullableValue(val.TextValue2);
                    row["TextValue3"] = NullableValue(val.TextValue3);
                    row["TextValue4"] = NullableValue(val.TextValue4);

                    //BOOLS (BITS)
                    row["BitValue1"] = NullableValue(val.BitValue1);
                    row["BitValue2"] = NullableValue(val.BitValue2);
                    row["BitValue3"] = NullableValue(val.BitValue3);
                    row["BitValue4"] = NullableValue(val.BitValue4);

                    //MONEY
                    row["MoneyValue1"] = NullableValue(val.MoneyValue1);
                    row["MoneyValue2"] = NullableValue(val.MoneyValue2);
                    row["MoneyValue3"] = NullableValue(val.MoneyValue3);
                    row["MoneyValue4"] = NullableValue(val.MoneyValue4);

                    tbl.Rows.Add(row);
                }
            }

            return tbl;
        }

        public static DataTable ToGenericDatatableNullableArgs(this List<BigGenericTableType> args)
        {
            DataTable tbl = new DataTable();

            tbl.Columns.Add("NumericValue1", typeof(int));
            tbl.Columns.Add("NumericValue2", typeof(int));
            tbl.Columns.Add("NumericValue3", typeof(int));
            tbl.Columns.Add("NumericValue4", typeof(int));
            tbl.Columns.Add("NumericValue5", typeof(int));
            tbl.Columns.Add("NumericValue6", typeof(int));
            tbl.Columns.Add("NumericValue7", typeof(int));
            tbl.Columns.Add("NumericValue8", typeof(int));
            tbl.Columns.Add("NumericValue9", typeof(int));
            tbl.Columns.Add("NumericValue10", typeof(int));
            tbl.Columns.Add("NumericValue11", typeof(int));
            tbl.Columns.Add("NumericValue12", typeof(int));
            tbl.Columns.Add("NumericValue13", typeof(int));
            tbl.Columns.Add("NumericValue14", typeof(int));
            tbl.Columns.Add("NumericValue15", typeof(int));
            tbl.Columns.Add("NumericValue16", typeof(int));
            tbl.Columns.Add("NumericValue17", typeof(int));
            tbl.Columns.Add("NumericValue18", typeof(int));
            tbl.Columns.Add("NumericValue19", typeof(int));
            tbl.Columns.Add("NumericValue20", typeof(int));

            tbl.Columns.Add("TextValue1", typeof(string));
            tbl.Columns.Add("TextValue2", typeof(string));
            tbl.Columns.Add("TextValue3", typeof(string));
            tbl.Columns.Add("TextValue4", typeof(string));
            tbl.Columns.Add("TextValue5", typeof(string));
            tbl.Columns.Add("TextValue6", typeof(string));
            tbl.Columns.Add("TextValue7", typeof(string));
            tbl.Columns.Add("TextValue8", typeof(string));

            tbl.Columns.Add("BitValue1", typeof(bool));
            tbl.Columns.Add("BitValue2", typeof(bool));
            tbl.Columns.Add("BitValue3", typeof(bool));
            tbl.Columns.Add("BitValue4", typeof(bool));
            tbl.Columns.Add("BitValue5", typeof(bool));
            tbl.Columns.Add("BitValue6", typeof(bool));
            tbl.Columns.Add("BitValue7", typeof(bool));
            tbl.Columns.Add("BitValue8", typeof(bool));

            tbl.Columns.Add("DecimalValue1", typeof(decimal));
            tbl.Columns.Add("DecimalValue2", typeof(decimal));
            tbl.Columns.Add("DecimalValue3", typeof(decimal));
            tbl.Columns.Add("DecimalValue4", typeof(decimal));
            tbl.Columns.Add("DecimalValue5", typeof(decimal));
            tbl.Columns.Add("DecimalValue6", typeof(decimal));
            tbl.Columns.Add("DecimalValue7", typeof(decimal));
            tbl.Columns.Add("DecimalValue8", typeof(decimal));

            tbl.Columns.Add("DateValue1", typeof(DateTime));
            tbl.Columns.Add("DateValue2", typeof(DateTime));
            tbl.Columns.Add("DateValue3", typeof(DateTime));
            tbl.Columns.Add("DateValue4", typeof(DateTime));
            tbl.Columns.Add("DateValue5", typeof(DateTime));
            tbl.Columns.Add("DateValue6", typeof(DateTime));
            tbl.Columns.Add("DateValue7", typeof(DateTime));
            tbl.Columns.Add("DateValue8", typeof(DateTime));

            if (args != null && args.Any())
            {
                foreach (var val in args)
                {
                    DataRow row = tbl.NewRow();

                    //NUMERIC (INTS)
                    row["NumericValue1"] = NullableValue(val.NumericValue1);
                    row["NumericValue2"] = NullableValue(val.NumericValue2);
                    row["NumericValue3"] = NullableValue(val.NumericValue3);
                    row["NumericValue4"] = NullableValue(val.NumericValue4);
                    row["NumericValue5"] = NullableValue(val.NumericValue5);
                    row["NumericValue6"] = NullableValue(val.NumericValue6);
                    row["NumericValue7"] = NullableValue(val.NumericValue7);
                    row["NumericValue8"] = NullableValue(val.NumericValue8);
                    row["NumericValue9"] = NullableValue(val.NumericValue9);
                    row["NumericValue10"] = NullableValue(val.NumericValue10);
                    row["NumericValue11"] = NullableValue(val.NumericValue11);
                    row["NumericValue12"] = NullableValue(val.NumericValue12);
                    row["NumericValue13"] = NullableValue(val.NumericValue13);
                    row["NumericValue14"] = NullableValue(val.NumericValue14);
                    row["NumericValue15"] = NullableValue(val.NumericValue15);
                    row["NumericValue16"] = NullableValue(val.NumericValue16);
                    row["NumericValue17"] = NullableValue(val.NumericValue17);
                    row["NumericValue18"] = NullableValue(val.NumericValue18);
                    row["NumericValue19"] = NullableValue(val.NumericValue19);
                    row["NumericValue20"] = NullableValue(val.NumericValue20);

                    //TEXTS (NVARCHARS)
                    row["TextValue1"] = NullableValue(val.TextValue1);
                    row["TextValue2"] = NullableValue(val.TextValue2);
                    row["TextValue3"] = NullableValue(val.TextValue3);
                    row["TextValue4"] = NullableValue(val.TextValue4);
                    row["TextValue5"] = NullableValue(val.TextValue5);
                    row["TextValue6"] = NullableValue(val.TextValue6);
                    row["TextValue7"] = NullableValue(val.TextValue7);
                    row["TextValue8"] = NullableValue(val.TextValue8);

                    //BOOLS (BITS)
                    row["BitValue1"] = NullableValue(val.BitValue1);
                    row["BitValue2"] = NullableValue(val.BitValue2);
                    row["BitValue3"] = NullableValue(val.BitValue3);
                    row["BitValue4"] = NullableValue(val.BitValue4);
                    row["BitValue5"] = NullableValue(val.BitValue5);
                    row["BitValue6"] = NullableValue(val.BitValue6);
                    row["BitValue7"] = NullableValue(val.BitValue7);
                    row["BitValue8"] = NullableValue(val.BitValue8);

                    //DECIMALS
                    row["DecimalValue1"] = NullableValue(val.DecimalValue1);
                    row["DecimalValue2"] = NullableValue(val.DecimalValue2);
                    row["DecimalValue3"] = NullableValue(val.DecimalValue3);
                    row["DecimalValue4"] = NullableValue(val.DecimalValue4);
                    row["DecimalValue5"] = NullableValue(val.DecimalValue5);
                    row["DecimalValue6"] = NullableValue(val.DecimalValue6);
                    row["DecimalValue7"] = NullableValue(val.DecimalValue7);
                    row["DecimalValue8"] = NullableValue(val.DecimalValue8);

                    //DATES
                    row["DateValue1"] = NullableValue(val.DateValue1);
                    row["DateValue2"] = NullableValue(val.DateValue2);
                    row["DateValue3"] = NullableValue(val.DateValue3);
                    row["DateValue4"] = NullableValue(val.DateValue4);
                    row["DateValue5"] = NullableValue(val.DateValue5);
                    row["DateValue6"] = NullableValue(val.DateValue6);
                    row["DateValue7"] = NullableValue(val.DateValue7);
                    row["DateValue8"] = NullableValue(val.DateValue8);

                    tbl.Rows.Add(row);
                }
            }

            return tbl;
        }

        public static DataTable ToGenericDatatableArgs(this IEnumerable<BigTextGenericTableType> args)
        {
            DataTable tbl = new DataTable();
            tbl.Columns.Add("NumericValue1", typeof(int));
            tbl.Columns.Add("NumericValue2", typeof(int));
            tbl.Columns.Add("NumericValue3", typeof(int));
            tbl.Columns.Add("NumericValue4", typeof(int));
            tbl.Columns.Add("NumericValue5", typeof(int));
            tbl.Columns.Add("TextValue1", typeof(string));
            tbl.Columns.Add("TextValue2", typeof(string));
            tbl.Columns.Add("TextValue3", typeof(string));
            tbl.Columns.Add("TextValue4", typeof(string));
            tbl.Columns.Add("TextValue5", typeof(string));
            tbl.Columns.Add("TextValue6", typeof(string));
            tbl.Columns.Add("TextValue7", typeof(string));
            tbl.Columns.Add("TextValue8", typeof(string));
            tbl.Columns.Add("TextValue9", typeof(string));
            tbl.Columns.Add("TextValue10", typeof(string));
            tbl.Columns.Add("TextValue11", typeof(string));
            tbl.Columns.Add("TextValue12", typeof(string));
            tbl.Columns.Add("TextValue13", typeof(string));
            tbl.Columns.Add("TextValue14", typeof(string));
            tbl.Columns.Add("TextValue15", typeof(string));
            tbl.Columns.Add("BitValue1", typeof(bool));
            tbl.Columns.Add("BitValue2", typeof(bool));
            tbl.Columns.Add("BitValue3", typeof(bool));
            tbl.Columns.Add("BitValue4", typeof(bool));
            tbl.Columns.Add("DecimalValue1", typeof(decimal));
            tbl.Columns.Add("DecimalValue2", typeof(decimal));
            tbl.Columns.Add("DecimalValue3", typeof(decimal));
            tbl.Columns.Add("DecimalValue4", typeof(decimal));
            tbl.Columns.Add("DecimalValue5", typeof(decimal));
            tbl.Columns.Add("DateValue1", typeof(DateTime));
            tbl.Columns.Add("DateValue2", typeof(DateTime));
            tbl.Columns.Add("DateValue3", typeof(DateTime));
            tbl.Columns.Add("DateValue4", typeof(DateTime));
            tbl.Columns.Add("DateValue5", typeof(DateTime));

            Func<object, object> fnValue = (value) => value ?? DBNull.Value;

            if (args != null && args.Any())
            {
                foreach (var val in args)
                {
                    DataRow row = tbl.NewRow();

                    row["NumericValue1"] = fnValue(val.NumericValue1);
                    row["NumericValue2"] = fnValue(val.NumericValue2);
                    row["NumericValue3"] = fnValue(val.NumericValue3);
                    row["NumericValue4"] = fnValue(val.NumericValue4);
                    row["NumericValue5"] = fnValue(val.NumericValue5);


                    row["TextValue1"] = fnValue(val.TextValue1);
                    row["TextValue2"] = fnValue(val.TextValue2);
                    row["TextValue3"] = fnValue(val.TextValue3);
                    row["TextValue4"] = fnValue(val.TextValue4);
                    row["TextValue5"] = fnValue(val.TextValue5);
                    row["TextValue6"] = fnValue(val.TextValue6);
                    row["TextValue7"] = fnValue(val.TextValue7);
                    row["TextValue8"] = fnValue(val.TextValue8);
                    row["TextValue9"] = fnValue(val.TextValue9);
                    row["TextValue10"] = fnValue(val.TextValue10);
                    row["TextValue11"] = fnValue(val.TextValue11);
                    row["TextValue12"] = fnValue(val.TextValue12);
                    row["TextValue13"] = fnValue(val.TextValue13);
                    row["TextValue14"] = fnValue(val.TextValue14);
                    row["TextValue15"] = fnValue(val.TextValue15);


                    row["BitValue1"] = fnValue(val.BitValue1);
                    row["BitValue2"] = fnValue(val.BitValue2);
                    row["BitValue3"] = fnValue(val.BitValue3);
                    row["BitValue4"] = fnValue(val.BitValue4);


                    row["DecimalValue1"] = fnValue(val.DecimalValue1);
                    row["DecimalValue2"] = fnValue(val.DecimalValue2);
                    row["DecimalValue3"] = fnValue(val.DecimalValue3);
                    row["DecimalValue4"] = fnValue(val.DecimalValue4);
                    row["DecimalValue5"] = fnValue(val.DecimalValue5);


                    row["DateValue1"] = fnValue(val.DateValue1);
                    row["DateValue2"] = fnValue(val.DateValue2);
                    row["DateValue3"] = fnValue(val.DateValue3);
                    row["DateValue4"] = fnValue(val.DateValue4);
                    row["DateValue5"] = fnValue(val.DateValue5);


                    tbl.Rows.Add(row);
                }
            }

            return tbl;
        }


        public static DataTable ToGenericDatatableNullableArgs(this List<BigTextGenericTableType> args)
        {
            DataTable tbl = new DataTable();
            tbl.Columns.Add("NumericValue1", typeof(int));
            tbl.Columns.Add("NumericValue2", typeof(int));
            tbl.Columns.Add("NumericValue3", typeof(int));
            tbl.Columns.Add("NumericValue4", typeof(int));
            tbl.Columns.Add("NumericValue5", typeof(int));
            tbl.Columns.Add("TextValue1", typeof(string));
            tbl.Columns.Add("TextValue2", typeof(string));
            tbl.Columns.Add("TextValue3", typeof(string));
            tbl.Columns.Add("TextValue4", typeof(string));
            tbl.Columns.Add("TextValue5", typeof(string));
            tbl.Columns.Add("TextValue6", typeof(string));
            tbl.Columns.Add("TextValue7", typeof(string));
            tbl.Columns.Add("TextValue8", typeof(string));
            tbl.Columns.Add("TextValue9", typeof(string));
            tbl.Columns.Add("TextValue10", typeof(string));
            tbl.Columns.Add("TextValue11", typeof(string));
            tbl.Columns.Add("TextValue12", typeof(string));
            tbl.Columns.Add("TextValue13", typeof(string));
            tbl.Columns.Add("TextValue14", typeof(string));
            tbl.Columns.Add("TextValue15", typeof(string));
            tbl.Columns.Add("BitValue1", typeof(bool));
            tbl.Columns.Add("BitValue2", typeof(bool));
            tbl.Columns.Add("BitValue3", typeof(bool));
            tbl.Columns.Add("BitValue4", typeof(bool));
            tbl.Columns.Add("DecimalValue1", typeof(decimal));
            tbl.Columns.Add("DecimalValue2", typeof(decimal));
            tbl.Columns.Add("DecimalValue3", typeof(decimal));
            tbl.Columns.Add("DecimalValue4", typeof(decimal));
            tbl.Columns.Add("DecimalValue5", typeof(decimal));
            tbl.Columns.Add("DateValue1", typeof(DateTime));
            tbl.Columns.Add("DateValue2", typeof(DateTime));
            tbl.Columns.Add("DateValue3", typeof(DateTime));
            tbl.Columns.Add("DateValue4", typeof(DateTime));
            tbl.Columns.Add("DateValue5", typeof(DateTime));

            if (args != null && args.Any())
            {
                foreach (var val in args)
                {
                    DataRow row = tbl.NewRow();


                    row["NumericValue1"] = NullableValue(val.NumericValue1);
                    row["NumericValue2"] = NullableValue(val.NumericValue2);
                    row["NumericValue3"] = NullableValue(val.NumericValue3);
                    row["NumericValue4"] = NullableValue(val.NumericValue4);
                    row["NumericValue5"] = NullableValue(val.NumericValue5);

                    //TEXTS (NVARCHARS)
                    row["TextValue1"] = NullableValue(val.TextValue1);
                    row["TextValue2"] = NullableValue(val.TextValue2);
                    row["TextValue3"] = NullableValue(val.TextValue3);
                    row["TextValue4"] = NullableValue(val.TextValue4);
                    row["TextValue5"] = NullableValue(val.TextValue5);
                    row["TextValue6"] = NullableValue(val.TextValue6);
                    row["TextValue7"] = NullableValue(val.TextValue7);
                    row["TextValue8"] = NullableValue(val.TextValue8);
                    row["TextValue9"] = NullableValue(val.TextValue9);
                    row["TextValue10"] = NullableValue(val.TextValue10);
                    row["TextValue11"] = NullableValue(val.TextValue11);
                    row["TextValue12"] = NullableValue(val.TextValue12);
                    row["TextValue13"] = NullableValue(val.TextValue13);
                    row["TextValue14"] = NullableValue(val.TextValue14);
                    row["TextValue15"] = NullableValue(val.TextValue15);

                    //BOOLS (BITS)
                    row["BitValue1"] = NullableValue(val.BitValue1);
                    row["BitValue2"] = NullableValue(val.BitValue2);
                    row["BitValue3"] = NullableValue(val.BitValue3);
                    row["BitValue4"] = NullableValue(val.BitValue4);


                    //DECIMALS
                    row["DecimalValue1"] = NullableValue(val.DecimalValue1);
                    row["DecimalValue2"] = NullableValue(val.DecimalValue2);
                    row["DecimalValue3"] = NullableValue(val.DecimalValue3);
                    row["DecimalValue4"] = NullableValue(val.DecimalValue4);
                    row["DecimalValue5"] = NullableValue(val.DecimalValue5);

                    //DATES
                    row["DateValue1"] = NullableValue(val.DateValue1);
                    row["DateValue2"] = NullableValue(val.DateValue2);
                    row["DateValue3"] = NullableValue(val.DateValue3);
                    row["DateValue4"] = NullableValue(val.DateValue4);
                    row["DateValue5"] = NullableValue(val.DateValue5);

                    tbl.Rows.Add(row);
                }
            }

            return tbl;
        }


        //GenericDateAndTimeTableType
        public static DataTable ToGenericDateAndTimeTableTypeArgs(this List<GenericDateAndTimeTableType> args)
        {
            DataTable tbl = new DataTable();
            tbl.Columns.Add("NumericValue1", typeof(int));
            tbl.Columns.Add("NumericValue2", typeof(int));
            tbl.Columns.Add("NumericValue3", typeof(int));
            tbl.Columns.Add("NumericValue4", typeof(int));
            tbl.Columns.Add("TextValue1", typeof(string));
            tbl.Columns.Add("TextValue2", typeof(string));
            tbl.Columns.Add("TextValue3", typeof(string));
            tbl.Columns.Add("TextValue4", typeof(string));
            tbl.Columns.Add("BitValue1", typeof(bool));
            tbl.Columns.Add("BitValue2", typeof(bool));
            tbl.Columns.Add("BitValue3", typeof(bool));
            tbl.Columns.Add("BitValue4", typeof(bool));
            tbl.Columns.Add("DateValue1", typeof(DateTime));
            tbl.Columns.Add("DateValue2", typeof(DateTime));
            tbl.Columns.Add("DateValue3", typeof(DateTime));
            tbl.Columns.Add("DateValue4", typeof(DateTime));
            tbl.Columns.Add("TimeValue1", typeof(TimeSpan));
            tbl.Columns.Add("TimeValue2", typeof(TimeSpan));
            tbl.Columns.Add("TimeValue3", typeof(TimeSpan));
            tbl.Columns.Add("TimeValue4", typeof(TimeSpan));

            if (args != null && args.Any())
            {
                foreach (var val in args)
                {
                    DataRow row = tbl.NewRow();
                    row["NumericValue1"] = val.NumericValue1 ?? 0;
                    row["NumericValue2"] = val.NumericValue2 ?? 0;
                    row["NumericValue3"] = val.NumericValue3 ?? 0;
                    row["NumericValue4"] = val.NumericValue4 ?? 0;
                    row["TextValue1"] = val.TextValue1 ?? "";
                    row["TextValue2"] = val.TextValue2 ?? "";
                    row["TextValue3"] = val.TextValue3 ?? "";
                    row["TextValue4"] = val.TextValue4 ?? "";
                    row["BitValue1"] = val.BitValue1 ?? false;
                    row["BitValue2"] = val.BitValue2 ?? false;
                    row["BitValue3"] = val.BitValue3 ?? false;
                    row["BitValue4"] = val.BitValue4 ?? false;
                    row["DateValue1"] = NullableValue(val.DateValue1);
                    row["DateValue2"] = NullableValue(val.DateValue2);
                    row["DateValue3"] = NullableValue(val.DateValue3);
                    row["DateValue4"] = NullableValue(val.DateValue4);
                    row["TimeValue1"] = NullableValue(val.TimeValue1);
                    row["TimeValue2"] = NullableValue(val.TimeValue2);
                    row["TimeValue3"] = NullableValue(val.TimeValue3);
                    row["TimeValue4"] = NullableValue(val.TimeValue4);

                    tbl.Rows.Add(row);
                }
            }

            return tbl;
        }

        public static DataTable ToGenericDatatableNullableArgs(this List<BigTextTwentyGenericTableType> args)
        {
            DataTable tbl = new DataTable();
            tbl.Columns.Add("NumericValue1", typeof(int));
            tbl.Columns.Add("NumericValue2", typeof(int));
            tbl.Columns.Add("NumericValue3", typeof(int));
            tbl.Columns.Add("NumericValue4", typeof(int));
            tbl.Columns.Add("NumericValue5", typeof(int));
            tbl.Columns.Add("TextValue1", typeof(string));
            tbl.Columns.Add("TextValue2", typeof(string));
            tbl.Columns.Add("TextValue3", typeof(string));
            tbl.Columns.Add("TextValue4", typeof(string));
            tbl.Columns.Add("TextValue5", typeof(string));
            tbl.Columns.Add("TextValue6", typeof(string));
            tbl.Columns.Add("TextValue7", typeof(string));
            tbl.Columns.Add("TextValue8", typeof(string));
            tbl.Columns.Add("TextValue9", typeof(string));
            tbl.Columns.Add("TextValue10", typeof(string));
            tbl.Columns.Add("TextValue11", typeof(string));
            tbl.Columns.Add("TextValue12", typeof(string));
            tbl.Columns.Add("TextValue13", typeof(string));
            tbl.Columns.Add("TextValue14", typeof(string));
            tbl.Columns.Add("TextValue15", typeof(string));
            tbl.Columns.Add("TextValue16", typeof(string));
            tbl.Columns.Add("TextValue17", typeof(string));
            tbl.Columns.Add("TextValue18", typeof(string));
            tbl.Columns.Add("TextValue19", typeof(string));
            tbl.Columns.Add("TextValue20", typeof(string));
            tbl.Columns.Add("BitValue1", typeof(bool));
            tbl.Columns.Add("BitValue2", typeof(bool));
            tbl.Columns.Add("BitValue3", typeof(bool));
            tbl.Columns.Add("BitValue4", typeof(bool));
            tbl.Columns.Add("BitValue5", typeof(bool));
            tbl.Columns.Add("BitValue6", typeof(bool));
            tbl.Columns.Add("BitValue7", typeof(bool));
            tbl.Columns.Add("BitValue8", typeof(bool));
            tbl.Columns.Add("BitValue9", typeof(bool));
            tbl.Columns.Add("BitValue10", typeof(bool));
            tbl.Columns.Add("BitValue11", typeof(bool));
            tbl.Columns.Add("BitValue12", typeof(bool));
            tbl.Columns.Add("DecimalValue1", typeof(decimal));
            tbl.Columns.Add("DecimalValue2", typeof(decimal));
            tbl.Columns.Add("DecimalValue3", typeof(decimal));
            tbl.Columns.Add("DecimalValue4", typeof(decimal));
            tbl.Columns.Add("DecimalValue5", typeof(decimal));
            tbl.Columns.Add("DateValue1", typeof(DateTime));
            tbl.Columns.Add("DateValue2", typeof(DateTime));
            tbl.Columns.Add("DateValue3", typeof(DateTime));
            tbl.Columns.Add("DateValue4", typeof(DateTime));
            tbl.Columns.Add("DateValue5", typeof(DateTime));

            if (args != null && args.Any())
            {
                foreach (var val in args)
                {
                    DataRow row = tbl.NewRow();


                    row["NumericValue1"] = NullableValue(val.NumericValue1);
                    row["NumericValue2"] = NullableValue(val.NumericValue2);
                    row["NumericValue3"] = NullableValue(val.NumericValue3);
                    row["NumericValue4"] = NullableValue(val.NumericValue4);
                    row["NumericValue5"] = NullableValue(val.NumericValue5);

                    //TEXTS (NVARCHARS)
                    row["TextValue1"] = NullableValue(val.TextValue1);
                    row["TextValue2"] = NullableValue(val.TextValue2);
                    row["TextValue3"] = NullableValue(val.TextValue3);
                    row["TextValue4"] = NullableValue(val.TextValue4);
                    row["TextValue5"] = NullableValue(val.TextValue5);
                    row["TextValue6"] = NullableValue(val.TextValue6);
                    row["TextValue7"] = NullableValue(val.TextValue7);
                    row["TextValue8"] = NullableValue(val.TextValue8);
                    row["TextValue9"] = NullableValue(val.TextValue9);
                    row["TextValue10"] = NullableValue(val.TextValue10);
                    row["TextValue11"] = NullableValue(val.TextValue11);
                    row["TextValue12"] = NullableValue(val.TextValue12);
                    row["TextValue13"] = NullableValue(val.TextValue13);
                    row["TextValue14"] = NullableValue(val.TextValue14);
                    row["TextValue15"] = NullableValue(val.TextValue15);
                    row["TextValue16"] = NullableValue(val.TextValue16);
                    row["TextValue17"] = NullableValue(val.TextValue17);
                    row["TextValue18"] = NullableValue(val.TextValue18);
                    row["TextValue19"] = NullableValue(val.TextValue19);
                    row["TextValue20"] = NullableValue(val.TextValue20);

                    //BOOLS (BITS)
                    row["BitValue1"] = NullableValue(val.BitValue1);
                    row["BitValue2"] = NullableValue(val.BitValue2);
                    row["BitValue3"] = NullableValue(val.BitValue3);
                    row["BitValue4"] = NullableValue(val.BitValue4);
                    row["BitValue5"] = NullableValue(val.BitValue5);
                    row["BitValue6"] = NullableValue(val.BitValue6);
                    row["BitValue7"] = NullableValue(val.BitValue7);
                    row["BitValue8"] = NullableValue(val.BitValue8);
                    row["BitValue9"] = NullableValue(val.BitValue9);
                    row["BitValue10"] = NullableValue(val.BitValue10);
                    row["BitValue11"] = NullableValue(val.BitValue11);
                    row["BitValue12"] = NullableValue(val.BitValue12);


                    //DECIMALS
                    row["DecimalValue1"] = NullableValue(val.DecimalValue1);
                    row["DecimalValue2"] = NullableValue(val.DecimalValue2);
                    row["DecimalValue3"] = NullableValue(val.DecimalValue3);
                    row["DecimalValue4"] = NullableValue(val.DecimalValue4);
                    row["DecimalValue5"] = NullableValue(val.DecimalValue5);

                    //DATES
                    row["DateValue1"] = NullableValue(val.DateValue1);
                    row["DateValue2"] = NullableValue(val.DateValue2);
                    row["DateValue3"] = NullableValue(val.DateValue3);
                    row["DateValue4"] = NullableValue(val.DateValue4);
                    row["DateValue5"] = NullableValue(val.DateValue5);

                    tbl.Rows.Add(row);
                }
            }

            return tbl;
        }
        public static DataTable ToGenericDatatableWithDecimalNullableArgs(this List<GenericTableTypeWithDecimal> args)
        {
            DataTable tbl = new DataTable();

            tbl.Columns.Add("NumericValue1", typeof(int));
            tbl.Columns.Add("NumericValue2", typeof(int));
            tbl.Columns.Add("NumericValue3", typeof(int));
            tbl.Columns.Add("NumericValue4", typeof(int));

            tbl.Columns.Add("TextValue1", typeof(string));
            tbl.Columns.Add("TextValue2", typeof(string));
            tbl.Columns.Add("TextValue3", typeof(string));
            tbl.Columns.Add("TextValue4", typeof(string));

            tbl.Columns.Add("BitValue1", typeof(bool));
            tbl.Columns.Add("BitValue2", typeof(bool));
            tbl.Columns.Add("BitValue3", typeof(bool));
            tbl.Columns.Add("BitValue4", typeof(bool));

            tbl.Columns.Add("DecimalValue1", typeof(decimal));
            tbl.Columns.Add("DecimalValue2", typeof(decimal));
            tbl.Columns.Add("DecimalValue3", typeof(decimal));
            tbl.Columns.Add("DecimalValue4", typeof(decimal));

            if (args != null && args.Any())
            {
                foreach (var val in args)
                {
                    DataRow row = tbl.NewRow();

                    //NUMERIC (INTS)
                    row["NumericValue1"] = NullableValue(val.NumericValue1);
                    row["NumericValue2"] = NullableValue(val.NumericValue2);
                    row["NumericValue3"] = NullableValue(val.NumericValue3);
                    row["NumericValue4"] = NullableValue(val.NumericValue4);

                    //TEXTS (NVARCHARS)
                    row["TextValue1"] = NullableValue(val.TextValue1);
                    row["TextValue2"] = NullableValue(val.TextValue2);
                    row["TextValue3"] = NullableValue(val.TextValue3);
                    row["TextValue4"] = NullableValue(val.TextValue4);

                    //BOOLS (BITS)
                    row["BitValue1"] = NullableValue(val.BitValue1);
                    row["BitValue2"] = NullableValue(val.BitValue2);
                    row["BitValue3"] = NullableValue(val.BitValue3);
                    row["BitValue4"] = NullableValue(val.BitValue4);

                    //DECIMALS
                    row["DecimalValue1"] = NullableValue(val.DecimalValue1);
                    row["DecimalValue2"] = NullableValue(val.DecimalValue2);
                    row["DecimalValue3"] = NullableValue(val.DecimalValue3);
                    row["DecimalValue4"] = NullableValue(val.DecimalValue4);

                    tbl.Rows.Add(row);
                }
            }

            return tbl;
        }


        public static DataTable ToGenericDatatableNullableArgs(this List<BigMoneyGenericTableType> args)
        {
            DataTable tbl = new DataTable();

            tbl.Columns.Add("NumericValue1", typeof(int));
            tbl.Columns.Add("NumericValue2", typeof(int));
            tbl.Columns.Add("NumericValue3", typeof(int));
            tbl.Columns.Add("NumericValue4", typeof(int));
            tbl.Columns.Add("NumericValue5", typeof(int));
            tbl.Columns.Add("NumericValue6", typeof(int));
            tbl.Columns.Add("NumericValue7", typeof(int));
            tbl.Columns.Add("NumericValue8", typeof(int));
            tbl.Columns.Add("NumericValue9", typeof(int));
            tbl.Columns.Add("NumericValue10", typeof(int));

            tbl.Columns.Add("TextValue1", typeof(string));
            tbl.Columns.Add("TextValue2", typeof(string));
            tbl.Columns.Add("TextValue3", typeof(string));
            tbl.Columns.Add("TextValue4", typeof(string));
            tbl.Columns.Add("TextValue5", typeof(string));
            tbl.Columns.Add("TextValue6", typeof(string));
            tbl.Columns.Add("TextValue7", typeof(string));
            tbl.Columns.Add("TextValue8", typeof(string));
            tbl.Columns.Add("TextValue9", typeof(string));
            tbl.Columns.Add("TextValue10", typeof(string));

            tbl.Columns.Add("BitValue1", typeof(bool));
            tbl.Columns.Add("BitValue2", typeof(bool));
            tbl.Columns.Add("BitValue3", typeof(bool));
            tbl.Columns.Add("BitValue4", typeof(bool));
            tbl.Columns.Add("BitValue5", typeof(bool));

            tbl.Columns.Add("DecimalValue1", typeof(decimal));
            tbl.Columns.Add("DecimalValue2", typeof(decimal));
            tbl.Columns.Add("DecimalValue3", typeof(decimal));
            tbl.Columns.Add("DecimalValue4", typeof(decimal));
            tbl.Columns.Add("DecimalValue5", typeof(decimal));
            tbl.Columns.Add("DecimalValue6", typeof(decimal));
            tbl.Columns.Add("DecimalValue7", typeof(decimal));
            tbl.Columns.Add("DecimalValue8", typeof(decimal));
            tbl.Columns.Add("DecimalValue9", typeof(decimal));
            tbl.Columns.Add("DecimalValue10", typeof(decimal));

            tbl.Columns.Add("DateValue1", typeof(DateTime));
            tbl.Columns.Add("DateValue2", typeof(DateTime));
            tbl.Columns.Add("DateValue3", typeof(DateTime));
            tbl.Columns.Add("DateValue4", typeof(DateTime));
            tbl.Columns.Add("DateValue5", typeof(DateTime));


            tbl.Columns.Add("MoneyValue1", typeof(decimal));
            tbl.Columns.Add("MoneyValue2", typeof(decimal));
            tbl.Columns.Add("MoneyValue3", typeof(decimal));
            tbl.Columns.Add("MoneyValue4", typeof(decimal));
            tbl.Columns.Add("MoneyValue5", typeof(decimal));
            tbl.Columns.Add("MoneyValue6", typeof(decimal));
            tbl.Columns.Add("MoneyValue7", typeof(decimal));
            tbl.Columns.Add("MoneyValue8", typeof(decimal));
            tbl.Columns.Add("MoneyValue9", typeof(decimal));
            tbl.Columns.Add("MoneyValue10", typeof(decimal));


            if (args != null && args.Any())
            {
                foreach (var val in args)
                {
                    DataRow row = tbl.NewRow();

                    //NUMERIC (INTS)
                    row["NumericValue1"] = NullableValue(val.NumericValue1);
                    row["NumericValue2"] = NullableValue(val.NumericValue2);
                    row["NumericValue3"] = NullableValue(val.NumericValue3);
                    row["NumericValue4"] = NullableValue(val.NumericValue4);
                    row["NumericValue5"] = NullableValue(val.NumericValue5);
                    row["NumericValue6"] = NullableValue(val.NumericValue6);
                    row["NumericValue7"] = NullableValue(val.NumericValue7);
                    row["NumericValue8"] = NullableValue(val.NumericValue8);
                    row["NumericValue9"] = NullableValue(val.NumericValue9);
                    row["NumericValue10"] = NullableValue(val.NumericValue10);

                    //TEXTS (NVARCHARS)
                    row["TextValue1"] = NullableValue(val.TextValue1);
                    row["TextValue2"] = NullableValue(val.TextValue2);
                    row["TextValue3"] = NullableValue(val.TextValue3);
                    row["TextValue4"] = NullableValue(val.TextValue4);
                    row["TextValue5"] = NullableValue(val.TextValue5);
                    row["TextValue6"] = NullableValue(val.TextValue6);
                    row["TextValue7"] = NullableValue(val.TextValue7);
                    row["TextValue8"] = NullableValue(val.TextValue8);
                    row["TextValue9"] = NullableValue(val.TextValue9);
                    row["TextValue10"] = NullableValue(val.TextValue10);

                    //BOOLS (BITS)
                    row["BitValue1"] = NullableValue(val.BitValue1);
                    row["BitValue2"] = NullableValue(val.BitValue2);
                    row["BitValue3"] = NullableValue(val.BitValue3);
                    row["BitValue4"] = NullableValue(val.BitValue4);
                    row["BitValue5"] = NullableValue(val.BitValue5);

                    //DECIMALS
                    row["DecimalValue1"] = NullableValue(val.DecimalValue1);
                    row["DecimalValue2"] = NullableValue(val.DecimalValue2);
                    row["DecimalValue3"] = NullableValue(val.DecimalValue3);
                    row["DecimalValue4"] = NullableValue(val.DecimalValue4);
                    row["DecimalValue5"] = NullableValue(val.DecimalValue5);
                    row["DecimalValue6"] = NullableValue(val.DecimalValue6);
                    row["DecimalValue7"] = NullableValue(val.DecimalValue7);
                    row["DecimalValue8"] = NullableValue(val.DecimalValue8);
                    row["DecimalValue9"] = NullableValue(val.DecimalValue9);
                    row["DecimalValue10"] = NullableValue(val.DecimalValue10);

                    //DATES
                    row["DateValue1"] = NullableValue(val.DateValue1);
                    row["DateValue2"] = NullableValue(val.DateValue2);
                    row["DateValue3"] = NullableValue(val.DateValue3);
                    row["DateValue4"] = NullableValue(val.DateValue4);
                    row["DateValue5"] = NullableValue(val.DateValue5);

                    //MONEY
                    row["MoneyValue1"] = NullableValue(val.MoneyValue1);
                    row["MoneyValue2"] = NullableValue(val.MoneyValue2);
                    row["MoneyValue3"] = NullableValue(val.MoneyValue3);
                    row["MoneyValue4"] = NullableValue(val.MoneyValue4);
                    row["MoneyValue5"] = NullableValue(val.MoneyValue5);
                    row["MoneyValue6"] = NullableValue(val.MoneyValue6);
                    row["MoneyValue7"] = NullableValue(val.MoneyValue7);
                    row["MoneyValue8"] = NullableValue(val.MoneyValue8);
                    row["MoneyValue9"] = NullableValue(val.MoneyValue9);
                    row["MoneyValue10"] = NullableValue(val.MoneyValue10);

                    tbl.Rows.Add(row);
                }
            }

            return tbl;
        }



        public static DataTable ToGenericDatatableNullableArgs(this List<GenericTwentyTableType> args)
        {
            DataTable tbl = new DataTable();

            tbl.Columns.Add("NumericValue1", typeof(int));
            tbl.Columns.Add("NumericValue2", typeof(int));
            tbl.Columns.Add("NumericValue3", typeof(int));
            tbl.Columns.Add("NumericValue4", typeof(int));
            tbl.Columns.Add("NumericValue5", typeof(int));
            tbl.Columns.Add("NumericValue6", typeof(int));
            tbl.Columns.Add("NumericValue7", typeof(int));
            tbl.Columns.Add("NumericValue8", typeof(int));
            tbl.Columns.Add("NumericValue9", typeof(int));
            tbl.Columns.Add("NumericValue10", typeof(int));
            tbl.Columns.Add("NumericValue11", typeof(int));
            tbl.Columns.Add("NumericValue12", typeof(int));
            tbl.Columns.Add("NumericValue13", typeof(int));
            tbl.Columns.Add("NumericValue14", typeof(int));
            tbl.Columns.Add("NumericValue15", typeof(int));
            tbl.Columns.Add("NumericValue16", typeof(int));
            tbl.Columns.Add("NumericValue17", typeof(int));
            tbl.Columns.Add("NumericValue18", typeof(int));
            tbl.Columns.Add("NumericValue19", typeof(int));
            tbl.Columns.Add("NumericValue20", typeof(int));

            tbl.Columns.Add("TextValue1", typeof(string));
            tbl.Columns.Add("TextValue2", typeof(string));
            tbl.Columns.Add("TextValue3", typeof(string));
            tbl.Columns.Add("TextValue4", typeof(string));
            tbl.Columns.Add("TextValue5", typeof(string));
            tbl.Columns.Add("TextValue6", typeof(string));
            tbl.Columns.Add("TextValue7", typeof(string));
            tbl.Columns.Add("TextValue8", typeof(string));
            tbl.Columns.Add("TextValue9", typeof(string));
            tbl.Columns.Add("TextValue10", typeof(string));
            tbl.Columns.Add("TextValue11", typeof(string));
            tbl.Columns.Add("TextValue12", typeof(string));
            tbl.Columns.Add("TextValue13", typeof(string));
            tbl.Columns.Add("TextValue14", typeof(string));
            tbl.Columns.Add("TextValue15", typeof(string));
            tbl.Columns.Add("TextValue16", typeof(string));
            tbl.Columns.Add("TextValue17", typeof(string));
            tbl.Columns.Add("TextValue18", typeof(string));
            tbl.Columns.Add("TextValue19", typeof(string));
            tbl.Columns.Add("TextValue20", typeof(string));

            tbl.Columns.Add("BitValue1", typeof(bool));
            tbl.Columns.Add("BitValue2", typeof(bool));
            tbl.Columns.Add("BitValue3", typeof(bool));
            tbl.Columns.Add("BitValue4", typeof(bool));
            tbl.Columns.Add("BitValue5", typeof(bool));
            tbl.Columns.Add("BitValue6", typeof(bool));
            tbl.Columns.Add("BitValue7", typeof(bool));
            tbl.Columns.Add("BitValue8", typeof(bool));
            tbl.Columns.Add("BitValue9", typeof(bool));
            tbl.Columns.Add("BitValue10", typeof(bool));
            tbl.Columns.Add("BitValue11", typeof(bool));
            tbl.Columns.Add("BitValue12", typeof(bool));
            tbl.Columns.Add("BitValue13", typeof(bool));
            tbl.Columns.Add("BitValue14", typeof(bool));
            tbl.Columns.Add("BitValue15", typeof(bool));
            tbl.Columns.Add("BitValue16", typeof(bool));
            tbl.Columns.Add("BitValue17", typeof(bool));
            tbl.Columns.Add("BitValue18", typeof(bool));
            tbl.Columns.Add("BitValue19", typeof(bool));
            tbl.Columns.Add("BitValue20", typeof(bool));

            tbl.Columns.Add("DecimalValue1", typeof(decimal));
            tbl.Columns.Add("DecimalValue2", typeof(decimal));
            tbl.Columns.Add("DecimalValue3", typeof(decimal));
            tbl.Columns.Add("DecimalValue4", typeof(decimal));
            tbl.Columns.Add("DecimalValue5", typeof(decimal));
            tbl.Columns.Add("DecimalValue6", typeof(decimal));
            tbl.Columns.Add("DecimalValue7", typeof(decimal));
            tbl.Columns.Add("DecimalValue8", typeof(decimal));
            tbl.Columns.Add("DecimalValue9", typeof(decimal));
            tbl.Columns.Add("DecimalValue10", typeof(decimal));
            tbl.Columns.Add("DecimalValue11", typeof(decimal));
            tbl.Columns.Add("DecimalValue12", typeof(decimal));
            tbl.Columns.Add("DecimalValue13", typeof(decimal));
            tbl.Columns.Add("DecimalValue14", typeof(decimal));
            tbl.Columns.Add("DecimalValue15", typeof(decimal));
            tbl.Columns.Add("DecimalValue16", typeof(decimal));
            tbl.Columns.Add("DecimalValue17", typeof(decimal));
            tbl.Columns.Add("DecimalValue18", typeof(decimal));
            tbl.Columns.Add("DecimalValue19", typeof(decimal));
            tbl.Columns.Add("DecimalValue20", typeof(decimal));

            tbl.Columns.Add("DateValue1", typeof(DateTime));
            tbl.Columns.Add("DateValue2", typeof(DateTime));
            tbl.Columns.Add("DateValue3", typeof(DateTime));
            tbl.Columns.Add("DateValue4", typeof(DateTime));
            tbl.Columns.Add("DateValue5", typeof(DateTime));
            tbl.Columns.Add("DateValue6", typeof(DateTime));
            tbl.Columns.Add("DateValue7", typeof(DateTime));
            tbl.Columns.Add("DateValue8", typeof(DateTime));
            tbl.Columns.Add("DateValue9", typeof(DateTime));
            tbl.Columns.Add("DateValue10", typeof(DateTime));
            tbl.Columns.Add("DateValue11", typeof(DateTime));
            tbl.Columns.Add("DateValue12", typeof(DateTime));
            tbl.Columns.Add("DateValue13", typeof(DateTime));
            tbl.Columns.Add("DateValue14", typeof(DateTime));
            tbl.Columns.Add("DateValue15", typeof(DateTime));
            tbl.Columns.Add("DateValue16", typeof(DateTime));
            tbl.Columns.Add("DateValue17", typeof(DateTime));
            tbl.Columns.Add("DateValue18", typeof(DateTime));
            tbl.Columns.Add("DateValue19", typeof(DateTime));
            tbl.Columns.Add("DateValue20", typeof(DateTime));





            if (args != null && args.Any())
            {
                foreach (var val in args)
                {
                    DataRow row = tbl.NewRow();

                    //NUMERIC (INTS)
                    row["NumericValue1"] = NullableValue(val.NumericValue1);
                    row["NumericValue2"] = NullableValue(val.NumericValue2);
                    row["NumericValue3"] = NullableValue(val.NumericValue3);
                    row["NumericValue4"] = NullableValue(val.NumericValue4);
                    row["NumericValue5"] = NullableValue(val.NumericValue5);
                    row["NumericValue6"] = NullableValue(val.NumericValue6);
                    row["NumericValue7"] = NullableValue(val.NumericValue7);
                    row["NumericValue8"] = NullableValue(val.NumericValue8);
                    row["NumericValue9"] = NullableValue(val.NumericValue9);
                    row["NumericValue10"] = NullableValue(val.NumericValue10);
                    row["NumericValue11"] = NullableValue(val.NumericValue11);
                    row["NumericValue12"] = NullableValue(val.NumericValue12);
                    row["NumericValue13"] = NullableValue(val.NumericValue13);
                    row["NumericValue14"] = NullableValue(val.NumericValue14);
                    row["NumericValue15"] = NullableValue(val.NumericValue15);
                    row["NumericValue16"] = NullableValue(val.NumericValue16);
                    row["NumericValue17"] = NullableValue(val.NumericValue17);
                    row["NumericValue18"] = NullableValue(val.NumericValue18);
                    row["NumericValue19"] = NullableValue(val.NumericValue19);
                    row["NumericValue20"] = NullableValue(val.NumericValue20);

                    //TEXTS (NVARCHARS)
                    row["TextValue1"] = NullableValue(val.TextValue1);
                    row["TextValue2"] = NullableValue(val.TextValue2);
                    row["TextValue3"] = NullableValue(val.TextValue3);
                    row["TextValue4"] = NullableValue(val.TextValue4);
                    row["TextValue5"] = NullableValue(val.TextValue5);
                    row["TextValue6"] = NullableValue(val.TextValue6);
                    row["TextValue7"] = NullableValue(val.TextValue7);
                    row["TextValue8"] = NullableValue(val.TextValue8);
                    row["TextValue9"] = NullableValue(val.TextValue9);
                    row["TextValue10"] = NullableValue(val.TextValue10);
                    row["TextValue11"] = NullableValue(val.TextValue11);
                    row["TextValue12"] = NullableValue(val.TextValue12);
                    row["TextValue13"] = NullableValue(val.TextValue13);
                    row["TextValue14"] = NullableValue(val.TextValue14);
                    row["TextValue15"] = NullableValue(val.TextValue15);
                    row["TextValue16"] = NullableValue(val.TextValue16);
                    row["TextValue17"] = NullableValue(val.TextValue17);
                    row["TextValue18"] = NullableValue(val.TextValue18);
                    row["TextValue19"] = NullableValue(val.TextValue19);
                    row["TextValue20"] = NullableValue(val.TextValue20);

                    //BOOLS (BITS)
                    row["BitValue1"] = NullableValue(val.BitValue1);
                    row["BitValue2"] = NullableValue(val.BitValue2);
                    row["BitValue3"] = NullableValue(val.BitValue3);
                    row["BitValue4"] = NullableValue(val.BitValue4);
                    row["BitValue5"] = NullableValue(val.BitValue5);
                    row["BitValue6"] = NullableValue(val.BitValue6);
                    row["BitValue7"] = NullableValue(val.BitValue7);
                    row["BitValue8"] = NullableValue(val.BitValue8);
                    row["BitValue9"] = NullableValue(val.BitValue9);
                    row["BitValue10"] = NullableValue(val.BitValue10);
                    row["BitValue11"] = NullableValue(val.BitValue11);
                    row["BitValue12"] = NullableValue(val.BitValue12);
                    row["BitValue13"] = NullableValue(val.BitValue13);
                    row["BitValue14"] = NullableValue(val.BitValue14);
                    row["BitValue15"] = NullableValue(val.BitValue15);
                    row["BitValue16"] = NullableValue(val.BitValue16);
                    row["BitValue17"] = NullableValue(val.BitValue17);
                    row["BitValue18"] = NullableValue(val.BitValue18);
                    row["BitValue19"] = NullableValue(val.BitValue19);
                    row["BitValue20"] = NullableValue(val.BitValue20);

                    //DECIMALS
                    row["DecimalValue1"] = NullableValue(val.DecimalValue1);
                    row["DecimalValue2"] = NullableValue(val.DecimalValue2);
                    row["DecimalValue3"] = NullableValue(val.DecimalValue3);
                    row["DecimalValue4"] = NullableValue(val.DecimalValue4);
                    row["DecimalValue5"] = NullableValue(val.DecimalValue5);
                    row["DecimalValue6"] = NullableValue(val.DecimalValue6);
                    row["DecimalValue7"] = NullableValue(val.DecimalValue7);
                    row["DecimalValue8"] = NullableValue(val.DecimalValue8);
                    row["DecimalValue9"] = NullableValue(val.DecimalValue9);
                    row["DecimalValue10"] = NullableValue(val.DecimalValue10);
                    row["DecimalValue11"] = NullableValue(val.DecimalValue11);
                    row["DecimalValue12"] = NullableValue(val.DecimalValue12);
                    row["DecimalValue13"] = NullableValue(val.DecimalValue13);
                    row["DecimalValue14"] = NullableValue(val.DecimalValue14);
                    row["DecimalValue15"] = NullableValue(val.DecimalValue15);
                    row["DecimalValue16"] = NullableValue(val.DecimalValue16);
                    row["DecimalValue17"] = NullableValue(val.DecimalValue17);
                    row["DecimalValue18"] = NullableValue(val.DecimalValue18);
                    row["DecimalValue19"] = NullableValue(val.DecimalValue19);
                    row["DecimalValue20"] = NullableValue(val.DecimalValue20);

                    //DATES
                    row["DateValue1"] = NullableValue(val.DateValue1);
                    row["DateValue2"] = NullableValue(val.DateValue2);
                    row["DateValue3"] = NullableValue(val.DateValue3);
                    row["DateValue4"] = NullableValue(val.DateValue4);
                    row["DateValue5"] = NullableValue(val.DateValue5);
                    row["DateValue6"] = NullableValue(val.DateValue6);
                    row["DateValue7"] = NullableValue(val.DateValue7);
                    row["DateValue8"] = NullableValue(val.DateValue8);
                    row["DateValue9"] = NullableValue(val.DateValue9);
                    row["DateValue10"] = NullableValue(val.DateValue10);
                    row["DateValue11"] = NullableValue(val.DateValue11);
                    row["DateValue12"] = NullableValue(val.DateValue12);
                    row["DateValue13"] = NullableValue(val.DateValue13);
                    row["DateValue14"] = NullableValue(val.DateValue14);
                    row["DateValue15"] = NullableValue(val.DateValue15);
                    row["DateValue16"] = NullableValue(val.DateValue16);
                    row["DateValue17"] = NullableValue(val.DateValue17);
                    row["DateValue18"] = NullableValue(val.DateValue18);
                    row["DateValue19"] = NullableValue(val.DateValue19);
                    row["DateValue20"] = NullableValue(val.DateValue20);

                    tbl.Rows.Add(row);
                }
            }

            return tbl;
        }


        #endregion "DataTable extensions"


        #region "Other extensions"

        public static byte[] ToByteArray(this Stream stream)
        {
            stream.Position = 0;
            var bytes = new List<byte>();

            int b;

            // -1 is a special value that mark the end of the stream
            while ((b = stream.ReadByte()) != -1)
                bytes.Add((byte)b);

            return bytes.ToArray();
        }

        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) // Check if the string is null or empty
            {
                return value;
            }

            // If the string length is greater than maxLength, truncate it
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }


        public static string Join(this List<string> list, string separator)
        {
            if (list == null || !list.Any())
            {
                return string.Empty;
            }
            return string.Join(separator, list);
        }

        #endregion "Other extensions"

    }
}