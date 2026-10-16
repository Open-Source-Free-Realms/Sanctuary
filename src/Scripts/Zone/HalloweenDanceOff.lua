
local EventSchedule = require("EventSchedule")

local EventIntervalSeconds = 30 * EventSchedule.SecondsPerMinute
local KickOffLeadSeconds = 2 * EventSchedule.SecondsPerMinute

local HalloweenSeasonName = "Halloween"
local NoCompositeEffectId = 0

local AnnouncementX, AnnouncementZ = -2384, 1211
local AnnouncementRadius = 120

local PumpkinPrinceNpcId = 40001
local VampireDanceCrewNpcIds = { 40002, 40003, 40004 }
local WerewolfDanceCrewNpcIds = { 40005, 40006, 40007 }
local DanceOffBoomboxNpcId = 40008
local WerewolfCakeNpcId = 40009
local EdSullenfangsNpcId = 40010
local JakeBlackforestNpcId = 40011
local VampireCakeNpcId = 40012

local FireworksCompositeEffectId = 5349
local BoomboxSparksCompositeEffectId = 5120
local CakeSeconds = 5 * EventSchedule.SecondsPerMinute

local WalkSpeed = 1.5
local MaxWalkSeconds = 20
local BanterSeconds = 10
local DanceMoveSeconds = 8

local StandAnimationIds = {
    vampire = 1,
    werewolf = 4310,
}

local KickOffText = "The Vampires vs Werewolves Dance-off is about to kick-off in Blackspore!"
local StartText = "Pumpkin Prince: Let the Dance-off begin!"
local EndText = "Pumpkin Prince: This Dance-off is now over!"

local WonderfulNightStringId = 432436
local MangyWolvesStringId = 432150
local PayAttentionStringId = 432151
local HitItStringId = 432152
local CallThatDancingStringId = 432153
local MasterTheFloorStringId = 432387
local ActuallyYourDanceStringId = 432396
local FinaleStringId = 432384
local GoodLuckToppingStringId = 432161
local ShowEmStringId = 432162
local DropTheBeatStringId = 432163
local TerribleMovesStringId = 432164
local IsThatDancingStringId = 432402
local NeedPracticeStringId = 432401
local TurnUpTheHeatStringId = 432385
local TooAwesomeStringId = 432389
local FinalDanceStringId = 432386
local DidntStandAChanceStringId = 432392
local DeliciousVictoryStringId = 432148
local CheatingVampiresStringId = 432400
local RematchStringId = 432160
local VampiresRewardStringId = 432433
local WerewolvesAreBestStringId = 432159
local NotOverStringId = 432394
local LostToBrutesStringId = 432149
local WerewolvesRewardStringId = 432435

local TotemSongAreaId = 4299
local TotemSongSeconds = 19
local XmasSongAreaId = 4300
local XmasSongSeconds = 18
local SlideSongAreaId = 4302
local SlideSongSeconds = 29
local ChickenSongAreaId = 4303
local ChickenSongSeconds = 27
local ChillerSongAreaId = 4304
local ChillerSongSeconds = 21
local BboySongAreaId = 4305
local BboySongSeconds = 26

local Vampire, Werewolf, Prince = "vampire", "werewolf", "prince"

local TotemMoves = { 3551 }
local XmasMoves = { 3531, 3532, 3533, 3534 }
local SlideMoves = { 3521, 3522, 3523, 3524, 3525 }
local ChickenMoves = { 35010, 35020, 35030, 35040, 35050 }
local ChillerMoves = { 3511, 3512, 3513, 3514 }
local BboyMoves = { 3501 }

