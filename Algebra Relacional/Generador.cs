using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/*============================================================================
  Generación de SQL (etapa 4). Destino: SQLite (≥ 3.39, por RIGHT/FULL JOIN).

  Recorre el AST ya validado por el semántico y arma un script:
    - tabla con datos asignada    → CREATE TABLE + INSERT INTO
    - otra asignación (variable)  → CTE (WITH nombre AS (...)) en cada consulta que la usa
    - consulta                    → SELECT ... ORDER BY ...;

  LA REGLA QUE HACE FUNCIONAR TODO: el SQL de cada nodo es un SELECT cuyas
  columnas son exactamente las de su esquema, en el mismo orden, y cada una
  se llama con su nombre completo entre comillas ("alumno.cuil"). Así, después
  de un join, alumno.cuil y examen.cuil siguen siendo dos columnas distintas,
  y el nodo de arriba siempre sabe cómo se llama cada columna de su hijo.
  Solo el SELECT final de cada consulta pone nombres cortos (cuil) cuando no
  hay ambigüedad.

  Cada hijo se usa como subconsulta con un alias único: (SELECT ...) AS t1.
============================================================================*/

public class Generador {
    int contador = 0;                                          // para los alias t1, t2, ...
    readonly HashSet<string> tablas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, Asignacion> variables = new Dictionary<string, Asignacion>(StringComparer.OrdinalIgnoreCase);
    readonly List<string> ordenVariables = new List<string>();  // en orden de definición

    public string Generar(Programa p) {
        var sb = new StringBuilder();
        foreach (var ins in p.Instrucciones) {
            if (ins is Asignacion a && a.Expr is TablaDatos t) {
                sb.AppendLine(CrearTabla(a.Nombre, t));
                tablas.Add(a.Nombre);
            } else if (ins is Asignacion v) {
                variables[v.Nombre] = v;                       // se genera dentro de las consultas que la usan
                ordenVariables.Add(v.Nombre);
            } else {
                sb.AppendLine($"-- Consulta (línea {ins.Linea})");
                sb.AppendLine(Consulta(ins.Expr));
            }
        }
        return sb.ToString();
    }

    /*------------------------------ Sentencias ----------------------------*/

    string CrearTabla(string nombre, TablaDatos t) {
        var sb = new StringBuilder();
        var cols = t.Esquema.Atributos.Select(a => "    " + (Id(a.Nombre) + " " + TipoSql(a.Tipo)).TrimEnd());
        sb.AppendLine($"CREATE TABLE {Id(nombre)} (\n{string.Join(",\n", cols)}\n);");
        if (t.Filas.Count > 0) {
            var filas = t.Filas.Select(f => "    (" + string.Join(", ", f.Select(Valor)) + ")");
            sb.AppendLine($"INSERT INTO {Id(nombre)} VALUES\n{string.Join(",\n", filas)};");
        }
        return sb.ToString();
    }

    // WITH (las variables que usa, en orden de definición) + SELECT final con
    // nombres cortos y ORDER BY, para que el resultado salga siempre igual.
    string Consulta(ExpRel e) {
        var sb = new StringBuilder();
        var usadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        VariablesUsadas(e, usadas);
        var ctes = ordenVariables.Where(usadas.Contains)
                                 .Select(n => $"{Id(n)} AS (\n{Indentar(Expr(variables[n].Expr))}\n)").ToList();
        if (ctes.Count > 0) sb.AppendLine("WITH " + string.Join(",\n", ctes));

        string alias = NuevoAlias();
        var attrs = e.Esquema.Atributos;
        var cols = attrs.Select(a => $"{Ref(alias, a)} AS {Id(NombreFinal(a, attrs))}");
        sb.AppendLine($"SELECT {string.Join(", ", cols)}");
        sb.AppendLine($"FROM (\n{Indentar(Expr(e))}\n) AS {alias}");
        sb.Append("ORDER BY " + string.Join(", ", Enumerable.Range(1, attrs.Count)) + ";");
        sb.AppendLine();
        return sb.ToString();
    }

    // En el resultado final la columna se llama "cuil" si es la única con ese
    // nombre, y "alumno.cuil" si hay otra (por ejemplo, después de un theta-join).
    static string NombreFinal(Atributo a, List<Atributo> todos) =>
        todos.Count(x => Semantico.Iguales(x.Nombre, a.Nombre)) == 1 ? a.Nombre : a.NombreCompleto;

