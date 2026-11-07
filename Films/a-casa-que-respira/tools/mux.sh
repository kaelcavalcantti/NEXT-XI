#!/usr/bin/env bash
# Junta os quadros (24 fps) e o áudio mixado em um MP4 H.264/AAC 1920x1080.
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p output
OUT=output/A_Casa_que_Respira_Parte1.mp4
ffmpeg -y -loglevel warning -stats -framerate 24 -i build/frames/f%05d.jpg -i build/audio/mix.wav \
  -map 0:v -map 1:a -vf hqdn3d=1.5:1.5:4:4 -c:v libx264 -preset slow -crf ${CRF:-25} -tune film -pix_fmt yuv420p -r 24 \
  -c:a aac -b:a 256k -ar 48000 -shortest -movflags +faststart \
  -metadata title="A Casa que Respira — Parte 1: A Herança" -metadata language=por "$OUT"
ffprobe -v error -show_entries format=duration,size:stream=codec_name,width,height,r_frame_rate,sample_rate -of compact "$OUT"
echo "vídeo: $OUT"
