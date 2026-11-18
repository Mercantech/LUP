#!/bin/sh
set -eu

if [ "${LUP_HOME_QA_APPROVED:-}" != "synthetic-only-20261118" ]; then
    printf '%s\n' 'Set LUP_HOME_QA_APPROVED=synthetic-only-20261118 after the staging gate.'
    exit 2
fi
: "${LUP_HOME_QA_API_IMAGE:?Supply the exact candidate API image}"
: "${LUP_HOME_QA_WEB_IMAGE:?Supply the exact candidate web image}"
qa_dir=$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)
qa_repo=$(CDPATH='' cd -- "$qa_dir/../.." && pwd)
cd "$qa_repo"
docker image inspect "$LUP_HOME_QA_API_IMAGE" >/dev/null
docker image inspect "$LUP_HOME_QA_WEB_IMAGE" >/dev/null
docker network inspect lup-home-runner-control >/dev/null
docker volume inspect lup-hjem-stage-connection-20261118 >/dev/null
if docker volume inspect stage-hjem-workshop_qa-db >/dev/null 2>&1; then
    printf '%s\n' 'A previous staging database exists. Stop and dispose that test stack before a fresh acceptance run.'
    exit 2
fi
compose() {
    docker compose --env-file /dev/null -f "$qa_dir/compose.yml" "$@"
}
compose config --quiet
compose build issuer browser
compose up -d issuer db --wait
qa_table_count=$(compose exec -T db psql -U lup_home_qa -d lup_home_qa_20261118 -v ON_ERROR_STOP=1 -t -A -c \
    "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public';")
if [ "$qa_table_count" != "0" ]; then
    printf '%s\n' 'Staging database is not empty. Stop before attaching an application.'
    exit 2
fi
compose up -d api web
compose exec -T --user nginx web sh -ec \
    'test -r /usr/share/nginx/html/appsettings.json && test -r /usr/share/nginx/html/js/home-game.js'
printf '%s\n' 'Only the private synthetic stage-hjem-workshop project was started.'
compose run --rm browser
compose exec -T db psql -U lup_home_qa -d lup_home_qa_20261118 -v ON_ERROR_STOP=1 -t -A -c \
    'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;'
compose exec -T db psql -U lup_home_qa -d lup_home_qa_20261118 -v ON_ERROR_STOP=1 -t -A -c \
    'SELECT COUNT(*) FROM quest_gold_awards;'
docker inspect "$(compose ps -q api)" --format 'Exact staging API image ID {{.Image}}'
docker inspect "$(compose ps -q web)" --format 'Exact staging web image ID {{.Image}}'
