#!/bin/sh
# Backup diário do banco (seção 9): pg_dump às 03:00, no horário de Brasília (TZ do contêiner), com 14 dias
# guardados em /backups, que é uma pasta do servidor (BACKUP_PASTA). "sh /backup/backup.sh agora" faz um backup na
# hora, como antes de atualizar o sistema. A conexão vem de PGHOST, PGUSER, PGDATABASE e PGPASSWORD.
set -eu

PASTA=/backups
HORA=${BACKUP_HORA:-03:00}
DIAS=${BACKUP_DIAS:-14}

fazer_backup() {
    arquivo="$PASTA/crm-$(date +%Y-%m-%d-%H%M%S).dump"

    # Grava num arquivo temporário e só troca o nome no fim: um backup interrompido nunca parece completo.
    if ! pg_dump --format=custom --file="$arquivo.parcial"; then
        rm -f "$arquivo.parcial"
        echo "Falha no backup em $(date '+%d/%m/%Y %H:%M'): veja a mensagem acima." >&2
        return 1
    fi
    mv "$arquivo.parcial" "$arquivo"

    # Apaga os backups com mais de DIAS dias.
    find "$PASTA" -name 'crm-*.dump' -mtime +"$((DIAS - 1))" -delete
    echo "Backup gravado: $arquivo ($(du -h "$arquivo" | cut -f1))."
}

if [ "${1:-}" = "agora" ]; then
    fazer_backup
    exit 0
fi

echo "Backup diário às $HORA ($TZ), com $DIAS dias guardados em $PASTA."
while true; do
    agora=$(date +%s)
    proximo=$(date -d "today $HORA" +%s)
    if [ "$proximo" -le "$agora" ]; then
        proximo=$(date -d "tomorrow $HORA" +%s)
    fi
    sleep $((proximo - agora))
    fazer_backup || true
done
