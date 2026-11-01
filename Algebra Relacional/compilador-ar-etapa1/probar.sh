#!/bin/bash
# Corre todas las pruebas: las válidas tienen que dar OK y las inválidas, error.
dotnet build -c Release -o bin/out > /dev/null || { echo "Falló la compilación"; exit 1; }
fallos=0
for f in pruebas/validas/*.ra; do
  if dotnet bin/out/AR.dll "$f" > /dev/null; then echo "bien   $f"; else echo "FALLA  $f (tendría que ser válida)"; fallos=$((fallos+1)); fi
done
for f in pruebas/invalidas/*.ra; do
  if dotnet bin/out/AR.dll "$f" > /dev/null; then echo "FALLA  $f (tendría que dar error)"; fallos=$((fallos+1)); else echo "bien   $f"; fi
done
echo "$fallos prueba(s) fallaron"
exit $fallos
