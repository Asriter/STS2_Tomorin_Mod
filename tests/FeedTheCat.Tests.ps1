$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot

function Assert-FileExists($Path) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $Path))) {
        throw "Missing file: $Path"
    }
}

function Assert-Contains($Path, $Pattern, $Message) {
    $content = Get-Content -LiteralPath (Join-Path $root $Path) -Raw -ErrorAction Stop
    if ($content -notmatch $Pattern) {
        throw $Message
    }
}

Assert-FileExists "Scripts/Events/FeedTheCat.cs"
Assert-FileExists "Scripts/Events/FeedTheCatVoteCoordinator.cs"
Assert-FileExists "Scripts/Relics/MatchaParfait.cs"
Assert-FileExists "Scripts/Relics/EmptyParfait.cs"
Assert-FileExists "STS2_Tomorin_Mod/localization/eng/events.json"
Assert-FileExists "STS2_Tomorin_Mod/localization/zhs/events.json"

Assert-Contains "Scripts/Events/FeedTheCat.cs" "class\s+FeedTheCat\s*:\s*CustomEventModel" "FeedTheCat must extend CustomEventModel."
Assert-Contains "Scripts/Events/FeedTheCat.cs" "override\s+bool\s+IsShared\s*=>\s*false" "FeedTheCat must remain non-shared."
Assert-Contains "Scripts/Events/FeedTheCat.cs" "_allowFixedSelectionCheck\s*&&\s*runState\.CurrentActIndex\s*==\s*1" "FeedTheCat must not be allowed from the random event pool."
Assert-Contains "Scripts/Events/FeedTheCat.cs" "LockedOption\(\""Waiting\"",\s*WaitingPage\)" "Waiting page must keep a locked option instead of ending the event."
Assert-Contains "Scripts/Events/FeedTheCat.cs" "CardPileCmd\.AddCurseToDeck<Debt>" "Debt option must use native Debt."
Assert-Contains "Scripts/Events/FeedTheCat.cs" "CardSelectCmd\.FromDeckForRemoval" "Remove option must use native deck removal selection."
Assert-Contains "Scripts/Events/FeedTheCat.cs" "CardSelectCmd\.FromDeckForUpgrade" "Upgrade option must use native deck upgrade selection."
Assert-Contains "Scripts/Events/FeedTheCat.cs" "PlayerCmd\.LoseGold\(200m" "Gold option must lose exactly 200 gold."
Assert-Contains "Scripts/Events/FeedTheCatVoteCoordinator.cs" "PlayerChoiceResult\.FromIndex" "Final branch must be synchronized as an index."
Assert-Contains "Scripts/Events/FeedTheCatVoteCoordinator.cs" "SyncLocalChoice" "Host must broadcast final branch."
Assert-Contains "Scripts/Events/FeedTheCatVoteCoordinator.cs" "WaitForRemoteChoice" "Clients must wait for host final branch."
Assert-Contains "Scripts/Events/FeedTheCatVoteCoordinator.cs" "RunState\.Players" "Host vote resolution must wait for all run players, not only current event instances."
Assert-Contains "Scripts/Events/FeedTheCatVoteCoordinator.cs" "eventsByPlayer\.TryGetValue" "Final branch must apply back to event instances by player NetId."
Assert-Contains "Scripts/Patch/FixedFirstEventPatch.cs" "ModelDb\.Event<FeedTheCat>\(\)" "Fixed first event must target FeedTheCat."
Assert-Contains "Scripts/Patch/FixedFirstEventPatch.cs" "!fixedEvent\.IsAllowed\(concreteRunState\)" "Fixed first event must keep IsAllowed guard."
Assert-Contains "Scripts/RelicPools/TomorinRelicPool.cs" "ModelDb\.Relic<MatchaParfait>\(\)" "MatchaParfait must be registered."
Assert-Contains "Scripts/RelicPools/TomorinRelicPool.cs" "ModelDb\.Relic<EmptyParfait>\(\)" "EmptyParfait must be registered."
Assert-Contains "STS2_Tomorin_Mod/localization/eng/events.json" "FEED_THE_CAT\.pages\.INITIAL\.description" "English event localization must include initial page."
Assert-Contains "STS2_Tomorin_Mod/localization/zhs/events.json" "FEED_THE_CAT\.pages\.INITIAL\.description" "Chinese event localization must include initial page."

Write-Host "FeedTheCat focused checks passed."
