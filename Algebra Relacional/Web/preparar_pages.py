"""Prepara la versión publicada de la app para GitHub Pages.

Uso:  python preparar_pages.py <carpeta wwwroot publicada> <ruta base>
      python preparar_pages.py Web/bin/publicada/wwwroot /Compiladores/

GitHub Pages sirve el sitio en una subcarpeta (https://usuario.github.io/Compiladores/),
así que hay que:
  1. cambiar <base href="/"> de index.html por la subcarpeta;
  2. actualizar el hash de index.html en service-worker-assets.js (el service worker
     verifica la integridad de cada archivo al guardarlo en caché; si no coincide,
     no se instala y la app no funciona sin conexión);
  3. copiar index.html como 404.html (GitHub Pages responde 404.html para las
     rutas que no existen, y así la app igual arranca);
  4. agregar .nojekyll (si no, GitHub Pages ignora la carpeta _framework).
"""
import base64
import hashlib
import json
import pathlib
import re
import sys


def main():
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    carpeta = pathlib.Path(sys.argv[1])
    base = "/" + sys.argv[2].strip("/") + "/" if sys.argv[2].strip("/") else "/"

    indice = carpeta / "index.html"
    html = indice.read_text(encoding="utf-8")
    html, cambios = re.subn(r'<base href="[^"]*"\s*/?>', f'<base href="{base}" />', html)
    if cambios != 1:
        sys.exit("No se encontró <base href> en index.html")
    indice.write_text(html, encoding="utf-8", newline="\n")
    contenido = indice.read_bytes()

    # Las versiones comprimidas de index.html quedaron viejas: GitHub Pages no las usa.
    for viejo in ("index.html.br", "index.html.gz"):
        (carpeta / viejo).unlink(missing_ok=True)

    activos = carpeta / "service-worker-assets.js"
    texto = activos.read_text(encoding="utf-8")
    prefijo = "self.assetsManifest = "
    manifiesto = json.loads(texto[len(prefijo):].rstrip().rstrip(";"))
    hash_nuevo = "sha256-" + base64.b64encode(hashlib.sha256(contenido).digest()).decode()
    for activo in manifiesto["assets"]:
        if activo["url"] == "index.html":
            activo["hash"] = hash_nuevo
    activos.write_text(prefijo + json.dumps(manifiesto, indent=2) + ";\n", encoding="utf-8", newline="\n")

    (carpeta / "404.html").write_bytes(contenido)
    (carpeta / ".nojekyll").write_text("", encoding="utf-8")
    print(f"Listo: base {base}, hash de index.html {hash_nuevo}")


if __name__ == "__main__":
    main()
