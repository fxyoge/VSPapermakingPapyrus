#!/usr/bin/env bash
set -euo pipefail

usage() {
    echo "Usage: $0 CACHE_DIR MODS_DIR [modid@version ...]" >&2
}

if (( $# < 2 )); then
    usage
    exit 2
fi

cache_dir="$1"
mods_dir="$2"
shift 2

mkdir -p "$cache_dir" "$mods_dir"

# This directory is staging owned by this script. Avoid carrying a mod from one
# test matrix entry into another when the same workspace is reused locally.
find "$mods_dir" -maxdepth 1 -type f -name '*.zip' -delete

for specification in "$@"; do
    if [[ "$specification" != *@* ]]; then
        echo "Invalid mod specification '$specification'; expected modid@version." >&2
        exit 2
    fi

    mod_id="${specification%@*}"
    mod_version="${specification##*@}"
    if [[ ! "$mod_id" =~ ^[a-z][a-z0-9]*$ || -z "$mod_version" ]]; then
        echo "Invalid mod specification '$specification'; expected modid@version." >&2
        exit 2
    fi

    metadata_file="$cache_dir/$mod_id.json"
    archive_file="$cache_dir/$mod_id-$mod_version.zip"

    curl --fail --location --silent --show-error \
        --retry 3 \
        --output "$metadata_file.tmp" \
        "https://mods.vintagestory.at/api/mod/$mod_id"
    mv "$metadata_file.tmp" "$metadata_file"

    release="$(
        jq --arg version "$mod_version" \
            -cer '.mod.releases[] | select(.modversion == $version)' \
            "$metadata_file" |
            head -n 1
    )"
    if [[ -z "$release" ]]; then
        echo "Vintage Story ModDB has no $mod_id release at version $mod_version." >&2
        exit 1
    fi

    download_url="$(jq -er '.mainfile' <<<"$release")"
    expected_filename="$(jq -er '.filename' <<<"$release")"
    if [[ "$expected_filename" == */* || "$expected_filename" == *\\* ]]; then
        echo "ModDB returned an unsafe filename for $specification: $expected_filename" >&2
        exit 1
    fi

    if [[ ! -f "$archive_file" ]] || ! unzip -tqq "$archive_file"; then
        curl --fail --location --silent --show-error \
            --retry 3 \
            --output "$archive_file.tmp" \
            "$download_url"
        unzip -tq "$archive_file.tmp" >/dev/null
        mv "$archive_file.tmp" "$archive_file"
    fi

    manifest="$(
        unzip -p "$archive_file" modinfo.json |
            jq -cer '{modid, version}'
    )"
    actual_id="$(jq -er '.modid' <<<"$manifest")"
    actual_version="$(jq -er '.version' <<<"$manifest")"
    if [[ "$actual_id" != "$mod_id" || "$actual_version" != "$mod_version" ]]; then
        echo "Downloaded $specification but its modinfo declares $actual_id@$actual_version." >&2
        exit 1
    fi

    cp "$archive_file" "$mods_dir/$expected_filename"
    echo "Installed $specification as $mods_dir/$expected_filename"
done
