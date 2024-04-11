using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace StabilityMatrix.Core.Models.Api.Comfy;

/// <summary>
/// Collection of preprocessors included in
/// </summary>
/// <param name="Value"></param>
[PublicAPI]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public record ComfyAuxPreprocessor(string Value) : StringValue(Value)
{
    public static ComfyAuxPreprocessor None { get; } = new("none");
    public static ComfyAuxPreprocessor AnimeFaceSemSegPreprocessor { get; } =
        new("AnimeFace_SemSegPreprocessor");
    public static ComfyAuxPreprocessor BinaryPreprocessor { get; } = new("BinaryPreprocessor");
    public static ComfyAuxPreprocessor CannyEdgePreprocessor { get; } = new("CannyEdgePreprocessor");
    public static ComfyAuxPreprocessor ColorPreprocessor { get; } = new("ColorPreprocessor");
    public static ComfyAuxPreprocessor DensePosePreprocessor { get; } = new("DensePosePreprocessor");
    public static ComfyAuxPreprocessor DepthAnythingPreprocessor { get; } = new("DepthAnythingPreprocessor");
    public static ComfyAuxPreprocessor ZoeDepthAnythingPreprocessor { get; } =
        new("Zoe_DepthAnythingPreprocessor");
    public static ComfyAuxPreprocessor DiffusionEdgePreprocessor { get; } = new("DiffusionEdge_Preprocessor");
    public static ComfyAuxPreprocessor DWPreprocessor { get; } = new("DWPreprocessor");
    public static ComfyAuxPreprocessor AnimalPosePreprocessor { get; } = new("AnimalPosePreprocessor");
    public static ComfyAuxPreprocessor HEDPreprocessor { get; } = new("HEDPreprocessor");
    public static ComfyAuxPreprocessor FakeScribblePreprocessor { get; } = new("FakeScribblePreprocessor");
    public static ComfyAuxPreprocessor LeReSDepthMapPreprocessor { get; } = new("LeReS-DepthMapPreprocessor");
    public static ComfyAuxPreprocessor LineArtPreprocessor { get; } = new("LineArtPreprocessor");
    public static ComfyAuxPreprocessor AnimeLineArtPreprocessor { get; } = new("AnimeLineArtPreprocessor");
    public static ComfyAuxPreprocessor LineartStandardPreprocessor { get; } =
        new("LineartStandardPreprocessor");
    public static ComfyAuxPreprocessor Manga2AnimeLineArtPreprocessor { get; } =
        new("Manga2Anime_LineArt_Preprocessor");
    public static ComfyAuxPreprocessor MediaPipeFaceMeshPreprocessor { get; } =
        new("MediaPipe-FaceMeshPreprocessor");
    public static ComfyAuxPreprocessor MeshGraphormerDepthMapPreprocessor { get; } =
        new("MeshGraphormer-DepthMapPreprocessor");
    public static ComfyAuxPreprocessor MiDaSNormalMapPreprocessor { get; } =
        new("MiDaS-NormalMapPreprocessor");
    public static ComfyAuxPreprocessor MiDaSDepthMapPreprocessor { get; } = new("MiDaS-DepthMapPreprocessor");
    public static ComfyAuxPreprocessor MLSDPreprocessor { get; } = new("M-LSDPreprocessor");
    public static ComfyAuxPreprocessor BAENormalMapPreprocessor { get; } = new("BAE-NormalMapPreprocessor");
    public static ComfyAuxPreprocessor OneFormerCOCOSemSegPreprocessor { get; } =
        new("OneFormer-COCO-SemSegPreprocessor");
    public static ComfyAuxPreprocessor OneFormerADE20KSemSegPreprocessor { get; } =
        new("OneFormer-ADE20K-SemSegPreprocessor");
    public static ComfyAuxPreprocessor OpenposePreprocessor { get; } = new("OpenposePreprocessor");
    public static ComfyAuxPreprocessor PiDiNetPreprocessor { get; } = new("PiDiNetPreprocessor");
    public static ComfyAuxPreprocessor SavePoseKpsAsJsonFile { get; } = new("SavePoseKpsAsJsonFile");
    public static ComfyAuxPreprocessor FacialPartColoringFromPoseKps { get; } =
        new("FacialPartColoringFromPoseKps");
    public static ComfyAuxPreprocessor ImageLuminanceDetector { get; } = new("ImageLuminanceDetector");
    public static ComfyAuxPreprocessor ImageIntensityDetector { get; } = new("ImageIntensityDetector");
    public static ComfyAuxPreprocessor ScribblePreprocessor { get; } = new("ScribblePreprocessor");
    public static ComfyAuxPreprocessor ScribbleXDoGPreprocessor { get; } = new("Scribble_XDoG_Preprocessor");
    public static ComfyAuxPreprocessor SAMPreprocessor { get; } = new("SAMPreprocessor");
    public static ComfyAuxPreprocessor ShufflePreprocessor { get; } = new("ShufflePreprocessor");
    public static ComfyAuxPreprocessor TEEDPreprocessor { get; } = new("TEEDPreprocessor");
    public static ComfyAuxPreprocessor TilePreprocessor { get; } = new("TilePreprocessor");
    public static ComfyAuxPreprocessor UniFormerSemSegPreprocessor { get; } =
        new("UniFormer-SemSegPreprocessor");
    public static ComfyAuxPreprocessor SemSegPreprocessor { get; } = new("SemSegPreprocessor");
    public static ComfyAuxPreprocessor UnimatchOptFlowPreprocessor { get; } =
        new("Unimatch_OptFlowPreprocessor");
    public static ComfyAuxPreprocessor MaskOptFlow { get; } = new("MaskOptFlow");
    public static ComfyAuxPreprocessor ZoeDepthMapPreprocessor { get; } = new("Zoe-DepthMapPreprocessor");

    private static Dictionary<ComfyAuxPreprocessor, string> DisplayNamesMapping { get; } =
        new()
        {
            [None] = "None",
            [AnimeFaceSemSegPreprocessor] = "Anime Face SemSeg Preprocessor",
            [BinaryPreprocessor] = "Binary Preprocessor",
            [CannyEdgePreprocessor] = "Canny Edge Preprocessor",
            [ColorPreprocessor] = "Color Preprocessor",
            [DensePosePreprocessor] = "DensePose Preprocessor",
            [DepthAnythingPreprocessor] = "Depth Anything Preprocessor",
            [ZoeDepthAnythingPreprocessor] = "Zoe Depth Anything Preprocessor",
            [DiffusionEdgePreprocessor] = "Diffusion Edge Preprocessor",
            [DWPreprocessor] = "DW Preprocessor",
            [AnimalPosePreprocessor] = "Animal Pose Preprocessor",
            [HEDPreprocessor] = "HED Preprocessor",
            [FakeScribblePreprocessor] = "Fake Scribble Preprocessor",
            [LeReSDepthMapPreprocessor] = "LeReS-DepthMap Preprocessor",
            [LineArtPreprocessor] = "LineArt Preprocessor",
            [AnimeLineArtPreprocessor] = "Anime LineArt Preprocessor",
            [LineartStandardPreprocessor] = "Lineart Standard Preprocessor",
            [Manga2AnimeLineArtPreprocessor] = "Manga2Anime LineArt Preprocessor",
            [MediaPipeFaceMeshPreprocessor] = "MediaPipe FaceMesh Preprocessor",
            [MeshGraphormerDepthMapPreprocessor] = "MeshGraphormer DepthMap Preprocessor",
            [MiDaSNormalMapPreprocessor] = "MiDaS NormalMap Preprocessor",
            [MiDaSDepthMapPreprocessor] = "MiDaS DepthMap Preprocessor",
            [MLSDPreprocessor] = "M-LSD Preprocessor",
            [BAENormalMapPreprocessor] = "BAE NormalMap Preprocessor",
            [OneFormerCOCOSemSegPreprocessor] = "OneFormer COCO SemSeg Preprocessor",
            [OneFormerADE20KSemSegPreprocessor] = "OneFormer ADE20K SemSeg Preprocessor",
            [OpenposePreprocessor] = "Openpose Preprocessor",
            [PiDiNetPreprocessor] = "PiDiNet Preprocessor",
            [SavePoseKpsAsJsonFile] = "Save Pose Kps As Json File",
            [FacialPartColoringFromPoseKps] = "Facial Part Coloring From Pose Kps",
            [ImageLuminanceDetector] = "Image Luminance Detector",
            [ImageIntensityDetector] = "Image Intensity Detector",
            [ScribblePreprocessor] = "Scribble Preprocessor",
            [ScribbleXDoGPreprocessor] = "Scribble XDoG Preprocessor",
            [SAMPreprocessor] = "SAM Preprocessor",
            [ShufflePreprocessor] = "Shuffle Preprocessor",
            [TEEDPreprocessor] = "TEED Preprocessor",
            [TilePreprocessor] = "Tile Preprocessor",
            [UniFormerSemSegPreprocessor] = "UniFormer SemSeg Preprocessor",
            [SemSegPreprocessor] = "SemSeg Preprocessor",
            [UnimatchOptFlowPreprocessor] = "Unimatch OptFlow Preprocessor",
            [MaskOptFlow] = "Mask OptFlow",
            [ZoeDepthMapPreprocessor] = "Zoe DepthMap Preprocessor"
        };

    public static IEnumerable<ComfyAuxPreprocessor> Defaults => DisplayNamesMapping.Keys;

    public string DisplayName => DisplayNamesMapping.GetValueOrDefault(this, Value);

    /// <inheritdoc />
    public override string ToString() => Value;
}
