using System.Collections.Generic;



using System;



public class Parser {
	public const int _EOF = 0;
	public const int _ident = 1;
	public const int _numero = 2;
	public const int _cadena = 3;
	public const int _grupo = 4;
	public const int maxT = 70;

	const bool _T = true;
	const bool _x = false;
	const int minErrDist = 2;
	
	public Scanner scanner;
	public Errors  errors;

	public Token t;    // last recognized token
	public Token la;   // lookahead token
	int errDist = minErrDist;

public Programa programa;            // el AST completo, para usarlo desde Program.cs

  // Operadores de comparación: los usa el resolver del theta-join.
  static readonly HashSet<string> opRel = new HashSet<string> {
    "=", "<>", "!=", "≠", "<", ">", "<=", ">=", "≤", "≥"
  };

  // ¿La sentencia empieza con "nombre = ..."? Mira dos tokens hacia adelante.
  bool EsAsignacion() {
    scanner.ResetPeek();
    return la.kind == _ident && scanner.Peek().val == "=";
  }

  // Después de un join: ¿lo que sigue es una condición o la relación derecha?
  //   R ⋈ a = b S        → condición (ident seguido de un comparador)
  //   R ⋈ R.a = S.b S    → condición (ident seguido de un punto)
  //   R ⋈ (a = b) S      → condición (se mira adentro del paréntesis)
  //   R ⋈ S              → relación
  //   R ⋈ (σ a = 1 S)    → relación
  bool EsCondicionJoin() {
    scanner.ResetPeek();
    Token x = la;
    while (x.val == "(") x = scanner.Peek();          // saltea paréntesis de apertura
    if (x.kind == _numero || x.kind == _cadena) return true;
    string v = x.val.ToLower();
    if (v == "not" || v == "¬" || v == "-") return true;
    if (x.kind == _ident) {
      Token sig = scanner.Peek();
      return sig.val == "." || opRel.Contains(sig.val);
    }
    return false;
  }

  // 'Base de Datos' → Base de Datos
  static string SinComillas(string s) { return s.Substring(1, s.Length - 2); }



	public Parser(Scanner scanner) {
		this.scanner = scanner;
		errors = new Errors();
	}

	void SynErr (int n) {
		if (errDist >= minErrDist) errors.SynErr(la.line, la.col, n);
		errDist = 0;
	}

	public void SemErr (string msg) {
		if (errDist >= minErrDist) errors.SemErr(t.line, t.col, msg);
		errDist = 0;
	}
	
	void Get () {
		for (;;) {
			t = la;
			la = scanner.Scan();
			if (la.kind <= maxT) { ++errDist; break; }

			la = t;
		}
	}
	
	void Expect (int n) {
		if (la.kind==n) Get(); else { SynErr(n); }
	}
	
	bool StartOf (int s) {
		return set[s, la.kind];
	}
	
	void ExpectWeak (int n, int follow) {
		if (la.kind == n) Get();
		else {
			SynErr(n);
			while (!StartOf(follow)) Get();
		}
	}


	bool WeakSeparator(int n, int syFol, int repFol) {
		int kind = la.kind;
		if (kind == n) {Get(); return true;}
		else if (StartOf(repFol)) {return false;}
		else {
			SynErr(n);
			while (!(set[syFol, kind] || set[repFol, kind] || set[0, kind])) {
				Get();
				kind = la.kind;
			}
			return StartOf(syFol);
		}
	}

	
	void AR() {
		Instruccion s; programa = new Programa(); 
		if (la.kind == 4) {
			Get();
			programa.Grupo = t.val.Substring(6).Trim(); 
		}
		while (StartOf(1)) {
			Sentencia(out s);
			programa.Instrucciones.Add(s); 
			if (la.kind == 5) {
				Get();
			}
		}
	}

