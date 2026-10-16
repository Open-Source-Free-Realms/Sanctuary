local StormEffectId = 16531

local BlockStanding = require("BlockStanding")

local stormTagId = nil

local function onTick(npc)
    local occupied = BlockStanding.isOccupied(npc)

    if occupied and stormTagId == nil then
        stormTagId = npc:addEffectTag(StormEffectId)
    elseif not occupied and stormTagId ~= nil then
        npc:removeEffectTag(stormTagId)
        stormTagId = nil
    end
end

registerCallback("tick", onTick)
