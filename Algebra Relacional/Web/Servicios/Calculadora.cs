namespace ARWeb.Servicios;

// Todo lo que la app necesita del compilador, sin nada de interfaz ni de
// JavaScript (por eso se puede probar con PruebasWeb/).
//
// El dataset y la consulta se escriben en dos editores, pero el compilador
// recibe un solo programa: dataset + "\n" + consulta. Acá se separan otra vez
// los errores (¿son del dataset o de la consulta?) y se ajustan sus líneas.

public enum Origen { Dataset, Consulta }

public record ErrorVista(Origen Origen, int Linea, int Col, string Mensaje);

// El resultado de una consulta, como lo devuelve ejecutarSql (wwwroot/js/app.js).
public class TablaSql {
    public List<string> Columnas { get; set; } = new();
    public List<List<string>> Filas { get; set; } = new();
}

public class RespuestaSql {
    public bool Ok { get; set; }
    public string Error { get; set; }
    public List<TablaSql> Resultados { get; set; } = new();
}

// Una tabla (o variable) definida en el dataset.
public record TablaDataset(string Nombre, bool EsVariable, List<Atributo> Atributos, List<List<string>> Filas);

public class AnalisisDataset {
    public string Texto;
    public List<ErrorVista> Errores = new();
    public List<TablaDataset> Tablas = new();
}

// Una consulta del programa, con el texto que escribió el alumno.
public record ConsultaCompilada(Consulta Nodo, string Texto, List<Asignacion> Asignaciones);

public class Compilacion {
    public List<ErrorVista> Errores = new();
    public string TituloErrores;
    public List<ConsultaCompilada> Consultas = new();
    public string Sql, Ast;
    public bool DatasetConErrores;
    public bool Ok => Errores.Count == 0;
}

public static class Calculadora {

    /*-------------------------------------------------------------- Dataset ---*/

    // Compila solo el dataset, para mostrar sus tablas (o sus errores).
    public static AnalisisDataset AnalizarDataset(string dataset) {
        var a = new AnalisisDataset { Texto = dataset ?? "" };
        var r = Compilador.Compilar(a.Texto);
        foreach (var e in r.Errores) a.Errores.Add(new ErrorVista(Origen.Dataset, e.Linea, e.Col, e.Mensaje));
        if (r.Errores.Count > 0) return a;
        foreach (var asig in r.Programa.Instrucciones.OfType<Asignacion>()) {
            var filas = asig.Expr is TablaDatos td
                ? td.Filas.Select(f => f.Select(TextoConstante).ToList()).ToList()
                : null;
            a.Tablas.Add(new TablaDataset(asig.Nombre, filas == null, asig.Expr.Esquema.Atributos, filas));
        }
        return a;
    }

    // Un valor de la tabla tal como se muestra (null = NULL de SQL).
    static string TextoConstante(Constante c) =>
        c.Tipo == TipoDato.Simbolo && c.Valor.Equals("null", StringComparison.OrdinalIgnoreCase) ? null : c.Valor;

    /*------------------------------------------------------------- Consulta ---*/

    public static Compilacion Compilar(string dataset, string consulta, AnalisisDataset analisis = null) {
        dataset ??= ""; consulta ??= "";
        var c = new Compilacion();

        // Si el dataset solo ya tiene errores, no tiene sentido mirar la consulta:
        // un error del dataset podría aparecer recién en las líneas de la consulta.
        analisis ??= AnalizarDataset(dataset);
        if (analisis.Errores.Count > 0) {
            c.Errores.AddRange(analisis.Errores);
            c.DatasetConErrores = true;
            c.TituloErrores = "El dataset tiene errores";
            return c;
        }

        string programa = dataset + "\n" + consulta;
        int lineasDataset = dataset.Split('\n').Length;
        var r = Compilador.Compilar(programa);
        c.Ast = r.Ast;

        if (r.Errores.Count > 0) {
            bool sintaxis = r.HayErroresDeSintaxis, varios = r.Errores.Count > 1;
            c.TituloErrores = sintaxis ? (varios ? "Errores de sintaxis" : "Error de sintaxis")
                                       : (varios ? "Errores semánticos" : "Error semántico");
            foreach (var e in r.Errores)
                c.Errores.Add(e.Linea <= lineasDataset
                    ? new ErrorVista(Origen.Dataset, e.Linea, e.Col, e.Mensaje)
                    : new ErrorVista(Origen.Consulta, e.Linea - lineasDataset, e.Col, e.Mensaje));
            return c;
        }

        c.Sql = r.Sql;
        var instr = r.Programa.Instrucciones;
        var lineas = programa.Split('\n');
        var asignaciones = new List<Asignacion>();
        for (int i = 0; i < instr.Count; i++) {
            if (instr[i] is Asignacion a) { asignaciones.Add(a); continue; }
            var siguiente = i + 1 < instr.Count ? instr[i + 1] : null;
            c.Consultas.Add(new ConsultaCompilada((Consulta)instr[i], TextoSentencia(lineas, instr[i], siguiente),
                                                  new List<Asignacion>(asignaciones)));
        }
        return c;
    }

