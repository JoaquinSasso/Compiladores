#!/bin/bash
# Corre todas las pruebas:
#   pruebas/validas/    → tienen que compilar sin errores
#   pruebas/invalidas/  → tienen que dar error
#   pruebas/ast/        → el árbol impreso tiene que ser igual al archivo .esperado
dotnet build -c Release -o bin/out > /dev/null || { echo "Falló la compilación"; exit 1; }
fallos=0
for f in pruebas/validas/*.ra; do
  if dotnet bin/out/AR.dll "$f" > /dev/null; then echo "bien   $f"; else echo "FALLA  $f (tendría que ser válida)"; fallos=$((fallos+1)); fi
done
for f in pruebas/invalidas/*.ra; do
  if dotnet bin/out/AR.dll "$f" > /dev/null; then echo "FALLA  $f (tendría que dar error)"; fallos=$((fallos+1)); else echo "bien   $f"; fi
done
for f in pruebas/ast/*.ra; do
  esperado="${f%.ra}.esperado"
  if diff <(dotnet bin/out/AR.dll "$f") "$esperado" > /dev/null; then
    echo "bien   $f"
  else
    echo "FALLA  $f (el árbol no coincide con $esperado):"
    diff <(dotnet bin/out/AR.dll "$f") "$esperado"
    fallos=$((fallos+1))
  fi
done
echo "$fallos prueba(s) fallaron"
exit $fallos
