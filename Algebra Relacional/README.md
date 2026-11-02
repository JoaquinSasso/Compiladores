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
| `Ast.cs` | Las clases de los nodos del árbol y la impresión del árbol. |
| `Semantico.cs` | Análisis semántico: tabla de símbolos, esquemas y chequeos. |
| `Generador.cs` | Generación del SQL. |
| `Scanner.frame`, `Parser.frame` | Plantillas de Coco/R. Tienen que ser de la misma versión que el Coco que usen. |
| `Program.cs` | Programa principal: lee un archivo y ejecuta parser → semántico → generador. |
| `pruebas/validas/` | Programas sin errores de sintaxis. |
| `pruebas/invalidas/` | Programas que tienen que dar errores de sintaxis. |
| `pruebas/ast/` | Cada `.ra` tiene al lado un `.esperado` con el árbol escrito a mano. |
| `pruebas/semanticas/` | Cada `.ra` tiene al lado un `.esperado` con los esquemas o los errores semánticos. |
| `pruebas/sql/` | Cada `.ra` tiene al lado un `.esperado` con el resultado de sus consultas en SQLite, calculado a mano. |

`Scanner.cs` y `Parser.cs` los genera Coco/R: **no se editan a mano**.

## Cómo compilar y probar (Ubuntu)

```bash
mono Coco.exe AR.atg                              # generar Scanner.cs y Parser.cs
dotnet run -- pruebas/validas/01_clase_algebra.ra # ver el SQL (o los errores)
dotnet run -- pruebas/validas/01_clase_algebra.ra | python3 pruebas/ejecutar_sql.py  # ejecutarlo
dotnet run -- --esquemas pruebas/validas/01_clase_algebra.ra  # ver los esquemas
dotnet run -- --ast pruebas/ast/03_joins.ra       # ver solo el árbol
./probar.sh                                       # correr todas las pruebas
```

Coco/R tiene que terminar con `0 errors detected` y **sin avisos "LL1 warning"**.
Requiere .NET 10 (`net10.0` en `AR.csproj`), mono para ejecutar `Coco.exe` y Python 3 para las pruebas de SQL.