local Rounds = {
    {
        side = Vampire, areaId = TotemSongAreaId, moves = TotemMoves, danceSeconds = TotemSongSeconds,
        banter = {
            { atSeconds = 0, side = Prince, stringId = WonderfulNightStringId },
            { atSeconds = 3, side = Vampire, stringId = PayAttentionStringId },
            { atSeconds = 6, side = Werewolf, stringId = NeedPracticeStringId },
            { atSeconds = 9, side = Vampire, stringId = HitItStringId },
        },
        taunts = {
            { atSeconds = 5, side = Werewolf, stringId = IsThatDancingStringId },
            { atSeconds = 12, side = Werewolf, stringId = TerribleMovesStringId },
        },
    },
    {
        side = Werewolf, areaId = XmasSongAreaId, moves = XmasMoves, danceSeconds = XmasSongSeconds,
        banter = {
            { atSeconds = 0, side = Vampire, stringId = MasterTheFloorStringId },
            { atSeconds = 4, side = Werewolf, stringId = ShowEmStringId },
            { atSeconds = 9, side = Werewolf, stringId = DropTheBeatStringId },
        },
        taunts = {
            { atSeconds = 5, side = Vampire, stringId = CallThatDancingStringId },
            { atSeconds = 12, side = Vampire, stringId = ActuallyYourDanceStringId },
        },
    },
    {
        side = Vampire, areaId = SlideSongAreaId, moves = SlideMoves, danceSeconds = SlideSongSeconds,
        banter = {
            { atSeconds = 0, side = Werewolf, stringId = GoodLuckToppingStringId },
            { atSeconds = 4, side = Vampire, stringId = PayAttentionStringId },
            { atSeconds = 9, side = Vampire, stringId = HitItStringId },
        },
        taunts = {
            { atSeconds = 6, side = Werewolf, stringId = IsThatDancingStringId },
            { atSeconds = 14, side = Werewolf, stringId = NeedPracticeStringId },
            { atSeconds = 22, side = Werewolf, stringId = TerribleMovesStringId },
        },
    },
    {
        side = Werewolf, areaId = ChickenSongAreaId, moves = ChickenMoves, danceSeconds = ChickenSongSeconds,
        banter = {
            { atSeconds = 0, side = Vampire, stringId = MangyWolvesStringId },
            { atSeconds = 4, side = Werewolf, stringId = TurnUpTheHeatStringId },
            { atSeconds = 9, side = Werewolf, stringId = DropTheBeatStringId },
        },
        taunts = {
            { atSeconds = 6, side = Vampire, stringId = ActuallyYourDanceStringId },
            { atSeconds = 16, side = Vampire, stringId = CallThatDancingStringId },
        },
    },
    {
        side = Vampire, areaId = ChillerSongAreaId, moves = ChillerMoves, danceSeconds = ChillerSongSeconds,
        banter = {
            { atSeconds = 0, side = Werewolf, stringId = TooAwesomeStringId },
            { atSeconds = 4, side = Vampire, stringId = FinaleStringId },
            { atSeconds = 9, side = Vampire, stringId = HitItStringId },
        },
        taunts = {
            { atSeconds = 4, side = Werewolf, stringId = IsThatDancingStringId },
            { atSeconds = 12, side = Werewolf, stringId = TerribleMovesStringId },
        },
    },
    {
        side = Werewolf, areaId = BboySongAreaId, moves = BboyMoves, danceSeconds = BboySongSeconds,
        banter = {
            { atSeconds = 0, side = Vampire, stringId = MasterTheFloorStringId },
            { atSeconds = 4, side = Werewolf, stringId = FinalDanceStringId },
            { atSeconds = 9, side = Werewolf, stringId = DropTheBeatStringId },
        },
        taunts = {
            { atSeconds = 6, side = Vampire, stringId = ActuallyYourDanceStringId },
            { atSeconds = 14, side = Vampire, stringId = CallThatDancingStringId },
        },
    },
}

local Endings = {
    vampire = {
        winnerLineIds = {
            DidntStandAChanceStringId,
            DeliciousVictoryStringId,
        },
        loserLineIds = {
            CheatingVampiresStringId,
            RematchStringId,
        },
        rewardLineId = VampiresRewardStringId,
        cakeNpcId = VampireCakeNpcId,
    },
    werewolf = {
        winnerLineIds = {
            WerewolvesAreBestStringId,
        },
        loserLineIds = {
            NotOverStringId,
            LostToBrutesStringId,
        },
        rewardLineId = WerewolvesRewardStringId,
        cakeNpcId = WerewolfCakeNpcId,
    },
}
local EndingTimes = { announce = 2, winner = 6, loser = 9, reward = 13 }

