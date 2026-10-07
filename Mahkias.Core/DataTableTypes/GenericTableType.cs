using System;
using System.Collections.Generic;
using System.Text;

namespace Mahkias.Core.DataTableTypes
{
    public class GenericTableType
    {
        public int? NumericValue1 { get; set; }
        public int? NumericValue2 { get; set; }
        public int? NumericValue3 { get; set; }
        public int? NumericValue4 { get; set; }
        public string? TextValue1 { get; set; }
        public string? TextValue2 { get; set; }
        public string? TextValue3 { get; set; }
        public string? TextValue4 { get; set; }
        public bool? BitValue1 { get; set; }
        public bool? BitValue2 { get; set; }
        public bool? BitValue3 { get; set; }
        public bool? BitValue4 { get; set; }

    }

    public class DefaultGenericTableType : GenericTableType
    {

        public int? NumericValue5 { get; set; }
        public int? NumericValue6 { get; set; }
        public int? NumericValue7 { get; set; }
        public int? NumericValue8 { get; set; }
        public int? NumericValue9 { get; set; }
        public int? NumericValue10 { get; set; }

        public string? TextValue5 { get; set; }
        public string? TextValue6 { get; set; }
        public string? TextValue7 { get; set; }
        public string? TextValue8 { get; set; }
        public string? TextValue9 { get; set; }
        public string? TextValue10 { get; set; }


        public bool? BitValue5 { get; set; }
        public bool? BitValue6 { get; set; }
        public bool? BitValue7 { get; set; }
        public bool? BitValue8 { get; set; }
        public bool? BitValue9 { get; set; }
        public bool? BitValue10 { get; set; }

        public DateTime? DateValue1 { get; set; }
        public DateTime? DateValue2 { get; set; }
        public DateTime? DateValue3 { get; set; }
        public DateTime? DateValue4 { get; set; }
        public DateTime? DateValue5 { get; set; }
        public DateTime? DateValue6 { get; set; }
        public DateTime? DateValue7 { get; set; }
        public DateTime? DateValue8 { get; set; }
        public DateTime? DateValue9 { get; set; }
        public DateTime? DateValue10 { get; set; }
        public decimal? DecimalValue1 { get; set; }
        public decimal? DecimalValue2 { get; set; }
        public decimal? DecimalValue3 { get; set; }
        public decimal? DecimalValue4 { get; set; }
        public decimal? DecimalValue5 { get; set; }
        public decimal? DecimalValue6 { get; set; }
        public decimal? DecimalValue7 { get; set; }
        public decimal? DecimalValue8 { get; set; }
        public decimal? DecimalValue9 { get; set; }
        public decimal? DecimalValue10 { get; set; }
        public decimal? MoneyValue1 { get; set; }
        public decimal? MoneyValue2 { get; set; }
        public decimal? MoneyValue3 { get; set; }
        public decimal? MoneyValue4 { get; set; }
        public decimal? MoneyValue5 { get; set; }
        public decimal? MoneyValue6 { get; set; }
        public decimal? MoneyValue7 { get; set; }
        public decimal? MoneyValue8 { get; set; }
        public decimal? MoneyValue9 { get; set; }
        public decimal? MoneyValue10 { get; set; }
    }

    public class GenericDateAndTimeTableType : GenericTableType
    {
        public DateTime? DateValue1 { get; set; }
        public DateTime? DateValue2 { get; set; }
        public DateTime? DateValue3 { get; set; }
        public DateTime? DateValue4 { get; set; }
        public TimeSpan? TimeValue1 { get; set; }
        public TimeSpan? TimeValue2 { get; set; }
        public TimeSpan? TimeValue3 { get; set; }
        public TimeSpan? TimeValue4 { get; set; }
    }

    public class GenericTableTypeWithDecimal : GenericTableType
    {
        public decimal? DecimalValue1 { get; set; }
        public decimal? DecimalValue2 { get; set; }
        public decimal? DecimalValue3 { get; set; }
        public decimal? DecimalValue4 { get; set; }

    }

