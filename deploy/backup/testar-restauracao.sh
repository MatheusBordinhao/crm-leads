#!/bin/sh
# Teste mensal da restauração (seção 9): restaura o backup mais recente, ou o arquivo informado, num banco à parte,
# compara as contagens com as do banco em uso e apaga o banco de teste. O banco em uso não é tocado.
#   docker compose -f docker-compose.prod.yml exec backup sh /backup/testar-restauracao.sh [/backups/crm-....dump]
set -eu

arquivo=${1:-$(ls -1t /backups/crm-*.dump 2>/dev/null | head -n 1)}
if [ -z "$arquivo" ] || [ ! -f "$arquivo" ]; then
    echo "Nenhum backup encontrado em /backups." >&2
    exit 1
fi

banco=crm_teste_restauracao
dropdb --if-exists "$banco"
createdb "$banco"
trap 'dropdb --if-exists "$banco"' EXIT
pg_restore --no-owner --exit-on-error --dbname="$banco" "$arquivo"

echo "Backup restaurado: $arquivo"
printf '%-20s %12s %12s\n' "Tabela" "No backup" "Agora"
for tabela in usuarios leads entradas_recebidas interacoes historico_etapas importacoes auditoria; do
    no_backup=$(psql --dbname="$banco" --tuples-only --no-align --command="SELECT count(*) FROM $tabela")
    agora=$(psql --tuples-only --no-align --command="SELECT count(*) FROM $tabela")
    printf '%-20s %12s %12s\n' "$tabela" "$no_backup" "$agora"
done
echo "Restauração testada. O que entrou depois do backup não está nele, então o backup pode ter números menores."
