// Funciones de JavaScript que usa la app (Pages/Home.razor) a través de JSInterop.

// ---------------------------------------------------------------- SQLite ----
// SQLite compilado a WebAssembly (sql.js, en lib/sqljs). Se carga una sola vez.
let sqlite = null;

async function cargarSqlite() {
    if (!sqlite) sqlite = await initSqlJs({ locateFile: archivo => "lib/sqljs/" + archivo });
    return sqlite;
}

function texto(v) {
    if (v === null || v === undefined) return null;          // NULL de SQL
    if (typeof v === "number" && !Number.isInteger(v))
        return String(Number(v.toFixed(4)));                  // 5.666666… → 5.6667
    return String(v);
}

// Ejecuta el script que generó el compilador en una base nueva, en memoria.
// Devuelve el resultado de cada sentencia que produce filas (las consultas),
// en orden: una por cada consulta del programa, aunque no tenga filas.
export async function ejecutarSql(script) {
    const SQL = await cargarSqlite();
    const base = new SQL.Database();
    const resultados = [];
    try {
        for (const sentencia of base.iterateStatements(script)) {
            const columnas = sentencia.getColumnNames();
            const filas = [];
            while (sentencia.step()) filas.push(sentencia.get().map(texto));
            if (columnas.length > 0) resultados.push({ columnas, filas });
        }
        return { ok: true, resultados };
    } catch (e) {
        return { ok: false, error: String(e && e.message ? e.message : e) };
    } finally {
        base.close();
    }
}

// ---------------------------------------------------------------- Editor ----
// Inserta texto donde está el cursor (o reemplaza lo seleccionado) y avisa a
// Blazor con un evento "input", para que se actualice la variable enlazada.
// No le da el foco al editor: en el teléfono no se abre el teclado en cada
// símbolo. Si el editor todavía no se tocó, el texto va al final.
//
// Si el editor tiene el foco (el teclado del teléfono está abierto), el
// navegador del teléfono desplaza la página para mostrar el cursor y deja los
// botones de los símbolos fuera de la pantalla. Por eso se guarda dónde estaba
// la página y, durante un instante, se deshace cualquier desplazamiento.
export function insertar(area, txt) {
    const x = window.scrollX, y = window.scrollY;
    const quedarse = () => {
        if (window.scrollX !== x || window.scrollY !== y) window.scrollTo(x, y);
    };
    // El evento "scroll" lo deshace antes de que se vea; los temporizadores,
    // por si el navegador desplaza la página sin avisar.
    window.addEventListener("scroll", quedarse);
    for (const ms of [0, 50, 150, 300]) setTimeout(quedarse, ms);
    setTimeout(() => window.removeEventListener("scroll", quedarse), 400);

    if (!area.dataset.usado) {
        area.selectionStart = area.selectionEnd = area.value.length;
        area.dataset.usado = "si";
    }
    area.setRangeText(txt, area.selectionStart, area.selectionEnd, "end");
    area.dispatchEvent(new Event("input", { bubbles: true }));
    quedarse();
}

// Con el teclado del teléfono abierto, Chrome desplaza la página en cada
// cambio hasta que el editor entero quede a la vista, y los botones de los
// símbolos se van de la pantalla. Para que no tenga que desplazarla, el
// editor se achica hasta entrar en el espacio que deja el teclado (con un
// mínimo de dos líneas; el texto se desplaza adentro). Al cerrar el teclado
// vuelve a su alto.
function ajustarAlto(area) {
    const vista = window.visualViewport;
    if (area.dataset.achicado) {
        area.style.height = area.style.minHeight = "";
        delete area.dataset.achicado;
    }
    if (!vista || document.activeElement !== area) return;
    const libre = vista.offsetTop + vista.height - area.getBoundingClientRect().top - 8;
    if (libre < area.offsetHeight) {
        area.style.height = area.style.minHeight = Math.max(libre, 72) + "px";
        area.dataset.achicado = "si";
    }
}

// Prepara el editor de la consulta: recuerda si ya se tocó (para insertar
// donde está el cursor), lo ajusta al teclado del teléfono, y Ctrl+Enter (o
// Cmd+Enter) aprieta el botón Ejecutar.
export function prepararEditor(area, boton) {
    area.addEventListener("focus", () => { area.dataset.usado = "si"; ajustarAlto(area); });
    area.addEventListener("blur", () => ajustarAlto(area));
    if (window.visualViewport)
        window.visualViewport.addEventListener("resize", () => ajustarAlto(area));
    area.addEventListener("keydown", e => {
        if (e.key === "Enter" && (e.ctrlKey || e.metaKey)) {
            e.preventDefault();
            boton.click();
        }
    });
}

// ------------------------------------------------------------ Guardado ----
// Lo que el alumno escribió queda guardado en este navegador.
export function leer(clave) {
    try { return localStorage.getItem(clave); } catch { return null; }
}

export function guardar(clave, valor) {
    try { localStorage.setItem(clave, valor); } catch { /* sin almacenamiento: no pasa nada */ }
}
