# Yurt brand

The Yurt logo is a yurt seen from the front, crowned by a shanyrak (the wooden ring at the top of every yurt, the Kazakh symbol of home and family). Idea: Yurt is the home your cafe lives in.

## Files

| File | Use |
| --- | --- |
| `yurt-mark.svg` | The mark alone, full colour. Works on light and dark backgrounds. |
| `yurt-mark-mono.svg` | One colour (`currentColor`), for receipts, stamps, engraving. |
| `yurt-logo-light.svg` | Mark + "Yurt" wordmark for light backgrounds. |
| `yurt-logo-dark.svg` | Mark + wordmark for dark backgrounds. |
| `yurt-app-icon.svg` | Square app icon: the mark on felt. |
| `png/` | Ready PNGs (mark 512, logos 1200 wide, app icon 1024, favicons). |
| `app-assets/` | Sources for `@capacitor/assets` (icon, adaptive icon layers, light and dark splash). |

The wordmark is Unbounded Bold converted to outlines, so the SVGs need no font.

## Colours

| Name | Hex | Comes from | Used for |
| --- | --- | --- | --- |
| Felt | `#F5EFE4` | white felt cover | page background, app icon background |
| Wood | `#4A2F1E` | shanyrak and roof poles | wordmark, door, app accent colour |
| Carpet red | `#B83A2B` | tekemet and syrmak carpets | main brand colour, buttons (white text, contrast 5.7) |
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
  --iconBackgroundColor '#F5EFE4' --iconBackgroundColorDark '#1C1917' \
  --splashBackgroundColor '#F5EFE4' --splashBackgroundColorDark '#1C1917'
```

Run the last command in `frontend/admin-native` too (copy the same files to its `assets/` folder first). `render.mjs` also writes the apps' `logo.png` and `favicon.ico` and the landing page's favicons.
