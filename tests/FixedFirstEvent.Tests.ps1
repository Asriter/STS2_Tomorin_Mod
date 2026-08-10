$ErrorActionPreference = "Stop"

$patchPath = Join-Path $PSScriptRoot "..\Scripts\Patch\FixedFirstEventPatch.cs"

if (-not (Test-Path $patchPath)) {
    throw "FixedFirstEventPatch.cs must exist."
}

$patch = Get-Content -Raw $patchPath

if ($patch -notmatch "HarmonyPatch\s*\(\s*typeof\s*\(\s*Hook\s*\)\s*,\s*nameof\s*\(\s*Hook\.ModifyNextEvent\s*\)\s*\)") {
    throw "FixedFirstEventPatch must patch Hook.ModifyNextEvent."
}

if ($patch -notmatch "CurrentActIndex\s*!=\s*1") {
    throw "FixedFirstEventPatch must target the second act only."
}

if ($patch -notmatch "BossEncounter" -or $patch -notmatch "ModelDb\.Encounter\s*<\s*RaanaBoss\s*>\s*\(") {
    throw "FixedFirstEventPatch must only force FEED_THE_CAT when the current boss is Raana."
}

if ($patch -notmatch "ModelDb\.Event\s*<\s*FeedTheCat\s*>\s*\(") {
    throw "FixedFirstEventPatch must force FEED_THE_CAT via FeedTheCat."
}

if ($patch -notmatch "HasEnteredEventInCurrentAct\s*\(") {
    throw "FixedFirstEventPatch must check whether an event has already been entered this act."
}

if ($patch -notmatch "RoomType\.Event") {
    throw "FixedFirstEventPatch must detect prior event rooms via RoomType.Event."
}

if ($patch -notmatch "VisitedEventIds\.Contains\s*\(\s*fixedEvent\.Id\s*\)") {
    throw "FixedFirstEventPatch must not force FEED_THE_CAT again if already visited."
}

if ($patch -notmatch "fixedEvent\.IsAllowed\s*\(") {
    throw "FixedFirstEventPatch must respect the fixed event's IsAllowed check."
}

if ($patch -notmatch "BeginFixedSelectionCheck\s*\(" -or $patch -notmatch "EndFixedSelectionCheck\s*\(") {
    throw "FixedFirstEventPatch must authorize FeedTheCat IsAllowed only during fixed selection."
}

if ($patch -notmatch "__result\s*=\s*fixedEvent") {
    throw "FixedFirstEventPatch must replace the next event result with FEED_THE_CAT."
}

Write-Host "Fixed first event behavior checks passed."
