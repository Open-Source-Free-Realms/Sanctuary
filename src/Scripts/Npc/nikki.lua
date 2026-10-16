local GreetingStringId = 429535
local WaveAnimationId = 3333
local GreetRadius = 12
local RegreetCooldownSeconds = 120

local GreetOnApproach = require("GreetOnApproach")

local function greet(npc)
    npc:sayLocalized(GreetingStringId)
    npc:playAnimation(WaveAnimationId)
end

registerCallback("second", GreetOnApproach.create(GreetRadius, RegreetCooldownSeconds, greet))
