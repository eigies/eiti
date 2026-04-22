[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$BotToken,
    [Parameter(Mandatory)]
    [string]$ChatId,
    [Parameter(Mandatory)]
    [string]$Message
)

$ErrorActionPreference = 'Stop'

$uri = "https://api.telegram.org/bot$BotToken/sendMessage"
$body = @{
    chat_id = $ChatId
    text = $Message
    disable_web_page_preview = 'true'
}

$response = Invoke-RestMethod -Method Post -Uri $uri -Body $body
if (-not $response.ok) {
    throw 'Telegram API did not confirm message delivery.'
}

Write-Host '[OK] Telegram message sent.'
