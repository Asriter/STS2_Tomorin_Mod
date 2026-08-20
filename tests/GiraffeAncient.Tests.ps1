$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot

function Get-RepositoryPath([string]$path) {
    return Join-Path $root $path
}

function Assert-FileExists([string]$path) {
    if (-not (Test-Path -LiteralPath (Get-RepositoryPath $path))) {
        throw "缺少文件：$path"
    }
}

function Assert-Contains([string]$content, [string]$pattern, [string]$message) {
    if ($content -notmatch $pattern) {
        throw $message
    }
}

function Get-GiraffeSource {
    $sources = Get-ChildItem -LiteralPath (Get-RepositoryPath "Scripts/Relics/Giraffe") -Filter "*.cs" -File -Recurse |
        ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw -Encoding utf8 }
    return $sources -join [Environment]::NewLine
}

function Assert-LocalizationKeys([string]$path, [string[]]$keys) {
    $json = Get-Content -LiteralPath (Get-RepositoryPath $path) -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
    foreach ($key in $keys) {
        if (-not $json.ContainsKey($key)) {
            throw "$path 缺少本地化键：$key"
        }
    }
}

$eventPath = "Scripts/Events/GiraffeAncient.cs"
$baseRelicPath = "Scripts/Relics/Giraffe/GiraffeStageDeviceRelic.cs"
$ancientLocalizationPaths = @(
    "STS2_Tomorin_Mod/localization/eng/ancients.json",
    "STS2_Tomorin_Mod/localization/zhs/ancients.json")
$relicLocalizationPaths = @(
    "STS2_Tomorin_Mod/localization/eng/relics.json",
    "STS2_Tomorin_Mod/localization/zhs/relics.json")
$eventLocalizationPaths = @(
    "STS2_Tomorin_Mod/localization/eng/events.json",
    "STS2_Tomorin_Mod/localization/zhs/events.json")

Assert-FileExists $eventPath
Assert-FileExists $baseRelicPath
foreach ($path in $ancientLocalizationPaths + $relicLocalizationPaths + $eventLocalizationPaths) {
    Assert-FileExists $path
}

$eventSource = Get-Content -LiteralPath (Get-RepositoryPath $eventPath) -Raw -Encoding utf8
$giraffeSource = Get-GiraffeSource
$baseRelicSource = Get-Content -LiteralPath (Get-RepositoryPath $baseRelicPath) -Raw -Encoding utf8

Assert-Contains $eventSource "class\s+GiraffeAncient\s*:\s*CustomAncientModel" "长颈鹿事件必须继承 CustomAncientModel。"
Assert-Contains $eventSource "GenerateInitialOptions" "长颈鹿事件必须生成初始选项。"
Assert-Contains $eventSource "RelicCanSpawnAtCustomAncient" "长颈鹿事件必须过滤当前不可出现的遗物选项。"
Assert-Contains $eventSource "RelicOption" "长颈鹿事件选项必须通过遗物选项直接结算。"
Assert-Contains $eventSource "IsValidForAct" "长颈鹿事件必须声明章节可用性接口。"
Assert-Contains $eventSource "act\s+is\s+Glory" "长颈鹿事件必须限制在现有荣耀章节。"
Assert-Contains $eventSource "IsAllowed" "长颈鹿事件必须声明队伍可用性接口。"
Assert-Contains $eventSource "player\.Character\s+is\s+Tomorin" "长颈鹿事件必须只允许全 Tomorin 队伍进入。"
Assert-Contains $baseRelicSource "class\s+GiraffeStageDeviceRelic\s*:\s*BaseRelicModel" "舞台装置必须继承统一的基础遗物类。"
Assert-Contains $baseRelicSource "RelicRarity\.Event" "舞台装置必须归类为事件遗物。"

$tiers = [ordered]@{
    "高风险档" = @("BurningStageDevice", "MassacreStageDevice", "HuntingStageDevice", "FinaleStageDevice")
    "中风险档" = @("ReproductionStageDevice", "DesireStageDevice", "CompetitionStageDevice")
    "低风险档" = @("FarewellStageDevice", "PrideStageDevice", "InterludeStageDevice", "StarPickingStageDevice")
}

foreach ($tier in $tiers.GetEnumerator()) {
    foreach ($device in $tier.Value) {
        Assert-Contains $eventSource $device "该档位必须包含 $device。"
        Assert-Contains $giraffeSource $device "必须存在舞台装置类 $device。"
    }
}

Assert-Contains $giraffeSource "\[Pool\(typeof\(EventRelicPool\)\)\]" "舞台装置必须注册到 EventRelicPool。"

