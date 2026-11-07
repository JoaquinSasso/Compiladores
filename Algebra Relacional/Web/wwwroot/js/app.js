// Funciones de JavaScript que usa la app a través de JSInterop
// (Interop/Js.cs y Componentes/EditorCodigo.razor).

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

// ------------------------------------------------------- Resaltado ----
// Un analizador léxico chico, solo para pintar el texto: reconoce los mismos
// tokens que el scanner del compilador (AR.atg), con una clase por categoría.
const UNARIOS = new Set(["π", "σ", "ρ", "γ", "pi", "sigma", "rho", "gamma"]);
const BINARIOS = new Set(["∪", "∩", "×", "⨯", "⋈", "⟕", "⟖", "⟗", "÷", "/", "*", "-", "union", "intersect",
    "except", "x", "cross", "join", "natural", "inner", "left", "right", "full", "outer"]);
const LOGICOS = new Set(["∧", "∨", "¬", "and", "or", "not"]);
const AGREGADOS = new Set(["count", "sum", "avg", "min", "max"]);
const CONSTANTES = new Set(["null", "true", "false"]);

const PATRON = /(--[^\n]*)|(\/\*[\s\S]*?(?:\*\/|$))|('[^'\n]*'?)|(group:[^\n]*)|(->|<-|→|←)|(<=|>=|<>|!=|[=<>≠≤≥])|(\d+(?:\.\d+)?)|([A-Za-z_]\w*)|(\s+)|([\s\S])/gi;

function tokens(txt) {
    const lista = [];
    PATRON.lastIndex = 0;
    let m;
    while ((m = PATRON.exec(txt)) !== null) {
        const v = m[0];
        let clase;
        if (m[1] || m[2]) clase = "com";
        else if (m[3]) clase = v.length > 1 && v.endsWith("'") ? "cad" : "cad abierta";
        else if (m[4]) clase = "grp";
        else if (m[5]) clase = "fle";
        else if (m[6]) clase = "cmp";
        else if (m[7]) clase = "num";
        else if (m[8]) {
            const p = v.toLowerCase();
            clase = UNARIOS.has(p) ? "una" : BINARIOS.has(p) ? "bin" : LOGICOS.has(p) ? "log"
                  : AGREGADOS.has(p) ? "agg" : CONSTANTES.has(p) ? "cte" : "id";
        }
        else if (m[9]) clase = "";
        else clase = UNARIOS.has(v) ? "una" : BINARIOS.has(v) ? "bin" : LOGICOS.has(v) ? "log"
                   : "(){}".includes(v) ? "par" : "pun";
        lista.push({ v, desde: m.index, hasta: m.index + v.length, clase });
    }
    return lista;
}

const escapar = s => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");

// ¿La posición cae dentro de un comentario o una cadena?
function enComentarioOCadena(txt, pos) {
    for (const t of tokens(txt.slice(0, pos)))
        if (t.hasta >= pos && (t.clase === "com" || t.clase.startsWith("cad"))) return true;
    return false;
}

// Los paréntesis que se corresponden (y los que no tienen pareja).
function parejas(lista) {
    const pila = [], pareja = new Map(), sueltos = new Set();
    lista.forEach((t, i) => {
        if (t.clase !== "par" || (t.v !== "(" && t.v !== ")")) return;
        if (t.v === "(") pila.push(i);
        else if (pila.length) { const j = pila.pop(); pareja.set(i, j); pareja.set(j, i); }
        else sueltos.add(i);
    });
    pila.forEach(i => sueltos.add(i));
    return { pareja, sueltos };
}

const LIMITE_RESALTADO = 60000;     // con textos más largos no se pinta (sería lento)

