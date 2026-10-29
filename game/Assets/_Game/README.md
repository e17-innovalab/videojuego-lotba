# City Rush

Juego principal del proyecto Unity `game/`. Los recursos propios están directamente en `Assets/_Game/`; la antigua carpeta `Demo2` se eliminó. TextMesh Pro se conserva en `Assets/ThirdParty/` porque proporciona las fuentes, shaders y configuración usados por la interfaz.

## Entrada y escenarios

Abrir `Scenes/CityRush.unity`. Configuración ofrece **Obelisco**, **La Boca** y **La Boca → Obelisco** antes de empezar. La tercera opción combina ambos barrios durante una única partida: 360 m de La Boca, 192 m de calle arbolada y Obelisco continuo.

Mapas, transiciones y recorridos están organizados en `Maps`. Las cuatro escenas están registradas en Editor Build Settings. El arte se reemplaza mediante prefabs estandarizados y perfiles; ver [contrato de arte y recorrido](Maps/README.md).

## Controles y menú

Los controles se explican en el menú principal. No hay tutoriales superpuestos durante la carrera, flechas inferiores ni botón de salto.

| Acción | Teclado | Celular |
|---|---|---|
| Carril | A/D o ←/→ | Deslizamiento lateral |
| Salto | W o ↑ | Deslizamiento hacia arriba |
| Frenar | Espacio | FRENAR |
| Pausa | P o Escape | II |

El final ofrece **Reintentar** o volver al inicio. La pausa también permite **Volver al inicio**, descartando la partida actual. Configuración tiene su propio panel, accesible desde el inicio y desde pausa. Se retiró la opción de efectos visuales del menú. Las opciones presentes son sonido, vibración, inclinación y recorrido; las funciones de dispositivo no disponibles quedan deshabilitadas. Durante una partida se ocultan las opciones de recorrido y quedan únicamente los ajustes de efecto inmediato.

## Racha, multiplicador y monedas

- El contador de monedas aumenta **+10 fijo** por recogida. El puntaje aumenta **10 × multiplicador**, mostrado por separado en el feedback. Se quitó la suma automática de puntos por distancia.
- La barra se carga con monedas: el primer nivel requiere aproximadamente ocho; cada nivel añade tres monedas a la exigencia de carga. La pérdida gradual hace que la cantidad real dependa del ritmo de recogida.
- La barra baja continuamente a razón de 2,5 % por segundo de carrera. Si llega a cero, la racha se reinicia y el multiplicador vuelve a ×1.
- Al llenar la barra sube un nivel y queda una reserva de 30 % para que el nivel recién alcanzado no desaparezca al cuadro siguiente.
- El multiplicador no tiene un tope de diseño. Se usan puntos de 64 bits para ampliar el margen del marcador.
- Un choque conserva su penalización y reinicia la racha.
- Las monedas recogidas siguen una animación de ascenso y reducción durante 0,32 s antes de volver al pool. El texto de puntos también asciende.

`Settings/CityRushSettings.asset` contiene estos ajustes junto con duración, velocidad, aceleración, separación de patrones, anticipación y tiempo de carril. Se conservaron únicamente los dos divisores centrales de carril, también en los prefabs del escenario urbano base.

## Android, vibración e inclinación

La orientación del proyecto queda en vertical. No se cambia automáticamente la plataforma de build ni se instala un SDK.

- **Vibración:** solicitada por defecto, efectiva únicamente cuando Android confirma `hasVibrator()`. Monedas e impactos emiten feedback distinto, limitado en frecuencia. La opción se guarda independientemente del sonido.
- **Acelerómetro:** apagado por defecto. Solo se habilita si el Input System expone un acelerómetro en un dispositivo móvil. Se calibra durante los primeros 0,35 s al empezar o continuar; sostener el teléfono centrado en ese instante.
- **Control por inclinación:** izquierda/centro/derecha eligen el carril correspondiente. Filtrado, umbrales con histéresis y separación entre comandos evitan cambios por ruido. Los gestos y el teclado conservan prioridad temporal.
- Se detiene el muestreo y se cancela el feedback al perder foco o pausar. El dispositivo se libera al cerrar la escena.
- El procesador de Android añade `android.permission.VIBRATE` al manifest generado sin reemplazar la actividad de Unity.
- En Editor y en la integración web actual no se habilita hardware que no pueda confirmarse. No hay un puente JavaScript para los sensores o la vibración del navegador en esta entrega; web conserva gestos y teclado.

Referencia de implementación: [sensores del Input System](https://github.com/Unity-Technologies/InputSystem/blob/develop/Packages/com.unity.inputsystem/Documentation~/supported-sensors-reference.md) y [API de feedback háptico de Android](https://developer.android.com/develop/ui/views/haptics/haptics-apis).

## Prefabs y código

City Rush es independiente de la primera demo. Se conservan prefabs de UI, ciclista, feedback, vehículos, obstáculos, módulos de ambas zonas y Obelisco. Los módulos comparten materiales y agrupan geometría fija por material. Las instancias de monedas y obstáculos utilizan pools.

- `RushSession`: puntos, ritmo, carga de racha y estado de partida.
- `RushController`: prioridades de entrada, pausa y transición de escenarios.
- `RushWorld`: presentación, patrones y encuentros barridos.
- `RushHud`: interfaz y configuración.
- `Platform/RushDevices`: capacidades, vibración y prueba de concepto del acelerómetro.
- `RushSceneFlow`: selección y transición de escena.
- `Editor/`: generación y actualización de assets/escenas, y permiso de Android.

`LOTBA > Update City Rush maps and UI` conserva los módulos y perfiles existentes y reconstruye las escenas contenedoras. El arte definitivo debe editarse dentro de los prefabs, según `Maps/README.md`.

## Estado de esta entrega

Se importó y compiló el código para editar y guardar las escenas. No se ejecutaron pruebas ni Play Mode. No se generó Android ni WebGL; sensores y vibración requieren la prueba manual en un teléfono compatible. Los assets son placeholders y el balance de la nueva racha queda pendiente de esa revisión.

La primera demo y sus builds antiguos se eliminaron por pedido del usuario. La carpeta `TEMP_Demo2` de la raíz conserva únicamente material temporal; no es necesaria para jugar.

## Cámara y obstáculos

La cámara persigue lateralmente al ciclista con amortiguación corta (0,13 s; fuerza de seguimiento 95 %). Estos valores se ajustan en `CityRushSettings.asset`. La transición tiene 16 módulos (192 m) con veredas sin autos.

Los obstáculos se configuran en `Obstacles/Definitions` y se seleccionan mediante catálogos por mapa. La Boca incluye el bondi 46 azul/celeste; Obelisco, el 91 verde y el 36 rojo. Comparten modelo y cambian número/colores por datos. Los bondis miden 8,8 m de largo y 3,25 m de alto, son no saltables y mantienen su volumen de colisión hasta que pasa la parte trasera. La transición utiliza autos y obstáculos bajos.

Ver [contrato de obstáculos y API de cambio](Obstacles/README.md). No se ejecutaron pruebas ni se inició Play Mode para esta entrega; se detuvo la sesión que estaba abierta para guardar assets.
