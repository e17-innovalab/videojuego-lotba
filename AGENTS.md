# Telemetría de partida

Al agregar o cambiar una mecánica que afecte decisiones, recompensas, pérdidas, progreso o el cierre de una partida, revisar el registro en `RunSession` y el esquema en `RunTelemetry`. Actualizar los eventos, el resumen, el panel y el JSON cuando cambien los hechos que registramos. Verificar que se registren acciones confirmadas y que `ArtPreview` quede fuera de la telemetría de partidas. Incrementar `schemaVersion` si cambia el significado o la estructura de los datos exportados.