local DanceSpot = { x = -2384.40, y = -26.77, z = 1214.94 }
local BoomboxSpot = { x = -2384.79, y = -26.83, z = 1210.41 }
local PrinceSpot = { x = -2383.08, y = -25.15, z = 1227.72 }
local HomeSpots = {
    [Vampire] = { x = -2380.40, y = -25.85, z = 1219.47 },
    [Werewolf] = { x = -2390.10, y = -25.39, z = 1219.21 },
}
local CakeSpot = { x = -2384.40, y = -26.77, z = 1217.5 }

local CrewRowDistance = 1.5
local CrewSpacing = 1.2
local CrewScatterMinBehind = 1.0
local CrewScatterMaxBehind = 3.0
local CrewScatterMaxAcross = 2.0
local CrewMinSeparation = 1.2
local CrewScatterAttempts = 20
local StopDancingSeconds = 2

local leaders = {}
local crews = {}
local prince = nil
local boombox = nil
local boomboxSparksTagId = nil
local cake = nil
local cakeSeconds = 0
local kickOffSentFor = nil

local running = false
local winner = nil
local roundIndex = 1
local phase = nil
local phaseSeconds = 0
local nextLine = 1
local moveIndex = 1
local moveSeconds = 0

local function atan2(y, x)
    if x > 0 then
        return math.atan(y / x)
    elseif x < 0 then
        return math.atan(y / x) + (y >= 0 and math.pi or -math.pi)
    end

    return y > 0 and math.pi / 2 or (y < 0 and -math.pi / 2 or 0)
end

local function headingTowards(from, to)
    return atan2(to.x - from.x, to.z - from.z)
end

local function forwardDirection(from, to)
    local forwardX, forwardZ = to.x - from.x, to.z - from.z
    local length = math.sqrt(forwardX * forwardX + forwardZ * forwardZ)

    if length == 0 then
        return 0, -1
    end

    return forwardX / length, forwardZ / length
end

local function crewSpot(leaderSpot, faceSpot, index, count)
    local forwardX, forwardZ = forwardDirection(leaderSpot, faceSpot)
    local across = (index - (count + 1) / 2) * CrewSpacing

    return {
        x = leaderSpot.x - forwardX * CrewRowDistance + forwardZ * across,
        y = leaderSpot.y,
        z = leaderSpot.z - forwardZ * CrewRowDistance - forwardX * across,
    }
end

local function scatteredCrewSpot(leaderSpot, faceSpot)
    local forwardX, forwardZ = forwardDirection(leaderSpot, faceSpot)
    local behind = CrewScatterMinBehind + math.random() * (CrewScatterMaxBehind - CrewScatterMinBehind)
    local across = (math.random() * 2 - 1) * CrewScatterMaxAcross

    return {
        x = leaderSpot.x - forwardX * behind + forwardZ * across,
        y = leaderSpot.y,
        z = leaderSpot.z - forwardZ * behind - forwardX * across,
    }
end

local function scatteredCrewSpots(home, rival, count)
    local spots = {}

    for index = 1, count do
        local spot

        for _ = 1, CrewScatterAttempts do
            spot = scatteredCrewSpot(home, rival)
            local clear = (spot.x - home.x) ^ 2 + (spot.z - home.z) ^ 2 >= CrewMinSeparation ^ 2

            for _, other in ipairs(spots) do
                if (spot.x - other.x) ^ 2 + (spot.z - other.z) ^ 2 < CrewMinSeparation ^ 2 then
                    clear = false
                end
            end

            if clear then
                break
            end
        end

        spots[index] = spot
    end

    return spots
end

local function otherSide(side)
    return side == Vampire and Werewolf or Vampire
end