	void Sentencia(out Instruccion s) {
		s = null; ExpRel e; string nombre; Token pos; 
		if (EsAsignacion()) {
			Expect(1);
			nombre = t.val; pos = t; 
			Expect(6);
			Expr(out e);
			s = new Asignacion(nombre, e, pos); 
		} else if (StartOf(1)) {
			pos = la; 
			Expr(out e);
			s = new Consulta(e, pos); 
		} else SynErr(71);
	}

	void Expr(out ExpRel e) {
		ExpRel der; TipoOp op; Token pos; 
		Inter(out e);
		while (StartOf(2)) {
			pos = la; 
			OpUnion(out op);
			Inter(out der);
			e = new OpBinaria(op, e, der, null, pos); 
		}
	}

	void Inter(out ExpRel e) {
		ExpRel der; Token pos; 
		Prod(out e);
		while (la.kind == 11 || la.kind == 12) {
			pos = la; 
			if (la.kind == 11) {
				Get();
			} else {
				Get();
			}
			Prod(out der);
			e = new OpBinaria(TipoOp.Interseccion, e, der, null, pos); 
		}
	}

	void OpUnion(out TipoOp op) {
		op = TipoOp.Union; 
		if (la.kind == 7 || la.kind == 8) {
			if (la.kind == 7) {
				Get();
			} else {
				Get();
			}
		} else if (la.kind == 9 || la.kind == 10) {
			if (la.kind == 9) {
				Get();
			} else {
				Get();
			}
			op = TipoOp.Diferencia; 
		} else SynErr(72);
	}

	void Prod(out ExpRel e) {
		ExpRel der; TipoOp op; Condicion c; Token pos; 
		Unario(out e);
		while (StartOf(3)) {
			pos = la; 
			OpProd(out op, out c);
			Unario(out der);
			e = new OpBinaria(op, e, der, c, pos); 
		}
	}

	void Unario(out ExpRel e) {
		e = null; ExpRel hijo; Condicion c; Token pos;
		List<ItemProy> items; string rel; List<ParRenombre> pares;
		List<AtributoRef> grupo; List<FuncionAgregada> aggs; 
		switch (la.kind) {
		case 31: case 32: {
			if (la.kind == 31) {
				Get();
			} else {
				Get();
			}
			pos = t; 
			ListaProy(out items);
			Unario(out hijo);
			e = new Proyeccion(items, hijo, pos); 
			break;
		}
		case 33: case 34: {
			if (la.kind == 33) {
				Get();
			} else {
				Get();
			}
			pos = t; 
			Cond(out c);
			Unario(out hijo);
			e = new Seleccion(c, hijo, pos); 
			break;
		}
		case 35: case 36: {
			if (la.kind == 35) {
				Get();
			} else {
				Get();
			}
			pos = t; 
			Renombre(out rel, out pares);
			Unario(out hijo);
			if (pares == null) e = new RenombreRel(rel, hijo, pos);
			else e = new RenombreAttr(pares, hijo, pos); 
			break;
		}
		case 37: case 38: {
			if (la.kind == 37) {
				Get();
			} else {
				Get();
			}
			pos = t; 
			Agrupar(out grupo, out aggs);
			Unario(out hijo);
			e = new Agrupamiento(grupo, aggs, hijo, pos); 
			break;
		}
		case 39: {
			Get();
			Expr(out e);
			Expect(40);
			break;
		}
		case 67: {
			RelInline(out e);
			break;
		}
		case 1: {
			Get();
			e = new Relacion(t.val, t); 
			break;
		}
		default: SynErr(73); break;
		}
	}

