local GreetingStringId = 5650
local GreetRadius = 30
local RegreetCooldownSeconds = 120

local GreetOnApproach = require("GreetOnApproach")

local function greet(npc)
    npc:sayLocalized(GreetingStringId)
end

registerCallback("second", GreetOnApproach.create(GreetRadius, RegreetCooldownSeconds, greet))
