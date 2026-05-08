#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://127.0.0.1:5000/v1/chat/completions}"
API_KEY="${API_KEY:-your_api_key}"
VISION_MODEL="${VISION_MODEL:-gpt-4-turbo}"
IMAGE_URL="${IMAGE_URL:-https://upload.wikimedia.org/wikipedia/commons/thumb/d/dd/Gfp-wisconsin-madison-the-nature-boardwalk.jpg/2560px-Gfp-wisconsin-madison-the-nature-boardwalk.jpg}"
PROMPT="${PROMPT:-What's in this image?}"

curl -sS "$API_URL" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer ${API_KEY}" \
  -d "$(cat <<JSON
{
  "model": "${VISION_MODEL}",
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
            "url": "${IMAGE_URL}"
          }
        }
      ]
    }
  ],
  "max_tokens": 300
}
JSON
)"

