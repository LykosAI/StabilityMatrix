using System.Globalization;
using StabilityMatrix.Avalonia.Converters;

namespace StabilityMatrix.Tests.Avalonia.Converters;

[TestClass]
public class NullableDefaultNumericConverterTests
{
    [TestMethod]
    public void Convert_IntToDecimal_ValueReturnsNullable()
    {
        const int value = 123;

        var converter = NullableDefaultNumericConverters.IntToDecimal;

        var result = converter.Convert(value, typeof(decimal?), null, CultureInfo.InvariantCulture);

        Assert.AreEqual((decimal?)123, result);
    }

    [TestMethod]
    public void ConvertBack_IntToDecimal_NullableReturnsDefault()
    {
        decimal? value = null;

        var converter = NullableDefaultNumericConverters.IntToDecimal;

        var result = converter.ConvertBack(value, typeof(int), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void ConvertBack_IntToDouble_NanReturnsDefault()
    {
        const double value = double.NaN;

        var converter = new NullableDefaultNumericConverter<int, double>();

        var result = converter.ConvertBack(value, typeof(int), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(0, result);
    }
}
