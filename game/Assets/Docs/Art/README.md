# Guía para artistas

Esta guía permite trabajar en el arte del juego sin tener que entender sus scripts. El objetivo es que el equipo pueda reemplazar modelos y sumar variantes manteniendo la misma circulación y reglas.

## Tu primer cambio de arte

Vas a cambiar una cuadra de La Boca y verla en movimiento. Trabajá en el duplicado para conservar el módulo que usa el juego.

1. En **Project**, abrí `Assets/_LOTBA_Game/Maps/Zones/LaBoca/Prefabs/Modules`. Seleccioná `Module 0.prefab`, duplicalo con **Ctrl+D** y nombrá la copia `Module_Practica`.
2. Abrí la copia con doble clic. En **Hierarchy**, expandí **Art**. Agregá un prefab visual propio o un cubo de prueba directamente bajo **Art**, junto a el montaje **Streetscape**. Si usás un cubo, ponelo en la posición local **(5.7, 1, 6)** para verlo sobre la vereda. Conservá la raíz, **Entry**, **Exit** y **Art**. La calle del módulo está en **Art → montaje visual → Road**.
3. Guardá el prefab y salí de **Prefab Mode**. Abrí **LOTBA > Open art preview** y seleccioná **Art preview - configure here** en **Hierarchy**.
4. En el **Inspector**, elegí **Mode = Module**, arrastrá `Module_Practica` a **Module** y asigná `Maps/Zones/LaBoca/LaBoca.asset` a **Module Environment**.
5. Entrá en **Play**. Deberías ver el nuevo objeto en la misma posición de cada tramo, con la superficie de La Boca. Podés desactivar **Show Obstacles** y **Show Coins** para mirar la cuadra con más claridad.

Si ves la cuadra original, comprobá que **Module** apunta a la copia y que guardaste el cambio bajo **Art**. Para ajustar la posición, salí de Play y editá el prefab. Consultá la [guía de prefabs](Prefabs.md) y la [guía de ArtPreview](Preview.md) para seguir trabajando.

## Elegí una tarea

| Quiero… | Guía |
|---|---|
| Ver una zona o módulo en movimiento sin menús | [Prueba de arte](Preview.md) |
| Cambiar edificios, vegetación, materiales o un obstáculo | [Reemplazar arte y trabajar con prefabs](Prefabs.md) |
| Elegir qué obstáculos pueden aparecer en cada barrio | [Catálogos de obstáculos por zona](../Development/Obstacles.md#catálogos-y-pools) |
| Entender cómo se repite una calle y hacer una transición creíble | [Zonas y transiciones](ZonesAndTransitions.md) |
| Crear módulos, una zona nueva o un recorrido | [Nuevos mapas y recorridos](NewMaps.md) |

Después del primer ejercicio, elegí la guía que corresponda a tu tarea.

## Las cinco piezas del escenario

| Pieza | Qué significa |
|---|---|
| Modelo | Geometría y materiales: un edificio, árbol, auto, etc. |
| Prefab | Archivo reutilizable que reúne objetos y sus propiedades. Puede contener otros prefabs. |
| Módulo | Un pedazo de calle de **12 metros**, con suelo y espacios preparados para colocar arte. |
| Zona o mapa | Una ficha que reúne módulos, ambiente y tráfico de un barrio. En el Inspector se llama `MapDefinition`. |
| Recorrido | Una lista que dice por qué zonas se pasa y cuántos módulos dura cada una. Se llama `RouteDefinition`. |

Una **escena de Unity** contiene los objetos que hacen funcionar el juego. No es lo mismo que una zona: un recorrido puede pasar por varios barrios dentro de una única escena.

## Dónde trabajar

En La Boca, los prefabs `Streetscape_01` a `Streetscape_04` agrupan edificios y mobiliario bajo **Art**. La Boca, Obelisco y la transición usan el mismo esquema. Cada **Streetscape** incluye **Road** como prefab anidado. El **RoadModule** referencia su **Surface**. Si sustituís el montaje completo, reasigná **Road Surface** a la superficie nueva. Para cambiar el piso de un barrio, asigná **Road Material** en su ficha de zona. Para dar un color propio a una pieza, asignale un material duplicado desde el Inspector.

Las rutas siguientes están dentro de `Assets/_LOTBA_Game/`.

| Recurso | Ubicación |
|---|---|
| Edificios y módulos de La Boca | `Maps/Zones/LaBoca/Prefabs/Art` y `Prefabs/Modules` |
| Edificios y módulos de Obelisco | `Maps/Zones/Obelisco/Prefabs/Art` y `Prefabs/Modules` |
| Tramo que une ambos barrios | `Maps/Zones/Transitions/LaBocaToObelisco` |
| Suelo y bordes compartidos | `Maps/Zones/Shared Prefabs/Road_Base.prefab` |
| Orden de los recorridos | `Maps/Routes/Definitions` |
| Obstáculos y variantes por barrio | `Obstacles` |
| Materiales y meshes compartidos | `Art` |

Abrí el prefab desde **Project** para editarlo en **Prefab Mode**. Guardá allí el trabajo que debe repetirse. La decoración colocada solamente en la escena de juego no forma parte del módulo que el sistema genera durante la partida.

## Qué podés hacer sin programar

Podés reemplazar modelos, duplicar módulos, ordenar variantes, crear zonas y recorridos. Para revisarlos, usar **LOTBA > Open art preview** y elegir la referencia en el Inspector. No hace falta crear una escena por zona ni añadir código.

Guardá el trabajo visual en prefabs y perfiles. Los montajes de La Boca, Obelisco y la transición reúnen prefabs editables de edificios, vegetación, iluminación y mobiliario según la zona. Abrí el prefab de edificio para mover una ventana o un balcón. Si el cambio debe afectar solo una cuadra, hacé una variante del prefab y asignala en su Streetscape.

El recorrido del producto se asigna en Game world > RoadStreamer > Route dentro de Game.unity. Para experimentar, usá la escena de revisión y conservá esa asignación.

Para problemas de reglas, carga o generación, consultá la [guía de desarrollo](../Development/README.md).
