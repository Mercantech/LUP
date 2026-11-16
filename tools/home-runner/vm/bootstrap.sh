#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

if [[ "$(id -u)" != 0 ]] || ! systemd-detect-virt --vm --quiet; then
    echo "This bootstrap must run as root inside the dedicated worker VM." >&2
    exit 1
fi

source_ref="${LUP_HOME_SOURCE_REF:-d5628a587c3b61151f3a9fbf424f4830736d3616}"
slots="${LUP_HOME_WORKER_SLOTS:-2}"
handoff=/etc/lup-home-runner-handoff.pem
if [[ ! "$source_ref" =~ ^[0-9a-f]{40}$ || ! "$slots" =~ ^[1-4]$ ]] || ! test -r "$handoff"; then
    echo "A pinned LUP source revision, 1–4 runner slots and a public handoff key are required." >&2
    exit 2
fi

exec 3>&1
bootstrap_log=/var/log/lup-home-runner-bootstrap.log
exec >>"$bootstrap_log" 2>&1
signal() {
    printf '%s\n' "$*" >&3
}
bootstrap_failed() {
    local code=$?
    trap - ERR
    signal "LUP_HOME_WORKER_FAILED $stage exit=$code"
    case "$stage" in
        package-*|gvisor-*)
            while IFS= read -r line; do signal "LUP_HOME_WORKER_DIAG $line"; done \
                < <(tail -24 "$bootstrap_log" | tr -d '\r')
            ;;
    esac
    exit "$code"
}

stage=package-recovery
trap bootstrap_failed ERR
source_dir=/opt/lup-home-worker-source
src="$source_dir/tools/home-runner"
runner_env=/etc/lup-home-runner.env
tls=/etc/lup-home-runner/tls

sync_source() {
    install -d -m 0700 "$source_dir"
    git -C "$source_dir" init -q
    git -C "$source_dir" fetch --quiet --depth=1 https://github.com/Mercantech/LUP.git "$source_ref"
    git -C "$source_dir" checkout --quiet --detach FETCH_HEAD
    test "$(git -C "$source_dir" rev-parse HEAD)" = "$source_ref"
}

verify_runner() {
    local token fingerprint envelope results test_status diagnostic
    stage=deployed-tests
    signal "LUP_HOME_WORKER_STAGE $stage"
    token=$(sed -n 's/^HOME_RUNNER_TOKEN=//p' "$runner_env")
    [[ "$token" =~ ^[0-9a-f]{64}$ ]]
    if ! grep -qE '^127[.]0[.]0[.]1[[:space:]]+lup-home-worker$' /etc/hosts; then
        printf '127.0.0.1 lup-home-worker\n' >>/etc/hosts
    fi
    results=$(mktemp)
    HOME_RUNNER_URL=https://lup-home-worker:8443 HOME_RUNNER_TOKEN="$token" SSL_CERT_FILE="$tls/cert.pem" \
        python3 "$src/verify.py" DeployedRunnerTests >"$results" 2>&1 && test_status=0 || test_status=$?
    cat "$results"
    while IFS= read -r diagnostic; do signal "LUP_HOME_WORKER_TEST $diagnostic"; done < <(
        sed -nE 's/^((ERROR|FAIL): test_[a-z_]+).*/\1/p; s/^Ran ([0-9]+) tests? in .*/Ran \1 tests/p' "$results"
        grep -oE 'HTTP Error [0-9]{3}|CERTIFICATE_VERIFY_FAILED|Connection refused|Connection reset by peer|Name or service not known|Temporary failure in name resolution|TimeoutError|KeyError|JSONDecodeError|COMPILE_ERROR|RUN_TIMEOUT|RUN_EXPIRED|RUN_CANCELLED|RUN_INVALID|WORKER_BUSY|WORKER_UNAVAILABLE|CONTAINER_EXITED' "$results" | sort -u
    )
    rm -f "$results"
    if ((test_status != 0)); then return "$test_status"; fi

    fingerprint=$(openssl x509 -in "$tls/cert.pem" -noout -fingerprint -sha256 | cut -d= -f2 | tr -d ':')
    [[ "$fingerprint" =~ ^[0-9A-F]{64}$ ]]
    envelope=$(printf '%s' "$token" | openssl pkeyutl -encrypt -pubin -inkey "$handoff" \
        -pkeyopt rsa_padding_mode:oaep -pkeyopt rsa_oaep_md:sha256 -pkeyopt rsa_mgf1_md:sha256 | openssl base64 -A)
    signal "LUP_HOME_WORKER_SECRET_ENVELOPE $envelope"
    signal "LUP_HOME_WORKER_READY $fingerprint"
}

if [[ "${1:-}" == --verify-only ]]; then
    stage=verification-source
    signal "LUP_HOME_WORKER_STAGE $stage"
    sync_source
    verify_runner
    exit 0
fi