Assert-Contains $giraffeSource "class\s+BurningStageDevice[\s\S]*?AfterObtained" "燃烧的舞台装置必须实现取得时接口。"
Assert-Contains $giraffeSource "class\s+BurningStageDevice[\s\S]*?AfterCompose" "燃烧的舞台装置必须响应作词接口。"
Assert-Contains $giraffeSource "class\s+MassacreStageDevice[\s\S]*?TryModifyRewards" "皆杀的舞台装置必须响应奖励修改接口。"
Assert-Contains $giraffeSource "class\s+HuntingStageDevice[\s\S]*?AfterCardExhausted" "狩猎的舞台装置必须响应消耗接口。"
Assert-Contains $giraffeSource "class\s+FinaleStageDevice[\s\S]*?TryModifyEnergyCostInCombat" "终幕的舞台装置必须响应战斗费用接口。"
Assert-Contains $giraffeSource "class\s+FinaleStageDevice[\s\S]*?AfterSideTurnEnd" "终幕的舞台装置必须响应回合结束接口。"

$ancientKeys = @(
    "STS2_TOMORIN_MOD-GIRAFFE_ANCIENT.title",
    "STS2_TOMORIN_MOD-GIRAFFE_ANCIENT.epithet",
    "STS2_TOMORIN_MOD-GIRAFFE_ANCIENT.talk.firstVisitEver.0-0.ancient",
    "STS2_TOMORIN_MOD-GIRAFFE_ANCIENT.talk.ANY.0-0r.ancient")
$relicKeys = @(
    "STS2_TOMORIN_MOD-BURNING_STAGE_DEVICE.title", "STS2_TOMORIN_MOD-BURNING_STAGE_DEVICE.description", "STS2_TOMORIN_MOD-BURNING_STAGE_DEVICE.flavor",
    "STS2_TOMORIN_MOD-MASSACRE_STAGE_DEVICE.title", "STS2_TOMORIN_MOD-MASSACRE_STAGE_DEVICE.description", "STS2_TOMORIN_MOD-MASSACRE_STAGE_DEVICE.flavor",
    "STS2_TOMORIN_MOD-HUNTING_STAGE_DEVICE.title", "STS2_TOMORIN_MOD-HUNTING_STAGE_DEVICE.description", "STS2_TOMORIN_MOD-HUNTING_STAGE_DEVICE.flavor",
    "STS2_TOMORIN_MOD-FINALE_STAGE_DEVICE.title", "STS2_TOMORIN_MOD-FINALE_STAGE_DEVICE.description", "STS2_TOMORIN_MOD-FINALE_STAGE_DEVICE.flavor",
    "STS2_TOMORIN_MOD-REPRODUCTION_STAGE_DEVICE.title", "STS2_TOMORIN_MOD-REPRODUCTION_STAGE_DEVICE.description", "STS2_TOMORIN_MOD-REPRODUCTION_STAGE_DEVICE.flavor",
    "STS2_TOMORIN_MOD-DESIRE_STAGE_DEVICE.title", "STS2_TOMORIN_MOD-DESIRE_STAGE_DEVICE.description", "STS2_TOMORIN_MOD-DESIRE_STAGE_DEVICE.flavor",
    "STS2_TOMORIN_MOD-COMPETITION_STAGE_DEVICE.title", "STS2_TOMORIN_MOD-COMPETITION_STAGE_DEVICE.description", "STS2_TOMORIN_MOD-COMPETITION_STAGE_DEVICE.flavor",
    "STS2_TOMORIN_MOD-FAREWELL_STAGE_DEVICE.title", "STS2_TOMORIN_MOD-FAREWELL_STAGE_DEVICE.description", "STS2_TOMORIN_MOD-FAREWELL_STAGE_DEVICE.flavor",
    "STS2_TOMORIN_MOD-PRIDE_STAGE_DEVICE.title", "STS2_TOMORIN_MOD-PRIDE_STAGE_DEVICE.description", "STS2_TOMORIN_MOD-PRIDE_STAGE_DEVICE.flavor",
    "STS2_TOMORIN_MOD-INTERLUDE_STAGE_DEVICE.title", "STS2_TOMORIN_MOD-INTERLUDE_STAGE_DEVICE.description", "STS2_TOMORIN_MOD-INTERLUDE_STAGE_DEVICE.flavor",
    "STS2_TOMORIN_MOD-STAR_PICKING_STAGE_DEVICE.title", "STS2_TOMORIN_MOD-STAR_PICKING_STAGE_DEVICE.description", "STS2_TOMORIN_MOD-STAR_PICKING_STAGE_DEVICE.flavor")

foreach ($path in $ancientLocalizationPaths) {
    Assert-LocalizationKeys $path $ancientKeys
}
foreach ($path in $relicLocalizationPaths) {
    Assert-LocalizationKeys $path $relicKeys
}
$optionKeys = $tiers.Values | ForEach-Object { $_ } | ForEach-Object {
    $entry = ([regex]::Replace([string]$_, '(?<!^)([A-Z])', '_$1')).ToUpperInvariant()
    "STS2_TOMORIN_MOD-GIRAFFE_ANCIENT.pages.INITIAL.options.STS2_TOMORIN_MOD-$entry.title"
}
foreach ($path in $eventLocalizationPaths) {
    Assert-LocalizationKeys $path $optionKeys
}

Write-Host "GiraffeAncient 静态结构与本地化检查通过。"
