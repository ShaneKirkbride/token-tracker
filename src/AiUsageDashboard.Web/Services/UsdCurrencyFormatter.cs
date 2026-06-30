using System.Globalization;

namespace AiUsageDashboard.Web.Services;

public static class UsdCurrencyFormatter
{
    private static readonly CultureInfo UsCulture = CultureInfo.GetCultureInfo("en-US");

    public static string Format(decimal value) => value.ToString("C", UsCulture);
}
