using System;
using System.IO;
using System.Text;

// El compilador completo: scanner → parser (AST) → semántico → generador de SQL.
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
        string archivo = args[args.Length - 1];

        // El scanner de Coco/R lee UTF-8 solo si la entrada empieza con BOM.
        // Leemos el archivo como texto y lo volvemos a armar con BOM, así
        // funciona con símbolos (σ, π, ⋈) se haya guardado como se haya guardado.
        string texto = File.ReadAllText(archivo, Encoding.UTF8);
        var bytes = new UTF8Encoding(true).GetPreamble();
        var stream = new MemoryStream();
        stream.Write(bytes, 0, bytes.Length);
        var contenido = Encoding.UTF8.GetBytes(texto);
        stream.Write(contenido, 0, contenido.Length);
        stream.Position = 0;

        Scanner scanner = new Scanner(stream);
        Parser parser = new Parser(scanner);
        parser.errors.errMsgFormat = "-- línea {0} col {1}: {2}";
        parser.Parse();

        if (parser.errors.count > 0) {
            Console.WriteLine(parser.errors.count + " error(es) de sintaxis");
            return 1;
        }
        if (modo == "--ast") {
            Console.Write(ImpresorAST.Imprimir(parser.programa));
            return 0;
        }

        // Análisis semántico sobre el AST.
        var semantico = new Semantico();
        semantico.Analizar(parser.programa);
        if (semantico.Errores.Count > 0) {
            foreach (var e in semantico.Errores) Console.WriteLine(e);
            Console.WriteLine(semantico.Errores.Count + " error(es) semántico(s)");
            return 1;
        }

        if (modo == "--esquemas") {
            foreach (var ins in parser.programa.Instrucciones) {
                string titulo = ins is Asignacion a ? a.Nombre : "Consulta (línea " + ins.Linea + ")";
                Console.WriteLine(titulo + " " + ins.Expr.Esquema);
            }
            return 0;
        }

        // Generación de SQL a partir del AST validado.
        Console.Write(new Generador().Generar(parser.programa));
        return 0;
    }
}
