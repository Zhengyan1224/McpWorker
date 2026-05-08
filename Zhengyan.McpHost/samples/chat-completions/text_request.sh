#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://127.0.0.1:5000/v1/chat/completions}"
API_KEY="${API_KEY:-your_api_key}"
MODEL="${MODEL:-No models available}"

curl -sS "$API_URL" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer ${API_KEY}" \
  -d "$(cat <<JSON
{
  "model": "${MODEL}",
  "messages": [
    {
      "role": "system",
      "content": "You are a helpful assistant."
    },
    {
      "role": "user",
      "content": "Hello!"
    }
  ],
  "max_tokens": 300
}
JSON
)"

