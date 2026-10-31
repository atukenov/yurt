# Yurt brand

The Yurt logo is a yurt reduced to plain geometry: a half-circle roof on a square base, with the shanyrak opening at the top, a felt band between roof and walls, and a door. It is one flat colour, so it works at any size and in a single colour. Idea: a simple, solid home every cafe builds on.

## Files

| File | Use |
| --- | --- |
| `yurt-mark.svg` | The mark alone in carpet red. Works on light and dark backgrounds. |
| `yurt-mark-felt.svg` | The mark in felt, for red or dark backgrounds (app icon). |
| `yurt-mark-mono.svg` | One colour (`currentColor`), for receipts, stamps, engraving. |
| `yurt-logo-light.svg` | Mark + "Yurt" wordmark for light backgrounds. |
| `yurt-logo-dark.svg` | Mark + wordmark for dark backgrounds. |
| `yurt-app-icon.svg` | Square app icon: the felt mark on carpet red. |
| `png/` | Ready PNGs (mark 512, logos 1200 wide, app icon 1024, favicons). |
| `app-assets/` | Sources for `@capacitor/assets` (icon, adaptive icon layers, light and dark splash). |

The wordmark is lowercase Unbounded SemiBold converted to outlines, so the SVGs need no font.

## Colours

| Name | Hex | Comes from | Used for |
| --- | --- | --- | --- |
| Felt | `#F5EFE4` | white felt cover | page background, the mark on the app icon |
| Wood | `#4A2F1E` | shanyrak and roof poles | wordmark, app accent colour |
| Carpet red | `#B83A2B` | tekemet and syrmak carpets | the mark, app icon background, buttons (white text, contrast 5.7) |
| Steppe turquoise | `#1F7A74` | tiles, sky over the steppe | second colour, links, tags (use `#1B6B65` for text on felt) |
| Sun gold | `#E3A93B` | steppe sun, brass details | small accents only, never body text on light |
| Night | `#1C1917` | evening inside the yurt | dark mode background |

The apps' default brand is Carpet red with Wood as accent (`shared-ui` theme `brand.css`, `BrandService`, backend `BrandDto.Default`). A cafe with its own brand keeps its own colours.

Fonts: Unbounded for headings and the wordmark, Golos Text for body text (both cover Kazakh and Russian).

## Regenerating the pictures

After changing an SVG:

```sh
NODE_PATH="$(npm root -g)" node brand/render.mjs   # needs Playwright
cp brand/app-assets/*.png frontend/assets/
cd frontend && npx @capacitor/assets@3.0.5 generate --android --ios \
  --iconBackgroundColor '#B83A2B' --iconBackgroundColorDark '#B83A2B' \
  --splashBackgroundColor '#F5EFE4' --splashBackgroundColorDark '#1C1917'
```

Run the last command in `frontend/admin-native` too (copy the same files to its `assets/` folder first). `render.mjs` also writes the apps' `logo.png` and `favicon.ico` and the landing page's favicons.
