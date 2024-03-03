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

// from https://raw.githubusercontent.com/Stability-AI/StableSwarmUI/master/src/Core/Settings.cs


using FreneticUtilities.FreneticDataSyntax;

namespace StabilityMatrix.Core.Models.FDS;

/// <summary>Central default settings list.</summary>
public class StableSwarmSettings : AutoConfiguration
{
    [ConfigComment("Settings related to file paths.")]
    public PathsData Paths = new();

    [ConfigComment("Settings related to networking and the webserver.")]
    public NetworkData Network = new();

    [ConfigComment("Restrictions to apply to default users.")]
    public UserRestriction DefaultUserRestriction = new();

    [ConfigComment(
        "Default settings for users (unless the user modifies them, if so permitted).\n(NOTE: Usually, don't edit this. Go to the 'User' tab to edit your User-Settings)."
    )]
    public User DefaultUser = new();

    [ConfigComment("Settings related to backends.")]
    public BackendData Backends = new();

    [ConfigComment(
        "If this is set to 'true', hides the installer page. If 'false', the installer page will be shown."
    )]
    public bool IsInstalled = false;

    [ConfigComment(
        "Ratelimit, in milliseconds, between Nvidia GPU status queries. Default is 1000 ms (1 second)."
    )]
    public long NvidiaQueryRateLimitMS = 1000;

    [ConfigComment(
        "How to launch the UI. If 'none', just quietly launch.\nIf 'web', launch your web-browser to the page.\nIf 'webinstall', launch web-browser to the install page.\nIf 'electron', launch the UI in an electron window (NOT YET IMPLEMENTED)."
    )]
    [ManualSettingsOptions(Impl = null, Vals = new string[] { "none", "web", "webinstall", "electron" })]
    public string LaunchMode = "webinstall";

    [ConfigComment("The minimum tier of logs that should be visible in the console.\nDefault is 'info'.")]
    public string LogLevel = "Info";

    [ConfigComment("Settings related to the User Interface.")]
    public UIData UI = new();

    [ConfigComment("Settings related to webhooks.")]
    public WebHooksData WebHooks = new();

    /// <summary>Settings related to backends.</summary>
    public class BackendData : AutoConfiguration
    {
        [ConfigComment("How many times to retry initializing a backend before giving up. Default is 3.")]
        public int MaxBackendInitAttempts = 3;

        [ConfigComment(
            "Safety check, the maximum duration all requests can be waiting for a backend before the system declares a backend handling failure."
        )]
        public int MaxTimeoutMinutes = 20;

        [ConfigComment(
            "The maximum duration an individual request can be waiting on a backend to be available before giving up.\n"
                + "Not to be confused with 'MaxTimeoutMinutes' which requires backends be unresponsive for that duration, this duration includes requests that are merely waiting because other requests are queued."
                + "\nDefaults to 60 * 24 * 7 = 1 week (ultra-long max queue duration)."
        )]
        public int PerRequestTimeoutMinutes = 60 * 24 * 7;

        [ConfigComment(
            "The maximum number of pending requests to continue forcing orderly processing of.\nOver this limit, requests may start going out of order."
        )]
        public int MaxRequestsForcedOrder = 20;

        [ConfigComment(
            "How many minutes to wait after the last generation before automatically freeing up VRAM (to prevent issues with other programs).\nThis has the downside of a small added bit of time to load back onto VRAM at next usage.\nUse a decimal number to free after seconds.\nDefaults to 10 minutes."
        )]
        public double ClearVRAMAfterMinutes = 10;

        [ConfigComment(
            "How many minutes to wait after the last generation before automatically freeing up system RAM (to prevent issues with other programs).\nThis has the downside of causing models to fully load from data drive at next usage.\nUse a decimal number to free after seconds.\nDefaults to 60 minutes (one hour)."
        )]
        public double ClearSystemRAMAfterMinutes = 60;
    }

    /// <summary>Settings related to networking and the webserver.</summary>
    public class NetworkData : AutoConfiguration
    {
        [ConfigComment(
            "What web host address to use. `localhost` means your PC only."
                + "\nLinux users may use `0.0.0.0` to mean accessible to anyone that can connect to your PC (ie LAN users, or the public if your firewall is open)."
                + "\nWindows users may use `*` for that, though it may require additional Windows firewall configuration."
                + "\nAdvanced server users may wish to manually specify a host bind address here."
        )]
        public string Host = "localhost";

        [ConfigComment("What web port to use. Default is '7801'.")]
        public int Port = 7801;

        [ConfigComment(
            "If true, if the port is already in use, the server will try to find another port to use instead.\nIf false, the server will fail to start if the port is already in use."
        )]
        public bool PortCanChange = true;

        [ConfigComment(
            "Backends are automatically assigned unique ports. This value selects which port number to start the assignment from.\nDefault is '7820'."
        )]
        public int BackendStartingPort = 7820;
    }

