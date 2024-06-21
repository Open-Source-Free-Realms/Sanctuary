using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

public static class PacketReaderExtensions
{
    public static string ReadExternalLoginPacketName(this PacketReader reader)
    {
        var opCode = reader.Read<byte>();

        var name = opCode switch
        {
            1 => "LoginRequest",
            2 => "LoginReply",
            3 => "Logout",
            4 => "ForceDisconnect",
            5 => "CharacterCreateRequest",
            6 => "CharacterCreateReply",
            7 => "CharacterLoginRequest",
            8 => "CharacterLoginReply",
            9 => "CharacterDeleteRequest",
            10 => "CharacterDeleteReply",
            11 => "CharacterSelectInfoRequest",
            12 => "CharacterSelectInfoReply",
            13 => "ServerListRequest",
            14 => "ServerListReply",
            15 => "ServerUpdate",
            16 => reader.Read<short>() switch
            {
                210 => "PacketCheckNameRequest",
                _ => "TunnelAppPacketClientToServer"
            },
            17 => reader.Read<short>() switch
            {
                211 => "PacketCheckNameReply",
                _ => "TunnelAppPacketServerToClient"
            },
            18 => "CharacterTransferRequest",
            19 => "CharacterTransferReply",
            _ => $"Unknown OpCode: {opCode}",
        };

        return name;
    }

    public static string ReadClientGatewayPacketName(this PacketReader reader)
    {
        var opCode = reader.Read<short>();

        var name = opCode switch
        {
            1 => "PacketLogin",
            2 => "PacketLoginReply",
            3 => "PacketLogout",
            4 => "PacketServerForcedLogout",
            5 => "PacketTunneledClientPacket",
            6 => "PacketTunneledClientWorldPacket",
            7 => "PacketClientIsHosted",
            8 => "PacketOnlineStatusRequest",
            9 => "PacketOnlineStatusReply",
            10 => "PacketTunneledClientGatewayPacket",
            _ => $"Unknown OpCode: {opCode}",
        };

        return name;
    }

