namespace StabilityMatrix.Core.Models.Api;

public enum CivitModelVersionInstallState
{
    NotInstalled,

    /// <summary>
    /// Only supporting components (VAE, text encoder, etc) of the version are installed,
    /// none of its main model files.
    /// </summary>
    ComponentsOnly,

    Installed,
}
