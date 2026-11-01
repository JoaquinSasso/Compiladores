using System.Collections.Generic;
using System.Linq;
using System.Text;

/*============================================================================
  AST — Árbol de sintaxis abstracta del compilador de álgebra relacional.

  Lo construye el parser (acciones semánticas en AR.atg) y después lo recorren
  el análisis semántico y el generador de SQL.

  OJO con los nombres: Coco/R crea en la clase Parser un método por cada
  no terminal (Cond, Attr, Valor...). Si una clase del AST se llamara igual,
  C# no sabría si se habla del método o de la clase. Por eso acá los nombres
  son distintos: Condicion, AtributoRef, Constante, etc.
============================================================================*/

// Base de todos los nodos: guarda la posición para informar errores.
public abstract class Nodo {
    public int Linea, Col;
    protected Nodo(Token t) { if (t != null) { Linea = t.line; Col = t.col; } }
}

/*------------------------------ Programa ----------------------------------*/

public class Programa : Nodo {
    public string Grupo;                                   // "group: Nombre" (opcional)
    public List<Instruccion> Instrucciones = new List<Instruccion>();
    public Programa() : base(null) {}
}

public abstract class Instruccion : Nodo {
    public ExpRel Expr;
    protected Instruccion(ExpRel e, Token t) : base(t) { Expr = e; }
}

public class Asignacion : Instruccion {                           // nombre = expresión
    public string Nombre;
    public Asignacion(string nombre, ExpRel e, Token t) : base(e, t) { Nombre = nombre; }
}

public class Consulta : Instruccion {                             // expresión suelta
    public Consulta(ExpRel e, Token t) : base(e, t) {}
}

/*----------------------- Expresiones relacionales -------------------------*/

public abstract class ExpRel : Nodo {
    protected ExpRel(Token t) : base(t) {}
    public abstract string Etiqueta();                     // texto del nodo al imprimir
    public abstract IEnumerable<ExpRel> Hijos();
}

// Hoja: el nombre de una relación o de una variable asignada.
public class Relacion : ExpRel {
    public string Nombre;
    public Relacion(string nombre, Token t) : base(t) { Nombre = nombre; }
    public override string Etiqueta() => Nombre;
    public override IEnumerable<ExpRel> Hijos() => new ExpRel[0];
}

// Operadores unarios: todos tienen un único hijo.
public abstract class OpUnaria : ExpRel {
    public ExpRel Hijo;
    protected OpUnaria(ExpRel hijo, Token t) : base(t) { Hijo = hijo; }
    public override IEnumerable<ExpRel> Hijos() => new[] { Hijo };
}

public class Seleccion : OpUnaria {                               // σ cond (R)
    public Condicion Cond;
    public Seleccion(Condicion c, ExpRel hijo, Token t) : base(hijo, t) { Cond = c; }
    public override string Etiqueta() => "σ " + Cond.Texto(true);
}

public class ItemProy {                                           // a  ó  a → alias
    public AtributoRef Attr; public string Alias;
    public ItemProy(AtributoRef a, string alias) { Attr = a; Alias = alias; }
    public override string ToString() => Alias == null ? Attr.ToString() : Attr + " → " + Alias;
}

public class Proyeccion : OpUnaria {                              // π a, b (R)
    public List<ItemProy> Items;
    public Proyeccion(List<ItemProy> items, ExpRel hijo, Token t) : base(hijo, t) { Items = items; }
    public override string Etiqueta() => "π " + string.Join(", ", Items);
}

public class RenombreRel : OpUnaria {                             // ρ S (R)
    public string NuevoNombre;
    public RenombreRel(string nuevo, ExpRel hijo, Token t) : base(hijo, t) { NuevoNombre = nuevo; }
    public override string Etiqueta() => "ρ " + NuevoNombre;
}

public class ParRenombre {                                        // nuevo ← viejo
    public AtributoRef Viejo; public string Nuevo;
    public ParRenombre(AtributoRef viejo, string nuevo) { Viejo = viejo; Nuevo = nuevo; }
    public override string ToString() => Nuevo + " ← " + Viejo;
}

public class RenombreAttr : OpUnaria {                            // ρ b←a, d←c (R)
    public List<ParRenombre> Pares;
    public RenombreAttr(List<ParRenombre> pares, ExpRel hijo, Token t) : base(hijo, t) { Pares = pares; }
    public override string Etiqueta() => "ρ " + string.Join(", ", Pares);
}

public class FuncionAgregada {                                    // count(*) → cant
    public string Funcion; public AtributoRef Attr; public string Alias;   // Attr null = (*)
    public int Linea, Col;
    public FuncionAgregada(string f, AtributoRef a, string alias, Token t) {
        Funcion = f; Attr = a; Alias = alias; Linea = t.line; Col = t.col;
    }
    public override string ToString() => Funcion + "(" + (Attr == null ? "*" : Attr.ToString()) + ") → " + Alias;
}

public class Agrupamiento : OpUnaria {                            // γ a, b; count(*) → x (R)
    public List<AtributoRef> Grupo; public List<FuncionAgregada> Agregados;
    public Agrupamiento(List<AtributoRef> g, List<FuncionAgregada> ag, ExpRel hijo, Token t) : base(hijo, t) {
        Grupo = g; Agregados = ag;
    }
    public override string Etiqueta() =>
        "γ " + (Grupo.Count > 0 ? string.Join(", ", Grupo) + "; " : "") + string.Join(", ", Agregados);
}

public enum TipoOp { Union, Diferencia, Interseccion, Producto, Join, JoinIzq, JoinDer, JoinCompleto, Division }

