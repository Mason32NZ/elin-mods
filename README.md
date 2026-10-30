# Material Alchemy

Configurable material conversion recipes for Elin. Turn excess rocks into ore, fiber, fragments, logs, and shining ore, break those materials back down into rocks, and fill the gaps in vanilla crafting without replacing anything.

## Features

- 17 recipes across the Alchemy Table, Blacksmith Table, Smelter, Gem Cutter, and Wood Cutter
- Every recipe can be enabled or disabled independently
- Two-way conversion for the basic materials
- Preserves the input item's material on the crafted result
- Never overwrites vanilla recipes
- Configurable ingot cost for the Material Hammer
- The Material Hammer recipe is disabled by default; all other recipes are enabled

## Recipes

### Alchemy Table

| Input | Output |
| --- | --- |
| Rock | Ore |
| Rock | Fiber |
| Rock | Fragment |
| Rock | Log |
| Rock | Shining Ore |
| Ore | Rock |
| Fiber | Rock |
| Fragment | Rock |
| Log | Rock |
| Shining Ore | Rock |

### Blacksmith Table

| Input | Output |
| --- | --- |
| Ingot | Scrap |
| Ingots (10 by default) | Material Hammer (disabled by default) |

### Smelter

| Input | Output |
| --- | --- |
| Scrap | Ingot |

### Gem Cutter

| Input | Output |
| --- | --- |
| Sun Crystal | Shining Ore |
| Earth Crystal | Shining Ore |
| Mana Crystal | Shining Ore |

### Wood Cutter

| Input | Output |
| --- | --- |
| Plank | Stick x2 |

## Configuration

Every recipe can be enabled or disabled in `BepInEx/config/m32.material_alchemy.cfg`.

The Material Hammer recipe is disabled by default. Its ingot requirement is also configurable and defaults to 10.

For in-game configuration, use [Mod Config GUI](https://steamcommunity.com/sharedfiles/filedetails/?id=3379819704).

## Compatibility

- Built for Elin 0.23.325
- Requires BepInEx 5
- Does not replace or edit vanilla recipes
- May conflict with other mods that patch the same crafting or recipe UI methods
