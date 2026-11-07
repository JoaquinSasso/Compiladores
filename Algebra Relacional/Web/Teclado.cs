namespace ARWeb;

// Los botones de símbolos de la calculadora. Cada tecla muestra un símbolo,
// inserta un texto en el editor y tiene una ayuda (con la palabra equivalente,
// para quien quiera escribirlo con el teclado).
public record Tecla(string Simbolo, string Inserta, string Ayuda);
public record GrupoTeclas(string Nombre, Tecla[] Teclas);

public static class Teclado {
    public static readonly GrupoTeclas[] Grupos = {
        new("Unarios", new Tecla[] {
            new("π", "π ", "Proyección (pi): π a, b (R)"),
            new("σ", "σ ", "Selección (sigma): σ a = 1 (R)"),
            new("ρ", "ρ ", "Renombre (rho): ρ S (R) o ρ nuevo←viejo (R)"),
            new("γ", "γ ", "Agrupamiento (gamma): γ a; count(*) → n (R)"),
        }),
        new("Binarios", new Tecla[] {
            new("∪", " ∪ ", "Unión (union)"),
            new("−", " - ", "Diferencia (- o except)"),
            new("∩", " ∩ ", "Intersección (intersect)"),
            new("×", " × ", "Producto cartesiano (x o cross join)"),
            new("⋈", " ⋈ ", "Join natural, o theta-join con condición (join)"),
            new("⟕", " ⟕ ", "Join externo izquierdo (left join)"),
            new("⟖", " ⟖ ", "Join externo derecho (right join)"),
            new("⟗", " ⟗ ", "Join externo completo (full join)"),
            new("÷", " ÷ ", "División (/)"),
        }),
        new("Condiciones", new Tecla[] {
            new("=", " = ", "Igual"),
            new("≠", " ≠ ", "Distinto (<> o !=)"),
            new("<", " < ", "Menor"),
            new(">", " > ", "Mayor"),
            new("≤", " ≤ ", "Menor o igual (<=)"),
            new("≥", " ≥ ", "Mayor o igual (>=)"),
            new("∧", " ∧ ", "Y (and)"),
            new("∨", " ∨ ", "O (or)"),
            new("¬", "¬ ", "No (not)"),
        }),
        new("Otros", new Tecla[] {
            new("→", " → ", "Alias en π y γ, o renombre viejo→nuevo (->)"),
            new("←", "←", "Renombre nuevo←viejo (<-)"),
            new("(", "(", "Abrir paréntesis"),
            new(")", ")", "Cerrar paréntesis"),
            new("'", "'", "Comilla (para las cadenas)"),
        }),
    };
}