	void OpProd(out TipoOp op, out Condicion c) {
		op = TipoOp.Producto; c = null; 
		switch (la.kind) {
		case 13: case 14: case 15: case 16: {
			if (la.kind == 13) {
				Get();
			} else if (la.kind == 14) {
				Get();
			} else if (la.kind == 15) {
				Get();
			} else {
				Get();
				Expect(17);
			}
			break;
		}
		case 18: case 19: {
			if (la.kind == 18) {
				Get();
			} else {
				Get();
				Expect(17);
			}
			op = TipoOp.Join; 
			break;
		}
		case 17: case 20: case 21: {
			if (la.kind == 20) {
				Get();
			} else if (la.kind == 17) {
				Get();
			} else {
				Get();
				Expect(17);
			}
			op = TipoOp.Join; 
			if (EsCondicionJoin()) {
				Cond(out c);
			}
			break;
		}
		case 22: case 23: {
			if (la.kind == 22) {
				Get();
			} else {
				Get();
				if (la.kind == 24) {
					Get();
				}
				Expect(17);
			}
			op = TipoOp.JoinIzq; 
			if (EsCondicionJoin()) {
				Cond(out c);
			}
			break;
		}
		case 25: case 26: {
			if (la.kind == 25) {
				Get();
			} else {
				Get();
				if (la.kind == 24) {
					Get();
				}
				Expect(17);
			}
			op = TipoOp.JoinDer; 
			if (EsCondicionJoin()) {
				Cond(out c);
			}
			break;
		}
		case 27: case 28: {
			if (la.kind == 27) {
				Get();
			} else {
				Get();
				if (la.kind == 24) {
					Get();
				}
				Expect(17);
			}
			op = TipoOp.JoinCompleto; 
			if (EsCondicionJoin()) {
				Cond(out c);
			}
			break;
		}
		case 29: case 30: {
			if (la.kind == 29) {
				Get();
			} else {
				Get();
			}
			op = TipoOp.Division; 
			break;
		}
		default: SynErr(74); break;
		}
	}

	void Cond(out Condicion c) {
		Condicion der; Token pos; 
		CondY(out c);
		while (la.kind == 52 || la.kind == 53) {
			if (la.kind == 52) {
				Get();
			} else {
				Get();
			}
			pos = t; 
			CondY(out der);
			c = new CondLogica("or", c, der, pos); 
		}
	}

	void ListaProy(out List<ItemProy> items ) {
		items = new List<ItemProy>(); ItemProy i; 
		ElemProy(out i);
		items.Add(i); 
		while (la.kind == 41) {
			Get();
			ElemProy(out i);
			items.Add(i); 
		}
	}

	void Renombre(out string rel, out List<ParRenombre> pares ) {
		rel = null; pares = null; string id; Token tok; ParRenombre p; 
		Expect(1);
		id = t.val; tok = t; 
		if (StartOf(4)) {
			ParRen(id, tok, out p);
			pares = new List<ParRenombre> { p }; 
			while (la.kind == 41) {
				Get();
				Expect(1);
				id = t.val; tok = t; 
				ParRen(id, tok, out p);
				pares.Add(p); 
			}
		}
		if (pares == null) rel = id; 
	}

	void Agrupar(out List<AtributoRef> grupo, out List<FuncionAgregada> aggs ) {
		grupo = new List<AtributoRef>(); aggs = new List<FuncionAgregada>();
		FuncionAgregada f; 
		if (la.kind == 1) {
			ListaAttr(out grupo);
		}
		if (la.kind == 5) {
			Get();
		}
		Agregado(out f);
		aggs.Add(f); 
		while (la.kind == 41) {
			Get();
			Agregado(out f);
			aggs.Add(f); 
		}
	}

	void RelInline(out ExpRel e) {
		e = null; var cols = new List<ColumnaDef>(); var filas = new List<List<Constante>>();
		ColumnaDef col; List<Constante> fila; Token pos; 
		Expect(67);
		pos = t; 
		Columna(out col);
		cols.Add(col); 
		while (la.kind == 41) {
			Get();
			Columna(out col);
			cols.Add(col); 
		}
		while (StartOf(5)) {
			Fila(out fila);
			filas.Add(fila); 
		}
		Expect(68);
		e = new TablaDatos(cols, filas, pos); 
	}

	void ElemProy(out ItemProy i) {
		AtributoRef a; string alias = null; 
		Attr(out a);
		if (la.kind == 48 || la.kind == 49) {
			Flecha();
			Expect(1);
			alias = t.val; 
		}
		i = new ItemProy(a, alias); 
	}