    public static string ReadTunneledPacketName(this PacketReader reader)
    {
        var opCode = reader.Read<short>();

        var name = opCode switch
        {
            10 => "PacketClientFinishedLoading",
            12 => "PacketSendSelfToClient",
            13 => "PacketClientIsReady",
            14 => "PacketZoneDoneSendingInitialData",
            15 => reader.Read<short>() switch
            {
                1 => "PacketChat",
                2 => "ChatPacketEnterArea",
                3 => "ChatPacketDebugChat",
                4 => "ChatPacketFromStringId",
                5 => "TellEchoPacket",
                _ => "BaseChatPacket"
            },
            16 => "PacketClientLogout",
            22 => "PacketTargetClientNotOnline",
            26 => reader.Read<short>() switch
            {
                2 => "CommandPacketWhoReply",
                3 => "CommandPacketShowDialog",
                4 => "CommandPacketEndDialog",
                6 => "PacketDialogResponse",
                7 => "CommandPacketPlaySoundAtLocation",
                8 => "CommandPacketInteractRequest",
                9 => "CommandPacketInteractionList",
                10 => "CommandPacketInteractionSelect",
                11 => "CommandPacketInteractionStartWheel",
                12 => "CommandPacketStartFlashGame",
                13 => "CommandPacketSetProfile",
                14 => "CommandPacketAddFriendRequest",
                15 => "CommandPacketRemoveFriendRequest",
                16 => "CommandPacketConfirmFriendRequest",
                17 => "CommandPacketConfirmFriendResponse",
                18 => "CommandPacketSetChatBubbleColor",
                19 => "CommandPacketSelectPlayer",
                20 => "FreeInteractionNpc",
                21 => "CommandPacketFriendsPositionRequest",
                22 => "CommandPacketMoveAndInteract",
                23 => "CommandPacketQuestAbandon",
                24 => "CommandPacketRecipeStart",
                25 => "CommandPacketShowRecipeWindow",
                26 => "CommandPacketActivateProfileFailed",
                28 => "CommandPacketPlayDialogEffect",
                30 => "CommandPacketIgnoreRequest",
                31 => "CommandPacketSetActiveVehicleGuid",
                32 => "CommandPacketChatChannelOn",
                33 => "CommandPacketChatChannelOff",
                34 => "CommandPacketRequestPlayerPositions",
                35 => "CommandPacketRequestPlayerPositionsReply",
                36 => "CommandPacketSetProfileByItemDefinitionId",
                37 => "CommandPacketRequestRewardPreviewUpdate",
                38 => "CommandPacketRequestRewardPreviewUpdateReply",
                39 => "CommandPacketPlaySoundIdOnTarget",
                40 => "CommandPacketRequestPlayIntroEncounter",
                42 => "CommandPacketClosedMinigameEndScreen",
                43 => "ClearInteractionMerchantSetId",
                44 => "CommandPacketRequestStorageCheck",
                45 => "CommandPacketDirectInspect",
                1169 => "AdminCommandPacketShipCombatCommand",
                _ => "BaseCommandPacket"
            },
            31 => "PacketClientBeginZoning",
            32 => reader.Read<short>() switch
            {
                1 => "CombatPacketAutoAttackTarget",
                3 => "CombatPacketSingleAttackTarget",
                4 => "CombatPacketAttackTargetDamage",
                5 => "CombatPacketAttackAttackerMissed",
                6 => "CombatPacketAttackTargetDodged",
                7 => "CombatPacketAttackProcessed",
                9 => "CombatPacketEnableBossDisplay",
                _ => "BaseCombatPacket"
            },
            33 => reader.Read<short>() switch
            {
                1 => "VehicleRacePacketUpdateVehicleGameState",
                2 => "VehicleRacePacketUpdateVehicleGamePlayerState",
                3 => "VehicleRacePacketUpdatePlayerCount",
                4 => "VehicleRacePacketUpdateNpcCount",
                5 => "VehicleRacePacketUpdateVehicleRaceClientData",
                6 => "VehicleRacePacketUpdateVehicleRaceServerData",
                7 => "VehicleRacePacketGateHit",
                8 => "VehicleRacePacketVehicleHit",
                9 => "VehicleRacePacketRegisterVehicleRacePlayer",
                10 => "VehicleRacePacketVehicleRaceCountdownStatus",
                11 => "VehicleRacePacketCreateProxiedVehicleRace",
                12 => "VehicleRacePacketDestroyProxiedVehicleRace",
                13 => "VehicleRacePacketKickProxiedVehicleRace",
                14 => "VehicleRacePacketUpdateVehicleRaceProgressData",
                15 => "VehicleRacePacketCreateProxiedVehicleRacePickup",
                16 => "VehicleRacePacketDestroyProxiedVehicleRacePickup",
                17 => "VehicleRacePacketPickupHitRequest",
                18 => "VehicleRacePacketPickupHitResponse",
                19 => "VehicleRacePacketUseItemRequest",
                20 => "VehicleRacePacketUseItemResponse",
                21 => "VehicleRacePacketItemExpired",
                22 => "VehicleRacePacketBoostAwardRequest",
                23 => "VehicleRacePacketBoostAwardResponse",
                24 => "VehicleRacePacketCreateProxiedVehicleRaceMissile",
                25 => "VehicleRacePacketDestroyProxiedVehicleRaceMissile",
                26 => "VehicleRacePacketUpdateVehicleRaceMissileData",
                27 => "VehicleRacePacketCreateProxiedVehicleRaceExplosion",
                28 => "VehicleRacePacketDestroyProxiedVehicleRaceExplosion",
                29 => "VehicleRacePacketCreateProxiedVehicleRaceMine",
                30 => "VehicleRacePacketDestroyProxiedVehicleRaceMine",
                31 => "VehicleRacePacketUpdateVehicleRaceMineData",
                32 => "VehicleRacePacketResetNpcsRequest",
                33 => "VehicleRacePacketResetProxiedVehicleRace",
                34 => "VehicleRacePacketSetRaceConfig",
                _ => "BaseVehicleRacePacket"
            },
            34 => reader.Read<short>() switch
            {
                1 => "VehicleDemolitionDerbyPacketUpdateVehicleGameState",
                2 => "VehicleDemolitionDerbyPacketUpdateVehicleGamePlayerState",
                3 => "VehicleDemolitionDerbyPacketUpdatePlayerCount",
                4 => "VehicleDemolitionDerbyPacketUpdateNpcCount",
                5 => "VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyData",
                6 => "VehicleDemolitionDerbyPacketRegisterVehicleDemolitionDerbyPlayer",
                7 => "VehicleDemolitionDerbyPacketVehicleDemolitionDerbyCountdownStatus",
                8 => "VehicleDemolitionDerbyPacketDestroyProxiedVehicleDemolitionDerby",
                9 => "VehicleDemolitionDerbyPacketKickProxiedVehicleDemolitionDerby",
                10 => "VehicleDemolitionDerbyPacketCreateProxiedVehicleDemolitionDerby",
                11 => "VehicleDemolitionDerbyPacketUpdateVehicleDemolitionDerbyStateData",
                12 => "VehicleDemolitionDerbyPacketCreateProxiedVehicleDemolitionDerbyPickup",
                13 => "VehicleDemolitionDerbyPacketDestroyProxiedVehicleDemolitionDerbyPickup",
                14 => "VehicleDemolitionDerbyPacketPickupHitRequest",
                15 => "VehicleDemolitionDerbyPacketPickupHitResponse",
                16 => "VehicleDemolitionDerbyPacketVehicleHit",
                17 => "VehicleDemolitionDerbyPacketItemExpired",
                18 => "VehicleDemolitionDerbyPacketResetProxiedVehicleDemolitionDerby",
                19 => "VehicleDemolitionDerbyPacketSetDerbyConfig",
                20 => "VehicleDemolitionDerbyPacketAnnouncerMessage",
                _ => "BaseVehicleDemolitionDerbyPacket"
            },
            35 => reader.Read<short>() switch
            {
                1 => "PlayerUpdatePacketAddPc",
                2 => "PlayerUpdatePacketAddNpc",
                3 => "PlayerUpdatePacketRemovePlayer",
                4 => "PlayerUpdatePacketKnockback",
                5 => "PlayerUpdatePacketUpdateHitpoints",
                6 => "PlayerUpdatePacketEquipItemChange",
                7 => "PlayerUpdatePacketEquippedItemsChange",
                8 => "PlayerUpdatePacketSetAnimation",
                9 => "PlayerUpdatePacketUpdateMana",
                10 => "PlayerUpdatePacketAddNotifications",
                11 => "PlayerUpdatePacketRemoveNotifications",
                12 => "PlayerUpdatePacketNpcRelevance",
                13 => "PlayerUpdatePacketUpdateScale",
                14 => "PlayerUpdatePacketUpdateTemporaryAppearance",
                15 => "PlayerUpdatePacketRemoveTemporaryAppearance",
                16 => "PlayerUpdatePacketPlayCompositeEffect",
                17 => "PlayerUpdatePacketSetLookAt",
                18 => "PlayerUpdatePacketUpdateLivesRemaining",
                19 => "PlayerUpdatePacketRenamePlayer",
                20 => "PlayerUpdatePacketUpdateCharacterState",
                21 => "PlayerUpdatePacketUpdateWalkAnim",
                22 => "PlayerUpdatePacketQueueAnimation",
                23 => "PlayerUpdatePacketExpectedSpeed",
                24 => "PlayerUpdatePacketScriptedAnimation",
                25 => "PlayerUpdatePacketUpdateRunAnim",
                26 => "PlayerUpdatePacketUpdateIdleAnim",
                27 => "PlayerUpdatePacketThoughtBubble",
                28 => "PlayerUpdatePacketSetDisposition",
                29 => "PlayerUpdatePacketLootEvent",
                30 => "PlayerUpdatePacketHeadInflationScale",
                31 => "PlayerUpdatePacketSlotCompositeEffectOverride",
                32 => "PlayerUpdatePacketFreeze",
                33 => "PlayerUpdatePacketRequestStripEffect",
                34 => "PlayerUpdatePacketItemDefinitionRequest",
                35 => "PlayerUpdatePacketHitPointModification",
                36 => "PlayerUpdateTriggerEffectPackagePacket",
                37 => "PlayerUpdatePacketItemDefinitions",
                38 => "PlayerUpdatePacketPreferredLanguages",
                39 => "PlayerUpdatePacketCustomizationChange",
                40 => "PlayerUpdatePacketPlayerTitle",
                41 => "PlayerUpdatePacketAddEffectTagCompositeEffect",
                42 => "PlayerUpdatePacketRemoveEffectTagCompositeEffect",
                43 => "PlayerUpdatePacketEffectTagCompositeEffectsEnable",
                44 => "PlayerUpdatePacketStartRentalUpsell",
                45 => "PlayerUpdatePacketSetSpawnAnimation",
                46 => "PlayerUpdatePacketCustomizeNpc",
                47 => "PlayerUpdatePacketSetSpawnerActivationEffect",
                48 => "PlayerUpdatePacketRemoveNpcCustomization",
                49 => "PlayerUpdatePacketReplaceBaseModel",
                50 => "PlayerUpdatePacketSetCollidable",
                51 => "PlayerUpdatePacketUpdateOwner",
                52 => "PlayerUpdatePacketUpdateTintAlias",
                53 => "PlayerUpdatePacketMoveOnRail",
                54 => "PlayerUpdatePacketClearMovementRail",
                55 => "PlayerUpdatePacketMoveOnRelativeRail",
                56 => "PlayerUpdatePacketDestroyed",
                57 => "PlayerUpdatePacketUpdateShields",
                58 => "PlayerUpdatePacketHitPointAndShieldsModification",
                59 => "PlayerUpdatePacketSeekTarget",
                60 => "PlayerUpdatePacketSeekTargetUpdate",
                61 => "PlayerUpdatePacketUpdateActiveWieldType",
                62 => "PlayerUpdateLaunchProjectilePacket",
                63 => "PlayerUpdatePacketSetSynchronizedAnimations",
                64 => "HudMessagePacket",
                65 => "PlayerUpdatePacketCustomizationData",
                66 => "PlayerMemberStatusUpdatePacket",
                70 => "PlayerUpdatePacketPopup",
                71 => "PlayerUpdateProfileNameplateImageIdPacket",
                _ => "BasePlayerUpdatePacket"
            },
            36 => reader.Read<short>() switch
            {
                1 => "AbilityPacketFailed",
                3 => "AbilityPacketStartCasting",
                4 => "AbilityPacketLaunchAndLand",
                5 => "AbilityPacketSetDefinition",
                6 => "AbilityPacketClientMoveAndCast",
                7 => "AbilityPacketPurchaseAbility",
                8 => "AbilityPacketUpdateAbilityExperience",
                9 => "AbilityPacketStopAura",
                10 => "AbilityPacketClientRequestStartAbility",
                11 => "AbilityPacketMeleeRefresh",
                12 => "AbilityPacketRequestAbilityDefinition",
                13 => "AbilityPacketAbilityDefinition",
                14 => "AbilityPacketDetonateProjectile",
                15 => "AbilityPacketPulseLocationTargeting",
                16 => "AbilityPacketReceivePulseLocation",
                17 => "AbilityPacketExecuteClientLua",
                18 => "AbilityPacketCastInterrupt",
                _ => "BaseAbilityPacket"
            },
            38 => reader.Read<short>() switch
            {
                1 => "ClientUpdatePacketHitpoints",
                2 => "ClientUpdatePacketItemAdd",
                3 => "ClientUpdatePacketItemUpdate",
                4 => "ClientUpdatePacketItemDelete",
                5 => "ClientUpdatePacketEquipItem",
                6 => "ClientUpdatePacketUnequipSlot",
                7 => "ClientUpdatePacketUpdateStat",
                8 => "ClientUpdatePacketCollectionStart",
                9 => "ClientUpdatePacketCollectionRemove",
                10 => "ClientUpdatePacketCollectionAddEntry",
                11 => "ClientUpdatePacketCollectionRemoveEntry",
                12 => "ClientUpdatePacketUpdateLocation",
                13 => "ClientUpdatePacketMana",
                14 => "ClientUpdatePacketUpdateProfileExperience",
                15 => "ClientUpdatePacketAddProfileAbilitySetApl",
                16 => "ClientUpdatePacketAddEffectTag",
                17 => "ClientUpdatePacketRemoveEffectTag",
                18 => "ClientUpdatePacketUpdateProfileRank",
                19 => "ClientUpdatePacketCoinCount",
                20 => "ClientUpdatePacketDeleteProfile",
                21 => "ClientUpdatePacketActivateProfile",
                22 => "ClientUpdatePacketAddAbility",
                23 => "ClientUpdatePacketNotifyPlayer",
                24 => "ClientUpdatePacketUpdateProfileAbilitySetApl",
                25 => "ClientUpdatePacketUpdateActionBarSlot",
                26 => "ClientUpdatePacketDoneSendingPreloadCharacters",
                27 => "ClientUpdatePacketSetGrandfatheredStatus",
                29 => "ClientUpdatePacketImmediateActivationFailed",
                30 => "ClientUpdatePacketImmediateActivationCheck",
                31 => "ClientUpdatePacketStorage",
                _ => "BaseClientUpdatePacket"
            },
            39 => reader.Read<byte>() switch
            {
                1 => "MiniGameDataLockedPacket",
                2 => "MiniGameDataUnlockedPacket",
                14 => "MiniGamePayloadPacket",
                15 => "MiniGameMessagePacket",
                16 => "MiniGameInfoPacket",
                18 => "MiniGameGameOverPacket",
                20 => "MiniGameUpdateGameTimeScalar",
                21 => "MiniGameStarsEarnedPacket",
                22 => "MiniGameRewardStatusPacket",
                23 => "MiniGameKnockOutPacket",
                38 => "RequestTCGChallengePacket",
                40 => "TradingCardStartGamePacket",
                44 => "MiniGameStoreAdvertisementWorldClient",
                45 => "MiniGameLootWheelSetItemToLandOnPacket",
                46 => "MiniGameLootWheelOnRotationStoppedPacket",
                47 => "MiniGameGameEndScorePacket",
                50 => "MiniGameGroupInfoPacket",
                62 => reader.Read<int>() switch
                {
                    1 => "MiniGameBossUpdatePacket",
                    2 => "MiniGameBossDeletePacket",
                    _ => "BaseMiniGameBossPacket"
                },
                67 => "MiniGameCreateGameResultPacket",
                68 => "MiniGameUpdateRewardPacket",
                _ => "BaseMiniGamePacket"
            },
            40 => reader.Read<short>() switch
            {
                1 => "GroupPacketGroupInvite",
                2 => "GroupPacketGroupInviteReply",
                3 => "GroupPacketGroupLeave",
                4 => "GroupPacketGroupAccept",
                5 => "GroupPacketGroupAcceptReply",
                6 => "GroupPacketGroupKick",
                7 => "GroupPacketGroupKickReply",
                8 => "GroupPacketGroupUpdate",
                9 => "GroupPacketGroupMemberUpdate",
                11 => "GroupPacketRenamePlayer",
                13 => "GroupPacketAnnounceEncounterReply",
                14 => "GroupChangingJobPacket",
                15 => "GroupChangingJobUpdatePacket",
                16 => "GroupMapPingPacket",
                17 => "GroupInMinigamePacket",
                _ => "BaseGroupPacket"
            },
            41 => reader.Read<short>() switch
            {
                2 => "EncounterPacketPlayerEnter",
                3 => "EncounterPacketEncounterComplete",
                4 => "EncounterPacketEncounterLaunchFailed",
                5 => "EncounterPacketObjectiveState",
                6 => "EncounterPacketEncounterShutdown",
                102 => "EncounterInvitationPacket",
                103 => "EncounterInvitationResponsePacket",
                104 => "EncounterDuelInvitationPacket",
                105 => "EncounterDuelInvitationResponsePacket",
                106 => "EncounterStatePacket",
                107 => "EncounterZoneIsReadyPacket",
                108 => "EncounterParticipantRequestEntrancePacket",
                109 => "EncounterParticipantRequestExitPacket",
                114 => "EncounterDetailsResponsePacket",
                120 => "EncounterParticipantMessagePacket",
                121 => "EncounterParticipantTerminateEncounterPacket",
                122 => "EncounterParticipantResumePacket",
                124 => "EncounterParticipantCancelPendingEncounterPacket",
                125 => "EncounterShowRespawnWindowPacket",
                129 => "EncounterRunningListRequestPacket",
                130 => "EncounterRunningListResponsePacket",
                132 => "EncounterOverworldCombatPacket",
                133 => "EncounterPacketIsFighting",
                _ => "BaseEncounterPacket"
            },
            42 => reader.Read<short>() switch
            {
                2 => "InventoryPacketEquippedRemove",
                3 => "InventoryPacketEquipByGuid",
                4 => "InventoryPacketItemRequirementRequest",
                6 => "InventoryPacketItemActionBarAssign",
                8 => "InventoryPacketEquipByItemRecord",
                9 => "InventoryPacketItemActionBarAssignByItemRecord",
                10 => "InventoryPacketUseStyleCard",
                11 => "InventoryPacketPreviewStyleCard",
                12 => "InventoryPacketUseStyleCardByItemRecord",
                13 => "InventoryPacketEquipLightsaber",
                14 => "InventoryPacketEquipItemGroup",
                15 => "InventoryPacketUseImmediateActivationItem",
                16 => "InventoryPacketCheckImmediateActivationItem",
                17 => "InventoryPacketMoveItemToStorage",
                _ => "BaseInventoryPacket"
            },
            43 => "PacketSendZoneDetails",
            44 => reader.Read<short>() switch
            {
                1 => "ReferenceDataPacketItemClassDefinitions",
                2 => "ReferenceDataPacketItemCategoryDefinitions",
                3 => "ReferenceDataPacketClientProfileData",
                _ => "ReferenceDataPacket"
            },
            45 => reader.Read<byte>() switch
            {
                1 => "ObjectiveActivatePacket",
                2 => "ObjectiveUpdatePacket",
                3 => "ObjectiveCompletePacket",
                4 => "ObjectiveFailPacket",
                5 => "ObjectiveUnhidePacket",
                6 => "ObjectiveLookAtPacket",
                7 => "ObjectiveClientCompletePacket",
                8 => "ObjectiveClientClearPacket",
                9 => "ObjectiveClientCompleteFailedPacket",
                10 => "ObjectiveUIEventPacket",
                11 => "ObjectiveFirstMovementPacket",
                12 => "ObjectiveHousingFixtureMovePacket",
                _ => "BaseObjectivePacket"
            },
            47 => reader.Read<byte>() switch
            {
                1 => "TaskAddPacket",
                2 => "TaskUpdatePacket",
                3 => "TaskCompletePacket",
                4 => "TaskFailPacket",
                6 => "SelectTaskRequest",
                7 => "ExecuteScriptPacket",
                8 => "ExecuteScriptWithStringParamsPacket",
                9 => "LoadingScreenPacket",
                10 => "StartTimerPacket",
                12 => "SelectQuestPacket",
                13 => "SelectedQuestLockedPacket",
                14 => "ObjectiveTargetUpdatePacket",
                15 => "UiMessagePacket",
                16 => "CinematicStartLookAtPacket",
                _ => "BaseUiPacket"
            },
            49 => reader.Read<int>() switch
            {
                1 => "QuestInfoPacket",
                2 => "QuestReplyPacket",
                3 => "QuestAddPacket",
                4 => "QuestCompletePacket",
                5 => "QuestFailedPacket",
                6 => "QuestAbandonedPacket",
                7 => "QuestObjectiveAddedPacket",
                8 => "QuestObjectiveActivatedPacket",
                9 => "QuestObjectiveUpdatePacket",
                10 => "QuestObjectiveCompletePacket",
                11 => "QuestObjectiveFailedPacket",
                12 => "CompletedQuestCountUpdatePacket",
                13 => "QuestEndPacket",
                14 => "QuestEndReplyPacket",
                15 => "QuestStartBreadcrumbPacket",
                16 => "QuestEndBlockedPacket",
                _ => "BaseQuestPacket"
            },
            50 => reader.Read<byte>() switch
            {
                1 => "RewardBundlePacket",
                2 => "RewardNonBundledItem",
                6 => "RewardPacketNonMemberRewardDeferred",
                7 => "RewardPacketNonMemberRewardGranted",
                _ => "RewardBasePacket"
            },
            52 => "PacketGameTimeSync",
            53 => reader.Read<byte>() switch
            {
                1 => "PetDecisionPacket",
                2 => "PetResponsePacket",
                3 => "PetStatusPacket",
                4 => "PetSummonRecallPacket",
                5 => "PetListPacket",
                6 => "PetRequestNamePacket",
                7 => "PetPerformTrickPacket",
                8 => "PetTrickGestureResult",
                9 => "PetActivePacket",
                10 => "PetSetNamePacket",
                11 => "PetEquipPacket",
                12 => "PetTrickSkillLevelPacket",
                13 => "PetTrickAddPacket",
                14 => "PetTrickFavoriteSetPacket",
                15 => "PetTrickNotifyPacket",
                16 => "PetUtilityPacket",
                17 => "PetDeletePacket",
                18 => "PetUtilityGroomPacket",
                19 => "PetUtilityFeedPacket",
                20 => "PetUtilityNotifyPacket",
                21 => "PetUpdateUtilityItemChangePacket",
                22 => "PetTactileNotify",
                23 => "PetExpireTimePacket",
                24 => "PetUiModePacket",
                25 => "PetBuyRequestPacket",
                26 => "PetCanGoLocationPacket",
                28 => "PetInWaterPacket",
                29 => "PetTempLockoutError",
                31 => "PetWorldToClientBuyResponse",
                32 => "PetSummonByItemIdPacket",
                33 => "PetPlayWithToyPacket",
                34 => "PetMoodListPacket",
                35 => "PetEquipByItemRecordPacket",
                36 => "PetPacketOfferUpsell",
                38 => "PetPreviewRequestPacket",
                _ => "PetBasePacket"
            },
            56 => "PacketPointOfInterestDefinitionRequest",
            57 => "PacketPointOfInterestDefinitionReply",
            58 => "PacketWorldTeleportRequest",
            59 => reader.Read<byte>() switch
            {
                2 => "TradeInvitePacket",
                3 => "TradeInviteReplyPacket",
                4 => "TradeStartSessionPacket",
                5 => "TradeEndSessionPacket",
                6 => "TradeAcceptPacket",
                7 => "TradeCancelPacket",
                8 => "TradeLockPacket",
                9 => "TradeOfferItemPacket",
                10 => "TradeCoinCountPacket",
                11 => "TradeUpdateCoinCountPacket",
                12 => "TradeUpdateAddItemPacket",
                13 => "TradeUpdateItemCountPacket",
                14 => "TradeUpdateAcceptPacket",
                15 => "TradeMessagePacket",
                16 => "TradeRejectedPacket",
                17 => "TradeUpdateLockPacket",
                18 => "TradeConfirmPacket",
                19 => "TradeItemRemovedPacket",
                _ => "BaseTradePacket"
            },
            62 => "EncounterDataCommon",
            63 => reader.Read<byte>() switch
            {
                1 => "RecipeAddPacket",
                2 => "RecipeComponentUpdatePacket",
                3 => "RecipeRemovePacket",
                5 => "RecipeUpdatePacket",
                _ => "BaseRecipePacket"
            },
            66 => reader.Read<short>() switch
            {
                0 => "PacketInGamePurchaseGiftNotification",
                1 => "PacketInGamePurchasePreviewOrder",
                2 => "PacketInGamePurchasePreviewOrderResponse",
                3 => "PacketInGamePurchasePlaceOrderPacket",
                4 => "PacketInGamePurchasePlaceOrderResponse",
                5 => reader.Read<int>() switch
                {
                    1 => "PacketInGamePurchaseStoreBundles",
                    2 => "PacketInGamePurchaseStoreBundleUpdate",
                    _ => "PacketInGamePurchaseStoreBundleBase"
                },
                6 => "PacketInGamePurchaseStoreBundleCategoryGroups",
                7 => "PacketInGamePurchaseStoreBundleCategories",
                8 => "PacketInGamePurchaseExclusivePartnerStoreBundles",
                9 => "PacketInGamePurchaseStoreBundleGroups",
                10 => "PacketInGamePurchaseWalletInfoRequest",
                11 => "PacketInGamePurchaseWalletInfoResponse",
                12 => "PacketInGamePurchaseServerStatusRequest",
                13 => "PacketInGamePurchaseServerStatusResponse",
                14 => "PacketInGamePurchaseStationCashProductsRequest",
                15 => "PacketInGamePurchaseStationCashProductsResponse",
                16 => "PacketInGamePurchaseCurrencyCodesRequest",
                17 => "PacketInGamePurchaseCurrencyCodesResponse",
                18 => "PacketInGamePurchaseStateCodesRequest",
                19 => "PacketInGamePurchaseStateCodesResponse",
                20 => "PacketInGamePurchaseCountryCodesRequest",
                21 => "PacketInGamePurchaseCountryCodesResponse",
                22 => "PacketInGamePurchaseSubscriptionProductsRequest",
                23 => "PacketInGamePurchaseSubscriptionProductsResponse",
                24 => "PacketInGamePurchaseEnableMarketplace",
                25 => "PacketInGamePurchaseAccountInfoRequest",
                26 => "PacketInGamePurchaseAccountInfoResponse",
                27 => "PacketInGamePurchaseStoreBundleContentRequest",
                28 => "PacketInGamePurchaseStoreBundleContentResponse",
                29 => "PacketInGamePurchaseStoreClientStatistics",
                31 => "PacketInGamePurchaseDisplayMannequinStoreBundles",
                32 => "PacketInGamePurchaseIOTD",
                33 => "PacketInGamePurchaseStoreEnablePaymentSources",
                36 => "PacketInGamePurchasePlaceOrderClientTicket",
                38 => "InGamePurchaseUpdateItemRequirementsRequest",
                39 => "InGamePurchaseUpdateItemRequirementsReply",
                40 => "InGamePurchaseUpdateSaleDisplay",
                41 => "PacketInGamePurchaseBOTD",
                42 => "InGamePurchaseMerchantListPacket",
                43 => "InGamePurchaseBuyBundleFromMerchantRequest",
                44 => "InGamePurchaseBuyBundleFromMerchantResponse",
                45 => "InGamePurchaseUpdateMerchantSellBundleQuantityAvailableForPurchase",
                _ => "PacketBaseInGamePurchase"
            },
            67 => reader.Read<short>() switch
            {
                1 => "QuickChatSendDataPacket",
                2 => "QuickChatSendTellPacket",
                3 => "QuickChatSendChatToChannelPacket",
                _ => "BaseQuickChatPacket"
            },
            68 => reader.Read<short>() switch
            {
                1 => "ReportPacketReportPlayer",
                3 => "ReportPacketReportHouse",
                _ => "BaseReportPacket"
            },
            72 => reader.Read<byte>() switch
            {
                1 => "AddAcquaintancePacket",
                2 => "RemoveAcquaintancePacket",
                3 => "AcquaintanceOnlinePacket",
                _ => "BaseAcquaintancePacket"
            },
            73 => "PacketClientServerShuttingDown",
            74 => reader.Read<byte>() switch
            {
                0 => "FriendMessagePacket",
                1 => "FriendListPacket",
                2 => "FriendOnlinePacket",
                3 => "FriendOfflinePacket",
                5 => "FriendUpdatePositionsPacket",
                6 => "FriendAddPacket",
                7 => "FriendRemovePacket",
                9 => "FriendStatusPacket",
                10 => "FriendRenamePacket",
                _ => "BaseFriendPacket"
            },
            76 => reader.Read<short>() switch
            {
                1 => "SoccerPacketUpdatePlayerPos",
                2 => "SoccerPacketGoalMade",
                3 => "SoccerPacketRegisterPlayer",
                4 => "SoccerPacketUpdateSoccerBall",
                5 => "SoccerPacketUpdateGameState",
                6 => "SoccerPacketSetPlayerTeam",
                7 => "SoccerPacketSetClientConfig",
                8 => "SoccerPacketTeamColours",
                9 => "SoccerPacketSpawnPickUp",
                10 => "SoccerPacketAcquirePickUp",
                11 => "SoccerPacketAcquireBall",
                12 => "SoccerPacketFreeBall",
                13 => "SoccerPacketImpulseBall",
                14 => "SoccerPacketUpdatePlayerState",
                15 => "SoccerPacketUpdatePlayerStats",
                16 => "SoccerPacketUpdatePlayerControls",
                17 => "SoccerPacketStatRollInfo",
                18 => "SoccerPacketHighArcBall",
                19 => "SoccerPacketPlayerVariableStats",
                21 => "SoccerPacketSetSoccerPlayerOneTimeStats",
                22 => "SoccerPacketSpecialAnimTime",
                23 => "SoccerPacketUpdateGameTime",
                24 => "SoccerPacketSendAIInfo",
                25 => "SoccerPacketBallBounce",
                _ => "BaseSoccerPacket"
            },
            77 => reader.Read<short>() switch
            {
                3 => "BroadcastPacketWorld",
                _ => "BaseBroadcastPacket"
            },
            81 => "PacketClientKickedFromServer",
            82 => "PacketUpdateClientSessionData",
            83 => reader.Read<short>() switch
            {
                1 => "BugSubmissionPacketAddBug",
                _ => "BaseBugSubmissionPacket"
            },
            86 => "PacketWorldDisplayInfo",
            87 => "PacketMOTD",
            88 => "PacketSetLocale",
            89 => "PacketSetClientArea",
            90 => "PacketZoneTeleportRequest",
            92 => "PacketWorldShutdownNotice",
            93 => "PacketLoadWelcomeScreen",
            94 => reader.Read<short>() switch
            {
                2 => "ShipCombatPacketUpdateGameState",
                3 => "ShipCombatPacketUpdatePlayerGameStates",
                4 => "ShipCombatPacketRefreshUI",
                5 => "ShipCombatPacketRegisterShipPlayer",
                6 => "ShipCombatPacketRemoveShipPlayer",
                7 => "ShipCombatPacketSetPlayerCount",
                8 => "ShipCombatPacketUpdatePlayer",
                9 => "ShipCombatPacketCreateProxiedShip",
                10 => "ShipCombatPacketDestroyProxiedShip",
                11 => "ShipCombatPacketResetProxiedShip",
                12 => "ShipCombatPacketZoneConfig",
                13 => "ShipCombatPacketCameraConfig",
                16 => "ShipCombatPacketUpdateShipPhysics",
                17 => "ShipCombatPacketUpdatePlayerState",
                18 => "ShipCombatPacketUpdateWorldForces",
                19 => "ShipCombatPacketUpdateShipCannons",
                20 => "ShipCombatPacketFireAtLocation",
                21 => "ShipCombatPacketSelectShip",
                22 => "ShipCombatPacketSpecialMove",
                23 => "ShipCombatPacketShipHit",
                24 => "ShipCombatPacketPlayerAwards",
                25 => "ShipCombatPacketPlaySound",
                26 => "ShipCombatPacketSpawnProjectile",
                27 => "ShipCombatPacketDespawnProjectile",
                28 => "ShipCombatPacketCreateProxiedPickup",
                29 => "ShipCombatPacketDestroyProxiedPickup",
                30 => "ShipCombatPacketMarkTreasurePickups",
                31 => "ShipCombatPacketCreateProxiedStructure",
                32 => "ShipCombatPacketDestroyProxiedStructure",
                33 => "ShipCombatPacketUpdateProxiedStructure",
                34 => "ShipCombatPacketAnnouncerMessage",
                35 => "ShipCombatPacketRenderDebugData",
                36 => "ShipCombatPacketAIDebugData",
                _ => "BaseShipCombatPacket"
            },
            95 => reader.Read<byte>() switch
            {
                63 => "MiniGameFlashCommandAdminPacket",
                _ => "BaseMiniGameAdminPacket"
            },
            96 => "PacketZonePing",
            97 => "PacketClientExitLaunchUrl",
            98 => reader.Read<byte>() switch
            {
                1 => "ClientPathRequestPacket",
                2 => "ClientPathReplyPacket",
                _ => "ClientPathBasePacket"
            },
            99 => "PacketClientPendingKickFromServer",
            100 => "PacketMembershipActivation",
            101 => reader.Read<byte>() switch
            {
                1 => "JoinLobbyGamePacket",
                2 => "LeaveLobbyGamePacket",
                3 => "StartLobbyGamePacket",
                4 => "UpdateLobbyGamePacket",
                5 => "CreateLobbyGamePacket",
                6 => "SendLobbyToClientPacket",
                7 => "SendLeaveLobbyToClientPacket",
                8 => "RemoveLobbyGamePacket",
                9 => "LeaveLobbyPacket",
                11 => "LobbyErrorMessagePacket",
                12 => "ShowLobbyUiPacket",
                _ => "BaseLobbyPacket"
            },
            102 => reader.Read<short>() switch
            {
                1 => "LobbyGameDefinitionPacketDefinitionsRequest",
                2 => "LobbyGameDefinitionPacketDefinitionsResponse",
                _ => "BaseLobbyGameDefinitionPacket"
            },
            103 => "PacketShowSystemMessage",
            104 => "PacketPOIChangeMessage",
            105 => "PacketClientMetrics",
            107 => reader.Read<byte>() switch
            {
                1 => "FirstTimeEventTriggerRequest",
                2 => "FirstTimeEventStatePacket",
                3 => "FirstTimeEventClearRequest",
                4 => "FirstTimeEventEnablePacket",
                5 => "FirstTimeEventScriptPacket",
                _ => "BaseFirstTimeEventPacket"
            },
            108 => reader.Read<short>() switch
            {
                1 => "GetAllAvailableClaimItemsRequestPacket",
                2 => "GetAllAvailableClaimItemsResponsePacket",
                3 => "ClaimItemsRequestPacket",
                4 => "ClaimItemsResponsePacket",
                5 => "ClaimItemsItemDefinitionsRequest",
                6 => "ClaimItemsItemDefinitionsResponse",
                _ => "BaseClaimPacket"
            },
            109 => "PacketClientLog",
            112 => reader.Read<byte>() switch
            {
                1 => "IgnoreListPacket",
                2 => "IgnoreAddPacket",
                3 => "IgnoreRemovePacket",
                _ => "BaseIgnorePacket"
            },
            114 => reader.Read<short>() switch
            {
                1 => "KeyCodeRedemptionNotificationPacket",
                2 => "PromotionalBundleDataPacket",
                _ => "BasePromotionalPacket"
            },
            115 => "PacketAddClientPortraitCrc",
            117 => "OneTimeSessionRequestPacket",
            118 => "OneTimeSessionResponsePacket",
            122 => "PacketZoneSafeTeleportRequest",
            125 => "PlayerUpdatePacketUpdatePosition",
            126 => "PlayerUpdatePacketCameraUpdate",
            127 => reader.Read<short>() switch
            {
                1 => "ClientHousingPacketPlaceFixtureRequest",
                2 => "ClientHousingPacketPlaceFixture",
                3 => "ClientHousingPacketPickupFixture",
                4 => "ClientHousingPacketPickupAllFixtures",
                5 => "ClientHousingPacketSaveFixture",
                6 => "ClientHousingPacketSetEditMode",
                7 => "ClientHousingPacketPayUpkeep",
                9 => "ClientHousingPacketChangeHouseName",
                10 => "ClientHousingPacketLeaveHouse",
                11 => "ClientHousingPacketToggleLocked",
                12 => "ClientHousingPacketToggleFloraAllowed",
                13 => "ClientHousingPacketTogglePetAutospawn",
                14 => "ClientHousingPacketRequestPlayerHouses",
                15 => "ClientHousingPacketInvitePlayer",
                16 => "ClientHousingPacketDeclineInvite",
                17 => "ClientHousingPacketRequestName",
                18 => "ClientHousingPacketExplosionResetRequest",
                19 => "ClientHousingPacketEnterRequest",
                20 => "ClientHousingPacketPreviewByItemDefinitionIdRequest",
                21 => "ClientHousingPacketRequestVisitToNeighbor",
                22 => "ClientHousingPacketLockHouse",
                23 => "ClientHousingPacketRequestGrant",
                28 => "HousingPacketInstanceData",
                39 => "HousingPacketInstanceList",
                40 => "HousingPacketFixtureUpdate",
                41 => "HousingPacketFixtureRemove",
                42 => "HousingPacketFixtureAsset",
                43 => "HousingPacketFixtureItemList",
                44 => "HousingPacketUpdateHouseInfo",
                45 => "HousingPacketZoneData",
                46 => "HousingPacketHousingError",
                47 => "HousingPacketInviteNotification",
                48 => "HousingPacketDeclineNotification",
                49 => "HousingPacketNotifyHouseAdded",
                50 => "HousingPacketSetHeadSize",
                51 => "HousingPacketUpdateFixturePosition",
                53 => "HousingPacketFixturePlacementDenied",
                55 => "HousingPacketExplosion",
                56 => "HousingPacketExplosionReset",
                57 => "AdminHousingServerVersionPacket",
                59 => "ClientHousingPacketApplyCustomizationToFixtureGroupAndType",
                60 => "ClientHousingPacketRemoveCustomizationFromFixtureGroupAndType",
                _ => "BaseHousingPacket"
            },
            129 => reader.Read<short>() switch
            {
                1 => "GuildCreatePacket",
                2 => "GuildDeletePacket",
                3 => "GuildInvitePacket",
                4 => "GuildPromotePacket",
                5 => "GuildDemotePacket",
                6 => "GuildRemovePacket",
                7 => "GuildQuitPacket",
                8 => "GuildInviteAcceptPacket",
                9 => "GuildInviteDeclinePacket",
                10 => "GuildInviteTimeOutPacket",
                11 => "GuildMotdUpdatePacket",
                13 => "GuildRenameGuildPacket",
                14 => "GuildNameRequestPacket",
                15 => "GuildMemberLocationRequest",
                16 => "GuildPaidRenameCheckRequestPacket",
                17 => "GuildMemberStatusUpdatePacket",
                18 => "GuildNameUpdatePacket",
                19 => "GuildNameAcceptedPacket",
                20 => "GuildNameRejectedPacket",
                21 => "GuildRoleNameChangedPacket",
                22 => "GuildDataFullPacket",
                23 => "GuildErrorPacket",
                24 => "GuildInviteNotificationPacket",
                25 => "GuildMemberLocationUpdatePacket",
                26 => "GuildInviteCancelPacket",
                27 => "GuildInviteAcceptNotificationPacket",
                28 => "GuildInviteDeclineNotificationPacket",
                29 => "GuildDeleteNotificationPacket",
                30 => "GuildPaidRenameCheckReplyPacket",
                31 => "GuildPlayerStatusUpdatePacket",
                32 => "GuildCanCreateGuildPacket",
                _ => "BaseGuildPacket"
            },
            130 => reader.Read<short>() switch
            {
                2 => "BrokerSearchPacket",
                3 => "BrokerPlaceItemPacket",
                4 => "BrokerReListItemPacket",
                5 => "BrokerBuyItemPacket",
                6 => "BrokerCancelItemPacket",
                7 => "BrokerRequestItemSaleCoinPacket",
                8 => "BrokerMyItemsPacket",
                9 => "BrokerMyItemAddedPacket",
                10 => "BrokerSearchItemsPacket",
                11 => "BrokerRemoveItemFromListsPacket",
                12 => "BrokerInformationPacket",
                16 => "BrokerErrorPacket",
                _ => "BaseBrokerPacket"
            },
            131 => reader.Read<short>() switch
            {
                41 => "AdminGuildServerVersionPacket",
                _ => "BaseAdminGuildPacket"
            },
            132 => reader.Read<short>() switch
            {
                17 => "AdminBrokerServerVersionPacket",
                _ => "BaseAdminBrokerPacket"
            },
            133 => reader.Read<short>() switch
            {
                1 => "BattleMagesPacketCommand",
                2 => "BattleMagesPacketUpdateData",
                3 => "BattleMagesPacketRegisterPlayer",
                4 => "BattleMagesPacketCreateProxiedPlayer",
                5 => "BattleMagesPacketKickProxiedPlayer",
                6 => "BattleMagesPacketUpdateGameState",
                7 => "BattleMagesPacketUpdatePlayerState",
                8 => "BattleMagesPacketCameraConfig",
                9 => "BattleMagesPacketRequestAttack",
                11 => "BattleMagesPacketCreateProxiedProjectile",
                12 => "BattleMagesPacketDestroyProxiedProjectile",
                13 => "BattleMagesPacketUpdateProxiedProjectile",
                _ => "BaseBattleMagesPacket"
            },
            137 => reader.Read<byte>() switch
            {
                1 => "VehicleUpdateConfigPacket",
                2 => "VehicleUpdateItemPacket",
                3 => "VehicleRefreshConfigsPacket",
                _ => "BaseVehicleLoadoutPacket"
            },
            138 => reader.Read<short>() switch
            {
                1 => "FishingPacketUpdateData",
                2 => "FishingPacketRegisterPlayerRequest",
                3 => "FishingPacketRegisterPlayerResponse",
                4 => "FishingPacketFishInfoUpdate",
                5 => "FishingPacketCastAnimRequest",
                6 => "FishingPacketCastRequest",
                7 => "FishingPacketReelInRequest",
                8 => "FishingPacketSpawnProxiedFishingBobber",
                10 => "FishingPacketUpdateProxiedFishingBobber",
                11 => "FishingPacketSpawnProxiedFishingSchool",
                12 => "FishingPacketDespawnProxiedFishingSchool",
                13 => "FishingPacketUpdateProxiedFishingSchool",
                14 => "FishingPacketFishingResult",
                15 => "FishingPacketSpecialRequest",
                16 => "FishingPacketSpecialResponse",
                18 => "FishingPacketSpawnFishRun",
                _ => "BaseFishingPacket"
            },
            139 => reader.Read<short>() switch
            {
                1 => "VehiclePartPacketRequestVehicleConfig",
                2 => "VehiclePartPacketVehicleConfig",
                3 => "VehiclePartPacketRequestVehicleEquipmentArrayUpdate",
                4 => "VehiclePartPacketVehicleUpdateDecal",
                5 => "VehiclePartPacketRequestSelectVehicle",
                6 => "VehiclePartPacketRequestRemovePartFromSlot",
                _ => "BaseVehiclePartPacket"
            },
            141 => reader.Read<short>() switch
            {
                1 => "ListQueuesRequestPacket",
                2 => "ListQueuesResponsePacket",
                3 => "AddMatchRequestPacket",
                4 => "AddMatchRequestResponsePacket",
                5 => "ClearMatchRequestPacket",
                6 => "CancelMatchRequestPacket",
                9 => "MatchInvitationRequestPacket",
                10 => "MatchInvitationResponsePacket",
                12 => "SelectQueueForUserPacket",
                13 => "QueueStatsRequestPacket",
                14 => "QueueStatsResponsePacket",
                15 => "MatchmakingServerStatusPacket",
                18 => "RemoveParticipantFromQueueResponsePacket",
                _ => "BaseMatchmakingPacket"
            },
            142 => "PacketClientLuaMetrics",
            143 => reader.Read<byte>() switch
            {
                1 => "RepeatingActivityAddPacket",
                2 => "RepeatingActivityStatePacket",
                3 => "RepeatingActivityConsecutiveMsgPacket",
                4 => "RepeatingActivityClearRequest",
                5 => "RepeatingActivitySetConsecutivePacket",
                6 => "RepeatingActivityAddCountPacket",
                7 => "RepeatingActivityAddCountResultPacket",
                8 => "RepeatingActivitySetCountPacket",
                9 => "RepeatingActivityGetCountRemainingPacket",
                10 => "RepeatingActivityCountRemainingInfoPacket",
                _ => "BaseRepeatingActivityPacket"
            },
            144 => "PacketClientGameSettings",
            145 => "PacketClientTrialProfileUpsell",
            147 => reader.Read<byte>() switch
            {
                1 => "ActivityProfileListPacket",
                2 => "ActivityJoinErrorPacket",
                _ => "ActivityManagerBasePacket"
            },
            149 => reader.Read<byte>() switch
            {
                1 => "StartInspectPacket",
                2 => "StopInspectPacket",
                4 => "WorldInspectReplyPacket",
                _ => "BaseInspectPacket"
            },
            150 => reader.Read<int>() switch
            {
                2 => "AchievementAddPacket",
                3 => "AchievementCompletePacket",
                4 => "AchievementInitializePacket",
                6 => "AchievementObjectiveActivatedPacket",
                5 => "AchievementObjectiveAddedPacket",
                8 => "AchievementObjectiveCompletePacket",
                7 => "AchievementObjectiveUpdatePacket",
                _ => "BaseAchievementPacket"
            },
            152 => reader.Read<int>() switch
            {
                1 => "PlayerTitleAddPacket",
                2 => "PlayerTitleRemovePacket",
                3 => "PlayerTitleUpdateAllPacket",
                4 => "PlayerTitleRequestSelectPacket",
                5 => "PlayerTitleRefreshRequestPacket",
                7 => "PlayerTitleSNSUpdatePacket",
                _ => "BasePlayerTitlePacket"
            },
            155 => reader.Read<byte>() switch
            {
                1 => "BeginJobCustimzationPacket",
                2 => "FinalizeJobCustomzationPacket",
                _ => "JobCustomizationBasePacket"
            },
            156 => reader.Read<short>() switch
            {
                1 => "PacketGeneratePortraitRequest",
                2 => "PacketPortraitDataRequest",
                3 => "PacketPlayerImageData",
                4 => "PacketSnapshotRequest",
                _ => "BaseFotomatPacket"
            },
            157 => "PacketUpdateUserAge",
            164 => "PlayerUpdatePacketJump",
            165 => reader.Read<short>() switch
            {
                1 => "CoinStoreItemListPacket",
                2 => "CoinStoreItemDefinitionsRequestPacket",
                3 => "CoinStoreItemDefinitionsResponsePacket",
                4 => "CoinStoreSellToClientRequestPacket",
                5 => "CoinStoreBuyFromClientRequestPacket",
                6 => "CoinStoreTransactionCompletePacket",
                7 => "CoinStoreOpenPacket",
                8 => "CoinStoreItemDynamicListUpdateRequestPacket",
                9 => "CoinStoreItemDynamicListUpdateResponsePacket",
                10 => "CoinStoreMerchantListPacket",
                11 => "CoinStoreClearTransactionHistoryPacket",
                13 => "CoinStoreBuyBackResponsePacket",
                14 => "CoinStoreSellToClientAndGiftRequestPacket",
                15 => "CoinStoreBuyBackRequestPacket",
                17 => "CoinStoreReceiveGiftItemPacket",
                18 => "CoinStoreGiftTransactionCompletePacket",
                _ => "BaseCoinStorePacket"
            },
            166 => "PacketInitializationParameters",
            167 => reader.Read<byte>() switch
            {
                1 => reader.Read<byte>() switch
                {
                    1 => "ActivityPacketListOfActivities",
                    2 => "ActivityPacketJoinActivityRequest",
                    4 => "ActivityPacketActivityListRefreshRequest",
                    5 => "ActivityPacketUpdateActivityFeaturedStatus",
                    _ => "BaseActivityPacket"
                },
                2 => reader.Read<byte>() switch
                {
                    1 => "ScheduledActivityPacketListOfActivities",
                    _ => "BaseScheduledActivityPacket"
                },
                _ => "BaseActivityServicePacket"
            },
            168 => reader.Read<byte>() switch
            {
                1 => "PacketMountRequest",
                2 => "PacketMountResponse",
                3 => "PacketDismountRequest",
                4 => "PacketDismountResponse",
                5 => "PacketMountList",
                6 => "PacketMountSpawn",
                8 => "PacketMountSpawnByItemDefinitionId",
                9 => "PacketSetAutomount",
                10 => "PacketMountStartTrial",
                11 => "PacketMountEndTrial",
                13 => "PacketMountTrialFailed",
                14 => "PacketMountUpgradeRequest",
                15 => "PacketMountUpgradeResponse",
                _ => "MountBasePacket"
            },
            169 => "PacketClientInitializationDetails",
            171 => "PacketClientNotifyCoinSpinAvailable",
            172 => "PacketClientAreaTimer",
            173 => reader.Read<short>() switch
            {
                0 => "SetLoyaltyRewardLogoutInformation",
                _ => "BaseLoyaltyRewardPacket"
            },
            174 => reader.Read<byte>() switch
            {
                3 => "RatingPacketClientDataRequest",
                5 => "RatingPacketDataReply",
                6 => "RatingPacketAddCandidate",
                7 => "RatingPacketRemoveCandidate",
                8 => "RatingPacketAddVote",
                12 => "RatingPacketSearchRequest",
                13 => "RatingPacketSearchReply",
                16 => "RatingPacketCandidateInfoRequest",
                17 => "RatingPacketCandidateInfoReply",
                18 => "RatingPacketVoteReply",
                20 => "RatingPacketRequestFeatured",
                22 => "RatingPacketSendFeatured",
                23 => "RatingPacketVersion",
                _ => "BaseRatingPacket"
            },
            175 => reader.Read<int>() switch
            {
                1 => "ClientActivityLaunchPacketInviteDetails",
                2 => "ClientActivityLaunchPacketInviteMemberReply",
                3 => "ClientActivityLaunchPacketInviteMemberResult",
                4 => "ClientActivityLaunchPacketPlayerHasBeenAccepted",
                5 => "ClientActivityLaunchPacketActivityLaunched",
                6 => "ClientActivityLaunchPacketActivityCanceled",
                7 => "ClientActivityLaunchPacketAddMember",
                8 => "ClientActivityLaunchPacketRemoveMember",
                9 => "ClientActivityLaunchPacketUpdateMemberState",
                13 => "ClientActivityLaunchPacketKickMemberEvent",
                14 => "ClientActivityLaunchPacketOnMatchmakingStart",
                15 => "ClientActivityLaunchPacketOnMatchmakingLaunch",
                16 => "ClientActivityLaunchPacketOnMatchmakingCancel",
                17 => "ClientActivityLaunchPacketInviteResponse",
                18 => "ClientActivityLaunchPacketMemberReady",
                19 => "ClientActivityLaunchPacketMemberQuit",
                20 => "ClientActivityLaunchPacketInviteMemberRequest",
                21 => "ClientActivityLaunchPacketKickMemberRequest",
                22 => "ClientActivityLaunchPacketOwnerLaunchRequest",
                23 => "ClientActivityLaunchPacketOwnerMatchmakingRequest",
                _ => "ClientActivityLaunchBasePacket"
            },
            177 => "PacketClientFlashTimer",
            180 => reader.Read<int>() switch
            {
                1 => "PacketInviteAndStartMiniGameInvitePacket",
                3 => "PacketInviteAndStartMiniGameStartGamePacket",
                _ => "PacketInviteAndStartMiniGamePacket"
            },
            181 => "PlayerUpdatePacketFlourish",
            185 => "PlayerUpdatePacketUpdatePositionOnPlatform",
            186 => "PacketClientMembershipVipInfo",
            188 => reader.Read<short>() switch
            {
                1 => "FactoryPacketUpdateFactoryInfo",
                2 => "FactoryPacketRequestAddBlueprintToFoundation",
                5 => "FactoryPacketNpcTooltipRequest",
                6 => "FactoryPacketListToolsRequest",
                7 => "FactoryPacketEquipToolRequest",
                8 => "FactoryPacketCreateBlueprintRequest",
                14 => "FactoryPacketRequestRecipeReward",
                16 => "FactoryPacketUpdateFoundation",
                17 => "FactoryPacketUpdateBlueprint",
                20 => "FactoryPacketAddBlueprintToFoundationFailure",
                21 => "FactoryPacketNpcTooltipReply",
                22 => "FactoryPacketListToolsResponse",
                23 => "FactoryPacketEquipToolResponse",
                24 => "FactoryPacketOpenBlueprintBrowserOnFoundation",
                25 => "FactoryPacketCreateBlueprintResponse",
                26 => "FactoryPacketOpenToolshed",
                27 => "FactoryPacketOpenBlueprintGenerator",
                28 => "FactoryPacketUpdatePlot",
                29 => "FactoryPacketUpdateExperience",
                30 => "FactoryPacketDoUpsell",
                _ => "BaseFactoryPacket"
            },
            189 => "PacketMembershipSubscriptionInfo",
            190 => "PacketClientCaisNotification",
            192 => reader.Read<short>() switch
            {
                1 => "CheckNamePacket",
                2 => "CheckNameResponsePacket",
                3 => "ChangeNameRequestPacket",
                4 => "NameChangeResponsePacket",
                5 => "ChangeNameRequestStatusPacket",
                6 => "CheckNameStatusResponsePacket",
                _ => "BaseNameChangePacket"
            },
            193 => reader.Read<byte>() switch
            {
                1 => "AnnouncementDataRequestPacket",
                2 => "AnnouncementDataSendPacket",
                3 => "AnnouncementAdminSendAll",
                4 => "MemberPanelDataSendPacket",
                _ => "AnnouncementBasePacket"
            },
            194 => reader.Read<byte>() switch
            {
                2 => "WallOfDataPlayerKeyboardPacket",
                3 => "WallOfDataPlayerClickMovePacket",
                4 => "WallOfDataUIEventPacket",
                5 => "WallOfDataMembershipPurchasePacket",
                6 => "WallOfDataWalletBalancePacket",
                _ => "WallOfDataBasePacket"
            },
            195 => "PacketClientSettings",
            196 => "PacketClientSubstringBlacklist",
            197 => reader.Read<short>() switch
            {
                1 => "SnsIntegrationSecureSignatureResponse",
                2 => "SnsIntegrationFirstTimeLogin",
                3 => "SnsIntegrationGuildCreated",
                5 => "SnsIntegrationMaxLevelReached",
                6 => "SnsIntegrationSecureSignatureRequest",
                _ => "BaseSnsIntegrationPacket"
            },
            199 => "PacketClientTrialPetUpsell",
            201 => "PacketClientTrialMountUpsell",
            202 => reader.Read<byte>() switch
            {
                1 => "MysteryChestInfoRequestPacket",
                2 => "MysteryChestInfoReplyPacket",
                _ => "MysteryChestBasePacket"
            },
            203 => reader.Read<byte>() switch
            {
                4 => "NudgeUpdatePacket",
                _ => "NudgeBasePacket"
            },
            204 => "PacketEnterSocialZoneRequest",
            205 => reader.Read<short>() switch
            {
                1 => "SocialSharePacketGetProviderStatusRequest",
                2 => "SocialSharePacketGetProviderStatusResponse",
                3 => "SocialSharePacketPublishRequest",
                4 => "SocialSharePacketPublishResponse",
                _ => "BaseSocialSharePacket"
            },
            207 => reader.Read<int>() switch
            {
                0 => "ProgressiveQuestShowWindowPacket",
                1 => "ProgressiveQuestClientDataPacket",
                2 => "ProgressiveQuestRequestStartQuestPacket",
                3 => "ProgressiveQuestRequestClaimSlotPacket",
                4 => "ProgressiveQuestRequestClaimPrizePacket",
                7 => "ProgressiveQuestNotifyRewardItemPacket",
                _ => "BaseProgressiveQuestPacket"
            },
            208 => "PacketUpdateClientAreaCompositeEffect",
            209 => reader.Read<int>() switch
            {
                1 => "AdventurersJournalInfoPacket",
                2 => "AdventurersJournalQuestUpdatePacket",
                _ => "BaseAdventurersJournalPacket"
            },
            210 => "PacketCheckNameRequest",
            211 => "PacketCheckNameReply",
            _ => $"Unknown OpCode: {opCode}",
        };

        return name;
    }
}