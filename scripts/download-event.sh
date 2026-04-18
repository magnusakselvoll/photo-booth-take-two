#!/bin/bash
set -euo pipefail

usage() {
    echo "Usage: $0 <base-url> <output-folder>" >&2
    echo "" >&2
    echo "  base-url       Base URL of the photo booth server (e.g. http://192.168.1.42:5192)" >&2
    echo "  output-folder  Directory where photos will be saved (created if it does not exist)" >&2
    echo "" >&2
    echo "Example:" >&2
    echo "  $0 http://192.168.1.42:5192 ~/Desktop/birthday-photos" >&2
    exit 2
}

if [[ $# -ne 2 ]]; then
    usage
fi

base="${1%/}"
out="$2"

check_dep() {
    if ! command -v "$1" &>/dev/null; then
        echo "Error: '$1' is required but not found on PATH." >&2
        echo "Install it with: $2" >&2
        exit 1
    fi
}

check_dep curl "brew install curl  (macOS)  |  apt install curl  (Debian/Ubuntu)"
check_dep jq   "brew install jq    (macOS)  |  apt install jq    (Debian/Ubuntu)"

echo "Checking server at $base ..."
if ! curl --fail --silent --show-error --max-time 10 "$base/api/photos/" -o /dev/null; then
    echo "Error: could not reach $base/api/photos/ — check the URL and that the server is running." >&2
    exit 1
fi

mkdir -p "$out"

echo "Fetching photo list ..."
photos_json=$(curl --fail --silent --show-error --retry 3 --retry-all-errors --retry-delay 2 --max-time 30 "$base/api/photos/")

total=$(echo "$photos_json" | jq 'length')

if [[ "$total" -eq 0 ]]; then
    echo "No photos found on the server."
    exit 0
fi

echo "Found $total photo(s). Saving to $out ..."

downloaded=0
skipped=0
failed=0
n=0

while IFS=$'\t' read -r id code; do
    n=$((n + 1))
    target="$out/$(printf '%05d' "$code").jpg"

    if [[ -s "$target" ]]; then
        echo "[$n/$total] skip     $(basename "$target")"
        skipped=$((skipped + 1))
        continue
    fi

    tmp="$target.tmp"
    if curl --fail --retry 5 --retry-all-errors --retry-delay 2 --max-time 60 \
            --silent --show-error \
            "$base/api/photos/$id/image" -o "$tmp"; then
        mv "$tmp" "$target"
        echo "[$n/$total] downloaded $(basename "$target")"
        downloaded=$((downloaded + 1))
    else
        rm -f "$tmp"
        echo "[$n/$total] FAILED   $(basename "$target")  (id=$id)" >&2
        failed=$((failed + 1))
    fi
done < <(echo "$photos_json" | jq -r '.[] | "\(.id)\t\(.code)"')

echo ""
echo "Done. downloaded=$downloaded  skipped=$skipped  failed=$failed"

if [[ "$failed" -gt 0 ]]; then
    exit 1
fi
