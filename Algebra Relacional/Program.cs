using System;
using System.IO;
using System.Text;

// La línea de comandos del compilador. Todo el trabajo lo hace Compilador.Compilar
// (Compilador.cs): scanner → parser (AST) → semántico → generador de SQL.
// Uso: dotnet run -- [--ast | --esquemas] archivo.ra
//   (sin opción)  imprime el script SQL
//   --ast         solo análisis sintáctico: imprime el AST
//   --esquemas    hasta el semántico: imprime el esquema de cada sentencia
class Program {
    static int Main(string[] args) {
        // La salida siempre en UTF-8 (sin BOM). En Ubuntu ya lo es, pero en Windows
        // la consola usa otra página de códigos y se pierden σ, π, ⋈ y los acentos.
        Console.OutputEncoding = new UTF8Encoding(false);
        string modo = args.Length == 2 ? args[0] : "--sql";
        if (args.Length < 1 || args.Length > 2 || (modo != "--sql" && modo != "--ast" && modo != "--esquemas")) {
            Console.WriteLine("Uso: AR [--ast | --esquemas] <archivo>");
            return 2;
        }

        var r = Compilador.Compilar(File.ReadAllText(args[args.Length - 1], Encoding.UTF8));

        if (r.HayErroresDeSintaxis) {
            foreach (var e in r.Errores) Console.WriteLine(e);
            Console.WriteLine(r.Errores.Count + " error(es) de sintaxis");
            return 1;
        }
        if (modo == "--ast") {
            Console.Write(r.Ast);
            return 0;
        }
        if (r.Errores.Count > 0) {
            foreach (var e in r.Errores) Console.WriteLine(e);
            Console.WriteLine(r.Errores.Count + " error(es) semántico(s)");
            return 1;
        }
        if (modo == "--esquemas") {
            foreach (var ins in r.Programa.Instrucciones)
                Console.WriteLine(Compilador.Titulo(ins) + " " + ins.Expr.Esquema);
            return 0;
        }
        Console.Write(r.Sql);
        return 0;
    }
}