    /// <summary>Settings related to file paths.</summary>
    public class PathsData : AutoConfiguration
    {
        [ConfigComment(
            "Root path for model files. Use a full-formed path (starting with '/' or a Windows drive like 'C:') to use an absolute path.\nDefaults to 'Models'."
        )]
        public string ModelRoot = "Models";

        [ConfigComment(
            "The model folder to use within 'ModelRoot'.\nDefaults to 'Stable-Diffusion'.\nAbsolute paths work too."
        )]
        public string SDModelFolder = "Stable-Diffusion";

        [ConfigComment(
            "The LoRA (or related adapter type) model folder to use within 'ModelRoot'.\nDefaults to 'Lora'.\nAbsolute paths work too."
        )]
        public string SDLoraFolder = "Lora";

        [ConfigComment(
            "The VAE (autoencoder) model folder to use within 'ModelRoot'.\nDefaults to 'VAE'.\nAbsolute paths work too."
        )]
        public string SDVAEFolder = "VAE";

        [ConfigComment(
            "The Embedding (eg textual inversion) model folder to use within 'ModelRoot'.\nDefaults to 'Embeddings'.\nAbsolute paths work too."
        )]
        public string SDEmbeddingFolder = "Embeddings";

        [ConfigComment(
            "The ControlNets model folder to use within 'ModelRoot'.\nDefaults to 'controlnet'.\nAbsolute paths work too."
        )]
        public string SDControlNetsFolder = "controlnet";

        [ConfigComment(
            "The CLIP Vision model folder to use within 'ModelRoot'.\nDefaults to 'clip_vision'.\nAbsolute paths work too."
        )]
        public string SDClipVisionFolder = "clip_vision";

        [ConfigComment("Root path for data (user configs, etc).\nDefaults to 'Data'")]
        public string DataPath = "Data";

        [ConfigComment("Root path for output files (images, etc).\nDefaults to 'Output'")]
        public string OutputPath = "Output";

        [ConfigComment("The folder for wildcard (.txt) files, under Data.\nDefaults to 'Wildcards'")]
        public string WildcardsFolder = "Wildcards";

        [ConfigComment(
            "When true, output paths always have the username as a folder.\nWhen false, this will be skipped.\nKeep this on in multi-user environments."
        )]
        public bool AppendUserNameToOutputPath = true;
    }

    /// <summary>Settings to control restrictions on users.</summary>
    public class UserRestriction : AutoConfiguration
    {
        [ConfigComment("How many directories deep a user's custom OutPath can be.\nDefault is 5.")]
        public int MaxOutPathDepth = 5;

        [ConfigComment("Which user-settings the user is allowed to modify.\nDefault is all of them.")]
        public List<string> AllowedSettings = new() { "*" };

        [ConfigComment(
            "If true, the user is treated as a full admin.\nThis includes the ability to modify these settings."
        )]
        public bool Admin = false;

        [ConfigComment("If true, user may load models.\nIf false, they may only use already-loaded models.")]
        public bool CanChangeModels = true;

        [ConfigComment(
            "What models are allowed, as a path regex.\nDirectory-separator is always '/'. Can be '.*' for all, 'MyFolder/.*' for only within that folder, etc.\nDefault is all."
        )]
        public string AllowedModels = ".*";

        [ConfigComment("Generic permission flags. '*' means all.\nDefault is all.")]
        public List<string> PermissionFlags = new() { "*" };

        [ConfigComment("How many images can try to be generating at the same time on this user.")]
        public int MaxT2ISimultaneous = 32;
    }

    /// <summary>Settings per-user.</summary>
    public class User : AutoConfiguration
    {
        public class OutPath : AutoConfiguration
        {
            [ConfigComment(
                "Builder for output file paths. Can use auto-filling placeholders like '[model]' for the model name, '[prompt]' for a snippet of prompt text, etc.\n"
                    + "Full details in the docs: https://github.com/Stability-AI/StableSwarmUI/blob/master/docs/User%20Settings.md#path-format"
            )]
            public string Format = "raw/[year]-[month]-[day]/[hour][minute]-[prompt]-[model]-[seed]";

            [ConfigComment("How long any one part can be.\nDefault is 40 characters.")]
            public int MaxLenPerPart = 40;
        }

        [ConfigComment("Settings related to output path building.")]
        public OutPath OutPathBuilder = new();

        public class FileFormatData : AutoConfiguration
        {
            [ConfigComment("What format to save images in.\nDefault is '.jpg' (at 100% quality).")]
            public string ImageFormat = "JPG";

            [ConfigComment("Whether to store metadata into saved images.\nDefaults enabled.")]
            public bool SaveMetadata = true;

            [ConfigComment(
                "If set to non-0, adds DPI metadata to saved images.\n'72' is a good value for compatibility with some external software."
            )]
            public int DPI = 0;