local function spawnAt(zone, npcId, spot, heading)
    return zone:spawnNpc(npcId, spot.x, spot.y, spot.z, heading)
end

local function sideMembers(side)
    local members = { leaders[side] }

    for _, npc in ipairs(crews[side]) do
        members[#members + 1] = npc
    end

    return members
end

local function spawnSide(zone, side, leaderNpcId, crewNpcIds)
    local home = HomeSpots[side]
    local rival = HomeSpots[otherSide(side)]

    leaders[side] = spawnAt(zone, leaderNpcId, home, headingTowards(home, rival))
    crews[side] = {}

    for index, spot in ipairs(scatteredCrewSpots(home, rival, #crewNpcIds)) do
        crews[side][index] = spawnAt(zone, crewNpcIds[index], spot, headingTowards(spot, rival))
    end

    for _, npc in ipairs(sideMembers(side)) do
        npc:setSpeed(WalkSpeed)
        npc:setBaseAnimation(StandAnimationIds[side])
    end
end

local function spawnHosts(zone)
    prince = spawnAt(zone, PumpkinPrinceNpcId, PrinceSpot, headingTowards(PrinceSpot, BoomboxSpot))
    boombox = spawnAt(zone, DanceOffBoomboxNpcId, BoomboxSpot, headingTowards(BoomboxSpot, PrinceSpot))
    spawnSide(zone, Vampire, EdSullenfangsNpcId, VampireDanceCrewNpcIds)
    spawnSide(zone, Werewolf, JakeBlackforestNpcId, WerewolfDanceCrewNpcIds)
end

local function removeHosts()
    for _, npc in ipairs({ prince, boombox }) do
        npc:remove(NoCompositeEffectId)
    end

    for _, side in ipairs({ Vampire, Werewolf }) do
        for _, npc in ipairs(sideMembers(side)) do
            npc:remove(NoCompositeEffectId)
        end
    end

    prince, boombox, leaders, crews = nil, nil, {}, {}
end

local function walkSideToDance(side)
    leaders[side]:moveTo(DanceSpot.x, DanceSpot.y, DanceSpot.z, true)

    for index, npc in ipairs(crews[side]) do
        local target = crewSpot(DanceSpot, PrinceSpot, index, #crews[side])
        npc:moveTo(target.x, target.y, target.z, true)
    end
end

local function walkSideHome(side)
    local home = HomeSpots[side]
    local rival = HomeSpots[otherSide(side)]
    leaders[side]:moveTo(home.x, home.y, home.z, true)

    for index, target in ipairs(scatteredCrewSpots(home, rival, #crews[side])) do
        crews[side][index]:moveTo(target.x, target.y, target.z, true)
    end
end

local turned = {}

local function turnArrived(side, faceSpot, force)
    local allTurned = true

    for _, npc in ipairs(sideMembers(side)) do
        if not turned[npc] then
            if npc.isMoving and not force then
                allTurned = false
            else
                npc:setHeading(headingTowards(npc.position, faceSpot))
                npc:setBaseAnimation(StandAnimationIds[side])
                turned[npc] = true
            end
        end
    end

    return allTurned
end

local function setSideAnimation(side, animationId)
    for _, npc in ipairs(sideMembers(side)) do
        npc:setBaseAnimation(animationId)
    end
end

local function say(side, stringId)
    local speaker = side == Prince and prince or leaders[side]

    if speaker then
        speaker:sayLocalized(stringId)
    end
end

local function sayDueLines(lines)
    while lines[nextLine] and phaseSeconds >= lines[nextLine].atSeconds do
        say(lines[nextLine].side, lines[nextLine].stringId)
        nextLine = nextLine + 1
    end
end

local function enterPhase(newPhase)
    turned = {}
    phase = newPhase
    phaseSeconds = 0
    nextLine = 1
end

local function startEvent(zone)
    if cake then
        cake:remove(NoCompositeEffectId)
        cake = nil
        removeHosts()
    end

    running = true
    local sides = { Vampire, Werewolf }
    winner = sides[math.random(#sides)]
    roundIndex = 1
    spawnHosts(zone)
    zone:broadcastNear(StartText, AnnouncementX, AnnouncementZ, AnnouncementRadius)
    enterPhase("banter")
end

local function updateRound(zone, round)
    if phase == "banter" then
        sayDueLines(round.banter)

        if phaseSeconds >= BanterSeconds then
            walkSideToDance(round.side)
            enterPhase("walking")
        end
    elseif phase == "walking" then
        if turnArrived(round.side, PrinceSpot, phaseSeconds >= MaxWalkSeconds) then
            zone:setClientAreaEnabled(round.areaId, true)
            boomboxSparksTagId = boombox:addEffectTag(BoomboxSparksCompositeEffectId)
            moveIndex = 1
            moveSeconds = DanceMoveSeconds
            enterPhase("dancing")
        end
    elseif phase == "dancing" then
        sayDueLines(round.taunts)
        moveSeconds = moveSeconds + 1

        if moveSeconds >= DanceMoveSeconds then
            setSideAnimation(round.side, round.moves[moveIndex])
            moveIndex = moveIndex % #round.moves + 1
            moveSeconds = 0
        end

        if phaseSeconds >= round.danceSeconds then
            zone:setClientAreaEnabled(round.areaId, false)
            boombox:removeEffectTag(boomboxSparksTagId)
            setSideAnimation(round.side, StandAnimationIds[round.side])
            enterPhase("stopping")
        end
    elseif phase == "stopping" then
        if phaseSeconds >= StopDancingSeconds then
            walkSideHome(round.side)
            enterPhase("returning")
        end
    elseif phase == "returning" then
        if turnArrived(round.side, leaders[otherSide(round.side)].position, phaseSeconds >= MaxWalkSeconds) then
            roundIndex = roundIndex + 1
            enterPhase(Rounds[roundIndex] and "banter" or "ending")
        end
    end
end

local function updateEnding(zone)
    local ending = Endings[winner]
    local loser = otherSide(winner)

    if phaseSeconds == EndingTimes.announce then
        zone:broadcastNear(EndText, AnnouncementX, AnnouncementZ, AnnouncementRadius)
    elseif phaseSeconds == EndingTimes.winner then
        say(winner, ending.winnerLineIds[math.random(#ending.winnerLineIds)])
    elseif phaseSeconds == EndingTimes.loser then
        say(loser, ending.loserLineIds[math.random(#ending.loserLineIds)])
    elseif phaseSeconds == EndingTimes.reward then
        say(Prince, ending.rewardLineId)
        cake = spawnAt(zone, ending.cakeNpcId, CakeSpot, headingTowards(CakeSpot, DanceSpot))

        if cake then
            cake:playEffect(FireworksCompositeEffectId)
        end

        cakeSeconds = 0
        running = false
    end
end

registerCallback("second", function(zone)
    local time = os.time()

    if cake then
        cakeSeconds = cakeSeconds + 1

        if cakeSeconds >= CakeSeconds then
            cake:remove(NoCompositeEffectId)
            cake = nil
            removeHosts()
        end
    end

    if running then
        phaseSeconds = phaseSeconds + 1

        if phase == "ending" then
            updateEnding(zone)
        else
            updateRound(zone, Rounds[roundIndex])
        end

        return
    end

    if not zone:isSeasonActive(HalloweenSeasonName) then
        return
    end

    local secondOfDay = EventSchedule.secondOfDay(time)
    local secondsToStart = (EventIntervalSeconds - secondOfDay % EventIntervalSeconds) % EventIntervalSeconds
    local slot = time + secondsToStart

    if (secondOfDay + secondsToStart) % EventSchedule.BruceStageSeconds == 0 then
        return
    end

    if secondsToStart == KickOffLeadSeconds and kickOffSentFor ~= slot then
        zone:broadcast(KickOffText)
        kickOffSentFor = slot
    end

    if secondsToStart == 0 then
        startEvent(zone)
    end
end)
