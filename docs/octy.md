# Open City Spec

*This ia WIP save file structure.*

Save file in City Limits are nothing more than JSON files.

## Top-level

```json
{
    "city": "",
    "mayor": "",
    "version": "",
}
```

## Zones

```json
{
    "zones": {
        "commercial": {},
        "residential": {},
        "industrial": {}
    }
}
```

Within each zone type are the zones themselves with their coordinates on the map. Each zone is given a random number as an id.

```json
{
    "1": {
        "x": 654,
        "y": 564
    },
    "2": {
        "x": 321,
        "y": 85
    }
}
```