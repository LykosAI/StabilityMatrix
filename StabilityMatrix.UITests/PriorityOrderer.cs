using StabilityMatrix.UITests.Attributes;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace StabilityMatrix.UITests;

public class PriorityOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
    {
        var sortedMethods = new SortedDictionary<int, List<TTestCase>>();

        foreach (var testCase in testCases)
        {
            var priority = 0;

            foreach (
                var attr in testCase.TestMethod.Method.GetCustomAttributes(
                    typeof(TestPriorityAttribute).AssemblyQualifiedName
                )
            )
            {
                priority = attr.GetNamedArgument<int>("Priority");
            }

            GetOrCreate(sortedMethods, priority).Add(testCase);
        }

        foreach (var list in sortedMethods.Keys.Select(priority => sortedMethods[priority]))
        {
            list.Sort(
                (x, y) =>
                    StringComparer.OrdinalIgnoreCase.Compare(
                        x.TestMethod.Method.Name,
                        y.TestMethod.Method.Name
                    )
            );
            foreach (var testCase in list)
            {
                yield return testCase;
            }
        }
    }

    private static TValue GetOrCreate<TKey, TValue>(IDictionary<TKey, TValue> dictionary, TKey key)
        where TValue : new()
    {
        if (dictionary.TryGetValue(key, out var result))
            return result;

        result = new TValue();
        dictionary[key] = result;

        return result;
    }
}