	void Attr(out AtributoRef a) {
		string n1; Token pos; 
		Expect(1);
		n1 = t.val; pos = t; a = new AtributoRef(null, n1, pos); 
		if (la.kind == 47) {
			Get();
			Expect(1);
			a = new AtributoRef(n1, t.val, pos); 
		}
	}

	void Flecha() {
		if (la.kind == 48) {
			Get();
		} else if (la.kind == 49) {
			Get();
		} else SynErr(75);
	}

	void ParRen(string id, Token tok, out ParRenombre p) {
		p = null; AtributoRef a; 
		if (la.kind == 50 || la.kind == 51) {
			FlechaIzq();
			Attr(out a);
			p = new ParRenombre(a, id); 
		} else if (la.kind == 48 || la.kind == 49) {
			Flecha();
			Expect(1);
			p = new ParRenombre(new AtributoRef(null, id, tok), t.val); 
		} else SynErr(76);
	}

	void FlechaIzq() {
		if (la.kind == 50) {
			Get();
		} else if (la.kind == 51) {
			Get();
		} else SynErr(77);
	}

	void ListaAttr(out List<AtributoRef> lista ) {
		lista = new List<AtributoRef>(); AtributoRef a; 
		Attr(out a);
		lista.Add(a); 
		while (la.kind == 41) {
			Get();
			Attr(out a);
			lista.Add(a); 
		}
	}

	void Agregado(out FuncionAgregada f) {
		f = null; string fn; AtributoRef a = null; Token pos; 
		FuncAgg(out fn);
		pos = t; 
		Expect(39);
		if (la.kind == 18) {
			Get();
		} else if (la.kind == 1) {
			Attr(out a);
		} else SynErr(78);
		Expect(40);
		Flecha();
		Expect(1);
		f = new FuncionAgregada(fn, a, t.val, pos); 
	}

	void FuncAgg(out string fn) {
		if (la.kind == 42) {
			Get();
		} else if (la.kind == 43) {
			Get();
		} else if (la.kind == 44) {
			Get();
		} else if (la.kind == 45) {
			Get();
		} else if (la.kind == 46) {
			Get();
		} else SynErr(79);
		fn = t.val.ToLower(); 
	}

	void CondY(out Condicion c) {
		Condicion der; Token pos; 
		CondNo(out c);
		while (la.kind == 54 || la.kind == 55) {
			if (la.kind == 54) {
				Get();
			} else {
				Get();
			}
			pos = t; 
			CondNo(out der);
			c = new CondLogica("and", c, der, pos); 
		}
	}

	void CondNo(out Condicion c) {
		c = null; TerminoCond izq, der; string op; Token pos; 
		if (la.kind == 56 || la.kind == 57) {
			if (la.kind == 56) {
				Get();
			} else {
				Get();
			}
			pos = t; 
			CondNo(out c);
			c = new Negacion(c, pos); 
		} else if (la.kind == 39) {
			Get();
			Cond(out c);
			Expect(40);
		} else if (StartOf(5)) {
			Operando(out izq);
			OpRel(out op);
			pos = t; 
			Operando(out der);
			c = new Comparacion(izq, op, der, pos); 
		} else SynErr(80);
	}

	void Operando(out TerminoCond o) {
		o = null; AtributoRef a; string signo = ""; 
		if (la.kind == 1) {
			Attr(out a);
			o = a; 
		} else if (la.kind == 2 || la.kind == 9) {
			if (la.kind == 9) {
				Get();
				signo = "-"; 
			}
			Expect(2);
			o = new Constante(TipoDato.Numero, signo + t.val, t); 
		} else if (la.kind == 3) {
			Get();
			o = new Constante(TipoDato.Cadena, SinComillas(t.val), t); 
		} else SynErr(81);
	}

