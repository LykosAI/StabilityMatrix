namespace StabilityMatrix.Core.Inference.Profiles
{
    public class WanInferenceProfile : InferenceProfileBase
    {
        public override string Name => "WAN";
        public override Type SettingsType => typeof(WanSettings);

        // Existing logic like BuildWorkflow(), BuildAddons(), etc.
    }
}
