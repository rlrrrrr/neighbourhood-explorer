#!/bin/sh
set -eu

quant="${QUANT:-Q4_K_M}"
model_path="${CLEF_MODEL_PATH:-/models/Clef-Flash-${quant}.gguf}"

if [ ! -f "$model_path" ]; then
  model_url="${CLEF_MODEL_URL:-https://huggingface.co/ggml-org/Clef-Flash-GGUF/resolve/main/Clef-Flash-${quant}.gguf}"
  echo "CLEF: téléchargement de ${model_url} vers ${model_path}..."
  curl -fL --retry 3 --retry-delay 5 -o "${model_path}.part" "${model_url}"
  mv "${model_path}.part" "${model_path}"
fi

exec llama-server --model "$model_path" --host 0.0.0.0 --port 8080 "$@"