    public class BigGenericTableType : GenericTableTypeWithDecimal
    {
        public int? NumericValue5 { get; set; }
        public int? NumericValue6 { get; set; }
        public int? NumericValue7 { get; set; }
        public int? NumericValue8 { get; set; }
        public int? NumericValue9 { get; set; }
        public int? NumericValue10 { get; set; }
        public int? NumericValue11 { get; set; }
        public int? NumericValue12 { get; set; }
        public int? NumericValue13 { get; set; }
        public int? NumericValue14 { get; set; }
        public int? NumericValue15 { get; set; }
        public int? NumericValue16 { get; set; }
        public int? NumericValue17 { get; set; }
        public int? NumericValue18 { get; set; }
        public int? NumericValue19 { get; set; }
        public int? NumericValue20 { get; set; }
        public string? TextValue5 { get; set; }
        public string? TextValue6 { get; set; }
        public string? TextValue7 { get; set; }
        public string? TextValue8 { get; set; }
        public bool? BitValue5 { get; set; }
        public bool? BitValue6 { get; set; }
        public bool? BitValue7 { get; set; }
        public bool? BitValue8 { get; set; }
        public decimal? DecimalValue5 { get; set; }
        public decimal? DecimalValue6 { get; set; }
        public decimal? DecimalValue7 { get; set; }
        public decimal? DecimalValue8 { get; set; }

        public DateTime? DateValue1 { get; set; }
        public DateTime? DateValue2 { get; set; }
        public DateTime? DateValue3 { get; set; }
        public DateTime? DateValue4 { get; set; }
        public DateTime? DateValue5 { get; set; }
        public DateTime? DateValue6 { get; set; }
        public DateTime? DateValue7 { get; set; }
        public DateTime? DateValue8 { get; set; }
    }


    public class BigTextGenericTableType : GenericTableType
    {
        public int? NumericValue5 { get; set; }
       
        public string? TextValue5 { get; set; }
        public string? TextValue6 { get; set; }
        public string? TextValue7 { get; set; }
        public string? TextValue8 { get; set; }
        public string? TextValue9 { get; set; }
        public string? TextValue10 { get; set; }
        public string? TextValue11 { get; set; }
        public string? TextValue12 { get; set; }
        public string? TextValue13 { get; set; }
        public string? TextValue14 { get; set; }
        public string? TextValue15 { get; set; }


        public decimal? DecimalValue1 { get; set; }
        public decimal? DecimalValue2 { get; set; }
        public decimal? DecimalValue3 { get; set; }
        public decimal? DecimalValue4 { get; set; }
        public decimal? DecimalValue5 { get; set; }


        public DateTime? DateValue1 { get; set; }
        public DateTime? DateValue2 { get; set; }
        public DateTime? DateValue3 { get; set; }
        public DateTime? DateValue4 { get; set; }
        public DateTime? DateValue5 { get; set; }

    }


    public class BigTextTwentyGenericTableType : BigTextGenericTableType
    {
        public string? TextValue16 { get; set; }
        public string? TextValue17 { get; set; }
        public string? TextValue18 { get; set; }
        public string? TextValue19 { get; set; }
        public string? TextValue20 { get; set; }


        public bool? BitValue5 { get; set; }
        public bool? BitValue6 { get; set; }
        public bool? BitValue7 { get; set; }
        public bool? BitValue8 { get; set; }
        public bool? BitValue9 { get; set; }
        public bool? BitValue10 { get; set; }
        public bool? BitValue11 { get; set; }
        public bool? BitValue12 { get; set; }


    }


    public class BigMoneyGenericTableType : GenericTableTypeWithDecimal
    {

        public int? NumericValue5 { get; set; }
        public int? NumericValue6 { get; set; }
        public int? NumericValue7 { get; set; }
        public int? NumericValue8 { get; set; }
        public int? NumericValue9 { get; set; }
        public int? NumericValue10 { get; set; }

