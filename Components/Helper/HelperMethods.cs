using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kuyumcu.Components.Helper
{
    public static class HelperMethods
    {
        public static string FormatPhoneNumber(this string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return phoneNumber;
            }

            var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());

            if (digits.Length != 10)
            {
                return phoneNumber;
            }

            return $"({digits.Substring(0, 3)}) {digits.Substring(3, 3)} {digits.Substring(6, 2)} {digits.Substring(8, 2)}";
        }
        public static string FormatPrice(decimal tutar)
        {
            return tutar % 1 == 0 ? $"{tutar:N0}" : $"{tutar:N2}";
        }
        public static Kuyumcu.Services.SortDirection? GetSortDirection(MudBlazor.SortDirection? sort) => sort switch
        {
            MudBlazor.SortDirection.Descending => Kuyumcu.Services.SortDirection.Descending,
            MudBlazor.SortDirection.Ascending => Kuyumcu.Services.SortDirection.Ascending,
            _ => null

        };
    }
}
