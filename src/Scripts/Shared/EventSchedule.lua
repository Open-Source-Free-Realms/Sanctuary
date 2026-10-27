local EventSchedule = {}

EventSchedule.SecondsPerMinute = 60
EventSchedule.SecondsPerHour = 60 * EventSchedule.SecondsPerMinute

EventSchedule.BruceStageSeconds = 2 * EventSchedule.SecondsPerHour

function EventSchedule.secondOfDay(time)
    local date = os.date("*t", time)
    return date.hour * EventSchedule.SecondsPerHour + date.min * EventSchedule.SecondsPerMinute + date.sec
end

return EventSchedule
