using System.Globalization;

namespace AiUsageDashboard.Web.Services;

public static class UsdCurrencyFormatter
{
    private static readonly CultureInfo UsCulture = CultureInfo.GetCultureInfo("en-US");

    public static string Format(decimal value) => value > 0m && value < 0.01m ? "<$0.01" : value.ToString("C", UsCulture);
}
