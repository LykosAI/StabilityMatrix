namespace StabilityMatrix.UITests;

public static class WaitHelper
{
    public static async Task<T> WaitForConditionAsync<T>(
        Func<T> getter,
        Func<T, bool> condition,
        int delayMs = 50,
        int maxAttempts = 20,
        int initialDelayMs = 100
    )
    {
        await Task.Delay(initialDelayMs);

        for (var i = 0; i < maxAttempts; i++)
        {
            await Task.Delay(delayMs);

            var result = getter();

            if (condition(result))
            {
                return result;
            }
        }

        throw new TimeoutException("Waited too long for a condition to be met");
    }

    public static async Task<T> WaitForNotNullAsync<T>(
        Func<T?> getter,
        int delayMs = 50,
        int maxAttempts = 20,
        int initialDelayMs = 100
    )
    {
        await Task.Delay(initialDelayMs);

        for (var i = 0; i < maxAttempts; i++)
        {
            await Task.Delay(delayMs);

            if (getter() is { } result)
            {
                return result;
            }
        }

        throw new TimeoutException("Waited too long for a non-null value");
    }
}
