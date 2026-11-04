using System.Globalization;

namespace LabApp.Core;

public sealed record ScalarValue(string Raw, bool IsNumber)
{
    public static bool TryParse(string token, out ScalarValue value)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            value = null!;
            return false;
        }
        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            value = new ScalarValue(token, true);
            return true;
        }
        value = new ScalarValue(token, false);
        return true;
    }

    public double AsNumber() => double.Parse(Raw, CultureInfo.InvariantCulture);
    public override string ToString() => Raw;
}