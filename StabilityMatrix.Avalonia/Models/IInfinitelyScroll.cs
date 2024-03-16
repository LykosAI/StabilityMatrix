using System.Threading.Tasks;

namespace StabilityMatrix.Avalonia.Models;

public interface IInfinitelyScroll
{
    Task LoadNextPageAsync();
}
