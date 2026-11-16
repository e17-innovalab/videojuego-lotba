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

```bash
# Modo normal
npm start

# Modo desarrollo con watch nativo de Node
npm run dev
```

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
