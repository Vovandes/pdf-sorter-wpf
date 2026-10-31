<#
.SYNOPSIS
    Прогоняет демо-сценарий на наборе samples/ и сохраняет настоящие скриншоты окна в docs/.

.DESCRIPTION
    1. Собирает решение (Release) и заново генерирует samples/.
    2. Снимает главное окно, предпросмотр и результат выполнения (перемещение в sorted/).
    3. Копирует журнал выполненного пакета в docs/journal_example.json.
    4. Проверяет пробный прогон (файлы не меняются) и отмену последней операции (всё возвращается на место).
    Журналы и настройки пишутся в .local/demo, а не в %LOCALAPPDATA%.

.EXAMPLE
    .\tools\make-screenshots.ps1
#>
[CmdletBinding()]
param(
    [int]$Width = 1440,
    [int]$Height = 1000,
    [double]$Scale = 1.25
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$samples = Join-Path $root 'samples'
$sorted = Join-Path $root 'sorted'
$dataDir = Join-Path $root '.local\demo'
$docs = Join-Path $root 'docs'
$exe = Join-Path $root 'src\PdfSorter.App\bin\Release\net8.0-windows\PdfSorter.exe'

function Invoke-App([string]$Stage, [string]$Screenshot, [string]$Mode = 'move', [int]$SelectRule = 1) {
    $arguments = @(
        '--data-dir', "`"$dataDir`"", '--source', "`"$samples`"", '--target', "`"$sorted`"",
        '--rules', "`"$samples\rules.json`"", '--subfolders', '--mode', $Mode,
        '--stage', $Stage, '--select-rule', $SelectRule,
        '--width', $Width, '--height', $Height, '--scale', $Scale.ToString([Globalization.CultureInfo]::InvariantCulture),
        '--screenshot', "`"$Screenshot`"")
    $stderr = Join-Path $dataDir 'stderr.txt'
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru -RedirectStandardError $stderr
    if (-not $process.WaitForExit(120000)) {
        Stop-Process -Id $process.Id -Force
        throw "Приложение не завершилось за 2 минуты (этап $Stage)."
    }
    if ($process.ExitCode -ne 0) {
        throw "Этап $Stage завершился с кодом $($process.ExitCode): $(Get-Content $stderr -Raw)"
    }
    Write-Host "  [$Stage] -> $Screenshot"
}

function Get-TreeSnapshot([string]$Path) {
    if (-not (Test-Path $Path)) { return '' }
    (Get-ChildItem $Path -Recurse -File | Sort-Object FullName |
        ForEach-Object { '{0}|{1}|{2}' -f $_.FullName.Substring($Path.Length), $_.Length, $_.LastWriteTimeUtc.Ticks }) -join "`n"
}

function Get-LatestJournal([switch]$DryRun) {
    Get-ChildItem (Join-Path $dataDir 'logs') -Filter 'batch_*.json' |
        Where-Object { $_.Name.EndsWith('-dryrun.json') -eq [bool]$DryRun } |
        Sort-Object LastWriteTime | Select-Object -Last 1
}

Write-Host '1. Сборка и демо-набор'
dotnet build PdfSorter.sln -c Release --nologo -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Сборка не удалась.' }
dotnet run --project tools/PdfSorter.SampleGenerator -c Release --no-build -- $samples | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Генерация samples не удалась.' }
foreach ($path in $sorted, $dataDir) {
    if (Test-Path $path) { Remove-Item $path -Recurse -Force }
}
New-Item -ItemType Directory -Force $dataDir, $docs | Out-Null
$original = Get-TreeSnapshot $samples

Write-Host '2. Скриншоты'
Invoke-App -Stage main -SelectRule 4 -Screenshot (Join-Path $docs 'screenshot_main.png')
Invoke-App -Stage preview -SelectRule 4 -Screenshot (Join-Path $docs 'screenshot_preview.png')

Invoke-App -Stage dryrun -Screenshot (Join-Path $dataDir 'dryrun.png')
if ((Get-TreeSnapshot $samples) -ne $original -or (Test-Path $sorted)) { throw 'Пробный прогон изменил файлы!' }
Write-Host '  пробный прогон: файлы не изменены, журнал' (Get-LatestJournal -DryRun).Name

Invoke-App -Stage run -Screenshot (Join-Path $docs 'screenshot_done.png')
$journal = Get-LatestJournal
Copy-Item $journal.FullName (Join-Path $docs 'journal_example.json') -Force
$batch = Get-Content $journal.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
Write-Host ('  выполнение: перемещено {0}, ошибок {1}, файлов в sorted: {2}' -f
    $batch.summary.done, $batch.summary.failed, (Get-ChildItem $sorted -Recurse -File).Count)

Write-Host '3. Отмена последней операции'
Invoke-App -Stage undo -Screenshot (Join-Path $dataDir 'undo.png')
if ((Get-TreeSnapshot $samples) -ne $original) { throw 'После отмены набор samples не совпадает с исходным!' }
if (Test-Path $sorted) { throw 'После отмены осталась папка sorted.' }
Write-Host '  отмена: все файлы возвращены, созданные папки удалены'

Write-Host "Готово. Скриншоты и пример журнала — в $docs"
