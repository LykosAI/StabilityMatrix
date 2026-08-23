using StabilityMatrix.Core.Models;

namespace StabilityMatrix.Avalonia.Models.Inference;

/// <summary>
/// A companion file (text encoder or VAE) a workflow profile expects: how to recognize an
/// installed copy by filename, and the official download to offer when none is installed.
/// Matcher and download live in one record so they can never disagree about what a profile
/// needs. The UNET is never a companion — that's the user's choice (including CivitAI
/// fine-tunes); companions are the official files every variant of a family shares.
/// </summary>
public record InferenceCompanion(
    string DisplayName,
    Func<string, bool> MatchesFileName,
    RemoteResource Resource
);