	void OpRel(out string op) {
		op = null; 
		switch (la.kind) {
		case 6: {
			Get();
			op = "="; 
			break;
		}
		case 58: case 59: case 60: {
			if (la.kind == 58) {
				Get();
			} else if (la.kind == 59) {
				Get();
			} else {
				Get();
			}
			op = "<>"; 
			break;
		}
		case 61: {
			Get();
			op = "<"; 
			break;
		}
		case 62: {
			Get();
			op = ">"; 
			break;
		}
		case 63: case 64: {
			if (la.kind == 63) {
				Get();
			} else {
				Get();
			}
			op = "<="; 
			break;
		}
		case 65: case 66: {
			if (la.kind == 65) {
				Get();
			} else {
				Get();
			}
			op = ">="; 
			break;
		}
		default: SynErr(82); break;
		}
	}

	void Columna(out ColumnaDef c) {
		AtributoRef a; string tipo = null; 
		Attr(out a);
		if (la.kind == 69) {
			Get();
			Expect(1);
			tipo = t.val.ToLower(); 
		}
		c = new ColumnaDef(a, tipo); 
	}

	void Fila(out List<Constante> f ) {
		f = new List<Constante>(); Constante v; 
		Valor(out v);
		f.Add(v); 
		while (la.kind == 41) {
			Get();
			Valor(out v);
			f.Add(v); 
		}
	}

	void Valor(out Constante v) {
		v = null; string signo = ""; 
		if (la.kind == 2 || la.kind == 9) {
			if (la.kind == 9) {
				Get();
				signo = "-"; 
			}
			Expect(2);
			v = new Constante(TipoDato.Numero, signo + t.val, t); 
		} else if (la.kind == 3) {
			Get();
			v = new Constante(TipoDato.Cadena, SinComillas(t.val), t); 
		} else if (la.kind == 1) {
			Get();
			v = new Constante(TipoDato.Simbolo, t.val, t); 
		} else SynErr(83);
	}



	public void Parse() {
		la = new Token();
		la.val = "";		
		Get();
		AR();
		Expect(0);

	}
	
	static readonly bool[,] set = {
		{_T,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x},
		{_x,_T,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_T, _T,_T,_T,_T, _T,_T,_T,_T, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_T, _x,_x,_x,_x},
		{_x,_x,_x,_x, _x,_x,_x,_T, _T,_T,_T,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x},
		{_x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_T,_T,_T, _T,_T,_T,_T, _T,_T,_T,_T, _x,_T,_T,_T, _T,_T,_T,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x},
		{_x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _T,_T,_T,_T, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x},
		{_x,_T,_T,_T, _x,_x,_x,_x, _x,_T,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x, _x,_x,_x,_x}

	};
} // end Parser


public class Errors {
	public int count = 0;                                    // number of errors detected
	public System.IO.TextWriter errorStream = Console.Out;   // error messages go to this stream
	public string errMsgFormat = "-- line {0} col {1}: {2}"; // 0=line, 1=column, 2=text

