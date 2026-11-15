namespace CtbProva.Api.Tests;

public class EvenNumbersTests
{
    [Theory]
    [InlineData(new[] { 1, 2, 3, 4 }, 6L)]
    [InlineData(new[] { -2, 3, 4 }, 2L)]
    [InlineData(new int[0], 0L)]
    [InlineData(new[] { 1, 3, 5 }, 0L)]
    [InlineData(new[] { int.MaxValue - 1, int.MaxValue - 1 }, 2L * (int.MaxValue - 1))]
    public void Sum_ReturnsSumOfEvenNumbers(int[] numbers, long expected)
    {
        Assert.Equal(expected, EvenNumbers.Sum(numbers));
    }

    [Fact]
    public void Sum_NullList_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => EvenNumbers.Sum(null!));
    }
}
