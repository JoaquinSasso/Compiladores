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
| 3. Análisis semántico (tabla de símbolos, esquemas, chequeos) | ✅ Hecha: el programa imprime el esquema de cada sentencia |
| 4. Generación de SQL | ✅ Hecha: el programa imprime un script SQL para SQLite |
| 5. Integración web (Blazor WASM) | Futuro, fuera del alcance de la evaluación |

## Entorno

- **Sistema operativo:** Ubuntu 26.04.
- **Python 3** (solo para las pruebas: su módulo `sqlite3` ejecuta el SQL generado).
- **.NET 10** (`dotnet-sdk-10.0`). En `AR.csproj` tiene que decir `<TargetFramework>net10.0</TargetFramework>`.
- **Coco/R para C#**, versión "Apr 19, 2011". Se ejecuta con `mono Coco.exe AR.atg` (se baja de https://ssw.jku.at/Research/Projects/Coco/). El aviso "The runtime version supported by this application is unavailable" de mono es inofensivo.

### Ciclo de trabajo

```bash
mono Coco.exe AR.atg      # regenera Scanner.cs y Parser.cs a partir de la gramática
./probar.sh               # compila y corre todas las pruebas
dotnet run -- archivo.ra             # compila: imprime el script SQL (o los errores)
dotnet run -- --esquemas archivo.ra  # hasta el semántico: imprime el esquema de cada sentencia
dotnet run -- --ast archivo.ra       # solo sintaxis: imprime el AST
dotnet run -- archivo.ra | python3 pruebas/ejecutar_sql.py   # ejecuta el SQL en SQLite
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
| `Semantico.cs` | Análisis semántico: tabla de símbolos, esquemas (`Esquema`, `Atributo`, `Dominio`) y chequeos. |
| `Generador.cs` | Generación de SQL para SQLite a partir del AST validado. |
| `Program.cs` | Punto de entrada: lee el archivo, agrega el BOM y ejecuta parser → semántico → generador. Imprime el SQL (con `--esquemas`, los esquemas; con `--ast`, solo el AST). |
| `probar.sh` | Corre todas las pruebas. |
| `GUIA.md` | Guía para la exposición: las fases con un ejemplo real, y cada operador escrito con el teclado latino. Los ejemplos de su sección 4 se verifican a mano al cambiar el lenguaje. |
| `ejemplos/*.ra` | Programas de ejemplo para la exposición (no son pruebas). |
| `pruebas/validas/*.ra` | Programas sin errores de sintaxis (se corren con `--ast`; pueden usar tablas no declaradas). |
| `pruebas/invalidas/*.ra` | Programas que tienen que dar un error de sintaxis. |
| `pruebas/ast/*.ra` + `*.esperado` | El árbol impreso tiene que ser idéntico al `.esperado`, que se escribió a mano. |
| `pruebas/semanticas/*.ra` + `*.esperado` | La salida de `--esquemas` (esquemas o errores semánticos) tiene que ser idéntica al `.esperado`. |
| `pruebas/sql/*.ra` + `*.esperado` | El SQL generado se ejecuta en SQLite; el resultado de cada consulta tiene que ser idéntico al `.esperado`, calculado a mano. |
| `pruebas/ejecutar_sql.py` | Ejecuta un script SQL en una base SQLite en memoria e imprime el resultado de cada consulta. |

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
- La gramática acepta **varias consultas** en un mismo programa, y el análisis semántico también las permite: cada una es un resultado (a diferencia de RelaX, que admite una sola consulta final).

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

## El análisis semántico (`Semantico.cs`)

Recorre el AST y calcula el **esquema de salida** de cada `ExpRel` (lista ordenada de `Atributo { Calificador, Nombre, Tipo: Dominio }`), verificando las reglas en el camino. El esquema queda guardado en el nodo (`ExpRel.Esquema`), para que lo use el generador de SQL. Los errores se informan como `-- línea L col C: mensaje`, en castellano, y Coco/R usa el mismo formato (`errMsgFormat`) para los de sintaxis.

**Clases:** `Dominio` (`Numero`, `Cadena`, `Booleano`, `Desconocido`), `Atributo`, `Esquema` y `Semantico`. Ninguna coincide con un no terminal (ojo: `Columna` sí es un no terminal, por eso la clase se llama `Atributo`).

**Tabla de símbolos:** nombre → esquema y línea de definición. Un nombre cuya definición tuvo errores igual queda registrado (con esquema `null`), para que sus usos no den "no está definida".

**Decisiones tomadas (no cambiarlas sin motivo):**

- **Calificadores:** al definir una tabla con datos, sus columnas llevan el nombre de la tabla (`materia.codM`). Una variable (`x = expresión`) **conserva los calificadores de su expresión**, como RelaX, que reemplaza la variable por su expresión: `aprobados = π cuil (examen)` tiene la columna `examen.cuil`, no `aprobados.cuil`. Si no fuera así, `t = alumno ⋈θ examen` tendría dos `t.cuil`.
- **Los nombres no distinguen mayúsculas** (relaciones, variables y atributos), igual que las palabras clave y que SQLite.
- **Se permiten varias consultas** en un mismo programa; cada una es un resultado. Un programa sin consultas (solo un dataset) también es válido.
- **Dominios:** el declarado (`number`, `string`, `boolean`; `date` se trata como cadena) o el del primer valor no nulo. `null` entra en cualquier columna. Una columna con solo `null` es `Desconocido`, compatible con todo. Un identificador suelto que no sea `null`/`true`/`false` es error (las cadenas van entre comillas).
- **π con alias y ρ de atributos** cambian el nombre y **conservan el calificador** (`π cuil → id (alumno)` da `alumno.id`). **ρ S** cambia el calificador de todas las columnas. **γ:** las columnas de agrupamiento conservan el calificador; los agregados no llevan (`cant`). `count`, `sum` y `avg` dan número; `min` y `max`, el dominio del atributo; solo `count` acepta `*`.
- **∪ ∩ −:** mismo grado y dominios compatibles posición por posición; el resultado toma los nombres de la izquierda.
- **× y theta-join:** concatenan los esquemas. Si una columna aparece igual en los dos lados (`R × R`) es error: hay que renombrar con ρ.
- **Join natural (también los externos sin condición):** al menos un atributo en común por nombre, sin mirar el calificador; los comunes aparecen una vez, con el calificador de la izquierda. Si un común aparece dos veces en un mismo lado, es error.
- **`R ÷ S`:** cada atributo de S está en R (una sola vez y con dominio compatible); el resultado son los de R que no están en S, y tiene que quedar alguno.
- **Comparaciones:** los dos lados con dominios compatibles.
- **Ningún esquema puede tener dos veces la misma columna** (mismo calificador y nombre): se chequea después de π, ρ y γ.
- **Sin errores en cascada:** si el esquema de un subárbol no se puede calcular, el método devuelve `null` y los nodos de arriba no siguen chequeando.

**Pruebas:** `pruebas/semanticas/*.ra` + `*.esperado`: la salida completa (esquemas o errores) tiene que ser idéntica. Una prueba por cada tipo de error, más `01_esquemas.ra` con el esquema de cada operador.

## El generador de SQL (`Generador.cs`)

**Destino:** SQLite ≥ 3.39 (por `RIGHT` y `FULL JOIN`), para poder ejecutarlo en el navegador más adelante. Recorre el AST ya validado (usa los esquemas que dejó el semántico en cada nodo) y produce un script.

**La regla central:** el SQL de cada nodo es un `SELECT` cuyas columnas son **exactamente las de su esquema, en el mismo orden, con el nombre completo entre comillas** (`"alumno.cuil"`). Así, después de un join, `alumno.cuil` y `examen.cuil` siguen siendo columnas distintas, y el padre siempre sabe cómo se llama cada columna del hijo. Cada hijo se usa como subconsulta con un alias único (`(...) AS t1`, `t2`, ... con un contador). Solo el `SELECT` final de cada consulta pone nombres cortos (`cuil`), salvo que haya dos con el mismo nombre (entonces `alumno.cuil`), y agrega `ORDER BY 1, 2, ...` para que el resultado salga siempre en el mismo orden.

| Entrada | SQL |
|---|---|
| Asignación de una `TablaDatos` | `CREATE TABLE nombre (...)` (columnas con nombre corto; `NUMERIC`, `TEXT`, `BOOLEAN` o sin tipo) + `INSERT INTO ... VALUES` |
| Otra asignación (variable) | No genera nada en su lugar: es un CTE (`WITH nombre AS (...)`) en cada consulta que la usa, directa o indirectamente, en orden de definición |
| Consulta | `WITH ... SELECT <nombres cortos> FROM (E) AS tN ORDER BY ...;` |
| `Relacion` (tabla) | `SELECT "cuil" AS "alumno.cuil", ... FROM "alumno"` |
| `Relacion` (variable) | `SELECT * FROM "variable"` (el CTE ya tiene los nombres completos) |
| `TablaDatos` dentro de una expresión | `SELECT 1 AS "a", 'x' AS "b" UNION ALL SELECT 2, 'y'` |
| `σ c (E)` | `SELECT * FROM (E) AS tN WHERE c` |
| `π a, b (E)` | `SELECT DISTINCT ...` (**siempre DISTINCT**: el álgebra trabaja con conjuntos) |
| `∪ ∩ −` | `SELECT * FROM (L) AS t1 UNION/INTERSECT/EXCEPT SELECT * FROM (R) AS t2` (en SQLite los miembros de un `UNION` no pueden ir entre paréntesis) |
| `×` | `CROSS JOIN` con la lista de columnas explícita |
| theta-join y externos con condición | `JOIN` / `LEFT JOIN` / `RIGHT JOIN` / `FULL JOIN` ... `ON c` |
| join natural (y externos sin condición) | **No se usa `NATURAL JOIN`** (las columnas tienen nombres completos distintos): `JOIN ... ON t1."alumno.cuil" = t2."examen.cuil"`. En `⟖` y `⟗` el atributo común sale de `COALESCE(izq, der)` |
| `γ` | `COUNT/SUM/AVG/MIN/MAX` + `GROUP BY` (sin agrupamiento no lleva `GROUP BY`) |
| `ρ S`, `ρ b←a` | `SELECT t."viejo" AS "nuevo", ...` posición por posición |
| `÷` | No existe en SQL. Se genera directamente la equivalencia `π_Z(R) − π_Z((π_Z(R) × S) − R)` con `EXCEPT`; en la resta interna R se reordena como (Z, atributos de S), porque `EXCEPT` compara por posición. (Se eligió generarla en SQL en lugar de reescribir el AST, para no tener que volver a calcular los esquemas de nodos nuevos.) |

- Constantes: números tal cual, cadenas entre `'...'`, y `null`/`true`/`false` como `NULL`/`TRUE`/`FALSE` (SQLite muestra los booleanos como 1 y 0).
- Las filas repetidas en los datos de una tabla no se eliminan (el `CREATE TABLE` las guarda tal cual).
- **Verificación:** `pruebas/sql/*.ra` se compilan, el SQL se ejecuta con `pruebas/ejecutar_sql.py` (módulo `sqlite3` de Python, porque no hace falta instalar el cliente `sqlite3`) y la salida se compara con el `.esperado`, que tiene **los resultados calculados a mano** con el dataset `ClaseAlgebra`. Pendiente: contrastarlos también con RelaX.

## Próximos pasos

- Una función de biblioteca `Compilar(string fuente) → { sql, errores, ast }`, sin dependencias de consola (ver abajo).
- Traducir al castellano los mensajes de error de sintaxis de Coco/R (trampa 9).

## Integración futura con la web

El compilador tiene que funcionar como una **biblioteca sin dependencias de interfaz**, con una función del estilo `Compilar(string fuente) → { sql, errores, ast }`. El texto que llegue desde la web se convierte en un stream UTF-8 con BOM (ver la trampa 2). La interfaz mostraría los símbolos griegos con botones para insertarlos; el lenguaje ya acepta tanto los símbolos como las palabras.

## Estilo

- El código, los comentarios, los mensajes de error y las pruebas van en **castellano**.
- **El objetivo es que sea fácil de explicar en una evaluación de Compiladores.** Preferir código claro y por fases (scanner → parser → AST → semántico → generador) a soluciones ingeniosas.
- Un cambio a la vez, con pruebas, y verificando que `./probar.sh` termine con `0 prueba(s) fallaron`.
