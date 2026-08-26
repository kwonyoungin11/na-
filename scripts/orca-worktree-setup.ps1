# Orca worktree setup for 동국문화재연구원 낙찰분석프로그램.
# Runs from the NEW worktree root after git worktree add.
# Copies API keys, junctions live nara-workspace data, cmake-builds C++ ingest.
# Does not run the Python collector. Does not publish/launch the C# WPF app.

$ErrorActionPreference = "Stop"

function Write-Step([string]$msg) { Write-Host "==> $msg" }

function Get-SamePath([string]$a, [string]$b) {
    if ([string]::IsNullOrWhiteSpace($a) -or [string]::IsNullOrWhiteSpace($b)) { return $false }
    try {
        return ((Resolve-Path -LiteralPath $a).Path.TrimEnd('\') -ieq (Resolve-Path -LiteralPath $b).Path.TrimEnd('\'))
    } catch {
        return ($a.TrimEnd('\') -ieq $b.TrimEnd('\'))
    }
}

$Root = $env:ORCA_WORKTREE_PATH
if ([string]::IsNullOrWhiteSpace($Root)) { $Root = (Get-Location).Path }
$Root = $Root.Trim().Trim('"')
Set-Location -LiteralPath $Root

$Primary = $env:ORCA_ROOT_PATH
if ([string]::IsNullOrWhiteSpace($Primary)) { $Primary = "A:\c" }
$Primary = $Primary.Trim().Trim('"')

Write-Step "nara award-analysis Orca worktree setup"
Write-Host "NARA_WORKTREE=$Root"
Write-Host "NARA_PRIMARY=$Primary"
if (-not [string]::IsNullOrWhiteSpace($env:ORCA_WORKSPACE_NAME)) {
    Write-Host "ORCA_WORKSPACE_NAME=$($env:ORCA_WORKSPACE_NAME)"
}

$cmakeList = Join-Path $Root "cc-feature-implementer\desktop\nara_collect_cpp\CMakeLists.txt"
$pathsPy = Join-Path $Root "nara_workspace_paths.py"
if (-not (Test-Path -LiteralPath $cmakeList)) {
    throw "Not the nara award-analysis tree (nara_collect_cpp/CMakeLists.txt missing)."
}
if (-not (Test-Path -LiteralPath $pathsPy)) {
    throw "Not the nara award-analysis tree (nara_workspace_paths.py missing)."
}

function Copy-Secret([string]$rel) {
    $src = Join-Path $Primary $rel
    $dst = Join-Path $Root $rel
    if (-not (Test-Path -LiteralPath $src)) {
        Write-Host "SKIP secret missing on primary: $rel"
        return
    }
    $dstDir = Split-Path -Parent $dst
    if (-not (Test-Path -LiteralPath $dstDir)) {
        New-Item -ItemType Directory -Path $dstDir -Force | Out-Null
    }
    if (Test-Path -LiteralPath $dst) {
        Write-Host "secret already present: $rel"
        return
    }
    Copy-Item -LiteralPath $src -Destination $dst -Force
    Write-Host "copied secret: $rel"
}

Write-Step "copy gitignored API keys"
Copy-Secret ".env"
Copy-Secret ".agents\skills\API\.api_key.env"
Copy-Secret "nara-workspace\.env"

function Ensure-Share([string]$rel) {
    $src = Join-Path $Primary $rel
    $dst = Join-Path $Root $rel
    if (-not (Test-Path -LiteralPath $src)) {
        Write-Host "SKIP share missing on primary: $rel"
        return
    }
    if (Get-SamePath $Root $Primary) {
        Write-Host "primary checkout — leave $rel in place"
        return
    }
    if (Test-Path -LiteralPath $dst) {
        Write-Host "already present: $rel"
        return
    }
    $parent = Split-Path -Parent $dst
    if (-not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    New-Item -ItemType Junction -Path $dst -Target $src | Out-Null
    Write-Host "junction $rel -> $src"
}

Write-Step "share live nara-workspace data from primary"
@(
    "nara-workspace\master",
    "nara-workspace\app",
    "nara-workspace\output",
    "nara-workspace\first-stage",
    "nara-workspace\source-data",
    "nara-workspace\analysis",
    "nara-workspace\reports",
    "nara-workspace\logs"
) | ForEach-Object { Ensure-Share $_ }

$cmakeBin = @(
    "C:\Program Files\CMake\bin",
    "C:\CMake\bin",
    (Join-Path ${env:ProgramFiles} "CMake\bin")
) | Where-Object { Test-Path -LiteralPath (Join-Path $_ "cmake.exe") } | Select-Object -First 1
if ($cmakeBin) { $env:PATH = "$cmakeBin;" + $env:PATH }
if (-not (Get-Command cmake -ErrorAction SilentlyContinue)) {
    throw "CMake not on PATH. Install to C:\Program Files\CMake\bin."
}

$cppSrc = Join-Path $Root "cc-feature-implementer\desktop\nara_collect_cpp"
$cppBuild = Join-Path $cppSrc "build"
$cache = Join-Path $cppBuild "CMakeCache.txt"
Write-Step "C++ nara_igunsul_ingest cmake"
if (-not (Test-Path -LiteralPath $cache)) {
    cmake -S $cppSrc -B $cppBuild -G "Visual Studio 17 2022" -A x64
    if ($LASTEXITCODE -ne 0) { throw "cmake configure failed: $LASTEXITCODE" }
} else {
    Write-Host "cmake cache present — skip configure"
}
cmake --build $cppBuild --config Release --target nara_igunsul_ingest
if ($LASTEXITCODE -ne 0) { throw "cmake build failed: $LASTEXITCODE" }

$exe = Join-Path $cppBuild "Release\nara_igunsul_ingest.exe"
$fixtures = Join-Path $Root "cc-feature-implementer\desktop\NaraCollector.Desktop.Tests\Fixtures\igunsul"
if ((Test-Path -LiteralPath $exe) -and (Test-Path -LiteralPath $fixtures)) {
    Write-Step "smoke C++ igunsul ingest fixtures"
    $out = Join-Path $env:TEMP "nara-igunsul-setup.jsonl"
    & $exe --dir $fixtures --out $out
    if ($LASTEXITCODE -ne 0) { throw "nara_igunsul_ingest smoke failed: $LASTEXITCODE" }
    $lines = @(Get-Content -LiteralPath $out -ErrorAction SilentlyContinue)
    Write-Host ("fixture jsonl rows=" + $lines.Count)
} else {
    Write-Host "SKIP ingest smoke (exe or fixtures missing)"
}

Write-Host "invariants: award-only master; regions 전국/대구/경북 separate; C++ collector replacing C# and Python"
Write-Host "NARA_WORKTREE_SETUP_OK"
exit 0
