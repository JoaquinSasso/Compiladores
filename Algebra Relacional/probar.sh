#!/bin/bash
# Corre todas las pruebas:
#   pruebas/validas/     → no tienen que dar errores de sintaxis
#   pruebas/invalidas/   → tienen que dar un error de sintaxis
#   pruebas/ast/         → el árbol impreso tiene que ser igual al archivo .esperado
#   pruebas/semanticas/  → la salida completa (esquemas o errores semánticos)
#                          tiene que ser igual al archivo .esperado
#   pruebas/sql/         → el SQL generado se ejecuta en SQLite y el resultado
#                          de cada consulta tiene que ser igual al .esperado
dotnet build -c Release -o bin/out > /dev/null || { echo "Falló la compilación"; exit 1; }
# En Ubuntu el intérprete es python3; en Windows (Git Bash) suele ser python,
# y python3 es un alias de la Microsoft Store que no funciona.
if python3 -c "" 2> /dev/null; then python=python3; else python=python; fi
fallos=0
for f in pruebas/validas/*.ra; do
  if dotnet bin/out/AR.dll --ast "$f" > /dev/null; then echo "bien   $f"; else echo "FALLA  $f (tendría que ser válida)"; fallos=$((fallos+1)); fi
done
for f in pruebas/invalidas/*.ra; do
  if dotnet bin/out/AR.dll --ast "$f" > /dev/null; then echo "FALLA  $f (tendría que dar error)"; fallos=$((fallos+1)); else echo "bien   $f"; fi
done
# compara la salida del compilador con el .esperado; $1 = archivo, $2 = opciones
comparar() {
  esperado="${1%.ra}.esperado"
  if diff <(dotnet bin/out/AR.dll $2 "$1") "$esperado" > /dev/null; then
    echo "bien   $1"
  else
    echo "FALLA  $1 (la salida no coincide con $esperado):"
    diff <(dotnet bin/out/AR.dll $2 "$1") "$esperado"
    fallos=$((fallos+1))
  fi
}
for f in pruebas/ast/*.ra; do comparar "$f" --ast; done
for f in pruebas/semanticas/*.ra; do comparar "$f" --esquemas; done
# el SQL se ejecuta con el módulo sqlite3 de Python (pruebas/ejecutar_sql.py)
for f in pruebas/sql/*.ra; do
  esperado="${f%.ra}.esperado"
  if diff <(dotnet bin/out/AR.dll "$f" | $python pruebas/ejecutar_sql.py) "$esperado" > /dev/null; then
    echo "bien   $f"
  else
    echo "FALLA  $f (el resultado en SQLite no coincide con $esperado):"
    diff <(dotnet bin/out/AR.dll "$f" | $python pruebas/ejecutar_sql.py) "$esperado"
    fallos=$((fallos+1))
  fi
done
echo "$fallos prueba(s) fallaron"
exit $fallos
