local BlockStanding = {
    Radius = 0.9,
    MinHeight = 0.8,
    MaxHeight = 2.2,
}

function BlockStanding.isOccupied(npc)
    return #npc:getPlayersOnTop(BlockStanding.Radius, BlockStanding.MinHeight, BlockStanding.MaxHeight) > 0
end

return BlockStanding
