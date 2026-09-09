set shell := ["bash", "-uc"]
mod be

[private]
default: help

# Elenca i comandi del progetto
help:
    @just --list

alias list := help

# Avvia lo stack backend in container con hot reload
up-all: (be::env)
    docker compose -f docker-compose.dev.yml up -d --build

# Ferma lo stack senza eliminare dati
down-all:
    docker compose -f docker-compose.dev.yml down

# Ricrea API e Scheduler mantenendo database e chiavi
restart-all:
    docker compose -f docker-compose.dev.yml up -d --build --force-recreate --no-deps api scheduler

logs-all:
    docker compose -f docker-compose.dev.yml logs -f

ps-all:
    docker compose -f docker-compose.dev.yml ps

up-be:
    @just be up

down-be:
    @just be down

install:
    @just be restore

test:
    @just be test

lint:
    @just be lint

alias watch-all := up-all
alias watch-all-down := down-all
alias watch-all-logs := logs-all
