#!/usr/bin/env bash
set -euo pipefail

project="src/PapermakingPapyrus/PapermakingPapyrus.csproj"
modinfo="src/PapermakingPapyrus/modinfo.json"
version="$(jq -er '.version' "${modinfo}")"
output="dist/papermakingpapyrus_${version}.zip"

dotnet build "${project}" --configuration Release -p:Version="${version}"
mkdir -p dist
rm -f "${output}"
(
    cd src/PapermakingPapyrus/bin/Release
    zip -qr "../../../../${output}" \
        papermakingpapyrus.dll \
        papermakingpapyrus.deps.json \
        papermakingpapyrus.pdb \
        modinfo.json \
        assets
)
unzip -tq "${output}"
printf 'Wrote %s\n' "${output}"
