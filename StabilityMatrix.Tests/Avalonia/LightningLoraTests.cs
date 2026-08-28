using StabilityMatrix.Avalonia.Models.Inference;

namespace StabilityMatrix.Tests.Avalonia;

[TestClass]
public class LightningLoraTests
{
    [TestMethod]
    [DataRow("Wan2.2-I2V-A14B-4steps-lora-rank64-Seko-V1.safetensors", 4)]
    [DataRow("Wan2.2-T2V-A14B-4steps-lora-rank64-Seko-V1.safetensors", 4)]
    [DataRow("minimax_h3_fl2v_turbo_8step_v1.0_comfyui_bf16.safetensors", 8)]
    [DataRow("minimax_h3_fl2v_turbo_4step_v1.0_768p_comfyui_bf16.safetensors", 4)]
    public void Detects_StepCountLoras(string fileName, int expectedSteps)
    {
        Assert.IsTrue(LightningLora.TryDetect(fileName, out var steps));
        Assert.AreEqual(expectedSteps, steps);
    }

    [TestMethod]
    [DataRow("wan21_lightx2v_cfg_step_distill_lora.safetensors")]
    [DataRow("sdxl_lightning_lora.safetensors")]
    [DataRow("some_turbo_speed_lora.safetensors")]
    public void Detects_TokenOnlyLoras_WithoutStepCount(string fileName)
    {
        Assert.IsTrue(LightningLora.TryDetect(fileName, out var steps));
        Assert.IsNull(steps);
    }

    [TestMethod]
    [DataRow("my_character_lora_v2.safetensors")]
    [DataRow("detail_tweaker_xl.safetensors")]
    [DataRow("footstep_sound_style.safetensors")]
    [DataRow("20steps_of_stairs_style.safetensors")]
    public void Ignores_OrdinaryLoras(string fileName)
    {
        Assert.IsFalse(LightningLora.TryDetect(fileName, out _));
    }
}
