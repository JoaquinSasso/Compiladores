using System;
using System.Collections.Generic;
using System.Linq;

/*============================================================================
  Análisis semántico (etapa 3).

  Recorre el AST y calcula el ESQUEMA DE SALIDA de cada expresión: la lista
  ordenada de columnas (calificador, nombre y dominio) que produce. En el
  camino verifica las reglas del álgebra relacional y guarda el esquema en
  cada nodo (ExpRel.Esquema), para que lo use después el generador de SQL.

  Reglas de nombres:
    - Al definir una tabla con datos, el calificador de sus columnas es el
      nombre de la tabla:  materia = {codM, ...}  →  materia.codM
    - Una variable (x = expresión) conserva los calificadores de su expresión,
      igual que RelaX, que reemplaza la variable por su expresión.
    - Los nombres no distinguen mayúsculas (como las palabras clave y SQLite).

  Recuperación de errores: cuando el esquema de una expresión no se puede
  calcular, el método devuelve null y los nodos de arriba no siguen
  chequeando, para no informar errores en cascada.
============================================================================*/

// Dominio (tipo) de una columna. Desconocido: columna que solo tiene null;
// es compatible con cualquier otro dominio.
public enum Dominio { Numero, Cadena, Booleano, Desconocido }

// Una columna de un esquema: materia.codM: número
public class Atributo {
    public string Calificador, Nombre;                     // Calificador null = sin calificar
    public Dominio Tipo;
    public Atributo(string cal, string nombre, Dominio tipo) { Calificador = cal; Nombre = nombre; Tipo = tipo; }

    public string NombreCompleto => Calificador == null ? Nombre : Calificador + "." + Nombre;
    public Atributo ConCalificador(string cal) => new Atributo(cal, Nombre, Tipo);
    public Atributo ConNombre(string nombre) => new Atributo(Calificador, nombre, Tipo);
    public override string ToString() => NombreCompleto + ": " + Semantico.NombreDominio(Tipo);
}

// El esquema de una relación: sus columnas, en orden.
public class Esquema {
    public List<Atributo> Atributos;
    public Esquema(IEnumerable<Atributo> attrs) { Atributos = attrs.ToList(); }
    public int Grado => Atributos.Count;

    // Posiciones de las columnas que coinciden con la referencia (a  ó  R.a).
    public List<int> Coincidencias(AtributoRef r) {
        var res = new List<int>();
        for (int i = 0; i < Atributos.Count; i++)
            if (Semantico.Iguales(Atributos[i].Nombre, r.Nombre) &&
                (r.Relacion == null || Semantico.Iguales(Atributos[i].Calificador, r.Relacion)))
                res.Add(i);
        return res;
    }

    // Posiciones de las columnas que se llaman así, sin mirar el calificador.
    public List<int> ConNombre(string nombre) =>
        Enumerable.Range(0, Atributos.Count).Where(i => Semantico.Iguales(Atributos[i].Nombre, nombre)).ToList();

    public string Nombres() => string.Join(", ", Atributos.Select(a => a.NombreCompleto));
    public override string ToString() => "(" + string.Join(", ", Atributos) + ")";
}

public class Semantico {
    public List<string> Errores = new List<string>();

    // Tabla de símbolos: nombre de tabla o variable → su esquema y dónde se definió.
    // El esquema puede ser null si la definición tuvo errores: el nombre igual queda
    // definido, así sus usos no dan "no está definida".
    class Simbolo { public Esquema Esquema; public int Linea; }
    readonly Dictionary<string, Simbolo> simbolos = new Dictionary<string, Simbolo>(StringComparer.OrdinalIgnoreCase);

    // Tipos que se pueden declarar en el encabezado de una tabla (balance:number).
    // Las fechas de la cátedra son cadenas, así que date se trata como string.
    static readonly Dictionary<string, Dominio> tiposDeclarables = new Dictionary<string, Dominio> {
        { "number", Dominio.Numero }, { "string", Dominio.Cadena },
        { "boolean", Dominio.Booleano }, { "date", Dominio.Cadena },
    };

