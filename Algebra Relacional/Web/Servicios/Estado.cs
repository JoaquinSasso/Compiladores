namespace ARWeb.Servicios;

// Lo que la app recuerda entre visitas (se guarda como JSON en localStorage):
// los datasets, las pestañas de consulta, el historial, las guías abiertas y
// las respuestas de los ejercicios.
public class DatasetGuardado {
    public string Id { get; set; }
    public string Nombre { get; set; }
    public string Texto { get; set; } = "";
    public string Guia { get; set; }                 // si es el dataset de una guía, su Id
}

public class PestanaConsulta {
    public string Id { get; set; }
    public string Nombre { get; set; }
    public string Texto { get; set; } = "";
}

public class GuiaGuardada {
    public string Id { get; set; }
    public string Texto { get; set; }
    public bool Incluida { get; set; }               // la guía de ejemplo (no se puede borrar)
}

public class EjercicioGuardado {
    public string Guia { get; set; }
    public int Numero { get; set; }
    public string DatasetAnterior { get; set; }      // para volver a él al salir del ejercicio
}

public class Estado {
    public List<DatasetGuardado> Datasets { get; set; } = new();
    public string DatasetActual { get; set; }
    public List<PestanaConsulta> Pestanas { get; set; } = new();
    public string PestanaActual { get; set; }
    public List<string> Historial { get; set; } = new();
    public List<GuiaGuardada> Guias { get; set; } = new();
    public Dictionary<string, string> Respuestas { get; set; } = new();   // id del ejercicio → consulta
    public List<string> Resueltos { get; set; } = new();
    public bool ConvertirPalabras { get; set; } = true;
    // El ejercicio que se está resolviendo (null: la calculadora libre)
    public EjercicioGuardado Ejercicio { get; set; }

    public const int MaximoHistorial = 30;
    public const string IdEjemplo = "ejemplo";

    // El estado de la primera visita. Si había algo de la versión anterior
    // (que guardaba una sola consulta y un solo dataset), se conserva.
    public static Estado Nuevo(string consultaVieja = null, string datasetViejo = null) {
        var e = new Estado();
        e.Datasets.Add(new DatasetGuardado { Id = IdEjemplo, Nombre = "Clase de álgebra", Texto = Ejemplos.Dataset });
        if (datasetViejo != null && datasetViejo != Ejemplos.Dataset) {
            e.Datasets.Add(new DatasetGuardado { Id = NuevoId(), Nombre = "Mi dataset", Texto = datasetViejo });
        }
        e.DatasetActual = e.Datasets[^1].Id;
        e.Pestanas.Add(new PestanaConsulta { Id = NuevoId(), Nombre = "Consulta 1", Texto = consultaVieja ?? Ejemplos.Consulta });
        e.PestanaActual = e.Pestanas[0].Id;
        e.Completar();
        return e;
    }

    // Arregla un estado leído de localStorage (que podría venir incompleto).
    public void Completar() {
        Datasets ??= new(); Pestanas ??= new(); Historial ??= new(); Guias ??= new();
        Respuestas ??= new(); Resueltos ??= new();
        if (Datasets.Count == 0) Datasets.Add(new DatasetGuardado { Id = IdEjemplo, Nombre = "Clase de álgebra", Texto = Ejemplos.Dataset });
        if (Dataset == null) DatasetActual = Datasets[0].Id;
        if (Pestanas.Count == 0) Pestanas.Add(new PestanaConsulta { Id = NuevoId(), Nombre = "Consulta 1", Texto = "" });
        if (Pestana == null) PestanaActual = Pestanas[0].Id;
        if (!Guias.Any(g => g.Incluida))
            Guias.Insert(0, new GuiaGuardada { Id = IdEjemplo, Texto = ARWeb.Servicios.Guias.GuiaEjemplo, Incluida = true });
        else
            Guias.First(g => g.Incluida).Texto = ARWeb.Servicios.Guias.GuiaEjemplo;     // siempre la versión actual
    }

    public DatasetGuardado Dataset => Datasets.FirstOrDefault(d => d.Id == DatasetActual);
    public PestanaConsulta Pestana => Pestanas.FirstOrDefault(p => p.Id == PestanaActual);

