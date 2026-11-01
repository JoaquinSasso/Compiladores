# Compilador de álgebra relacional a SQL — contexto del proyecto

Este archivo es el contexto para trabajar en el proyecto. Leelo completo antes de tocar código.

## Qué es

Es el **trabajo integrador de la materia Compiladores** (FCEFN–UNSJ, 2026), hecho en grupo. La consigna es desarrollar un compilador propio, con la función que el grupo elija.

Elegimos un **compilador de álgebra relacional que genera SQL**. Toma el álgebra relacional que se enseña en Base de Datos 1 (misma facultad) y la traduce a un script SQL que se puede ejecutar.

- **Motivación:** la cátedra de Base de Datos 1 usa [RelaX](https://dbis-uibk.github.io/relax/) para practicar álgebra relacional, y los alumnos necesitan una computadora para usarlo. La idea a futuro es una app web responsive, usable desde el teléfono, con el compilador corriendo en el navegador (Blazor WebAssembly + SQLite en WASM).
- **Lo que se evalúa en Compiladores es el compilador**, no la interfaz. La app web viene después y no es prioridad ahora.
- **Herramienta obligatoria:** Coco/R para C#. El parser es descendente recursivo y la gramática tiene que ser LL(1). Los conflictos se resuelven con *resolvers* `IF(...)`.

## Estado actual

| Etapa | Estado |
|---|---|
| 1. Scanner + parser (reconocedor) | ✅ Hecha |
| 2. Construcción del AST | ✅ Hecha: el programa imprime el árbol |
| 3. Análisis semántico (tabla de símbolos, esquemas, chequeos) | ⏳ **Siguiente** |
| 4. Generación de SQL | Pendiente |
| 5. Integración web (Blazor WASM) | Futuro, fuera del alcance de la evaluación |

## Entorno

- **Sistema operativo:** Ubuntu 26.04.
- **.NET 10** (`dotnet-sdk-10.0`). En `AR.csproj` tiene que decir `<TargetFramework>net10.0</TargetFramework>`.
- **Coco/R para C#**, versión "Apr 19, 2011". Se ejecuta con `mono Coco.exe AR.atg` (se baja de https://ssw.jku.at/Research/Projects/Coco/). El aviso "The runtime version supported by this application is unavailable" de mono es inofensivo.

### Ciclo de trabajo

```bash
mono Coco.exe AR.atg      # regenera Scanner.cs y Parser.cs a partir de la gramática
./probar.sh               # compila y corre todas las pruebas
dotnet run -- archivo.ra  # compila un archivo e imprime el AST (o los errores)
```

Coco/R tiene que terminar con `0 errors detected` y **sin ningún `LL1 warning`**. Un aviso LL(1) no se ignora: significa que el parser puede elegir mal una alternativa.

## Estructura de archivos

Todo el proyecto vive en la raíz de `Algebra Relacional/` (el repositorio git es la carpeta padre, `Compiladores/`). Antes había una carpeta por entrega (`compilador-ar-etapa1/`, `compilador-ar-etapa2/`); se unificaron en una sola y lo de cada entrega queda en la historia de git.

| Archivo | Qué es |
|---|---|
| `AR.atg` | La gramática de Coco/R, con sus acciones semánticas. **Es la fuente del scanner y del parser.** |
| `Coco.exe` | Coco/R para C#, versionado en el repo para no tener que bajarlo. |
| `Scanner.frame`, `Parser.frame` | Plantillas de Coco/R originales (misma versión que `Coco.exe`, con fin de línea CRLF). No modificarlas. |
| `Scanner.cs`, `Parser.cs` | **Generados por Coco/R. Nunca editarlos a mano**: se sobrescriben en cada regeneración. Se versionan igual, para poder compilar sin mono. Las copias `*.old` que deja Coco/R están en `.gitignore`. |
| `Ast.cs` | Las clases de los nodos del AST y la impresión del árbol (`ImpresorAST`). |
| `Program.cs` | Punto de entrada: lee el archivo, agrega el BOM, ejecuta el parser e imprime el AST. |
| `probar.sh` | Corre todas las pruebas. |
| `pruebas/validas/*.ra` | Programas que tienen que compilar sin errores. |
| `pruebas/invalidas/*.ra` | Programas que tienen que dar error. |
| `pruebas/ast/*.ra` + `*.esperado` | El árbol impreso tiene que ser idéntico al `.esperado`, que se escribió a mano. |

## Reglas y trampas importantes

1. **`AR.atg` está guardado en UTF-8 con BOM.** Sin el BOM, Coco/R no reconoce σ, π, ⋈, etc. Al editar el archivo hay que conservarlo. En Python: `open(p, encoding='utf-8-sig')` para leer y escribir.
2. **La entrada del compilador también tiene que llegar con BOM.** El scanner generado solo usa UTF-8 si el stream empieza con BOM. `Program.cs` lee el archivo como texto y arma un `MemoryStream` con BOM; cualquier otro punto de entrada (por ejemplo, la web) tiene que hacer lo mismo.
3. **Ningún nombre de clase del AST puede coincidir con un no terminal.** Coco/R crea un método en `Parser` por cada no terminal (`Cond`, `Attr`, `Valor`, `Sentencia`...), y C# no distingue método de clase. Por eso las clases se llaman `Condicion`, `AtributoRef`, `Constante`, `Instruccion`, etc.
4. **Si un atributo tiene tipos genéricos, va entre `<. ... .>`.** Por ejemplo, `ListaProy<. out List<ItemProy> items .>`; con `<...>`, Coco/R confunde el `>` de `List<X>` con el cierre.
5. **Los parámetros `out` se inicializan al principio de la producción** (`(. e = null; .)`). Si no, C# da error CS0177 porque en las ramas de error de sintaxis no se asignan.
6. **Las clases del AST son `public`**, porque `Parser` es público y expone el campo `programa`.
7. **`IGNORECASE` está activo:** las palabras clave no distinguen mayúsculas. `x` y `X` son el producto cartesiano, así que **ninguna relación, variable ni atributo puede llamarse `x`**. Es una decisión aceptada, porque las diapositivas de la cátedra usan `ALUMNOS X MATERIAS`.
8. **Los resolvers usan `scanner.ResetPeek()` y después `scanner.Peek()`.** El primer `Peek()` devuelve el token siguiente a `la`.
9. **Los mensajes de error de sintaxis los genera Coco/R en inglés** (`")" expected`). Traducirlos queda como mejora pendiente.
10. **Cada cambio en el lenguaje se acompaña de pruebas nuevas:** al menos una válida y una inválida, y un `.esperado` si cambia la forma del árbol.

## El lenguaje fuente (versión 1)

La sintaxis se tomó de RelaX, recortada a lo que usa la cátedra de Base de Datos 1 (diapositivas de la Unidad 4, "Modelo Relacional – Manipulación"). **Cada operador se puede escribir con su símbolo o con una palabra.**

### Operadores

| Operador | Símbolo | Palabras | Ejemplo |
|---|---|---|---|
| Proyección | `π` | `pi` | `π nyAp, cuil -> id (alumno)` |
| Selección | `σ` | `sigma` | `σ nota >= 4 and codM = 1 (examen)` |
| Renombrar relación | `ρ` | `rho` | `ρ A (alumno)` |
| Renombrar atributos | `ρ` | `rho` | `ρ nuevo←viejo, n2←v2 (R)` o `rho viejo->nuevo R` |
| Agrupamiento | `γ` | `gamma` | `γ carrera; count(*) -> cant (materia)`, `γ count(*) -> total R` |
| Unión | `∪` | `union` | `AI ∪ AG` |
| Diferencia | `-` | `except` | `AI - AG` |
| Intersección | `∩` | `intersect` | `AI ∩ AG` |
| Producto cartesiano | `×` `⨯` | `x`, `cross join` | `ALUMNOS X MATERIAS` |
| Join natural | `⋈`, `*` | `join`, `natural join` | `alumno ⋈ examen` |
| Theta-join | `⋈` | `join`, `inner join` | `alumno ⋈ alumno.cuil = examen.cuil examen` |
| Joins externos | `⟕` `⟖` `⟗` | `left/right/full [outer] join` | `R left join R.a = S.b S` (sin condición = natural) |
| División | `÷` | `/` | `R1 ÷ R2` |
| Asignación | | | `aprobados = π cuil (σ nota >= 4 examen)` |

- **Condiciones:** comparaciones `= <> != ≠ < > <= ≤ >= ≥` combinadas con `and`/`∧`, `or`/`∨`, `not`/`¬` y paréntesis. Los operandos pueden ser atributos (`a` o `R.a`), números (con `-` opcional) o cadenas entre comillas simples. **No hay funciones ni aritmética**: la cátedra no las usa.
- **Funciones de agregación (solo en γ):** `count`, `sum`, `avg`, `min`, `max`, con un atributo o `*`.
- **Los paréntesis alrededor del operando de un unario son opcionales:** `π a σ b = 1 R` equivale a `π a (σ b = 1 (R))`.
- **Comentarios:** `-- hasta fin de línea` y `/* ... */`.
- **`;` opcional** al final de cada sentencia.

### Precedencia (de menor a mayor; todos los binarios asocian a izquierda)

1. `∪` `-` → no terminal `Expr`
2. `∩` → `Inter`
3. `×` `⋈` `*` `⟕` `⟖` `⟗` `÷` → `Prod`
4. unarios `π` `σ` `ρ` `γ` → `Unario`

En las condiciones: `or` < `and` < `not` < comparación.

### Datasets (formato de la cátedra / RelaX)

```
group: ClaseAlgebra
materia = {
codM, nomM, areaConoc
1, 'Base de Datos', 'ISyBD'
2, 'Programacion Procedural', 'AyP'
}
alumno = {
cuil, nyAp, dire
10, 'Juan', 'Ignacio de la Roza 1000 Oeste'
}
```

- La línea `group: Nombre` es un único token (`grupo`) que llega hasta el fin de línea, y es opcional.
- Una tabla con datos (`RelInline`) es una expresión más. Definir una tabla es simplemente una asignación.
- El encabezado lleva las columnas separadas por coma, con tipo opcional (`balance:number`). Los valores de cada fila también van separados por coma, y **una fila nueva empieza cuando aparece un valor sin coma adelante**: los saltos de línea se ignoran.
- Los valores pueden ser números, cadenas entre comillas simples o identificadores sueltos (`null`, `true`, `false`). Las fechas de la cátedra son cadenas (`'10/10/2020'`, a veces con errores de tipeo como `'11/9/20202'`), así que **no hace falta un tipo fecha**.

## Decisiones de diseño de la gramática (no cambiarlas sin motivo)

Hay **dos resolvers**, en la sección inicial de `AR.atg`:

1. **`EsAsignacion()`:** una sentencia que empieza con `ident` es una asignación si el token siguiente es `=`; si no, es una consulta.
2. **`EsCondicionJoin()`:** después de `⋈`/`join`/joins externos decide si sigue una condición (theta-join) o directamente la relación derecha (join natural). Se saltea los `(` iniciales y mira el primer token real. Si es un número, una cadena, `not`/`¬` o `-`, es una condición. Si es un identificador seguido de `.` o de un comparador, también. En cualquier otro caso es la relación. Así se resuelven `R ⋈ (a = b) S` (condición) y `R ⋈ (σ a = 1 S)` (relación).

Otras decisiones:

- `x` es palabra reservada (ver la trampa 7).
- `*` se acepta como join natural porque es la notación de la diapositiva 24. No genera conflictos porque no hay multiplicación.
- Los nombres de las funciones de agregación son palabras clave. Por eso γ distingue sin resolver entre la lista de agrupamiento (identificadores) y los agregados.
- La gramática acepta **varias consultas** en un mismo programa. Si se limita a una consulta final, como en RelaX, lo decide el análisis semántico (está pendiente).

## El AST (`Ast.cs`)

```
Programa { Grupo, List<Instruccion> }
Instruccion (abstracta) { ExpRel Expr }
 ├── Asignacion { Nombre }
 └── Consulta
ExpRel (abstracta) { Etiqueta(), Hijos() }
 ├── Relacion { Nombre }                                   (hoja: tabla o variable)
 ├── TablaDatos { List<ColumnaDef>, List<List<Constante>> } (hoja: tabla con datos)
 ├── OpUnaria (abstracta) { Hijo }
 │    ├── Seleccion { Condicion }
 │    ├── Proyeccion { List<ItemProy{AtributoRef, Alias}> }
 │    ├── RenombreRel { NuevoNombre }
 │    ├── RenombreAttr { List<ParRenombre{Viejo: AtributoRef, Nuevo}> }
 │    └── Agrupamiento { List<AtributoRef> Grupo, List<FuncionAgregada> }
 └── OpBinaria { TipoOp, Izq, Der, Condicion Cond }        (en los joins, Cond null = natural)
TipoOp: Union, Diferencia, Interseccion, Producto, Join, JoinIzq, JoinDer, JoinCompleto, Division
Condicion (abstracta)
 ├── CondLogica { Op "and"|"or", Izq, Der }
 ├── Negacion { Hijo }
 └── Comparacion { TerminoCond Izq, Op, TerminoCond Der }   (Op normalizado: = <> < > <= >=)
TerminoCond (abstracta)
 ├── AtributoRef { Relacion (null si no está calificado), Nombre }
 └── Constante { TipoDato Numero|Cadena|Simbolo, Valor }   (cadenas sin comillas)
```

Todos los nodos guardan `Linea` y `Col`. Los paréntesis no generan nodo: ya están representados en la forma del árbol. Las variantes de cada operador se normalizan en el parser (`≠`, `!=` y `<>` pasan a `"<>"`, `∧` pasa a `"and"`, etc.).

## Próximo paso: análisis semántico (etapa 3)

**Objetivo:** recorrer el AST y calcular el **esquema de salida** de cada nodo (lista ordenada de columnas, cada una con calificador, nombre y tipo), verificando las reglas en el camino. Es conveniente hacerlo en una clase aparte (`Semantico.cs`), no dentro del `.atg`. Los errores se informan con línea y columna, en castellano.

**Tabla de símbolos:** relación o variable → esquema. Siguiendo la regla de RelaX, al definir una tabla el calificador de sus columnas es el nombre de la tabla (`materia.codM`).

**Chequeos a implementar:**

- La relación o variable usada está definida, y antes de usarse.
- No hay nombres definidos dos veces.
- Cada atributo existe en el esquema del hijo, y no es ambiguo (si aparece en dos relaciones, hay que calificarlo).
- Cada fila de una tabla tiene tantos valores como columnas.
- El tipo de cada columna se deduce del primer valor no nulo, o se toma del tipo declarado. Todos los valores de la columna tienen que ser compatibles con ese tipo.
- **∪, ∩ y − exigen relaciones "unión compatibles"**, como define la cátedra: mismo grado y mismos dominios, posición por posición.
- El join natural requiere al menos un atributo en común (diapositiva 24).
- **División `R ÷ S`:** los atributos de S tienen que estar incluidos en los de R. El resultado son los atributos Z de R que no están en S.
- **Comparaciones:** los dos lados tienen que ser del mismo tipo.
- **γ:** `sum` y `avg` solo sobre números. El esquema de salida son las columnas de agrupamiento más los alias de los agregados.
- **ρ de atributos:** el atributo viejo existe. **ρ de relación:** cambia el calificador de todas las columnas.
- **π con alias:** la columna toma el nombre nuevo.
- Decidir si se permite más de una consulta por programa.

**Pruebas:** agregar `pruebas/semanticas/` con programas que tengan que dar un error semántico específico, y comparar el mensaje esperado.

## Después: generación de SQL (etapa 4)

**Destino:** SQLite, para poder ejecutarlo en el navegador más adelante. El generador recorre el AST validado y produce un script.

| Entrada | SQL |
|---|---|
| Asignación de una `TablaDatos` | `CREATE TABLE nombre (...)` + `INSERT INTO ...` |
| Otra asignación | CTE: `WITH nombre AS (...)` |
| Consulta | `SELECT ...` |
| `Relacion` | `SELECT * FROM nombre` |
| `σ c (E)` | `SELECT * FROM (E) AS tN WHERE c` |
| `π a, b (E)` | `SELECT DISTINCT a, b FROM (E) AS tN` (**siempre DISTINCT**: el álgebra trabaja con conjuntos) |
| `∪ ∩ −` | `UNION`, `INTERSECT`, `EXCEPT` (ya eliminan duplicados) |
| `×` | `CROSS JOIN` |
| theta-join | `JOIN ... ON c` |
| join natural | `NATURAL JOIN` |
| `⟕ ⟖ ⟗` | `LEFT / RIGHT / FULL OUTER JOIN` (SQLite ≥ 3.39) |
| `γ` | `GROUP BY` (sin agrupamiento, la consulta no lleva `GROUP BY`) |
| `ρ S` | alias de la subconsulta |
| `ρ b←a` | `SELECT a AS b, ...` |
| `÷` | No existe en SQL: **reescribir en el AST antes de generar** como `π_Z(R) − π_Z((π_Z(R) × S) − R)`. Hay que reordenar las columnas según el esquema, porque `EXCEPT` compara por posición. |

- Cada subconsulta lleva un alias único (`t1`, `t2`, ... con un contador).
- **Problema conocido a resolver:** después de un join hay columnas con el mismo nombre (`alumno.cuil` y `examen.cuil`). Con el esquema que calcula el semántico, el generador tiene que escribir la lista de columnas explícita, con alias únicos entre comillas (por ejemplo `"alumno.cuil"`), en lugar de usar `SELECT *`.
- **Verificación:** ejecutar el SQL generado en SQLite con el dataset `ClaseAlgebra` y comparar los resultados con los que da RelaX para los mismos ejercicios.

## Integración futura con la web

El compilador tiene que funcionar como una **biblioteca sin dependencias de interfaz**, con una función del estilo `Compilar(string fuente) → { sql, errores, ast }`. El texto que llegue desde la web se convierte en un stream UTF-8 con BOM (ver la trampa 2). La interfaz mostraría los símbolos griegos con botones para insertarlos; el lenguaje ya acepta tanto los símbolos como las palabras.

## Estilo

- El código, los comentarios, los mensajes de error y las pruebas van en **castellano**.
- **El objetivo es que sea fácil de explicar en una evaluación de Compiladores.** Preferir código claro y por fases (scanner → parser → AST → semántico → generador) a soluciones ingeniosas.
- Un cambio a la vez, con pruebas, y verificando que `./probar.sh` termine con `0 prueba(s) fallaron`.
