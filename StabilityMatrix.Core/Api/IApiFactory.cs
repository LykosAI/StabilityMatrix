namespace StabilityMatrix.Core.Api;

public interface IApiFactory
{
    public T CreateRefitClient<T>(Uri baseAddress);
}
