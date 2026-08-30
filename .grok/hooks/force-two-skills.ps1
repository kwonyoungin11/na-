# Forces superpowers (TDD) at production edits and unlazy at completion.
# Command path is cwd-relative. Do not use GROK_WORKSPACE_ROOT in the JSON command.
# ASCII-only: Windows PowerShell 5 misparses UTF-8 regex without a BOM.
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false

$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

try {
    $ev = $raw | ConvertFrom-Json
} catch {
    exit 0
}

$eventName = $env:GROK_HOOK_EVENT
if (-not $eventName -and $ev.hookEventName) { $eventName = [string]$ev.hookEventName }
$eventName = ([string]$eventName).ToLowerInvariant().Replace('-', '_')

$tddSkill = '.grok/skills/test-driven-development/SKILL.md'
$spSkill = '.grok/skills/superpowers/SKILL.md'
$unlazySkill = '.grok/skills/unlazy/SKILL.md'
$tddRepo = '.grok/skills/cpp-tdd-cmake/SKILL.md'

$forceCtx = @"
FORCED SKILLS - usage points:
- Implementing production .cpp/.h/.cs: read $spSkill then $tddSkill then $tddRepo.
  Iron law: no production code without a failing nara_*_tests.exe first.
- Claiming substantial work done: read $unlazySkill. Write GATES.md from .grok/skills/unlazy/templates/gates-leaf.md. Report only commands actually run (pass/fail).
MCP is enabled. Use search_tool then use_tool.
"@

function Emit-Json([hashtable]$obj) {
    $json = $obj | ConvertTo-Json -Compress -Depth 8
    [Console]::Out.Write($json)
}

function Emit-Context([string]$hookEvent, [string]$text) {
    Emit-Json @{
        additionalContext = $text
        hookSpecificOutput = @{
            hookEventName = $hookEvent
            additionalContext = $text
        }
    }
}

$sessionId = 'anon'
if ($ev.sessionId) { $sessionId = [string]$ev.sessionId }
$promptId = 'none'
if ($ev.promptId) { $promptId = [string]$ev.promptId }

$stateDir = Join-Path $env:TEMP 'nara-force-skills'
New-Item -ItemType Directory -Force -Path $stateDir | Out-Null
$statePath = Join-Path $stateDir ($sessionId + '.json')

function Read-State {
    $h = @{ edited = @{}; blocks = @{} }
    if (-not (Test-Path $statePath)) { return $h }
    try {
        $j = Get-Content -Raw -Encoding utf8 $statePath | ConvertFrom-Json
        if ($j.edited) {
            $j.edited.PSObject.Properties | ForEach-Object { $h.edited[$_.Name] = [bool]$_.Value }
        }
        if ($j.blocks) {
            $j.blocks.PSObject.Properties | ForEach-Object { $h.blocks[$_.Name] = [int]$_.Value }
        }
    } catch { }
    return $h
}

function Write-State($h) {
    ($h | ConvertTo-Json -Compress -Depth 6) | Set-Content -Encoding utf8 $statePath
}

function Get-EditPath($inputObj) {
    if (-not $inputObj) { return '' }
    foreach ($name in @('target_file', 'file_path', 'path', 'targetFile', 'filePath')) {
        $p = $inputObj.PSObject.Properties[$name]
        if ($p -and $p.Value) { return [string]$p.Value }
    }
    return ''
}

function Test-ProductionPath([string]$path) {
    if ([string]::IsNullOrWhiteSpace($path)) { return $false }
    return $path -match '\.(cpp|h|hpp|cc|cxx|cs)$'
}

switch -Regex ($eventName) {
    '^session_start$' {
        Emit-Context 'SessionStart' $forceCtx
        exit 0
    }
    '^pre_tool_use$' {
        $editPath = Get-EditPath $ev.toolInput
        if (Test-ProductionPath $editPath) {
            $state = Read-State
            $state.edited[$promptId] = $true
            Write-State $state
            Emit-Context 'PreToolUse' $forceCtx
        } else {
            Emit-Json @{ decision = 'allow' }
        }
        exit 0
    }
    '^stop$' {
        $reason = [string]$ev.reason
        if ($reason -and $reason -ne 'end_turn') { exit 0 }

        $active = $false
        if ($ev.PSObject.Properties['stopHookActive'] -and $ev.stopHookActive) {
            $active = [bool]$ev.stopHookActive
        }

        $state = Read-State
        $edited = $false
        if ($state.edited.ContainsKey($promptId)) { $edited = [bool]$state.edited[$promptId] }
        if (-not $edited) { exit 0 }

        $blockCount = 0
        if ($state.blocks.ContainsKey($promptId)) { $blockCount = [int]$state.blocks[$promptId] }
        if ($active -and $blockCount -ge 2) { exit 0 }

        $last = ''
        if ($ev.lastAssistantMessage) { $last = [string]$ev.lastAssistantMessage }

        $isQuestion = $last -match '\?[ \t]*$'
        if ($isQuestion) { exit 0 }

        $doneKo = ([string][char]0xC644) + ([char]0xB8CC)
        $fixKo = ([string][char]0xACE0) + ([char]0xCCD0)
        $passKo = ([string][char]0xD1B5) + ([char]0xACFC)
        $looksDone = ($last -match '(?i)(fixed|done\.|all tests pass|verification run|complete[d]?)') -or
            $last.Contains($doneKo) -or $last.Contains($fixKo) -or $last.Contains($passKo)
        $hasEvidence = $last -match '(?i)(nara_[a-z0-9_]*_tests|FAIL:|GATES\.md|cmake --build)'
        $needTdd = $looksDone -and -not $hasEvidence

        $gatesUnmet = $false
        $cwd = (Get-Location).Path
        if ($ev.workspaceRoot) { $cwd = [string]$ev.workspaceRoot }
        elseif ($ev.cwd) { $cwd = [string]$ev.cwd }
        $gatesPath = Join-Path $cwd 'GATES.md'
        $checker = Join-Path $cwd '.grok\skills\unlazy\scripts\gate-check.mjs'
        if ((Test-Path $gatesPath) -and (Test-Path $checker) -and $looksDone) {
            $statusOut = & node $checker --status $gatesPath 2>&1 | Out-String
            if ($statusOut -notmatch 'ALL MET') { $gatesUnmet = $true }
        }

        if (-not $needTdd -and -not $gatesUnmet) { exit 0 }

        $state.blocks[$promptId] = $blockCount + 1
        Write-State $state

        $reasonText = @"
Stop blocked: load forced skills before claiming done.
1) Superpowers TDD - read $spSkill, $tddSkill, $tddRepo.
   Watch a nara_*_tests.exe FAIL, then write production, then run the same exe PASS.
2) Unlazy - read $unlazySkill.
   Substantial work needs GATES.md (CHECK/EXPECT). Report only commands you ran, with pass/fail.
"@
        Emit-Json @{
            decision = 'block'
            reason = $reasonText
        }
        exit 0
    }
    default { exit 0 }
}
