#!/usr/bin/env python3
"""Configurazione locale senza stampare credenziali; tooling .NET sull’host."""
import os
import base64
from pathlib import Path
import secrets
import subprocess
import sys
root = Path(__file__).resolve().parents[1]
os.chdir(root)
path = root / '.env'
if not path.exists():
    path.write_text('MSSQL_SA_PASSWORD=' + secrets.token_urlsafe(32) + 'aA1!\n')
    path.chmod(0o600)
values = {}
for line in path.read_text().splitlines():
    if line.strip() and not line.lstrip().startswith('#'):
        key, sep, value = line.partition('=')
        if sep:
            values[key.strip()] = value.strip()
if not values.get('AZURITE_ACCOUNT_KEY'):
    values['AZURITE_ACCOUNT_KEY'] = base64.b64encode(secrets.token_bytes(64)).decode()
    with path.open('a') as output:
        output.write('\nAZURITE_ACCOUNT_KEY=' + values['AZURITE_ACCOUNT_KEY'] + '\n')
    path.chmod(0o600)
env = {**os.environ, **values}
env.setdefault('ASPNETCORE_ENVIRONMENT', 'Development')
env.setdefault('DOTNET_ENVIRONMENT', 'Development')
env.setdefault('DOTNET_CLI_HOME', str(root / '.local/dotnet'))
env.setdefault('DataProtection__KeyPath', str(root / '.local/keys'))
env.setdefault('Email__LocalDirectory', str(root / '.local/mail'))
env.setdefault('Email__Provider', 'Local')
env.setdefault('Storage__PlayerPhotos__PublicBaseUrl', f'http://localhost:{env.get("AZURITE_BLOB_PORT", "10010")}/fantastiche/player-photos')
env.setdefault('Storage__ClubLogos__PublicBaseUrl', f'http://localhost:{env.get("AZURITE_BLOB_PORT", "10010")}/fantastiche/club-logos')
env.setdefault('Storage__LeagueLogos__PublicBaseUrl', f'http://localhost:{env.get("AZURITE_BLOB_PORT", "10010")}/fantastiche/league-logos')
env.setdefault('Storage__LeagueLogos__ConnectionString', f'DefaultEndpointsProtocol=http;AccountName=fantastiche;AccountKey={env["AZURITE_ACCOUNT_KEY"]};BlobEndpoint=http://127.0.0.1:{env.get("AZURITE_BLOB_PORT", "10010")}/fantastiche;')
password = env['MSSQL_SA_PASSWORD']
# Le virgolette seguono le regole ADO.NET per i valori con punto e virgola.
escaped = password.replace('"', '""')
env.setdefault('ConnectionStrings__Fantastiche', f'Server=127.0.0.1,{env.get("MSSQL_PORT", "14333")};Database=Fantastiche;User Id=sa;Password="{escaped}";Encrypt=True;TrustServerCertificate=True')
if len(sys.argv) > 1 and sys.argv[1] == 'env':
    print('Configurazione locale pronta in be/.env (segreti non mostrati).')
    sys.exit(0)
if len(sys.argv) <= 1:
    sys.exit('Specificare il comando da eseguire.')
sys.exit(subprocess.run(sys.argv[1:], env=env).returncode)
