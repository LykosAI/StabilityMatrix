using System.Threading.Tasks;

namespace StabilityMatrix.Models.Packages;

public interface ISharedFolderStrategy
{
    Task ExecuteAsync(BasePackage package);
}