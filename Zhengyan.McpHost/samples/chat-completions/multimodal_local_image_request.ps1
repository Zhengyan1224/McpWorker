param(
    [string]$ImagePath = $env:IMAGE_PATH,
    [string]$ApiUrl = $env:API_URL,
    [string]$ApiKey = $env:API_KEY,
    [string]$VisionModel = $env:VISION_MODEL,
    [string]$Prompt = $env:PROMPT
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-MimeType {
    param([string]$Path)
    switch ([System.IO.Path]::GetExtension($Path).ToLowerInvariant()) {
        ".jpg" { return "image/jpeg" }
        ".jpeg" { return "image/jpeg" }
        ".png" { return "image/png" }
        ".gif" { return "image/gif" }
        ".webp" { return "image/webp" }
        ".bmp" { return "image/bmp" }
        ".svg" { return "image/svg+xml" }
        default { return "application/octet-stream" }
    }
}

if ([string]::IsNullOrWhiteSpace($ImagePath)) { $ImagePath = ".\demo.jpg" }
if (-not (Test-Path -LiteralPath $ImagePath -PathType Leaf)) {
    throw "Image not found: $ImagePath`nUsage: .\multimodal_local_image_request.ps1 -ImagePath .\demo.jpg"
}
if ([string]::IsNullOrWhiteSpace($ApiUrl)) { $ApiUrl = "http://127.0.0.1:5000/v1/chat/completions" }
if ([string]::IsNullOrWhiteSpace($ApiKey)) { $ApiKey = "your_api_key" }
if ([string]::IsNullOrWhiteSpace($VisionModel)) { $VisionModel = "gpt-4-turbo" }
if ([string]::IsNullOrWhiteSpace($Prompt)) { $Prompt = "What is in this image? Please describe briefly." }

$mimeType = Get-MimeType -Path $ImagePath
$imageBase64 = [System.Convert]::ToBase64String([System.IO.File]::ReadAllBytes($ImagePath))
$dataUrl = "data:$mimeType;base64,$imageBase64"

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
                        url = $dataUrl
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
