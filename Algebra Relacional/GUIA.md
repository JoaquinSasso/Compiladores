# Guía: cómo se compila una consulta de álgebra relacional

Esta guía recorre, paso a paso, qué hace el compilador con una consulta, desde el texto que escribe el usuario hasta el resultado en SQLite. Todas las salidas que se muestran son las que imprime el compilador de verdad con el archivo [ejemplos/aprobados.ra](ejemplos/aprobados.ra). Al final hay una tabla de cómo se escribe cada operador **solo con el teclado**, sin símbolos griegos.

## 1. Las fases del compilador

```
texto .ra ──► Scanner ──► Parser ──► AST ──► Semántico ──► Generador ──► SQL ──► SQLite
              (tokens)   (sintaxis)  (árbol)  (esquemas)    (traducción)         (resultado)
              └──────── Coco/R ─────┘         └────────── código propio en C# ──────┘
```

| Fase | Qué hace | Archivo |
|---|---|---|
| Scanner | Agrupa los caracteres en *tokens* (palabras clave, nombres, números, cadenas). | `Scanner.cs` (lo genera Coco/R) |
| Parser | Verifica que los tokens respeten la gramática y arma el árbol (AST). | `Parser.cs` (lo genera Coco/R desde `AR.atg`) |
| Semántico | Recorre el árbol, calcula las columnas que produce cada operador y verifica las reglas del álgebra. | `Semantico.cs` |
| Generador | Traduce el árbol validado a un script SQL para SQLite. | `Generador.cs` |

## 2. Cómo ejecutarlo

Hace falta .NET 10. Para correr las pruebas también Python 3, y mono solo si se cambia la gramática.

```bash
dotnet run -- ejemplos/aprobados.ra               # compila: imprime el script SQL
```

```bash
dotnet run -- --ast ejemplos/aprobados.ra         # solo sintaxis: imprime el árbol
```

```bash
dotnet run -- --esquemas ejemplos/aprobados.ra    # hasta el semántico: imprime los esquemas
```

```bash
dotnet run -- ejemplos/aprobados.ra | python3 pruebas/ejecutar_sql.py   # ejecuta el SQL en SQLite
```

```bash
./probar.sh                                       # corre todas las pruebas
```

```bash
mono Coco.exe AR.atg                              # regenera Scanner.cs y Parser.cs (solo si se cambia AR.atg)
```

Para la exposición conviene tener `bin/out/AR.dll` ya compilado y usar `dotnet bin/out/AR.dll` en lugar de `dotnet run --`, que es más rápido (`./probar.sh` lo deja armado).

## 3. Ejemplo paso a paso

Consulta: *nombres de los alumnos que aprobaron algún examen (nota >= 4).* El archivo [ejemplos/aprobados.ra](ejemplos/aprobados.ra) tiene dos tablas con datos (`alumno` y `examen`) y estas dos sentencias:

```
aprobados = pi cuil (sigma nota >= 4 examen)
pi nyAp (alumno join aprobados)
```

La primera es una **asignación** (define la variable `aprobados`) y la segunda es la **consulta**. En notación matemática serían `aprobados = π cuil (σ nota>=4 (examen))` y `π nyAp (alumno ⋈ aprobados)`.

### Paso 0: la entrada

`Program.cs` lee el archivo y lo vuelve a armar en memoria **con BOM** (la marca UTF-8 del principio). Sin eso el scanner generado por Coco/R no reconocería símbolos como `π` o `⋈`. Con palabras latinas no se nota, pero el compilador acepta las dos formas.

### Paso 1: el scanner (tokens)

El scanner lee carácter por carácter y los agrupa en tokens, ignorando espacios, saltos de línea y comentarios (`-- ...` y `/* ... */`). Por ejemplo, `pi nyAp (alumno join aprobados)` se convierte en:

| Texto | `pi` | `nyAp` | `(` | `alumno` | `join` | `aprobados` | `)` |
|---|---|---|---|---|---|---|---|
| Token | palabra clave π | identificador | `(` | identificador | palabra clave ⋈ | identificador | `)` |

Las palabras clave **no distinguen mayúsculas** (`PI`, `Pi` y `pi` son lo mismo). Por eso `x` es el producto cartesiano y ninguna relación ni atributo puede llamarse `x`.

### Paso 2: el parser y el AST

El parser es descendente recursivo (uno por cada regla de la gramática, generado por Coco/R) y la gramática es LL(1). Dos decisiones no se pueden tomar mirando solo el próximo token y se resuelven con *resolvers*:

