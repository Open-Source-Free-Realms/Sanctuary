local counter = 1

function onSecond(npc)
    counter = counter - 1
    if counter <= 0 then
        npc:say("Welcome!")
        counter = math.random(10, 20)
    end
end