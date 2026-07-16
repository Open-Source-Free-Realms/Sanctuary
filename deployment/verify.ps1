[CmdletBinding()]
param(
    [string]$SanctuaryRoot,
    [string[]]$ForbiddenToken = @()
)

$ErrorActionPreference = 'Stop'
$enforcerRoot = $PSScriptRoot
$packageRoot = Split-Path -Parent $enforcerRoot
$srcRoot = Join-Path $packageRoot 'src'
$assetRoot = Join-Path $enforcerRoot 'ClientAssets'
$baseCommit = '580e8cd59c52e6787f5fc22d352d22499aca8ab7'
$errors = [System.Collections.Generic.List[string]]::new()
$isFullSanctuaryCheckout =
    (Test-Path -LiteralPath (Join-Path $srcRoot 'Sanctuary.Core\Sanctuary.Core.csproj') -PathType Leaf) -and
    (Test-Path -LiteralPath (Join-Path $srcRoot 'Sanctuary.Gateway\Sanctuary.Gateway.csproj') -PathType Leaf)

if (-not $SanctuaryRoot -and $isFullSanctuaryCheckout) {
    $SanctuaryRoot = $packageRoot
}

function Add-ValidationError {
    param([string]$Message)
    $errors.Add($Message)
}

$expectedSourceFiles = @(
    'Sanctuary.Core\Configuration\GatewayServerOptions.cs'
    'Sanctuary.Gateway\GatewayConnection.cs'
    'Sanctuary.Gateway\gateway.json'
    'Sanctuary.Gateway\Handlers\BaseCommandPacket\CommandPacketSetProfileHandler.cs'
    'Sanctuary.Gateway\Handlers\BaseInventoryPacket\InventoryPacketEquipByGuidHandler.cs'
    'Sanctuary.Gateway\Handlers\BaseInventoryPacket\InventoryPacketEquipByItemRecordHandler.cs'
    'Sanctuary.Gateway\Handlers\BaseInventoryPacket\InventoryPacketEquippedRemoveHandler.cs'
    'Sanctuary.Gateway\Handlers\BaseInventoryPacket\InventoryPacketUseStyleCardHandler.cs'
)

$expectedAssets = @(
    [pscustomobject]@{ Name = 'enforcer_loc_backpedal.gr2'; Bytes = 26364; Sha256 = 'BF9FFC87B9746DCC6716DC3D254C8C76A39B35B27792130A884FD16710D676E8' }
    [pscustomobject]@{ Name = 'enforcer_loc_run.gr2'; Bytes = 20740; Sha256 = '53BAC321CBFFF5B9790695EEB4BCBAA289E944E649FCDE92ED36174A5D94680B' }
    [pscustomobject]@{ Name = 'enforcer_loc_walk.gr2'; Bytes = 20072; Sha256 = 'D92DFF1DCFB9A79EC75348D73377298B76F2F7C73393636D4C87129144F1E262' }
    [pscustomobject]@{ Name = 'enforcer_m_body_lod0.dme'; Bytes = 59027; Sha256 = '1612D9B30D5CC8193EC40DE8EC829F90D0F91C70BC41B913EB005B7B0AEF3966' }
    [pscustomobject]@{ Name = 'enforcer_m_chest_jacket.adr'; Bytes = 131; Sha256 = 'FDDA810BEF474D2BFEFBDD3028872DBB1C89E2F6AF5E69343ED4B6A3F3444246' }
    [pscustomobject]@{ Name = 'enforcer_m_chest_jacket.dds'; Bytes = 43832; Sha256 = '14412DECD40C4A7363DA75683C7EFF79E48B66C8F655AA3D30DE168F53081EB0' }
    [pscustomobject]@{ Name = 'enforcer_m_chest_jacket.dma'; Bytes = 212; Sha256 = 'C6DA92C6A825CD2FF0DE81222A3E78463476157BDEB8DFC743A4F4EFCF35D5DA' }
    [pscustomobject]@{ Name = 'enforcer_m_chest_jacket.dme'; Bytes = 21836; Sha256 = 'CB763E36CB26262E6BC4EC4972D4506BAA31BD4589530A56060CC37533DA6F66' }
    [pscustomobject]@{ Name = 'enforcer_m_legs_pants.adr'; Bytes = 124; Sha256 = 'A47AC04713853969BC1B9130CD87E81449E015920B32AA6671CB22A5A392840F' }
    [pscustomobject]@{ Name = 'enforcer_m_legs_pants.dds'; Bytes = 11064; Sha256 = '6A15732490F1153CE0D2A62BAF929DBA908F92F9BFC7E4814CE4468D8D3A07D1' }
    [pscustomobject]@{ Name = 'enforcer_m_legs_pants.dma'; Bytes = 230; Sha256 = 'B475CF80DB3B7F87CB94F090560E0DD1D5EBE513CE0BF059F7FE52B5C200B2AE' }
    [pscustomobject]@{ Name = 'enforcer_m_legs_pants.dme'; Bytes = 15142; Sha256 = '86C3C9BFED41DAC59D48F37844F0B3F32D01F3F6EB2FB45CB7703F62346F0254' }
    [pscustomobject]@{ Name = 'enforcer_m.adr'; Bytes = 691; Sha256 = '3238487C820E082AC8514622E5643C5DF3DF91703D08D767D2FBD6F175445235' }
    [pscustomobject]@{ Name = 'enforcer_m.dds'; Bytes = 43832; Sha256 = '1896A47DF7260B6F188FCA9079A9A80FF58692DF30AC8A0BB9519F15ED490306' }
    [pscustomobject]@{ Name = 'enforcer_m.dma'; Bytes = 199; Sha256 = '66D6E7D9640807EF03FA412618C14C0BFD041A8A8CBAAF038636C1F38914856B' }
    [pscustomobject]@{ Name = 'enforcer_m.dsk'; Bytes = 3295; Sha256 = 'AA632175EB7C5D745CB6F30877BFF368E766A93B0F253AA20987C53B6C78774F' }
)

