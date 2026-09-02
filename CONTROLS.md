# Controls

## Camera And Selection

- `W`, `A`, `S`, `D`: pan the camera
- Middle-mouse drag: pan the camera
- Mouse wheel: zoom the world, or scroll the menu under the pointer
- Left click: select a creature, building, or world target
- Left drag: box-select creatures using their circular hitboxes
- Right click: move selected creatures around the exact clicked world point
- Hold `F`: focus the camera on the current selection
- `Tab`: cycle the current selection
- `Escape`: close the active menu or cancel the current mode

Group moves use deterministic hexagonal formation slots. Creatures route continuously and resume
their role tasks after the move.

## Building And Orders

- Select a building from the colony menu, then left click or drag to place it
- `R`: rotate the active building placement
- Use the mining-order controls to select mineable tiles and issue or cancel orders
- Use the role radial menu on a selected trilobite to change its assignment

## Debug

- `` ` `` (tilde): open or close the debug menu
- `F3`: show or hide runtime metrics
- `Space`: pause or resume while the debug menu is open
- `Enter`: advance one simulation tick while paused
- `1`-`4`: choose a tick speed; while the BFS section is active, `1`-`3` select a field
- `P`: spawn one debug enemy

The debug menu contains `Show Role Labels` and `Show Hitboxes` toggles. Hitboxes are lime circles.
Hovering a visible hitbox shows its owner and dimensions; selected or hovered moving creatures show
their desired route.

Mining swings display a translucent magenta action hurtbox. Melee swings display a translucent
red action hurtbox. These action volumes are visible during normal play; lime physical hitboxes
remain controlled by `Show Hitboxes`. Creatures flash red briefly whenever shared damage handling
records a hit.
