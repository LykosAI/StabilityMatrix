# Accounts

The Accounts settings page manages external service integrations. Connecting accounts allows Stability Matrix to download models that require authentication, access membership features, and use cloud-based image generation services.

[`Section Overview`](overview.md) | [`Home`](../README.md)

## Table of Contents

- [Membership and Lykos Account](#membership-and-lykos-account)
- [CivitAI API Key](#civitai-api-key)
- [HuggingFace Token](#huggingface-token)
- [Image Generation APIs](#image-generation-apis)

---

## Membership and Lykos Account

A Lykos account is the central identity for Stability Matrix. Lykos is the team behind Stability Matrix, and signing in unlocks membership benefits and account-linked features across the application.

### Membership

Lykos offers a membership program that supports ongoing development, access to pre-release preview builds of Stability Matrix, and unlocks additional features on the [Lykos.ai](https://lykos.ai) website. Membership is managed entirely in-house by the Lykos team at [lykos.ai/membership](https://lykos.ai/membership?ref=a1).

| Tier | Description |
|---|---|
| **Visionary** | Highest monthly credits and vote power. Shout-out in both release and Preview/Dev changelog. Maximum support for development. |
| **Pioneer** | Higher monthly credits and vote power. Shout-out in release changelog. |
| **Insider** | Increased credits and vote power over Supporter. Accellerated Model Discovery |
| **Supporter** | 1,000 monthly credits, 2× vote power. The entry-level tier that supports development and server costs. |

All support tiers receive special roles and dedicated supporter channel on the Stability Matrix Discord server and early access to new features through **Preview** and **Dev** update channels in [Update Settings](updates.md).

When you are an active subscriber, the Accounts page shows your tier, the date you started supporting, and a quick link to manage your membership. A thank-you message and community Discord link are also displayed.

If you are not subscribed, a membership card appears with a **Become a Supporter** button that opens the Lykos membership page in your browser.

### Connecting a Lykos Account

1. Navigate to **Settings → Accounts**.
2. Under the Lykos section, click **Connect**.
3. A device authentication dialog appears with a code.
4. Open your browser and go to the URL shown in the dialog.
5. Enter the code to authorize Stability Matrix.
6. Once authorized, the dialog closes and your account appears on the Accounts page.

> [!NOTE]
> The device code flow does not require entering your email or password into Stability Matrix itself. Authentication happens entirely in your browser on the Lykos website.

Your profile image is fetched from [Gravatar](https://gravatar.com/) using the email address associated with your Lykos account. You can update your avatar at any time through Gravatar, accessible from the profile image context menu.

### Managing Your Account

Once connected, clicking your profile image provides quick actions:

- **Manage Lykos Account** — opens the Lykos account management page in your browser, where you can update your subscription, email, and password
- **Edit Gravatar** — opens gravatar.com to change your profile picture
- **Copy User ID** — copies your Lykos user ID to the clipboard

### Disconnecting

To disconnect your Lykos account, click **Disconnect** under the Lykos section. This removes your stored tokens and signs you out. Your membership can be restored by reconnecting.

---

## CivitAI API Key

A CivitAI API key enables model downloads from CivitAI that require authentication and unlocks additional integrations.

### What the API Key Unlocks

- **Gated model downloads** — models behind an "early-access" paywall on CivitAI
- **NSFW content browsing** — accessing NSFW-tagged models in the Model Browser (requires enabling the NSFW toggle in the Model Browser itself)

### How to Get an API Key

1. Log in to [civitai.com](https://civitai.com).
2. Click on avatar in upper-right. Go to **Account Settings → API Keys**.
3. Click **Create API Key** and give it a name (e.g., "Stability Matrix").
4. Copy the generated key. Save this key somewhere safe (ie: text file, password/note manager, etc.) as CivitAI will only provide this key once.

### Connecting in Stability Matrix

1. Navigate to **Settings → Accounts**.
2. Under the CivitAI section, click **Connect**.
3. Paste your API key into the dialog and click **Connect**.

Stability Matrix validates the key by fetching your CivitAI profile. Once validated, your CivitAI username appears on the Accounts page.

### How the Key Is Used

The API key is sent as a `Bearer` token in the `Authorization` header for all requests to `civitai.com`, including model downloads and API calls. This allows Stability Matrix to download models that would otherwise return an authentication error.

### Security

API keys are stored encrypted at rest in the `user-secrets.data` file inside Stability Matrix's application data directory. The key is not stored in plain text, and the file is encrypted using a key derived from system-specific identifiers.

The `user-secrets.data` location depends on your operating system:

| Platform | Path |
|---|---|
| Windows | `%APPDATA%\StabilityMatrix\user-secrets.data` (`C:\Users\{username}\AppData\Roaming\StabilityMatrix`) |
| Linux | `~/.config/StabilityMatrix/user-secrets.data` |
| macOS | `~/Library/Application Support/StabilityMatrix/user-secrets.data` |

### Disconnecting

To remove your CivitAI API key, click **Disconnect** under the CivitAI section. This deletes the stored key and stops sending authentication headers on CivitAI requests.

---

## HuggingFace Token

A HuggingFace access token enables downloading gated models from the Hugging Face Hub.

### What the Token Unlocks

- **Gated models** — models that require accepting a license agreement before download (e.g., FLUX,)

### Important: Accept the Model License First

Even with a valid token, gated models require you to accept the model's license terms on the Hugging Face website before the download will succeed. Visit the model's page on [huggingface.co](https://huggingface.co) and click **Agree and access repository** before attempting to download through Stability Matrix.

### How to Get a Token

1. Log in to [huggingface.co](https://huggingface.co).
2. Go to **Settings → Access Tokens**.
3. Click **Create new token**.
4. Select the **Read** permission type (sufficient for model downloads).
5. Give the token a name (e.g., "Stability Matrix").
6. Copy the generated token. Save this key somewhere safe (ie: text file, password/note manager, etc.) as HuggingFace will only provide this key once.

### Connecting in Stability Matrix

1. Navigate to **Settings → Accounts**.
2. Under the HuggingFace section, click **Connect**.
3. Paste your token into the dialog and click **Connect**.

Stability Matrix validates the token by calling the HuggingFace API. Once validated, your HuggingFace username appears on the Accounts page.

### How the Token Is Used

The token is used in two ways:

1. **Direct downloads** — when Stability Matrix downloads a model directly from `huggingface.co`, the token is sent as a `Bearer` token in the `Authorization` header
2. **User-configurable environment variable** — you can inject the token into launched packages by setting `HF_TOKEN` in the [Environment Variables editor](../advanced/environment-variables.md). Stability Matrix does **not** automatically inject the token into package environments.

### Security

The token is stored encrypted at rest in the `user-secrets.data` file inside Stability Matrix's application data directory, using the same encryption mechanism as the CivitAI API key.

### Disconnecting

To remove your HuggingFace token, click **Disconnect** under the HuggingFace section. This deletes the stored token. Direct downloads from Hugging Face will no longer include authentication headers.

> [!IMPORTANT]
> If you previously set `HF_TOKEN` manually in the Environment Variables editor, disconnecting here does **not** remove it from there. You must remove it from the Environment Variables editor separately.

---

## Image Generation APIs

The Image Generation APIs section contains keys for cloud-based image generation services used by the **Image Lab** feature.

### Gemini API Key

A Gemini API key enables the **Nano Banana** image generation provider in the Image Lab, which uses Google's Gemini models for AI-powered image generation.

> [!NOTE]
> The Gemini API key requires a paid-tier Google AI account with billing enabled. Free-tier keys will not work for Image Lab generation.

#### How to Get an API Key

1. Go to [Google AI Studio](https://aistudio.google.com/api-keys).
2. Sign in with your Google account.
3. Click **Create API Key**.
4. Copy the generated key.

#### Connecting in Stability Matrix

1. Navigate to **Settings → Accounts**.
2. Under the **Image Generation APIs** section, click **Connect** next to Gemini API.
3. Paste your API key into the dialog and click **Connect**.

Stability Matrix checks that a key is present. The key is validated at generation time when the Image Lab makes its first request.

#### Security

The API key is stored encrypted at rest in the `user-secrets.data` file inside Stability Matrix's application data directory, using the same encryption mechanism as other stored credentials.

#### Disconnecting

To remove your Gemini API key, click **Disconnect** under the Gemini API section. This deletes the stored key. The Nano Banana provider in the Image Lab will no longer be available until a new key is added.
