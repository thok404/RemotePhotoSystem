# RemotePhotoSystem Gallery Manager Guide

### What This Tool Does

`Gallery Manager` is the web-based gallery management tool for RemotePhotoSystem.  
It manages image URL entries and exports the gallery JSON that Unity can bake.

Unity no longer edits image URLs one by one.  
The recommended workflow is:

1. Manage the gallery in this web tool.
2. Export `RemotePhotoGalleryConfig.json`.
3. Put the JSON file into the Unity project.
4. Assign it to `RemotePhotoGalleryService`.
5. Click `Bake Gallery Config Into Runtime Arrays`.

### How To Open

Open this file directly in a browser:

```text
Tools/RemotePhotoSystemGallery/index.html
```

This is a local static web tool. It does not require a server or dependencies.

### Adding A Few Images

For small test galleries:

1. Click `Add Blank Entry`.
2. Fill in the `URL`.
3. Choose `Orientation`:
   - `Landscape`: horizontal image
   - `Portrait`: vertical image
4. Optionally fill in `Tags` and `Note`.
5. Click `Export Unity JSON`.

### Importing Many Images

If you already have many direct image URLs:

1. Paste one URL per line into `Bulk URL Import`.
2. Choose a default orientation in `Default Orientation`.
3. Click `Import Pasted URLs`.
4. You can batch edit orientation later, or use image probing to detect it automatically.

You can also use `Import URL TXT` to import a `.txt` file.  
The TXT format is one image URL per line.

### Table Fields

- `ID`
  - A stable name for web-tool management.
  - Ignored by Unity bake.
- `URL`
  - Direct image URL.
  - Unity bake only accepts URLs starting with `http://` or `https://`.
- `Orientation`
  - Decides whether the image goes into the landscape or portrait pool.
  - Landscape frames only pick from landscape images. Portrait frames only pick from portrait images.
- `Tags`
  - For filtering and organizing inside the web tool.
  - Ignored by Unity bake.
- `Note`
  - Personal note.
  - Ignored by Unity bake.
- `Metadata`
  - Image size probe result.
  - Ignored by Unity bake.

### Batch Operations

Select entries first, then use batch buttons:

- `Select Page`
  - Select entries on the current page.
- `Select Filtered`
  - Select all entries in the current filter result.
- `Clear Selection`
  - Clear selection.
- `Set Selected Landscape`
  - Set selected entries to landscape.
- `Set Selected Portrait`
  - Set selected entries to portrait.
- `Generate IDs`
  - Regenerate IDs from URL file names.
- `Remove Duplicate URLs`
  - Remove duplicate URLs.
- `Delete Selected`
  - Delete selected entries.
- `Add Tag To Selected`
  - Add a tag to selected entries.
- `Remove Tag From Selected`
  - Remove a tag from selected entries.

### Large Gallery Performance

The tool does not render tens of thousands of rows at once.  
It uses pagination so large URL lists remain manageable.

Use:

- `Search` to search URL, ID, tags, and notes
- `Orientation` to filter landscape or portrait entries
- `Metadata` to filter probe status
- `Page Size` to change how many rows are visible at once

### Image Size Probing

`Probe Image Sizes` asks the browser to load each image once and read its width and height.

After a successful probe:

- `Metadata` records width and height.
- If `Apply Detected Orientation` is checked, the tool updates orientation automatically.
  - Width greater than or equal to height: `Landscape`
  - Height greater than width: `Portrait`

### Probe Scope

`Probe Scope` controls which entries are probed:

- `Selected`
  - Probe selected entries only.
- `Filtered`
  - Probe the current filter result.
- `Missing Metadata`
  - Probe entries without metadata only.
- `All`
  - Probe every entry.

For incremental updates, prefer `Missing Metadata` or `Selected` to avoid unnecessary image-source reads.

### Local Cache And Force Refresh

The tool stores image metadata in browser `IndexedDB`.  
The cache key is the image URL.

During a normal probe:

- If cached width and height already exist for that URL, the tool uses the cache.
- This reduces read operations on image hosting such as Cloudflare R2.

When `Force Refresh` is checked:

- The tool ignores local cache.
- It loads the image again and reads the size.
- Then it updates the local cache.

Use `Force Refresh` when the image behind the same URL has changed in cloud storage.

### Exporting To Unity

Click:

```text
Export Unity JSON
```

This exports:

```text
RemotePhotoGalleryConfig.json
```

Then in Unity:

1. Put the JSON file anywhere under `Assets`.
2. Select the scene object with `RemotePhotoGalleryService`.
3. Assign the JSON to `Gallery Config JSON`.
4. Click `Bake Gallery Config Into Runtime Arrays`.

Unity bake only reads:

- `url`
- `orientation`

Unity bake ignores:

- `id`
- `tags`
- `note`
- `metadata`

### Where Fit Mode Is Set

Fit mode is not set in the gallery.  
It is set on each frame through `RemotePhotoFrameDisplay`:

`RemotePhotoFrameDisplay` must be placed directly on the frame GameObject that has the `MeshRenderer`.

```text
Photo Fit Mode
```

`Default Texture` is the image shown when the world starts; if it is empty, the script does not modify the material Albedo, so the material keeps its own default texture or color. `Fallback Texture` is the image shown when a download fails or the frame is cleared; if it is empty, the script also leaves the material Albedo unchanged.

This lets the same image appear differently on different frames.

The letterbox color for `Contain` mode is set on the same component:

```text
Contain Background Color
```

If the Inspector says the current material is not using a RemotePhotoSystem photo frame shader, choose:

```text
Apply Unlit Shader
Apply Lit Shader
```

`Unlit` is best for flat photo display that should not react to scene lighting. Both RemotePhotoSystem photo frame shaders are fixed to opaque rendering and do not provide a transparent mode.

`Lit` is best when the photo surface should react to scene lighting. It supports `Color`, `Metallic Strength`, `Smoothness Strength`, a `Metallic/Smoothness Mask` texture, and a `Normal Map` detail texture with its own `Tiling/Offset`.

With these shaders, `Contain` mode no longer stretches edge pixels outward. It fills the empty area with `Contain Background Color`.

If an image appears rotated incorrectly, adjust this field on the same component:

```text
Photo Rotation
```

The slider range is `0-360` degrees. Download debug logs can be enabled with `Debug Download Logs`.

### FAQ

#### Why Did Image Probing Fail?

Possible reasons:

- The URL is not a direct image link.
- The image server blocks browser loading.
- The request timed out.
- The image host blocks cross-origin access.

If probing fails, you can still set `Orientation` manually.

#### Why Does Unity Not Pick One Of My Images?

Check:

- The URL starts with `http://` or `https://`.
- You baked the JSON again in Unity.
- Landscape frames only pick `Landscape`; portrait frames only pick `Portrait`.

#### Do Tags Enter The Game?

No.  
`Tags` are only for filtering and organizing inside the web tool. Unity bake ignores them.

#### Does Metadata Enter The Game?

No.  
`Metadata` is only for orientation detection and cache-friendly probing in the web tool. Unity bake ignores it.
