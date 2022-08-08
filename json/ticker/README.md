# News Ticker

With the exception of certain policies, the majority of the lore in City Limits comes from the news ticker. During regular gameplay (when there isn't any disasters or financial problems), the news ticker provides fictional turn of events that happen in the city, such as the Kitty Kibble shortage from SimCity 3000.

## Configuration

The news ticker is completely configurable. News can be added or removed from the game without having to touch the code. The configuration is stored in the `config.json` file.

```json
{
    "outlet": "Pawprint Press",
    "competing_outlet": "Citizen Telegram",
    "ticker_files": [
        "adverts.json",
        "sammy.json",
        "kittykibble.json",
        "citylife.json",
        "extra_lore.json"
    ]
}
```

The ``outlet`` is the city's news brand, the ``competing_outlet`` is part of the optional ``extra_lore.json`` file, and ```ticker_files``` is an array of filenames that contain the news itself.
