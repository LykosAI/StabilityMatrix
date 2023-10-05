using System;
using StabilityMatrix.Core.Api;

namespace StabilityMatrix.Avalonia.DesignData;

public class MockApiFactory : IApiFactory
{
    public T CreateRefitClient<T>(Uri baseAddress)
    {
        throw new NotImplementedException();
    }
}