    // El texto de una sentencia: desde donde empieza hasta donde empieza la
    // siguiente, sin los comentarios ni las líneas vacías del final (que son
    // de la sentencia siguiente) ni el ";".
    public static string TextoSentencia(string[] lineas, Instruccion ins, Instruccion siguiente) {
        int desdeL = ins.Linea - 1, desdeC = ins.Col - 1;
        int hastaL = siguiente != null ? siguiente.Linea - 1 : lineas.Length - 1;
        int hastaC = siguiente != null ? siguiente.Col - 1 : lineas[hastaL].Length;
        var partes = new List<string>();
        for (int l = desdeL; l <= hastaL && l < lineas.Length; l++) {
            string s = lineas[l].TrimEnd('\r');
            int fin = l == hastaL ? Math.Min(hastaC, s.Length) : s.Length;
            int ini = l == desdeL ? Math.Min(desdeC, fin) : 0;
            partes.Add(s.Substring(ini, fin - ini));
        }
        // Afuera las líneas finales vacías o que son solo comentario
        while (partes.Count > 1 && (partes[^1].Trim().Length == 0 || partes[^1].TrimStart().StartsWith("--")))
            partes.RemoveAt(partes.Count - 1);
        string texto = string.Join("\n", partes).Trim();
        return texto.EndsWith(";") ? texto[..^1].TrimEnd() : texto;
    }

    // Línea y columna (desde 1) → posición en el texto.
    public static int Posicion(string texto, int linea, int col) {
        int pos = 0;
        for (int l = 1; l < linea; l++) {
            int nl = texto.IndexOf('\n', pos);
            if (nl < 0) return texto.Length;
            pos = nl + 1;
        }
        return Math.Min(texto.Length, pos + Math.Max(0, col - 1));
    }

    /*--------------------------------------------------- Árbol paso a paso ---*/

    // Un nodo del árbol de operadores. Las variables se "abren": su hijo es la
    // expresión que tienen asignada (así se ve de dónde sale cada fila).
    public class NodoArbol {
        public int Id, Nivel;
        public ExpRel Expr;
        public string Etiqueta;
        public string Variable;                           // nombre, si el nodo es una variable
        public List<NodoArbol> Hijos = new();
        public double X;                                  // posición horizontal (en "casilleros")
    }

    public static List<NodoArbol> Arbol(ConsultaCompilada c) {
        var variables = new Dictionary<string, Asignacion>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in c.Asignaciones)
            if (a.Expr is not TablaDatos) variables[a.Nombre] = a;
        var nodos = new List<NodoArbol>();
        double hojas = 0;

