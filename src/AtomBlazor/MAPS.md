# World map data

`world.json` is derived from Natural Earth v5.1.2, Admin 0 Countries at 1:50m scale.

- Source: https://github.com/nvkelso/natural-earth-vector/blob/v5.1.2/geojson/ne_50m_admin_0_countries.geojson
- License: public domain, https://www.naturalearthdata.com/about/terms-of-use/
- Rebuild: regenerate `world.json` from the Natural Earth source GeoJSON (round coordinates to four decimals, keep English names and ISO aliases, exclude Antarctica).

The build retains polygon geometry, rounds coordinates to four decimal places,
keeps English names and country-code aliases, and excludes Antarctica. There are
241 countries and regions. Small islands absent from the source are also absent
here. This is an overview visualization, not a detailed boundary reference.
France and Norway use Natural Earth's ISO fallback codes; Kosovo accepts XK/KOS.
Regions without an ISO code are matched by their English names.