// Operadores binarios. En los joins, Cond == null significa join natural.
public class OpBinaria : ExpRel {
    public TipoOp Op; public ExpRel Izq, Der; public Condicion Cond;
    public OpBinaria(TipoOp op, ExpRel izq, ExpRel der, Condicion c, Token t) : base(t) {
        Op = op; Izq = izq; Der = der; Cond = c;
    }
    static readonly Dictionary<TipoOp, string> simbolo = new Dictionary<TipoOp, string> {
        { TipoOp.Union, "∪" }, { TipoOp.Diferencia, "−" }, { TipoOp.Interseccion, "∩" },
        { TipoOp.Producto, "×" }, { TipoOp.Join, "⋈" }, { TipoOp.JoinIzq, "⟕" },
        { TipoOp.JoinDer, "⟖" }, { TipoOp.JoinCompleto, "⟗" }, { TipoOp.Division, "÷" },
    };
    public bool EsJoin => Op == TipoOp.Join || Op == TipoOp.JoinIzq || Op == TipoOp.JoinDer || Op == TipoOp.JoinCompleto;
    public override string Etiqueta() =>
        simbolo[Op] + (!EsJoin ? "" : Cond == null ? " (natural)" : " " + Cond.Texto(true));
    public override IEnumerable<ExpRel> Hijos() => new[] { Izq, Der };
}

public class ColumnaDef {                                         // codM  ó  codM:number
    public AtributoRef Attr; public string Tipo;
    public ColumnaDef(AtributoRef a, string tipo) { Attr = a; Tipo = tipo; }
    public override string ToString() => Tipo == null ? Attr.ToString() : Attr + ":" + Tipo;
}

public class TablaDatos : ExpRel {                                // { encabezado filas }
    public List<ColumnaDef> Columnas; public List<List<Constante>> Filas;
    public TablaDatos(List<ColumnaDef> cols, List<List<Constante>> filas, Token t) : base(t) {
        Columnas = cols; Filas = filas;
    }
    public override string Etiqueta() =>
        "Tabla {" + string.Join(", ", Columnas) + "} · " + Filas.Count + (Filas.Count == 1 ? " fila" : " filas");
    public override IEnumerable<ExpRel> Hijos() => new ExpRel[0];
}

/*------------------------------ Condiciones -------------------------------*/

public abstract class Condicion : Nodo {
    protected Condicion(Token t) : base(t) {}
    // arriba = es la condición completa (no hacen falta paréntesis externos)
    public abstract string Texto(bool arriba);
}

public class CondLogica : Condicion {                             // izq ∧ der,  izq ∨ der
    public string Op; public Condicion Izq, Der;           // Op: "and" | "or"
    public CondLogica(string op, Condicion izq, Condicion der, Token t) : base(t) { Op = op; Izq = izq; Der = der; }
    public override string Texto(bool arriba) {
        string s = Izq.Texto(false) + (Op == "and" ? " ∧ " : " ∨ ") + Der.Texto(false);
        return arriba ? s : "(" + s + ")";
    }
}

public class Negacion : Condicion {                               // ¬ cond
    public Condicion Hijo;
    public Negacion(Condicion hijo, Token t) : base(t) { Hijo = hijo; }
    public override string Texto(bool arriba) => "¬" + (Hijo is Comparacion ? "(" + Hijo.Texto(true) + ")" : Hijo.Texto(false));
}

public class Comparacion : Condicion {                            // a = 'x',  R.a > S.b
    public TerminoCond Izq, Der; public string Op;         // Op normalizado: = <> < > <= >=
    public Comparacion(TerminoCond izq, string op, TerminoCond der, Token t) : base(t) { Izq = izq; Op = op; Der = der; }
    public override string Texto(bool arriba) => Izq + " " + Op + " " + Der;
}

// Lo que puede aparecer a cada lado de una comparación.
public abstract class TerminoCond : Nodo { protected TerminoCond(Token t) : base(t) {} }

public class AtributoRef : TerminoCond {                          // nombre  ó  Relacion.nombre
    public string Relacion, Nombre;                        // Relacion null = sin calificar
    public AtributoRef(string rel, string nombre, Token t) : base(t) { Relacion = rel; Nombre = nombre; }
    public override string ToString() => Relacion == null ? Nombre : Relacion + "." + Nombre;
}

public enum TipoDato { Numero, Cadena, Simbolo }                  // Simbolo: null, true, false o texto sin comillas

public class Constante : TerminoCond {
    public TipoDato Tipo; public string Valor;             // las cadenas se guardan sin comillas
    public Constante(TipoDato tipo, string valor, Token t) : base(t) { Tipo = tipo; Valor = valor; }
    public override string ToString() => Tipo == TipoDato.Cadena ? "'" + Valor + "'" : Valor;
}

/*------------------------- Impresión del árbol ----------------------------*/

public static class ImpresorAST {
    public static string Imprimir(Programa p) {
        var sb = new StringBuilder();
        sb.AppendLine(p.Grupo == null ? "Programa" : "Programa (grupo: " + p.Grupo + ")");
        for (int i = 0; i < p.Instrucciones.Count; i++) {
            var ins = p.Instrucciones[i];
            bool ultima = i == p.Instrucciones.Count - 1;
            sb.AppendLine((ultima ? "└── " : "├── ") +
                (ins is Asignacion a ? "Asignación " + a.Nombre : "Consulta"));
            Nodo(sb, ins.Expr, ultima ? "    " : "│   ", true);
        }
        return sb.ToString();
    }

    static void Nodo(StringBuilder sb, ExpRel n, string prefijo, bool ultimo) {
        sb.AppendLine(prefijo + (ultimo ? "└── " : "├── ") + n.Etiqueta());
        var hijos = n.Hijos().ToList();
        for (int i = 0; i < hijos.Count; i++)
            Nodo(sb, hijos[i], prefijo + (ultimo ? "    " : "│   "), i == hijos.Count - 1);
    }
}