        public string? TextValue5 { get; set; }
        public string? TextValue6 { get; set; }
        public string? TextValue7 { get; set; }
        public string? TextValue8 { get; set; }
        public string? TextValue9 { get; set; }
        public string? TextValue10 { get; set; }
        public bool? BitValue5 { get; set; }

        public decimal? DecimalValue5 { get; set; }
        public decimal? DecimalValue6 { get; set; }
        public decimal? DecimalValue7 { get; set; }
        public decimal? DecimalValue8 { get; set; }
        public decimal? DecimalValue9 { get; set; }
        public decimal? DecimalValue10 { get; set; }


        public DateTime? DateValue1 { get; set; }
        public DateTime? DateValue2 { get; set; }
        public DateTime? DateValue3 { get; set; }
        public DateTime? DateValue4 { get; set; }
        public DateTime? DateValue5 { get; set; }


        public decimal? MoneyValue1 { get; set; }
        public decimal? MoneyValue2 { get; set; }
        public decimal? MoneyValue3 { get; set; }
        public decimal? MoneyValue4 { get; set; }
        public decimal? MoneyValue5 { get; set; }
        public decimal? MoneyValue6 { get; set; }
        public decimal? MoneyValue7 { get; set; }
        public decimal? MoneyValue8 { get; set; }
        public decimal? MoneyValue9 { get; set; }
        public decimal? MoneyValue10 { get; set; }
    }

    public class GenericTwentyTableType : BigTextTwentyGenericTableType
    {
        public int? NumericValue6 { get; set; }
        public int? NumericValue7 { get; set; }
        public int? NumericValue8 { get; set; }
        public int? NumericValue9 { get; set; }
        public int? NumericValue10 { get; set; }
        public int? NumericValue11 { get; set; }
        public int? NumericValue12 { get; set; }
        public int? NumericValue13 { get; set; }
        public int? NumericValue14 { get; set; }
        public int? NumericValue15 { get; set; }
        public int? NumericValue16 { get; set; }
        public int? NumericValue17 { get; set; }
        public int? NumericValue18 { get; set; }
        public int? NumericValue19 { get; set; }
        public int? NumericValue20 { get; set; }



        public decimal? DecimalValue6 { get; set; }
        public decimal? DecimalValue7 { get; set; }
        public decimal? DecimalValue8 { get; set; }
        public decimal? DecimalValue9 { get; set; }
        public decimal? DecimalValue10 { get; set; }
        public decimal? DecimalValue11 { get; set; }
        public decimal? DecimalValue12 { get; set; }
        public decimal? DecimalValue13 { get; set; }
        public decimal? DecimalValue14 { get; set; }
        public decimal? DecimalValue15 { get; set; }
        public decimal? DecimalValue16 { get; set; }
        public decimal? DecimalValue17 { get; set; }
        public decimal? DecimalValue18 { get; set; }
        public decimal? DecimalValue19 { get; set; }
        public decimal? DecimalValue20 { get; set; }


        public DateTime? DateValue6 { get; set; }
        public DateTime? DateValue7 { get; set; }
        public DateTime? DateValue8 { get; set; }
        public DateTime? DateValue9 { get; set; }
        public DateTime? DateValue10 { get; set; }
        public DateTime? DateValue11 { get; set; }
        public DateTime? DateValue12 { get; set; }
        public DateTime? DateValue13 { get; set; }
        public DateTime? DateValue14 { get; set; }
        public DateTime? DateValue15 { get; set; }
        public DateTime? DateValue16 { get; set; }
        public DateTime? DateValue17 { get; set; }
        public DateTime? DateValue18 { get; set; }
        public DateTime? DateValue19 { get; set; }
        public DateTime? DateValue20 { get; set; }


        public bool? BitValue13 { get; set; }
        public bool? BitValue14 { get; set; }
        public bool? BitValue15 { get; set; }
        public bool? BitValue16 { get; set; }
        public bool? BitValue17 { get; set; }
        public bool? BitValue18 { get; set; }
        public bool? BitValue19 { get; set; }
        public bool? BitValue20 { get; set; }


    }
}
