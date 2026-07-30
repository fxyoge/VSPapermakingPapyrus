# Papermaking: Papyrus

## Development

Copy a local `.env` from `.env.example` with your `VINTAGE_STORY` configured.

`just test` - run full test suite (takes a while; integration-test heavy) 
`just test bookbinders@1.5.1` - test against one or more mods (format: `modid@version`)
`just release` - create a new release to `dist/`, drop that into your VS mod folder for local testing

## Release

* Update `modinfo.json`
* Push a new tag
