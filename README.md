# Papermaking: Papyrus

A Vintage Story mod that models papyrus as crossed, pressed pith strips. The
first playable slice turns harvested papyrus tops into dry strips with a knife,
then conditions complete eight-strip batches in a sealed water barrel.

## Development

The build targets the currently installed Vintage Story 1.22.x assemblies.
Override `VINTAGE_STORY` when the game is installed elsewhere.

```sh
just validate
just install
just release
```

`just validate` is the clean-checkout definition of done: formatting, ordinary
tests, Atlas game scenarios, and a verified release archive.

