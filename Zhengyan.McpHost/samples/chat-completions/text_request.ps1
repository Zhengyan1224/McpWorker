
param(
    [string]$ApiUrl = $env:API_URL,
    [string]$ApiKey = $env:API_KEY,
    [string]$Model = $env:MODEL
)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ApiUrl)) { $ApiUrl = "http://127.0.0.1:5000/v1/chat/completions" }
if ([string]::IsNullOrWhiteSpace($ApiKey)) { $ApiKey = "your_api_key" }
if ([string]::IsNullOrWhiteSpace($Model)) { $Model = "No models available" }

$headers = @{
    "Content-Type"  = "application/json"
    "Authorization" = "Bearer $ApiKey"
}

$bodyObject = @{
    model      = $Model
    messages   = @(
        @{
            role    = "system"
            content = "You are a helpful assistant."
        },
        @{
            role    = "user"
            content = "Hello!"
        }
    )
    max_tokens = 300
}

$body = $bodyObject | ConvertTo-Json -Depth 10
$response = Invoke-RestMethod -Method Post -Uri $ApiUrl -Headers $headers -Body $body
$response | ConvertTo-Json -Depth 100

