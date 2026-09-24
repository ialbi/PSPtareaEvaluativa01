#!/usr/bin/env bash
# Ejecutar desde cualquier carpeta: bash pruebas.sh
# Requiere haber compilado: dotnet build MadLib.sln -c Release
set -euo pipefail

carpeta="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cliente="$carpeta/MadLibCliente/bin/Release/net8.0/MadLibCliente.dll"
temporal="$(mktemp -d)"
trap 'rm -rf -- "$temporal"' EXIT

if [[ ! -f "$cliente" ]]; then
    echo 'Primero compila: dotnet build MadLib.sln -c Release'
    exit 1
fi

# Cada prueba envía líneas como si las escribiéramos por teclado.
# timeout evita que una avería en la comunicación bloquee las pruebas.
probar_cuento() {
    local nombre="$1"
    local entrada="$2"
    local esperado="$3"

    printf '%s\n' "$entrada" | timeout 20s dotnet "$cliente" > "$temporal/salida.txt" 2>&1

    # Extraer solo el cuento que ve el usuario, sin los registros de comunicación.
    awk '
        /Cuento recibido completo/ { split($0, partes, "("); restantes = partes[2] + 0 }
        $0 == "*************************" { separadores++; if (separadores == 2) getline; next }
        separadores == 2 && restantes > 0 { print; restantes--; if (restantes == 0) exit }
    ' "$temporal/salida.txt" > "$temporal/real.txt"
    printf '%s\n' "$esperado" > "$temporal/esperado.txt"

    if ! diff -u "$temporal/esperado.txt" "$temporal/real.txt"; then
        echo "ERROR: $nombre"
        cat "$temporal/salida.txt"
        exit 1
    fi
    grep -Fq '[Cliente] Conexión establecida. Pipe: MadLibPSP01_' "$temporal/salida.txt"
    grep -Fq '[Servidor] Finalizado.' "$temporal/salida.txt"
    # El servidor debe anunciar que está listo antes de la primera pregunta.
    awk '
        /\[Servidor\] Enviado: LISTO/ { listo = 1 }
        /Nombre del cuento/ { if (!listo) exit 1; encontrado = 1; exit }
        END { if (!encontrado) exit 1 }
    ' "$temporal/salida.txt"
    echo "OK: $nombre"
}

probar_cuento 'cuento1: respuesta con espacios' \
$'cuento1\nviajar por el mundo\ndivertido\nsalir' \
$'En vacaciones quiero viajar por el mundo\nCreo que es muy divertido\nDeberias probarlo'

probar_cuento 'cuento2: seis respuestas' \
$'cuento2\npaso\nbailar\nargentino\narte\nfácil\nvídeos\nsalir' \
$'El primer paso para aprender a bailar\ntango argentino es descubrir el mundo\nde este fascinante arte\nHoy en día es muy fácil lograr esto\nviendo vídeos por Internet\nde distintos bailarines de tango'

probar_cuento 'cuento3: varios huecos por línea' \
$'cuento3\npaso\nbailar\nargentino\ntodo\narte\nfácil\nvídeos\nsalir' \
$'El primer paso para aprender a bailar\ntango argentino es descubrir todo el mundo\nde este fascinante arte\nHoy en día es muy fácil lograr esto\nviendo vídeos por Internet\nde distintos bailarines de tango'

probar_cuento 'cuento4: descripción con espacios' \
$'cuento4\ncantar\ndivertido\nbailar\nsalir' \
$'Cuando crezca, quiero cantar.\nDicen que es algo muy divertido.\nTal vez tú también quieras bailar.'

probar_cuento 'cuento5: respuesta vacía y huecos repetidos' \
$'cuento5\n\ndivertido\nlibro favorito\nsalir\nsalir' \
$'En vacaciones quiero .\nCreo que es muy divertido.\nNo olvides llevar tu libro favorito.\nEs algo que siempre quise salir.\nDeberias probarlo alguna vez'

# Tras un error, la misma conexión debe permitir elegir y completar otro cuento.
printf '%s\n' cuento999 cuento1 cantar divertido cuento1 bailar agradable salir | \
    timeout 20s dotnet "$cliente" > "$temporal/salida.txt" 2>&1
grep -Fq '[Cliente] Recibido: ERROR' "$temporal/salida.txt"
[[ "$(grep -Fxc '*************************' "$temporal/salida.txt")" == 4 ]]
grep -Fq '[Servidor] Finalizado.' "$temporal/salida.txt"
echo 'OK: cuento inexistente y dos cuentos consecutivos'
echo 'Todas las pruebas han pasado.'
