# Guía para artistas

Esta guía permite trabajar en el arte del juego sin tener que entender sus scripts. El objetivo es que el equipo pueda reemplazar modelos y sumar variantes manteniendo la misma circulación y reglas.

## Elegí una tarea

| Quiero… | Guía |
|---|---|
| Cambiar edificios, vegetación, materiales o un obstáculo | [Reemplazar arte y trabajar con prefabs](Prefabs.md) |
| Entender cómo se repite una calle y hacer una transición creíble | [Zonas y transiciones](ZonesAndTransitions.md) |
| Crear módulos, una zona nueva o un recorrido | [Nuevos mapas y recorridos](NewMaps.md) |

Para empezar, seguí las guías en ese orden. No hace falta crear un mapa nuevo para reemplazar el arte de uno existente.

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

Las rutas siguientes están dentro de `Assets/_LOTBA_Game/`.

| Recurso | Ubicación |
|---|---|
| Edificios y módulos de La Boca | `Maps/LaBoca/Prefabs/Art` y `Prefabs/Modules` |
| Edificios y módulos de Obelisco | `Maps/Obelisco/Prefabs/Art` y `Prefabs/Modules` |
| Tramo que une ambos barrios | `Maps/Transitions/LaBocaToObelisco` |
| Suelo y bordes compartidos | `Maps/Shared/Prefabs/Road base.prefab` |
| Orden de los recorridos | `Maps/Routes/Definitions` |
| Obstáculos y variantes por barrio | `Obstacles` |
| Materiales y meshes compartidos | `Art` |

Abrí el prefab desde **Project** para editarlo en **Prefab Mode**. Guardá allí el trabajo que debe repetirse. La decoración colocada solamente en la escena de juego no forma parte del módulo que el sistema genera durante la partida.

## Qué podés hacer sin programar

Podés reemplazar modelos, duplicar módulos, ordenar sus variantes, crear fichas de zonas y modificar los tramos de un recorrido existente. Crear una zona no agrega automáticamente un botón al menú. Para una nueva opción seleccionable, entregá sus recursos a desarrollo siguiendo [Nuevos mapas](NewMaps.md).

Las escenas de juego se reconstruyen con una herramienta del equipo. El arte definitivo debe quedar en prefabs y fichas de configuración. Coordiná el uso de **LOTBA > Update maps and UI**: reconstruye escenas, interfaz y vistas previas; no es necesario ejecutarlo por cada cambio visual de un prefab.

Para problemas de reglas, carga o generación, consultá la [guía de desarrollo](../Development/README.md).