        NodoArbol Armar(ExpRel e, int nivel) {
            var n = new NodoArbol { Id = nodos.Count, Nivel = nivel, Expr = e, Etiqueta = e.Etiqueta() };
            nodos.Add(n);
            if (e is TablaDatos) n.Etiqueta = "{ tabla }";
            var hijos = e.Hijos().ToList();
            if (e is Relacion r && variables.TryGetValue(r.Nombre, out var v)) {
                n.Variable = v.Nombre;
                hijos = new List<ExpRel> { v.Expr };
            }
            foreach (var h in hijos) n.Hijos.Add(Armar(h, nivel + 1));
            n.X = n.Hijos.Count == 0 ? hojas++ : n.Hijos.Average(h => h.X);
            return n;
        }
        Armar(c.Nodo.Expr, 0);
        return nodos;
    }

    // Un solo script con una consulta por nodo, en el mismo orden: se ejecuta
    // una vez y devuelve el resultado de cada paso. Se reutiliza el generador
    // tal cual: un programa con las asignaciones previas y una consulta por nodo.
    public static string ScriptPasos(ConsultaCompilada c, IEnumerable<ExpRel> nodos) {
        var p = new Programa();
        p.Instrucciones.AddRange(c.Asignaciones);
        foreach (var e in nodos) p.Instrucciones.Add(new Consulta(e, null));
        return new Generador().Generar(p);
    }

    // El SQL de un solo paso (para mostrarlo debajo del árbol).
    public static string SqlPaso(ConsultaCompilada c, ExpRel e) => ScriptPasos(c, new[] { e });

    // Qué hace cada operador, en palabras.
    public static string Describir(NodoArbol n) {
        var e = n.Expr;
        if (n.Variable != null) return $"La variable {n.Variable}: el resultado de su expresión (abajo).";
        return e switch {
            Relacion r => $"La relación {r.Nombre}, tal como está en el dataset.",
            TablaDatos => "Una tabla escrita dentro de la consulta.",
            Seleccion s => $"Selección: deja solo las filas que cumplen {s.Cond.Texto(true)}.",
            Proyeccion p => $"Proyección: se queda con {Lista(p.Items.Select(i => i.ToString()))}, sin filas repetidas.",
            RenombreRel rr => $"Renombre: la relación pasa a llamarse {rr.NuevoNombre} (sus atributos quedan como {rr.NuevoNombre}.atributo).",
            RenombreAttr ra => $"Renombre de atributos: {Lista(ra.Pares.Select(p => p.Viejo + " pasa a llamarse " + p.Nuevo))}.",
            Agrupamiento g => (g.Grupo.Count > 0 ? $"Agrupamiento: una fila por cada valor de {Lista(g.Grupo.Select(a => a.ToString()))}, con " : "Agrupamiento: una sola fila con ")
                              + Lista(g.Agregados.Select(a => a.ToString())) + ".",
            OpBinaria b => DescribirBinaria(b),
            _ => "",
        };
    }

    static string DescribirBinaria(OpBinaria b) {
        string cond = b.Cond == null ? "" : b.Cond.Texto(true);
        return b.Op switch {
            TipoOp.Union => "Unión: las filas que están en alguna de las dos relaciones.",
            TipoOp.Interseccion => "Intersección: las filas que están en las dos relaciones.",
            TipoOp.Diferencia => "Diferencia: las filas de la izquierda que no están en la derecha.",
            TipoOp.Producto => "Producto cartesiano: cada fila de la izquierda combinada con cada fila de la derecha.",
            TipoOp.Division => "División: los valores de la izquierda que aparecen combinados con todas las filas de la derecha.",
            TipoOp.Join => b.Cond == null
                ? "Join natural: combina las filas que coinciden en los atributos con el mismo nombre."
                : $"Theta-join: combina las filas que cumplen {cond}.",
            TipoOp.JoinIzq => "Join externo izquierdo: como el join, pero también quedan las filas de la izquierda sin pareja (con NULL)." + (cond != "" ? $" Condición: {cond}." : ""),
            TipoOp.JoinDer => "Join externo derecho: como el join, pero también quedan las filas de la derecha sin pareja (con NULL)." + (cond != "" ? $" Condición: {cond}." : ""),
            TipoOp.JoinCompleto => "Join externo completo: como el join, pero también quedan las filas sin pareja de los dos lados (con NULL)." + (cond != "" ? $" Condición: {cond}." : ""),
            _ => "",
        };
    }

    static string Lista(IEnumerable<string> xs) {
        var l = xs.ToList();
        return l.Count <= 1 ? string.Join("", l) : string.Join(", ", l.Take(l.Count - 1)) + " y " + l[^1];
    }

    /*-------------------------------------------------------- Sugerencias ---*/

    // Nombres para autocompletar: tablas y variables del dataset, variables de
    // la consulta y atributos. Con "R.", solo los atributos de R.
    public static List<string> Sugerencias(string prefijo, AnalisisDataset ds, string consulta, int maximo = 8) {
        if (string.IsNullOrEmpty(prefijo)) return new();
        var tablas = ds?.Tablas ?? new List<TablaDataset>();
        IEnumerable<string> candidatos;
        string buscado = prefijo;
        int punto = prefijo.LastIndexOf('.');
        if (punto >= 0) {
            string rel = prefijo[..punto];
            buscado = prefijo[(punto + 1)..];
            var t = tablas.FirstOrDefault(x => x.Nombre.Equals(rel, StringComparison.OrdinalIgnoreCase));
            var attrs = t != null ? t.Atributos.Select(a => a.Nombre) : tablas.SelectMany(x => x.Atributos.Select(a => a.Nombre));
            candidatos = attrs.Select(a => rel + "." + a);
            buscado = prefijo;
        } else {
            var variablesConsulta = System.Text.RegularExpressions.Regex
                .Matches(consulta ?? "", @"(?m)^\s*([A-Za-z_]\w*)\s*=(?!=)")
                .Select(m => m.Groups[1].Value);
            candidatos = tablas.Select(t => t.Nombre)
                .Concat(variablesConsulta)
                .Concat(tablas.SelectMany(t => t.Atributos.Select(a => a.Nombre)));
        }
        return candidatos
            .Where(c => c.StartsWith(buscado, StringComparison.OrdinalIgnoreCase) && !c.Equals(buscado, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maximo)
            .ToList();
    }
}