    // Las variables que usa e, directamente o a través de otras variables.
    void VariablesUsadas(ExpRel e, HashSet<string> usadas) {
        if (e is Relacion r && variables.ContainsKey(r.Nombre) && usadas.Add(r.Nombre))
            VariablesUsadas(variables[r.Nombre].Expr, usadas);
        foreach (var h in e.Hijos()) VariablesUsadas(h, usadas);
    }

    /*----------------------------- Expresiones ----------------------------*/

    // El SELECT de un nodo: sus columnas son las del esquema, con nombre completo.
    string Expr(ExpRel e) => e switch {
        Relacion r      => GenRelacion(r),
        TablaDatos t    => GenTablaDatos(t),
        Seleccion s     => GenSeleccion(s),
        Proyeccion p    => GenProyeccion(p),
        RenombreRel rr  => GenRenombre(rr),
        RenombreAttr ra => GenRenombre(ra),
        Agrupamiento g  => GenAgrupamiento(g),
        OpBinaria b     => GenBinaria(b),
        _ => throw new InvalidOperationException("nodo desconocido: " + e.GetType().Name),
    };

    // Tabla: sus columnas se guardan con el nombre corto, así que acá se les pone
    // el nombre completo. Variable: el CTE ya tiene los nombres completos.
    string GenRelacion(Relacion r) {
        if (!tablas.Contains(r.Nombre)) return $"SELECT * FROM {Id(r.Nombre)}";
        var cols = r.Esquema.Atributos.Select(a => $"{Id(a.Nombre)} AS {Id(a.NombreCompleto)}");
        return $"SELECT {string.Join(", ", cols)} FROM {Id(r.Nombre)}";
    }

    // Tabla con datos dentro de una expresión (sin asignar): una fila por SELECT.
    string GenTablaDatos(TablaDatos t) {
        var attrs = t.Esquema.Atributos;
        if (t.Filas.Count == 0)
            return "SELECT " + string.Join(", ", attrs.Select(a => "NULL AS " + Id(a.NombreCompleto))) + " WHERE 0";
        var filas = t.Filas.Select((f, n) => "SELECT " + string.Join(", ",
                        f.Select((v, i) => n == 0 ? Valor(v) + " AS " + Id(attrs[i].NombreCompleto) : Valor(v))));
        return string.Join("\nUNION ALL\n", filas);
    }

    string GenSeleccion(Seleccion s) {
        string alias = NuevoAlias();
        string sub = Sub(s.Hijo, alias);
        return $"SELECT * FROM {sub}\nWHERE {Cond(s.Cond, s.Hijo.Esquema, Refs(alias, s.Hijo.Esquema))}";
    }

    // Siempre DISTINCT: el álgebra relacional trabaja con conjuntos.
    string GenProyeccion(Proyeccion p) {
        string alias = NuevoAlias();
        string sub = Sub(p.Hijo, alias);
        var hijo = p.Hijo.Esquema;
        var cols = p.Items.Select((item, i) =>
            $"{Ref(alias, hijo.Atributos[Posicion(item.Attr, hijo)])} AS {Id(p.Esquema.Atributos[i].NombreCompleto)}");
        return $"SELECT DISTINCT {string.Join(", ", cols)}\nFROM {sub}";
    }

    // ρ S y ρ b←a: la columna i del hijo pasa a llamarse como la columna i del nodo.
    string GenRenombre(OpUnaria r) {
        string alias = NuevoAlias();
        string sub = Sub(r.Hijo, alias);
        var cols = r.Hijo.Esquema.Atributos.Select((a, i) => $"{Ref(alias, a)} AS {Id(r.Esquema.Atributos[i].NombreCompleto)}");
        return $"SELECT {string.Join(", ", cols)}\nFROM {sub}";
    }

