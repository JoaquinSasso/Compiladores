using ARWeb.Servicios;
using Microsoft.JSInterop;

namespace ARWeb.Interop;

// Acceso a las funciones de wwwroot/js/app.js (SQLite, editor, guardado,
// archivos, portapapeles). El módulo se importa una sola vez, la primera
// vez que se usa.
public class Js : IAsyncDisposable {
    readonly IJSRuntime runtime;
    Task<IJSObjectReference> modulo;

    public Js(IJSRuntime runtime) { this.runtime = runtime; }

    public Task<IJSObjectReference> Modulo() =>
        modulo ??= runtime.InvokeAsync<IJSObjectReference>("import", "./js/app.js").AsTask();

    async ValueTask Llamar(string funcion, params object[] args) => await (await Modulo()).InvokeVoidAsync(funcion, args);
    async ValueTask<T> Pedir<T>(string funcion, params object[] args) => await (await Modulo()).InvokeAsync<T>(funcion, args);

    // SQLite (sql.js)
    public ValueTask<RespuestaSql> EjecutarSql(string script) => Pedir<RespuestaSql>("ejecutarSql", script);

    // localStorage
    public ValueTask<string> Leer(string clave) => Pedir<string>("leer", clave);
    public ValueTask Guardar(string clave, string valor) => Llamar("guardar", clave, valor);

    // Archivos y portapapeles
    public ValueTask Descargar(string nombre, string contenido, string tipo) => Llamar("descargar", nombre, contenido, tipo);
    public ValueTask<bool> Copiar(string texto) => Pedir<bool>("copiar", texto);
    public ValueTask<bool> Confirmar(string mensaje) => runtime.InvokeAsync<bool>("confirm", mensaje);
    public ValueTask<string> Preguntar(string mensaje, string valor) => runtime.InvokeAsync<string>("prompt", mensaje, valor);

    // Enlaces para compartir: el contenido va comprimido en el #hash de la URL.
    public ValueTask<string> EnlaceCompartido(string json) => Pedir<string>("enlaceCompartido", json);
    public ValueTask<string> LeerCompartido() => Pedir<string>("leerCompartido");
    public ValueTask<string> Compartir(string url, string titulo) => Pedir<string>("compartir", url, titulo);

    public async ValueTask DisposeAsync() {
        if (modulo != null && modulo.IsCompletedSuccessfully) await modulo.Result.DisposeAsync();
    }
}
