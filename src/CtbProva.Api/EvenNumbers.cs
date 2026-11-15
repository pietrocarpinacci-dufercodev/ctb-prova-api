public static class EvenNumbers
{
    /// <summary>
    /// Returns the sum of the even numbers in <paramref name="numbers"/>, negative even numbers included.
    /// Returns 0 when there are no even numbers. The result is a <see cref="long"/> so it does not overflow.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="numbers"/> is null.</exception>
    public static long Sum(IEnumerable<int> numbers)
    {
        ArgumentNullException.ThrowIfNull(numbers);
        return numbers.Where(n => n % 2 == 0).Sum(n => (long)n);
    }
}
