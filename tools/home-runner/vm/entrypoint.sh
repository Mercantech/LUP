#!/bin/sh
set -eu
umask 077

test -c /dev/kvm && test -r /dev/kvm && test -w /dev/kvm
test -f /vm/.lup-home-vm-volume

# The same guest disk must never be open by two VMM containers.
exec 9>/vm/disk.lock
if ! flock -n 9; then
    echo 'The Hjem.exe guest disk is already in use.' >&2
    exit 1
fi

if [ ! -s /vm/guest.qcow2 ]; then
    : "${LUP_VM_USER_DATA_B64:?Cloud-init data is required for the first boot}"
    rm -f /vm/guest.qcow2.new
    cp --sparse=always /opt/lup-vm/base.qcow2 /vm/guest.qcow2.new
    chmod 0600 /vm/guest.qcow2.new
    qemu-img resize /vm/guest.qcow2.new 32G
    cp /usr/share/OVMF/OVMF_VARS_4M.fd /vm/ovmf-vars.fd
    printf '%s' "$LUP_VM_USER_DATA_B64" | base64 -d > /vm/user-data
    printf 'instance-id: lup-home-worker-v1\nlocal-hostname: lup-home-worker\n' > /vm/meta-data
    cloud-localds /vm/seed.iso /vm/user-data /vm/meta-data
    rm -f /vm/user-data /vm/meta-data
    mv /vm/guest.qcow2.new /vm/guest.qcow2
fi

rm -f /vm/qmp.sock
network_restriction=on
if [ "${LUP_VM_BOOTSTRAP:-0}" = 1 ]; then
    network_restriction=off
fi

exec qemu-system-x86_64 \
    -nodefaults -no-user-config -display none -serial stdio -monitor none \
    -machine q35,accel=kvm -cpu host -smp 2 -m 4096 \
    -drive if=pflash,format=raw,readonly=on,file=/usr/share/OVMF/OVMF_CODE_4M.fd \
    -drive if=pflash,format=raw,file=/vm/ovmf-vars.fd \
    -drive if=virtio,format=qcow2,file=/vm/guest.qcow2,cache=none,discard=unmap \
    -drive if=virtio,format=raw,readonly=on,file=/vm/seed.iso \
    -nic "user,model=virtio-net-pci,restrict=$network_restriction,hostfwd=tcp::8443-:8443" \
    -qmp unix:/vm/qmp.sock,server=on,wait=off \
    -sandbox on,obsolete=deny,elevateprivileges=deny,spawn=deny,resourcecontrol=deny
