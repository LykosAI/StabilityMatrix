// The MIT License (MIT)
//
// Copyright (c) 2024 Stability AI
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
//     of this software and associated documentation files (the "Software"), to deal
//     in the Software without restriction, including without limitation the rights
//     to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//     copies of the Software, and to permit persons to whom the Software is
//     furnished to do so, subject to the following conditions:
//
//     The above copyright notice and this permission notice shall be included in all
//     copies or substantial portions of the Software.
//
//     THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//     IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//     FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//     AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//     LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//     OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//     SOFTWARE.

// from https://github.com/Stability-AI/StableSwarmUI/blob/master/src/BuiltinExtensions/ComfyUIBackend/ComfyUISelfStartBackend.cs


using FreneticUtilities.FreneticDataSyntax;

namespace StabilityMatrix.Core.Models.FDS;

public class ComfyUiSelfStartSettings : AutoConfiguration
{
    [ConfigComment(
        "The location of the 'main.py' file. Can be an absolute or relative path, but must end with 'main.py'.\nIf you used the installer, this should be 'dlbackend/ComfyUI/main.py'."
    )]
    public string StartScript = "";

    [ConfigComment("Any arguments to include in the launch script.")]
    public string ExtraArgs = "";

    [ConfigComment(
        "If unchecked, the system will automatically add some relevant arguments to the comfy launch. If checked, automatic args (other than port) won't be added."
    )]
    public bool DisableInternalArgs = false;

    [ConfigComment("If checked, will automatically keep the comfy backend up to date when launching.")]
    public bool AutoUpdate = true;

    [ConfigComment(
        "If checked, tells Comfy to generate image previews. If unchecked, previews will not be generated, and images won't show up until they're done."
    )]
    public bool EnablePreviews = true;

    [ConfigComment("Which GPU to use, if multiple are available.")]
    public int GPU_ID = 0;

    [ConfigComment("How many extra requests may queue up on this backend while one is processing.")]
    public int OverQueue = 1;

    [ConfigComment(
        "Whether the Comfy backend should automatically update nodes within Swarm's managed nodes folder.\nYou can update every launch, never update automatically, or force-update (bypasses some common git issues)."
    )]
    public string UpdateManagedNodes = "true";
}