- `EsAsignacion()`: una sentencia que empieza con un nombre es una asignación si lo que sigue es `=`. En `aprobados = ...` lo es; en `pi nyAp ...` no.
- `EsCondicionJoin()`: después de `join` decide si viene una condición (theta-join) o directamente la relación derecha (join natural). Acá sigue `aprobados` y después `)`, o sea que no hay condición: es un **join natural**.

El resultado es el árbol de sintaxis abstracta (`--ast`):

```
Programa (grupo: Exposicion)
├── Asignación alumno
│   └── Tabla {cuil, nyAp, carrera} · 3 filas
├── Asignación examen
│   └── Tabla {cuil, codM, nota} · 3 filas
├── Asignación aprobados
│   └── π cuil
│       └── σ nota >= 4
│           └── examen
└── Consulta
    └── π nyAp
        └── ⋈ (natural)
            ├── alumno
            └── aprobados
```

Dos cosas para destacar en la exposición:

- El árbol imprime `π`, `σ` y `⋈` aunque se escribió `pi`, `sigma` y `join`: **las palabras y los símbolos producen el mismo árbol**.
- La precedencia está en la forma del árbol: los operadores unarios (`π σ ρ γ`) ligan más fuerte que los binarios, así que `pi nyAp (alumno join aprobados)` necesita los paréntesis para proyectar *después* del join.

Si la sintaxis está mal, el compilador se detiene acá. Por ejemplo, con `pi nyAp (alumno` (falta el paréntesis):

```
-- línea 2 col 1: ")" expected
1 error(es) de sintaxis
```

### Paso 3: el análisis semántico

La sintaxis correcta no alcanza: hay que verificar que la consulta tenga sentido. El semántico recorre el árbol de abajo hacia arriba y calcula el **esquema** de cada nodo (las columnas que produce, con su calificador y su tipo), y lo guarda en el propio nodo. En el camino verifica que las relaciones estén definidas, que los atributos existan y no sean ambiguos, que los tipos sean compatibles, que la unión sea "unión compatible", etc. (`--esquemas`):

```
alumno (alumno.cuil: número, alumno.nyAp: cadena, alumno.carrera: cadena)
examen (examen.cuil: número, examen.codM: número, examen.nota: número)
aprobados (examen.cuil: número)
Consulta (línea 21) (alumno.nyAp: cadena)
```

Se ve cómo funciona:

- Cada tabla califica sus columnas con su nombre (`alumno.cuil`).
- La variable `aprobados` conserva el calificador de su expresión (`examen.cuil`), como hace RelaX.
- En el join natural, `alumno.cuil` y `examen.cuil` se igualan porque **se llaman igual**; el calificador no se mira.
- El tipo de cada columna sale del primer valor que no es `null` de la tabla.

Un error semántico, por ejemplo `pi apellido (alumno)`, se informa con línea, columna y las opciones disponibles:

```
-- línea 5 col 4: el atributo 'apellido' no existe; los disponibles son: alumno.cuil, alumno.nyAp
1 error(es) semántico(s)
```

### Paso 4: la generación de SQL

Con el árbol validado y los esquemas calculados, el generador produce el script. La regla central: **el SQL de cada nodo es un `SELECT` con exactamente las columnas de su esquema, con el nombre completo entre comillas** (`"alumno.cuil"`). Así, después de un join, `alumno.cuil` y `examen.cuil` siguen siendo columnas distintas. Cada subconsulta lleva un alias único (`t1`, `t2`, ...).

Las tablas con datos se vuelven `CREATE TABLE` + `INSERT`, y la variable `aprobados` se vuelve un `WITH` en la consulta que la usa:

```sql
WITH "aprobados" AS (
    SELECT DISTINCT t1."examen.cuil" AS "examen.cuil"
    FROM (
        SELECT * FROM (
            SELECT "cuil" AS "examen.cuil", "codM" AS "examen.codM", "nota" AS "examen.nota" FROM "examen"
        ) AS t2
        WHERE t2."examen.nota" >= 4
    ) AS t1
)
SELECT t3."alumno.nyAp" AS "nyAp"
FROM (
    SELECT DISTINCT t4."alumno.nyAp" AS "alumno.nyAp"
    FROM (
        SELECT t5."alumno.cuil" AS "alumno.cuil", t5."alumno.nyAp" AS "alumno.nyAp", t5."alumno.carrera" AS "alumno.carrera"
        FROM (
            SELECT "cuil" AS "alumno.cuil", "nyAp" AS "alumno.nyAp", "carrera" AS "alumno.carrera" FROM "alumno"
        ) AS t5
        JOIN (
            SELECT * FROM "aprobados"
        ) AS t6
        ON t5."alumno.cuil" = t6."examen.cuil"
    ) AS t4
) AS t3
ORDER BY 1;
```

