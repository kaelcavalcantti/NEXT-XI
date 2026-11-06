#!/usr/bin/env bash
# Produção completa de "A Casa que Respira — Parte 1".
# Requisitos: Node 18+, Python 3.10+, ffmpeg (com libx264 e rubberband), Chromium do Playwright.
set -euo pipefail
cd "$(dirname "$0")"
echo "== 1/7 dependências JS (three.js, fontes)"; npm install --silent
echo "== 2/7 dependências Python"; pip install -q -r requirements.txt
echo "== 3/7 modelo de voz (Kokoro PT-BR)"; bash tools/fetch_models.sh
echo "== 4/7 linha do tempo"; node tools/export_timeline.mjs
echo "== 5/7 vozes (TTS neural) e sincronia labial"; python3 audio/voices.py
echo "== 6/7 sonoplastia, trilha e mixagem"; python3 audio/mix.py
echo "== 7/7 animação (quadros 1920x1080) e MP4"
node tools/render.mjs --workers "${WORKERS:-2}"
bash tools/mux.sh
