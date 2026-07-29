#!/usr/bin/env bash
set -euo pipefail

version="${1:?usage: install-vintage-story.sh VERSION DESTINATION}"
destination="${2:?usage: install-vintage-story.sh VERSION DESTINATION}"

if [[ ! "${version}" =~ ^[0-9]+\.[0-9]+\.[0-9]+([.-][0-9A-Za-z.-]+)?$ ]]; then
    printf 'Invalid Vintage Story version: %s\n' "${version}" >&2
    exit 1
fi

if [[ -f "${destination}/VintagestoryAPI.dll" ]]; then
    printf 'Vintage Story %s is already installed at %s\n' "${version}" "${destination}"
    exit 0
fi

archive="$(mktemp)"
trap 'rm -f "${archive}"' EXIT
url="https://cdn.vintagestory.at/gamefiles/stable/vs_server_linux-x64_${version}.tar.gz"

mkdir -p "${destination}"
printf 'Downloading Vintage Story server %s\n' "${version}"
curl --fail --location --retry 3 --silent --show-error --output "${archive}" "${url}"
tar -xzf "${archive}" -C "${destination}"
test -f "${destination}/VintagestoryAPI.dll"

