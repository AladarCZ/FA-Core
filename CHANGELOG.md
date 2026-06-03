# Changelog

## Unreleased

- Added the FA cover station code path with multiblock placement, animated lid/fuel door interaction zones, fuel handling, cauldron liquid handling, and timed plate/armor coating processes.
- Added coating liquids for supported metal coatings, including liquid container properties, dye-based textures, creative bucket stacks, and language entries.
- Added client-side cover station tessellation for charcoal fuel and cauldron liquid meshes without duplicating the station model.
- Added a client-side immersed item mesh preview for plates and FA armor pieces in the cover station cauldron.
- Changed cover station fuel handling to use normal RMB on the fuel tray for charcoal/coal insertion, and adjusted immersed item preview draw order/height.
- Fixed immersed item preview tessellation to use block-atlas terrain meshes like the fuel/liquid overlays, and tightened cover station redraws for both multiblock positions.
- Fixed cover station liquid and immersed item alignment to rotate into the correct proxy side for each station orientation.
- Added Greenwich/FA armor immersed preview support by resolving ARL-style armor shape and texture attributes.
- Fixed cover station finish-state rendering so completed coated items remain visible at the cauldron bottom, fuel doors can still be used while lit, and fuel tray meshes align by station orientation.
- Changed cover station fuel tray rendering to reuse forge `fuel-coal` and `fuel-ember` textures so lit fuel visibly switches to the ember texture.
- Adjusted cover station sounds so sulfur pours use the vanilla water pour sound, fuel insertion uses charcoal placement sounds through the runtime `game:` sound domain, and bubbling plays only when plate dissolution or armor coating starts.
- Added the same charcoal placement sound feedback when taking fuel back out of the cover station fuel tray.
- Added the same vanilla water pour feedback when taking liquid back out of the cover station cauldron.
- Added a client-side looping bubbling sound while plate dissolution or armor coating is actively running, with cleanup on completion, block removal, and chunk unload.
- Cleaned up cover station block info and interaction helpers to show only user-facing cauldron contents, fuel count, and current RMB actions for the selected zone.
- Fixed duplicate cover station HUD info and added remaining time display for active plate dissolution and armor coating.
- Added splash feedback when inserting or removing immersed plates/armor.
- Corrected the animated fuel door selection box rotation so it follows the opened model more accurately, and let the cover station table storage zone pass through to vanilla ground storage behavior.
- Added Greenwich armor spawn command support through `/fac greenwich`, with normalized material/cover inputs and ARL-compatible attribute setup.
- Added FA station source/project scaffolding and source guide documentation for the code mod build.
