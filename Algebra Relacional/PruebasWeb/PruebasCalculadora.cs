using ARWeb;
using ARWeb.Servicios;
using Xunit;

// La compilación con dataset y consulta separados, el árbol paso a paso y las sugerencias.
public class PruebasCalculadora {
    const string Dataset = "R = {\na, b\n1, 'x'\n2, 'y'\n}\nS = {\nb, c\n'x', 10\n}\n";   // 9 líneas (con la vacía del final)

    [Fact]
    public void ErrorDeLaConsultaConSuLinea() {
        var c = Calculadora.Compilar(Dataset, "π a (R)\nσ z = 1 (R)");
        var e = Assert.Single(c.Errores);
        Assert.Equal(Origen.Consulta, e.Origen);
        Assert.Equal(2, e.Linea);
        Assert.Equal(3, e.Col);
        Assert.Equal("Error semántico", c.TituloErrores);
    }

    [Fact]
    public void ErrorDeSintaxisEnCastellano() {
        var c = Calculadora.Compilar(Dataset, "π a (R");
        var e = Assert.Single(c.Errores);
        Assert.Equal("se esperaba \")\"", e.Mensaje);
        Assert.Equal("Error de sintaxis", c.TituloErrores);
    }

    [Fact]
    public void ElDatasetConErroresNoSeMezclaConLaConsulta() {
        // A la tabla le falta la llave de cierre: el error podría aparecer en la consulta.
        var c = Calculadora.Compilar("R = {\na\n1\n", "π a (R)");
        Assert.True(c.DatasetConErrores);
        Assert.All(c.Errores, e => Assert.Equal(Origen.Dataset, e.Origen));
    }

    [Fact]
    public void TextoDeCadaConsulta() {
        string consulta = "-- primera\nπ a\n  (R);\n\n-- segunda\nt = σ a = 1 (R)\nπ b (t)  -- fin";
        var c = Calculadora.Compilar(Dataset, consulta);
        Assert.True(c.Ok);
        Assert.Equal(2, c.Consultas.Count);
        Assert.Equal("π a\n  (R)", c.Consultas[0].Texto);
        Assert.Equal("π b (t)  -- fin", c.Consultas[1].Texto);
        Assert.Single(c.Consultas[1].Asignaciones, a => a.Nombre == "t");
    }

    [Fact]
    public void ArbolConVariablesAbiertas() {
        var c = Calculadora.Compilar(Dataset, "t = σ a = 1 (R)\nπ c (t ⋈ S)");
        var nodos = Calculadora.Arbol(c.Consultas[0]);
        Assert.Equal(new[] { "π c", "⋈ (natural)", "t", "σ a = 1", "R", "S" }, nodos.Select(n => n.Etiqueta));
        Assert.Equal("t", nodos[2].Variable);
        Assert.Equal(nodos[3], Assert.Single(nodos[2].Hijos));
        // Un padre queda centrado sobre sus hijos
        Assert.Equal((nodos[2].X + nodos[5].X) / 2, nodos[1].X);
    }

    [Fact]
    public void CadaPasoDelArbolTieneSuResultado() {
        var c = Calculadora.Compilar(Dataset, "t = σ a = 1 (R)\nπ c (t ⋈ S)");
        var nodos = Calculadora.Arbol(c.Consultas[0]);
        var r = Sqlite.Ejecutar(Calculadora.ScriptPasos(c.Consultas[0], nodos.Select(n => n.Expr)));
        Assert.Equal(nodos.Count, r.Count);
        Assert.Equal(new[] { 1, 1, 1, 1, 2, 1 }, r.Select(t => t.Filas.Count));
        Assert.Equal("10", r[0].Filas[0][0]);
    }

    [Fact]
    public void Posicion() {
        Assert.Equal(0, Calculadora.Posicion("ab\ncd", 1, 1));
        Assert.Equal(4, Calculadora.Posicion("ab\ncd", 2, 2));
        Assert.Equal(5, Calculadora.Posicion("ab\ncd", 9, 1));
    }

    [Fact]
    public void Sugerencias() {
        var ds = Calculadora.AnalizarDataset(Ejemplos.Dataset);
        Assert.Equal(new[] { "alumno", "areaConoc" }, Calculadora.Sugerencias("a", ds, ""));
        Assert.Equal(new[] { "examen.codM" }, Calculadora.Sugerencias("examen.c", ds, "").Where(s => s.Contains("cod")));
        Assert.Contains("aprobados", Calculadora.Sugerencias("apr", ds, "aprobados = π cuil (examen)\nπ nyAp (alumno ⋈ apr"));
        Assert.Empty(Calculadora.Sugerencias("alumno", ds, ""));          // ya está completa
    }

    [Fact]
    public void TablasDelDataset() {
        var ds = Calculadora.AnalizarDataset(Ejemplos.Dataset + "\nv = π cuil (alumno)\nn = {\na\nnull\n}");
        Assert.Equal(new[] { "materia", "alumno", "examen", "boleta", "v", "n" }, ds.Tablas.Select(t => t.Nombre));
        Assert.True(ds.Tablas[4].EsVariable);
        Assert.Equal(4, ds.Tablas[1].Filas.Count);
        Assert.Null(ds.Tablas[5].Filas[0][0]);                          // null se muestra como NULL
    }

    [Fact]
    public void LosEjemplosDeLaAyudaFuncionan() {
        var ds = Calculadora.AnalizarDataset(Ejemplos.Dataset);
        foreach (var e in Ayuda.Secciones.SelectMany(s => s.Entradas)) {
            var c = Calculadora.Compilar(Ejemplos.Dataset, e.Ejemplo, ds);
            Assert.True(c.Ok, e.Nombre + ": " + string.Join("; ", c.Errores.Select(x => x.Mensaje)));
            var r = Sqlite.Ejecutar(c.Sql);
            Assert.True(r[^1].Filas.Count > 0, e.Nombre + ": el ejemplo da una relación vacía");
        }
    }
}
