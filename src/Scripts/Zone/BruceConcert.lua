local EventSchedule = require("EventSchedule")

local BruceNpcId = 3549

local SnowhillStageMusicAreaId = 17
local WugachugLargeMusicAreaId = 4285

local StageChangeCompositeEffectId = 21

local GatherSeconds = 5 * EventSchedule.SecondsPerMinute
local SongSeconds = 181

local WaveAnimationId = 3333
local HeyAnimationId = 3354
local YoAnimationId = 3363
local PointAnimationId = 3319
local WaitAnimationId = 3347
local TapAnimationId = 3346
local AirGuitarAnimationId = 3350
local IdleAnimationIds = { WaveAnimationId, HeyAnimationId, YoAnimationId, PointAnimationId, WaitAnimationId, TapAnimationId }
local IdleMinSeconds = 15
local IdleMaxSeconds = 25
local PerformAnimationSeconds = 8

local Stages = {
    {
        name = "Snowhill",
        x = 317.66, y = 29.83, z = 484.14, heading = -2.4,
        musicAreaIds = { SnowhillStageMusicAreaId },
        message = "Bruce: I'm about to rock the Snowhill stage, come jam with me!",
    },
    {
        name = "Wugachug",
        x = 74.6, y = 26.78, z = -413.2, heading = -1.715594,
        musicAreaIds = { WugachugLargeMusicAreaId },
        message = "Bruce: I'm about to rock the Wugachug stage, come jam with me!",
    },
}

local Phase = {
    Gathering = "Gathering",
    Playing = "Playing",
    Gone = "Gone",
}

local currentSlot = nil
local currentPhase = Phase.Gone
local bruce = nil
local nextAnimationSecond = 0

local function getSlot(secondOfDay)
    return math.floor(secondOfDay / EventSchedule.BruceStageSeconds)
end

local function getStage(slot)
    return Stages[slot % #Stages + 1]
end

local function getPhase(secondOfDay)
    local secondOfSlot = secondOfDay % EventSchedule.BruceStageSeconds

    if secondOfSlot < GatherSeconds then
        return Phase.Gathering
    end

    if secondOfSlot < GatherSeconds + SongSeconds then
        return Phase.Playing
    end

    return Phase.Gone
end

local function setStageMusic(zone, stage, enabled)
    for _, areaId in ipairs(stage.musicAreaIds) do
        zone:setClientAreaEnabled(areaId, enabled)
    end
end

local function spawnBruce(zone, stage)
    bruce = zone:spawnNpc(BruceNpcId, stage.x, stage.y, stage.z, stage.heading)

    if bruce then
        bruce:playEffect(StageChangeCompositeEffectId)
    end
end

local function removeBruce()
    if bruce then
        bruce:remove(StageChangeCompositeEffectId)
        bruce = nil
    end
end

local function enterPhase(zone, slot, phase, secondOfDay)
    local stage = getStage(slot)

    if currentSlot and currentPhase == Phase.Playing then
        setStageMusic(zone, getStage(currentSlot), false)
    end

    if phase == Phase.Gone then
        removeBruce()
    elseif not bruce or slot ~= currentSlot then
        removeBruce()
        spawnBruce(zone, stage)
    end

    if phase == Phase.Gathering then
        zone:broadcast(stage.message)
    elseif phase == Phase.Playing then
        setStageMusic(zone, stage, true)
    end

    currentSlot = slot
    currentPhase = phase
    nextAnimationSecond = secondOfDay
end

local function animateBruce(secondOfDay)
    if not bruce or secondOfDay < nextAnimationSecond then
        return
    end

    if currentPhase == Phase.Gathering then
        bruce:playAnimation(IdleAnimationIds[math.random(#IdleAnimationIds)])
        nextAnimationSecond = secondOfDay + math.random(IdleMinSeconds, IdleMaxSeconds)
    elseif currentPhase == Phase.Playing then
        bruce:playAnimation(AirGuitarAnimationId)
        nextAnimationSecond = secondOfDay + PerformAnimationSeconds
    end
end

local function update(zone)
    local secondOfDay = EventSchedule.secondOfDay()
    local slot = getSlot(secondOfDay)
    local phase = getPhase(secondOfDay)

    if slot ~= currentSlot or phase ~= currentPhase then
        enterPhase(zone, slot, phase, secondOfDay)
    end

    animateBruce(secondOfDay)
end

registerCallback("start", update)

registerCallback("second", update)
