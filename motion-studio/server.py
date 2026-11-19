#!/usr/bin/env python3
"""Motion Studio: servidor local mínimo.

Serve a página (index.html) e repassa /c/* para o ComfyUI, evitando CORS.
Uso: python3 server.py   (abre em http://127.0.0.1:8765)
Variáveis: COMFY_URL (padrão http://127.0.0.1:8188), PORT (padrão 8765)
"""
import os
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

HERE = Path(__file__).resolve().parent
COMFY = os.environ.get("COMFY_URL", "http://127.0.0.1:8188").rstrip("/")
PORT = int(os.environ.get("PORT", "8765"))

FILES = {
    "/": ("index.html", "text/html; charset=utf-8"),
    "/workflow": ("workflow.json", "application/json"),
}
PASS_HEADERS = ("Content-Type", "Content-Length", "Content-Range", "Accept-Ranges")


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def log_message(self, *args):  # silencia o log de cada requisição
        pass

    def _proxy(self):
        url = COMFY + self.path[2:]  # remove o prefixo "/c"
        length = int(self.headers.get("Content-Length") or 0)
        body = self.rfile.read(length) if length else None
        headers = {k: v for k, v in self.headers.items() if k.lower() in ("content-type", "range")}
        req = urllib.request.Request(url, data=body, headers=headers, method=self.command)
        try:
            resp = urllib.request.urlopen(req, timeout=600)
        except urllib.error.HTTPError as e:
            resp = e
        except Exception as e:  # ComfyUI fora do ar
            msg = f"ComfyUI inacessível em {COMFY}: {e}".encode()
            self.send_response(502)
            self.send_header("Content-Type", "text/plain; charset=utf-8")
            self.send_header("Content-Length", str(len(msg)))
            self.end_headers()
            self.wfile.write(msg)
            return
        data = resp.read()
        self.send_response(resp.status if hasattr(resp, "status") else resp.code)
        for h in PASS_HEADERS:
            if resp.headers.get(h) and h != "Content-Length":
                self.send_header(h, resp.headers[h])
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        path = self.path.split("?")[0]
        if path in FILES:
            name, ctype = FILES[path]
            data = (HERE / name).read_bytes()
            self.send_response(200)
            self.send_header("Content-Type", ctype)
            self.send_header("Content-Length", str(len(data)))
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(data)
        elif self.path.startswith("/c/"):
            self._proxy()
        else:
            self.send_error(404)

    def do_POST(self):
        if self.path.startswith("/c/"):
            self._proxy()
        else:
            self.send_error(404)


if __name__ == "__main__":
    srv = ThreadingHTTPServer(("127.0.0.1", PORT), Handler)
    print(f"Motion Studio em http://127.0.0.1:{PORT}  (ComfyUI: {COMFY})")
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        pass
