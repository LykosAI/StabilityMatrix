using Refit;

namespace StabilityMatrix.Core.Api;

public interface IApiFactory
{
    public T CreateRefitClient<T>(Uri baseAddress);

    public T CreateRefitClient<T>(Uri baseAddress, RefitSettings refitSettings);
}
