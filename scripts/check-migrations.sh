#!/usr/bin/env bash
# Fails when a migration added since BASE (default origin/main) drops or renames anything.
# Migrations must be additive so the previous deploy keeps working against a migrated database
# (see "Ground rules" in docs/MODULARIZATION_TODO.md).
#
# To intentionally ship a destructive change, split it: first release stops using the column,
# a later release drops it, and add `// migration-guard: allow` on the offending line.
set -euo pipefail

BASE="${1:-origin/main}"
DIR="backend/src/Yurt.Infrastructure/Migrations"
PATTERN='\.(DropTable|DropColumn|RenameTable|RenameColumn|DropSchema|RenameIndex)\('

mapfile -t added < <(git diff --name-only --diff-filter=A "$BASE"...HEAD -- "$DIR" | grep -E '/[0-9]{14}_[^/]+\.cs$' | grep -v '\.Designer\.cs$' || true)

if ((${#added[@]} == 0)); then
  echo "No new migrations."
  exit 0
fi

status=0
for f in "${added[@]}"; do
  # Only Up() matters for forward compatibility; Down() is expected to drop.
  up="$(awk '/protected override void Up\(/{p=1} /protected override void Down\(/{p=0} p' "$f")"
  hits="$(printf '%s\n' "$up" | grep -nE "$PATTERN" | grep -v 'migration-guard: allow' || true)"
  if [[ -n "$hits" ]]; then
    echo "::error file=$f::Migration is not additive (drops or renames in Up()):"
    echo "$hits"
    status=1
  else
    echo "ok  $f"
  fi
done
exit $status
