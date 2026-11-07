#!/usr/bin/env python3
# Genera Gramatica.atg a partir de AR.atg: la misma gramática, pero sin los
# atributos (<out ExpRel e>) ni las acciones semánticas ((. código C# .)), para
# poder leerla y analizarla sola. Coco/R la acepta tal cual, así que también se
# puede verificar que es LL(1) sin el resto del compilador.
#
# AR.atg sigue siendo la fuente: Gramatica.atg no se edita a mano.
#
# Uso: python extraer_gramatica.py              (re)genera Gramatica.atg
#      python extraer_gramatica.py --verificar  falla si Gramatica.atg está desactualizada
import re
import sys
import textwrap
from pathlib import Path

CARPETA = Path(__file__).parent
FUENTE = CARPETA / "AR.atg"
DESTINO = CARPETA / "Gramatica.atg"

ENCABEZADO = """\
/*----------------------------------------------------------------------------
  Gramatica.atg — Gramática del compilador de álgebra relacional a SQL

  ARCHIVO GENERADO a partir de AR.atg con extraer_gramatica.py: no editarlo
  a mano. Es la misma gramática que usa el compilador, sin los atributos ni
  las acciones semánticas (el código C# que arma el AST).

  Notación (EBNF de Coco/R):
    "x"        terminal (palabra clave o símbolo)
    ident ...  terminales definidos en TOKENS
    [ ... ]    opcional (cero o una vez)
    { ... }    repetición (cero o más veces)
    ( ... )    agrupamiento
    a | b      alternativas
    IGNORECASE las palabras clave no distinguen mayúsculas

  La gramática es LL(1) salvo en dos puntos, que se resuelven con un
  resolver IF(...) (una condición que mira tokens hacia adelante):
    EsAsignacion()     una sentencia que empieza con ident es una asignación
                       si el token siguiente es "=".
    EsCondicionJoin()  después de un join sigue una condición (theta-join) si,
                       salteando los "(" iniciales, el primer token es un
                       número, una cadena, "not", "¬" o "-", o es un ident
                       seguido de "." o de un comparador. Si no, sigue la
                       relación derecha (join natural).

  Si se sacan los dos IF(...), Coco/R informa los "LL1 warning" de esos
  conflictos (en Sentencia y en OpProd).

  Para analizarla con Coco/R sin pisar el Parser.cs y el Scanner.cs del
  compilador, se genera en otra carpeta (-o). Con -trace F además lista los
  conjuntos FIRST y FOLLOW en trace.txt (queda en esta carpeta):
    ./Coco.exe Gramatica.atg -o <otra carpeta> -trace F
----------------------------------------------------------------------------*/
"""


def leer(ruta):
    # utf-8-sig: AR.atg tiene BOM
    return ruta.read_text(encoding="utf-8-sig").replace("\r\n", "\n")


def limpiar(producciones):
    """Saca de la sección PRODUCTIONS las acciones, los atributos y los comentarios //."""
    s = re.sub(r"\(\..*?\.\)", " ", producciones, flags=re.S)  # (. código C# .)
    s = re.sub(r"<\..*?\.>", " ", s, flags=re.S)                # <. atributos con genéricos .>
    s = re.sub(r"(?<=\w)<[^<>\"']*>", " ", s)                   # Nombre<atributos>
    s = re.sub(r"//[^\n]*", "", s)                              # comentarios de línea
    return s


# Piezas de una producción: comentario /* */, resolver IF(...), terminal entre
# comillas, nombre, símbolo de la notación, o espacios.
PIEZA = re.compile(
    r"(/\*.*?\*/)"
    r"|(IF\s*\((?:[^()]|\([^()]*\))*\))"
    r"|(\"[^\"]*\"|'[^']*')"
    r"|(\w+)"
    r"|([=|()\[\]{}.])"
    r"|(\s+)",
    re.S,
)


def piezas(texto):
    pos = 0
    while pos < len(texto):
        m = PIEZA.match(texto, pos)
        if not m:
            sys.exit(f"extraer_gramatica: no se entiende el texto en: {texto[pos:pos + 40]!r}")
        pos = m.end()
        if m.group(6) is None:          # los espacios se descartan
            yield m.group(0)


