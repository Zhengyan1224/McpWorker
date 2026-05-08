#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://127.0.0.1:5000/v1/chat/completions}"
API_KEY="${API_KEY:-your_api_key}"
VISION_MODEL="${VISION_MODEL:-gpt-4-turbo}"
PROMPT="${PROMPT:-请流式描述这张图片，并给出3条关键信息。}"
IMAGE_PATH="${1:-${IMAGE_PATH:-./demo.jpg}}"

if [[ ! -f "${IMAGE_PATH}" ]]; then
  echo "Image not found: ${IMAGE_PATH}" >&2
  echo "Usage: $0 /path/to/image.jpg" >&2
  exit 1
fi

if ! command -v file >/dev/null 2>&1; then
  echo "Required command not found: file" >&2
  exit 1
fi

if ! command -v base64 >/dev/null 2>&1; then
  echo "Required command not found: base64" >&2
  exit 1
fi

MIME_TYPE="$(file --mime-type -b "${IMAGE_PATH}")"
if base64 --help 2>&1 | grep -q -- "-w"; then
  IMAGE_B64="$(base64 -w 0 "${IMAGE_PATH}")"
else
  IMAGE_B64="$(base64 "${IMAGE_PATH}" | tr -d '\n')"
fi

DATA_URL="data:${MIME_TYPE};base64,${IMAGE_B64}"

curl -N "$API_URL" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer ${API_KEY}" \
  -d "$(cat <<JSON
{
  "model": "${VISION_MODEL}",
  "stream": true,
  "messages": [
    {
      "role": "user",
      "content": [
        {
          "type": "text",
          "text": "${PROMPT}"
        },
        {
          "type": "image_url",
          "image_url": {
            "url": "${DATA_URL}"
          }
        }
      ]
    }
  ],
  "max_tokens": 300
}
JSON
)"