    // γ: sin atributos de agrupamiento la consulta no lleva GROUP BY.
    string GenAgrupamiento(Agrupamiento g) {
        string alias = NuevoAlias();
        string sub = Sub(g.Hijo, alias);
        var hijo = g.Hijo.Esquema;
        var salida = g.Esquema.Atributos;
        var grupo = g.Grupo.Select(a => Ref(alias, hijo.Atributos[Posicion(a, hijo)])).ToList();
        var cols = grupo.Select((r, i) => $"{r} AS {Id(salida[i].NombreCompleto)}").ToList();
        for (int i = 0; i < g.Agregados.Count; i++) {
            var f = g.Agregados[i];
            string arg = f.Attr == null ? "*" : Ref(alias, hijo.Atributos[Posicion(f.Attr, hijo)]);
            cols.Add($"{f.Funcion.ToUpper()}({arg}) AS {Id(salida[grupo.Count + i].NombreCompleto)}");
        }
        string sql = $"SELECT {string.Join(", ", cols)}\nFROM {sub}";
        return grupo.Count == 0 ? sql : sql + "\nGROUP BY " + string.Join(", ", grupo);
    }

    string GenBinaria(OpBinaria b) {
        switch (b.Op) {
            case TipoOp.Union:        return Conjunto(b, "UNION");
            case TipoOp.Interseccion: return Conjunto(b, "INTERSECT");
            case TipoOp.Diferencia:   return Conjunto(b, "EXCEPT");
            case TipoOp.Division:     return Division(b);
        }
        string a1 = NuevoAlias(), a2 = NuevoAlias();
        string izq = Sub(b.Izq, a1), der = Sub(b.Der, a2);
        Esquema ei = b.Izq.Esquema, ed = b.Der.Esquema;

        if (b.Op == TipoOp.Producto || b.Cond != null) {
            // × y theta-join: todas las columnas de los dos lados.
            var refs = Refs(a1, ei).Concat(Refs(a2, ed)).ToList();
            var cols = refs.Select((r, i) => $"{r} AS {Id(b.Esquema.Atributos[i].NombreCompleto)}");
            string sel = $"SELECT {string.Join(", ", cols)}\nFROM {izq}\n";
            if (b.Op == TipoOp.Producto) return sel + $"CROSS JOIN {der}";
            var juntas = new Esquema(ei.Atributos.Concat(ed.Atributos));
            return sel + $"{TipoJoin(b.Op)} {der}\nON {Cond(b.Cond, juntas, refs)}";
        }

        // Join natural. No se usa NATURAL JOIN de SQL porque las columnas tienen
        // nombres completos distintos ("alumno.cuil" y "examen.cuil"): la
        // igualdad de los atributos comunes se escribe a mano en el ON.
        var iguales = new List<string>();
        var columnas = new List<string>();
        var comunesDer = new HashSet<int>();
        foreach (var a in ei.Atributos) {
            var enDer = ed.ConNombre(a.Nombre);
            string ri = Ref(a1, a);
            if (enDer.Count == 0) { columnas.Add(ri); continue; }
            string rd = Ref(a2, ed.Atributos[enDer[0]]);
            comunesDer.Add(enDer[0]);
            iguales.Add($"{ri} = {rd}");
            // En ⟖ y ⟗ la fila puede venir solo de la derecha: el valor común sale de donde haya.
            columnas.Add(b.Op == TipoOp.JoinDer || b.Op == TipoOp.JoinCompleto ? $"COALESCE({ri}, {rd})" : ri);
        }
        for (int i = 0; i < ed.Grado; i++)
            if (!comunesDer.Contains(i)) columnas.Add(Ref(a2, ed.Atributos[i]));
        var lista = columnas.Select((c, i) => $"{c} AS {Id(b.Esquema.Atributos[i].NombreCompleto)}");
        return $"SELECT {string.Join(", ", lista)}\nFROM {izq}\n{TipoJoin(b.Op)} {der}\nON {string.Join(" AND ", iguales)}";
    }

    static string TipoJoin(TipoOp op) => op switch {
        TipoOp.JoinIzq => "LEFT JOIN", TipoOp.JoinDer => "RIGHT JOIN",
        TipoOp.JoinCompleto => "FULL JOIN", _ => "JOIN",
    };

    // ∪ ∩ −: en SQLite los miembros de un UNION no pueden ir entre paréntesis,
    // así que cada lado es SELECT * FROM (...). Las columnas del resultado toman
    // los nombres del lado izquierdo, igual que en el esquema.
    string Conjunto(OpBinaria b, string op) {
        string izq = Sub(b.Izq, NuevoAlias()), der = Sub(b.Der, NuevoAlias());
        return $"SELECT * FROM {izq}\n{op}\nSELECT * FROM {der}";
    }

