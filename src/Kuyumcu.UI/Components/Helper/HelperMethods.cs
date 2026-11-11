using Kuyumcu.Helpers;

namespace Kuyumcu.Components.Helper
{
    public static class HelperMethods
    {
        public static string FormatPhoneNumber(this string phoneNumber) => FormatHelper.FormatPhoneNumber(phoneNumber);

        public static string FormatPrice(decimal tutar) => FormatHelper.FormatPrice(tutar);

        public static Kuyumcu.Services.SortDirection? GetSortDirection(MudBlazor.SortDirection? sort) => sort switch
        {
            MudBlazor.SortDirection.Descending => Kuyumcu.Services.SortDirection.Descending,
            MudBlazor.SortDirection.Ascending => Kuyumcu.Services.SortDirection.Ascending,
            _ => null
        };
    }
}
