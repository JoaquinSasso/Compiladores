using ARWeb;
using ARWeb.Servicios;
using Xunit;

// CSV, el estado guardado y la traducción de los mensajes de Coco/R.
public class PruebasVarias {
    [Fact]
    public void ImportarCsv() {
        string csv = "Código;Nombre y apellido;Promedio;x\r\n1;\"Pérez; Juan\";7,5;\r\n2;O'Higgins;8;true\r\n";
        string tabla = Csv.ATabla("Mis alumnos", csv);
        Assert.Equal("Mis_alumnos = {\nCodigo, Nombre_y_apellido, Promedio, x_\n1, 'Pérez; Juan', 7.5, null\n2, 'O’Higgins', 8, true\n}\n", tabla);
        // y el resultado es un dataset válido
        var ds = Calculadora.AnalizarDataset(tabla);
        Assert.Empty(ds.Errores);
        Assert.Equal("número", Semantico.NombreDominio(ds.Tablas[0].Atributos[2].Tipo));
    }

    [Fact]
    public void ColumnasRepetidasEnElCsv() {
        Assert.StartsWith("t = {\na, A_2, col3\n", Csv.ATabla("t", "a,A,\n1,2,3\n"));
    }

    [Fact]
    public void Identificadores() {
        Assert.Equal("_2024", Csv.Identificador("2024", "t"));
        Assert.Equal("JOIN_", Csv.Identificador("JOIN", "t"));
        Assert.Equal("t", Csv.Identificador("¿?", "t"));
        Assert.Equal("anio", Csv.Identificador("año", "t"));
    }

    [Fact]
    public void ExportarCsvYMarkdown() {
        var cols = new List<string> { "a", "b" };
        var filas = new List<List<string>> { new() { "1, 2", null }, new() { "say \"hi\"", "x|y" } };
        Assert.Equal("a,b\r\n\"1, 2\",\r\n\"say \"\"hi\"\"\",x|y\r\n", Csv.Exportar(cols, filas));
        Assert.Equal("| a | b |\n|---|---|\n| 1, 2 | NULL |\n| say \"hi\" | x\\|y |\n", Csv.Markdown(cols, filas));
    }

    [Fact]
    public void EstadoNuevoConservaLoDeLaVersionAnterior() {
        var e = Estado.Nuevo("π a (R)", "R = {\na\n1\n}");
        Assert.Equal(2, e.Datasets.Count);
        Assert.Equal("Mi dataset", e.Dataset.Nombre);
        Assert.Equal("π a (R)", e.Pestana.Texto);
        Assert.Single(e.Guias, g => g.Incluida);
    }

    [Fact]
    public void PestanasEHistorial() {
        var e = Estado.Nuevo();
        var p2 = e.NuevaPestana("π a (R)");
        Assert.Equal("Consulta 2", p2.Nombre);
        Assert.Equal(p2.Id, e.PestanaActual);
        e.CerrarPestana(p2.Id);
        Assert.Equal("π a (R)", e.Historial[0]);
        Assert.Equal("Consulta 1", e.Pestana.Nombre);
        // el historial no repite y tiene un máximo
        for (int i = 0; i < 40; i++) e.AgregarHistorial("q" + (i % 35));
        e.AgregarHistorial("q3");
        Assert.Equal(Estado.MaximoHistorial, e.Historial.Count);
        Assert.Equal("q3", e.Historial[0]);
        Assert.Single(e.Historial, h => h == "q3");
    }

    [Fact]
    public void GuiasYSusDatasets() {
        var e = Estado.Nuevo();
        var g = e.AgregarGuia("## ejercicio: a\n## solución\nR");
        Assert.Same(g, e.AgregarGuia("## ejercicio: a\n## solución\nR"));      // no se duplica
        var d = e.DatasetDeGuia(g.Id, "Mi guía", "R = {\na\n1\n}");
        Assert.Same(d, e.DatasetDeGuia(g.Id, "Mi guía", "otra cosa"));
        e.Ejercicio = new EjercicioGuardado { Guia = g.Id, Numero = 1 };
        e.EliminarGuia(g.Id);
        Assert.DoesNotContain(e.Guias, x => x.Id == g.Id);
        Assert.DoesNotContain(e.Datasets, x => x.Id == d.Id);
        Assert.Null(e.Ejercicio);
        e.EliminarGuia(Estado.IdEjemplo);                                    // la de ejemplo no se borra
        Assert.Single(e.Guias);
    }

    [Theory]
    [InlineData("\")\" expected", "se esperaba \")\"")]
    [InlineData("ident expected", "se esperaba un nombre")]
    [InlineData("EOF expected", "se esperaba el fin del programa (sobra algo)")]
    [InlineData("invalid OpRel", "se esperaba un comparador (=, ≠, <, >, ≤, ≥)")]
    [InlineData("invalid Inventado", "se esperaba Inventado")]
    [InlineData("otro mensaje", "otro mensaje")]
    public void MensajesDeSintaxis(string ingles, string castellano) =>
        Assert.Equal(castellano, Compilador.TraducirSintaxis(ingles));
}
