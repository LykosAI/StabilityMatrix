using System.ComponentModel;
using StabilityMatrix.ViewModels;

namespace StabilityMatrix.DesignData;

[DesignOnly(true)]
public class MockCheckpointManagerViewModel : CheckpointManagerViewModel
{
    public MockCheckpointManagerViewModel() : base(null!)
    {
        CheckpointFolders = new()
        {
            new()
            {
                Title = "Stable Diffusion",
                CheckpointFiles = new()
                {
                    new()
                    {
                        Title = "Stable Diffusion v1.5",
                        FilePath = "v1-5-pruned-emaonly.safetensors",
                    },
                    new()
                    {
                        Title = "Scenery Mix",
                        FilePath = "scenery-mix.pt",
                    },
                    new()
                    {
                        Title = "Example Realistic",
                        FilePath = "exr-v21.safetensors",
                        ConnectedModel = new()
                        {
                            ModelName = "Example Realistic",
                            VersionName = "Hybrid v41",
                            ModelDescription = "Example Description",
                        }
                    },
                    new()
                    {
                        Title = "Painting e12",
                        FilePath = "painting-e12.pt",
                    },
                }
            },
            new()
            {
                Title = "Lora",
                IsCurrentDragTarget = true,
                CheckpointFiles = new()
                {
                    new()
                    {
                        Title = "Detail Tweaker LoRA",
                        FilePath = "add_detail.safetensors",
                    },
                    new()
                    {
                        Title = "Armor Suit LoRa",
                        FilePath = "ArmorSuit_v1.safetensors",
                    },
                }
            }
        };
    }
}
