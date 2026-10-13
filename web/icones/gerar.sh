#!/bin/sh
# Gera os PNGs do PWA (web/public) a partir dos SVGs, num contêiner Linux, sem instalar nada no Windows (README › PWA):
#   docker run --rm -v "${PWD}/web:/web" -w /web alpine:3.22 sh icones/gerar.sh
# Rode de novo quando o favicon.svg ou os SVGs desta pasta mudarem, como quando chegar o logo da empresa.
set -eu

apk add --no-cache rsvg-convert > /dev/null

# O ícone de sempre (purpose any), com os cantos arredondados do favicon.
rsvg-convert -w 192 -h 192 public/favicon.svg -o public/icone-192.png
rsvg-convert -w 512 -h 512 public/favicon.svg -o public/icone-512.png

# O da tela inicial do celular (purpose maskable) e o do iPhone, com o fundo inteiro.
rsvg-convert -w 512 -h 512 icones/icone-maskable.svg -o public/icone-maskable-512.png
rsvg-convert -w 180 -h 180 icones/icone-maskable.svg -o public/apple-touch-icon.png

# O da barra de notificações do Android, só com o desenho.
rsvg-convert -w 96 -h 96 icones/icone-notificacao.svg -o public/icone-notificacao.png

echo "Ícones gerados em web/public."