def formatear(nombre, cuerpo):
    """Una producción, con cada alternativa de primer nivel en su propia línea."""
    alternativas = [[]]
    profundidad = 0
    for p in cuerpo:
        if p in ("(", "[", "{"):
            profundidad += 1
        elif p in (")", "]", "}"):
            profundidad -= 1
        if p == "|" and profundidad == 0:
            alternativas.append([])
        else:
            alternativas[-1].append(p)
    textos = [" ".join(a) for a in alternativas]
    if len(textos) == 1:
        return f"{nombre} = {textos[0]} ."
    lineas = [nombre, "  = " + textos[0]]
    lineas += ["  | " + t for t in textos[1:]]
    lineas.append("  .")
    return "\n".join(lineas)


def codigo_resolvers(declaraciones, producciones):
    """De las declaraciones C# de AR.atg, deja solo los métodos que usan los
    IF(...) y lo que esos métodos usan (por ejemplo, opRel). El resto (el campo
    programa, SinComillas) es para armar el AST y no forma parte de la gramática."""
    bloques = [b.strip("\n") for b in re.split(r"\n\s*\n", declaraciones) if b.strip()]

    def declara(bloque):        # nombre que declara el bloque: "opRel =", "EsAsignacion("
        codigo = [l for l in bloque.split("\n") if not l.strip().startswith("//")]
        m = re.search(r"(\w+)\s*(?:=|\()", codigo[0]) if codigo else None
        return m.group(1) if m else None

    usados = set(re.findall(r"IF\s*\(\s*(\w+)\s*\(", producciones))
    elegidos = []
    cambio = True
    while cambio:               # agrega bloques hasta que no aparezca ninguno nuevo
        cambio = False
        for b in bloques:
            if b not in elegidos and declara(b) in usados:
                elegidos.append(b)
                usados |= set(re.findall(r"\w+", b))
                cambio = True
    # en el orden en que aparecen en AR.atg
    return "\n\n".join(textwrap.dedent(b) for b in bloques if b in elegidos)


def generar():
    atg = leer(FUENTE)
    m = re.search(r"^COMPILER (\w+)\n(.*?)^(IGNORECASE.*?)^PRODUCTIONS\n(.*)^END \1\.",
                  atg, flags=re.S | re.M)
    if not m:
        sys.exit("extraer_gramatica: AR.atg no tiene la forma esperada")
    nombre, declaraciones, lexico, producciones = m.groups()
    resolvers = codigo_resolvers(declaraciones, producciones)

    salida = []
    lista = list(piezas(limpiar(producciones)))
    i = 0
    while i < len(lista):
        p = lista[i]
        if p.startswith("/*"):                  # comentario entre producciones
            salida.append(p)
            i += 1
            continue
        # producción: Nombre = cuerpo .
        if i + 1 >= len(lista) or lista[i + 1] != "=":
            sys.exit(f"extraer_gramatica: se esperaba '=' después de {p}")
        cuerpo = []
        i += 2
        profundidad = 0
        while lista[i] != "." or profundidad > 0:
            if lista[i] in ("(", "[", "{"):
                profundidad += 1
            elif lista[i] in (")", "]", "}"):
                profundidad -= 1
            cuerpo.append(lista[i])
            i += 1
        salida.append(formatear(p, cuerpo))
        i += 1

    return (ENCABEZADO + f"\nCOMPILER {nombre}\n\n" + resolvers + "\n\n" + lexico.rstrip() + "\n\n"
            + "PRODUCTIONS\n\n" + "\n\n".join(salida) + f"\n\nEND {nombre}.\n")


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")    # en Windows, Python usa otra codificación por defecto
    texto = generar()
    if "--verificar" in sys.argv:
        if not DESTINO.exists() or leer(DESTINO) != texto:
            print("Gramatica.atg está desactualizada: correr  python extraer_gramatica.py")
            sys.exit(1)
    else:
        # con BOM, igual que AR.atg, para que Coco/R lea bien los símbolos
        DESTINO.write_text(texto, encoding="utf-8-sig", newline="\n")
        print("Gramatica.atg generada")
