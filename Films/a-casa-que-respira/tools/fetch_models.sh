#!/usr/bin/env bash
# Baixa o modelo de voz neural Kokoro-82M (Apache-2.0) e as vozes PT-BR a partir do registro npm
# (pacotes que redistribuem os pesos) e monta models/kokoro-fp32.onnx + models/voices-ptbr.bin.
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p models build/npm
cd build/npm
for p in kokoro-fp32a-shards@1.0.0 kokoro-fp32b-shards@1.0.0 kokoro-fp32c-shards@1.0.0 kokoro-js@1.2.1; do
  d="${p%@*}"; [ -d "$d" ] && continue
  mkdir -p "$d" && (cd "$d" && npm pack "$p" --silent >/dev/null && tar xzf ./*.tgz)
done
cd ../..
if [ ! -f models/kokoro-fp32.onnx ]; then
  : > models/kokoro-fp32.onnx
  for i in $(seq 0 18); do cat build/npm/kokoro-fp32*-shards/package/kokoro-fp32.part$i.bin >> models/kokoro-fp32.onnx; done
fi
python3 - <<'PY'
import numpy as np
d = {n: np.fromfile(f'build/npm/kokoro-js/package/voices/{n}.bin', dtype=np.float32).reshape(-1, 1, 256) for n in ['pf_dora', 'pm_alex', 'pm_santa']}
with open('models/voices-ptbr.bin', 'wb') as f: np.savez(f, **d)
print('vozes PT-BR:', list(d))
PY
ls -la models
