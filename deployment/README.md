# Enforcer job integration

This directory accompanies the Enforcer source overlay for the `enforcer-job-profile` branch of `raisingkaines/Sanctuary`.

## Authorization

Enforcer is whitelist-only and fail-closed.

Gateway creates runtime profile `20` only when one whitelist entry matches both:

- The database character's immutable `UserId`.
- The same character's immutable `CharacterId`.

Zero values are rejected. An absent or empty list authorizes nobody. Character names, models, administrator status, profile ownership, and possession of the client assets are not authorization fallbacks.

The public branch should not contain live IDs. Configure production with environment variables:

```text
Server__EnforcerWhitelist__0__UserId=<USER_ID>
Server__EnforcerWhitelist__0__CharacterId=<CHARACTER_ID>
```

For additional entries, increment the array index to `1`, `2`, and so on. You may instead merge the property from `gateway-whitelist.example.json` into the private deployed `gateway.json`.

## Sanctuary source

The overlay replaces exactly these target-branch files:

- `Sanctuary.Core/Configuration/GatewayServerOptions.cs`
- `Sanctuary.Gateway/GatewayConnection.cs`
- `Sanctuary.Gateway/gateway.json`
- `Sanctuary.Gateway/Handlers/BaseCommandPacket/CommandPacketSetProfileHandler.cs`
- `Sanctuary.Gateway/Handlers/BaseInventoryPacket/InventoryPacketEquipByGuidHandler.cs`
- `Sanctuary.Gateway/Handlers/BaseInventoryPacket/InventoryPacketEquipByItemRecordHandler.cs`
- `Sanctuary.Gateway/Handlers/BaseInventoryPacket/InventoryPacketEquippedRemoveHandler.cs`
- `Sanctuary.Gateway/Handlers/BaseInventoryPacket/InventoryPacketUseStyleCardHandler.cs`

The committed `gateway.json` keeps `EnforcerWhitelist` empty. The target branch already contains model `536` and item definitions `309` and `310`. No resource-table edit is required. Do not add profile `20` to `Resources/Profiles.json`; the profile must remain runtime-only.

The runtime implementation:

- Preserves the character's real storage profile.
- Creates an Enforcer alias as active profile `20`.
- Uses model `536`, name string `2301`, class `47`, jacket `309`, and pants `310`.
- Creates transient uniform item IDs that are never written to the database.
- Clears the old head, hair, face paint, skin, and model customization overlays.
- Blocks profile switching, equipping, unequipping, and style-card changes while Enforcer is active.
- Maps profile `20` back to the stored profile during disconnect persistence.

The client renders the active profile `20` job label in red.

## Client assets

Copy all 16 raw files from `ClientAssets` directly into the game client's asset root. Keep the filenames unchanged. Do not add `.z` to the stored files and do not put them in manifest bucket directories.

Only walk, run, and backpedal locomotion clips are included. The model relies on its existing animation references for everything else.

## AuthBridge

AuthBridge must advertise manifest overrides for the 16 loose files. The recommended installer is idempotent: it adds only entries that are missing, so it works both before and after the three locomotion overrides were deployed.

From the AuthBridge source root:

```powershell
powershell -ExecutionPolicy Bypass -File <path>\install-authbridge-enforcer.ps1 -ServerPath .\server.mjs
```

The installer creates a timestamped backup beside `server.mjs` before changing it. `AuthBridge/authbridge-enforcer-assets.patch` is also provided for review or for a clean pre-Enforcer AuthBridge baseline; apply it only when `git apply --check` succeeds.

`AuthBridge/Assets_manifest.enforcer.txt` records the expected CRC and generated wrapped size for each file.

Restart AuthBridge after applying the patch. Fully exit and relaunch the game client after any prior asset-load failure.

## Verify

From the Sanctuary repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\deployment\verify.ps1
```

The verifier supports both this full repository layout and the original standalone eight-file source overlay. It checks the required Sanctuary changes, empty public whitelist, AuthBridge patch, and all 16 client-asset checksums.

## Build

From the Sanctuary repository root:

```powershell
dotnet build .\src\Sanctuary.Gateway\Sanctuary.Gateway.csproj -c Release
```

This overlay was built against commit `580e8cd59c52e6787f5fc22d352d22499aca8ab7` with zero warnings and zero errors.

## Test

1. A listed character should load as profile `20`, model `536`, jacket `309`, and pants `310`.
2. Its old avatar head and hair should not remain merged into the Enforcer model.
3. Walk, run, and backpedal should animate.
4. An unlisted character, including another character on the same account, must load normally.
5. Removing the whitelist entry and restarting Gateway must deny Enforcer on the next login.

For immediate rollback, empty the whitelist and restart Gateway.
