using System;
using System.IO;
using System.Text;

// Etapa 3: el compilador valida la entrada, hace el análisis semántico e
// imprime el esquema de cada sentencia.
// Uso: dotnet run -- [--ast] archivo.ra
//   --ast  solo análisis sintáctico: imprime el AST y no hace el semántico
class Program {
    static int Main(string[] args) {
        bool soloAst = args.Length == 2 && args[0] == "--ast";
        if (args.Length != 1 && !soloAst) {
            Console.WriteLine("Uso: AR [--ast] <archivo>");
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
        if (soloAst) {
            Console.Write(ImpresorAST.Imprimir(parser.programa));
            return 0;
        }

        // Etapa 3: análisis semántico sobre el AST.
        var semantico = new Semantico();
        semantico.Analizar(parser.programa);
        if (semantico.Errores.Count > 0) {
            foreach (var e in semantico.Errores) Console.WriteLine(e);
            Console.WriteLine(semantico.Errores.Count + " error(es) semántico(s)");
            return 1;
        }

        // Sin errores: el esquema de salida de cada sentencia.
        foreach (var ins in parser.programa.Instrucciones) {
            string titulo = ins is Asignacion a ? a.Nombre : "Consulta (línea " + ins.Linea + ")";
            Console.WriteLine(titulo + " " + ins.Expr.Esquema);
        }
        return 0;
    }
}
