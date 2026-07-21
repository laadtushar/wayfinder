# Per-site POI content

One JSON per site, referenced by its WorldPackage as a TextAsset. Schema:
`{ siteId, pois: [ { id, title, fact, source, placementHint, positionX?, positionZ? } ] }`.

Conventions:

- `id` is `<siteId>/<slug>` — stable forever; it ends up in players' field logs.
- `positionX/positionZ` are **site-local metres** (terrain centered on the
  origin), baked by a placement pass once the site's terrain is imported.
  A POI without both fields is unplaced and skipped at runtime with a warning.
- `(0, 0)` — exactly the player spawn — is **not a legal POI position**; the
  parser treats double-zero as "unplaced". Author positions at least a few
  metres off spawn.
- Facts carry their sources; keep the `[unverified]` flags honest.
- Authoring guidance: docs/specs/2026-07-21-terrain-and-poi-content.md.

Note on `mars-olympus/caldera-rim-drop`: the hint says "southern rim" but the
baked position is the **north** rim deliberately — from the north rim you look
*across* at the southern wall's landslide scars, which is the sight line the
fact describes.
