# Terminology

Common image and video generation terms you will see in Stability Matrix, upstream package documentation, model cards, and community guides.

[`Section Overview`](overview.md) | [`Home`](../README.md)

## Table of Contents

- [Core Generation Terms](#core-generation-terms)
- [Model Components](#model-components)
- [Conditioning and Guidance](#conditioning-and-guidance)
- [Image Editing Terms](#image-editing-terms)
- [Model Add-Ons and Variants](#model-add-ons-and-variants)
- [Video Terms](#video-terms)
- [Performance and Precision Terms](#performance-and-precision-terms)

---

## Core Generation Terms

**How these terms fit together**

Most image-generation workflows start with a model, a prompt, a seed, and a set of sampling controls. The model defines the ecosystem and capabilities, the prompt describes what you want, the seed picks a starting noise pattern, and the sampler-related settings determine how that noise turns into a final image.

**Checkpoint / Model**

A checkpoint or model is the trained weight file or model bundle used for generation or editing. In older Stable Diffusion ecosystems this is often a single `.safetensors` file. In newer families such as FLUX.2, Qwen Image Edit, Z-Image, and WAN 2.x, the usable model may instead be split into multiple files or distributed as a diffusers-style bundle.

For most users, the model is the main thing that determines what kinds of outputs and workflows are possible. It affects whether a setup leans toward realism, illustration, text rendering, editing, or video, and it often determines which secondary files or add-ons are compatible.

**Prompt**

The prompt is the text description telling the model what you want to generate. It can be a short phrase, a structured prompt, or a more natural instruction depending on the model family.

Different ecosystems respond differently to prompt style. Some older families reward tag-heavy phrasing, ordered keyword lists, and short descriptive fragments. Many newer families respond better to plain-language instructions or longer semantic descriptions.

**Negative Prompt**

A negative prompt describes what you do not want in the result. It is still very important in SDXL-based families such as SDXL 1.0, Pony, Illustrious, and NoobAI. It is usually less dominant in newer instruction-led families such as FLUX Kontext, Qwen Image Edit, Anima, and some WAN editing or video workflows.

They are commonly used to suppress artifacts, anatomy problems, unwanted text, watermarks, muddy detail, or style traits you do not want carried into the final output.

**Seed**

The seed is the random starting value used to initialize generation. Same model plus same prompt plus same settings plus same seed usually means a reproducible output.

Changing the seed while keeping everything else fixed usually changes composition, pose, framing, or local detail arrangement while staying in the same general prompt space. That is why users often keep a seed when refining a result, but change it when they want fresh variations.

**Steps**

Steps are the number of denoising iterations used to turn noise into an image. More steps usually improve detail up to a point, then start giving diminishing returns.

Too few steps can leave the output muddy or undercooked. Too many can waste time, overcook textures, or add very little additional quality depending on the model, sampler, and scheduler combination.

**Sampler**

The sampler is the algorithm that decides how each denoising step is performed. Common examples include Euler, Euler Ancestral, DPM++ 2M, and UniPC.

Sampler choice can change the feel of an image even when the prompt and seed stay the same. Different samplers can affect sharpness, smoothness, contrast, painterliness, stability, and how "creative" or literal the result feels.

**Scheduler**

The scheduler is the noise schedule used by the sampler across the denoising process. Common examples include Normal, Karras, Exponential, and SGM Uniform.

The simplest way to picture it is that the sampler is the solver, while the scheduler controls how noise levels are spaced through the run. That is why some guides recommend not just a sampler, but a sampler and scheduler pairing.

**CFG / Guidance Scale**

CFG means Classifier-Free Guidance. It controls how strongly the output follows the prompt. Lower CFG usually gives looser, more flexible, or more creative output. Higher CFG usually pushes the model to obey the prompt more strictly, but it can also introduce artifacts or make the image feel forced.

Useful ranges vary by family, and the numbers below are community rules of thumb that shift with each model and fine-tune rather than fixed rules. SDXL-style models often live around 4.5 to 8, FLUX dev-style models often work around 3 to 5, and turbo or distilled models such as Z-Image Turbo may work better much closer to 1.0 to 3.0. If CFG is too high, images can become brittle, oversaturated, distorted, or unnatural. If it is too low, the image may drift away from the prompt, fall back toward the model's built-in composition biases, or come out washed out and lacking detail.

**Denoise Strength**

Denoise strength controls how much of the input image is changed in image-to-image and related latent editing workflows. Low denoise preserves more of the original composition and structure. High denoise gives the model more freedom to reinterpret or replace what it started from.

This is one of the most important edit-workflow settings because it determines whether you are making a light refinement, a substantial reinterpretation, or something close to starting over from the source image.

## Model Components

**What each component does in the pipeline**

In a typical diffusion pipeline, your prompt is first turned into machine-readable vectors by a text encoder. The generator then starts from random noise in a compressed space called the latent, repeatedly denoises that latent, and finally converts the latent back into pixels. Older Stable Diffusion families usually do this with a UNet-style denoiser; newer families often use a DiT-style denoiser instead.

**UNet**

The UNet is the denoising network used in traditional Stable Diffusion architectures. It is called "U-Net" because its original design has a downsampling path and an upsampling path with skip connections between them, which gives it a U-shaped structure.

In image generation, the UNet does not directly paint pixels from scratch. Instead, it looks at a noisy latent and predicts how to move that latent toward a cleaner image representation step by step. Each denoising step uses the prompt conditioning plus the current noise level to decide what should be kept, changed, or clarified.

SD 1.5, SDXL 1.0, and most SDXL fine-tune ecosystems such as Pony, Illustrious, and NoobAI are still UNet-based. Many surrounding tools such as ControlNet and IP-Adapter were also built first around UNet-style diffusion pipelines, which is why those ecosystems often feel especially mature.

**DiT**

DiT stands for Diffusion Transformer. It fills the same broad role as a UNet, but uses transformer-style attention blocks instead of the classic UNet layout as the core denoiser.

The idea is still the same: start from noise, then repeatedly predict a cleaner version. The difference is architectural. A DiT-based model is using transformer machinery to reason over the latent representation, which can improve scaling behavior and make it easier to build newer large-model families around attention-heavy designs.

When a guide says a model is "DiT-based," it usually means the main denoising engine is not a classic Stable Diffusion UNet. FLUX.1, FLUX.2, Qwen Image and Qwen Image Edit, Z-Image, and several newer video families fall into this broader transformer-led direction.

**VAE**

VAE stands for Variational Autoencoder. In image-generation workflows, the VAE is the component that converts between normal image pixels and the model's numerical, sometimes compressed if using a UNet workflow, representative latent space.

You can think of it as a translator between two worlds:

- VAE encode: image -> latent
- VAE decode: latent -> image

The denoiser usually works in latent space because latent tensors are much smaller than full-resolution images, which makes diffusion practical on consumer hardware. The VAE is what lets the pipeline move into that latent space and back out again.

This is also why the wrong VAE can visibly damage output. Common symptoms include washed-out colors, odd contrast, muddy textures, or images that simply do not decode correctly and result in an error. In older SD and SDXL workflows, matching the intended VAE can matter a lot.

**Latent**

A latent is the model's compressed internal representation of an image. It is not a normal image you would want to look at directly. It is a lower-dimensional, abstract data space where the model can represent composition, structure, color relationships, and other image information more efficiently.

Diffusion models usually do most of their work in latent space because operating directly on full-resolution pixels would be much heavier in VRAM, memory bandwidth, and compute time. So when people say a model is "denoising the latent," they mean it is gradually turning a noisy compressed representation into a clean compressed representation that can later be decoded into pixels.

This also explains why many settings affect the image before there is any visible image at all: the pipeline is shaping the latent first, then decoding the final result at the end.

**Text Encoder**

The text encoder is the component that turns your prompt into numerical representations the generator can actually use. Humans type words; the model consumes vectors. The text encoder is what bridges that gap.

It does not usually generate the image by itself. Instead, it converts the prompt into conditioning tensors or embeddings that guide the denoiser during sampling. That is why two models can both accept text prompts but respond very differently: their text encoders, tokenizer behavior, training data, and prompt conventions may differ.

In current image-generation pipelines:

- SD and SDXL families commonly use CLIP or OpenCLIP-style text encoders
- newer families such as FLUX, Qwen Image Edit, and WAN often use T5, UMT5, or larger encoder stacks

If a model family feels like it prefers natural-language instructions, sentence-style prompts, or stronger semantic understanding, that is often partly because its text-encoding stack differs from older CLIP-centered workflows.

**CLIP**

CLIP stands for Contrastive Language-Image Pre-training. It was designed to learn a shared representation between text and images, so that text descriptions and images that match end up close together in embedding space.

In practical image-generation usage, "CLIP" often refers to the prompt-understanding side of older Stable Diffusion pipelines. In other words, when people say a checkpoint "uses CLIP," they often mean the text encoder for that workflow comes from the CLIP/OpenCLIP family.

That matters because CLIP-era prompting tends to reward a certain style of prompt writing: weighted keywords, short descriptive fragments, tag-heavy phrasing, artist/style tokens, and carefully structured negatives. Many SDXL-derived ecosystems such as Pony, Illustrious, and NoobAI still inherit a lot of this prompt culture.

CLIP is also used more broadly outside the text encoder slot itself, including image-text matching, ranking, retrieval, and some conditioning features.

**CLIP Vision**

CLIP Vision is the image-encoder side of the CLIP family. Instead of reading text, it reads an image and converts that image into a feature representation the rest of the pipeline can compare against or condition on.

CLIP Vision is most often mentioned alongside tools like IP-Adapter. A reference image is run through CLIP Vision, useful visual features are extracted, and those features are then used to guide generation. Depending on the tool, that guidance may lean more toward style, composition, subject identity, or overall visual similarity.

If a workflow asks for a separate CLIP Vision model file, it usually means the feature extractor for reference-image conditioning is not bundled into the main checkpoint.

**T5 / T5-XXL / UMT5**

T5 and UMT5 are transformer-based text encoders from the broader language-model world. In image-generation pipelines, they are used as prompt encoders for newer architectures that want stronger language understanding than older CLIP-only setups typically provided.

The difference users notice is often prompt behavior. Models using T5- or UMT5-style encoders may respond better to plain-language instructions, longer semantic prompts, editing instructions, or more natural phrasing. That does not automatically make them "better" in every case, but it often makes them feel less tied to old keyword-stack prompting habits.

These encoders are also large. In many workflows they are distributed as separate files and can consume a meaningful amount of VRAM and RAM. That is why FLUX-family, Qwen Image Edit, and WAN workflows often involve more moving parts than a single older-style checkpoint file.

When you see model bundles that include a main transformer or denoiser plus one or more text encoders, this is usually what is going on: the pipeline has become more modular, and prompt understanding is being handled by larger dedicated language components.

## Conditioning and Guidance

**Conditioning**

Conditioning is any information the model uses to steer generation toward a desired result. Your text prompt is already a form of conditioning, but in practice the term is often used more broadly to mean all the extra signals layered on top of the prompt. That extra guidance can come from text embeddings, a pose skeleton, an edge map, a depth map, a reference image, a mask, or a lightweight adapter such as a LoRA.

Examples of conditioning include:

- prompt embeddings from the text encoder
- negative prompt embeddings
- ControlNet inputs such as canny, depth, or pose
- masks for inpainting
- reference-image features from IP-Adapter
- LoRAs or other adapters that alter the model's behavior

If you want a one-line summary, think of conditioning as "what information the model is being asked to obey."

**How conditioning changes the result**

Not all conditioning controls the same thing. Some methods mainly control structure, some mainly control style, some mainly inject learned concepts, and some blend several of those effects together.

That is why different conditioning types can cooperate or fight each other. Prompt strength, ControlNet strength, IP-Adapter scale, denoise strength, and LoRA weights all affect how much influence each signal gets. Understanding that balance is what helps users choose the right tool instead of stacking random add-ons and hoping for the best.

**ControlNet**

ControlNet is an add-on network that lets a diffusion model follow an external structural guide such as edges, depth, pose, lineart, segmentation, or similar control signals. It was designed so the original base model could stay mostly intact while a separate control branch learns how to inject that extra guidance.

ControlNet is what you reach for when you want the model to preserve layout or structure while still generating a new image. For example:

- use canny or lineart when you want the output to follow major outlines
- use depth when you want stronger scene geometry and spatial consistency
- use pose when you want a character to match a body position
- use segmentation or tile-based controls when you want region-level layout guidance

ControlNet is especially useful because it does not just "make the prompt stronger." It gives the model a separate structural signal to follow. That is why it can keep composition surprisingly stable even when the text prompt changes style, subject details, or rendering quality.

**Preprocessor**

A preprocessor is the tool that converts an input image into the control signal a ControlNet expects. The ControlNet usually does not want the original image directly. It wants a transformed representation that emphasizes a specific type of information.

Examples include:

- Canny: extracts strong edges
- depth: estimates scene depth or distance layers
- OpenPose: extracts body and limb skeletons
- lineart: simplifies the image into line structure
- normal maps or soft edge detectors: emphasize surface and contour information differently

This is why the same source image can produce very different results depending on the preprocessor. One preprocessor may preserve pose, another may preserve silhouette, and another may preserve spatial depth. In practice, many "ControlNet quality" problems are actually preprocessor choice problems.

**IP-Adapter**

IP-Adapter is a lightweight image-prompt adapter that uses features from a reference image to guide generation. Instead of only telling the model what you want with text, you also give it an image whose visual features can influence the output.

Technically, IP-Adapter works by extracting image features with an image encoder and injecting those features into added attention pathways, while leaving the original base model mostly frozen. From a user perspective, the important part is simpler: it lets you guide generation with image-based cues without replacing the whole checkpoint.

IP-Adapter is commonly used for:

- borrowing overall style or color feel from a reference image
- keeping composition or layout closer to a reference
- helping preserve character identity or facial cues with suitable variants
- combining text intent with image-driven visual guidance

It is not exactly the same as img2img. Img2img starts from the input image itself and denoises from it. IP-Adapter instead extracts guidance features from a reference image and uses them to influence a fresh generation. That difference is why IP-Adapter often feels more flexible for style and identity transfer.

**LoRA**

LoRA stands for Low-Rank Adaptation. It is a lightweight way of modifying a base model by adding a much smaller set of learned weights instead of retraining or replacing the whole model.

From a user's perspective, a LoRA is usually an add-on file that teaches the base model a concept, style, character, clothing pattern, pose bias, rendering look, or some other behavior. You load the base model, load the LoRA on top, and control its influence with a weight.

LoRAs are popular because they are small, easy to share, and stackable. They are often far smaller than full checkpoints, which makes experimentation much easier. They also preserve the base model's broad capabilities better than swapping to a totally different checkpoint for every idea.

A few rules of thumb:

- a low weight usually gives a lighter influence
- a high weight pushes the result harder toward the LoRA's learned behavior
- too many LoRAs, or badly matched ones, can fight each other and cause muddy or unstable outputs

LoRAs remain extremely common in SDXL ecosystems such as Pony, Illustrious, and NoobAI, and they are increasingly common in newer FLUX and Qwen-family workflows as well.

**LyCORIS**

LyCORIS is a family of LoRA-like adapter methods that use different internal math from basic LoRA, but serve a similar role for end users: they are lightweight add-ons that modify how a base model behaves.

From the user side, LyCORIS often feels almost the same as using a LoRA. You load an additional file, set a weight, and use it to bias the output toward a certain style, concept, character, or visual behavior. The main difference is under the hood, where different adapter variants may target the model in more flexible ways than standard LoRA.

In everyday community usage, many people talk about LyCORIS and LoRA almost interchangeably because the workflow is so similar. That is usually fine for practical docs, but technically LyCORIS is better understood as a broader family of adapter styles rather than literally the same method.

**Embedding / Textual Inversion**

An embedding, often called Textual Inversion in Stable Diffusion communities, is a learned prompt token rather than a full model add-on. It teaches the model that a special token or word should correspond to a certain concept, style, or negative concept.

The important difference from a LoRA is scope. A textual inversion embedding modifies prompt-space behavior by teaching the text encoder and model to associate a learned token with a concept. A LoRA usually changes the model more directly through added weights.

In typical use, an embedding often behaves like this:

- you load the embedding file
- you place its special token in the prompt
- the token activates the learned concept or style

Embeddings are usually much smaller than LoRAs. They were once a very common way to inject concepts, styles, and negative quality fixes into SD workflows. They still exist, especially in older ecosystems, but they are much less central today than LoRAs because LoRAs are usually more flexible and more powerful.

**Hypernetwork**

A hypernetwork is an older add-on model type that modifies activations during generation instead of replacing the whole checkpoint. It was an earlier way of steering a model toward a style or concept without doing a full new checkpoint training run.

From the user's point of view, hypernetworks filled a similar niche to LoRAs: small-ish add-ons that could shift the model's behavior. The reason you hear about them less now is that LoRAs and related adapter families largely became the preferred solution. They are usually easier to train, easier to distribute, and better supported by modern tools.

So if you see a guide mentioning hypernetworks, treat it as mostly historical or legacy terminology unless the workflow is specifically targeting an older ecosystem that still uses them.

## Image Editing Terms

**How edit workflows differ from pure text-to-image**

Text-to-image starts from noise and makes a new image from scratch. Edit workflows instead begin with an existing image, or an existing image plus a mask, and then change some or all of it.

The main distinction is scope:

- img2img changes the whole image, but tries to stay related to the starting image
- inpainting changes only selected areas
- outpainting extends beyond the original frame
- upscaling and refining are usually second-pass workflows focused on resolution or polish rather than composition

**Image to Image (img2img)**

Image to image, usually shortened to img2img, starts from an existing image instead of pure random noise. The input image is encoded into latent space, noise is added to it, and then the model denoises from that partially noised starting point while following the prompt.

The key result is that img2img tends to preserve some relationship to the source image. Depending on settings, that relationship may be loose or strong. Low denoise strength keeps more of the original composition, shapes, colors, and lighting. High denoise strength gives the model more freedom to reinterpret the image and can approach a near-regeneration.

This is why img2img is commonly used for:

- style transfer
- changing rendering style while keeping composition
- reworking anatomy or costume ideas without fully starting over
- polishing a rough image into something more coherent

If text-to-image is "generate from scratch," img2img is better thought of as "regenerate from a guided starting point."

**Inpainting**

Inpainting regenerates only a masked portion of an image. Instead of reworking the whole image, you mark a specific area and ask the model to fill or replace just that region.

This makes inpainting the precise edit tool in diffusion workflows. It is commonly used to:

- fix hands, faces, or eyes
- replace clothing, props, or background elements
- remove defects, artifacts, text, or watermarks
- add new objects into a scene without rebuilding the whole image

The masked area is where the model is allowed to invent new content. The surrounding unmasked area provides context, which helps the new content blend into the original scene. Good inpainting is often about controlling not just the prompt, but also the mask shape, the feathering of its edges, and the denoise strength.

**Outpainting**

Outpainting extends an image beyond its original borders. You enlarge the canvas, create empty or masked space around the existing image, and generate into that new area.

It is often used when you want to:

- widen a composition
- add headroom or side space to an image
- convert a portrait crop into a wider scene
- continue a background, landscape, or room beyond the original frame

Outpainting is basically a special case of inpainting where the masked region is outside the original content area. The challenge is not just inventing new content, but making it feel like a believable continuation of what was already there.

**Mask**

A mask is the region that tells the model where edits should happen. In most inpainting workflows, the masked area is the editable area and the unmasked area is meant to stay unchanged or mostly unchanged.

In common inpainting interfaces, this is usually presented as a white painted mask layer drawn over the image. You mark the area you want changed, and everything outside that painted region is treated as preserved context.

Some interfaces and workflows also let you import a separate black-and-white mask image and place it on top of the base image as the edit mask instead of painting it by hand.

Some interfaces also support multiple mask colors or extra region semantics, but the core idea stays the same: the mask defines the edit boundary.

Mask quality matters a lot. A hard mask edge can create obvious seams. A softer or slightly blurred edge often blends better. A mask that is too tight can starve the model of room to transition naturally, while a mask that is too large can cause the model to unnecessarily rewrite nearby areas.

**Hires Fix / High-Res Fix**

Hires Fix, or High-Res Fix, is a two-stage generation workflow designed to produce cleaner large images than a single high-resolution generation pass can often manage.

The usual pattern is:

1. Generate a smaller base image
2. Upscale that image
3. Run a second denoise pass to add or rebuild detail at the larger size

This matters because many models are more stable at moderate resolutions than at very large native resolutions. A direct high-resolution generation can be slower, heavier on VRAM, and sometimes structurally worse. Hires Fix gets around that by first solving composition at a smaller size and then improving detail in a second pass.

It is often used to reduce muddy detail, improve textures, and make large outputs feel more finished. But if the second denoise pass is too strong, it can also alter composition or introduce new mistakes.

**Refiner / Refining**

A refiner is a second model or second pass used after an initial image has already been generated. Its job is usually to improve detail, texture, edge quality, or overall finish rather than invent the whole composition from scratch.

In SDXL specifically, the refiner is a separate model intended for later denoising stages, where it can polish the output from the base model. In broader current usage, though, "refining" can mean any second-pass cleanup or enhancement workflow.

That can include:

- SDXL base -> SDXL refiner
- img2img cleanup passes
- targeted inpaint repairs
- upscale + denoise polish passes
- newer family-specific second-pass enhancement workflows

So when users say they are "refining" an image, they often mean they are no longer solving the big composition problem. They are trying to improve finish, clarity, and local detail.

**Upscale**

Upscaling means increasing image resolution. That can be done with a normal resize algorithm, but in image-generation communities it usually means using an AI upscaler or an upscale-plus-denoise workflow to add plausible new detail.

The important distinction is this:

- a basic resize makes the image bigger
- an AI upscale tries to make the image look more detailed as it gets bigger

Upscaling is useful when you want a larger final image for viewing, printing, or further editing. It is also often part of multi-stage workflows, where an image is generated at one size and then enlarged before another cleanup or refinement pass.

It is worth remembering that upscalers do not recover hidden real detail. They hallucinate plausible detail based on training and context. Sometimes that looks excellent; sometimes it invents textures or shapes you may not want.

## Model Add-Ons and Variants

**Lineage versus packaging**

This section is about lineage and packaging: what model you start from, how it was specialized, how it is distributed, and what larger ecosystem it belongs to.

Those are different questions:

- a base model is the original foundation other variants build on
- a fine-tune is a version trained further for a narrower purpose
- a merge is a blended checkpoint made from multiple models
- a quantized release is the same general model stored in a lower-precision format
- a model family is the broader ecosystem a release belongs to

Keeping those categories separate helps avoid common confusion, especially now that modern releases are often multi-file bundles rather than a single old-style checkpoint.

**Base Model**

A base model is the main underlying model that everything else builds on. It is the broad foundation before community specialization, custom style biasing, or downstream fine-tuning.

What matters in practice is that the base model usually determines the big compatibility rules:

- which LoRAs and adapters are likely to work well
- which prompt style tends to work best
- which ControlNets or secondary files are compatible
- what default strengths and resolutions are typical
- whether the workflow is oriented toward text-to-image, editing, or video

Current examples of important base-model ecosystems include:

- SDXL 1.0 as the major open 1024-native Stable Diffusion base family
- Anima as its own newer anime and illustration-focused base family
- FLUX.1 and FLUX.2 family releases for text-to-image and instruction-following image work
- Qwen Image Edit for instruction-driven image editing
- Z-Image Base and Z-Image Turbo for newer low-step image generation workflows
- WAN 2.1 and WAN 2.2 for open-weight video generation

**Fine-Tune**

A fine-tune is a model trained further from a base model so it becomes better at a narrower style, subject area, aesthetic, or use case. The base model gives it general capability; the fine-tune pushes it toward a specific behavior.

That specialization can target:

- a visual style or art direction
- a particular subject mix or character bias
- stronger realism or stronger illustration behavior
- better text rendering or editing behavior
- a narrower domain such as anime, fashion, portraits, or concept art

Most of the models people browse on sites like Hugging Face or CivitAI are not pure base models. They are fine-tunes, merges, or other derivatives built on top of a broader base family.

**Merge**

A merge is a model created by mathematically combining two or more checkpoints or fine-tunes. Instead of training from scratch, the creator blends multiple existing models to try to keep the strengths of each.

Merges are especially common in SDXL-derived communities because that ecosystem produced huge numbers of stylistically different checkpoints. A merge might try to combine, for example, one model's anatomy, another model's color handling, and another model's illustration style.

A merge can be very good, but it can also be less predictable than a cleaner base or fine-tune lineage. If a model feels powerful but a little "mystery meat" in behavior, it is often a heavily merged release.

**VAE-baked / AiO**

VAE-baked means the checkpoint already includes its VAE inside the model file, so you do not usually need to load a separate external VAE.

This term is most common in older Stable Diffusion checkpoint ecosystems, where releases could ship in several different ways. It also still comes up in SDXL discussions, where whether a checkpoint bakes in its VAE varies from release to release. Plenty of community SDXL checkpoints ship without a baked VAE, or bake in the notoriously broken fp16 VAE, so a matching external VAE was a near-mandatory download for much of the SDXL era. It is worth checking the model page rather than assuming. The common shipping options are:

- model only, requiring a matching external VAE
- model plus separate VAE
- model with the VAE already baked in

In newer DiT-based ecosystems, you may also see AiO, short for all-in-one. AiO usually means the full generation stack is packaged together as one coordinated model release, often including the transformer or denoiser, text encoders, and VAE in the same bundled file or tightly coupled package.

In many AiO releases, that really does mean a single bundled model file with the text encoder and or VAE included. The important nuance is that this is still not universal. Some modern DiT releases remain split into separate internal components, but are distributed and loaded as one complete package instead of expecting the user to assemble mismatched pieces manually.

Why it matters: if a model is VAE-baked, setup is simpler because you do not need to hunt for a matching external VAE. If a model is described as AiO, it usually means setup is simpler at a broader level because the main transformer, text encoders, and VAE are meant to be used together as one packaged release. That said, not all DiT models are AiO, and many modern ones remain modular by design for flexibility, swapping components, and memory management.

**Pruned Model**

A pruned model is a release where weights considered unnecessary for inference have been removed to reduce file size. The goal is usually to make the model smaller and easier to distribute without meaningfully harming inference quality.

For most end users, "pruned" usually means:

- smaller download size
- less storage use
- little or no meaningful difference for normal inference

It does not mean the model is fundamentally different in style or family. It usually means the same model has been packaged more efficiently for use rather than for continued training.

**Quantization / Quantized Models / Formats**

Quantization means storing model weights at lower precision so the model uses less VRAM and RAM. A quantized model is usually the same general model family, but represented in a more memory-efficient format.

Common quantized releases and formats include fp8 and int8 checkpoints, as well as packaged formats such as GGUF with variants like Q4, Q5, Q6, or Q8. These labels usually tell you both that the model has been compressed and, in many cases, roughly how aggressive that compression is.

What matters in practice is that quantization is both a precision choice and a release-format choice. Some quantized models are still distributed as ordinary checkpoint files in a lower precision such as fp8 or int8. Others are repackaged into formats such as GGUF that are designed around quantized inference workflows.

Quantized releases are especially relevant in newer heavy model ecosystems, where full-size versions may be too large for many local users. Often it is the reason a model becomes runnable at all on smaller GPUs.

**GGUF**

GGUF is a model file format commonly used for quantized transformer-style models. In image-generation contexts, it shows up most often with newer transformer-heavy families where full-size releases may be too heavy for many local systems.

The reason people care about GGUF is not the container format by itself. It is that GGUF releases are often paired with quantization levels that make otherwise large models more runnable on limited hardware, especially in workflows aimed at lower VRAM usage.

**Model Family / Base Family**

A model family, sometimes called a base family, is the broader ecosystem a model belongs to. This is often the most useful label for users because it tells you what kind of surrounding compatibility and prompt behavior to expect.

Family labels matter because LoRAs, VAEs, ControlNets, prompt conventions, tokenizer assumptions, and recommended settings are often family-specific. Two models may both generate images, but if they belong to different families they can behave very differently and may not share the same add-ons.

Common modern families and ecosystems include:

- **SDXL 1.0**: the major open Stable Diffusion XL base family, still foundational for a huge amount of community work
- **Pony**: a large SDXL-derived ecosystem known for stylized, character-heavy, and expressive prompt behavior
- **Illustrious / illustrative SDXL families**: SDXL derivatives centered on polished illustration and anime-adjacent output
- **NoobAI**: a newer, growing anime and illustration ecosystem derived from Illustrious. Many Illustrious LoRAs still work well with it, though the broader community content base is still larger around Illustrious. Workflows may use either v-prediction or EPS depending on the specific release and setup.
- **Anima**: a 2B anime and illustration-focused base model family made by CircleStone Labs in collaboration with Comfy Org, built for stylized character art, illustration-heavy workflows, and strong anime-oriented visual behavior
- **FLUX Kontext**: FLUX-family releases focused on instruction-following, contextual edits, and image-aware generation behavior
- **FLUX Klein**: smaller FLUX.2-oriented variants designed to be lighter and faster than the heavier full-dev style releases
- **Qwen Image Edit**: a modern instruction-led image-editing family with especially strong semantic editing and text editing behavior
- **Z-Image Base / Turbo**: newer image-generation families with a turbo-oriented low-step variant for speed-sensitive workflows
- **WAN 2.1 / WAN 2.2**: major modern open-weight video-generation families for text-to-video, image-to-video, and related tasks

When a guide says "use a LoRA for the same family" or "this workflow is family-specific," this is what it means: the surrounding tools and expectations are tied to the broader ecosystem, not just the single file you downloaded.

## Video Terms

**How video generation differs from single-image generation**

Image generation only has to make one frame look good. Video generation has to make many frames look good while also keeping them coherent across time.

That adds extra constraints:

- the model has to preserve subject identity, lighting, and scene structure across multiple frames
- motion has to feel believable instead of jittery or randomly changing
- longer clips cost more VRAM, memory bandwidth, time, and storage than a single still image

Because of that, video settings are not just about image quality. They also control duration, playback speed, and how stable the clip remains from frame to frame.

**Text to Video (T2V)**

Text to video means generating a video directly from a text prompt, without needing a starting still image. In other words, it is the video equivalent of text-to-image.

The model has to invent not just the subject and style, but also the sequence of frames over time. That makes T2V one of the harder tasks for generative models, because the system has to solve composition, appearance, and motion together.

In practice, T2V is commonly used for short cinematic clips, stylized motion shots, atmosphere tests, and concept-video generation. WAN 2.1 and WAN 2.2 are common examples in current open-weight local workflows.

**Image to Video (I2V)**

Image to video starts from a still image and animates it into a clip. Instead of inventing the whole scene from scratch, the model begins with an existing frame and predicts how that image should evolve over time.

This usually gives the user more control than pure text-to-video, because the first frame already locks in much of the composition, subject appearance, and visual style. The model is still generating new frames, but it is doing so from a stronger visual anchor.

I2V is often used for:

- animating illustrations or portraits
- adding camera motion to a still scene
- creating short loops or reaction shots from an existing image
- preserving a character or composition better than pure text-to-video often can

**Frame Count**

Frame count is the number of frames the model generates for the clip. More frames usually means a longer clip, but only when considered together with FPS.

The simple relationship is:

- clip length in seconds = frame count / FPS

So 48 frames at 24 FPS is about a 2-second clip, while 48 frames at 12 FPS is about a 4-second clip.

Higher frame counts usually require more compute, more VRAM or RAM pressure, more disk space, and more generation time. They can also make consistency problems more obvious, because the model has to keep the subject stable for longer.

**FPS**

FPS means frames per second in the saved output video. It controls playback speed, not the underlying visual content that was generated.

That distinction matters. If you keep the same frames but change the FPS, you are mostly changing how quickly those frames are shown, not asking the model to invent different motion.

So:

- higher FPS makes the clip play faster or look smoother if enough frames exist
- lower FPS makes the clip play slower or feel more choppy
- changing FPS after generation is often more like editing playback than changing the model's generation behavior

This is why frame count and FPS should be thought about together, not separately.

**Temporal Consistency**

Temporal consistency means how stable the video remains from one frame to the next. Good temporal consistency means a face stays the same person, clothing details stay recognizable, objects do not randomly change shape, and lighting does not flicker for no reason.

Poor temporal consistency is one of the main failure modes in generative video. It can show up as:

- flickering textures
- shape drift in hands, faces, or objects
- backgrounds changing between frames
- colors or lighting jumping around unnaturally

This is one of the hardest parts of video generation because the model is not only trying to make each frame look plausible by itself. It also has to make neighboring frames agree with each other. WAN 2.2 and other newer video families generally try to improve this compared with earlier open video releases.

**Keyframe / Start Frame / End Frame**

These are reference frames used to guide the video across time.

- a start frame anchors how the clip should begin
- an end frame anchors how the clip should finish
- a keyframe is a more general term for any frame used as a visual reference at a particular point in time

The idea is that the model is not generating every frame with equal freedom. It is being told that certain points in the clip should stay closer to specific reference images or target states.

This can be useful when you want to control transitions, preserve a character, move from one scene state to another, or create a more directed animation path instead of fully unconstrained motion.

## Performance and Precision Terms

**The hardware and runtime side**

This section is about the hardware and runtime side of generation: which backend is doing the work, what precision the model is stored or computed in, what memory-saving tricks are enabled, and why one setup may be faster or more compatible than another.

In practice, many generation problems that look like "the model is bad" are really performance-path problems instead. Wrong precision, unsupported attention kernels, weak backend support, insufficient VRAM, or aggressive offloading can all change speed, stability, or even whether a workflow runs at all.

**CUDA**

CUDA is NVIDIA's GPU compute platform and the main acceleration path used by most PyTorch-based image and video generation software on NVIDIA GPUs.

At its core, CUDA is what lets tensor operations run on an NVIDIA GPU instead of the CPU. It is also the ecosystem many surrounding optimizations are built around, including cuDNN, TensorRT, xFormers, Flash Attention, and a large amount of custom inference code. That is why NVIDIA workflows usually have the widest software support and the most mature optimized kernels.

You will often still see names like `torch.cuda`, `device="cuda"`, or `cuda:0` even in projects that also support AMD, Intel, or Apple hardware. That does not always mean the whole project is NVIDIA-only. It often means the codebase grew up in a CUDA-first ecosystem and kept CUDA-shaped API names as the common GPU interface.

**ROCm / HIP**

ROCm is AMD's GPU compute platform for AI and other accelerated workloads. In local generation workflows, it fills the same broad role on supported AMD hardware that CUDA fills on NVIDIA: it provides the runtime, compiler stack, libraries, and PyTorch integration needed to run models on the GPU.

HIP is the CUDA-like programming layer inside the ROCm ecosystem. Users mostly hear "ROCm" in setup guides, while developers often see HIP names in code and build tooling such as `hipblas`, `hiprand`, `hipcc`, or `hipify`.

The simple mental model is:

- ROCm = the full AMD compute platform
- HIP = the CUDA-like interface layer inside that platform

ROCm support can vary more by GPU generation, OS, wheel availability, and kernel support than CUDA support often does. But for supported Radeon and Instinct hardware, ROCm is the main native AMD path for local model inference.

**ZLUDA**

ZLUDA is a compatibility layer that lets some CUDA-targeted software run on non-NVIDIA hardware by translating enough of the CUDA-facing behavior for those applications to work.

You can think of it as taking software that expects CUDA-style code and CUDA API calls, then bridging or translating enough of that behavior into HIP and ROCm-compatible behavior for AMD hardware to execute it, using tooling provided by the HIP SDK such as `hipify`.

For local image generation, ZLUDA most often comes up as an alternative AMD path on Windows when native ROCm support is unavailable, incomplete, or simply not the preferred setup for a particular GPU or package. It is not the same thing as ROCm, and it should not be thought of as AMD's native compute stack.

Put simply:

- ROCm = AMD's native compute platform
- ZLUDA = a compatibility path for some CUDA-oriented software on other hardware

That distinction matters because ZLUDA compatibility is usually more package-specific and less universal than native CUDA or ROCm support. When a workflow relies on ZLUDA, support expectations, stability, and performance can differ significantly from the officially supported backend paths.

**IPEX**

IPEX means Intel Extension for PyTorch. It is Intel's optimized acceleration path for PyTorch workloads on Intel hardware, including Intel CPUs and, in some workflows, Intel Arc GPUs.

In image-generation communities, IPEX usually comes up when discussing Intel-native optimization, Intel Arc support, or performance improvements on Intel systems without going through NVIDIA CUDA or AMD ROCm. Like those other backend terms, it often appears in package names, install instructions, or troubleshooting guides as shorthand for "the Intel-optimized path."

**MPS**

MPS means the Apple Metal Performance Shaders backend as exposed through PyTorch on macOS. For local AI work, it is the Apple Silicon GPU acceleration path used on M-series Macs.

It allows model operations to run on the integrated Apple GPU instead of only on the CPU. That can make local inference much more usable on Mac hardware, but MPS is still its own backend with its own operator coverage, performance limits, and occasional compatibility gaps compared with CUDA.

**fp16 / bf16 / fp32**

These are floating-point precision formats used for model weights and inference math.

- fp32 is full 32-bit precision
- fp16 is 16-bit floating point
- bf16 is also 16-bit, but with a different bit layout designed to keep more exponent range

The practical tradeoff is simple: lower precision usually reduces VRAM usage and can increase speed, but it may also affect stability or compatibility depending on the hardware and model.

In many real workflows:

- fp32 is the heaviest and most conservative
- fp16 is very common for inference because it is much lighter than fp32
- bf16 is often preferred on hardware that supports it well because it can be more numerically stable than fp16 in some cases

**fp8**

fp8 is an 8-bit floating-point precision format used in some newer inference and quantized-model workflows. Compared with fp16 or bf16, it can reduce memory use further and sometimes improve throughput on hardware and software stacks that support it well.

In practice, fp8 usually matters most for newer transformer-heavy models where full-size weights are expensive to run. The tradeoff is the same general one as other lower-precision formats: lower VRAM use and potentially better speed, but also a higher chance of quality loss, unsupported code paths, or hardware-specific limitations.

**int8**

int8 is an 8-bit integer precision format used in quantized inference workflows. Unlike fp8, which is still a floating-point format, int8 stores values as integers and usually relies on extra scaling logic during inference.

For most users, int8 mostly means a more aggressively compressed model that can fit on weaker hardware than its fp16, bf16, or fp32 equivalent. The tradeoff is that int8 models are more dependent on runtime support, and depending on the implementation they may lose more quality or flexibility than lighter quantization approaches.

**xFormers**

xFormers is a library that provides optimized attention implementations and related memory-saving kernels. In many generation workflows, enabling xFormers can reduce VRAM use and sometimes improve speed.

Users usually encounter it as a toggle, install dependency, or troubleshooting detail. If a guide says "enable xFormers," it generally means the workflow can use a more memory-efficient attention path than the plain baseline implementation.

**Flash Attention**

Flash Attention is a highly optimized attention implementation designed to reduce memory traffic and make attention layers faster and more memory efficient.

This matters because attention is one of the more expensive parts of modern image and video models, especially in larger transformer-led architectures. Better attention kernels can noticeably improve performance or make a workflow fit into available memory when it otherwise would not.

Flash Attention is strongly associated with NVIDIA CUDA workflows, but supported ROCm paths also exist through AMD-backed kernel implementations and integrations. The important user-facing point is not the exact kernel internals. It is that Flash Attention is one of the main "fast path" optimizations users may see mentioned in setup guides for heavy models.

**Sage Attention**

Sage Attention is a newer family of attention kernels focused on inference acceleration through lower-precision attention math. It is mainly discussed in newer DiT and transformer-heavy workloads.

Compared with Flash Attention, Sage Attention is not just the same thing under a different name. It is a separate optimization family with different kernels and support expectations. In practice, it is usually mentioned when people are trying to push faster inference on newer NVIDIA GPUs for large transformer-based models.

**Offloading**

Offloading means moving part of a model out of VRAM and into system RAM when that part is not actively being used. The goal is to make a workflow fit on hardware that does not have enough GPU memory to keep the entire model resident at once.

The tradeoff is almost always speed. Offloading saves VRAM, but it usually makes generation slower because data has to move back and forth between GPU memory and system memory.

This is why offloading can be the difference between "runs" and "does not run," but it is rarely the fastest option.

**Tiled VAE Encode / Decode**

Tiled VAE encode/decode means running the VAE in smaller image chunks instead of processing the whole image at once. This is mainly a VRAM-management technique used when encoding an image into latent space or decoding a latent back into pixels would otherwise exceed available memory.

By breaking the image into tiles, the VAE only has to process one region at a time, which makes larger images possible on weaker hardware. The tradeoff is that tiled VAE workflows can sometimes introduce seams, slight inconsistency between regions, or slower total processing time if the implementation is not good.

Tiled VAE encode/decode is often the difference between successfully handling a large image and hitting an out-of-memory error during latent conversion.

**OOM / Out of Memory**

OOM means out of memory. It is the error you get when the GPU VRAM, system RAM, or sometimes both cannot hold the tensors needed for the current step.

In generation workflows, OOM errors usually show up because of one or more of these factors:

- resolution is too high
- batch size is too large
- the model or text encoder is too large for available memory
- attention or VAE operations spike memory usage
- too many model components are loaded at once

When users talk about "fitting a model," they are usually talking about avoiding OOM.

**Warmup / First-run Compile**

Warmup, sometimes called first-run compile or first-run initialization, is the extra setup cost many workflows pay on the first generation after launch.

The first run may be slower because kernels are being selected or compiled, memory pools are being initialized, graphs are being built, caches are being filled, or model components are being loaded into their working state.

That is why the first generation after starting a backend is often noticeably slower than the second or third. It does not always mean something is wrong; often the runtime is simply paying its one-time setup cost.
