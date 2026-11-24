"""Records real ComfyUI HTTP and websocket traffic for StabilityMatrix's fake ComfyUI server tests.

Usage: python -I record.py http://127.0.0.1:18188 <out_dir>
See README.md next to this script for the server setup.
"""

import asyncio
import base64
import json
import re
import sys
import time
from pathlib import Path

import aiohttp

# Nodes InferenceClientManager.LoadSharedPropertiesAsync queries, plus the graphs' own nodes.
QUERIED_NODES = [
    "CheckpointLoaderSimple",
    "ControlNetLoader",
    "LoraLoader",
    "UltralyticsDetectorProvider",
    "SAMLoader",
    "KSampler",
    "LatentUpscale",
    "UpscaleModelLoader",
    "Inference_Core_AIO_Preprocessor",
    "UNETLoader",
    "UnetLoaderGGUF",
    "DualCLIPLoader",
    "DualCLIPLoaderGGUF",
    "CLIPVisionLoader",
    "EmptyImage",
    "ColorTransfer",
    "SaveImage",
    "LoadImage",
]

CLIENT_ID = "recording-client"


def success_graph():
    return {
        "1": {"class_type": "EmptyImage", "inputs": {"width": 64, "height": 64, "batch_size": 4, "color": 0}},
        "2": {"class_type": "EmptyImage", "inputs": {"width": 64, "height": 64, "batch_size": 1, "color": 16744448}},
        "3": {
            "class_type": "ColorTransfer",
            "inputs": {
                "image_target": ["1", 0],
                "image_ref": ["2", 0],
                "method": "reinhard_lab",
                "source_stats": "per_frame",
                "strength": 1.0,
            },
        },
        "4": {"class_type": "SaveImage", "inputs": {"images": ["3", 0], "filename_prefix": "Inference/recording"}},
    }


def runtime_error_graph():
    return {
        "1": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": "broken_checkpoint.safetensors"}},
        "2": {"class_type": "EmptyImage", "inputs": {"width": 8, "height": 8, "batch_size": 1, "color": 0}},
        "3": {"class_type": "SaveImage", "inputs": {"images": ["2", 0], "filename_prefix": "Inference/err"}},
        # Forces the checkpoint loader to execute
        "4": {"class_type": "VAEDecode", "inputs": {"samples": ["5", 0], "vae": ["1", 2]}},
        "5": {"class_type": "EmptyLatentImage", "inputs": {"width": 64, "height": 64, "batch_size": 1}},
        "6": {"class_type": "SaveImage", "inputs": {"images": ["4", 0], "filename_prefix": "Inference/err2"}},
    }


def validation_error_graph():
    return {
        "1": {"class_type": "LoadImage", "inputs": {"image": "does-not-exist.png"}},
        "2": {"class_type": "SaveImage", "inputs": {"images": ["1", 0], "filename_prefix": "Inference/val"}},
    }


async def run(base: str, out: Path):
    out.mkdir(parents=True, exist_ok=True)
    messages: list[dict] = []
    t0 = time.monotonic()

    async with aiohttp.ClientSession() as http:
        stats = await (await http.get(f"{base}/system_stats")).json()

        object_info = {}
        for node in QUERIED_NODES:
            async with http.get(f"{base}/object_info/{node}") as r:
                object_info[node] = {"status": r.status, "body": await r.json()}

        ws = await http.ws_connect(f"{base.replace('http', 'ws')}/ws?clientId={CLIENT_ID}")

        async def reader():
            async for msg in ws:
                at = round(time.monotonic() - t0, 3)
                if msg.type == aiohttp.WSMsgType.TEXT:
                    messages.append({"at": at, "text": json.loads(msg.data)})
                elif msg.type == aiohttp.WSMsgType.BINARY:
                    messages.append({"at": at, "binary": base64.b64encode(msg.data).decode()})

        reader_task = asyncio.create_task(reader())

        async def queue(name, graph):
            async with http.post(f"{base}/prompt", json={"prompt": graph, "client_id": CLIENT_ID}) as r:
                body = await r.json()
                return {"name": name, "request": graph, "status": r.status, "response": body}

        def done(prompt_id):
            return any(
                m.get("text", {}).get("type") in ("execution_success", "execution_error")
                and m["text"]["data"].get("prompt_id") == prompt_id
                for m in messages
            )

        prompts = []
        for name, graph in [("success", success_graph()), ("runtime_error", runtime_error_graph())]:
            entry = await queue(name, graph)
            prompts.append(entry)
            prompt_id = entry["response"].get("prompt_id")
            for _ in range(600):
                if prompt_id and done(prompt_id):
                    break
                await asyncio.sleep(0.1)
            await asyncio.sleep(0.5)
            if prompt_id:
                async with http.get(f"{base}/history/{prompt_id}") as r:
                    entry["history"] = await r.json()

        prompts.append(await queue("validation_error", validation_error_graph()))

        success = prompts[0]
        outputs = success["history"][success["response"]["prompt_id"]]["outputs"]
        image = outputs["4"]["images"][0]
        async with http.get(
            f"{base}/view",
            params={"filename": image["filename"], "subfolder": image["subfolder"], "type": image["type"]},
        ) as r:
            (out / "view.png").write_bytes(await r.read())

        await ws.close()
        await reader_task

    recording = {
        "comfyui_version": stats["system"]["comfyui_version"],
        "client_id": CLIENT_ID,
        "object_info": object_info,
        "prompts": prompts,
        "websocket": messages,
    }
    text = json.dumps(recording, indent=2) + "\n"
    # Tracebacks carry the server's install path; keep only the part from ComfyUI/ or the venv down.
    text = re.sub(r'(?:[A-Za-z]:)?(?:\\\\|/)[^"]*?(?:\\\\|/)(ComfyUI|venv)(\\\\|/)', r"\1\2", text)
    (out / "recording.json").write_text(text, encoding="utf-8")
    print(f"recorded {len(messages)} websocket messages, ComfyUI {recording['comfyui_version']}")
    for p in prompts:
        print(p["name"], p["status"], json.dumps(p["response"])[:200])


if __name__ == "__main__":
    asyncio.run(run(sys.argv[1].rstrip("/"), Path(sys.argv[2])))
