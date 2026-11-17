# Backend MVP Local — Ingesta de `SessionReport v1`

Backend local mínimo en **Node.js + Express** para recibir y validar reportes de partida (`SessionReport v1`) generados por el juego Unity.

> [!NOTE]
> La carpeta `backend/data/` es **almacenamiento LOCAL temporal** para desarrollo y pruebas en esta etapa. Será reemplazada posteriormente por persistencia remota (Supabase / PostgreSQL).

## Requisitos

- Node.js `>= 20` (probado en `v20.19.0`)

## Instalación

```bash
cd backend
npm install
```

## Ejecución local

### 1. Modo local con archivos (por defecto)

Si no se configuran variables de Supabase, las sesiones se guardan en `backend/data/sessions/`:

```bash
# Modo normal
npm start

# Modo desarrollo con watch nativo de Node
npm run dev
```

### 2. Modo Supabase DEV

1. Copiar `backend/.env.example` a `backend/.env` (archivo local ignorado por Git).
2. Completar `SUPABASE_URL` y `SUPABASE_SECRET_KEY` (`sb_secret_...`) del proyecto DEV (`lotba-dev`).
3. Ejecutar:

```bash
npm run dev:supabase
```

> [!WARNING]
> `SUPABASE_SECRET_KEY` (formato habitual `sb_secret_...`) es una credencial privilegiada **exclusivamente server-side**. **Nunca** debe enviarse a Unity, subirse a Git ni imprimirse en logs.

Por defecto escucha en `http://localhost:3000` (configurable mediante `PORT`).

## Tests

```bash
npm test
```

Usa exclusivamente `node:test` y `node:assert` con un directorio temporal aislado.

## Endpoints

### `GET /api/health`

Verifica que el servidor esté activo.

Respuesta (`200 OK`):

```json
{
  "status": "ok"
}
```

### `POST /api/v1/sessions`

Recibe un `SessionReport v1` en JSON, valida los campos mínimos (`metadata`, `metadata.runId`, `metadata.reportVersion`, `summary`, `events`) y lo persiste de forma idempotente en `backend/data/sessions/session_<runId>.json` usando escritura exclusiva (`flag: 'wx'`).

Respuestas:

- **Sesión nueva (`201 Created`):**
  ```json
  {
    "status": "created",
    "runId": "2a246923b8cc43c5ab9c72a1ec5075ea"
  }
  ```
- **Sesión duplicada (`200 OK`):**
  ```json
  {
    "status": "already_exists",
    "runId": "2a246923b8cc43c5ab9c72a1ec5075ea"
  }
  ```
- **Payload o JSON inválido (`400 Bad Request`):**
  ```json
  {
    "error": "invalid_payload",
    "details": ["metadata.runId must be a non-empty string"]
  }
  ```
- **Error interno (`500 Internal Server Error`):**
  ```json
  {
    "error": "internal_server_error"
  }
  ```
