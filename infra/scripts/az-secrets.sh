#!/usr/bin/env bash
# =====================================================================
# Scrive/aggiorna un segreto nel Key Vault Fantastiche, a domande.
#   - "modifica": elenca i segreti esistenti, scegli, inserisci il valore
#   - "nuovo":    inserisci nome + valore
# Il valore è inserito in input NASCOSTO e non viene mai stampato.
# Richiede di essere già sulla subscription giusta (vedi `just infra az-env`).
# Segreti attesi: ConnectionStrings--Fantastiche, Mailgun--ApiKey, Bootstrap--Password.
# =====================================================================
set -uo pipefail

VAULT="kv-fantastiche-prod"
echo "  Vault: ${VAULT}"
echo ""

echo "  Azione:"
echo "    1) modifica un segreto esistente"
echo "    2) nuovo segreto"
read -rp "  Scelta [1]: " action
action="${action:-1}"

name=""
case "${action}" in
  1)
    echo ""
    echo "  Segreti esistenti in ${VAULT}:"
    secrets=()
    while IFS= read -r line; do
      [ -n "${line}" ] && secrets+=("${line}")
    done < <(az keyvault secret list --vault-name "${VAULT}" --query "[].name" -o tsv)
    if [ "${#secrets[@]}" -eq 0 ]; then
      echo "  (nessun segreto trovato, o nessun accesso al vault)"
      exit 1
    fi
    i=1
    for s in "${secrets[@]}"; do
      echo "    ${i}) ${s}"
      i=$((i + 1))
    done
    read -rp "  Quale (numero o nome): " pick
    if [[ "${pick}" =~ ^[0-9]+$ ]]; then
      idx=$((pick - 1))
      name="${secrets[${idx}]:-}"
    else
      name="${pick}"
    fi
    ;;
  2)
    read -rp "  Nome segreto (es. Mailgun--ApiKey): " name
    ;;
  *)
    echo "  Scelta non valida: ${action}"
    exit 1
    ;;
esac

if [ -z "${name}" ]; then
  echo "  Nome segreto vuoto, annullo."
  exit 1
fi

read -rsp "  Valore per ${name} (input nascosto): " value
echo ""
if [ -z "${value}" ]; then
  echo "  Valore vuoto, annullo."
  exit 1
fi

if az keyvault secret set --vault-name "${VAULT}" --name "${name}" --value "${value}" -o none 2>/dev/null; then
  echo "  ✓ '${name}' impostato in ${VAULT}"
else
  echo "  ✗ errore su ${VAULT} (esiste? permessi? subscription giusta?)"
  exit 1
fi
