param(
    [string]$ApiUrl = $env:API_URL,
    [string]$ApiKey = $env:API_KEY,
    [string]$VisionModel = $env:VISION_MODEL,
    [string]$ImageUrl = $env:IMAGE_URL,
    [string]$Prompt = $env:PROMPT
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ApiUrl)) { $ApiUrl = "http://127.0.0.1:5000/v1/chat/completions" }
if ([string]::IsNullOrWhiteSpace($ApiKey)) { $ApiKey = "your_api_key" }
if ([string]::IsNullOrWhiteSpace($VisionModel)) { $VisionModel = "gpt-4-turbo" }
if ([string]::IsNullOrWhiteSpace($ImageUrl)) { $ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/d/dd/Gfp-wisconsin-madison-the-nature-boardwalk.jpg/2560px-Gfp-wisconsin-madison-the-nature-boardwalk.jpg" }
if ([string]::IsNullOrWhiteSpace($Prompt)) { $Prompt = "What's in this image?" }

$headers = @{
    "Content-Type"  = "application/json"
    "Authorization" = "Bearer $ApiKey"
}

$bodyObject = @{
    model      = $VisionModel
    messages   = @(
        @{
            role    = "user"
            content = @(
                @{
                    type = "text"
                    text = $Prompt
                },
                @{
                    type      = "image_url"
                    image_url = @{
                        url = $ImageUrl
                    }
                }
            )
        }
    )
    max_tokens = 300
}

$body = $bodyObject | ConvertTo-Json -Depth 20
$response = Invoke-RestMethod -Method Post -Uri $ApiUrl -Headers $headers -Body $body
$response | ConvertTo-Json -Depth 100

