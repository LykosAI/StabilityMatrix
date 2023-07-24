using System.Collections.Generic;
using StabilityMatrix.Core.Models.Api;
using StabilityMatrix.Core.Services;
using StabilityMatrix.ViewModels;

namespace StabilityMatrix.DesignData;

public class MockModelVersionDialogViewModel : SelectModelVersionDialogViewModel
{
    public MockModelVersionDialogViewModel() : base(new CivitModel {Name = "Indigo furry mix", ModelVersions = new List<CivitModelVersion>
    {
        new()
        {
            Name = "v45_hybrid",
            Images = new List<CivitImage>
            {
                new ()
                {
                    Url = "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/fcb56b61-2cd6-40f4-a75d-e9448c0822d6/width=450/29933-2136606019-masterpiece,%20high%20quality,%20absurd%20res,%20digital%20painting%20_(artwork_),%20solo,%20(kemono_1.4),%20male%20anthro%20dragon,%20blue%20body,%20ice,%20(wa.jpeg"
                }
            },
            Files = new List<CivitFile>
            {
                new()
                {
                    Name = "indigoFurryMix_v45Hybrid.safetensors",
                    Metadata = new CivitFileMetadata
                    {
                        Fp = CivitModelFpType.fp16,
                        Size = CivitModelSize.pruned
                    },
                    SizeKb = 1312645
                },
                new()
                {
                    Name = "indigoFurryMix_v45Hybrid.safetensors",
                    Metadata = new CivitFileMetadata
                    {
                        Fp = CivitModelFpType.fp32,
                        Size = CivitModelSize.pruned
                    },
                    SizeKb = 5972132
                }
            }
        }
    }}, new SettingsManager {Settings = { ModelBrowserNsfwEnabled = true }})
    {

    }
}
