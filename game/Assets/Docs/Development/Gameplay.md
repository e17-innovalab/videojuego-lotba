# Jugabilidad y configuración

## Flujo y controles

El menú permite empezar y abrir Configuración. Antes de empezar se puede elegir Obelisco, La Boca o La Boca → Obelisco. Durante la partida se avanza automáticamente, se cambia entre tres carriles y se saltan obstáculos bajos para recoger monedas.

**FRENAR** termina la partida y muestra los resultados. La pausa permite continuar, configurar sonido y dispositivos o volver al inicio. Volver al inicio descarta la partida en curso. Los resultados permiten reintentar o regresar al menú.

El selector de recorrido está disponible antes de empezar; durante una partida solo se muestran ajustes de efecto inmediato. Las instrucciones de control están en el menú principal.

| Acción | Teclado | Celular |
|---|---|---|
| Carril | A/D o ←/→ | Deslizamiento lateral |
| Salto | W o ↑ | Deslizamiento hacia arriba |
| Frenar y terminar | Espacio | FRENAR |
| Pausa | P o Escape | Botón de pausa |

El control por inclinación puede habilitarse en un dispositivo compatible. Los gestos y el teclado tienen prioridad temporal sobre el acelerómetro.

## Monedas, puntaje y racha

- Cada recogida incrementa el valor del contador de monedas en **10**. `Coins` cuenta las recogidas y `CoinValue` expone `Coins × 10`.
- El puntaje suma **10 × multiplicador activo** por recogida. La distancia recorrida se registra por separado.
- La carga ganada por moneda es `1 / (FirstLevelCoins + ExtraCoinsPerLevel × (Multiplier − 1))`. Subir el multiplicador exige progresivamente más monedas.
- Al completar la barra, el multiplicador aumenta y se conserva una reserva de carga. No hay un máximo de diseño para el multiplicador.
- La barra pierde carga mientras la partida está en marcha. Cuando se vacía, la racha vuelve a cero y el multiplicador a ×1.
- El puntaje usa un entero de 64 bits. El multiplicador y la cantidad de recogidas usan enteros de 32 bits.

Un choque reinicia la racha y descuenta `min(puntaje, max(50, puntaje / 4))`, usando división entera. También reduce temporalmente la velocidad y activa 1,1 segundos de protección frente a impactos. Cada obstáculo puede cobrar un único impacto.

Las monedas recogidas ascienden y se reducen durante 0,32 segundos antes de volver al pool. El texto flotante muestra `+10` y, cuando corresponde, el cálculo del puntaje multiplicado. Los resultados distinguen valor de monedas, cantidad recogida, puntaje y distancia.

## Parámetros de diseño

El asset [GameSettings](../../_LOTBA_Game/Settings/GameSettings.asset) concentra los ajustes. Las velocidades del código están expresadas en metros por segundo.

| Parámetro | Valor de referencia | Efecto |
|---|---:|---|
| Duration | 120 s | Límite de tiempo de la partida |
| Initial Speed / Maximum Speed | 18 / 28 m/s | Rango de velocidad |
| Acceleration Time | 35 s | Constante de la curva exponencial de aceleración |
| Lane Change Time | 0,11 s | Duración del cambio lateral |
| Initial / Final Pattern Interval | 1,3 / 0,85 s | Intervalo entre patrones |
| Preview Time | 3,3 s | Anticipación para la aparición de encuentros |
| Camera Follow Time / Strength | 0,13 s / 0,95 | Amortiguación y seguimiento lateral |
| First Level Coins / Extra Coins Per Level | 8 / 3 | Exigencia de carga por nivel |
| Streak Drain Per Second | 0,025 | Pérdida de 2,5 % de la barra por segundo |
| Level Up Reserve | 0,30 | Carga conservada al subir de nivel |
| Seed | 2710 | Semilla de generación |

El Inspector del asset es la referencia para el balance activo. La duración de 120 segundos corresponde a la configuración del prototipo; el objetivo de producto de 5–8 minutos requiere ajustar y validar el recorrido completo.

La cámara persigue lateralmente al ciclista con amortiguación corta. La apariencia y los encuentros se describen en [Mapas](World.md) y [Obstáculos](Obstacles.md).

## Android, audio y sensores

La interfaz está orientada a pantalla vertical. `PortraitViewport` adapta el encuadre y la zona segura; en pantallas anchas mantiene una presentación vertical.

- **Sonido:** puede activarse o silenciarse desde Configuración.
- **Vibración:** se solicita por defecto y solo se habilita cuando Android confirma que existe un vibrador. Monedas e impactos usan feedback distinto y limitado en frecuencia.
- **Acelerómetro:** apagado por defecto. La opción solo se habilita cuando el Input System detecta un sensor compatible en un dispositivo móvil.
- **Calibración:** al empezar o continuar, sostener el teléfono centrado durante los primeros 0,35 segundos. El control usa filtrado, histéresis y separación entre comandos.
- **Pausa y pérdida de foco:** se detienen el muestreo del sensor, el audio de feedback y la vibración. El dispositivo se libera al cerrar la escena.

`AndroidHapticsPermissions` agrega `android.permission.VIBRATE` al manifest generado. Las opciones de sonido, vibración e inclinación se guardan por separado. Los dispositivos sin hardware compatible conservan los controles disponibles.

La integración web usa teclado y gestos; no implementa un puente JavaScript para acelerómetro o vibración del navegador. Sensores, vibración, legibilidad y rendimiento deben revisarse en dispositivos físicos antes de una entrega.

## Relación con el alcance educativo

El prototipo permite revisar ritmo, control, recompensas, pérdidas, racha y la decisión de frenar. El cierre reflexivo personalizado, el registro de decisiones y las situaciones de riesgo y recuperación necesitan consolidarse para cubrir el alcance de producto. Consultar el [README general](../../../../README.md) para distinguir ese objetivo de las funciones implementadas.
