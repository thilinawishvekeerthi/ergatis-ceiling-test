#!/usr/bin/env bash
# Starts a local PostgreSQL 16 cluster (no Docker, no root). Binaries: ~/.pg16, data: ~/.pg16-data.
set -euo pipefail
PG_HOME="${PG_HOME:-$HOME/.pg16}"
PG_DATA="${PG_DATA:-$HOME/.pg16-data}"
PG_PORT="${PG_PORT:-5433}"
export LD_LIBRARY_PATH="$PG_HOME/lib:${LD_LIBRARY_PATH:-}"

if [ ! -d "$PG_DATA" ]; then
  "$PG_HOME/bin/initdb" -D "$PG_DATA" -U postgres --auth=trust -E UTF8 --locale=C >/dev/null
fi
if ! "$PG_HOME/bin/pg_ctl" -D "$PG_DATA" status >/dev/null 2>&1; then
  "$PG_HOME/bin/pg_ctl" -D "$PG_DATA" -o "-p $PG_PORT -k /tmp -c listen_addresses=127.0.0.1" -l "$PG_DATA/server.log" -w start
fi
echo "PostgreSQL ready on 127.0.0.1:$PG_PORT (user postgres, trust auth, local dev only)"
