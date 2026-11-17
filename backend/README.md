# Backend de Telemetría — Ingesta de `SessionReport`

Backend en **Node.js + Express** para recibir, validar y persistir los reportes de partida (`SessionReport`) generados por el juego Unity, habilitando su análisis posterior en PostgreSQL / Supabase.

## 1. Objetivo

El backend centraliza la ingesta de telemetría sin acoplar la ejecución del juego a la red:

```text
Unity
  → SessionReport JSON
  → Node/Express
  → Supabase RPC
  → PostgreSQL
  → Analytics Views
  → BI Views
```

> [!NOTE]
> El gameplay funciona completamente **offline**. El backend **no es requerido para jugar**.

## 2. Stack

- **Node.js 20** (`>= 20.0.0`, probado en `v20.19.0`)
- **Express** (`^4.21.2`)
- **`@supabase/supabase-js`** (`^2.109.0`)
- **PostgreSQL / Supabase**
- **Node test runner** (`node:test` y `node:assert`)

## 3. Endpoints

Por defecto escucha en `http://localhost:3000` (configurable con `PORT`).

### `GET /api/health`

Verifica que el servicio esté activo.

- **`200 OK`**:
  ```json
  { "status": "ok" }
  ```

### `POST /api/v1/sessions`

Recibe un `SessionReport` en JSON, valida su estructura y lo persiste de forma idempotente usando `metadata.runId`.

- **Límite de tamaño:** `2 MB`
- **Idempotencia:** por `metadata.runId`

Respuestas:

| Estado | Código interno | Descripción |
| --- | --- | --- |
| `201 Created` | `created` | Primera ingesta de la sesión (`{ "status": "created", "runId": "..." }`) |
| `200 OK` | `already_exists` | Sesión duplicada ya registrada (`{ "status": "already_exists", "runId": "..." }`) |
| `400 Bad Request` | `invalid_payload` | Payload inválido (`{ "error": "invalid_payload", "details": [...] }`) |
| `400 Bad Request` | `invalid_json` | JSON malformado (`{ "error": "invalid_json", "details": [...] }`) |
| `413 Payload Too Large` | `payload_too_large` | El cuerpo supera los 2 MB (`{ "error": "payload_too_large" }`) |
| `500 Internal Server Error` | `internal_server_error` | Error inesperado (`{ "error": "internal_server_error" }`) |

## 4. Modos de almacenamiento

### Modo Local (por defecto)

Si no están definidas las variables de entorno de Supabase:

- Usa `localSessionStore`.
- Guarda cada reporte en `backend/data/sessions/session_<runId>.json`.

### Modo Supabase

Si están definidas ambas variables:

- `SUPABASE_URL`
- `SUPABASE_SECRET_KEY`

el backend usa `supabaseSessionStore`.

> [!WARNING]
> `SUPABASE_SECRET_KEY` es una credencial **exclusivamente server-side**:
> - Nunca debe ir en Unity.
> - Nunca debe publicarse en Git.
> - `backend/.env` está ignorado por Git (usar `backend/.env.example` como plantilla).

### Configuración inválida

Si solo se define una de las dos variables (`SUPABASE_URL` o `SUPABASE_SECRET_KEY`), el backend **falla al iniciar** (`process.exit(1)`) para evitar operar con una configuración parcial de forma silenciosa.

## 5. Comandos

Ejecutar desde `backend/`:

```bash
# Instalar dependencias
npm install

# Ejecutar suite de tests
npm test

# Iniciar servidor (modo normal)
npm start

# Iniciar servidor en modo desarrollo (--watch)
npm run dev

# Iniciar servidor cargando .env con Node 20 (--env-file=.env --watch)
npm run dev:supabase
```

## 6. Formato `SessionReport`

Cada reporte contiene tres bloques principales:

- `metadata`
- `summary`
- `events`

Detalles de versión y endings soportados:

- `reportVersion = "1.0"`
- Probado con `schemaVersion = 13` y `schemaVersion = 19`
- `gameVersion = "0.4.0"` en las pruebas de schema 19
- Endings soportados:
  - `brake`
  - `route_complete`
  - `overload`

## 7. Base de datos PostgreSQL

- **`public.sessions`**: una fila por partida.
- **`public.session_events`**: múltiples eventos asociados a una sesión (`sessions 1 → N session_events`).
- **`public.ingest_session_report(jsonb)`**: RPC transaccional e idempotente encargada de insertar la sesión y todos sus eventos en una única transacción.

## 8. Migraciones SQL

Las migraciones ubicadas en `backend/sql/` **deben aplicarse en orden** y **no deben reescribirse** una vez aplicadas:

1. **`001_session_storage.sql`**
   - Esquema inicial, tablas (`sessions`, `session_events`), índices, RPC `ingest_session_report` y permisos base.
2. **`002_schema_v19_columns.sql`**
   - Agrega columnas de CASI / pink gems / pink dodge, mantiene compatibilidad entre schema 13 y schema 19, y actualiza la RPC.
3. **`003_analytics_views.sql`**
   - Crea vistas analíticas base:
     - `vw_sessions_summary`
     - `vw_speed_decisions`
     - `vw_behavior_after_loss`
     - `vw_daily_metrics`
4. **`004_bi_views.sql`**
   - Crea vistas simplificadas para BI / dashboard:
     - `vw_bi_session_overview`
     - `vw_bi_behavior_metrics`
     - `vw_bi_speed_decisions`

## 9. Seguridad

- RPC `ingest_session_report(jsonb)` declarada con `SECURITY INVOKER`.
- `anon` y `authenticated` sin permiso `EXECUTE` sobre la RPC.
- `service_role` utilizado exclusivamente por el backend.
- Vistas analíticas y de BI configuradas con `security_invoker = true`.
- Vistas sin permiso `SELECT` para `PUBLIC`, `anon` ni `authenticated`.
- Para conectar un futuro dashboard externo (como Looker Studio), se recomienda crear un **rol PostgreSQL dedicado de solo lectura** (`read-only`). **No usar `service_role` directamente desde Looker Studio.**

## 10. Tests y validaciones realizadas

- Tests automatizados: `npm test` → **9/9 passing**.
- Validación manual en Supabase DEV (`lotba-dev`):
  - Ingesta real con `schemaVersion = 13`
  - Ingesta real con `schemaVersion = 19`
  - Idempotencia por `metadata.runId`
  - Rollback transaccional ante fallo de eventos
  - Ending `overload`
  - Campos CASI / pink gems / pink dodge
  - Analytics views (`003`)
  - BI views (`004`)
  - Compatibilidad histórica (valores `NULL` preservados en schema 13)

## 11. Estado del entorno

- Proyecto Supabase utilizado hasta ahora: **DEV (`lotba-dev`)**.
- Todavía **no existe deploy de producción** de este backend.
- No publicar Secret Keys ni credenciales.
- No almacenar ni enviar datos personales de alumnos.

## 12. Limitaciones actuales

- `recoveryAttempts` todavía no representa una mecánica completa de recovery.
- El tiempo real de reacción durante una oferta de velocidad no se puede calcular porque el tiempo del juego se pausa mientras la oferta está abierta.
- El near-miss de obstáculos comunes todavía no existe.
- La identificación de localización/colegio todavía no está implementada.