	public virtual void SynErr (int line, int col, int n) {
		string s;
		switch (n) {
			case 0: s = "EOF expected"; break;
			case 1: s = "ident expected"; break;
			case 2: s = "numero expected"; break;
			case 3: s = "cadena expected"; break;
			case 4: s = "grupo expected"; break;
			case 5: s = "\";\" expected"; break;
			case 6: s = "\"=\" expected"; break;
			case 7: s = "\"\u222a\" expected"; break;
			case 8: s = "\"union\" expected"; break;
			case 9: s = "\"-\" expected"; break;
			case 10: s = "\"except\" expected"; break;
			case 11: s = "\"\u2229\" expected"; break;
			case 12: s = "\"intersect\" expected"; break;
			case 13: s = "\"\u00d7\" expected"; break;
			case 14: s = "\"\u2a2f\" expected"; break;
			case 15: s = "\"x\" expected"; break;
			case 16: s = "\"cross\" expected"; break;
			case 17: s = "\"join\" expected"; break;
			case 18: s = "\"*\" expected"; break;
			case 19: s = "\"natural\" expected"; break;
			case 20: s = "\"\u22c8\" expected"; break;
			case 21: s = "\"inner\" expected"; break;
			case 22: s = "\"\u27d5\" expected"; break;
			case 23: s = "\"left\" expected"; break;
			case 24: s = "\"outer\" expected"; break;
			case 25: s = "\"\u27d6\" expected"; break;
			case 26: s = "\"right\" expected"; break;
			case 27: s = "\"\u27d7\" expected"; break;
			case 28: s = "\"full\" expected"; break;
			case 29: s = "\"\u00f7\" expected"; break;
			case 30: s = "\"/\" expected"; break;
			case 31: s = "\"\u03c0\" expected"; break;
			case 32: s = "\"pi\" expected"; break;
			case 33: s = "\"\u03c3\" expected"; break;
			case 34: s = "\"sigma\" expected"; break;
			case 35: s = "\"\u03c1\" expected"; break;
			case 36: s = "\"rho\" expected"; break;
			case 37: s = "\"\u03b3\" expected"; break;
			case 38: s = "\"gamma\" expected"; break;
			case 39: s = "\"(\" expected"; break;
			case 40: s = "\")\" expected"; break;
			case 41: s = "\",\" expected"; break;
			case 42: s = "\"count\" expected"; break;
			case 43: s = "\"sum\" expected"; break;
			case 44: s = "\"avg\" expected"; break;
			case 45: s = "\"min\" expected"; break;
			case 46: s = "\"max\" expected"; break;
			case 47: s = "\".\" expected"; break;
			case 48: s = "\"\u2192\" expected"; break;
			case 49: s = "\"->\" expected"; break;
			case 50: s = "\"\u2190\" expected"; break;
			case 51: s = "\"<-\" expected"; break;
			case 52: s = "\"\u2228\" expected"; break;
			case 53: s = "\"or\" expected"; break;
			case 54: s = "\"\u2227\" expected"; break;
			case 55: s = "\"and\" expected"; break;
			case 56: s = "\"\u00ac\" expected"; break;
			case 57: s = "\"not\" expected"; break;
			case 58: s = "\"<>\" expected"; break;
			case 59: s = "\"!=\" expected"; break;
			case 60: s = "\"\u2260\" expected"; break;
			case 61: s = "\"<\" expected"; break;
			case 62: s = "\">\" expected"; break;
			case 63: s = "\"<=\" expected"; break;
			case 64: s = "\"\u2264\" expected"; break;
			case 65: s = "\">=\" expected"; break;
			case 66: s = "\"\u2265\" expected"; break;
			case 67: s = "\"{\" expected"; break;
			case 68: s = "\"}\" expected"; break;
			case 69: s = "\":\" expected"; break;
			case 70: s = "??? expected"; break;
			case 71: s = "invalid Sentencia"; break;
			case 72: s = "invalid OpUnion"; break;
			case 73: s = "invalid Unario"; break;
			case 74: s = "invalid OpProd"; break;
			case 75: s = "invalid Flecha"; break;
			case 76: s = "invalid ParRen"; break;
			case 77: s = "invalid FlechaIzq"; break;
			case 78: s = "invalid Agregado"; break;
			case 79: s = "invalid FuncAgg"; break;
			case 80: s = "invalid CondNo"; break;
			case 81: s = "invalid Operando"; break;
			case 82: s = "invalid OpRel"; break;
			case 83: s = "invalid Valor"; break;

			default: s = "error " + n; break;
		}
		errorStream.WriteLine(errMsgFormat, line, col, s);
		count++;
	}

	public virtual void SemErr (int line, int col, string s) {
		errorStream.WriteLine(errMsgFormat, line, col, s);
		count++;
	}
	
	public virtual void SemErr (string s) {
		errorStream.WriteLine(s);
		count++;
	}
	
	public virtual void Warning (int line, int col, string s) {
		errorStream.WriteLine(errMsgFormat, line, col, s);
	}
	
	public virtual void Warning(string s) {
		errorStream.WriteLine(s);
	}
} // Errors


public class FatalError: Exception {
	public FatalError(string m): base(m) {}
}
