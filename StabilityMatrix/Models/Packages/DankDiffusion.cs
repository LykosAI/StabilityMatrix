using System.Threading.Tasks;

namespace StabilityMatrix.Models.Packages;

public class DankDiffusion : BasePackage
{
    public override string Name => "dank-diffusion";
    public override string DisplayName => "Dank Diffusion";
    public override string Author => "mohnjiles";
    public override string GithubUrl => "https://github.com/mohnjiles/dank-diffusion";
    public override string LaunchCommand => "";

    public override Task DownloadPackage()
    {
        throw new System.NotImplementedException();
    }

    public override Task InstallPackage()
    {
        throw new System.NotImplementedException();
    }
}
