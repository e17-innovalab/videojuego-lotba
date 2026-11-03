# Crear módulos, zonas y recorridos

[Inicio de arte](README.md) · [Prefabs](Prefabs.md) · [Prueba en movimiento](Preview.md)

## Agregar un módulo

1. Duplicá un prefab de `Maps/<Zona>/Prefabs/Modules` desde Project.
2. Conservá RoadModule, Entry, Exit, Road y Art.
3. Reemplazá o construí los objetos de Art con modelos o prefabs anidados. No hace falta combinar meshes.
4. Guardá el prefab. Revisá las medidas indicadas en el Inspector de RoadModule.
5. Abrí el MapDefinition de la zona y agregalo a Modules en el orden deseado.
6. Revisá sus uniones en [ArtPreview](Preview.md), incluyendo la última pieza contra la primera.

Duplicar un módulo mantiene referencias a sus visuales y materiales. Duplicarlos también si el cambio debe ser independiente.

## Crear una zona

1. Creá `Maps/<Nombre>/Prefabs/Art` y `Prefabs/Modules`.
2. Prepará uno o varios módulos estándar de 12 m.
3. Usá **Create > LOTBA > Maps > Map definition** en la carpeta de la zona.
4. Completá Display Name, Modules, Road Material, Obstacles, Sky y Fog. Landmark es opcional; es un hito separado que se mantiene visible al frente.
5. Revisá la ayuda y los errores del Inspector.
6. Creá un RouteDefinition con una sola sección: Map = la zona nueva, Module Count = 0 y Transition desactivado.
7. Abrí ArtPreview, elegí Route y asigná ese recorrido. Entrá en Play.

No hace falta crear una escena propia, agregar código ni registrar la zona en un menú.

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
