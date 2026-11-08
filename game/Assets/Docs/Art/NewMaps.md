# Crear módulos, zonas y recorridos

[Inicio de arte](README.md) · [Prefabs](Prefabs.md) · [Prueba en movimiento](Preview.md)

## Agregar un módulo

1. Duplicá `Maps/Zones/LaBoca/Prefabs/Modules/Module 0.prefab` o un módulo de la zona desde Project.
2. Conservá RoadModule, Entry, Exit y Art. Road está dentro de Streetscape. Si reemplazás el montaje completo, incluí Road y asigná su Surface al campo Road Surface del módulo.
3. Reemplazá o construí los objetos de Art con modelos o prefabs anidados. No hace falta combinar meshes.
4. Guardá el prefab. Revisá las medidas indicadas en el Inspector de RoadModule.
5. Abrí el MapDefinition de la zona y agregalo a Modules en el orden deseado.
6. Revisá sus uniones en [ArtPreview](Preview.md), incluyendo la última pieza contra la primera.

Duplicar un módulo mantiene referencias a sus visuales y materiales. Duplicarlos también si el cambio debe ser independiente.

## Crear una zona

1. Creá `Maps/Zones/<Nombre>/Prefabs/Art` y `Prefabs/Modules`.
2. Prepará uno o varios módulos estándar de 12 m.
3. Usá **Create > LOTBA > Maps > Map definition** en la carpeta de la zona.
4. Completá Display Name, Modules, Obstacles, Sky y Fog. En **Road Material** de este archivo MapDefinition, asigná el material que querés para el piso del barrio. Unity lo aplica a Surface durante la partida y en ArtPreview. Buscá `Road_` en el selector de materiales.
5. Revisá la ayuda y los errores del Inspector.
6. Creá un RouteDefinition con una sola sección: Map = la zona nueva, Module Count = 0 y Transition desactivado.
7. Abrí ArtPreview, elegí Route y asigná ese recorrido. Entrá en Play.

No hace falta crear una escena propia, agregar código ni registrar la zona en un menú.

## Agregar o modificar un hito

Un hito es un prefab que sirve de referencia visual al frente del recorrido, como el monumento del Obelisco. Se asigna por zona y se instancia una vez por cada sección que lo usa.

1. Guardá el modelo como prefab en `Maps/Zones/<Zona>/Prefabs/Landmarks`. Usá un pivote en la base y centrá el modelo respecto a ese pivote.
2. Abrí el archivo **MapDefinition** del barrio y arrastrá el prefab al campo **Landmark**. El ejemplo del juego es `Maps/Zones/Obelisco/Obelisco.asset`, que referencia `Maps/Zones/Obelisco/Prefabs/Landmarks/Obelisco.prefab`.
3. Ajustá **Landmark Distance** en metros. En Obelisco vale **115 m**. El hito comienza a esa distancia por delante del inicio de su sección. Al acercarte, se mantiene como mínimo a esa distancia frente al ciclista. Revisá que la niebla y el encuadre permitan verlo.
4. Para modificar su forma, escala o material, abrí el prefab. Para desplazar visualmente el modelo a un costado o en altura, mové un hijo dentro del prefab. El sistema coloca la raíz centrada en X y a nivel del piso.
5. Guardá y revisá en ArtPreview con **Route** para observar la aproximación desde otra zona. Para quitar el hito, dejá **Landmark** vacío en el MapDefinition.

## Crear un recorrido

1. En `Maps/Routes/Definitions`, usá **Create > LOTBA > Maps > Route definition** o duplicá uno existente.
2. En Sections, asigná las zonas en orden.
3. Indicá Module Count positivo para cada tramo intermedio. Cada unidad equivale a 12 m; el Inspector muestra los metros.
4. Dejá la última sección con **Module Count = 0**, que significa infinito.
5. Activá Transition solo en los enlaces que tengan duración positiva y un destino después.
6. Elegí Route en ArtPreview y asigná el recorrido para revisarlo.

Ejemplo mínimo: una sola sección, Map = La Boca, Module Count = 0 y Transition desactivado. Repite La Boca y no necesita enlaces.

Ejemplo con variedad: La Boca 30, transición 16 y Obelisco 0. La transición es una zona con una secuencia artística propia; ver [Transiciones](ZonesAndTransitions.md).

## Elegir el recorrido del producto

El jugador no elige recorrido. El equipo lo configura fuera de Play:

1. Abrir `Scenes/Game.unity`.
2. Seleccionar Game world.
3. Asignar el RouteDefinition en **RoadStreamer > Route**.
4. Guardar la escena.

Ese cambio define el recorrido de todas las partidas. Para ensayar alternativas, usar ArtPreview sin cambiar Game. No hay un comando de regeneración de escenas o UI.

## Entrega

Incluir prefabs, modelos, materiales, texturas, perfiles y sus `.meta`. Indicar qué zona utiliza los nuevos módulos y qué recorrido permite revisarlos. Mantener modelos fuente editables; los placeholders existentes sirven como referencia de tamaño y montaje.

El sistema mantiene calzada recta, nivelada y de ancho fijo. Curvas, pendientes u otras dimensiones requieren desarrollo; mover Entry o Exit no cambia el contrato del sistema.
