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
		if (la.kind == 4) {
			Get();
		}
		while (StartOf(1)) {
			Sentencia();
			if (la.kind == 5) {
				Get();
			}
		}
	}

	void Sentencia() {
		if (EsAsignacion()) {
			Expect(1);
			Expect(6);
			Expr();
		} else if (StartOf(1)) {
			Expr();
		} else SynErr(71);
	}

	void Expr() {
		Inter();
		while (StartOf(2)) {
			OpUnion();
			Inter();
		}
	}

	void Inter() {
		Prod();
		while (la.kind == 11 || la.kind == 12) {
			OpInter();
			Prod();
		}
	}

	void OpUnion() {
		if (la.kind == 7) {
			Get();
		} else if (la.kind == 8) {
			Get();
		} else if (la.kind == 9) {
			Get();
		} else if (la.kind == 10) {
			Get();
		} else SynErr(72);
	}

	void Prod() {
		Unario();
		while (StartOf(3)) {
			OpProd();
			Unario();
		}
	}

	void OpInter() {
		if (la.kind == 11) {
			Get();
		} else if (la.kind == 12) {
			Get();
		} else SynErr(73);
	}

	void Unario() {
		switch (la.kind) {
		case 31: case 32: {
			if (la.kind == 31) {
				Get();
			} else {
				Get();
			}
			ListaProy();
			Unario();
			break;
		}
		case 33: case 34: {
			if (la.kind == 33) {
				Get();
			} else {
				Get();
			}
			Cond();
			Unario();
			break;
		}
		case 35: case 36: {
			if (la.kind == 35) {
				Get();
			} else {
				Get();
			}
			Renombre();
			Unario();
			break;
		}
		case 37: case 38: {
			if (la.kind == 37) {
				Get();
			} else {
				Get();
			}
			Agrupar();
			Unario();
			break;
		}
		case 39: {
			Get();
			Expr();
			Expect(40);
			break;
		}
		case 67: {
			RelInline();
			break;
		}
		case 1: {
			Get();
			break;
		}
		default: SynErr(74); break;
		}
	}

	void OpProd() {
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
			if (EsCondicionJoin()) {
				Cond();
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
			if (EsCondicionJoin()) {
				Cond();
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
			if (EsCondicionJoin()) {
				Cond();
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
			if (EsCondicionJoin()) {
				Cond();
			}
			break;
		}
		case 29: case 30: {
			if (la.kind == 29) {
				Get();
			} else {
				Get();
			}
			break;
		}
		default: SynErr(75); break;
		}
	}

	void Cond() {
		CondY();
		while (la.kind == 52 || la.kind == 53) {
			if (la.kind == 52) {
				Get();
			} else {
				Get();
			}
			CondY();
		}
	}

	void ListaProy() {
		ElemProy();
		while (la.kind == 41) {
			Get();
			ElemProy();
		}
	}

	void Renombre() {
		Expect(1);
		if (StartOf(4)) {
			if (la.kind == 50 || la.kind == 51) {
				FlechaIzq();
				Attr();
			} else {
				Flecha();
				Expect(1);
			}
			while (la.kind == 41) {
				Get();
				Expect(1);
				if (la.kind == 50 || la.kind == 51) {
					FlechaIzq();
					Attr();
				} else if (la.kind == 48 || la.kind == 49) {
					Flecha();
					Expect(1);
				} else SynErr(76);
			}
		}
	}

	void Agrupar() {
		if (la.kind == 1) {
			ListaAttr();
		}
		if (la.kind == 5) {
			Get();
		}
		Agregado();
		while (la.kind == 41) {
			Get();
			Agregado();
		}
	}

	void RelInline() {
		Expect(67);
		Columna();
		while (la.kind == 41) {
			Get();
			Columna();
		}
		while (StartOf(5)) {
			Fila();
		}
		Expect(68);
	}

	void ElemProy() {
		Attr();
		if (la.kind == 48 || la.kind == 49) {
			Flecha();
			Expect(1);
		}
	}

	void Attr() {
		Expect(1);
		if (la.kind == 47) {
			Get();
			Expect(1);
		}
	}

	void Flecha() {
		if (la.kind == 48) {
			Get();
		} else if (la.kind == 49) {
			Get();
		} else SynErr(77);
	}

	void FlechaIzq() {
		if (la.kind == 50) {
			Get();
		} else if (la.kind == 51) {
			Get();
		} else SynErr(78);
	}

	void ListaAttr() {
		Attr();
		while (la.kind == 41) {
			Get();
			Attr();
		}
	}

	void Agregado() {
		FuncAgg();
		Expect(39);
		if (la.kind == 18) {
			Get();
		} else if (la.kind == 1) {
			Attr();
		} else SynErr(79);
		Expect(40);
		Flecha();
		Expect(1);
	}

	void FuncAgg() {
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
		} else SynErr(80);
	}

	void CondY() {
		CondNo();
		while (la.kind == 54 || la.kind == 55) {
			if (la.kind == 54) {
				Get();
			} else {
				Get();
			}
			CondNo();
		}
	}

	void CondNo() {
		if (la.kind == 56 || la.kind == 57) {
			if (la.kind == 56) {
				Get();
			} else {
				Get();
			}
			CondNo();
		} else if (la.kind == 39) {
			Get();
			Cond();
			Expect(40);
		} else if (StartOf(5)) {
			Operando();
			OpRel();
			Operando();
		} else SynErr(81);
	}

	void Operando() {
		if (la.kind == 1) {
			Attr();
		} else if (la.kind == 2 || la.kind == 9) {
			if (la.kind == 9) {
				Get();
			}
			Expect(2);
		} else if (la.kind == 3) {
			Get();
		} else SynErr(82);
	}

	void OpRel() {
		switch (la.kind) {
		case 6: {
			Get();
			break;
		}
		case 58: {
			Get();
			break;
		}
		case 59: {
			Get();
			break;
		}
		case 60: {
			Get();
			break;
		}
		case 61: {
			Get();
			break;
		}
		case 62: {
			Get();
			break;
		}
		case 63: {
			Get();
			break;
		}
		case 64: {
			Get();
			break;
		}
		case 65: {
			Get();
			break;
		}
		case 66: {
			Get();
			break;
		}
		default: SynErr(83); break;
		}
	}

	void Columna() {
		Attr();
		if (la.kind == 69) {
			Get();
			Expect(1);
		}
	}

	void Fila() {
		Valor();
		while (la.kind == 41) {
			Get();
			Valor();
		}
	}

	void Valor() {
		if (la.kind == 2 || la.kind == 9) {
			if (la.kind == 9) {
				Get();
			}
			Expect(2);
		} else if (la.kind == 3) {
			Get();
		} else if (la.kind == 1) {
			Get();
		} else SynErr(84);
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
			case 64: s = "\">=\" expected"; break;
			case 65: s = "\"\u2264\" expected"; break;
			case 66: s = "\"\u2265\" expected"; break;
			case 67: s = "\"{\" expected"; break;
			case 68: s = "\"}\" expected"; break;
			case 69: s = "\":\" expected"; break;
			case 70: s = "??? expected"; break;
			case 71: s = "invalid Sentencia"; break;
			case 72: s = "invalid OpUnion"; break;
			case 73: s = "invalid OpInter"; break;
			case 74: s = "invalid Unario"; break;
			case 75: s = "invalid OpProd"; break;
			case 76: s = "invalid Renombre"; break;
			case 77: s = "invalid Flecha"; break;
			case 78: s = "invalid FlechaIzq"; break;
			case 79: s = "invalid Agregado"; break;
			case 80: s = "invalid FuncAgg"; break;
			case 81: s = "invalid CondNo"; break;
			case 82: s = "invalid Operando"; break;
			case 83: s = "invalid OpRel"; break;
			case 84: s = "invalid Valor"; break;

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
