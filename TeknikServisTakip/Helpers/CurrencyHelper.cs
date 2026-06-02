using Microsoft.AspNetCore.Mvc.Rendering;

namespace TeknikServisTakip.Helpers
{
    public static class CurrencyHelper
    {
        public static string GetSymbol(string currency)
        {
            return currency switch
            {
                "TRY" => "₺",
                "USD" => "$",
                "EUR" => "€",
                "GBP" => "£",
                _ => "₺"
            };
        }

        public static string GetName(string currency)
        {
            return currency switch
            {
                "TRY" => "Türk Lirası",
                "USD" => "Amerikan Doları",
                "EUR" => "Euro",
                "GBP" => "İngiliz Sterlini",
                _ => "Türk Lirası"
            };
        }

        public static List<SelectListItem> GetCurrencyList()
        {
            return new List<SelectListItem>
        {
            new SelectListItem { Value = "TRY", Text = "₺ TL (Türk Lirası)" },
            new SelectListItem { Value = "USD", Text = "$ USD (Amerikan Doları)" },
            new SelectListItem { Value = "EUR", Text = "€ EUR (Euro)" },
            new SelectListItem { Value = "GBP", Text = "£ GBP (İngiliz Sterlini)" }
        };
        }
    }
}
