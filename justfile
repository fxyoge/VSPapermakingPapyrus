export VINTAGE_STORY := env_var_or_default("VINTAGE_STORY", "/workspaces/vintage-story-Game/active/files/extra/vintagestory")

build:
    dotnet build PapermakingPapyrus.slnx

format:
    dotnet format PapermakingPapyrus.slnx

lint:
    dotnet format PapermakingPapyrus.slnx --verify-no-changes

test:
    dotnet test PapermakingPapyrus.slnx

validate: lint test release

install: build
    #!/usr/bin/env bash
    set -euo pipefail
    destination="${VINTAGE_STORY_DATA:-/data/vintagestory}/Mods/papermakingpapyrus"
    mkdir -p "${destination}"
    cp -a src/PapermakingPapyrus/bin/Debug/. "${destination}/"
    printf 'Installed development build to %s\n' "${destination}"

release:
    #!/usr/bin/env bash
    set -euo pipefail
    version="$(jq -r .version src/PapermakingPapyrus/modinfo.json)"
    output="dist/papermakingpapyrus_${version}.zip"
    mkdir -p dist
    dotnet build src/PapermakingPapyrus/PapermakingPapyrus.csproj -c Release
    rm -f "${output}"
    (cd src/PapermakingPapyrus/bin/Release && zip -qr "../../../../${output}" \
        papermakingpapyrus.dll \
        papermakingpapyrus.deps.json \
        papermakingpapyrus.pdb \
        modinfo.json \
        assets)
    unzip -tq "${output}"
    printf 'Wrote %s\n' "${output}"

clean:
    dotnet clean PapermakingPapyrus.slnx

