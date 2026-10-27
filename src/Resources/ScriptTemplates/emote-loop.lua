-- @name emote-loop
-- @description Plays an emote every few seconds
-- @param anim anim 3333
-- @param interval int 10

local clock = 0

function onSecond()
    clock = clock + 1

    if clock % {{interval}} == 0 then
        npc:emote({{anim}})
    end
end
