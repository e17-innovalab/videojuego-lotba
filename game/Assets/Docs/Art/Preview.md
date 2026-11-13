# Probar arte en movimiento

[Inicio de arte](README.md) · [Prefabs](Prefabs.md) · [Recorridos](NewMaps.md)

## Preparar y abrir

1. Editá el prefab del módulo, edificio u obstáculo desde Project, fuera de Play.
2. Guardalo. Si necesitás conservar la versión compartida, duplicá el prefab visual y reasignalo antes de editarlo.
3. Usá **LOTBA > Open art preview** o abrí `Assets/_LOTBA_Game/Scenes/ArtPreview.unity`.
4. Seleccioná **Art preview - configure here** en Hierarchy.
5. Elegí Mode en su Inspector y completá los campos visibles.
6. Entrá en Play: comienza directamente, sin menú de juego.

No hay que ejecutar una actualización ni combinar meshes. Una modificación guardada en un prefab se utilizará la próxima vez que arranques la revisión.

## Modos

| Mode | Campos | Resultado |
|---|---|---|
| Route | Route | Recorre una o varias zonas, con las transiciones incluidas en el recorrido |
| Zone | Zone | Recorre todos los módulos de la zona en el orden del asset y vuelve al primero |
| Module | Module Environment y Module | Repite solamente ese módulo; Module Environment aporta suelo, ambiente, hito y obstáculos |

Usá Module para unir una pieza consigo misma, Zone para comprobar vecinos distintos del mismo barrio y Route para revisar la entrada y la salida de una transición.

### Revisar un solo barrio

Elegí **Zone** y asigná el `MapDefinition` del barrio, por ejemplo `Zone_ReservaEcologica`. Entrá en Play para ver todos sus módulos, incluido el empalme del último con el primero. ArtPreview crea una ruta temporal en memoria.

Usá **Route_LaBocaToObelisco** en modo Route para revisar el trayecto de La Boca al Obelisco, incluido el giro de la transición y el cambio de ambiente.

**Speed** usa metros por segundo: 22 m/s equivalen a 79,2 km/h. Se puede ajustar durante Play. Show Rider, Show Gems y Show Obstacles se pueden cambiar durante Play, incluso con la reproducción pausada. Afectan también a los objetos presentes; los elementos ocultos siguen simulándose y reciclando sus instancias. Permiten limpiar la imagen para estudiar fachadas o revisar la escala con tráfico. Detener y volver a entrar en Play después de cambiar el modo o las referencias de contenido.

## Referencias internas

La sección **Referencias internas** está plegada por defecto. Sus campos conectan la escena y no son opciones necesarias para el trabajo artístico habitual:

| Campo | Responsabilidad |
|---|---|
| Settings (`GameSettings`) | Parámetros compartidos de cámara, cambio de carril y aparición de encuentros. La velocidad de revisión se controla con Speed. |
| World (`RunnerWorld`) | Anima al ciclista, controla la cámara y presenta gemas y obstáculos. |
| Road Stream (`RoadStreamer`) | Coloca y recicla los módulos de 12 m y aplica el ambiente del recorrido. |

Las referencias ya están asignadas. **Editar el asset compartido de Settings modifica también el juego principal.** Para estudiar el arte, usar los controles visibles de la prueba. Cambiar estas conexiones solo al preparar o mantener la escena.

## Controles de revisión

- A/D o flechas izquierda/derecha: cambiar de carril.
- W o flecha arriba: saltar.
- P: pausar o continuar el movimiento.
- Salir de Play: terminar la revisión.

La velocidad es fija, el recorrido no termina por tiempo y los obstáculos no penalizan ni ralentizan al ciclista. No hay menú, puntaje visible, sonido ni vibración. Los encuentros utilizan el sistema de aparición del juego; esta escena no verifica dificultad, puntuación ni la experiencia educativa.

Para comparar el encuadre, usar Game en orientación vertical. La cámara mantiene la misma zona de juego 9:16 que la partida. El movimiento y la cámara se comparten con el juego.

## Qué se guarda y qué no

Guardar los **prefabs y perfiles**, fuera de Play. Los objetos que aparecen durante la revisión son instancias recicladas: moverlos no cambia el archivo original. Los cambios hechos en la escena o en componentes durante Play normalmente se pierden al salir.

Los assets pueden conservar cambios incluso durante Play. Detené la revisión antes de editar. Module y Zone usan rutas temporales en memoria. Route usa el asset asignado y editarlo afecta a todas las escenas que lo referencian.

## Revisar una transición antes

1. Duplicá un RouteDefinition para revisión.
2. Usá pocos módulos de origen, por ejemplo 4.
3. Conservá la transición completa y el destino con cantidad 0.
4. Elegí Route en ArtPreview y asigná el duplicado.

Así se llega rápido al enlace sin modificar el recorrido del producto. Revisar desde la aproximación: los módulos futuros ya se ven antes de cruzar el límite entre zonas.

## Problemas comunes

| Síntoma | Revisar |
|---|---|
| No aparece el cambio | Se editó el prefab correcto y está en Modules; reiniciar Play |
| No arranca | Mensaje de validación del Inspector y Console |
| No se ve una zona nueva | Elegir Zone, asignar su MapDefinition y reiniciar Play |
| Cambia todo el barrio | Se editó un visual o material compartido |
| Se pierde una edición | Se modificó una instancia durante Play en vez del prefab fuente |
| Se ven objetos por debajo del suelo | Escala, pivote y posición del visual bajo Art |

Esta escena queda fuera del build. Para probar menús y reglas completas, abrir **LOTBA > Open game**.
