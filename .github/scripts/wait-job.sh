#!/usr/bin/env bash
# Attende l'esito di un'esecuzione di Container Apps Job: exit 0 su Succeeded, 1 altrimenti.
# Uso: wait-job.sh <job> <resource-group> <execution-name>
set -eo pipefail
JOB="$1"; RG="$2"; EXEC="$3"
[ -n "$EXEC" ] || { echo "Nome esecuzione vuoto: job start non riuscito"; exit 1; }
echo "Esecuzione $EXEC"
for _ in $(seq 1 180); do
  STATUS=$(az containerapp job execution show -n "$JOB" -g "$RG" --job-execution-name "$EXEC" --query properties.status -o tsv 2>/dev/null || echo Pending)
  case "$STATUS" in
    Succeeded) echo "Migrazione completata"; exit 0 ;;
    Failed|Stopped|Degraded) echo "Migrazione $STATUS"; az containerapp job logs show -n "$JOB" -g "$RG" --execution "$EXEC" --container "$JOB" || true; exit 1 ;;
  esac
  sleep 10
done
echo "Timeout in attesa della migrazione"; exit 1
