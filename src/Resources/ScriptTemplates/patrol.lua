-- @name patrol
-- @description Walks between 2-4 points in order (x/z of 0 skips a point; use !whereami for coordinates)
-- @param x1 float 0
-- @param z1 float 0
-- @param x2 float 0
-- @param z2 float 0
-- @param x3 float 0
-- @param z3 float 0
-- @param x4 float 0
-- @param z4 float 0
-- @param wait int 8

local points = {}
local index = 0
local clock = 0

function onSpawn()
    local y = npc.position.y
    local all = { { {{x1}}, {{z1}} }, { {{x2}}, {{z2}} }, { {{x3}}, {{z3}} }, { {{x4}}, {{z4}} } }

    points = {}

    for _, point in ipairs(all) do
        if point[1] ~= 0 or point[2] ~= 0 then
            table.insert(points, { x = point[1], y = y, z = point[2] })
        end
    end
end

function onSecond()
    clock = clock + 1

    if #points < 2 or clock % {{wait}} ~= 0 then
        return
    end

    index = index % #points + 1
    npc:moveTo(points[index].x, points[index].y, points[index].z)
end