    // R ÷ S no existe en SQL. Se usa la equivalencia de la cátedra:
    //     R ÷ S = π_Z(R) − π_Z( (π_Z(R) × S) − R )
    // donde Z son los atributos de R que no están en S. Como EXCEPT compara por
    // posición, en la resta interna R se reordena como (Z, atributos de S).
    string Division(OpBinaria b) {
        Esquema r = b.Izq.Esquema, s = b.Der.Esquema;
        var z = b.Esquema.Atributos;
        var deR = s.Atributos.Select(a => r.Atributos[r.ConNombre(a.Nombre)[0]]).ToList();  // columnas de R que están en S

        string ProyZ() {                                        // SELECT DISTINCT Z FROM R
            string al = NuevoAlias();
            string sub = Sub(b.Izq, al);
            return $"SELECT DISTINCT {string.Join(", ", z.Select(a => $"{Ref(al, a)} AS {Id(a.NombreCompleto)}"))}\nFROM {sub}";
        }

        string aZ = NuevoAlias(), aS = NuevoAlias();
        string producto = $"SELECT {string.Join(", ", z.Select(a => Ref(aZ, a)).Concat(s.Atributos.Select(a => Ref(aS, a))))}\n" +
                          $"FROM (\n{Indentar(ProyZ())}\n) AS {aZ}\nCROSS JOIN {Sub(b.Der, aS)}";
        string aR = NuevoAlias();
        string rOrdenada = $"SELECT {string.Join(", ", z.Concat(deR).Select(a => Ref(aR, a)))}\nFROM {Sub(b.Izq, aR)}";
        string aF = NuevoAlias();
        string faltantes = $"SELECT {string.Join(", ", z.Select(a => Ref(aF, a)))}\n" +
                           $"FROM (\n{Indentar(producto + "\nEXCEPT\n" + rOrdenada)}\n) AS {aF}";
        return ProyZ() + "\nEXCEPT\n" + faltantes;
    }

    /*------------------------------ Condiciones ---------------------------*/

    // refs[i] es cómo se escribe en SQL la columna i del esquema s.
    string Cond(Condicion c, Esquema s, List<string> refs) => c switch {
        CondLogica l  => $"({Cond(l.Izq, s, refs)} {l.Op.ToUpper()} {Cond(l.Der, s, refs)})",
        Negacion n    => $"NOT ({Cond(n.Hijo, s, refs)})",
        Comparacion k => $"{Termino(k.Izq, s, refs)} {k.Op} {Termino(k.Der, s, refs)}",
        _ => throw new InvalidOperationException("condición desconocida"),
    };

    string Termino(TerminoCond t, Esquema s, List<string> refs) =>
        t is Constante c ? Valor(c) : refs[Posicion((AtributoRef)t, s)];

    /*------------------------------- Auxiliares ---------------------------*/

    string NuevoAlias() => "t" + (++contador);

    // (SELECT ...) AS tN, con el SELECT del hijo indentado.
    string Sub(ExpRel e, string alias) => $"(\n{Indentar(Expr(e))}\n) AS {alias}";

    // El semántico ya verificó que la referencia existe y no es ambigua.
    static int Posicion(AtributoRef r, Esquema s) => s.Coincidencias(r)[0];

    static List<string> Refs(string alias, Esquema s) => s.Atributos.Select(a => Ref(alias, a)).ToList();
    static string Ref(string alias, Atributo a) => alias + "." + Id(a.NombreCompleto);

    // Identificador SQL entre comillas dobles: "alumno.cuil".
    static string Id(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";

    static string Valor(Constante c) {
        if (c.Tipo == TipoDato.Numero) return c.Valor;
        if (c.Tipo == TipoDato.Cadena) return "'" + c.Valor.Replace("'", "''") + "'";
        return c.Valor.ToUpper();                              // NULL, TRUE, FALSE
    }

    static string TipoSql(Dominio d) => d switch {
        Dominio.Numero => "NUMERIC", Dominio.Cadena => "TEXT", Dominio.Booleano => "BOOLEAN", _ => "",
    };

    static string Indentar(string s) => string.Join("\n", s.Split('\n').Select(l => "    " + l));
}
