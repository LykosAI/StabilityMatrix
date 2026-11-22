#!/usr/bin/env bash
# Inicia o Motion Studio e abre no navegador. O ComfyUI precisa estar rodando (porta 8188).
cd "$(dirname "$0")" || exit 1
if ! curl -s -o /dev/null --max-time 3 "${COMFY_URL:-http://127.0.0.1:8188}/queue"; then
  echo "ComfyUI não está respondendo em ${COMFY_URL:-http://127.0.0.1:8188}. Inicie o ComfyUI primeiro." >&2
  exit 1
fi
(sleep 1; xdg-open "http://127.0.0.1:${PORT:-8765}" >/dev/null 2>&1) &
exec python3 server.py
