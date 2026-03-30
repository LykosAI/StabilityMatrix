using System.Text;
using System.Text.RegularExpressions;
using Injectio.Attributes;
using StabilityMatrix.Avalonia.Models.CheckpointOrganizer;
using StabilityMatrix.Avalonia.Models.Inference;
using StabilityMatrix.Core.Exceptions;
using StabilityMatrix.Core.Helper;
using StabilityMatrix.Core.Models;
using StabilityMatrix.Core.Models.Database;
using StabilityMatrix.Core.Models.FileInterfaces;
using ValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace StabilityMatrix.Avalonia.Services;

[RegisterSingleton<ModelOrganizationService>]
public class ModelOrganizationService
{
    private static readonly char[] DirectorySeparators =
    [
        Path.DirectorySeparatorChar,
        Path.AltDirectorySeparatorChar,
    ];
    private static readonly Regex TemplateRegex = new(@"\{([a-z_:\d\[\]]+)\}", RegexOptions.Compiled);
    private static readonly HashSet<char> InvalidSubstitutionChars =
    [
        .. Path.GetInvalidFileNameChars(),
        Path.DirectorySeparatorChar,
        Path.AltDirectorySeparatorChar,
    ];

    public ModelOrganizationPlan BuildPlan(
        IEnumerable<LocalModelFile> models,
        string modelsRoot,
        string scopePath,
        bool includeNested,
        string? template
    )
    {
        var effectiveTemplate = string.IsNullOrWhiteSpace(template)
            ? FileNameFormat.DefaultModelBrowserTemplate
            : template.Trim();

        var provider = new FileNameFormatProvider();
        var validationResult = provider.Validate(effectiveTemplate);
        if (validationResult != ValidationResult.Success)
        {
            return new ModelOrganizationPlan
            {
                Template = effectiveTemplate,
                ScopePath = scopePath,
                IncludeNested = includeNested,
                ValidationError = validationResult.ErrorMessage,
            };
        }

        var items = FilterModelsForScope(models, modelsRoot, scopePath, includeNested)
            .Select(model => BuildPreviewItem(model, scopePath, effectiveTemplate, modelsRoot))
            .ToList();

        ApplyDuplicateConflictDetection(items);
        ApplyExistingFileConflictDetection(items);

        return new ModelOrganizationPlan
        {
            Template = effectiveTemplate,
            ScopePath = scopePath,
            IncludeNested = includeNested,
            Items = items,
        };
    }

    public async Task<ModelOrganizationApplyResult> ApplyPlan(ModelOrganizationPlan plan)
    {
        var movedCount = 0;
        var skippedCount = plan.Items.Count(item => !item.CanApply);
        var conflictCount = plan.ConflictCount;
        var errors = new List<string>();

        foreach (var item in plan.Items.Where(item => item.CanApply))
        {
            try
            {
                await ApplyFileMovesAsync(item.FileMoves).ConfigureAwait(false);

                movedCount++;
            }
            catch (FileTransferExistsException e)
            {
                skippedCount++;
                conflictCount++;
                errors.Add($"Could not organize '{Path.GetFileName(item.SourcePath)}': {e.Message}");
            }
            catch (Exception e)
            {
                skippedCount++;
                errors.Add($"Could not organize '{Path.GetFileName(item.SourcePath)}': {e.Message}");
            }
        }

        return new ModelOrganizationApplyResult
        {
            MovedCount = movedCount,
            SkippedCount = skippedCount,
            ConflictCount = conflictCount,
            Errors = errors,
        };
    }

