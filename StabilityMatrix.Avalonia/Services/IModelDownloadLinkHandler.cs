using System.Threading.Tasks;

namespace StabilityMatrix.Avalonia.Services;

public interface IModelDownloadLinkHandler
{
    Task StartListening();
}