            [ConfigComment(
                "If set to true, a '.txt' file will be saved alongside images with the image metadata easily viewable.\nThis can work even if saving in the image is disabled. Defaults disabled."
            )]
            public bool SaveTextFileMetadata = false;
        }

        [ConfigComment("Settings related to saved file format.")]
        public FileFormatData FileFormat = new();

        [ConfigComment("Whether your files save to server data drive or not.")]
        public bool SaveFiles = true;

        [ConfigComment("If true, folders will be discard from starred image paths.")]
        public bool StarNoFolders = false;

        [ConfigComment("What theme to use. Default is 'dark_dreams'.")]
        public string Theme = "dark_dreams";

        [ConfigComment(
            "If enabled, batch size will be reset to 1 when parameters are loaded.\nThis can prevent accidents that might thrash your GPU or cause compatibility issues, especially for example when importing a comfy workflow.\nYou can still set the batch size at will in the GUI."
        )]
        public bool ResetBatchSizeToOne = false;

        public enum HintFormatOptions
        {
            BUTTON,
            HOVER,
            NONE
        }

        [ConfigComment("The format for parameter hints to display as.\nDefault is 'BUTTON'.")]
        [SettingsOptions(Impl = typeof(SettingsOptionsAttribute.ForEnum<HintFormatOptions>))]
        public string HintFormat = "BUTTON";

        public class VAEsData : AutoConfiguration
        {
            [ConfigComment(
                "What VAE to use with SDXL models by default. Use 'None' to use the one in the model."
            )]
            [ManualSettingsOptions(Impl = null, Vals = new string[] { "None" })]
            public string DefaultSDXLVAE = "None";

            [ConfigComment(
                "What VAE to use with SDv1 models by default. Use 'None' to use the one in the model."
            )]
            [ManualSettingsOptions(Impl = null, Vals = new string[] { "None" })]
            public string DefaultSDv1VAE = "None";
        }

        [ConfigComment("Options to override default VAEs with.")]
        public VAEsData VAEs = new();

        [ConfigComment(
            "When generating live previews, this is how many simultaneous generation requests can be waiting at one time."
        )]
        public int MaxSimulPreviews = 1;

        [ConfigComment(
            "Set to a number above 1 to allow generations of multiple images to automatically generate square mini-grids when they're done."
        )]
        public int MaxImagesInMiniGrid = 1;

        [ConfigComment("How many images the history view should stop trying to load after.")]
        public int MaxImagesInHistory = 1000;

        [ConfigComment(
            "If true, the Image History view will cache small preview thumbnails of images.\nThis should make things run faster. You can turn it off if you don't want that."
        )]
        public bool ImageHistoryUsePreviews = true;

        [ConfigComment(
            "Delay, in seconds, betweeen Generate Forever updates.\nIf the delay hits and a generation is still waiting, it will be skipped.\nDefault is 0.1 seconds."
        )]
        public double GenerateForeverDelay = 0.1;

        [ConfigComment("What language to display the UI in.\nDefault is 'en' (English).")]
        public string Language = "en";
    }

    /// <summary>UI-related settings.</summary>
    public class UIData : AutoConfiguration
    {
        [ConfigComment(
            "Optionally specify a (raw HTML) welcome message here. If specified, will override the automatic welcome messages."
        )]
        public string OverrideWelcomeMessage = "";
    }

    /// <summary>Webhook settings.</summary>
    public class WebHooksData : AutoConfiguration
    {
        [ConfigComment(
            "Webhook to call (empty JSON POST) when queues are starting up from idle.\nLeave empty to disable any webhook.\nCall must return before the first generation starts."
        )]
        public string QueueStartWebhook = "";

        [ConfigComment(
            "Webhook to call (empty JSON POST) when all queues are done and the server is going idle.\nLeave empty to disable any webhook.\nCall must return before queuing may restart."
        )]
        public string QueueEndWebhook = "";

        [ConfigComment(
            "How long to wait (in seconds) after all queues are done before sending the queue end webhook.\nThis is useful to prevent rapid start+end calls."
        )]
        public double QueueEndDelay = 1;
    }
}

[AttributeUsage(AttributeTargets.Field)]
public class SettingsOptionsAttribute : Attribute
{
    public abstract class AbstractImpl
    {
        public abstract string[] GetOptions { get; }
    }

    public class ForEnum<T> : AbstractImpl
        where T : Enum
    {
        public override string[] GetOptions => Enum.GetNames(typeof(T));
    }

    public Type Impl;

    public virtual string[] Options => (Activator.CreateInstance(Impl) as AbstractImpl).GetOptions;
}

[AttributeUsage(AttributeTargets.Field)]
public class ManualSettingsOptionsAttribute : SettingsOptionsAttribute
{
    public string[] Vals;

    public override string[] Options => Vals;
}
