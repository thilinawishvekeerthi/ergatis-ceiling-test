#!/usr/bin/env bash
set -euo pipefail
PG_HOME="${PG_HOME:-$HOME/.pg16}"
PG_DATA="${PG_DATA:-$HOME/.pg16-data}"
export LD_LIBRARY_PATH="$PG_HOME/lib:${LD_LIBRARY_PATH:-}"
"$PG_HOME/bin/pg_ctl" -D "$PG_DATA" -m fast stop
