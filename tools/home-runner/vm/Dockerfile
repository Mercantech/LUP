FROM debian:bookworm-slim

ARG UBUNTU_CLOUD_RELEASE=https://cloud-images.ubuntu.com/minimal/releases/noble/release-20261105
ARG UBUNTU_CLOUD_SIGNER=D2EB44626FDDC30B513D5BB71A5D6C4C7DB87C81

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates cloud-image-utils curl gnupg ovmf qemu-system-x86 qemu-utils socat util-linux \
    && rm -rf /var/lib/apt/lists/*

RUN set -eu; \
    install -d /opt/lup-vm; \
    work="$(mktemp -d)"; \
    cd "$work"; \
    image=ubuntu-24.04-minimal-cloudimg-amd64.img; \
    for file in "$image" SHA256SUMS SHA256SUMS.gpg; do \
        curl --fail --silent --show-error --location --retry 3 "$UBUNTU_CLOUD_RELEASE/$file" -o "$file"; \
    done; \
    export GNUPGHOME="$work/gnupg"; \
    install -m 0700 -d "$GNUPGHOME"; \
    curl --fail --silent --show-error --get \
        --data-urlencode op=get --data-urlencode "search=0x$UBUNTU_CLOUD_SIGNER" \
        'https://keyserver.ubuntu.com/pks/lookup' -o canonical.asc; \
    gpg --batch --import canonical.asc; \
    gpg --batch --status-fd 1 --verify SHA256SUMS.gpg SHA256SUMS > signature.status; \
    awk -v expected="$UBUNTU_CLOUD_SIGNER" \
        '$2=="VALIDSIG" && ($3==expected || $NF==expected) { good=1 } END { exit !good }' signature.status; \
    awk -v image="$image" '$2==image || $2=="*"image { print; count++ } END { exit count!=1 }' \
        SHA256SUMS > image.sha256; \
    sha256sum --check image.sha256; \
    chmod 0444 "$image"; \
    mv "$image" /opt/lup-vm/base.qcow2; \
    cd /; \
    rm -rf "$work"

RUN install -o 65532 -g 65532 -m 0700 -d /vm \
    && touch /vm/.lup-home-vm-volume \
    && chown 65532:65532 /vm/.lup-home-vm-volume

COPY --chmod=0755 entrypoint.sh /usr/local/bin/lup-home-vm
USER 65532:65532
ENTRYPOINT ["/usr/local/bin/lup-home-vm"]
