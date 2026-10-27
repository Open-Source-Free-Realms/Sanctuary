-- @name red-light-green-light
-- @description Click to join, walk away, then race back: anyone moving on "Red light!" is out
-- @param start float 30
-- @param finish float 4
-- @param item item 62538

-- States: idle (taking players), ready (waiting for them to walk out), green, red.
local state = "idle"
local players = {}
local timer = 0
local idleFor = 0

local function distance(a, b)
    return math.sqrt((a.x - b.x) ^ 2 + (a.z - b.z) ^ 2)
end

local function each(action)
    for guid, entry in pairs(players) do
        if not entry.out then
            action(guid, entry)
        end
    end
end

local function count()
    local left = 0
    each(function() left = left + 1 end)
    return left
end

local function reset()
    state = "idle"
    players = {}
    timer = 0
    npc:emote(3333) -- wave
end

local function win(entry)
    npc:playEffect(5353)
    npc:emote(3302) -- applaud
    npc:say(entry.player.name .. " wins! Fastest feet in the land!")

    if {{item}} > 0 then
        entry.player:giveItem({{item}}, 1)
    end

    reset()
end

local function remember()
    each(function(_, entry) entry.last = entry.player.position end)
end

function onInteract(player)
    npc:face(player)

    if state ~= "idle" and state ~= "ready" then
        player:message("A round is running. Wait for the next one!")
        return
    end

    if players[player.guid] then
        player:message("You're in! Walk at least " .. tostring({{start}}) .. " units away and wait for the whistle.")
        return
    end

    players[player.guid] = { player = player, out = false }
    state = "ready"
    idleFor = 0
    npc:say(player.name .. " joins the game! Walk " .. tostring({{start}}) .. " units away to start.")
end

function onSecond()
    if state == "idle" then
        return
    end

    timer = timer - 1

    if state == "ready" then
        idleFor = idleFor + 1

        if idleFor > 120 then
            npc:say("Nobody's ready? Game cancelled.")
            reset()
            return
        end

        local allOut = true
        each(function(_, entry)
            if distance(entry.player.position, npc.position) < {{start}} then allOut = false end
        end)

        if allOut and count() > 0 then
            state = "green"
            timer = math.random(3, 6)
            npc:say("Green light! GO!")
        end
        return
    end

    if count() == 0 then
        npc:say("Everyone's out! Better luck next time.")
        reset()
        return
    end

    if state == "green" then
        local winner = nil
        each(function(_, entry)
            if not winner and distance(entry.player.position, npc.position) <= {{finish}} then winner = entry end
        end)

        if winner then
            win(winner)
            return
        end

        if timer <= 0 then
            state = "red"
            timer = 3
            npc:emote(3368) -- startled turn
            npc:say("RED LIGHT!")
            remember()
        end
        return
    end

    -- Red light: anyone who moved more than a step since the call is out.
    each(function(_, entry)
        local now = entry.player.position

        if entry.last and distance(now, entry.last) > 1 then
            entry.out = true
            npc:say(entry.player.name .. ", I saw you move! You're out!")
            npc:emote(3317) -- no
        else
            entry.last = now
        end
    end)

    if timer <= 0 then
        state = "green"
        timer = math.random(2, 6)
        npc:say("Green light!")
    end
end