    public static string NuevoId() => Guid.NewGuid().ToString("N")[..10];

    /*----------------------------------------------------------- Pestañas ---*/

    public PestanaConsulta NuevaPestana(string texto = "", string nombre = null) {
        int n = 1;
        while (Pestanas.Any(p => p.Nombre == "Consulta " + n)) n++;
        var p = new PestanaConsulta { Id = NuevoId(), Nombre = nombre ?? "Consulta " + n, Texto = texto };
        Pestanas.Add(p);
        PestanaActual = p.Id;
        return p;
    }

    public void CerrarPestana(string id) {
        int i = Pestanas.FindIndex(p => p.Id == id);
        if (i < 0) return;
        if (!string.IsNullOrWhiteSpace(Pestanas[i].Texto)) AgregarHistorial(Pestanas[i].Texto);
        Pestanas.RemoveAt(i);
        if (Pestanas.Count == 0) Pestanas.Add(new PestanaConsulta { Id = NuevoId(), Nombre = "Consulta 1" });
        if (PestanaActual == id) PestanaActual = Pestanas[Math.Min(i, Pestanas.Count - 1)].Id;
    }

    /*---------------------------------------------------------- Historial ---*/

    public void AgregarHistorial(string texto) {
        texto = texto?.Trim();
        if (string.IsNullOrEmpty(texto)) return;
        Historial.Remove(texto);
        Historial.Insert(0, texto);
        if (Historial.Count > MaximoHistorial) Historial.RemoveRange(MaximoHistorial, Historial.Count - MaximoHistorial);
    }

    /*----------------------------------------------------------- Datasets ---*/

    public DatasetGuardado NuevoDataset(string nombre, string texto, string guia = null) {
        string baseNombre = nombre; int n = 2;
        while (Datasets.Any(d => d.Nombre == nombre)) nombre = baseNombre + " (" + n++ + ")";
        var d = new DatasetGuardado { Id = NuevoId(), Nombre = nombre, Texto = texto, Guia = guia };
        Datasets.Add(d);
        DatasetActual = d.Id;
        return d;
    }

    public void EliminarDataset(string id) {
        int i = Datasets.FindIndex(d => d.Id == id);
        if (i < 0) return;
        Datasets.RemoveAt(i);
        if (Datasets.Count == 0) Datasets.Add(new DatasetGuardado { Id = NuevoId(), Nombre = "Dataset vacío" });
        if (DatasetActual == id) DatasetActual = Datasets[Math.Min(i, Datasets.Count - 1)].Id;
    }

    // Abre algo compartido: si ya hay un dataset con el mismo texto, se usa ese.
    public DatasetGuardado BuscarOCrearDataset(string nombre, string texto) {
        var d = Datasets.FirstOrDefault(x => x.Texto == texto);
        if (d != null) { DatasetActual = d.Id; return d; }
        return NuevoDataset(nombre, texto);
    }

    /*------------------------------------------------------------- Guías ---*/

    public GuiaGuardada AgregarGuia(string texto) {
        var g = Guias.FirstOrDefault(x => x.Texto == texto);
        if (g != null) return g;
        g = new GuiaGuardada { Id = NuevoId(), Texto = texto };
        Guias.Add(g);
        return g;
    }

    public void EliminarGuia(string id) {
        Guias.RemoveAll(g => g.Id == id && !g.Incluida);
        Datasets.RemoveAll(d => d.Guia == id);
        if (Ejercicio?.Guia == id) Ejercicio = null;
        if (Dataset == null) DatasetActual = Datasets.FirstOrDefault()?.Id;
        Completar();
    }

    // El dataset de una guía se copia a la lista de datasets (así se ve en la
    // pestaña Dataset); si ya estaba, se reutiliza.
    public DatasetGuardado DatasetDeGuia(string guiaId, string titulo, string texto) {
        var d = Datasets.FirstOrDefault(x => x.Guia == guiaId);
        if (d == null) {
            d = new DatasetGuardado { Id = NuevoId(), Nombre = "Guía: " + titulo, Texto = texto, Guia = guiaId };
            Datasets.Add(d);
        }
        return d;
    }

    public static string IdEjercicio(string guia, int numero) => guia + "#" + numero;
}
