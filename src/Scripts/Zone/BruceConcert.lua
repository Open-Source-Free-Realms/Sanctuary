local EventSchedule = require("EventSchedule")

local BruceNpcId = 3549

local SnowhillStageMusicAreaId = 17
local WugachugLargeMusicAreaId = 4285

local StageChangeCompositeEffectId = 21

local Stages = {
    {
        name = "Snowhill",
        x = 317.66, y = 29.83, z = 484.14, heading = -2.4,
        musicAreaIds = { SnowhillStageMusicAreaId },
        message = "Bruce: I'm rocking the Snowhill stage now, come jam with me!",
    },
    {
        name = "Wugachug",
        x = 74.6, y = 26.78, z = -413.2, heading = -1.715594,
        musicAreaIds = { WugachugLargeMusicAreaId },
        message = "Bruce: I'm rocking the Wugachug stage now, come jam with me!",
    },
}

local currentStageIndex = nil
local bruce = nil

local function getStageIndex()
    return math.floor(EventSchedule.secondOfDay() / EventSchedule.BruceStageSeconds) % #Stages + 1
end

local function setStageMusic(zone, stage, enabled)
    for _, areaId in ipairs(stage.musicAreaIds) do
        zone:setClientAreaEnabled(areaId, enabled)
    end
end

local function moveToStage(zone, stageIndex)
    if currentStageIndex then
        setStageMusic(zone, Stages[currentStageIndex], false)
    end

    if bruce then
        bruce:remove(StageChangeCompositeEffectId)
        bruce = nil
    end

    local stage = Stages[stageIndex]
    bruce = zone:spawnNpc(BruceNpcId, stage.x, stage.y, stage.z, stage.heading)

    if bruce then
        bruce:playEffect(StageChangeCompositeEffectId)
    end

    setStageMusic(zone, stage, true)
    zone:broadcast(stage.message)
    currentStageIndex = stageIndex
end

registerCallback("start", function(zone)
    moveToStage(zone, getStageIndex())
end)

registerCallback("second", function(zone)
    local stageIndex = getStageIndex()

    if stageIndex ~= currentStageIndex then
        moveToStage(zone, stageIndex)
    end
end)
