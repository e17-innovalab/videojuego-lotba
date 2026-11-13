# Mundo y recorridos

[Desarrollo](README.md) · [Arte](../Art/README.md) · [Prueba de arte](../Art/Preview.md)

## Una escena, un recorrido

`Scenes/Game.unity` contiene el juego completo. En `Game world`, el componente `RoadStreamer` tiene el campo **Route**: esa referencia define el recorrido de todas las partidas. No hay elección de zonas en el menú ni carga de otra escena al empezar, reintentar o cambiar de barrio.

El recorrido predeterminado es `Maps/Routes/Definitions/Route_LaBocaToObelisco.asset`: La Boca durante 30 módulos (360 m), transición durante 16 (192 m) y Obelisco infinito. La sesión puede terminar por tiempo o frenado antes de recorrerlo entero.

Para cambiarlo: abrir Game, seleccionar Game world, asignar otro RouteDefinition y guardar la escena. No hace falta regenerar nada ni añadir escenas al build. La lista del build contiene solamente Game; ArtPreview es una herramienta local del Editor.

## Datos y responsabilidades

| Tipo | Responsabilidad |
|---|---|
| RoadStandard | Longitud de 12 m, calzada de 9 m, carriles a 2,7 m y altura libre de 4,2 m |
| RoadModule | Contenedor con Entry, Exit, Road y Art |
| MapDefinition | Secuencia ordenada de módulos, suelo, ambiente, hito visual y obstáculos |
| RouteSection | Zona, cantidad de módulos y condición de transición |
| RouteDefinition | Sección y módulo correspondientes a una distancia |
| RoadStreamer | Reciclado de módulos, ambiente e hitos |
| RunnerWorld | Ciclista, cámara, gemas, patrones y encuentros |

Los módulos y perfiles son assets normales editables. No hay generadores de arte ni un comando global de actualización. La interfaz se edita en `Prefabs/Game UI.prefab`; las escenas se editan y guardan directamente.

## Repetición y validación

Modules se recorre cíclicamente en orden: A, B, C, A... Cada sección empieza por el primer módulo de su zona. Module Count controla cuántos módulos dura una sección; no cambia el tamaño de las piezas.

Reglas:

- Al menos una sección, con una zona válida y módulos asignados.
- Secciones intermedias: cantidad positiva.
- Última sección: **0 = infinito**.
- Una transición necesita duración positiva y una sección de destino.
- Cada módulo conserva Entry en (0,0,0), Exit en (0,0,12), escala uno y referencias completas.

Los Inspectors muestran ayuda y errores. RoadStreamer valida la ruta antes de inicializar los pools. Una ruta de una sola zona con cantidad cero es válida y no requiere transición.

RoadStreamer mantiene 14 posiciones, dos detrás del avance. Cada instancia se coloca en Z = índice × 12 − distancia. Las instancias que salen se devuelven a pools por prefab. No modificar el contenido del recorrido durante Play: los pools se preparan al inicializarlo. `SetRoute` se utiliza antes de Initialize, no es un cambio de recorrido en caliente.

## Transiciones

Transition mezcla Sky, Fog, Fog Start y Fog End desde el perfil de enlace hacia el destino. El inicio utiliza el ambiente del propio enlace: hacerlo compatible con el origen. La arquitectura se cambia mediante módulos, no por interpolación de meshes. Tampoco hay fundido automático de texturas de calzada.

`NeedsGentlePatterns` suaviza oleadas que cruzan fronteras o caen dentro de una transición. Los obstáculos se eligen por la zona correspondiente a su posición futura.

El tramo de enlace actual contiene dos posiciones de salida, ocho de calle arbolada y seis de aproximación. Si Module Count supera la longitud de Modules, la secuencia vuelve a empezar. Consultar [Transiciones](../Art/ZonesAndTransitions.md).

## Prueba artística y mantenimiento

ArtPreviewController está disponible solo en el Editor. **Route** utiliza un RouteDefinition guardado. **Zone** crea una ruta temporal para repetir la lista completa del MapDefinition elegido. **Module** crea un mapa y una ruta temporales para repetir una sola pieza con el ambiente de Module Environment. Los tres modos reutilizan RunnerWorld y RoadStreamer con velocidad fija, sin límite temporal ni telemetría de partida.

El Inspector presenta modo, selección de contenido, velocidad y visibilidad. El Foldout Referencias internas está cerrado por defecto y agrupa Settings, World y Road Stream: parámetros compartidos, presentación y reciclado de módulos respectivamente. Settings apunta a un asset compartido; modificar sus valores afecta también a Game. Speed es el control de velocidad específico de la revisión.

No hay daño al ciclista ni feedback de dispositivo en esta revisión. Sí se usan los patrones, dimensiones, movimiento y animaciones del mundo real. Es una revisión visual, no una validación del balance de partida.

La escena de revisión conserva su propia composición y referencias a los mismos assets visuales. Si cambia el contrato de RunnerWorld, revisar ambas escenas. No mantener escenas separadas por barrio.

Cada módulo referencia un Streetscape con Road_Base anidado. RoadModule enlaza el renderer Surface y aplica Road Material de la zona. Los edificios y el mobiliario se componen de prefabs editables. Art/Meshes reúne geometrías compartidas en uso, entre ellas autos, detalles de calle, vegetación y el monumento del Obelisco.
