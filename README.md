# MadLib cliente-servidor

Dos programas de consola en C# basados en los ejemplos de PSP01: `ProcessStartInfo`,
`Process.Start`, pipes con nombre, `StreamReader`, `StreamWriter` y comunicación
síncrona mediante `ReadLine`, `WriteLine` y `Flush`.

## Ejecutar en este equipo

Desde la carpeta PSP01:

```bash
dotnet PSPtareaEvaluativa01/MadLibCliente/bin/Release/net8.0/MadLibCliente.dll
```

El cliente arranca el servidor automáticamente. Introduce `cuento1`, `cuento2`,
`cuento3`, `cuento4` o `cuento5`. Contesta las preguntas y verás el cuento al terminar.
En el menú, escribe `salir` para cerrar ambos procesos. Durante una pregunta,
una línea vacía, varias palabras o la palabra `salir` son respuestas válidas.
Los mensajes de cliente y servidor comparten consola y llevan un prefijo.

El último cuento completado se guarda en:

`MadLibCliente/bin/Release/net8.0/Servidor/resultado.txt`

Cada cuento completado sobrescribe el anterior. Un cuento inexistente permite
volver a elegir sin cerrar la conexión.

## Compilar y abrir en Visual Studio

Requiere SDK de .NET 8 y Visual Studio 2022 con soporte para .NET 8 (17.8 o posterior).
Abre `MadLib.sln` y establece `MadLibCliente` como proyecto de inicio.
Al compilar el cliente también se compila el servidor y se copian sus archivos y
los cuentos a la subcarpeta `Servidor` de la salida del cliente.
No hay que modificar rutas en el código. `dotnet` debe estar disponible en PATH.

Desde `PSPtareaEvaluativa01` también puedes usar:

```bash
dotnet build MadLib.sln -c Release
dotnet run --project MadLibCliente -c Release --no-build
```

Para trasladar el programa ya compilado, conserva toda la carpeta
`MadLibCliente/bin/Release/net8.0`, incluida `Servidor`. Ejecuta la DLL con .NET 8.
Los ejecutables nativos generados aquí corresponden a Linux; en Windows puedes
usar `dotnet MadLibCliente.dll` o volver a compilar la solución.

## Protocolo

Cada elemento viaja en una línea; cada envío se completa con `Flush()`.

| Emisor | Líneas enviadas |
|---|---|
| Servidor | `LISTO` (confirmación inicial, antes de mostrar el menú del cliente) |
| Cliente | `CUENTO`, nombre sin extensión |
| Servidor | `HUECO`, descripción entre los signos `<` y `>` |
| Cliente | `RESPUESTA`, respuesta completa (incluso vacía) |
| Servidor | `RESULTADO`, número de líneas, líneas del cuento |
| Servidor | `ERROR`, explicación |
| Cliente | `SALIR` |
| Servidor | `FIN` |

El tipo de mensaje y el contenido viajan por separado: escribir `SALIR` como
respuesta no cierra la conexión. El número de líneas delimita el resultado.
Un fin de entrada (`null`) se distingue de una respuesta vacía (`""`).

El servidor procesa los marcadores de izquierda a derecha sobre el texto original.
Cada aparición genera una pregunta independiente. El texto aportado por el usuario
no se vuelve a interpretar como un marcador. El cliente recibe solo la descripción
del hueco hasta que el cuento está terminado. Los archivos originales son texto
plano con marcadores, aunque la rúbrica mencione XML.

## Batería de pruebas

Para una batería sencilla en Bash, después de compilar ejecuta desde esta carpeta:

```bash
bash pruebas.sh
```

Son pruebas de integración: envían respuestas al cliente real, que arranca su
servidor y se comunica por el pipe. Comprueban los cinco cuentos completos con
`diff`, incluyendo respuestas vacías, frases y huecos repetidos, además de un
cuento inexistente y dos cuentos consecutivos. Requieren Bash, .NET y las
utilidades habituales de Linux (`timeout`, `awk`, `grep`, `diff`, `mktemp`).
Sobrescriben `resultado.txt`, igual que una ejecución manual.