Para leerlo de adentro hacia afuera, cada operador del árbol es una capa:

| Nodo del árbol | Capa de SQL |
|---|---|
| `examen` | `SELECT "cuil" AS "examen.cuil", ... FROM "examen"` |
| `σ nota >= 4` | `SELECT * FROM (...) AS t2 WHERE t2."examen.nota" >= 4` |
| `π cuil` | `SELECT DISTINCT ...` (**siempre DISTINCT**: el álgebra trabaja con conjuntos, sin filas repetidas) |
| `⋈ (natural)` | `JOIN ... ON t5."alumno.cuil" = t6."examen.cuil"` (la igualdad de los atributos comunes se escribe a mano) |
| `π nyAp` (último) | `SELECT DISTINCT t4."alumno.nyAp"` y, afuera, el `SELECT` final con el nombre corto `"nyAp"` |

El `ORDER BY 1` del final es solo para que el resultado salga siempre en el mismo orden.

### Paso 5: ejecutar el SQL

```bash
dotnet run -- ejemplos/aprobados.ra | python3 pruebas/ejecutar_sql.py
```

```
nyAp
Juan
```

Es el resultado correcto: Juan sacó 8 en una materia, Ana solo tiene un 3 y Andrea no rindió. No hace falta tener instalado el cliente `sqlite3`: `ejecutar_sql.py` usa el módulo de Python.

## 4. Operadores escritos con el teclado

Cada operador tiene su símbolo y una forma con letras. Los dos son equivalentes y se pueden mezclar. Los ejemplos de esta tabla usan las tablas `alumno(cuil, nyAp, carrera)`, `examen(cuil, codM, nota)` y `materia(codM, nomM)`.

| Operador | Símbolo | Con el teclado | Ejemplo |
|---|---|---|---|
| Proyección | `π` | `pi` | `pi nyAp, carrera (alumno)` |
| Proyección con alias | `π a → b` | `pi a -> b` | `pi cuil -> id (alumno)` |
| Selección | `σ` | `sigma` | `sigma nota >= 4 and codM = 1 (examen)` |
| Renombrar relación | `ρ` | `rho` | `rho A (alumno)` |
| Renombrar atributos | `ρ nuevo←viejo` | `rho nuevo<-viejo` o `rho viejo->nuevo` | `rho documento<-cuil (alumno)` |
| Agrupamiento | `γ` | `gamma` | `gamma carrera; count(*) -> cant (alumno)` |
| Agrupamiento sin grupo | `γ` | `gamma` | `gamma count(*) -> total alumno` |
| Agrupamiento con otro agregado | `γ` | `gamma` | `gamma codM; avg(nota) -> prom (examen)` |
| Unión | `∪` | `union` | `pi cuil (alumno) union pi cuil (examen)` |
| Diferencia | `-` | `-` o `except` | `pi cuil (alumno) except pi cuil (examen)` |
| Diferencia (con símbolo del teclado) | `-` | `-` | `pi cuil (alumno) - pi cuil (examen)` |
| Intersección | `∩` | `intersect` | `pi cuil (alumno) intersect pi cuil (examen)` |
| Producto cartesiano | `×` | `x` o `cross join` | `rho A (alumno) x rho B (alumno)` |
| Producto cartesiano (otra forma) | `×` | `cross join` | `rho A (alumno) cross join rho B (alumno)` |
| Join natural | `⋈` | `join`, `natural join` o `*` | `alumno join examen` |
| Join natural (otra forma) | `⋈` | `natural join` | `alumno natural join examen` |
| Join natural (con asterisco) | `⋈` | `*` | `alumno * examen` |
| Theta-join | `⋈ cond` | `join cond` o `inner join cond` | `alumno join alumno.cuil = examen.cuil examen` |
| Join externo izquierdo | `⟕` | `left join` o `left outer join` | `alumno left join examen` |
| Join externo derecho | `⟖` | `right join` o `right outer join` | `alumno right join examen` |
| Join externo completo | `⟗` | `full join` o `full outer join` | `alumno full outer join examen` |
| Join externo con condición | `⟕ cond` | `left join cond` | `alumno left join alumno.cuil = examen.cuil examen` |
| División | `÷` | `/` | `pi cuil, codM (examen) / pi codM (materia)` |
| Asignación | `=` | `=` | `aprobados = pi cuil (sigma nota >= 4 examen)` |