    private static async Task ApplyFileMovesAsync(IReadOnlyList<ModelOrganizationFileMove> moves)
    {
        EnsureMoveTargetsAvailable(moves);

        var completedMoves = new List<ModelOrganizationFileMove>();

        try
        {
            foreach (var move in moves.Where(move => !PathsEqual(move.SourcePath, move.TargetPath)))
            {
                var targetDirectory = Path.GetDirectoryName(move.TargetPath);
                if (!string.IsNullOrWhiteSpace(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                await FileTransfers
                    .MoveFileAsync(new FilePath(move.SourcePath), new FilePath(move.TargetPath))
                    .ConfigureAwait(false);
                completedMoves.Add(move);
            }
        }
        catch (Exception ex)
        {
            var rollbackErrors = await RollbackMovesAsync(completedMoves).ConfigureAwait(false);
            if (rollbackErrors.Count > 0)
            {
                throw new IOException(
                    $"Failed to organize files and rollback was incomplete: {string.Join("; ", rollbackErrors)}",
                    ex
                );
            }

            throw;
        }
    }

    private static void EnsureMoveTargetsAvailable(IReadOnlyList<ModelOrganizationFileMove> moves)
    {
        foreach (var move in moves.Where(move => !PathsEqual(move.SourcePath, move.TargetPath)))
        {
            if (File.Exists(move.TargetPath))
            {
                throw new FileTransferExistsException(move.SourcePath, move.TargetPath);
            }
        }
    }

    private static async Task<List<string>> RollbackMovesAsync(
        IReadOnlyCollection<ModelOrganizationFileMove> completedMoves
    )
    {
        var rollbackErrors = new List<string>();

        foreach (var move in completedMoves.Reverse())
        {
            try
            {
                if (!File.Exists(move.TargetPath) || File.Exists(move.SourcePath))
                {
                    continue;
                }

                var sourceDirectory = Path.GetDirectoryName(move.SourcePath);
                if (!string.IsNullOrWhiteSpace(sourceDirectory))
                {
                    Directory.CreateDirectory(sourceDirectory);
                }

                await FileTransfers
                    .MoveFileAsync(new FilePath(move.TargetPath), new FilePath(move.SourcePath))
                    .ConfigureAwait(false);
            }
            catch (Exception rollbackEx)
            {
                rollbackErrors.Add(
                    $"Could not restore '{Path.GetFileName(move.SourcePath)}': {rollbackEx.Message}"
                );
            }
        }

        return rollbackErrors;
    }

    private static IEnumerable<LocalModelFile> FilterModelsForScope(
        IEnumerable<LocalModelFile> models,
        string modelsRoot,
        string scopePath,
        bool includeNested
    )
    {
        var normalizedRoot = NormalizePath(modelsRoot);
        var normalizedScope = NormalizePath(scopePath);
        var includeAllModels = PathsEqual(normalizedRoot, normalizedScope);

        foreach (var model in models)
        {
            var sourcePath = model.GetFullPath(modelsRoot);
            var sourceDirectory = NormalizePath(Path.GetDirectoryName(sourcePath) ?? modelsRoot);

            if (includeAllModels)
            {
                yield return model;
                continue;
            }

            if (includeNested)
            {
                if (
                    sourceDirectory.StartsWith(
                        normalizedScope + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase
                    ) || PathsEqual(sourceDirectory, normalizedScope)
                )
                {
                    yield return model;
                }

                continue;
            }

            if (PathsEqual(sourceDirectory, normalizedScope))
            {
                yield return model;
            }
        }
    }

    private static ModelOrganizationPreviewItem BuildPreviewItem(
        LocalModelFile model,
        string scopePath,
        string template,
        string modelsRoot
    )
    {
        var sourcePath = model.GetFullPath(modelsRoot);
        if (!File.Exists(sourcePath))
        {
            return CreateSkippedItem(model, sourcePath, "Model file no longer exists.");
        }

        if (!model.HasConnectedModel)
        {
            return CreateSkippedItem(
                model,
                sourcePath,
                "Connected metadata is required to organize this file."
            );
        }

        var provider = new FileNameFormatProvider { LocalModelFile = model };
        if (!TryRenderRelativeTarget(provider, template, out var renderedTarget, out var renderError))
        {
            return CreateSkippedItem(model, sourcePath, renderError);
        }

        if (
            !TryBuildTargetPath(
                sourcePath,
                modelsRoot,
                scopePath,
                renderedTarget!,
                out var targetPath,
                out var pathError
            )
        )
        {
            return CreateSkippedItem(model, sourcePath, pathError);
        }

        var fileMoves = BuildFileMoves(sourcePath, targetPath!);
        if (fileMoves.Count == 0 || fileMoves.All(move => PathsEqual(move.SourcePath, move.TargetPath)))
        {
            return new ModelOrganizationPreviewItem
            {
                Model = model,
                SourcePath = sourcePath,
                TargetPath = targetPath,
                Status = ModelOrganizationPreviewStatus.Unchanged,
                Reason = "Already matches the current naming pattern.",
                FileMoves = fileMoves,
            };
        }

        return new ModelOrganizationPreviewItem
        {
            Model = model,
            SourcePath = sourcePath,
            TargetPath = targetPath,
            Status = ModelOrganizationPreviewStatus.Ready,
            FileMoves = fileMoves,
        };
    }

    private static ModelOrganizationPreviewItem CreateSkippedItem(
        LocalModelFile model,
        string sourcePath,
        string? reason
    )
    {
        return new ModelOrganizationPreviewItem
        {
            Model = model,
            SourcePath = sourcePath,
            Status = ModelOrganizationPreviewStatus.Skipped,
            Reason = reason,
        };
    }

    private static bool TryRenderRelativeTarget(
        FileNameFormatProvider provider,
        string template,
        out string? renderedTarget,
        out string? error
    )
    {
        var builder = new StringBuilder();
        var currentIndex = 0;

        foreach (var match in TemplateRegex.Matches(template).Cast<Match>())
        {
            if (match.Index > currentIndex)
            {
                builder.Append(template[currentIndex..match.Index]);
            }

            var variableText = match.Groups[1].Value;
            var variableName = provider.GetVariableName(variableText);
            if (!FileNameFormatProvider.LocalOrganizationVariables.Contains(variableName))
            {
                renderedTarget = null;
                error = $"Variable '{variableName}' is not supported for organizing local files.";
                return false;
            }

            if (
                !provider.TryResolveVariable(variableText, out var value, out error)
                || string.IsNullOrWhiteSpace(value)
            )
            {
                renderedTarget = null;
                return false;
            }

            builder.Append(SanitizeSubstitutionValue(value));
            currentIndex = match.Index + match.Length;
        }

        if (currentIndex < template.Length)
        {
            builder.Append(template[currentIndex..]);
        }

        renderedTarget = builder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(renderedTarget))
        {
            error = "The naming pattern resolved to an empty path.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryBuildTargetPath(
        string sourcePath,
        string modelsRoot,
        string scopePath,
        string renderedTarget,
        out string? targetPath,
        out string? error
    )
    {
        if (Path.IsPathRooted(renderedTarget))
        {
            targetPath = null;
            error = "Absolute paths are not allowed in the naming pattern.";
            return false;
        }

        var normalizedRelativeTarget = renderedTarget.Replace(
            Path.AltDirectorySeparatorChar,
            Path.DirectorySeparatorChar
        );
        var segments = normalizedRelativeTarget.Split(
            DirectorySeparators,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

        if (
            segments.Length == 0
            || segments.Any(segment =>
                segment is "." or ".."
                || string.IsNullOrWhiteSpace(segment)
                || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            )
        )
        {
            targetPath = null;
            error = "The naming pattern resolved to an invalid path.";
            return false;
        }

        var sourceDirectory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var relativeDirectory = segments.Length > 1 ? Path.Combine(segments[..^1]) : string.Empty;

        string destinationDirectory;
        if (string.IsNullOrWhiteSpace(relativeDirectory))
        {
            // Single-segment template (e.g. "{file_name}") — keep in the source directory
            destinationDirectory = sourceDirectory;
        }
        else
        {
            destinationDirectory = Path.Combine(
                ResolveOrganizationBaseDirectory(sourcePath, modelsRoot, scopePath),
                relativeDirectory
            );
        }

        // Use segments[^1] directly as the target base name.
        // The template variables (e.g. {file_name}) already return names without extensions,
        // so calling Path.GetFileNameWithoutExtension here would incorrectly strip
        // content after dots in the name (e.g. "wan2.1_model" → "wan2").
        var targetBaseName = segments[^1];
        if (string.IsNullOrWhiteSpace(targetBaseName))
        {
            targetPath = null;
            error = "The naming pattern did not produce a valid file name.";
            return false;
        }

        targetPath = Path.Combine(destinationDirectory, targetBaseName + Path.GetExtension(sourcePath));
        error = null;
        return true;
    }

    private static string ResolveOrganizationBaseDirectory(
        string sourcePath,
        string modelsRoot,
        string scopePath
    )
    {
        if (!PathsEqual(modelsRoot, scopePath))
        {
            return NormalizePath(scopePath);
        }

        // Organizing from the models root should still keep models within their top-level type folder
        // (Lora, StableDiffusion, etc.).
        var relativeToRoot = Path.GetRelativePath(modelsRoot, sourcePath);
        var pathComponents = relativeToRoot.Split(DirectorySeparators, StringSplitOptions.RemoveEmptyEntries);
        var typeFolder = pathComponents.Length > 1 ? pathComponents[0] : string.Empty;

        return string.IsNullOrWhiteSpace(typeFolder)
            ? NormalizePath(modelsRoot)
            : Path.Combine(NormalizePath(modelsRoot), typeFolder);
    }

    private static List<ModelOrganizationFileMove> BuildFileMoves(string sourcePath, string targetPath)
    {
        var moves = new List<ModelOrganizationFileMove>
        {
            new() { SourcePath = sourcePath, TargetPath = targetPath },
        };

        var sourceDirectory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var targetDirectory = Path.GetDirectoryName(targetPath) ?? string.Empty;
        var sourceBaseName = Path.GetFileNameWithoutExtension(sourcePath);
        var targetBaseName = Path.GetFileNameWithoutExtension(targetPath);

        var cmInfoPath = Path.Combine(sourceDirectory, sourceBaseName + ConnectedModelInfo.FileExtension);
        if (File.Exists(cmInfoPath))
        {
            moves.Add(
                new ModelOrganizationFileMove
                {
                    SourcePath = cmInfoPath,
                    TargetPath = Path.Combine(
                        targetDirectory,
                        targetBaseName + ConnectedModelInfo.FileExtension
                    ),
                }
            );
        }

        foreach (var previewPath in Directory.EnumerateFiles(sourceDirectory, $"{sourceBaseName}.preview.*"))
        {
            moves.Add(
                new ModelOrganizationFileMove
                {
                    SourcePath = previewPath,
                    TargetPath = Path.Combine(
                        targetDirectory,
                        $"{targetBaseName}.preview{Path.GetExtension(previewPath)}"
                    ),
                }
            );
        }

        var yamlPath = Path.Combine(sourceDirectory, $"{sourceBaseName}.yaml");
        if (File.Exists(yamlPath))
        {
            moves.Add(
                new ModelOrganizationFileMove
                {
                    SourcePath = yamlPath,
                    TargetPath = Path.Combine(targetDirectory, $"{targetBaseName}.yaml"),
                }
            );
        }

        return moves;
    }

    private static void ApplyDuplicateConflictDetection(List<ModelOrganizationPreviewItem> items)
    {
        var duplicateGroups = items
            .Where(item => item.Status == ModelOrganizationPreviewStatus.Ready && item.TargetPath is not null)
            .GroupBy(item => NormalizePath(item.TargetPath!))
            .Where(group => group.Count() > 1);

        foreach (var group in duplicateGroups)
        {
            foreach (var item in group.ToList())
            {
                ReplacePreviewItem(
                    items,
                    item,
                    item with
                    {
                        Status = ModelOrganizationPreviewStatus.Conflict,
                        Reason = "Multiple models resolve to the same target path.",
                    }
                );
            }
        }
    }

    private static void ApplyExistingFileConflictDetection(List<ModelOrganizationPreviewItem> items)
    {
        foreach (
            var item in items.Where(item => item.Status == ModelOrganizationPreviewStatus.Ready).ToList()
        )
        {
            var conflictingMove = item.FileMoves.FirstOrDefault(move =>
                File.Exists(move.TargetPath) && !PathsEqual(move.SourcePath, move.TargetPath)
            );

            if (conflictingMove is not null)
            {
                var conflictFile = Path.GetFileName(conflictingMove.TargetPath);
                ReplacePreviewItem(
                    items,
                    item,
                    item with
                    {
                        Status = ModelOrganizationPreviewStatus.Conflict,
                        Reason = $"A file already exists at the destination: {conflictFile}",
                    }
                );
            }
        }
    }

    private static void ReplacePreviewItem(
        List<ModelOrganizationPreviewItem> items,
        ModelOrganizationPreviewItem source,
        ModelOrganizationPreviewItem replacement
    )
    {
        var index = items.IndexOf(source);
        if (index >= 0)
        {
            items[index] = replacement;
        }
    }

    private static string SanitizeSubstitutionValue(string value)
    {
        return string.Concat(value.Select(ch => InvalidSubstitutionChars.Contains(ch) ? '_' : ch)).Trim();
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (left is null || right is null)
            return false;

        return string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);
    }
}