foreach ($relativePath in $expectedSourceFiles) {
    $path = Join-Path $srcRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Add-ValidationError "Missing source overlay file: $relativePath"
    }
}

if (-not $isFullSanctuaryCheckout) {
    $resolvedSrcRoot = (Resolve-Path -LiteralPath $srcRoot).Path.TrimEnd('\', '/')
    $actualOverlayFiles = @(
        Get-ChildItem -LiteralPath $srcRoot -Recurse -File |
            ForEach-Object { $_.FullName.Substring($resolvedSrcRoot.Length).TrimStart('\', '/') }
    )

    foreach ($relativePath in $actualOverlayFiles) {
        if ($relativePath -notin $expectedSourceFiles) {
            Add-ValidationError "Unexpected Sanctuary source/config overlay file: $relativePath"
        }
    }

    if ($actualOverlayFiles.Count -ne $expectedSourceFiles.Count) {
        Add-ValidationError "The Sanctuary src overlay must contain exactly $($expectedSourceFiles.Count) files."
    }
}

if (-not (Test-Path -LiteralPath $assetRoot -PathType Container)) {
    Add-ValidationError 'Missing ClientAssets directory.'
} else {
    $actualAssetFiles = @(Get-ChildItem -LiteralPath $assetRoot -File)
    $actualAssetNames = @($actualAssetFiles.Name | Sort-Object)
    $expectedAssetNames = @($expectedAssets.Name | Sort-Object)

    foreach ($name in $expectedAssetNames) {
        if ($name -notin $actualAssetNames) {
            Add-ValidationError "Missing client asset: $name"
        }
    }

    foreach ($name in $actualAssetNames) {
        if ($name -notin $expectedAssetNames) {
            Add-ValidationError "Unexpected client asset: $name"
        }
    }

    foreach ($expected in $expectedAssets) {
        $path = Join-Path $assetRoot $expected.Name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            continue
        }

        $file = Get-Item -LiteralPath $path
        if ($file.Length -ne $expected.Bytes) {
            Add-ValidationError "Wrong byte length for $($expected.Name): expected $($expected.Bytes), found $($file.Length)"
        }

        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        if ($hash -ne $expected.Sha256) {
            Add-ValidationError "Wrong SHA-256 for $($expected.Name): $hash"
        }
    }
}

$sumPath = Join-Path $enforcerRoot 'SHA256SUMS.txt'
if (-not (Test-Path -LiteralPath $sumPath -PathType Leaf)) {
    Add-ValidationError 'Missing SHA256SUMS.txt.'
} else {
    $sumLines = @(Get-Content -LiteralPath $sumPath | Where-Object { $_.Trim() })
    if ($sumLines.Count -ne $expectedAssets.Count) {
        Add-ValidationError "SHA256SUMS.txt must contain exactly $($expectedAssets.Count) entries."
    }

    $parsedSums = @{}
    foreach ($line in $sumLines) {
        if ($line -notmatch '^([A-Fa-f0-9]{64})\s+\*?(.+)$') {
            Add-ValidationError "Invalid checksum line: $line"
            continue
        }

        $parsedSums[$Matches[2].Replace('\', '/')] = $Matches[1].ToUpperInvariant()
    }

    foreach ($expected in $expectedAssets) {
        $relativePath = "ClientAssets/$($expected.Name)"
        if (-not $parsedSums.ContainsKey($relativePath)) {
            Add-ValidationError "Checksum list is missing $relativePath"
        } elseif ($parsedSums[$relativePath] -ne $expected.Sha256) {
            Add-ValidationError "Checksum list has the wrong hash for $relativePath"
        }
    }
}

$configPath = Join-Path $enforcerRoot 'gateway-whitelist.example.json'
try {
    $config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
    if (@($config.Server.EnforcerWhitelist).Count -ne 0) {
        Add-ValidationError 'The public example whitelist must be empty.'
    }
} catch {
    Add-ValidationError 'gateway-whitelist.example.json is missing or invalid.'
}

$gatewayConfigPath = Join-Path $srcRoot 'Sanctuary.Gateway\gateway.json'
try {
    $gatewayConfig = Get-Content -LiteralPath $gatewayConfigPath -Raw | ConvertFrom-Json
    if (@($gatewayConfig.Server.EnforcerWhitelist).Count -ne 0) {
        Add-ValidationError 'The committed Gateway whitelist must be empty.'
    }
} catch {
    Add-ValidationError 'The overlay gateway.json is missing or invalid.'
}

$optionsPath = Join-Path $srcRoot 'Sanctuary.Core\Configuration\GatewayServerOptions.cs'
$connectionPath = Join-Path $srcRoot 'Sanctuary.Gateway\GatewayConnection.cs'
if (Test-Path -LiteralPath $optionsPath) {
    $optionsText = Get-Content -LiteralPath $optionsPath -Raw
    if ($optionsText -notmatch 'EnforcerWhitelistEntry\[\]\s+EnforcerWhitelist\s*\{.*?\}\s*=\s*\[\]') {
        Add-ValidationError 'Gateway options do not default the Enforcer whitelist to empty.'
    }
}

if (Test-Path -LiteralPath $connectionPath) {
    $connectionText = Get-Content -LiteralPath $connectionPath -Raw
    if ($connectionText -notmatch 'entry\.UserId\s*==\s*dbCharacter\.UserId\s*&&\s*\r?\n\s*entry\.CharacterId\s*==\s*dbCharacter\.Id') {
        Add-ValidationError 'Gateway does not require the exact UserId and CharacterId pair.'
    }
    if ($connectionText -notmatch 'entry\.UserId\s*!=\s*0' -or $connectionText -notmatch 'entry\.CharacterId\s*!=\s*0') {
        Add-ValidationError 'Gateway does not reject zero-value whitelist entries.'
    }
}

if (-not $isFullSanctuaryCheckout) {
    foreach ($forbiddenName in @('Profiles.json', 'Models.txt', 'ClientItemDefinitions.json')) {
        if (Get-ChildItem -LiteralPath $srcRoot -Recurse -File -Filter $forbiddenName) {
            Add-ValidationError "The overlay must not replace existing branch resource/config file: $forbiddenName"
        }
    }
}

$authBridgePatch = Join-Path $enforcerRoot 'AuthBridge\authbridge-enforcer-assets.patch'
if (-not (Test-Path -LiteralPath $authBridgePatch -PathType Leaf)) {
    Add-ValidationError 'Missing AuthBridge patch.'
} else {
    $patchText = Get-Content -LiteralPath $authBridgePatch -Raw
    foreach ($expected in $expectedAssets) {
        if ($patchText.IndexOf("$($expected.Name).z", [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            Add-ValidationError "AuthBridge patch is missing $($expected.Name).z"
        }
    }

    $git = Get-Command git -ErrorAction SilentlyContinue
    if ($git) {
        $parseOutput = & $git.Source apply --numstat -- $authBridgePatch 2>&1
        if ($LASTEXITCODE -ne 0) {
            Add-ValidationError "Git could not parse the AuthBridge patch: $parseOutput"
        }
    }
}

$authBridgeInstaller = Join-Path $enforcerRoot 'AuthBridge\install-authbridge-enforcer.ps1'
if (-not (Test-Path -LiteralPath $authBridgeInstaller -PathType Leaf)) {
    Add-ValidationError 'Missing AuthBridge installer.'
} else {
    $installerText = Get-Content -LiteralPath $authBridgeInstaller -Raw
    foreach ($expected in $expectedAssets) {
        if ($installerText.IndexOf("$($expected.Name).z", [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            Add-ValidationError "AuthBridge installer is missing $($expected.Name).z"
        }
    }

    $parseTokens = $null
    $parseErrors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile(
        $authBridgeInstaller,
        [ref]$parseTokens,
        [ref]$parseErrors
    )
    foreach ($parseError in @($parseErrors)) {
        Add-ValidationError "AuthBridge installer parse error: $($parseError.Message)"
    }
}

$forbiddenExtensions = @('.dll', '.exe', '.pdb', '.db', '.sqlite', '.sqlite3', '.bak', '.log', '.z')
$publicationFiles = @(
    Get-ChildItem -LiteralPath $enforcerRoot -Recurse -File
    foreach ($relativePath in $expectedSourceFiles) {
        $path = Join-Path $srcRoot $relativePath
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            Get-Item -LiteralPath $path
        }
    }
)

foreach ($file in $publicationFiles) {
    if ($file.Extension.ToLowerInvariant() -in $forbiddenExtensions) {
        Add-ValidationError "Forbidden publication artifact: $($file.FullName)"
    }
}

$textExtensions = @('.cs', '.md', '.txt', '.json', '.patch', '.ps1', '.gitignore', '.gitattributes')
$textFiles = @($publicationFiles | Where-Object {
    $_.Name -in '.gitignore', '.gitattributes' -or $_.Extension.ToLowerInvariant() -in $textExtensions
})

foreach ($file in $textFiles) {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($token in $ForbiddenToken) {
        if ($token -and $text.IndexOf($token, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            Add-ValidationError "Forbidden token found in $($file.FullName)"
        }
    }
}

if ($SanctuaryRoot) {
    try {
        $resolvedSanctuaryRoot = (Resolve-Path -LiteralPath $SanctuaryRoot).Path
        $git = Get-Command git -ErrorAction Stop
        $actualCommit = (& $git.Source -C $resolvedSanctuaryRoot rev-parse HEAD 2>$null).Trim()
        if ($LASTEXITCODE -ne 0 -or -not $actualCommit) {
            throw 'The target is not a Git checkout.'
        }

        & $git.Source -C $resolvedSanctuaryRoot merge-base --is-ancestor $baseCommit $actualCommit
        if ($LASTEXITCODE -ne 0) {
            Add-ValidationError "Target checkout $actualCommit does not descend from required base $baseCommit."
        }

        $modelsPath = Join-Path $resolvedSanctuaryRoot 'src\Resources\Models.txt'
        if (-not (Select-String -LiteralPath $modelsPath -Pattern '^536\^enforcer_m\.adr\^' -Quiet)) {
            Add-ValidationError 'Target branch is missing model 536.'
        }

        $itemsPath = Join-Path $resolvedSanctuaryRoot 'src\Resources\ClientItemDefinitions.json'
        foreach ($id in 309, 310) {
            if (-not (Select-String -LiteralPath $itemsPath -Pattern ('^    "Id": ' + $id + ',$') -Quiet)) {
                Add-ValidationError "Target branch is missing item definition $id."
            }
        }
    } catch {
        Add-ValidationError "Could not validate SanctuaryRoot: $($_.Exception.Message)"
    }
}

if ($errors.Count -gt 0) {
    Write-Error ("Verification failed:`n - " + ($errors -join "`n - "))
    exit 1
}

$layout = if ($isFullSanctuaryCheckout) { 'full Sanctuary checkout' } else { 'standalone eight-file overlay' }
Write-Host "Verification passed: $layout, eight required Sanctuary source/config files, 16 client assets, empty public whitelist, and validated AuthBridge integration files."
