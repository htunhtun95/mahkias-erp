using System;

namespace Mahkias.Core
{
    public class AddressTableType
    {
        public int? Id { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string Address3 { get; set; }
        public int? CityId { get; set; }
        public string CityFriendlyUrl{ get; set; }
        public string CityName{ get; set; }
        public int? StateId { get; set; }
        public string StateFriendlyUrl { get; set; }
        public string StateName { get; set; }
        public string Postcode { get; set; }
        public int? CountryId { get; set; }
        public string CountryCode { get; set; }
        public Guid? TravellerAddressGuid { get; set; }
        public int? NumericValue1 { get; set; }
        public int? NumericValue2 { get; set; }
        public int? NumericValue3 { get; set; }
        public int? NumericValue4 { get; set; }
        public string TextValue1 { get; set; }
        public string TextValue2 { get; set; }
        public string TextValue3 { get; set; }
        public string TextValue4 { get; set; }
        public bool? BitValue1 { get; set; }
        public bool? BitValue2 { get; set; }
        public bool? BitValue3 { get; set; }
        public bool? BitValue4 { get; set; }
    }
}
