#!/usr/bin/env bash
set -euo pipefail
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
exec >>/var/log/lup-home-runner-bootstrap.log 2>&1
signal() {
    printf '%s\n' "$*" >&3
    if [[ -c /dev/ttyS0 ]]; then printf '%s\n' "$*" >/dev/ttyS0 || true; fi
}
stage=packages
trap 'signal "LUP_HOME_WORKER_FAILED $stage"' ERR
signal "LUP_HOME_WORKER_STAGE $stage"

export DEBIAN_FRONTEND=noninteractive
apt-get -o DPkg::Lock::Timeout=180 update -q
apt-get -o DPkg::Lock::Timeout=180 install -yq --no-install-recommends \
    ca-certificates curl docker.io git gnupg nginx openssl python3
openssl pkey -pubin -in "$handoff" -noout

stage=gvisor
signal "LUP_HOME_WORKER_STAGE $stage"
install -d -m 0755 /etc/apt/keyrings
archive_key=$(mktemp)
curl -fsSL --retry 3 --max-time 45 https://gvisor.dev/archive.key -o "$archive_key"
gpg --batch --yes --dearmor --output /etc/apt/keyrings/gvisor.gpg "$archive_key"
rm -f "$archive_key"
chmod 0644 /etc/apt/keyrings/gvisor.gpg
printf 'deb [arch=%s signed-by=/etc/apt/keyrings/gvisor.gpg] https://storage.googleapis.com/gvisor/releases release main\n' \
    "$(dpkg --print-architecture)" >/etc/apt/sources.list.d/gvisor.list
chmod 0644 /etc/apt/sources.list.d/gvisor.list
apt-get -o DPkg::Lock::Timeout=180 update -q
apt-get -o DPkg::Lock::Timeout=180 install -yq --no-install-recommends runsc
runsc install
systemctl enable docker
systemctl restart docker
docker info --format '{{json .Runtimes}}' | grep -q '"runsc"'

stage=images
signal "LUP_HOME_WORKER_STAGE $stage"
source_dir=/opt/lup-home-worker-source
install -d -m 0700 "$source_dir"
git -C "$source_dir" init -q
git -C "$source_dir" fetch --quiet --depth=1 https://github.com/Mercantech/LUP.git "$source_ref"
git -C "$source_dir" checkout --quiet --detach FETCH_HEAD
test "$(git -C "$source_dir" rev-parse HEAD)" = "$source_ref"
src="$source_dir/tools/home-runner"
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

runner_env=/etc/lup-home-runner.env
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

tls=/etc/lup-home-runner/tls
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

stage=deployed-tests
signal "LUP_HOME_WORKER_STAGE $stage"
if ! grep -qE '^127[.]0[.]0[.]1[[:space:]]+lup-home-worker$' /etc/hosts; then
    printf '127.0.0.1 lup-home-worker\n' >>/etc/hosts
fi
HOME_RUNNER_URL=https://lup-home-worker:8443 HOME_RUNNER_TOKEN="$runner_token" SSL_CERT_FILE="$tls/cert.pem" \
    python3 "$src/verify.py" DeployedRunnerTests

fingerprint=$(openssl x509 -in "$tls/cert.pem" -noout -fingerprint -sha256 | cut -d= -f2 | tr -d ':')
[[ "$fingerprint" =~ ^[0-9A-F]{64}$ ]]
envelope=$(printf '%s' "$runner_token" | openssl pkeyutl -encrypt -pubin -inkey "$handoff" \
    -pkeyopt rsa_padding_mode:oaep -pkeyopt rsa_oaep_md:sha256 -pkeyopt rsa_mgf1_md:sha256 | openssl base64 -A)
signal "LUP_HOME_WORKER_SECRET_ENVELOPE $envelope"
signal "LUP_HOME_WORKER_READY $fingerprint"
