#!/bin/sh
# O front num contêiner Linux, na imagem do Node do Dockerfile, a mesma do build de produção (Alpine). É o que o CI
# roda, e roda igual aqui, pelo Git Bash:
#   sh .github/scripts/front.sh completo   # npm ci, lint, format:check, a suíte e o build
#   sh .github/scripts/front.sh suite      # npm ci e só a suíte, com o relatório JSON do Vitest em $RESULTADO
# O web/ entra só para leitura: o node_modules fica num volume anônimo, que sai junto com o contêiner, e o build vai
# para uma pasta temporária dele. Assim, nada muda no node_modules nem no dist do Windows.
# FRONT_DOCKER_OPCOES passa opções a mais ao docker run, como "--cpuset-cpus=0,1 --memory=7g", que simulam o runner
# de 2 vCPUs do GitHub.
set -eu

modo=${1:-}
raiz=$(cd "$(dirname "$0")/../.." && pwd)

# No Git Bash, o Docker do Windows precisa do caminho C:/..., e a conversão automática do MSYS estragaria o -v.
if command -v cygpath > /dev/null 2>&1; then
  export MSYS_NO_PATHCONV=1
  caminho_docker() { cygpath -m "$1"; }
else
  caminho_docker() { printf '%s' "$1"; }
fi

imagem=$(sed -n 's/^FROM \(node:[^ ]*\).*/\1/p' "$raiz/Dockerfile" | head -n 1)
if [ -z "$imagem" ]; then
  echo "front.sh: não achei o FROM node: no Dockerfile." >&2
  exit 1
fi

instalar='npm ci --no-audit --no-fund'
case "$modo" in
  completo)
    comando="$instalar && npm run lint && npm run format:check && npm test && npm run build -- --outDir /tmp/dist --emptyOutDir"
    resultado=''
    ;;
  suite)
    comando="$instalar && npx vitest run --reporter=default --reporter=json --outputFile.json=/resultado/vitest.json"
    resultado=${RESULTADO:-$(mktemp -d)}
    mkdir -p "$resultado"
    ;;
  *)
    echo "Uso: sh .github/scripts/front.sh completo|suite" >&2
    exit 2
    ;;
esac

# O ponto de montagem do volume precisa existir: dentro do web/ só para leitura, o Docker não consegue criá-lo.
mkdir -p "$raiz/web/node_modules"

set -- --rm -v "$(caminho_docker "$raiz/web"):/web:ro" -v /web/node_modules -w /web
if [ -n "$resultado" ]; then
  set -- "$@" -v "$(caminho_docker "$resultado"):/resultado"
fi

echo "front.sh: $modo, na imagem $imagem"
# shellcheck disable=SC2086 # as opções a mais são separadas por espaço de propósito
docker run "$@" ${FRONT_DOCKER_OPCOES:-} "$imagem" sh -c "$comando"
if [ -n "$resultado" ]; then
  echo "front.sh: o relatório do Vitest está em $resultado/vitest.json"
fi
