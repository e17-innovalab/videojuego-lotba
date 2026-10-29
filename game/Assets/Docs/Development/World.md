# Mundo, recorridos e integración

[Desarrollo](README.md) · [Jugabilidad](Gameplay.md) · [Obstáculos](Obstacles.md) · [Autoría para artistas](../Art/NewMaps.md)

## Separación de responsabilidades

`GameSceneFlow` carga la escena elegida al comenzar una partida. Dentro de esa escena, `RoadStreamer` recorre un `RouteDefinition` según la distancia de `RunSession`. El cambio de barrio no carga otra escena ni reinicia estado, tiempo o sensores.

| Tipo | Datos o responsabilidad |
|---|---|
| `RoadStandard` | Medidas comunes del espacio jugable |
| `RoadModule` | Prefab con conectores de referencia, superficie y un hijo Art reemplazable |
| `MapDefinition` | Módulos ordenados, suelo, ambiente, landmark y catálogo de obstáculos |
| `RouteSection` | Mapa, cantidad de módulos y flag Transition |
| `RouteDefinition` | Resolución de sección y módulo por distancia; detección de fronteras para patrones suaves |
| `RoadStreamer` | Pools visuales, colocación de módulos, ambiente y landmarks |
| `RunnerWorld` | Patrones de recogidas y obstáculos, separados de la decoración |

El [contrato de arte](../Art/Prefabs.md) es la referencia de montaje. Cambiar las constantes de `RoadStandard` requiere revisar generación, prefabs y circulación; los conectores Entry/Exit no calculan automáticamente un nuevo tamaño ni una curva.

## Reciclado y repetición

`RoadStreamer` mantiene 14 slots y comienza dos índices detrás del avance. Calcula el primer índice con `floor(distancia / longitud) − 2`; coloca cada instancia en Z = `índice × longitud − distancia`.

Al cambiar un índice, devuelve la instancia anterior a su pool y toma el prefab que corresponde al nuevo. `Initialize` prepara pools por variante considerando su frecuencia en los perfiles del recorrido. No editar las listas durante una partida esperando que se amplíen automáticamente esos pools.

`MapDefinition.ModuleAt` usa módulo matemático sobre la longitud de su array: la elección es ordenada y cíclica, no aleatoria. `RouteDefinition` transforma el índice global en uno local de sección. Cada sección comienza desde el inicio de su lista.

Las secciones anteriores a la última tienen una longitud finita: `ModuleCount × RoadStandard.ModuleLength`. La última es continua por la lógica de `SectionAt`; su contador se deja en cero como convención de autoría. Validar antes de integrar que:

- El recorrido tenga al menos una sección y todas tengan Map.
- Cada mapa tenga Modules no vacío y sin referencias nulas.
- Los tramos intermedios tengan Module Count positivo.
- El suelo y el catálogo de tráfico sean los previstos para cada mapa.

Estas condiciones son necesarias para los cálculos y la inicialización; no hay un validador completo de recorridos en el Inspector.

## Ambiente y encuentros durante una transición

Si la sección activa tiene Transition y existe una siguiente, se interpolan Sky, Fog, Fog Start y Fog End desde su mapa hacia el mapa siguiente usando el progreso normalizado de esa sección. La entrada a la transición parte de su propio perfil; no hay un fundido previo desde la sección de origen.

El material de superficie se asigna por mapa cuando se coloca el módulo. No hay interpolación de texturas, morph de arquitectura, curvas ni desniveles. El enlace visual se prepara con arte y, si hace falta, varias secciones de perfiles intermedios. Ver [diseño de transiciones](../Art/ZonesAndTransitions.md).

`NeedsGentlePatterns` comprueba el intervalo de una oleada: devuelve verdadero si cruza una frontera de sección o toca una transición. `RunnerWorld` utiliza esa información para simplificar el encuentro y filtrar tráfico largo. La duración y el catálogo también afectan cómo se percibe el paso entre zonas.

Los landmarks se instancian separadamente y se mantienen como referencia visual al frente. No forman parte de la repetición de cada cuadra ni se comportan como obstáculos del carril.

## Organización de assets

Rutas relativas a `Assets/_LOTBA_Game/`:

```text
Maps/
  Shared/Prefabs/Road base.prefab
  <Zona>/
    <Zona>.asset
    Prefabs/Art/
    Prefabs/Modules/
    Prefabs/Landmarks/       Si corresponde
    Scenes/                 Si tiene una escena contenedora
  Transitions/<OrigenADestino>/
    Transition.asset
    Prefabs/Art/
    Prefabs/Modules/
  Routes/
    Definitions/
    Scenes/
```

Una zona nueva puede incorporarse a las secciones de un recorrido existente sin sumar una escena. Un RouteDefinition nuevo debe asignarse a un RoadStreamer antes de su inicialización. El sistema no ofrece un cambio público de ruta en caliente.

## Incorporar una nueva opción de recorrido al menú

Crear assets no alcanza para registrar una opción seleccionable. La selección actual es explícita y tiene tres valores. Para ampliarla:

1. Recibir los MapDefinition, RouteDefinition, módulos y catálogos preparados. Revisar referencias y continuidad con [la guía de entrega de arte](../Art/NewMaps.md).
2. Agregar un valor a `GameLocation` sin renumerar los existentes, que se guardan en preferencias locales.
3. Actualizar el rango de `GameSceneFlow.Selected`, la ruta de escena y los casos de `PathFor` y `NameFor`. Sus alternativas actuales no descubren assets automáticamente.
4. Agregar la selección al HUD: campos y eventos de `GameHud`, botón y enlaces de `GameUiSetup`, y las conexiones de `GameController` que correspondan. Mantener el selector oculto durante una partida.
5. Preparar una escena contenedora con el RouteDefinition asignado a RoadStreamer. Adaptar `MapAssetSetup` y su generación de escenas para conservar esa conexión. `ConfigureWorld` se utiliza desde la misma assembly de Editor.
6. Incluir la escena en Editor Build Settings **y** en la lista que produce `MapAssetSetup.Apply`. La herramienta reemplaza esa lista; una adición solo manual no permanece al reconstruir.
7. Revisar apertura desde el menú, reintento y retorno al inicio, además de la progresión entre secciones.

Los pasos de revisión se realizan por el equipo al integrar el cambio; este documento no presupone que una opción nueva esté implementada.

## Herramientas de autoría

`MapAssetSetup` conecta perfiles, carretera, cámara y vista previa. `MapArtBuilder` y `TransitionArtBuilder` generan recursos visuales base; `PrototypeArt` concentra recursos compartidos. `GameSceneBuilder` compone la escena de entrada y `GameUiSetup` actualiza el HUD.

**LOTBA > Update maps and UI** reconstruye los contenedores de las opciones conocidas y conserva perfiles y módulos existentes. Las vistas previas tienen tag EditorOnly y se ocultan durante la inicialización. Las ediciones artísticas persistentes pertenecen a prefabs y perfiles, no a esos objetos de vista previa.
