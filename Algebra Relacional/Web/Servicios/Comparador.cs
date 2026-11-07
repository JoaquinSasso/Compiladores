namespace ARWeb.Servicios;

// Corrige un ejercicio: compara el resultado del alumno con el de la solución.
//
// En el álgebra relacional una relación es un CONJUNTO de filas, así que no
// importa el orden de las filas ni si alguna aparece repetida. Las columnas:
// si las dos tienen los mismos nombres (en cualquier orden), se comparan por
// nombre; si no, por posición (así no importa qué alias eligió el alumno).
public class Correccion {
    public bool Correcto;
    public string Mensaje;
    public List<string> Columnas = new();
    public List<List<string>> Faltan = new();       // están en la solución y no en el resultado
    public List<List<string>> Sobran = new();       // están en el resultado y no en la solución
}

public static class Comparador {
    public static Correccion Comparar(TablaSql esperada, TablaSql obtenida) {
        var c = new Correccion { Columnas = esperada.Columnas };
        int ge = esperada.Columnas.Count, go = obtenida.Columnas.Count;
        if (ge != go) {
            c.Mensaje = $"El resultado tiene {go} {Plural(go, "columna", "columnas")} y se esperaban {ge} " +
                        $"({string.Join(", ", esperada.Columnas)}).";
            return c;
        }

        // ¿Se pueden emparejar las columnas por nombre?
        var orden = Enumerable.Range(0, go).ToArray();
        var porNombre = esperada.Columnas.Select(n => obtenida.Columnas.FindIndex(o => Igual(o, n))).ToArray();
        if (porNombre.All(i => i >= 0) && porNombre.Distinct().Count() == go) orden = porNombre;

        var filasEsperadas = new HashSet<string>(esperada.Filas.Select(Clave));
        var filasObtenidas = obtenida.Filas.Select(f => orden.Select(i => f[i]).ToList()).ToList();
        var clavesObtenidas = new HashSet<string>(filasObtenidas.Select(Clave));

        c.Faltan = Distintas(esperada.Filas.Where(f => !clavesObtenidas.Contains(Clave(f))));
        c.Sobran = Distintas(filasObtenidas.Where(f => !filasEsperadas.Contains(Clave(f))));
        c.Correcto = c.Faltan.Count == 0 && c.Sobran.Count == 0;
        if (c.Correcto) {
            int n = filasEsperadas.Count;
            c.Mensaje = $"¡Correcto! El resultado coincide con la solución ({n} {Plural(n, "fila", "filas")}).";
        } else {
            var partes = new List<string>();
            if (c.Faltan.Count > 0) partes.Add($"faltan {c.Faltan.Count} {Plural(c.Faltan.Count, "fila", "filas")}");
            if (c.Sobran.Count > 0) partes.Add($"sobran {c.Sobran.Count} {Plural(c.Sobran.Count, "fila", "filas")}");
            string m = string.Join(" y ", partes);
            c.Mensaje = "Todavía no: " + m + ".";
        }
        return c;
    }

    static bool Igual(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    // Las filas se comparan por su texto (NULL distinto de la cadena "NULL").
    static string Clave(List<string> f) => string.Join("\u001f", f.Select(v => v == null ? "\u0000" : v));

    static List<List<string>> Distintas(IEnumerable<List<string>> filas) {
        var vistas = new HashSet<string>();
        return filas.Where(f => vistas.Add(Clave(f))).ToList();
    }

    static string Plural(int n, string uno, string varios) => n == 1 ? uno : varios;
}
