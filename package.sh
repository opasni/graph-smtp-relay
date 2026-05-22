#!/usr/bin/env bash
set -euo pipefail

# Usage: ./package.sh <tag> <registry> [--export-archive]
# Example: ./package.sh 1.2.3 ghcr.io/myorg

TAG="${1:-}"
REGISTRY="${2:-}"
EXPORT_ARCHIVE="${3:-}"

if [[ -z "$TAG" || -z "$REGISTRY" ]]; then
  echo "Usage: $0 <tag> <registry> [--export-archive]"
  exit 1
fi

IMAGE="$REGISTRY/graph-smtp-relay:$TAG"
PLATFORMS="linux/amd64,linux/arm64"

# Build and push multi-arch image

echo "Building and pushing $IMAGE for $PLATFORMS..."
docker buildx build \
  --platform "$PLATFORMS" \
  --push \
  -t "$IMAGE" .

echo "Image pushed: $IMAGE"

docker buildx imagetools inspect "$IMAGE"

echo "Getting image digest..."
DIGEST=$(docker buildx imagetools inspect "$IMAGE" | grep Digest | head -n1 | awk '{print $2}')
echo "$IMAGE@$DIGEST" > "graph-smtp-relay_${TAG}_digest.txt"
echo "Image digest: $DIGEST"

if [[ "$EXPORT_ARCHIVE" == "--export-archive" ]]; then
  ARCHIVE="graph-smtp-relay_${TAG}_oci.tar"
  echo "Exporting OCI archive to $ARCHIVE..."
  docker buildx build \
    --platform linux/amd64 \
    -t "$IMAGE" \
    --output type=oci,dest="$ARCHIVE" .
  echo "SHA256 checksum for $ARCHIVE:" > "$ARCHIVE.sha256"
  sha256sum "$ARCHIVE" >> "$ARCHIVE.sha256"
fi

echo "Done."