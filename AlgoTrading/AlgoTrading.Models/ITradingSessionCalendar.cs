using System;

namespace AlgoTrading.Models
{
    public interface ITradingSessionCalendar
    {
        CalendarLookupResult GetSession(int instrumentToken, DateOnly date);
        CalendarLookupResult GetNextSession(int instrumentToken, DateTimeOffset after);
    }
}
