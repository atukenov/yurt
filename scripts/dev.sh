#!/usr/bin/env bash
# Run the whole Yurt platform locally: Postgres + API in Docker, customer and admin apps with `ng serve`.
#
#   ./scripts/dev.sh            start everything (default)
#   ./scripts/dev.sh stop       stop the apps and the containers
#   ./scripts/dev.sh reset      stop and delete the local database
#   ./scripts/dev.sh logs       follow API logs
#   ./scripts/dev.sh --no-frontend   only Postgres + API
#   ./scripts/dev.sh phone      like start, but a phone on the same Wi-Fi can open the customer app too
#                               (for trying Apple Pay / Google Pay on a real phone)
#
# Needs: Docker (with compose) and Node 20+. The .NET SDK is NOT required, the API builds in Docker.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
STATE="$ROOT/.local"
cd "$ROOT"

API_URL="http://localhost:8080"
CUSTOMER_PORT=4200
ADMIN_PORT=4300

need() { command -v "$1" >/dev/null 2>&1 || { echo "error: '$1' is required but not installed" >&2; exit 1; }; }

stop_frontend() {
  for f in customer admin; do
    if [[ -f "$STATE/$f.pid" ]]; then
      pid="$(cat "$STATE/$f.pid")"
      # ng serve spawns children; kill the whole process group
      kill -- "-$pid" 2>/dev/null || kill "$pid" 2>/dev/null || true
      rm -f "$STATE/$f.pid"
    fi
  done
}

# Start a command in the background in its own process group, so stop_frontend can kill it together
# with the children it spawns. `set -m` (job control) does this in any bash, including the old bash 3.2
# on macOS, which has no `setsid` command.
start_bg() { # name, command...
  local name="$1"; shift
  (cd "$ROOT/frontend" || exit 1; set -m; "$@" </dev/null >"$STATE/$name.log" 2>&1 & echo $! >"$STATE/$name.pid")
}

wait_for() { # url, label, tries
  local url="$1" label="$2" tries="${3:-90}"
  for ((i = 0; i < tries; i++)); do
    if curl -fsS -o /dev/null "$url" 2>/dev/null; then echo "  ✓ $label is up"; return 0; fi
    sleep 2
  done
  echo "  ✗ $label did not come up in time (see $STATE/*.log or './scripts/dev.sh logs')" >&2
  return 1
}

cmd="${1:-start}"
case "$cmd" in
  stop)
    stop_frontend
    docker compose down
    exit 0 ;;
  reset)
    stop_frontend
    docker compose down -v
    echo "Local database removed."
    exit 0 ;;
  logs)
    exec docker compose logs -f api ;;
  start | --no-frontend) ;;
  phone)
    # The computer's address on the Wi-Fi (macOS first, then Linux).
    LAN_IP="$(ipconfig getifaddr en0 2>/dev/null || ipconfig getifaddr en1 2>/dev/null || hostname -I 2>/dev/null | awk '{print $1}' || true)"
    [[ -n "$LAN_IP" ]] || { echo "error: could not find this computer's Wi-Fi address" >&2; exit 1; }
    export YURT_PHONE_ORIGIN="http://$LAN_IP:$CUSTOMER_PORT" ;;
  *) echo "usage: $0 [start|stop|reset|logs|--no-frontend|phone]" >&2; exit 1 ;;
esac

need docker
docker compose version >/dev/null 2>&1 || { echo "error: 'docker compose' plugin is required" >&2; exit 1; }
docker info >/dev/null 2>&1 || { echo "error: the Docker daemon is not running" >&2; exit 1; }

mkdir -p "$STATE"

echo "▶ Starting Postgres + API (first run builds the API image, this takes a few minutes)…"
docker compose up -d --build

echo "▶ Waiting for the API…"
wait_for "$API_URL/health" "API ($API_URL)" 120 || { docker compose logs --tail 40 api >&2; exit 1; }

if [[ "$cmd" != "--no-frontend" ]]; then
  need node
  need npm
  if [[ ! -d "$ROOT/frontend/node_modules" ]]; then
    echo "▶ Installing frontend dependencies…"
    (cd frontend && npm ci)
  fi

  stop_frontend
  echo "▶ Starting the customer app and the admin panel…"
  CUSTOMER_HOST=localhost
  [[ "$cmd" == "phone" ]] && CUSTOMER_HOST=0.0.0.0
  start_bg customer npx ng serve yurt-customer --configuration local --host "$CUSTOMER_HOST" --port "$CUSTOMER_PORT"
  start_bg admin npx ng serve yurt-admin --configuration local --port "$ADMIN_PORT"
  wait_for "http://localhost:$CUSTOMER_PORT" "customer app" 120 || tail -n 20 "$STATE/customer.log" >&2 || true
  wait_for "http://localhost:$ADMIN_PORT" "admin panel" 120 || tail -n 20 "$STATE/admin.log" >&2 || true
fi

cat <<EOF

Yurt is running locally:
  Customer app  http://localhost:$CUSTOMER_PORT
  Admin panel   http://localhost:$ADMIN_PORT   (cafe admin: admin / admin12345; platform operator: superadmin / superadmin12345; both must change the password at first login)
  API           $API_URL   (Swagger: $API_URL/swagger)
  Postgres      localhost:5432  (postgres / postgres, db YurtDb)

Stop with ./scripts/dev.sh stop, wipe the database with ./scripts/dev.sh reset.
EOF
if [[ "$cmd" == "phone" ]]; then
  echo "On a phone on the same Wi-Fi, open the customer app at $YURT_PHONE_ORIGIN"
fi
