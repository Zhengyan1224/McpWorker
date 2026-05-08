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
    throw "Image not found: $ImagePath`nUsage: .\multimodal_local_image_stream.ps1 -ImagePath .\demo.jpg"
}
if ([string]::IsNullOrWhiteSpace($ApiUrl)) { $ApiUrl = "http://127.0.0.1:5000/v1/chat/completions" }
if ([string]::IsNullOrWhiteSpace($ApiKey)) { $ApiKey = "your_api_key" }
if ([string]::IsNullOrWhiteSpace($VisionModel)) { $VisionModel = "gpt-4-turbo" }
if ([string]::IsNullOrWhiteSpace($Prompt)) { $Prompt = "Please stream a description of this image and provide 3 key points." }

if (-not (Get-Command curl.exe -ErrorAction SilentlyContinue)) {
    throw "curl.exe not found. Please install curl or use PowerShell 7 with curl available."
}

$mimeType = Get-MimeType -Path $ImagePath
$imageBase64 = [System.Convert]::ToBase64String([System.IO.File]::ReadAllBytes($ImagePath))
$dataUrl = "data:$mimeType;base64,$imageBase64"

$bodyObject = @{
    model      = $VisionModel
    stream     = $true
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
$tmpJson = [System.IO.Path]::GetTempFileName()

try {
    [System.IO.File]::WriteAllText($tmpJson, $body, [System.Text.UTF8Encoding]::new($false))

    & curl.exe -N $ApiUrl `
        -H "Content-Type: application/json" `
        -H "Authorization: Bearer $ApiKey" `
        --data-binary "@$tmpJson"
}
finally {
    if (Test-Path -LiteralPath $tmpJson) {
        Remove-Item -LiteralPath $tmpJson -Force -ErrorAction SilentlyContinue
    }
}