**Condiciones** (en `sigma` y en los joins):

| Qué | Símbolo | Con el teclado |
|---|---|---|
| Igual / distinto | `=` `≠` | `=`, `<>` o `!=` |
| Menor o igual / mayor o igual | `≤` `≥` | `<=` `>=` |
| Menor / mayor | `<` `>` | `<` `>` |
| Y / O / No | `∧` `∨` `¬` | `and` `or` `not` |

Los operandos pueden ser atributos (`nota` o `examen.nota`), números y cadenas entre **comillas simples** (`'Juan'`). Se combinan con paréntesis, y `or` liga menos que `and`, que liga menos que `not`. No hay funciones ni aritmética.

**Reglas generales de escritura:**

- **Paréntesis del operando:** son opcionales. `pi cuil sigma nota > 4 examen` equivale a `pi cuil (sigma nota > 4 (examen))`. Los binarios sí los necesitan cuando se quiere cambiar la precedencia.
- **Precedencia** (de menor a mayor): `union` y `-`, después `intersect`, después `x`, `join`, `*`, joins externos y `/`, y al final los unarios `pi sigma rho gamma`. Todos los binarios asocian a izquierda.
- **Mayúsculas:** las palabras clave y los nombres no las distinguen.
- **Nombres:** ninguna relación, variable ni atributo puede llamarse `x` (es el producto cartesiano).
- **Comentarios:** `-- hasta fin de línea` y `/* ... */`.
- **Punto y coma:** opcional al final de cada sentencia. Pero **dentro de `gamma` sí** separa los atributos de agrupamiento de los agregados: `gamma carrera; count(*) -> cant alumno`.
- **Agregados** (solo en `gamma`): `count`, `sum`, `avg`, `min`, `max`, con un atributo o `*` (solo `count` acepta `*`). Siempre llevan alias: `count(*) -> cant`.
- **Tablas con datos:** la primera línea tiene las columnas (con tipo opcional, `edad:number`) y las siguientes los valores; una fila nueva empieza cuando aparece un valor sin coma delante. Los valores son números, cadenas entre comillas simples, `null`, `true` o `false`:

```
alumno = {
cuil, nyAp, carrera
10, 'Juan', 'LSI'
20, 'Ana', 'LCC'
}
```

- **Dataset de la cátedra:** una línea `group: Nombre` al principio es opcional.

## 5. Cómo se traduce cada operador a SQL

| Álgebra | SQL |
|---|---|
| `sigma c (E)` | `SELECT * FROM (E) AS tN WHERE c` |
| `pi a, b (E)` | `SELECT DISTINCT a, b FROM (E) AS tN` |
| `union`, `intersect`, `-` | `UNION`, `INTERSECT`, `EXCEPT` |
| `x` | `CROSS JOIN` |
| theta-join | `JOIN ... ON cond` |
| join natural | `JOIN ... ON` con la igualdad de los atributos comunes (no se usa `NATURAL JOIN`) |
| `left` / `right` / `full join` | `LEFT JOIN` / `RIGHT JOIN` / `FULL JOIN` (SQLite 3.39 o más nuevo) |
| `gamma` | funciones de agregación + `GROUP BY` (sin atributos de agrupamiento no lleva `GROUP BY`) |
| `rho` | cada columna se vuelve a nombrar con `AS` |
| `/` (división) | no existe en SQL: se arma como `π_Z(R) − π_Z((π_Z(R) × S) − R)` usando `EXCEPT` |

## 6. Guion sugerido para la demo

1. Mostrar el archivo `ejemplos/aprobados.ra` (las tablas y las dos sentencias).
2. `dotnet bin/out/AR.dll --ast ejemplos/aprobados.ra`: el árbol. Señalar que sale `π σ ⋈` aunque se escribió con letras.
3. `dotnet bin/out/AR.dll --esquemas ejemplos/aprobados.ra`: lo que calculó el semántico.
4. `dotnet bin/out/AR.dll ejemplos/aprobados.ra`: el SQL, leyendo las capas de adentro hacia afuera.
5. Lo mismo con `| python3 pruebas/ejecutar_sql.py`: el resultado.
6. Romper la consulta a propósito para ver los dos tipos de error: un paréntesis sin cerrar (error de sintaxis) y `pi apellido (alumno)` (error semántico).
7. Cerrar con `./probar.sh`: todas las pruebas en `0 prueba(s) fallaron`.
