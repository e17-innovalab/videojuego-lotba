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

Cada reporte contiene tres bloques obligatorios y dos opcionales incorporados en `v0.5.0`:

- `metadata` (obligatorio)
- `summary` (obligatorio)
- `events` (obligatorio)
- `recoveryOpportunities` (opcional; ciclo completo por oportunidad de recupero en `v0.5.0`)
- `journey` (opcional; contexto operativo de visita escolar: `journeyId`, `schoolName`, `schoolShift`, `schoolDate`)

Detalles de versión y endings soportados:

- `reportVersion = "1.0"`
- Compatible con `schemaVersion = 13`, `19`, `20` temprano (`0.4.0` / `0.4.1`) y `20` definitivo (`0.5.0`)
- Objetos vacíos serializados por `JsonUtility` de Unity se normalizan a `NULL` en PostgreSQL, diferenciando `NULL` (no disponible históricamente) de `0` (medido en cero)
- Endings soportados:
  - `brake`
  - `route_complete`
  - `overload`

## 7. Base de datos PostgreSQL

- **`public.sessions`**: una fila por partida (metadatos, jornada escolar `journey_*` y resumen agregado).
- **`public.session_events`**: múltiples eventos asociados a una sesión (`sessions 1 → N session_events`).
- **`public.session_recovery_opportunities`**: oportunidades de recupero por sesión (`sessions 1 → N session_recovery_opportunities`).
- **`public.ingest_session_report(jsonb)`**: RPC transaccional e idempotente encargada de insertar la sesión, sus eventos y sus oportunidades de recupero en una única transacción.

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
5. **`005_schema_v20_columns.sql`**
   - Agrega columnas iniciales de `schemaVersion = 20` (`braking_*`, `recovery_boosts`, `brake_presses`, `brake_releases`, `brake_held_seconds` en `sessions`; `turbo_*` en `session_events`), actualiza `ingest_session_report` y extiende `vw_sessions_summary` y `vw_bi_session_overview`.
6. **`006_schema_v20_recovery_and_journey.sql`**
   - Completa el contrato `schemaVersion = 20` definitivo (`v0.5.0`): contexto de jornada escolar (`journey_*`), métricas de recupero y multiplicadores en `sessions`, campos nuevos por evento en `session_events`, tabla `session_recovery_opportunities`, normalización de objetos vacíos `JsonUtility`, diferenciación de decisiones en transición (`transition_decision_shown` y `recovery_entered` con `transition_choice`) y cuarta vista BI `vw_bi_recovery_opportunities`.
7. **`007_bi_readonly_role.sql`**
   - Encapsula las vistas bajo `lotba_bi_definer` (`NOLOGIN`, `security_invoker = false`) con RLS en las tres tablas base y configura `lotba_bi_readonly` (`LOGIN`, `CONNECTION LIMIT 10`, `default_transaction_read_only = on`) con acceso `SELECT` exclusivamente sobre las cuatro vistas BI (`vw_bi_session_overview`, `vw_bi_behavior_metrics`, `vw_bi_speed_decisions`, `vw_bi_recovery_opportunities`), sin almacenar ni alterar contraseñas existentes.

## 9. Seguridad

- RPC `ingest_session_report(jsonb)` declarada con `SECURITY INVOKER` y permisos `EXECUTE` restringidos a `service_role`.
- RLS habilitado en `sessions`, `session_events` y `session_recovery_opportunities`; `anon` y `authenticated` sin privilegios sobre tablas, secuencias, funciones ni vistas.
- `service_role` utilizado exclusivamente por el backend.
- El rol externo `lotba_bi_readonly` accede únicamente a las 4 vistas `vw_bi_*` mediante `lotba_bi_definer` (`CONNECTION LIMIT 10`, timeouts acotados y `default_transaction_read_only = on`). **Nunca exponer `SUPABASE_SECRET_KEY` ni credenciales en código o herramientas BI.**
- El cliente Unity mantiene `redirectLimit = 0` en `POST /api/v1/sessions` por seguridad; la URL productiva configurada en `SessionReportTransport.DefaultBaseUrl` debe apuntar al origen **HTTPS canónico sin redirects** (`301`/`302`/`308`).

## 10. Tests y validaciones

- Tests automatizados del backend: `npm test`.
- Validación en PostgreSQL local (instalación limpia `001`–`007` y simulación incremental sobre roles BI activos):
  - Compatibilidad de `schemaVersion = 13`, `19`, `20` temprano y `20` definitivo (`v0.5.0`)
  - Ingesta atómica e idempotente de `sessions`, `session_events` y `session_recovery_opportunities`
  - Reconciliación entre payload JSON, tablas base y vistas analíticas/BI
  - Verificación de permisos efectivos permitidos y denegados para `service_role`, `lotba_bi_readonly`, `anon` y `authenticated`

## 11. Estado del entorno

- Proyecto Supabase utilizado hasta ahora: **DEV (`lotba-dev`)**.
- Todavía **no existe deploy de producción** de este backend.
- No publicar Secret Keys ni credenciales.
- No almacenar ni enviar datos personales de alumnos (únicamente el contexto institucional agregado de jornada/colegio/turno/fecha).

## 12. Limitaciones y observaciones de dominio conocidas

- **Captura e inmutabilidad de `journey`:** en Unity (`GameController`), el contexto `journey` se exige al iniciar la partida y el botón de administración de jornada permanece oculto durante la carrera, pero el `Snapshot()` se toma al guardar el `SessionReport` al finalizar la partida (`RunState.Finished`). Una vez persistido en disco (`session_<runId>.json`), el archivo es inmutable ante cierres o cambios posteriores de jornada.
- **Velocidad cero en transiciones (`transition_decision_shown`):** en `v0.5.0`, el corredor se detiene físicamente (`speedKmh = 0`) antes de registrar el evento de transición; por ello `speed_kmh_at_offer` se expone como `NULL` (no `0`) y la velocidad crucero previa se refleja en `base_speed_kmh_at_offer`.
- **Ventana de salida de transición (`RecoverySourceSection` en `RunSession.Transitions.cs`):** cada sección de transición (`Transition = true`) se extiende unos metros después del punto `Stop` hasta cruzar `zone_entered` hacia el tramo jugable siguiente. Si ocurriera una colisión dentro de esa franja de salida, `AddLossToLedger` la registra en la sección de transición, mientras que la transición siguiente consulta únicamente `RecoverySourceSection` (retrocediendo sobre secciones `Transition = true`); por tanto, esa pérdida suma a `summary.pointsLost` / `gemsLost` pero no se ofrece en `trans-offer-*` (conviene tenerlo presente al contrastar `points_lost` total frente a `SUM(offered_points)`).
- **Campo legado `summary.recoveryBoosts` en `v0.5.0`:** permanece en `0` en `v0.5.0` porque el rediseño de zonas de recupero lo reemplazó funcionalmente por `recoveryOffers`, `recoveryAttempts`, `recoverySucceeded`, `recoveryFailed`, `recoveryDeclined`, `recoveryInterrupted` y `recoveryOffersClosed`; la columna `recovery_boosts` se conserva para compatibilidad con reportes `v0.4.1`.
- **Centinela `transitionSection = -1`:** dado que `JsonUtility` no serializa `int?` nulo, Unity emite `transitionSection = -1` en los eventos posteriores a la última transición planificada de la ruta (y el índice `>= 0` mientras exista una transición futura).
- El near-miss de obstáculos comunes todavía no existe.
