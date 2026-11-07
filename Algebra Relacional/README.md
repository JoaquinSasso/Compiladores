# Compilador de álgebra relacional a SQL

Traduce álgebra relacional (la sintaxis de RelaX y de Base de Datos 1) a un
script SQL para SQLite: valida la sintaxis y la semántica y, si no hay errores,
imprime el SQL.

Para ver cómo se compila una consulta paso a paso, y cómo se escribe cada
operador con el teclado, leer [GUIA.md](GUIA.md).

## Archivos

| Archivo | Qué es |
|---|---|
| `AR.atg` | La gramática con sus acciones semánticas. **Es el único archivo que se edita para cambiar el lenguaje.** Está en UTF-8 con BOM: no le saquen el BOM o Coco/R deja de reconocer σ, π, ⋈. |
| `Gramatica.atg` | La gramática sola, sin atributos ni acciones, para leerla y analizarla aparte. **Se genera** desde `AR.atg` con `python extraer_gramatica.py`: no se edita a mano. |
| `Ast.cs` | Las clases de los nodos del árbol y la impresión del árbol. |
| `Semantico.cs` | Análisis semántico: tabla de símbolos, esquemas y chequeos. |
| `Generador.cs` | Generación del SQL. |
| `Scanner.frame`, `Parser.frame` | Plantillas de Coco/R. Tienen que ser de la misma versión que el Coco que usen. |
| `Compilador.cs` | El compilador como biblioteca: `Compilador.Compilar(fuente)` devuelve los errores, el AST y el SQL. |
| `Program.cs` | Programa principal (línea de comandos): lee un archivo y llama a `Compilador.Compilar`. |
| `Web/` | La app web: calculadora de álgebra relacional que se instala como PWA y funciona sin conexión. |
| `pruebas/validas/` | Programas sin errores de sintaxis. |
| `pruebas/invalidas/` | Programas que tienen que dar errores de sintaxis. |
| `pruebas/ast/` | Cada `.ra` tiene al lado un `.esperado` con el árbol escrito a mano. |
| `pruebas/semanticas/` | Cada `.ra` tiene al lado un `.esperado` con los esquemas o los errores semánticos. |
| `pruebas/sql/` | Cada `.ra` tiene al lado un `.esperado` con el resultado de sus consultas en SQLite, calculado a mano. |

`Scanner.cs` y `Parser.cs` los genera Coco/R: **no se editan a mano**.

## Cómo compilar y probar (Ubuntu)

```bash
mono Coco.exe AR.atg                              # generar Scanner.cs y Parser.cs
python3 extraer_gramatica.py                      # regenerar Gramatica.atg (la gramática sin acciones)
dotnet run -- pruebas/validas/01_clase_algebra.ra # ver el SQL (o los errores)
dotnet run -- pruebas/validas/01_clase_algebra.ra | python3 pruebas/ejecutar_sql.py  # ejecutarlo
dotnet run -- --esquemas pruebas/validas/01_clase_algebra.ra  # ver los esquemas
dotnet run -- --ast pruebas/ast/03_joins.ra       # ver solo el árbol
./probar.sh                                       # correr todas las pruebas
```

## Cómo compilar y probar (Windows)

Hace falta el SDK de .NET 10, Python 3 y Git for Windows (por Git Bash, para
`probar.sh`). `Coco.exe` corre directo, sin mono. Desde Git Bash:

```bash
./Coco.exe AR.atg                                 # generar Scanner.cs y Parser.cs
python extraer_gramatica.py                       # regenerar Gramatica.atg (la gramática sin acciones)
dotnet run -- pruebas/validas/01_clase_algebra.ra # ver el SQL (o los errores)
dotnet run -- pruebas/validas/01_clase_algebra.ra | python pruebas/ejecutar_sql.py  # ejecutarlo
./probar.sh                                       # correr todas las pruebas
```

En Windows se usa `python`, no `python3` (ese nombre es un acceso directo a la
Microsoft Store). `probar.sh` elige solo el que funcione. Al regenerar con Coco/R,
`git status` puede marcar `Scanner.cs` y `Parser.cs` como modificados solo por los
fines de línea: `git diff --ignore-cr-at-eol` lo confirma.

Coco/R tiene que terminar con `0 errors detected` y **sin avisos "LL1 warning"**.

`Gramatica.atg` también se puede pasar sola por Coco/R para verificar que la gramática
es LL(1). Se genera en otra carpeta para no pisar `Parser.cs` y `Scanner.cs`; con
`-trace F` se listan los conjuntos FIRST y FOLLOW en `trace.txt`:

```bash
./Coco.exe Gramatica.atg -o /tmp/gramatica -trace F
```

`probar.sh` verifica que `Gramatica.atg` esté al día con `AR.atg` y que Coco/R la acepte sin avisos.

Requiere .NET 10 (`net10.0` en `AR.csproj`), mono para ejecutar `Coco.exe` y Python 3 para las pruebas de SQL.

## App web

En `Web/` hay una app web (Blazor WebAssembly) que usa el mismo compilador: una
calculadora con botones para los símbolos y una pestaña para cargar el dataset. El
SQL se ejecuta en el navegador con SQLite compilado a WebAssembly (sql.js), así que
no necesita servidor. Se puede instalar en el teléfono y funciona sin conexión.

```bash
dotnet run --project Web                             # abrir http://localhost:5041
dotnet publish Web -c Release -o Web/bin/publicada   # para subirla a un hosting estático
```

El modo sin conexión solo funciona en la versión publicada. Para probarla, servir
`Web/bin/publicada/wwwroot` con cualquier servidor estático, por ejemplo
`python -m http.server 5050 --directory Web/bin/publicada/wwwroot`.
