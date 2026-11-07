using ARWeb.Servicios;
using Xunit;

// Las guías de ejercicios y la corrección.
public class PruebasEjercicios {
    static TablaSql Tabla(string[] columnas, params string[][] filas) =>
        new() { Columnas = columnas.ToList(), Filas = filas.Select(f => f.ToList()).ToList() };

    [Fact]
    public void LeerUnaGuia() {
        var g = Guias.Leer("## guía: Práctica\n## dataset\nR = {\na\n1\n}\n## ejercicio: Uno\nHacer algo.\nEn dos líneas.\n## Solución\nπ a (R)\n## ejercicio\nOtro\n## solucion\nR\n");
        Assert.Empty(g.Errores);
        Assert.Equal("Práctica", g.Titulo);
        Assert.Equal("R = {\na\n1\n}\n", g.Dataset);
        Assert.Equal(2, g.Ejercicios.Count);
        Assert.Equal("Hacer algo.\nEn dos líneas.", g.Ejercicios[0].Enunciado);
        Assert.Equal("π a (R)", g.Ejercicios[0].Solucion);
        Assert.Equal("Ejercicio 2", g.Ejercicios[1].Titulo);
    }

    [Fact]
    public void GuiaConProblemas() {
        var g = Guias.Leer("## dataset\nR = {a\n1}\n## ejercicio: sin solución\nalgo\n## otra cosa\n");
        Assert.Equal(2, g.Errores.Count);
        Assert.Contains(g.Errores, e => e.Contains("no tiene solución"));
        Assert.Contains(g.Errores, e => e.Contains("sección desconocida"));
    }

    // Cada solución de la guía de ejemplo compila y da un resultado no vacío.
    [Fact]
    public void LaGuiaDeEjemploFunciona() {
        var g = Guias.Leer(Guias.GuiaEjemplo);
        Assert.Empty(g.Errores);
        Assert.Equal(9, g.Ejercicios.Count);
        var ds = Calculadora.AnalizarDataset(g.Dataset);
        Assert.Empty(ds.Errores);
        foreach (var e in g.Ejercicios) {
            var c = Calculadora.Compilar(g.Dataset, e.Solucion, ds);
            Assert.True(c.Ok, e.Titulo + ": " + string.Join("; ", c.Errores.Select(x => x.Mensaje)));
            Assert.NotEmpty(Sqlite.Ejecutar(c.Sql)[^1].Filas);
        }
    }

    // Una respuesta escrita de otra forma (otro alias, otro orden de columnas) también es correcta.
    [Fact]
    public void RespuestaEquivalente() {
        var g = Guias.Leer(Guias.GuiaEjemplo);
        var ej = g.Ejercicios.Single(e => e.Titulo == "Cantidad de exámenes por alumno");
        var sol = Sqlite.Ejecutar(Calculadora.Compilar(g.Dataset, ej.Solucion).Sql)[^1];
        var resp = Sqlite.Ejecutar(Calculadora.Compilar(g.Dataset, "γ cuil; count(codM) → n (examen)").Sql)[^1];
        Assert.True(Comparador.Comparar(sol, resp).Correcto);
    }

    [Fact]
    public void ColumnasPorNombreEnOtroOrden() {
        var esperada = Tabla(new[] { "a", "b" }, new[] { "1", "x" });
        var obtenida = Tabla(new[] { "B", "a" }, new[] { "x", "1" });
        Assert.True(Comparador.Comparar(esperada, obtenida).Correcto);
    }

    [Fact]
    public void FilasRepetidasYOrdenNoImportan() {
        var esperada = Tabla(new[] { "a" }, new[] { "1" }, new[] { "2" });
        var obtenida = Tabla(new[] { "z" }, new[] { "2" }, new[] { "1" }, new[] { "2" });
        var c = Comparador.Comparar(esperada, obtenida);
        Assert.True(c.Correcto);
        Assert.Contains("2 filas", c.Mensaje);
    }

    [Fact]
    public void FaltanYSobran() {
        var esperada = Tabla(new[] { "a" }, new[] { "1" }, new[] { "2" }, new string[] { null });
        var obtenida = Tabla(new[] { "a" }, new[] { "2" }, new[] { "3" }, new[] { "NULL" });
        var c = Comparador.Comparar(esperada, obtenida);
        Assert.False(c.Correcto);
        Assert.Equal(new[] { "1", null }, c.Faltan.Select(f => f[0]));
        Assert.Equal(new[] { "3", "NULL" }, c.Sobran.Select(f => f[0]));
        Assert.Equal("Todavía no: faltan 2 filas y sobran 2 filas.", c.Mensaje);
    }

    [Fact]
    public void DistintaCantidadDeColumnas() {
        var c = Comparador.Comparar(Tabla(new[] { "a" }), Tabla(new[] { "a", "b" }));
        Assert.False(c.Correcto);
        Assert.StartsWith("El resultado tiene 2 columnas y se esperaba 1", c.Mensaje.Replace("esperaban", "esperaba"));
    }
}
