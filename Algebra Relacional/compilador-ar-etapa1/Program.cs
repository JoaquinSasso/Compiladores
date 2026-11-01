using System;
using System.IO;
using System.Text;

// Etapa 1: el compilador solo dice si la entrada es válida.
// Uso: dotnet run -- archivo.ra
class Program {
    static int Main(string[] args) {
        if (args.Length != 1) {
            Console.WriteLine("Uso: AR <archivo>");
            return 2;
        }

        // El scanner de Coco/R lee UTF-8 solo si la entrada empieza con BOM.
        // Leemos el archivo como texto y lo volvemos a armar con BOM, así
        // funciona con símbolos (σ, π, ⋈) se haya guardado como se haya guardado.
        string texto = File.ReadAllText(args[0], Encoding.UTF8);
        var bytes = new UTF8Encoding(true).GetPreamble();
        var stream = new MemoryStream();
        stream.Write(bytes, 0, bytes.Length);
        var contenido = Encoding.UTF8.GetBytes(texto);
        stream.Write(contenido, 0, contenido.Length);
        stream.Position = 0;

        Scanner scanner = new Scanner(stream);
        Parser parser = new Parser(scanner);
        parser.Parse();

        if (parser.errors.count == 0) {
            Console.WriteLine("OK: la entrada es válida");
            return 0;
        }
        Console.WriteLine(parser.errors.count + " error(es)");
        return 1;
    }
}