signal "LUP_HOME_WORKER_STAGE $stage"
export DEBIAN_FRONTEND=noninteractive NEEDRESTART_MODE=l UCF_FORCE_CONFFOLD=1
apt_options=(
    -o DPkg::Lock::Timeout=45 -o Acquire::Retries=2
    -o Acquire::http::Timeout=20 -o Acquire::https::Timeout=20
    -o Dpkg::Use-Pty=0 -o Dpkg::Options::=--force-confold
)
# Remove the repository left by the earlier APT bootstrap. The worker uses
# the pinned upstream gVisor tarball so this update only contacts Ubuntu.
rm -f /etc/apt/sources.list.d/gvisor.list
timeout --kill-after=15s 240s dpkg --force-confold --configure -a </dev/null

stage=package-update
signal "LUP_HOME_WORKER_STAGE $stage"
timeout --kill-after=15s 240s apt-get "${apt_options[@]}" update -q </dev/null
stage=package-install
signal "LUP_HOME_WORKER_STAGE $stage"
timeout --kill-after=15s 360s apt-get "${apt_options[@]}" install -yq --no-install-recommends \
    bzip2 ca-certificates curl docker.io git nginx openssl python3 </dev/null
openssl pkey -pubin -in "$handoff" -noout

stage=gvisor-download
signal "LUP_HOME_WORKER_STAGE $stage"
test "$(uname -m)" = x86_64
gvisor_archive=$(mktemp --suffix=.tar.bz2)
curl -fsSL --connect-timeout 15 --max-time 240 --retry 2 --retry-max-time 300 \
    https://storage.googleapis.com/gvisor/releases/release/20261109.0/x86_64/gvisor.tar.bz2 -o "$gvisor_archive"
printf '%s  %s\n' \
    1a6b8f74c37b4931ef473f67ec1f601690855bedc52ac709f2fac965c69677aa0426ef44598d8eee1aed72cfdb9df87a2a5f352cb3348880fc61a7732bc10f2a \
    "$gvisor_archive" | sha512sum --check --strict
stage=gvisor-install
signal "LUP_HOME_WORKER_STAGE $stage"
tar --no-same-owner -xjf "$gvisor_archive" -C /usr/local/bin
rm -f "$gvisor_archive"
timeout --kill-after=5s 30s /usr/local/bin/runsc install
stage=gvisor-docker
signal "LUP_HOME_WORKER_STAGE $stage"
timeout --kill-after=10s 75s systemctl enable docker
timeout --kill-after=10s 75s systemctl restart docker
docker info --format '{{json .Runtimes}}' | grep -q '"runsc"'

stage=images
signal "LUP_HOME_WORKER_STAGE $stage"
sync_source
for runtime in python csharp arduino; do
    signal "LUP_HOME_WORKER_STAGE build-$runtime"
    docker build --target "$runtime" --tag "lup-home-$runtime:sdk1" "$src"
done

stage=services
signal "LUP_HOME_WORKER_STAGE $stage"
if ! id lup-home-runner >/dev/null 2>&1; then
    useradd --system --no-create-home --shell /usr/sbin/nologin lup-home-runner
fi
install -d -m 0755 /opt/lup-home-runner
install -m 0644 "$src/worker.py" /opt/lup-home-runner/worker.py
install -m 0644 "$src/lup-home-runner.service" /etc/systemd/system/lup-home-runner.service

if [[ -s "$runner_env" ]]; then
    runner_token=$(sed -n 's/^HOME_RUNNER_TOKEN=//p' "$runner_env")
    [[ "$runner_token" =~ ^[0-9a-f]{64}$ ]]
else
    runner_token=$(openssl rand -hex 32)
fi
printf 'HOME_RUNNER_TOKEN=%s\nHOME_RUNNER_CAPACITY=%s\nHOME_RUNNER_BIND=127.0.0.1\nHOME_RUNNER_PORT=8096\n' \
    "$runner_token" "$slots" >"$runner_env"
chmod 0600 "$runner_env"
systemctl daemon-reload
systemctl enable lup-home-runner
systemctl restart lup-home-runner

install -d -m 0700 "$tls"
if [[ ! -s "$tls/key.pem" || ! -s "$tls/cert.pem" ]]; then
    openssl req -x509 -newkey rsa:3072 -nodes -sha256 -days 365 \
        -subj /CN=lup-home-worker -addext subjectAltName=DNS:lup-home-worker \
        -addext basicConstraints=critical,CA:FALSE -addext extendedKeyUsage=serverAuth \
        -keyout "$tls/key.pem" -out "$tls/cert.pem"
fi
chmod 0600 "$tls/key.pem"
cat >/etc/nginx/conf.d/lup-home-runner.conf <<'NGINX'
server {
    listen 8443 ssl;
    server_name lup-home-worker;
    ssl_certificate /etc/lup-home-runner/tls/cert.pem;
    ssl_certificate_key /etc/lup-home-runner/tls/key.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    client_max_body_size 256k;
    access_log off;

    location / {
        proxy_pass http://127.0.0.1:8096;
        proxy_http_version 1.1;
        proxy_set_header Connection "";
        proxy_connect_timeout 3s;
        proxy_read_timeout 45s;
    }
}
NGINX
chmod 0644 /etc/nginx/conf.d/lup-home-runner.conf
if [[ -L /etc/nginx/sites-enabled/default ]]; then rm /etc/nginx/sites-enabled/default; fi
nginx -t
systemctl enable nginx
systemctl restart nginx

verify_runner