function pintar(area) {
    const capa = area._capa;
    if (!capa) return;
    const txt = area.value;
    const largo = txt.length > LIMITE_RESALTADO;
    area.classList.toggle("sin-resaltado", largo);
    if (largo) { capa.textContent = ""; return; }

    const lista = tokens(txt);
    const { pareja, sueltos } = parejas(lista);
    // Paréntesis junto al cursor y su pareja
    const resaltar = new Set();
    if (document.activeElement === area && area.selectionStart === area.selectionEnd) {
        const c = area.selectionStart;
        const i = lista.findIndex((t, k) => t.clase === "par" && (t.hasta === c || t.desde === c) && pareja.has(k));
        if (i >= 0) { resaltar.add(i); resaltar.add(pareja.get(i)); }
    }
    // Los errores: se marca el token donde empieza cada uno
    const marcas = (area._marcas || []).map(m => posicion(txt, m.linea, m.col));
    const conError = new Set();
    let errorAlFinal = false;
    for (const p of marcas) {
        let i = lista.findIndex(t => t.desde <= p && p < t.hasta && t.clase !== "");
        if (i < 0) i = lista.findIndex(t => t.desde >= p && t.clase !== "");
        if (i >= 0) conError.add(i); else errorAlFinal = true;
    }

    let html = "";
    lista.forEach((t, i) => {
        let clase = t.clase;
        if (sueltos.has(i)) clase += " suelto";
        if (resaltar.has(i)) clase += " pareja";
        if (conError.has(i)) clase += " err";
        html += clase ? `<span class="${clase.trim()}">${escapar(t.v)}</span>` : escapar(t.v);
    });
    if (errorAlFinal) html += `<span class="err fin"> </span>`;
    capa.innerHTML = html + "\n";       // el \n final: la última línea vacía también ocupa lugar
    capa.scrollTop = area.scrollTop;
    capa.scrollLeft = area.scrollLeft;
}

// Línea y columna (desde 1) → posición en el texto.
function posicion(txt, linea, col) {
    let p = 0;
    for (let l = 1; l < linea; l++) {
        const nl = txt.indexOf("\n", p);
        if (nl < 0) return txt.length;
        p = nl + 1;
    }
    return Math.min(txt.length, p + Math.max(0, col - 1));
}

