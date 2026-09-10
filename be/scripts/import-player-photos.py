#!/usr/bin/env python3
"""Scarica le card del CSV Fantacalcio nello storage Azurite locale dedicato."""
from __future__ import annotations

import argparse
from concurrent.futures import ThreadPoolExecutor, as_completed
import csv
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import struct
import time
from urllib.parse import urlparse

import requests
from azure.core.exceptions import ResourceExistsError, ResourceNotFoundError
from azure.storage.blob import BlobServiceClient, ContentSettings

ROOT = Path(__file__).resolve().parents[1]
MAX_BYTES = 4 * 1024 * 1024
SOURCE_HOST = 'content.fantacalcio.it'


def read_rows(path):
    records = []
    seen = set()
    with path.open(encoding='utf-8-sig', newline='') as source:
        for number, row in enumerate(csv.reader(source), 1):
            if len(row) != 19:
                raise ValueError(f'Riga {number}: attese 19 colonne.')
            external_id = row[0].strip()
            if not re.fullmatch(r'[0-9]{1,32}', external_id) or int(external_id) < 1:
                raise ValueError(f'Riga {number}: ID non valido.')
            external_id = str(int(external_id))
            if external_id in seen:
                raise ValueError(f'Riga {number}: ID duplicato.')
            seen.add(external_id)
            url = row[15].strip()
            parsed = urlparse(url)
            if (parsed.scheme != 'https' or parsed.hostname != SOURCE_HOST
                    or parsed.port not in (None, 443) or parsed.username or parsed.password
                    or not re.fullmatch(r'/web/campioncini/[0-9]+/card/[0-9]+\.png', parsed.path)):
                raise ValueError(f'Riga {number}: URL fuori dalla fonte prevista.')
            if str(int(parsed.path.rsplit('/', 1)[-1][:-4])) != external_id:
                raise ValueError(f'Riga {number}: immagine e ID non corrispondono.')
            records.append((external_id, url))
    if not records:
        raise ValueError('Il CSV è vuoto.')
    return records


def validate_png(data):
    if len(data) > MAX_BYTES or len(data) < 33 or data[:8] != b'\x89PNG\r\n\x1a\n' or data[12:16] != b'IHDR':
        raise ValueError('Il file non è un PNG valido o supera 4 MiB.')
    width, height = struct.unpack('>II', data[16:24])
    if not (0 < width <= 4096 and 0 < height <= 4096):
        raise ValueError('Dimensioni immagine non valide.')
    return hashlib.sha256(data).hexdigest()


def read_clubs(path):
    manifest = json.loads(path.read_text())
    if manifest.get('source') != 'FantacalcioCsv':
        raise ValueError('Fonte degli stemmi non valida.')
    records, seen = [], set()
    for item in manifest['items']:
        name, url = item['clubName'].strip(), item['sourceUrl']
        if not re.fullmatch(r'[A-Za-z0-9 -]{1,100}', name) or name.upper() in seen:
            raise ValueError('Nome club non valido o duplicato.')
        parsed = urlparse(url)
        if (parsed.scheme != 'https' or parsed.hostname != SOURCE_HOST
                or parsed.port not in (None, 443) or parsed.username or parsed.password
                or not re.fullmatch(r'/web/img/team/ico/[A-Za-z0-9_-]+\.png', parsed.path)):
            raise ValueError('URL stemma fuori dalla fonte prevista.')
        seen.add(name.upper())
        records.append((name, url))
    if not records:
        raise ValueError('Nessuno stemma da importare.')
    return records


