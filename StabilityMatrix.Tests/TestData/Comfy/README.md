# Recorded ComfyUI traffic

`Fakes/FakeComfyServer` serves these recordings, so tests run `ComfyClient` and
`InferenceClientManager` against what a real ComfyUI sends.

Each folder is one ComfyUI version:

- `recording.json`: the `/object_info/{node}` responses for every node `InferenceClientManager`
  queries, three prompts with their `/prompt` responses and `/history`, and every websocket
  message in order.
- `view.png`: what `/view` returned for the success prompt's first image.

The three prompts need no model weights:

| Name | Graph | Result |
| --- | --- | --- |
| `success` | 4 × `EmptyImage` → `ColorTransfer` → `SaveImage` | Progress 1/4 and 4/4, 4 saved images |
| `runtime_error` | `CheckpointLoaderSimple` on an empty file | `execution_error` from `CheckpointLoaderSimple` |
| `validation_error` | `LoadImage` of a missing file | HTTP 400, `prompt_outputs_failed_validation` |

## Recording a new version

1. Install ComfyUI in a scratch folder (CPU-only torch is enough).
2. Add empty placeholder model files so the model lists aren't empty:
   `checkpoints/sd_xl_base_1.0.safetensors`, `checkpoints/broken_checkpoint.safetensors` (the
   runtime error needs it), `loras/detail_tweaker_xl.safetensors`, `loras/style/ink_sketch.safetensors`,
   `diffusion_models/flux1-dev.safetensors`, `text_encoders/t5xxl_fp16.safetensors`,
   `text_encoders/clip_l.safetensors`, `vae/sdxl_vae.safetensors`,
   `controlnet/control_canny_sdxl.safetensors`, `upscale_models/4x-UltraSharp.pth` and
   `clip_vision/clip_vision_h.safetensors`.
3. Start it: `python main.py --cpu --listen 127.0.0.1 --port 18188`.
4. From ComfyUI's venv: `python -I record.py http://127.0.0.1:18188 <version>`.

`record.py` replaces the install path in tracebacks with `ComfyUI\` or `venv\`. Nothing else is
edited. `0.38.0` was recorded on Windows, so the LoRA in a subfolder is listed as
`style\ink_sketch.safetensors`.