// --------------------------------------------- Palabras → símbolos ----
// Al escribir "pi " queda "π ", "<=" queda "≤", "left join " queda "⟕ ", etc.
const PALABRAS = {
    "pi": "π", "sigma": "σ", "rho": "ρ", "gamma": "γ", "union": "∪", "intersect": "∩", "x": "×",
    "join": "⋈", "natural join": "⋈", "inner join": "⋈", "cross join": "×",
    "left join": "⟕", "left outer join": "⟕", "right join": "⟖", "right outer join": "⟖",
    "full join": "⟗", "full outer join": "⟗", "and": "∧", "or": "∨", "not": "¬",
};
const PARES = { "<=": "≤", ">=": "≥", "!=": "≠", "<>": "≠", "->": "→", "<-": "←" };
const ANTES_DE_PALABRA = /(^|[^\w.'])((?:(?:left|right|full)(?:\s+outer)?|natural|inner|cross)\s+join|[A-Za-z]+)$/i;

function reemplazar(area, desde, hasta, nuevo, cursor) {
    area.setSelectionRange(desde, hasta);
    // execCommand conserva el deshacer (Ctrl+Z); si no está, setRangeText.
    if (!document.execCommand || !document.execCommand("insertText", false, nuevo)) {
        area.setRangeText(nuevo, desde, hasta, "end");
        area.dispatchEvent(new Event("input", { bubbles: true }));
    }
    area.setSelectionRange(cursor, cursor);
}

function convertir(area, e) {
    if (!area._opciones.convertir || e.inputType !== "insertText" || !e.data) return;
    const c = area.selectionStart;
    if (c !== area.selectionEnd) return;
    const txt = area.value;
    // Dos caracteres: <= >= != <> -> <-
    const par = txt.slice(c - 2, c);
    if (e.data.length === 1 && PARES[par] && !enComentarioOCadena(txt, c - 2)) {
        if (par === "<-" && txt[c - 3] === "-") return;     // "<--" : es un comentario que empieza
        reemplazar(area, c - 2, c, PARES[par], c - 1);
        return;
    }
    // Una palabra seguida de un espacio o de un paréntesis
    if (e.data !== " " && e.data !== "(") return;
    const m = ANTES_DE_PALABRA.exec(txt.slice(0, c - 1));
    if (!m) return;
    const palabra = m[2].toLowerCase().replace(/\s+/g, " ");
    const simbolo = PALABRAS[palabra];
    if (!simbolo) return;
    const desde = c - 1 - m[2].length;
    if (enComentarioOCadena(txt, desde)) return;
    reemplazar(area, desde, c - 1, simbolo, desde + simbolo.length + 1);
}

// ---------------------------------------------------- Autocompletar ----
// La palabra que se está escribiendo (lo que hay antes del cursor), para que
// Blazor calcule las sugerencias. Solo se avisa cuando cambia.
function palabraActual(area) {
    if (document.activeElement !== area || area.selectionStart !== area.selectionEnd) return "";
    const c = area.selectionStart, txt = area.value;
    if (/\w/.test(txt[c] || "")) return "";                 // en el medio de una palabra
    const m = /[A-Za-z_][\w]*(?:\.[\w]*)?$/.exec(txt.slice(0, c));
    if (!m || enComentarioOCadena(txt, m.index)) return "";
    if (m.index > 0 && /[\w.]/.test(txt[m.index - 1])) return "";
    return m[0];
}

function avisarPalabra(area) {
    const p = palabraActual(area);
    if (p === area._palabra) return;
    area._palabra = p;
    area._dotnet.invokeMethodAsync("Palabra", p);
}

export function sugerir(area, palabra) { area._sugerencia = palabra || null; }

// Reemplaza la palabra que se está escribiendo por la sugerencia elegida.
export function completar(area, palabra) {
    const c = area.selectionStart;
    const prefijo = area._palabra || "";
    if (!area.dataset.usado) return;
    reemplazar(area, c - prefijo.length, c, palabra, c - prefijo.length + palabra.length);
    area._palabra = null;
    avisarPalabra(area);
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

// Lleva el cursor a una línea y columna (al tocar un error).
export function irA(area, linea, col) {
    const p = posicion(area.value, linea, col);
    area.dataset.usado = "si";
    area.focus({ preventScroll: true });
    area.setSelectionRange(p, p);
    // Desplaza el editor por dentro hasta la línea, y la página hasta el editor.
    const alto = parseFloat(getComputedStyle(area).lineHeight) || 24;
    area.scrollTop = Math.max(0, (linea - 2) * alto);
    area.scrollIntoView({ block: "center", behavior: "smooth" });
    pintar(area);
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
    if (!area._opciones.ajustarAlto || !vista || document.activeElement !== area) return;
    const libre = vista.offsetTop + vista.height - area.getBoundingClientRect().top - 8;
    if (libre < area.offsetHeight) {
        area.style.height = area.style.minHeight = Math.max(libre, 72) + "px";
        area.dataset.achicado = "si";
    }
}

// Prepara un editor: el resaltado (la capa de atrás), la conversión de
// palabras, el autocompletado, el ajuste al teclado del teléfono, y
// Ctrl+Enter (o Cmd+Enter) para ejecutar.
export function prepararEditor(area, capa, dotnet, opciones) {
    area._capa = capa;
    area._dotnet = dotnet;
    area._opciones = opciones || {};
    area._palabra = "";
    const alCambiarCursor = () => { pintar(area); if (area._opciones.sugerencias) avisarPalabra(area); };

    area.addEventListener("input", e => { convertir(area, e); alCambiarCursor(); });
    area.addEventListener("scroll", () => { capa.scrollTop = area.scrollTop; capa.scrollLeft = area.scrollLeft; });
    area.addEventListener("keyup", alCambiarCursor);
    area.addEventListener("click", alCambiarCursor);
    area.addEventListener("focus", () => { area.dataset.usado = "si"; ajustarAlto(area); alCambiarCursor(); });
    area.addEventListener("blur", () => { ajustarAlto(area); pintar(area); if (area._opciones.sugerencias) avisarPalabra(area); });
    if (window.visualViewport)
        window.visualViewport.addEventListener("resize", () => ajustarAlto(area));
    area.addEventListener("keydown", e => {
        if (e.key === "Enter" && (e.ctrlKey || e.metaKey)) {
            e.preventDefault();
            dotnet.invokeMethodAsync("Ejecutar");
        } else if (e.key === "Tab" && !e.shiftKey && area._sugerencia && area._palabra) {
            e.preventDefault();
            completar(area, area._sugerencia);
        }
    });
    // El alto del editor cambia (al estirarlo): la capa lo sigue sola (CSS),
    // pero el texto puede cambiar de líneas.
    if (window.ResizeObserver) new ResizeObserver(() => pintar(area)).observe(area);
    pintar(area);
}

export function opcionesEditor(area, opciones) { area._opciones = { ...area._opciones, ...opciones }; }

// Blazor cambió el texto (por ejemplo, al cambiar de pestaña): hay que repintar.
export function refrescar(area) { pintar(area); }

export function marcar(area, marcas) { area._marcas = marcas; pintar(area); }

// ------------------------------------------------------------ Guardado ----
// Lo que el alumno escribió queda guardado en este navegador.
export function leer(clave) {
    try { return localStorage.getItem(clave); } catch { return null; }
}

export function guardar(clave, valor) {
    try { localStorage.setItem(clave, valor); } catch { /* sin almacenamiento: no pasa nada */ }
}

// ----------------------------------------------- Archivos y portapapeles ----
export function descargar(nombre, contenido, tipo) {
    const url = URL.createObjectURL(new Blob([contenido], { type: tipo }));
    const a = document.createElement("a");
    a.href = url;
    a.download = nombre;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}

export async function copiar(txt) {
    try { await navigator.clipboard.writeText(txt); return true; } catch { return false; }
}

// -------------------------------------------------------------- Compartir ----
// Lo compartido viaja comprimido (deflate) en el #hash de la URL, así que no
// pasa por ningún servidor: el hash no se envía al pedir la página.
function aBase64Url(bytes) {
    let s = "";
    for (const b of bytes) s += String.fromCharCode(b);
    return btoa(s).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function deBase64Url(s) {
    s = s.replace(/-/g, "+").replace(/_/g, "/");
    const bin = atob(s + "===".slice((s.length + 3) % 4));
    return Uint8Array.from(bin, c => c.charCodeAt(0));
}

async function transformar(bytes, flujo) {
    return new Uint8Array(await new Response(new Blob([bytes]).stream().pipeThrough(flujo)).arrayBuffer());
}

export async function enlaceCompartido(json) {
    const datos = await transformar(new TextEncoder().encode(json), new CompressionStream("deflate-raw"));
    const url = new URL(location.href);
    url.hash = "c=" + aBase64Url(datos);
    return url.href;
}

// Si la página se abrió con un enlace compartido, devuelve su contenido (y
// saca el hash de la barra de direcciones, para no volver a abrirlo).
export async function leerCompartido() {
    const m = /^#c=([\w-]+)$/.exec(location.hash);
    if (!m) return null;
    history.replaceState(null, "", location.pathname + location.search);
    try {
        const datos = await transformar(deBase64Url(m[1]), new DecompressionStream("deflate-raw"));
        return new TextDecoder().decode(datos);
    } catch {
        return null;
    }
}

// En el teléfono abre el menú de compartir; si no, copia el enlace.
export async function compartir(url, titulo) {
    if (navigator.share && matchMedia("(pointer: coarse)").matches) {
        try { await navigator.share({ url, title: titulo }); return "compartido"; }
        catch (e) { if (e && e.name === "AbortError") return "cancelado"; }
    }
    return (await copiar(url)) ? "copiado" : "error";
}