def download(url):
    for attempt in range(3):
        try:
            with requests.get(url, timeout=(10, 30), stream=True, allow_redirects=False) as response:
                if response.status_code == 429 or response.status_code >= 500:
                    if attempt < 2:
                        delay = response.headers.get('Retry-After', '')
                        time.sleep(min(30, int(delay)) if delay.isdigit() else 2 ** attempt)
                        continue
                if response.is_redirect:
                    raise ValueError('Redirect non seguito: verificare la sorgente.')
                response.raise_for_status()
                chunks, size = [], 0
                for chunk in response.iter_content(65536):
                    size += len(chunk)
                    if size > MAX_BYTES:
                        raise ValueError('Immagine superiore a 4 MiB.')
                    chunks.append(chunk)
                data = b''.join(chunks)
                validate_png(data)
                return data
        except requests.RequestException as error:
            if isinstance(error, requests.HTTPError) and error.response is not None and 400 <= error.response.status_code < 500 and error.response.status_code != 429:
                raise
            if attempt == 2:
                raise
            time.sleep(2 ** attempt)
    raise ValueError('Download non riuscito.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('csv', type=Path)
    parser.add_argument('--clubs', action='store_true', help='Il file in ingresso è il manifest degli stemmi verificati.')
    parser.add_argument('--refresh', action='store_true', help='Rilegge anche le immagini già scaricate.')
    parser.add_argument('--workers', type=int, choices=range(1, 9), default=4)
    args = parser.parse_args()
    records = read_clubs(args.csv) if args.clubs else read_rows(args.csv)
    identity_field = 'clubName' if args.clubs else 'externalId'
    container_name = 'club-logos' if args.clubs else 'player-photos'
    env = {}
    for line in (ROOT / '.env').read_text().splitlines():
        if line.strip() and not line.lstrip().startswith('#'):
            key, sep, value = line.partition('=')
            if sep:
                env[key.strip()] = value.strip()
    env.update(os.environ)
    key = env.get('AZURITE_ACCOUNT_KEY')
    if not key:
        raise ValueError('Eseguire prima just be env.')
    port = int(env.get('AZURITE_BLOB_PORT', '10010'))
    service = BlobServiceClient(f'http://127.0.0.1:{port}/fantastiche', credential={'account_name': 'fantastiche', 'account_key': key})
    container = service.get_container_client(container_name)
    try:
        container.create_container(public_access='blob')
    except ResourceExistsError:
        if container.get_container_access_policy()['public_access'] != 'blob':
            raise ValueError('Il container esistente non è quello pubblico previsto per le sole foto.')
    directory = ROOT / '.local' / container_name
    directory.mkdir(parents=True, exist_ok=True)
    manifest_path = directory / 'manifest.json'
    old = json.loads(manifest_path.read_text()) if manifest_path.exists() else {'source': 'FantacalcioCsv', 'items': []}
    previous = {item[identity_field]: item for item in old['items']}
    completed = {}
    failures = []

    def import_one(record):
        external_id, url = record
        filename = external_id.lower().replace(' ', '-') if args.clubs else external_id
        target = directory / f'{filename}.png'
        saved = previous.get(external_id)
        cached = not args.refresh and saved and saved['sourceUrl'] == url and target.exists()
        data = target.read_bytes() if cached else download(url)
        sha = validate_png(data)
        if cached and sha != saved['sha256']:
            data = download(url)
            sha = validate_png(data)
            cached = False
        if not cached:
            temp = target.with_suffix('.png.tmp')
            temp.write_bytes(data)
            temp.replace(target)
        blob_name = f'clubs/{filename}.png' if args.clubs else f'fantacalcio/{filename}.png'
        blob = container.get_blob_client(blob_name)
        uploaded = False
        try:
            props = blob.get_blob_properties()
            exists = props.metadata.get('sha256') == sha and props.size == len(data)
        except ResourceNotFoundError:
            exists = False
        if not exists:
            blob.upload_blob(data, overwrite=True, content_settings=ContentSettings(content_type='image/png', cache_control='public, max-age=3600'), metadata={'sha256': sha})
            uploaded = True
        item = {
            identity_field: external_id, 'sourceUrl': url, 'blobName': blob_name,
            'contentType': 'image/png', 'contentLength': len(data), 'sha256': sha,
            'downloadedAt': saved['downloadedAt'] if cached else datetime.now(timezone.utc).isoformat(),
        }
        return item, uploaded

    def checkpoint():
        manifest = {'source': 'FantacalcioCsv', 'items': sorted(completed.values(), key=lambda item: item[identity_field].upper() if args.clubs else int(item[identity_field]))}
        temp = manifest_path.with_suffix('.json.tmp')
        temp.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n')
        temp.replace(manifest_path)

    uploaded = 0
    with ThreadPoolExecutor(max_workers=args.workers) as executor:
        futures = {executor.submit(import_one, record): record[0] for record in records}
        for future in as_completed(futures):
            external_id = futures[future]
            try:
                item, changed = future.result()
                completed[external_id] = item
                uploaded += int(changed)
            except Exception as error:
                failures.append({identity_field: external_id, 'error': str(error)})
            count = len(completed) + len(failures)
            if count % 20 == 0 or count == len(records):
                checkpoint()
                print(f'{count}/{len(records)} — disponibili {len(completed)}, errori {len(failures)}', flush=True)
    checkpoint()
    (directory / 'failures.json').write_text(json.dumps(failures, ensure_ascii=False, indent=2) + '\n')
    print(f'Importazione: {len(completed)} immagini, {uploaded} blob aggiornati. Manifest: {manifest_path}', flush=True)
    service.close()
    return 1 if failures else 0


if __name__ == '__main__':
    raise SystemExit(main())
