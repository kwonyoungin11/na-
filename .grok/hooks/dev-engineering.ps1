param()
$ErrorActionPreference = 'Continue'
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[Console]::InputEncoding = $utf8NoBom
[Console]::OutputEncoding = $utf8NoBom
$OutputEncoding = $utf8NoBom
$raw = [Console]::In.ReadToEnd()
$eventName = $env:GROK_HOOK_EVENT
if ([string]::IsNullOrWhiteSpace($eventName)) { $eventName = 'UserPromptSubmit' }
try {
    $parsed = $raw | ConvertFrom-Json
    if ($parsed.hookEventName) { $eventName = [string]$parsed.hookEventName }
} catch { }

$stopLike = @(
    'stop', 'Stop',
    'subagent_stop', 'SubagentStop', 'SubagentEnd',
    'stop_failure', 'StopFailure',
    'stop_cancelled', 'StopCancelled'
)
if ($stopLike -contains $eventName) {
    [Console]::Out.WriteLine('{"decision":"allow"}')
    exit 0
}

$rulePath = Join-Path $PSScriptRoot '..\rules\00-dev-engineering.md'
$ctx = $null
if (Test-Path -LiteralPath $rulePath) {
    $ctx = Get-Content -LiteralPath $rulePath -Raw -Encoding UTF8
}
if ([string]::IsNullOrWhiteSpace($ctx)) {
    $ctx = '개발 게이트 파일을 읽지 못함. .grok/rules/00-dev-engineering.md 를 읽고 그 턴을 끝낸다. 매 턴 15 서브에이전트/MCP 의식을 하지 않는다.'
}

$payload = [ordered]@{
    decision = 'allow'
    systemMessage = $ctx
    additionalContext = $ctx
    additional_context = $ctx
    hookSpecificOutput = [ordered]@{
        hookEventName = $eventName
        additionalContext = $ctx
        additional_context = $ctx
    }
}
$json = $payload | ConvertTo-Json -Compress -Depth 8
[Console]::Out.WriteLine($json)
exit 0
