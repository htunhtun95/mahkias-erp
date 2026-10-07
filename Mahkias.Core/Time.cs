using System;
using System.Collections.Generic;
using System.Text;

namespace Mahkias.Core
{
   public class Time
    {
        public int Hour { get; set; }
        public int Minute { get; set; }
        public int Second { get; set; }

        public string FormattedTime {
            get {

                return ToString("HH:mm");
                
            }
        }

        public static Time FromTimeSpan(TimeSpan span) {
             
            return new Time()
            {
                Hour = span.Hours,
                Minute = span.Minutes,
                Second = span.Seconds
            };
        }

        public override string ToString()
        {

            return Hour.ToString("D2") + ":" + Minute.ToString("D2") + ":" + Second.ToString("D2");

        }
        public string ToString(string format)
        {
            if (format == "hh:mm")
            {
                return Hour.ToString("D2") + ":" + Minute.ToString("D2");
            }
            else if (format == "HH:mm")
            {
                return Hour.ToString("D2") + ":" + Minute.ToString("D2");
            }
            else if (format == "hh:mmtt")
            {
                if (Hour < 12)
                {
                    return Hour.ToString("D2") + ":" + Minute.ToString("D2") + "am";
                }
                else
                {
                    return (Hour - 12).ToString("D2") + ":" + Minute.ToString("D2") + "pm";
                }
            }

            return Hour.ToString("D2") + ":" + Minute.ToString("D2") + ":" + Second.ToString("D2");

        }



    }
}
