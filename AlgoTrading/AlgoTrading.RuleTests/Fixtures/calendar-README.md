# Calendar fixtures and Batch 08 contract

These CSV files are synthetic, manually specified schedules. They are not an official exchange calendar and do not claim that a real exchange was closed or open on these dates. No online or database calendar was fetched.

calendar-days.csv declares every IST date from 24 through 29 September 2026. It deliberately closes a weekday, opens a Saturday for an evening session, closes Sunday and supplies different hours on Monday. calendar-lookups.csv independently specifies the expected sessions and statuses. The next opening after Thursday close is Saturday at 18:00; after Saturday close it is Monday at 10:00. At the end of supplied coverage the answer is unavailable, not a permanently closed market.

TradingSessionCalendar accepts an instrument token, inclusive coverage dates, a revision identifier and explicit day declarations. Times are carried by TradingSession. Nothing in production code embeds the fixture instrument, weekday rules, dates or opening/closing hours. Inputs are copied into immutable calendar state. One snapshot covers one instrument; other instruments require their own supplied snapshot and return unavailable from this one.

GetSession uses an IST DateOnly and reports Found, ClosedDay or Unavailable. GetNextSession takes an explicit instant and means strictly later opening. Before today's opening it can return today's session; exactly at the opening it searches for a later opening. Pass a qualifying period's completion to obtain its next-session activation. It does not claim to check whether a qualifying candle was finalized.

Every covered date must be explicitly declared, including weekends and holidays. Missing, duplicate, out-of-range, wrong-instrument and wrong-date entries are rejected. No session outside declared coverage is guessed. A session is one continuous interval within one IST date; overnight and split sessions remain explicitly unsupported rather than being fabricated. Calendar source adapters, revisions observed over time and production provider wiring are later work.

The tests also feed an exceptional session into IntradayCandleClock, verify completion/finalization gating and mutate/reorder the input list to check snapshot isolation and chronological lookup. Existing rule, timestamp and editor checks remain required.
