using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

// El compilador como biblioteca, sin dependencias de consola: lo usan
// Program.cs (la línea de comandos) y la app web (carpeta Web/).
//
//   var r = Compilador.Compilar(fuente);
//   r.Errores   errores de sintaxis o semánticos (vacía si compiló bien)
//   r.Ast       el árbol impreso (si no hubo errores de sintaxis)
//   r.Sql       el script SQL para SQLite (si no hubo ningún error)
//   r.Programa  el AST, con el esquema de cada expresión (si no hubo errores de sintaxis)

public enum FaseError { Sintaxis, Semantico }

public class ErrorCompilacion {
    public FaseError Fase;
    public int Linea, Col;
    public string Mensaje;
    public ErrorCompilacion(FaseError fase, int linea, int col, string mensaje) {
        Fase = fase; Linea = linea; Col = col; Mensaje = mensaje;
    }
    // El mismo formato que imprime la línea de comandos
    public override string ToString() => $"-- línea {Linea} col {Col}: {Mensaje}";
}

public class ResultadoCompilacion {
    public List<ErrorCompilacion> Errores = new List<ErrorCompilacion>();
    public Programa Programa;
    public string Ast;
    public string Sql;
    public bool HayErroresDeSintaxis => Errores.Exists(e => e.Fase == FaseError.Sintaxis);
}

public static class Compilador {
    public static ResultadoCompilacion Compilar(string fuente) {
        var r = new ResultadoCompilacion();

        // 1. Scanner + parser. El scanner de Coco/R lee UTF-8 solo si la entrada
        //    empieza con BOM, así que se lo agregamos al texto.
        var stream = new MemoryStream();
        var bom = new UTF8Encoding(true).GetPreamble();
        stream.Write(bom, 0, bom.Length);
        var contenido = Encoding.UTF8.GetBytes(fuente);
        stream.Write(contenido, 0, contenido.Length);
        stream.Position = 0;

        var parser = new Parser(new Scanner(stream));
        // Coco/R escribe los errores en un TextWriter: los juntamos y los separamos
        // en línea, columna y mensaje (separados por tabulador).
        var errores = new StringWriter();
        parser.errors.errorStream = errores;
        parser.errors.errMsgFormat = "{0}\t{1}\t{2}";
        parser.Parse();
        foreach (var linea in errores.ToString().Split('\n')) {
            var partes = linea.TrimEnd('\r').Split('\t', 3);
            if (partes.Length == 3)
                r.Errores.Add(new ErrorCompilacion(FaseError.Sintaxis, int.Parse(partes[0]), int.Parse(partes[1]),
                                                   TraducirSintaxis(partes[2])));
        }
        if (r.Errores.Count > 0) return r;

        r.Programa = parser.programa;
        r.Ast = ImpresorAST.Imprimir(parser.programa);

        // 2. Análisis semántico: deja el esquema de cada expresión en el AST.
        var semantico = new Semantico();
        semantico.Analizar(parser.programa);
        foreach (var e in semantico.Errores) {
            // Semantico arma los mensajes como "-- línea L col C: mensaje"
            var m = Regex.Match(e, @"^-- línea (\d+) col (\d+): (.*)$");
            r.Errores.Add(m.Success
                ? new ErrorCompilacion(FaseError.Semantico, int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), m.Groups[3].Value)
                : new ErrorCompilacion(FaseError.Semantico, 0, 0, e));
        }
        if (r.Errores.Count > 0) return r;

        // 3. Generación de SQL.
        r.Sql = new Generador().Generar(parser.programa);
        return r;
    }

    /*------------------- Mensajes de sintaxis en castellano ------------------*/

    // Coco/R genera los mensajes en inglés, dentro de Parser.cs (que no se edita
    // a mano). Tienen solo dos formas, así que se traducen acá:
    //   X expected     → se esperaba X       (X: un terminal)
    //   invalid NoTerm → se esperaba ...     (ninguna alternativa de NoTerm empieza con ese token)
    static readonly Dictionary<string, string> nombresTerminales = new Dictionary<string, string> {
        { "EOF", "el fin del programa (sobra algo)" },
        { "ident", "un nombre" },
        { "numero", "un número" },
        { "cadena", "una cadena entre comillas simples" },
        { "grupo", "group:" },
        { "???", "otro símbolo" },
    };

    static readonly Dictionary<string, string> noTerminales = new Dictionary<string, string> {
        { "Sentencia", "una consulta o una asignación (nombre = expresión)" },
        { "OpUnion", "∪ o −" },
        { "Unario", "una relación, un operador (π, σ, ρ, γ) o un paréntesis" },
        { "OpProd", "un operador binario (×, ⋈, ⟕, ⟖, ⟗, ÷)" },
        { "Flecha", "→ (o ->)" },
        { "ParRen", "un renombre: nuevo ← viejo o viejo → nuevo" },
        { "FlechaIzq", "← (o <-)" },
        { "Agregado", "una función de agregación (count, sum, avg, min, max)" },
        { "FuncAgg", "una función de agregación (count, sum, avg, min, max)" },
        { "CondNo", "una condición" },
        { "Operando", "un atributo, un número o una cadena" },
        { "OpRel", "un comparador (=, ≠, <, >, ≤, ≥)" },
        { "Valor", "un valor (número, cadena, null, true o false)" },
    };

    public static string TraducirSintaxis(string m) {
        var esperado = Regex.Match(m, "^(.*) expected$");
        if (esperado.Success) {
            string t = esperado.Groups[1].Value;
            return "se esperaba " + (nombresTerminales.TryGetValue(t, out var n) ? n : t);
        }
        var invalido = Regex.Match(m, @"^invalid (\w+)$");
        if (invalido.Success)
            return "se esperaba " + (noTerminales.TryGetValue(invalido.Groups[1].Value, out var n) ? n : invalido.Groups[1].Value);
        return m;
    }

    // Título de una sentencia, como lo muestra --esquemas
    public static string Titulo(Instruccion ins) =>
        ins is Asignacion a ? a.Nombre : "Consulta (línea " + ins.Linea + ")";
}
