using System.Text;

namespace ARWeb.Servicios;

// Importar una tabla desde un archivo CSV (por ejemplo, exportado de una
// planilla) y exportar un resultado como CSV o como tabla de Markdown.
public static class Csv {

    // Las palabras clave del lenguaje no pueden ser nombres de tablas ni de columnas.
    static readonly HashSet<string> reservadas = new(StringComparer.OrdinalIgnoreCase) {
        "pi", "sigma", "rho", "gamma", "union", "except", "intersect", "x", "cross", "join",
        "natural", "inner", "left", "right", "full", "outer", "count", "sum", "avg", "min", "max",
        "and", "or", "not",
    };

    // CSV → una tabla en el formato del dataset:  nombre = { columnas  filas }
    public static string ATabla(string nombre, string csv) {
        var filas = Leer(csv);
        if (filas.Count == 0) throw new FormatException("El archivo está vacío.");
        var columnas = filas[0].Select((c, i) => Identificador(c, "col" + (i + 1))).ToList();
        // Nombres repetidos: col, col_2, ...
        var usadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < columnas.Count; i++) {
            string b = columnas[i]; int n = 2;
            while (!usadas.Add(columnas[i])) columnas[i] = b + "_" + n++;
        }
        bool comaDecimal = Separador(csv) == ';';
        var sb = new StringBuilder();
        sb.Append(Identificador(nombre, "tabla")).Append(" = {\n");
        sb.Append(string.Join(", ", columnas)).Append('\n');
        foreach (var f in filas.Skip(1)) {
            if (f.All(string.IsNullOrWhiteSpace)) continue;
            var valores = Enumerable.Range(0, columnas.Count).Select(i => Valor(i < f.Count ? f[i] : "", comaDecimal));
            sb.Append(string.Join(", ", valores)).Append('\n');
        }
        sb.Append("}\n");
        return sb.ToString();
    }

    // Un valor del CSV como constante del lenguaje.
    static string Valor(string v, bool comaDecimal) {
        v = v.Trim();
        if (v.Length == 0 || v.Equals("null", StringComparison.OrdinalIgnoreCase)) return "null";
        if (v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("false", StringComparison.OrdinalIgnoreCase))
            return v.ToLowerInvariant();
        string n = comaDecimal ? v.Replace(',', '.') : v;
        if (System.Text.RegularExpressions.Regex.IsMatch(n, @"^-?\d+(\.\d+)?$")) return n;
        // Las cadenas del lenguaje no pueden tener comillas simples adentro.
        return "'" + v.Replace('\'', '’') + "'";
    }

    // Un nombre válido: solo letras sin acento, dígitos y _, y que no empiece con dígito.
    public static string Identificador(string s, string siVacio) {
        // (Sin string.Normalize: la app usa globalización invariante.)
        const string conAcento = "áéíóúüÁÉÍÓÚÜàèìòùÀÈÌÒÙ", sinAcento = "aeiouuAEIOUUaeiouAEIOU";
        var sb = new StringBuilder();
        foreach (char c in s.Trim().Replace("ñ", "ni").Replace("Ñ", "Ni")) {
            int k = conAcento.IndexOf(c);
            char ch = k >= 0 ? sinAcento[k] : c;
            sb.Append(ch < 128 && (char.IsLetterOrDigit(ch) || ch == '_') ? ch : '_');
        }
        string r = sb.ToString().Trim('_');
        if (r.Length == 0) r = siVacio;
        if (char.IsDigit(r[0])) r = "_" + r;
        if (reservadas.Contains(r)) r += "_";
        return r;
    }

    // El separador más probable: el que más aparece en la primera línea.
    static char Separador(string csv) {
        string primera = csv.Split('\n')[0];
        return new[] { ',', ';', '\t' }.OrderByDescending(c => primera.Count(x => x == c)).First();
    }

    // Lector de CSV con comillas dobles ("a, b" y "" para una comilla).
    public static List<List<string>> Leer(string csv) {
        char sep = Separador(csv);
        var filas = new List<List<string>>();
        var fila = new List<string>();
        var campo = new StringBuilder();
        bool entreComillas = false;
        for (int i = 0; i < csv.Length; i++) {
            char ch = csv[i];
            if (entreComillas) {
                if (ch == '"' && i + 1 < csv.Length && csv[i + 1] == '"') { campo.Append('"'); i++; }
                else if (ch == '"') entreComillas = false;
                else campo.Append(ch);
            } else if (ch == '"') entreComillas = true;
            else if (ch == sep) { fila.Add(campo.ToString()); campo.Clear(); }
            else if (ch == '\n') { fila.Add(campo.ToString()); campo.Clear(); filas.Add(fila); fila = new(); }
            else if (ch != '\r' && ch != '\uFEFF') campo.Append(ch);
        }
        if (campo.Length > 0 || fila.Count > 0) { fila.Add(campo.ToString()); filas.Add(fila); }
        return filas.Where(f => f.Any(c => c.Length > 0)).ToList();
    }

    /*------------------------------------------------------------ Exportar ---*/

    public static string Exportar(List<string> columnas, List<List<string>> filas) {
        var sb = new StringBuilder();
        sb.Append(string.Join(",", columnas.Select(Campo))).Append("\r\n");
        foreach (var f in filas) sb.Append(string.Join(",", f.Select(v => v == null ? "" : Campo(v)))).Append("\r\n");
        return sb.ToString();
    }

    static string Campo(string v) =>
        v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;

    public static string Markdown(List<string> columnas, List<List<string>> filas) {
        static string Celda(string v) => v == null ? "NULL" : v.Replace("|", "\\|").Replace("\n", " ");
        var sb = new StringBuilder();
        sb.Append("| ").Append(string.Join(" | ", columnas.Select(Celda))).Append(" |\n");
        sb.Append("|").Append(string.Join("|", columnas.Select(_ => "---"))).Append("|\n");
        foreach (var f in filas) sb.Append("| ").Append(string.Join(" | ", f.Select(Celda))).Append(" |\n");
        return sb.ToString();
    }
}
