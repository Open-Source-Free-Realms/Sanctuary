-- @name chicken-chase
-- @description Runs away from players who get close; catch it (click) for a prize, once per cooldown
-- @param item item 30589
-- @param leash float 25
-- @param speed float 9
-- @param cooldown int 60

local squawks = { "Bawk!", "BAWK BAWK!", "Not the face!", "You'll never take me alive!", "Bwaaak?!" }

local home = nil
local chaser = nil
local chasedFor = 0
local caughtAt = {}
local clock = 0

function onSpawn()
    home = npc.position
    npc:setSpeed({{speed}})
end

local function distance(a, b)
    return math.sqrt((a.x - b.x) ^ 2 + (a.z - b.z) ^ 2)
end

local function runFrom(position)
    local here = npc.position
    local dx, dz = here.x - position.x, here.z - position.z
    local length = math.sqrt(dx * dx + dz * dz)

    if length < 0.1 then
        dx, dz, length = 1, 0, 1
    end

    -- A dash away, with a sideways wobble, kept within the leash of home.
    local wobble = (math.random() - 0.5) * 6
    local x = here.x + dx / length * 8 - dz / length * wobble
    local z = here.z + dz / length * 8 + dx / length * wobble

    if distance({ x = x, z = z }, home) > {{leash}} then
        local angle = math.random() * math.pi * 2
        x = home.x + math.cos(angle) * {{leash}} * 0.5
        z = home.z + math.sin(angle) * {{leash}} * 0.5
    end

    npc:moveTo(x, home.y, z)
end

function onProximity(player)
    chaser = player
    chasedFor = 0
    npc:playEffect(5077) -- chicken clucking
    npc:say(squawks[math.random(#squawks)])
    runFrom(player.position)
end

function onSecond()
    clock = clock + 1

    if chaser == nil or home == nil then
        return
    end

    chasedFor = chasedFor + 1

    if chasedFor > 20 or distance(chaser.position, npc.position) > 12 then
        chaser = nil
        npc:moveTo(home.x, home.y, home.z)
        return
    end

    if distance(chaser.position, npc.position) < 6 then
        runFrom(chaser.position)
    end
end

function onInteract(player)
    if caughtAt[player.guid] and clock - caughtAt[player.guid] < {{cooldown}} then
        npc:say("You already caught me! Give a bird a break.")
        runFrom(player.position)
        return
    end

    caughtAt[player.guid] = clock
    chaser = nil
    npc:playEffect(46) -- teleport flash
    npc:emote(3339) -- afraid
    npc:say("Okay, okay, you got me, " .. player.name .. "! Take it and let me go!")

    if {{item}} > 0 then
        player:giveItem({{item}}, 1)
    end
end
