namespace ARWeb.Servicios;

// Guías de ejercicios. Una guía es un archivo de texto con este formato
// (las líneas que empiezan con ## separan las secciones; no pueden aparecer
// en el álgebra, donde los comentarios son -- y /* */):
//
//   ## guía: Clase de álgebra
//   ## dataset
//   alumno = { ... }
//   ## ejercicio: Nombres de los alumnos
//   Mostrar el nombre de todos los alumnos.
//   ## solución
//   π nyAp (alumno)
//   ## ejercicio: ...
public class Ejercicio {
    public int Numero;
    public string Titulo, Enunciado = "", Solucion = "";
}

public class Guia {
    public string Titulo = "Guía sin título", Dataset = "";
    public List<Ejercicio> Ejercicios = new();
    public List<string> Errores = new();
}

public static class Guias {
    enum Seccion { Ninguna, Dataset, Enunciado, Solucion }

    public static Guia Leer(string texto) {
        var g = new Guia();
        var dataset = new List<string>();
        var actual = Seccion.Ninguna;
        Ejercicio ej = null;
        var lineas = (texto ?? "").Replace("\r", "").Split('\n');
        for (int i = 0; i < lineas.Length; i++) {
            string l = lineas[i];
            if (l.StartsWith("##")) {
                string marca = l.TrimStart('#').Trim();
                string clave = marca, valor = "";
                int dos = marca.IndexOf(':');
                if (dos >= 0) { clave = marca[..dos].Trim(); valor = marca[(dos + 1)..].Trim(); }
                switch (Normalizar(clave)) {
                    case "guia": g.Titulo = valor.Length > 0 ? valor : g.Titulo; actual = Seccion.Ninguna; continue;
                    case "dataset": actual = Seccion.Dataset; continue;
                    case "ejercicio":
                        ej = new Ejercicio { Numero = g.Ejercicios.Count + 1, Titulo = valor.Length > 0 ? valor : "Ejercicio " + (g.Ejercicios.Count + 1) };
                        g.Ejercicios.Add(ej);
                        actual = Seccion.Enunciado; continue;
                    case "solucion":
                        if (ej == null) g.Errores.Add($"línea {i + 1}: hay una solución antes del primer ejercicio");
                        actual = Seccion.Solucion; continue;
                    default:
                        g.Errores.Add($"línea {i + 1}: sección desconocida \"{marca}\" (se esperaba guía, dataset, ejercicio o solución)");
                        continue;
                }
            }
            switch (actual) {
                case Seccion.Dataset: dataset.Add(l); break;
                case Seccion.Enunciado when ej != null: ej.Enunciado += l + "\n"; break;
                case Seccion.Solucion when ej != null: ej.Solucion += l + "\n"; break;
            }
        }
        g.Dataset = string.Join("\n", dataset).Trim() + "\n";
        foreach (var e in g.Ejercicios) {
            e.Enunciado = e.Enunciado.Trim();
            e.Solucion = e.Solucion.Trim();
            if (e.Solucion.Length == 0) g.Errores.Add($"el ejercicio {e.Numero} ({e.Titulo}) no tiene solución");
        }
        if (g.Ejercicios.Count == 0) g.Errores.Add("la guía no tiene ejercicios (cada uno empieza con \"## ejercicio: título\")");
        return g;
    }

    // "Solución" → "solucion", "GUÍA" → "guia"
    static string Normalizar(string s) => s.ToLowerInvariant()
        .Replace('á', 'a').Replace('é', 'e').Replace('í', 'i').Replace('ó', 'o').Replace('ú', 'u');

    // La guía que viene con la app: ejercicios sobre el dataset de la clase de álgebra.
    public const string GuiaEjemplo =
@"## guía: Clase de álgebra
## dataset
" + Ejemplos.Dataset + @"
## ejercicio: Nombres de los alumnos
Mostrar el nombre de todos los alumnos.
## solución
π nyAp (alumno)

## ejercicio: Exámenes aprobados
Mostrar todos los datos de los exámenes aprobados (nota mayor o igual a 4).
## solución
σ nota >= 4 (examen)

## ejercicio: Materias del área AyP
Mostrar el código y el nombre de las materias del área 'AyP'.
## solución
π codM, nomM (σ areaConoc = 'AyP' (materia))

## ejercicio: Alumnos que aprobaron algún examen
Mostrar el nombre de los alumnos que aprobaron al menos un examen.
## solución
π nyAp (alumno ⋈ σ nota >= 4 (examen))

## ejercicio: Alumnos que nunca rindieron
Mostrar el cuil y el nombre de los alumnos que no rindieron ningún examen.
## solución
π cuil, nyAp (alumno) - π cuil, nyAp (alumno ⋈ examen)

## ejercicio: Boletas sin examen
Mostrar el cuil y el código de materia de las boletas (inscripciones) que no tienen un examen rendido en esa materia.
## solución
π cuil, codM (boleta) - π cuil, codM (examen)

## ejercicio: Cantidad de exámenes por alumno
Para cada alumno que rindió, mostrar su cuil y la cantidad de exámenes que rindió.
## solución
γ cuil; count(*) → cantidad (examen)

## ejercicio: Mejor nota de cada materia
Para cada materia que tiene exámenes, mostrar su nombre y la nota más alta.
## solución
γ nomM; max(nota) → maxima (materia ⋈ examen)

## ejercicio: Inscriptos en todas las materias de AyP
Mostrar el cuil de los alumnos que tienen boleta en todas las materias del área 'AyP'.
## solución
π cuil, codM (boleta) ÷ π codM (σ areaConoc = 'AyP' (materia))
";
}
