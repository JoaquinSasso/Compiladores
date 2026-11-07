using System.Globalization;
using ARWeb.Servicios;
using Microsoft.Data.Sqlite;

// Hace lo mismo que ejecutarSql de wwwroot/js/app.js, pero con SQLite para .NET:
// ejecuta el script en una base en memoria y devuelve el resultado de cada consulta.
static class Sqlite {
    public static List<TablaSql> Ejecutar(string script) {
        using var conexion = new SqliteConnection("Data Source=:memory:");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = script;
        using var lector = comando.ExecuteReader();
        var resultados = new List<TablaSql>();
        do {
            if (lector.FieldCount == 0) continue;
            var t = new TablaSql();
            for (int i = 0; i < lector.FieldCount; i++) t.Columnas.Add(lector.GetName(i));
            while (lector.Read())
                t.Filas.Add(Enumerable.Range(0, lector.FieldCount).Select(i => Texto(lector.GetValue(i))).ToList());
            resultados.Add(t);
        } while (lector.NextResult());
        return resultados;
    }

    // Igual que texto() en app.js: los decimales con hasta 4 cifras.
    static string Texto(object v) => v switch {
        DBNull => null,
        double d when d == Math.Floor(d) => ((long)d).ToString(CultureInfo.InvariantCulture),
        double d => Math.Round(d, 4).ToString("R", CultureInfo.InvariantCulture),
        _ => Convert.ToString(v, CultureInfo.InvariantCulture),
    };
}