    public static bool Iguales(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    public static string NombreDominio(Dominio d) => d switch {
        Dominio.Numero => "número", Dominio.Cadena => "cadena",
        Dominio.Booleano => "booleano", _ => "desconocido",
    };

    // Desconocido (columna con solo null) es compatible con todo.
    static bool Compatibles(Dominio a, Dominio b) => a == b || a == Dominio.Desconocido || b == Dominio.Desconocido;
    static Dominio Combinar(Dominio a, Dominio b) => a == Dominio.Desconocido ? b : a;

    void Error(int linea, int col, string msg) => Errores.Add($"-- línea {linea} col {col}: {msg}");
    void Error(Nodo n, string msg) => Error(n.Linea, n.Col, msg);

    /*----------------------------- Programa -------------------------------*/

    // Se permiten varias consultas en un mismo programa: cada una es un resultado.
    public void Analizar(Programa p) {
        foreach (var ins in p.Instrucciones) {
            if (ins is Asignacion a) {
                // Primero la expresión y después el nombre: así "r = σ ... (r)" da
                // "no está definida", porque una relación no se puede usar antes de definirla.
                Esquema e = Analizar(a.Expr, a.Nombre);
                if (simbolos.TryGetValue(a.Nombre, out var previo))
                    Error(a, $"'{a.Nombre}' ya está definida (en la línea {previo.Linea})");
                else
                    simbolos[a.Nombre] = new Simbolo { Esquema = e, Linea = a.Linea };
            } else {
                Analizar(ins.Expr, null);
            }
        }
    }

    // Calcula el esquema de e y lo guarda en el nodo. nombre solo se usa cuando e
    // es una tabla con datos asignada a una variable (para calificar sus columnas).
    Esquema Analizar(ExpRel e, string nombre) {
        Esquema s = e switch {
            Relacion r     => AnalizarRelacion(r),
            TablaDatos t   => AnalizarTabla(t, nombre),
            Seleccion sel  => AnalizarSeleccion(sel),
            Proyeccion p   => AnalizarProyeccion(p),
            RenombreRel rr => AnalizarRenombreRel(rr),
            RenombreAttr ra => AnalizarRenombreAttr(ra),
            Agrupamiento g => AnalizarAgrupamiento(g),
            OpBinaria b    => AnalizarBinaria(b),
            _ => throw new InvalidOperationException("nodo desconocido: " + e.GetType().Name),
        };
        e.Esquema = s;
        return s;
    }

    /*------------------------------- Hojas --------------------------------*/

    Esquema AnalizarRelacion(Relacion r) {
        if (!simbolos.TryGetValue(r.Nombre, out var sim)) {
            Error(r, $"la relación '{r.Nombre}' no está definida");
            return null;
        }
        return sim.Esquema;
    }

    // { encabezado filas }: el dominio de cada columna es el declarado o, si no
    // se declaró, el del primer valor que no sea null. Los errores en las filas
    // no impiden calcular el esquema, así que la tabla siempre devuelve uno.
    Esquema AnalizarTabla(TablaDatos t, string nombre) {
        var attrs = new List<Atributo>();
        foreach (var col in t.Columnas) {
            if (col.Attr.Relacion != null)
                Error(col.Attr, $"la columna '{col.Attr}' no puede llevar calificador: toma el nombre de la tabla");
            if (attrs.Any(a => Iguales(a.Nombre, col.Attr.Nombre)))
                Error(col.Attr, $"la columna '{col.Attr.Nombre}' está repetida");
            Dominio tipo = Dominio.Desconocido;
            if (col.Tipo != null) {
                if (tiposDeclarables.ContainsKey(col.Tipo)) tipo = tiposDeclarables[col.Tipo];
                else Error(col.Attr, $"tipo '{col.Tipo}' desconocido (se acepta number, string, boolean o date)");
            }
            attrs.Add(new Atributo(nombre, col.Attr.Nombre, tipo));
        }

        foreach (var fila in t.Filas) {
            if (fila.Count != attrs.Count) {
                Error(fila[0], $"la fila tiene {Cantidad(fila.Count, "valor", "valores")}, " +
                               $"pero la tabla tiene {Cantidad(attrs.Count, "columna", "columnas")}");
                continue;
            }
            for (int i = 0; i < fila.Count; i++) {
                Dominio? d = DominioDe(fila[i]);
                if (d == null) continue;                               // null: entra en cualquier columna
                if (attrs[i].Tipo == Dominio.Desconocido)
                    attrs[i].Tipo = d.Value;                           // el primer valor fija el dominio
                else if (attrs[i].Tipo != d.Value)
                    Error(fila[i], $"el valor {fila[i]} es {NombreDominio(d.Value)}, " +
                                   $"pero la columna '{attrs[i].Nombre}' es {NombreDominio(attrs[i].Tipo)}");
            }
        }
        return new Esquema(attrs);
    }

    // Dominio de un valor de una fila; null para el valor null.
    Dominio? DominioDe(Constante c) {
        if (c.Tipo == TipoDato.Numero) return Dominio.Numero;
        if (c.Tipo == TipoDato.Cadena) return Dominio.Cadena;
        if (Iguales(c.Valor, "null")) return null;
        if (Iguales(c.Valor, "true") || Iguales(c.Valor, "false")) return Dominio.Booleano;
        Error(c, $"'{c.Valor}' no es un valor válido (las cadenas van entre comillas simples)");
        return null;
    }

    static string Cantidad(int n, string singular, string plural) => n + " " + (n == 1 ? singular : plural);

    /*--------------------------- Operadores unarios -----------------------*/

    // σ: el esquema no cambia; solo se chequea la condición.
    Esquema AnalizarSeleccion(Seleccion s) {
        Esquema hijo = Analizar(s.Hijo, null);
        if (hijo == null) return null;
        ChequearCondicion(s.Cond, hijo);
        return hijo;
    }

    // π: las columnas elegidas, en ese orden. Con alias, la columna cambia de
    // nombre y conserva el calificador (π cuil → id (alumno) da alumno.id).
    Esquema AnalizarProyeccion(Proyeccion p) {
        Esquema hijo = Analizar(p.Hijo, null);
        if (hijo == null) return null;
        int antes = Errores.Count;
        var attrs = new List<Atributo>();
        foreach (var item in p.Items) {
            int i = Resolver(item.Attr, hijo);
            if (i < 0) continue;
            attrs.Add(item.Alias == null ? hijo.Atributos[i] : hijo.Atributos[i].ConNombre(item.Alias));
        }
        if (Errores.Count != antes || !SinRepetidos(attrs, p, "π")) return null;
        return new Esquema(attrs);
    }

    // ρ S: todas las columnas pasan a tener calificador S.
    Esquema AnalizarRenombreRel(RenombreRel r) {
        Esquema hijo = Analizar(r.Hijo, null);
        if (hijo == null) return null;
        var attrs = hijo.Atributos.Select(a => a.ConCalificador(r.NuevoNombre)).ToList();
        return SinRepetidos(attrs, r, "ρ " + r.NuevoNombre) ? new Esquema(attrs) : null;
    }

    // ρ nuevo←viejo: el atributo viejo tiene que existir en el hijo.
    Esquema AnalizarRenombreAttr(RenombreAttr r) {
        Esquema hijo = Analizar(r.Hijo, null);
        if (hijo == null) return null;
        int antes = Errores.Count;
        var attrs = new List<Atributo>(hijo.Atributos);
        var renombrados = new HashSet<int>();
        foreach (var par in r.Pares) {
            int i = Resolver(par.Viejo, hijo);
            if (i < 0) continue;
            if (!renombrados.Add(i))
                Error(par.Viejo, $"el atributo '{par.Viejo}' se renombra dos veces");
            else
                attrs[i] = attrs[i].ConNombre(par.Nuevo);
        }
        if (Errores.Count != antes || !SinRepetidos(attrs, r, "ρ")) return null;
        return new Esquema(attrs);
    }

    // γ: las columnas de agrupamiento y después una columna por cada agregado
    // (sin calificador, con el nombre del alias).
    Esquema AnalizarAgrupamiento(Agrupamiento g) {
        Esquema hijo = Analizar(g.Hijo, null);
        if (hijo == null) return null;
        int antes = Errores.Count;
        var attrs = new List<Atributo>();
        foreach (var a in g.Grupo) {
            int i = Resolver(a, hijo);
            if (i >= 0) attrs.Add(hijo.Atributos[i]);
        }
        foreach (var f in g.Agregados) {
            Dominio tipo = Dominio.Numero;                         // count, sum y avg dan un número
            if (f.Attr == null) {
                if (f.Funcion != "count")
                    Error(f.Linea, f.Col, $"{f.Funcion}(*) no está permitido: solo count acepta *");
            } else {
                int i = Resolver(f.Attr, hijo);
                if (i < 0) continue;
                Dominio d = hijo.Atributos[i].Tipo;
                if ((f.Funcion == "sum" || f.Funcion == "avg") && !Compatibles(d, Dominio.Numero))
                    Error(f.Linea, f.Col, $"{f.Funcion} solo se aplica a atributos numéricos, " +
                                          $"y '{f.Attr}' es {NombreDominio(d)}");
                if (f.Funcion == "min" || f.Funcion == "max") tipo = d;
            }
            attrs.Add(new Atributo(null, f.Alias, tipo));
        }
        if (Errores.Count != antes || !SinRepetidos(attrs, g, "γ")) return null;
        return new Esquema(attrs);
    }

    /*--------------------------- Operadores binarios ----------------------*/

    Esquema AnalizarBinaria(OpBinaria b) {
        Esquema izq = Analizar(b.Izq, null);
        Esquema der = Analizar(b.Der, null);
        if (izq == null || der == null) return null;

        switch (b.Op) {
            case TipoOp.Union: case TipoOp.Interseccion: case TipoOp.Diferencia:
                return UnionCompatibles(b, izq, der);
            case TipoOp.Producto:
                return Concatenar(b, izq, der);
            case TipoOp.Division:
                return Division(b, izq, der);
            default:                                               // los cuatro joins
                if (b.Cond == null) return JoinNatural(b, izq, der);
                Esquema s = Concatenar(b, izq, der);               // theta-join: × y después σ
                if (s != null) ChequearCondicion(b.Cond, s);
                return s;
        }
    }

    // ∪ ∩ −: mismo grado y mismos dominios, posición por posición. El resultado
    // toma los nombres de la relación izquierda.
    Esquema UnionCompatibles(OpBinaria b, Esquema izq, Esquema der) {
        if (izq.Grado != der.Grado) {
            Error(b, $"{b.Simbolo} exige relaciones unión compatibles: la izquierda tiene grado {izq.Grado} " +
                     $"({izq.Nombres()}) y la derecha grado {der.Grado} ({der.Nombres()})");
            return null;
        }
        var attrs = new List<Atributo>();
        bool ok = true;
        for (int i = 0; i < izq.Grado; i++) {
            Atributo a = izq.Atributos[i], d = der.Atributos[i];
            if (!Compatibles(a.Tipo, d.Tipo)) {
                Error(b, $"{b.Simbolo} exige relaciones unión compatibles: la columna {i + 1} es " +
                         $"{NombreDominio(a.Tipo)} a la izquierda ('{a.NombreCompleto}') y " +
                         $"{NombreDominio(d.Tipo)} a la derecha ('{d.NombreCompleto}')");
                ok = false;
            }
            attrs.Add(new Atributo(a.Calificador, a.Nombre, Combinar(a.Tipo, d.Tipo)));
        }
        return ok ? new Esquema(attrs) : null;
    }

    // × y theta-join: las columnas de la izquierda y después las de la derecha.
    // Si una columna aparece igual en los dos lados (R × R), no habría forma de
    // nombrarla: hay que renombrar una de las relaciones con ρ.
    Esquema Concatenar(OpBinaria b, Esquema izq, Esquema der) {
        var repetidas = der.Atributos.Select(d => d.NombreCompleto)
                           .Where(n => izq.Atributos.Any(a => Iguales(a.NombreCompleto, n))).ToList();
        if (repetidas.Count > 0) {
            Error(b, $"{b.Simbolo}: las dos relaciones tienen " +
                     (repetidas.Count == 1 ? "la columna " : "las columnas ") + string.Join(", ", repetidas) +
                     "; renombrá una de ellas con ρ");
            return null;
        }
        return new Esquema(izq.Atributos.Concat(der.Atributos));
    }

    // Join natural: iguala los atributos que se llaman igual en los dos lados
    // (tiene que haber al menos uno). Esos atributos aparecen una sola vez.
    Esquema JoinNatural(OpBinaria b, Esquema izq, Esquema der) {
        var comunes = izq.Atributos.Select(a => a.Nombre)
                         .Where(n => der.ConNombre(n).Count > 0)
                         .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (comunes.Count == 0) {
            Error(b, $"{b.Simbolo}: el join natural necesita al menos un atributo en común, y no hay ninguno " +
                     $"entre ({izq.Nombres()}) y ({der.Nombres()})");
            return null;
        }
        var attrs = new List<Atributo>(izq.Atributos);
        bool ok = true;
        foreach (var n in comunes) {
            List<int> li = izq.ConNombre(n), ld = der.ConNombre(n);
            if (li.Count > 1 || ld.Count > 1) {
                var lado = li.Count > 1 ? izq : der;
                Error(b, $"{b.Simbolo}: en el join natural el atributo '{n}' aparece más de una vez en la relación " +
                         $"{(li.Count > 1 ? "izquierda" : "derecha")} ({lado.Nombres()}); renombralo con ρ o proyectá");
                ok = false;
                continue;
            }
            Atributo a = izq.Atributos[li[0]], d = der.Atributos[ld[0]];
            if (!Compatibles(a.Tipo, d.Tipo)) {
                Error(b, $"{b.Simbolo}: el atributo común '{n}' es {NombreDominio(a.Tipo)} a la izquierda " +
                         $"y {NombreDominio(d.Tipo)} a la derecha");
                ok = false;
            }
            attrs[li[0]] = new Atributo(a.Calificador, a.Nombre, Combinar(a.Tipo, d.Tipo));
        }
        attrs.AddRange(der.Atributos.Where(d => !comunes.Contains(d.Nombre, StringComparer.OrdinalIgnoreCase)));
        return ok ? new Esquema(attrs) : null;
    }

    // R ÷ S: los atributos de S tienen que estar en R. El resultado son los
    // atributos de R que no están en S (y tiene que quedar alguno).
    Esquema Division(OpBinaria b, Esquema izq, Esquema der) {
        bool ok = true;
        foreach (var d in der.Atributos) {
            List<int> li = izq.ConNombre(d.Nombre);
            if (li.Count == 0) {
                Error(b, $"÷: el atributo '{d.Nombre}' del divisor no está en el dividendo ({izq.Nombres()})");
                ok = false;
            } else if (li.Count > 1) {
                Error(b, $"÷: el atributo '{d.Nombre}' aparece más de una vez en el dividendo ({izq.Nombres()})");
                ok = false;
            } else if (!Compatibles(izq.Atributos[li[0]].Tipo, d.Tipo)) {
                Error(b, $"÷: el atributo '{d.Nombre}' es {NombreDominio(izq.Atributos[li[0]].Tipo)} en el dividendo " +
                         $"y {NombreDominio(d.Tipo)} en el divisor");
                ok = false;
            }
        }
        if (!ok) return null;
        var z = izq.Atributos.Where(a => der.ConNombre(a.Nombre).Count == 0).ToList();
        if (z.Count == 0) {
            Error(b, "÷: el dividendo no tiene atributos fuera de los del divisor, así que el resultado no tendría columnas");
            return null;
        }
        return new Esquema(z);
    }

    /*------------------------------ Condiciones ---------------------------*/

    void ChequearCondicion(Condicion c, Esquema s) {
        switch (c) {
            case CondLogica l: ChequearCondicion(l.Izq, s); ChequearCondicion(l.Der, s); break;
            case Negacion n:   ChequearCondicion(n.Hijo, s); break;
            case Comparacion cmp:
                Dominio? a = DominioDe(cmp.Izq, s), d = DominioDe(cmp.Der, s);
                if (a != null && d != null && !Compatibles(a.Value, d.Value))
                    Error(cmp, $"no se puede comparar {cmp.Izq} ({NombreDominio(a.Value)}) " +
                               $"con {cmp.Der} ({NombreDominio(d.Value)})");
                break;
        }
    }

    // Dominio de un lado de una comparación; null si el atributo no se encontró.
    Dominio? DominioDe(TerminoCond t, Esquema s) {
        if (t is Constante c) return c.Tipo == TipoDato.Numero ? Dominio.Numero : Dominio.Cadena;
        int i = Resolver((AtributoRef)t, s);
        return i < 0 ? (Dominio?)null : s.Atributos[i].Tipo;
    }

    /*------------------------------- Auxiliares ---------------------------*/

    // Busca el atributo en el esquema y devuelve su posición, o -1 si no existe
    // o es ambiguo (en ese caso informa el error).
    int Resolver(AtributoRef r, Esquema s) {
        var c = s.Coincidencias(r);
        if (c.Count == 1) return c[0];
        if (c.Count == 0)
            Error(r, $"el atributo '{r}' no existe; los disponibles son: {s.Nombres()}");
        else
            Error(r, $"el atributo '{r}' es ambiguo: puede ser " +
                     string.Join(" o ", c.Select(i => s.Atributos[i].NombreCompleto)) + "; calificalo");
        return -1;
    }

    // Un esquema no puede tener dos veces la misma columna: después no se la
    // podría nombrar sin ambigüedad.
    bool SinRepetidos(List<Atributo> attrs, Nodo n, string operador) {
        for (int i = 0; i < attrs.Count; i++)
            for (int j = 0; j < i; j++)
                if (Iguales(attrs[i].NombreCompleto, attrs[j].NombreCompleto)) {
                    Error(n, $"{operador}: la columna '{attrs[i].NombreCompleto}' quedaría repetida");
                    return false;
                }
        return true;
    }
}
