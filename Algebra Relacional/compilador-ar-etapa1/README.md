# Compilador de álgebra relacional — Etapa 1 (reconocedor)

Esta etapa solo dice si la entrada es válida. Todavía no arma el AST, no hace
análisis semántico ni genera SQL.

## Archivos

| Archivo | Qué es |
|---|---|
| `AR.atg` | La gramática (scanner + parser). **Es el único archivo que se edita para cambiar el lenguaje.** Está en UTF-8 con BOM: no le saquen el BOM o Coco/R deja de reconocer σ, π, ⋈. |
| `Scanner.frame`, `Parser.frame` | Plantillas de Coco/R. Tienen que ser de la misma versión que el Coco que usen. |
| `Program.cs` | Programa principal: lee un archivo y ejecuta el parser. |
| `pruebas/validas/` | Programas que tienen que dar `OK`. |
| `pruebas/invalidas/` | Programas que tienen que dar errores. |

`Scanner.cs` y `Parser.cs` los genera Coco/R: **no se editan a mano**.

## Cómo compilar y probar (Ubuntu)

```bash
# 1. Generar Scanner.cs y Parser.cs a partir de la gramática
mono Coco.exe AR.atg          # o: dotnet Coco.dll AR.atg

# 2. Compilar y probar un archivo
dotnet run -- pruebas/validas/01_clase_algebra.ra

# 3. Correr todas las pruebas
./probar.sh
```

Coco/R tiene que terminar con `0 errors detected` y **sin avisos "LL1 warning"**.
