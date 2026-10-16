namespace Nexus.Gateway.Common;

using System.Globalization;

public static class MoneyPrecision
{
    // Accept only positive amounts which can be formatted to two decimal places without rounding.
    public static bool TryParsePositive(string? text, out decimal amount)
    {
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount)
            && amount > 0m
            && amount == decimal.Round(amount, 2);
    }

    public static decimal EnsurePositive(string? text, string field = "Amount")
    {
        if (!TryParsePositive(text, out var amount))
            throw new GatewayInputException($"{field} phải là số > 0", field);
        return amount;
    }
}
