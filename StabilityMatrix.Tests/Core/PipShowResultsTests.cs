using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StabilityMatrix.Core.Python;

namespace StabilityMatrix.Tests.Core;

[TestClass]
public class PipShowResultsTests
{
    [TestMethod]
    public void TestSinglePackage()
    {
        var input = """
            Name: package-a
            Version: 1.0.0
            Summary: A test package
            Home-page: https://example.com
            Author: John Doe
            Author-email: john.doe@example.com
            License: MIT
            Location: /path/to/package
            Requires:
            Required-by:
            """;

        var result = PipShowResult.Parse(input);

        Assert.IsNotNull(result);
        Assert.AreEqual("package-a", result.Name);
        Assert.AreEqual("1.0.0", result.Version);
        Assert.AreEqual("A test package", result.Summary);
    }

    [TestMethod]
    public void TestMultiplePackages()
    {
        var input = """
            Name: package-a
            Version: 1.0.0
            Summary: A test package
            Home-page: https://example.com
            Author: John Doe
            Author-email: john.doe@example.com
            License: MIT
            Location: /path/to/package
            Requires:
            Required-by:
            ---
            Name: package-b
            Version: 2.0.0
            Summary: Another test package
            Home-page: https://example.com
            Author: Jane Doe
            Author-email: jane.doe@example.com
            License: Apache-2.0
            Location: /path/to/another/package
            Requires: package-a
            Required-by:
            """;

        var result = PipShowResult.Parse(input);

        Assert.IsNotNull(result);
        Assert.AreEqual("package-a", result.Name);
        Assert.AreEqual("1.0.0", result.Version);
        Assert.AreNotEqual("package-b", result.Name);
    }

    [TestMethod]
    public void TestMalformedPackage()
    {
        var input = """
            Name: package-a
            Version: 1.0.0
            Summary A test package
            Home-page: https://example.com
            Author: John Doe
            Author-email: john.doe@example.com
            License: MIT
            Location: /path/to/package
            Requires:
            Required-by:
            """;

        var result = PipShowResult.Parse(input);

        Assert.IsNotNull(result);
        Assert.AreEqual("package-a", result.Name);
        Assert.AreEqual("1.0.0", result.Version);
        Assert.IsNull(result.Summary);
    }

    [TestMethod]
    public void TestMultiLineLicense()
    {
        var input = """
            Name: package-a
            Version: 1.0.0
            Summary: A test package
            Home-page: https://example.com
            Author: John Doe
            Author-email: john.doe@example.com
            License: The MIT License (MIT)

                     Copyright (c) 2015 John Doe

                     Permission is hereby granted, free of charge, to any person obtaining a copy
                     of this software and associated documentation files (the "Software"), to deal
                     in the Software without restriction, including without limitation the rights
                     to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
                     copies of the Software, and to permit persons to whom the Software is
                     furnished to do so, subject to the following conditions:

                     The above copyright notice and this permission notice shall be included in all
                     copies or substantial portions of the Software.

                     THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
                     IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
                     FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
                     AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
                     LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
                     OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
                     SOFTWARE.
            Location: /path/to/package
            Requires:
            Required-by:
            """;

        var result = PipShowResult.Parse(input);

        Assert.IsNotNull(result);
        Assert.AreEqual("package-a", result.Name);
        Assert.AreEqual("1.0.0", result.Version);
        Assert.IsTrue(result.License?.StartsWith("License: The MIT License (MIT)"));
    }

    /// <summary>
    /// This test simulates the input that caused the crash reported in Sentry issue b125504f.
    /// The old implementation of PipShowResult.Parse used ToDictionary, which would throw an
    /// ArgumentException if the input contained multiple packages, as the "Name" key would be
    /// duplicated. The new implementation uses a foreach loop and TryAdd to prevent this crash.
    /// </summary>
    [TestMethod]
    public void TestDuplicatePackageNameInOutput()
    {
        var input = """
            Name: package-a
            Version: 1.0.0
            Summary: A test package
            Home-page: https://example.com
            Author: John Doe
            Author-email: john.doe@example.com
            License: MIT
            Location: /path/to/package
            Requires:
            Required-by:
            ---
            Name: package-a
            Version: 1.0.0
            Summary: A test package
            Home-page: https://example.com
            Author: John Doe
            Author-email: john.doe@example.com
            License: MIT
            Location: /path/to/package
            Requires:
            Required-by:
            """;

        var result = PipShowResult.Parse(input);

        Assert.IsNotNull(result);
        Assert.AreEqual("package-a", result.Name);
        Assert.AreEqual("1.0.0", result.Version);
    }

    [TestMethod]
    public void TestEmptyInputThrowsFormatException()
    {
        var input = "";
        Assert.ThrowsException<FormatException>(() => PipShowResult.Parse(input));
    }
}
