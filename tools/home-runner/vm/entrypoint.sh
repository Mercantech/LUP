#!/bin/bash
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

new_disk=0
if [ ! -s /vm/guest.qcow2 ]; then
    new_disk=1
    rm -f /vm/guest.qcow2.new
    cp --sparse=always /opt/lup-vm/base.qcow2 /vm/guest.qcow2.new
    chmod 0600 /vm/guest.qcow2.new
    qemu-img resize /vm/guest.qcow2.new 32G
    cp /usr/share/OVMF/OVMF_VARS_4M.fd /vm/ovmf-vars.fd
fi

if [ ! -s /vm/seed.iso ] || [ -n "${LUP_VM_RESEED_ID:-}" ]; then
    : "${LUP_VM_USER_DATA_B64:?Cloud-init data is required for provisioning}"
    instance_id="${LUP_VM_RESEED_ID:-lup-home-worker-v1}"
    case "$instance_id" in *[!a-z0-9-]*) echo 'Invalid guest instance id.' >&2; exit 1;; esac
    printf '%s' "$LUP_VM_USER_DATA_B64" | base64 -d > /vm/user-data
    printf 'instance-id: %s\nlocal-hostname: lup-home-worker\n' "$instance_id" > /vm/meta-data
    rm -f /vm/seed.iso.new
    cloud-localds /vm/seed.iso.new /vm/user-data /vm/meta-data
    mv /vm/seed.iso.new /vm/seed.iso
    rm -f /vm/user-data /vm/meta-data
fi

if [ "$new_disk" = 1 ]; then
    mv /vm/guest.qcow2.new /vm/guest.qcow2
fi

rm -f /vm/qmp.sock
network_restriction=on
host_forward=127.0.0.1:18443
set --
if [ "${LUP_VM_BOOTSTRAP:-0}" = 1 ]; then
    network_restriction=off
    host_forward=:8443
    set -- -serial file:/vm/bootstrap-console.log
fi

vm=(qemu-system-x86_64
    -nodefaults -no-user-config -display none -serial stdio "$@" -monitor none \
    -machine q35,accel=kvm -cpu host -smp 2 -m 4096 \
    -drive if=pflash,format=raw,readonly=on,file=/usr/share/OVMF/OVMF_CODE_4M.fd \
    -drive if=pflash,format=raw,file=/vm/ovmf-vars.fd \
    -drive if=virtio,format=qcow2,file=/vm/guest.qcow2,cache=none,discard=unmap \
    -drive if=virtio,format=raw,readonly=on,file=/vm/seed.iso \
    -nic "user,model=virtio-net-pci,restrict=$network_restriction,hostfwd=tcp:$host_forward-:8443" \
    -qmp unix:/vm/qmp.sock,server=on,wait=off \
    -sandbox on,obsolete=deny,elevateprivileges=deny,spawn=deny,resourcecontrol=deny)

if [ "$network_restriction" = off ]; then
    exec "${vm[@]}"
fi

# Give QEMU a local forwarding peer even when the VM has no external route.
"${vm[@]}" &
vm_pid=$!
proxy_pid=
socat TCP4-LISTEN:8443,bind=0.0.0.0,reuseaddr,fork,max-children=24 \
    TCP4:127.0.0.1:18443,connect-timeout=3 &
proxy_pid=$!

stop_proxy() {
    kill "$proxy_pid" 2>/dev/null || true
    wait "$proxy_pid" 2>/dev/null || true
}

stop_vm() {
    for attempt in {1..10}; do
        if ! kill -0 "$vm_pid" 2>/dev/null; then break; fi
        if [ -S /vm/qmp.sock ] &&
            printf '%s\r\n' '{"execute":"qmp_capabilities"}' '{"execute":"system_powerdown"}' |
            socat -T 2 -t 1 - UNIX-CONNECT:/vm/qmp.sock >/dev/null 2>&1; then
            break
        fi
        sleep 0.5
    done
    for attempt in {1..30}; do
        if ! kill -0 "$vm_pid" 2>/dev/null; then break; fi
        sleep 1
    done
    kill "$vm_pid" 2>/dev/null || true
    wait "$vm_pid" 2>/dev/null || true
}

shutdown() {
    trap '' TERM INT
    stop_proxy
    stop_vm
    exit 0
}
trap shutdown TERM INT

status=0
wait -n -p finished "$vm_pid" "$proxy_pid" || status=$?
if [ "${finished:-}" = "$vm_pid" ]; then
    stop_proxy
    exit "$status"
fi

stop_vm
echo 'The private Hjem.exe proxy stopped.' >&2
exit 1
