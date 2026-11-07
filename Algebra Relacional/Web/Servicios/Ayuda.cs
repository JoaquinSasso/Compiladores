namespace ARWeb.Servicios;

// El contenido de la pestaña Ayuda: cada operador con su símbolo, cómo se
// escribe con el teclado y un ejemplo que se puede probar con el dataset de
// la clase de álgebra (Ejemplos.Dataset).
public record EntradaAyuda(string Nombre, string Simbolo, string Palabras, string Descripcion, string Ejemplo);
public record SeccionAyuda(string Titulo, EntradaAyuda[] Entradas);

public static class Ayuda {
    public static readonly SeccionAyuda[] Secciones = {
        new("Operadores unarios", new EntradaAyuda[] {
            new("Selección", "σ", "sigma", "Deja las filas que cumplen la condición.",
                "σ nota >= 4 (examen)"),
            new("Proyección", "π", "pi", "Se queda con algunas columnas, sin filas repetidas. Con → se le cambia el nombre a una columna.",
                "π nyAp, cuil → id (alumno)"),
            new("Renombre de la relación", "ρ", "rho", "Cambia el nombre de la relación (útil para combinar una relación consigo misma).",
                "ρ A (alumno)"),
            new("Renombre de atributos", "ρ", "rho", "Cambia el nombre de uno o más atributos: nuevo ← viejo (o viejo → nuevo).",
                "ρ nombre ← nyAp (alumno)"),
            new("Agrupamiento", "γ", "gamma", "Agrupa por los atributos que van antes del ; y calcula count, sum, avg, min o max de cada grupo.",
                "γ cuil; count(*) → rendidos, avg(nota) → promedio (examen)"),
        }),
        new("Operadores binarios", new EntradaAyuda[] {
            new("Unión", "∪", "union", "Las filas de las dos relaciones. Tienen que tener la misma cantidad de columnas, del mismo tipo.",
                "π cuil (examen) ∪ π cuil (boleta)"),
            new("Diferencia", "−", "- o except", "Las filas de la izquierda que no están en la derecha.",
                "π cuil (alumno) - π cuil (examen)"),
            new("Intersección", "∩", "intersect", "Las filas que están en las dos relaciones.",
                "π cuil (examen) ∩ π cuil (boleta)"),
            new("Producto cartesiano", "×", "x o cross join", "Cada fila de la izquierda con cada fila de la derecha.",
                "π nyAp (alumno) × π nomM (materia)"),
            new("Join natural", "⋈", "join o natural join", "Combina las filas que coinciden en los atributos con el mismo nombre.",
                "π nyAp, nota (alumno ⋈ examen)"),
            new("Theta-join", "⋈", "join o inner join", "Combina las filas que cumplen una condición (va entre el ⋈ y la relación de la derecha).",
                "alumno ⋈ alumno.cuil = boleta.cuil and codM = 4 boleta"),
            new("Join externo izquierdo", "⟕", "left join", "Como el join, pero también quedan las filas de la izquierda sin pareja, con NULL.",
                "π nyAp, nota (alumno ⟕ examen)"),
            new("Join externo derecho", "⟖", "right join", "Como el join, pero también quedan las filas de la derecha sin pareja.",
                "π cuil, nomM (examen ⟖ materia)"),
            new("Join externo completo", "⟗", "full join", "Quedan las filas sin pareja de los dos lados.",
                "π nyAp, nomM (alumno ⟗ boleta ⟗ materia)"),
            new("División", "÷", "/", "Los valores de la izquierda que aparecen combinados con todas las filas de la derecha.",
                "π cuil, codM (boleta) ÷ π codM (σ areaConoc = 'AyP' (materia))"),
        }),
        new("Condiciones", new EntradaAyuda[] {
            new("Comparaciones", "= ≠ < > ≤ ≥", "= <> != < > <= >=", "Comparan un atributo con otro, con un número o con una cadena entre comillas simples.",
                "σ nomM <> 'Optativa' (materia)"),
            new("Y, o, no", "∧ ∨ ¬", "and or not", "Combinan comparaciones. Se pueden usar paréntesis.",
                "σ (nota >= 4 and codM = 1) or not cuil = 10 (examen)"),
        }),
        new("Variables", new EntradaAyuda[] {
            new("Asignación", "=", "nombre = expresión", "Guarda el resultado de una expresión con un nombre, para usarlo después. Cada expresión suelta muestra un resultado.",
                "aprobados = π cuil (σ nota >= 4 (examen))\nπ nyAp (alumno ⋈ aprobados)"),
        }),
    };
}
