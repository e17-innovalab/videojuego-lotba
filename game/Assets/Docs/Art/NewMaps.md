# Agregar módulos, zonas y recorridos

[Inicio de arte](README.md) · [Prefabs](Prefabs.md) · [Transiciones](ZonesAndTransitions.md)

## Elegir el tamaño del cambio

| Cambio | ¿Se puede preparar sin código? |
|---|---|
| Un edificio o aspecto nuevo | Sí: visual del prefab |
| Otra pieza para una zona existente | Sí: módulo y lista Modules |
| Una zona para usar dentro de un recorrido existente | Sí: perfil y secciones del recorrido |
| Un recorrido nuevo para entregar a desarrollo | Sí: perfil RouteDefinition |
| Otra opción seleccionable en el menú | Requiere integración de desarrollo |
| Curvas, desniveles o cambio del ancho jugable | Requiere cambios del sistema; no basta con el modelo |

## A. Agregar una variante a un barrio

1. En Project, seleccioná un prefab de `Maps/<Zona>/Prefabs/Modules` y duplicalo.
2. Dale un nombre que describa su uso, por ejemplo `Module_Plaza`.
3. Cambiá el arte siguiendo [Prefabs](Prefabs.md). Para independizar un visual compartido, duplicalo también y reemplazá su referencia bajo `Art`.
4. Abrí el perfil de la zona, por ejemplo `Maps/LaBoca/LaBoca.asset`.
5. En **Modules**, agregá una posición y arrastrá el módulo nuevo. Reordená la lista según la secuencia deseada.
6. Revisá las uniones con sus vecinos y la unión entre el final y el principio de la lista.

Agregar un prefab a la carpeta no lo conecta al recorrido: debe estar en **Modules**.

## B. Crear una zona nueva

Las rutas de trabajo empiezan en `Assets/_LOTBA_Game/`.

1. Creá `Maps/<NombreZona>/Prefabs/Art` y `Maps/<NombreZona>/Prefabs/Modules` desde Project.
2. Duplicá uno o varios módulos existentes dentro de la carpeta nueva. Conservá su estructura y medidas; reemplazá sus visuales.
3. En la carpeta de la zona, usá **Create > LOTBA > Maps > Map definition** y asigná un nombre al asset.
4. Completá su Inspector:

   | Campo | Qué asignar |
   |---|---|
   | Display Name | Nombre legible de la zona |
   | Modules | Una o más piezas de calle, en orden, sin posiciones vacías |
   | Road Material | Material de la superficie transitable |
   | Obstacles | Un catálogo de tráfico válido; elegirlo con diseño |
   | Landmark | Prefab de un hito visual, opcional |
   | Landmark Distance | Distancia visual de referencia del hito, si existe |
   | Sky | Color del fondo de cámara |
   | Fog / Fog Start / Fog End | Color y distancias de niebla |

5. Para los primeros montajes, podés usar el suelo y catálogo de una zona existente, ajustándolos después con el equipo.
6. Guardá el perfil y revisá el montaje de sus módulos. Un perfil sin módulos o con referencias vacías no está listo para integrarse.

La zona ya queda disponible como recurso para recorridos. No necesita una escena de Unity propia para insertarse como tramo.

## C. Insertarla en un recorrido existente

1. Guardá el trabajo del equipo y seleccioná el perfil correspondiente en `Maps/Routes/Definitions`.
2. En **Sections**, agregá una sección en el lugar donde deba aparecer la zona.
3. En **Map**, asigná su ficha. En **Module Count**, indicá cuántas piezas de 12 metros dura el tramo.
4. Si funciona como enlace hacia otra zona, activá **Transition** y prepará el arte de conexión según la [guía de transiciones](ZonesAndTransitions.md).
5. Todas las secciones intermedias deben tener una cantidad positiva. Dejá la última en cero como indicación de continuidad.
6. Revisá la entrada y la salida, el tráfico elegido y la duración total. Cambiar un perfil compartido modifica el recorrido que ya lo utiliza.

Si solo estás proponiendo un recorrido, duplicá el perfil y trabajá en esa propuesta. El duplicado no se selecciona automáticamente en el juego: necesita que desarrollo lo asigne o lo incorpore al menú.

## D. Preparar un recorrido nuevo

1. En `Maps/Routes/Definitions`, usá **Create > LOTBA > Maps > Route definition** o duplicá uno existente.
2. Armá las secciones: zona de origen, transición y destino, o las fases que necesite el diseño.
3. Asigná un mapa válido a cada sección y una cantidad positiva a todos los tramos anteriores al último.
4. Prepará una escena de revisión visual de las uniones. Anotá qué perfil debe utilizarse para revisar el recorrido en movimiento.
5. Entregá el perfil a desarrollo para conectarlo al juego.

El menú actual ofrece tres opciones concretas; no descubre recorridos nuevos buscando archivos en una carpeta. **LOTBA > Update maps and UI** tampoco registra automáticamente una cuarta opción y reconstruye la lista de escenas de build que conoce su código.

## Entrega a desarrollo

Indicar la ruta del perfil del recorrido, las zonas incluidas, sus módulos y la duración prevista en metros. Señalar si se cambia un recorrido existente o se pide una opción nueva en el menú. Incluir modelos, prefabs, materiales, texturas y sus `.meta`.

El equipo de desarrollo se encarga de la selección de recorrido, la escena contenedora si hace falta, el menú y la lista de escenas del build. Su procedimiento está en [Mundo y recorridos](../Development/World.md).
