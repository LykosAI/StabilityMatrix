"""Exercise the exact embedded launch wrapper against Wan2GP's pinned Gradio.

Run from the repository root (no GPU or Wan2GP installation needed):
    uv run --no-project --python 3.10 --with gradio==5.29.0 --with requests python Tools/tests/test_wan2gp_logging.py
"""

import contextlib
import inspect
import io
import logging
import os
from pathlib import Path
import textwrap
import types
import unittest
from unittest.mock import patch

os.environ["GRADIO_ANALYTICS_ENABLED"] = "False"

import gradio as gr
from fastapi import FastAPI
from fastapi.responses import JSONResponse
from fastapi.testclient import TestClient
from gradio.exceptions import Error as GradioError


class Wan2GPLoggingTests(unittest.TestCase):
    def setUp(self):
        source = (
            Path(__file__).resolve().parents[2]
            / "StabilityMatrix.Core/Models/Packages/Wan2GP.cs"
        ).read_text(encoding="utf-8-sig")
        script = textwrap.dedent(
            source.split('private const string GradioLogPatchScript = """', 1)[1]
            .split('""";', 1)[0]
        )
        namespace = {"__name__": "sm_logging_test"}
        exec(compile(script, "_sm_gradio_log_patch.py", "exec"), namespace)

        self.original_signature = inspect.signature(GradioError)
        self.default_error = GradioError()
        self.addCleanup(setattr, GradioError, "__init__", GradioError.__init__)
        for name in ("Error", "Info", "Warning"):
            self.addCleanup(setattr, gr, name, getattr(gr, name))
        root = logging.getLogger()
        self.addCleanup(setattr, root, "handlers", root.handlers[:])
        self.addCleanup(root.setLevel, root.level)
        self.stdout = io.StringIO()
        self.stderr = io.StringIO()
        output_context = contextlib.ExitStack()
        self.addCleanup(output_context.close)
        output_context.enter_context(contextlib.redirect_stdout(self.stdout))
        output_context.enter_context(contextlib.redirect_stderr(self.stderr))

        # Transformers isn't needed to reproduce the Gradio/FastAPI integration.
        transformers = types.ModuleType("transformers")
        utils = types.ModuleType("transformers.utils")
        tf_logging = types.ModuleType("transformers.utils.logging")
        tf_logging.set_verbosity = lambda level: None
        transformers.utils = utils
        utils.logging = tf_logging
        with patch.dict(
            "sys.modules",
            {
                "transformers": transformers,
                "transformers.utils": utils,
                "transformers.utils.logging": tf_logging,
            },
        ):
            namespace["_apply_logging_patch"]()

    def test_error_keeps_class_identity_and_signature(self):
        self.assertIs(gr.Error, GradioError)
        self.assertTrue(issubclass(gr.Error, Exception))
        self.assertEqual(inspect.signature(gr.Error), self.original_signature)

    def test_deepy_mounted_app_builds_and_handles_errors(self):
        # Deepy imports this symbol after the launch wrapper has patched Gradio.
        from gradio import Error as DeepyError

        deepy = FastAPI()

        async def invalid_value(request, error):
            return JSONResponse({"detail": error.message}, status_code=400)

        deepy.add_exception_handler(DeepyError, invalid_value)

        @deepy.get("/state")
        def state():
            return {"connected": True}

        @deepy.get("/error")
        def error():
            # Also test an alias imported before the wrapper ran.
            raise GradioError("Invalid Deepy request")

        app = FastAPI()
        app.mount("/deepy", deepy)
        with TestClient(app) as client:
            response = client.get("/deepy/state")
            self.assertEqual(response.status_code, 200)
            self.assertEqual(response.json(), {"connected": True})
            response = client.get("/deepy/error")
            self.assertEqual(response.status_code, 400)
            self.assertEqual(response.json(), {"detail": "Invalid Deepy request"})
        self.assertEqual(
            self.stderr.getvalue().count("[Gradio] ERROR: Invalid Deepy request"), 1
        )

    def test_error_preserves_arguments_and_logs_to_stderr(self):
        error = gr.Error("Bad input", 3, False, "Custom title", False)
        self.assertIs(type(error), GradioError)
        self.assertEqual(error.message, "Bad input")
        self.assertEqual(error.duration, 3)
        self.assertFalse(error.visible)
        self.assertEqual(error.title, "Custom title")
        self.assertFalse(error.print_exception)
        self.assertIn("[Gradio] ERROR: Bad input", self.stderr.getvalue())
        self.assertNotIn("[Gradio] ERROR:", self.stdout.getvalue())

    def test_error_accepts_defaults_and_keywords(self):
        default = gr.Error()
        self.assertEqual(vars(default), vars(self.default_error))
        error = gr.Error(message="Keyword error", duration=None, title="Notice")
        self.assertEqual(error.message, "Keyword error")
        self.assertIsNone(error.duration)
        self.assertEqual(error.title, "Notice")

    def test_error_can_be_caught_and_subclassed(self):
        class SpecificError(gr.Error):
            pass

        with self.assertRaises(gr.Error) as caught:
            raise SpecificError("Subclass error")
        self.assertIsInstance(caught.exception, GradioError)
        self.assertIn("[Gradio] ERROR: Subclass error", self.stderr.getvalue())

    def test_info_and_warning_still_log_to_stdout(self):
        gr.Info("Progress message")
        # Outside a Gradio callback this also emits a normal Python warning.
        with self.assertWarns(UserWarning):
            gr.Warning("Warning message")
        self.assertIn("[Gradio] Progress message", self.stdout.getvalue())
        self.assertIn("[Gradio] WARNING: Warning message", self.stdout.getvalue())


if __name__ == "__main__":
    unittest.main(verbosity=2)
