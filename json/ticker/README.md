# News Ticker

With the exception of certain policies, the majority of the lore in City Limits comes from the news ticker. During regular gameplay (when there isn't any disasters or financial problems), the news ticker provides fictional turn of events that happen in the city, such as the Kitty Kibble shortage from SimCity 3000.

## Extra Lore

Additionally, I've added my own turn of events with the ``extra_lore.json`` file. Internally, this is known as "Caseyverse" and has a global node of the same name. That node is designed so the game can function without it through the use of the ``is_caseyverse()`` function (which checks for that json file, at the moment) and any future related functions that deal with events.
