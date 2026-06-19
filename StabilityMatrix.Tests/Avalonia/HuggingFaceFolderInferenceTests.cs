using StabilityMatrix.Avalonia.Models.HuggingFace;
using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class HuggingFaceFolderInferenceTests
{
    [DataTestMethod]
    [DataRow("vae/diffusion_pytorch_model.safetensors", SharedFolderType.VAE)]
    [DataRow("ae.safetensors", SharedFolderType.VAE)]
    [DataRow("text_encoder/model.safetensors", SharedFolderType.TextEncoders)]
    [DataRow("clip_l.safetensors", SharedFolderType.TextEncoders)]
    [DataRow("t5xxl_fp16.safetensors", SharedFolderType.TextEncoders)]
    [DataRow("clip_vision/model.safetensors", SharedFolderType.ClipVision)]
    [DataRow("control_v11p_sd15_canny.safetensors", SharedFolderType.ControlNet)]
    [DataRow("ip-adapter_sd15.safetensors", SharedFolderType.IpAdapter)]
    [DataRow("t2i-adapter_xl_canny.safetensors", SharedFolderType.T2IAdapter)]
    [DataRow("my_lora.safetensors", SharedFolderType.Lora)]
    [DataRow("unet/flux1-dev.safetensors", SharedFolderType.DiffusionModels)]
    [DataRow("model-q4_k_m.gguf", SharedFolderType.DiffusionModels)]
    [DataRow("flux1-dev.safetensors", SharedFolderType.StableDiffusion)]
    [DataRow("v1-5-pruned-emaonly.safetensors", SharedFolderType.StableDiffusion)]
    public void Infer_ReturnsExpectedFolder(string path, SharedFolderType expected)
    {
        Assert.AreEqual(expected, HuggingFaceFolderInference.Infer(path));
    }

    [DataTestMethod]
    [DataRow("owner/name", "owner/name")]
    [DataRow("https://huggingface.co/owner/name", "owner/name")]
    [DataRow("huggingface.co/owner/name", "owner/name")]
    [DataRow("https://huggingface.co/owner/name/tree/main", "owner/name")]
    [DataRow("https://huggingface.co/owner/name/blob/main/file.safetensors", "owner/name")]
    [DataRow("  owner/name?library=diffusers  ", "owner/name")]
    public void TryParseRepoId_ParsesValidInput(string input, string expected)
    {
        Assert.IsTrue(HuggingFaceFolderInference.TryParseRepoId(input, out var repoId));
        Assert.AreEqual(expected, repoId);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("singleword")]
    [DataRow("https://example.com/owner/name")]
    [DataRow("https://huggingface.co/owner")]
    public void TryParseRepoId_RejectsInvalidInput(string input)
    {
        Assert.IsFalse(HuggingFaceFolderInference.TryParseRepoId(input, out _));
    }
}
