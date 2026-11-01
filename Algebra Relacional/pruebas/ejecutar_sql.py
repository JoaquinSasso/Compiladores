#!/usr/bin/env python3
# Ejecuta un script SQL (leído de la entrada estándar) en una base SQLite en
# memoria e imprime el resultado de cada consulta: una línea con los nombres
# de las columnas y una línea por fila, separadas por " | ".
# Lo usa probar.sh para verificar el SQL que genera el compilador.
# Uso: dotnet bin/out/AR.dll archivo.ra | python3 pruebas/ejecutar_sql.py
import sqlite3
import sys

def texto(v):
    if v is None:
        return "NULL"
    if isinstance(v, float) and v.is_integer():
        return str(int(v))          # 7.0 → 7, para que no dependa de cómo guarda SQLite el número
    return str(v)

base = sqlite3.connect(":memory:")
sentencia = ""
primera = True
for linea in sys.stdin:
    sentencia += linea
    if not sqlite3.complete_statement(sentencia):
        continue
    try:
        cursor = base.execute(sentencia)
    except sqlite3.Error as e:
        print("ERROR de SQLite:", e)
        print(sentencia)
        sys.exit(1)
    if cursor.description is not None:          # es una consulta: mostrar el resultado
        if not primera:
            print()
        primera = False
        print(" | ".join(c[0] for c in cursor.description))
        for fila in cursor.fetchall():
            print(" | ".join(texto(v) for v in fila))
    sentencia = ""
