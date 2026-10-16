local WarningAnimationId = 2801
local CollapseAnimationId = 2802
local StandAnimationId = 1
local CollapsedAnimationId = 104

local TickSeconds = 0.1
local WarningSeconds = 0.4
local CollisionOffSeconds = 1.0
local RespawnSeconds = 5

local BlockStanding = require("BlockStanding")

local state = "idle"
local seconds = 0

local function onTick(npc)
    seconds = seconds + TickSeconds

    if state == "idle" then
        if BlockStanding.isOccupied(npc) then
            npc:playAnimation(WarningAnimationId)
            state = "warning"
            seconds = 0
        end
    elseif state == "warning" and seconds >= WarningSeconds then
        npc:setBaseAnimation(CollapsedAnimationId)
        npc:playAnimation(CollapseAnimationId)
        state = "collapsing"
        seconds = 0
    elseif state == "collapsing" and seconds >= CollisionOffSeconds then
        npc:setCollidable(false)
        state = "gone"
        seconds = 0
    elseif state == "gone" and seconds >= RespawnSeconds then
        npc:setCollidable(true)
        npc:setBaseAnimation(StandAnimationId)
        state = "idle"
        seconds = 0
    end
end

registerCallback("tick", onTick)
