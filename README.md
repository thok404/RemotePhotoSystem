# RemotePhotoSystem

RemotePhotoSystem is a VRChat World photo frame system for loading remote image URLs into in-world frames.

It is designed around a local gallery JSON workflow:

- Manage photo URLs with the web gallery tool.
- Import the generated JSON into Unity.
- Bake the gallery into runtime URL arrays.
- Let players trigger random or sequence page changes in-world.

## Components

| Component | Purpose |
|---|---|
| `RemotePhotoManager` | Imports the gallery JSON, stores baked Landscape / Portrait galleries, controls play mode, loading mode, preload cache, and managed groups. |
| `RemotePhotoGroup` | Controls one set of frames, including permissions, cooldown, and synchronized photo assignment. |
| `RemotePhotoFrame` | Displays one photo on a mesh renderer and controls fit mode, projection mode, fallback texture, and background color. |
| `RemotePhotoButton` | Optional helper for prefab buttons that call `TriggerRandom()`, `TriggerPrevious()`, or `TriggerNext()` on a group. |

## Basic Setup

1. Create a `RemotePhotoManager` in the scene.
2. Assign a gallery JSON exported from the web gallery tool.
3. Click `Import JSON Into Gallery`.
4. Add one or more groups from the Manager Inspector.
5. Add `RemotePhotoFrame` components to photo mesh objects.
6. Assign frames to a group.
7. Connect buttons to group events:
   - `TriggerRandom()`
   - `TriggerPrevious()`
   - `TriggerNext()`

## Gallery JSON

Runtime only uses:

- `url`
- `orientation`

Other fields such as `id`, `tags`, `note`, and `metadata` are for gallery management tools.

## Loading Modes

| Mode | Behavior |
|---|---|
| `Preload` | Downloads future candidate images into a cache so frames can display faster when possible. |
| `NonPreload` | Downloads images only when frames need them. |

## Requirements

- VRChat SDK with UdonSharp support.
- Remote images must be direct image URLs supported by VRChat image loading.
- Photo frame materials should use the shaders included in this package.

## License

RemotePhotoSystem is distributed under the Apache License 2.0. This applies to
the core package and bundled assets in this repository unless a file states
otherwise.

Bundled example landscape images, simple geometric UI icons, and the frame
normal/detail texture were generated with ChatGPT and curated or edited for this
project.

VRChat SDK and UdonSharp are external requirements and are not included in this
repository. Separately distributed sponsor or paid frame model packs use their
own license.

## Notes

- Only one `RemotePhotoManager` should exist in a scene.
- Network sync sends URLs, not textures.
- If a client fails to download an image, that client shows its fallback texture while the synced URL remains unchanged.
