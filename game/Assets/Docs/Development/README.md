# Guía de desarrollo

Esta guía explica cómo funciona el juego y dónde cambiar su comportamiento. El [índice común](../README.md) permite elegir una tarea; la [guía de arte](../Art/README.md) contiene los pasos de autoría visual y las medidas que deben respetar los modelos. El [README del repositorio](../../../../README.md) describe el contexto y el alcance del producto.

## Nombre del juego

**Frená a Tiempo** es el nombre provisional, pendiente de definición final con el equipo y LOTBA. La fuente de verdad es [GameIdentity.cs](../../_LOTBA_Game/Runtime/GameIdentity.cs): `Title` define el nombre e `IsProvisional` indica su estado. La interfaz toma esos valores y muestra la aclaración de nombre provisional.

El nombre de la aplicación se sincroniza desde esa definición al cargar los scripts en el Editor y antes de un build. Los namespaces y las claves de preferencias son identificadores técnicos estables; no definen el nombre mostrado al público.

| Guía | Contenido |
|---|---|
| [Jugabilidad y configuración](Gameplay.md) | Flujo, controles, puntuación, balance, audio y vibración |
| [Mundo y recorridos](World.md) | Generación, perfiles, transiciones e integración de nuevas opciones |
| [Obstáculos](Obstacles.md) | Definiciones, catálogos, colisiones y API |

## Abrir el proyecto

1. Abrir la carpeta `game/` desde Unity Hub con la versión indicada en [ProjectVersion.txt](../../../ProjectSettings/ProjectVersion.txt).
2. Dejar que Unity resuelva los paquetes declarados en `Packages/manifest.json`.
3. Abrir [Game.unity](../../_LOTBA_Game/Scenes/Game.unity) o usar **LOTBA > Open game**.

En el Editor, Play Mode ejecuta la escena abierta. El orden de escenas del build se gestiona en Editor Build Settings y tiene `Game` como entrada. Solo Game integra el build. ArtPreview es una escena de revisión exclusiva del Editor, accesible con LOTBA > Open art preview.

## Organización de recursos

Las rutas de la tabla son relativas a `Assets/`.

| Carpeta | Responsabilidad |
|---|---|
| `_LOTBA_Game/Runtime` | Estado, control, presentación y soporte de dispositivos |
| `_LOTBA_Game/Editor` | Herramientas de autoría y configuración de assets |
| `_LOTBA_Game/Scenes` | Escena de entrada |
| `_LOTBA_Game/Maps` | Barrios, módulos, transiciones y recorridos |
| `_LOTBA_Game/Obstacles` | Definiciones, catálogos y prefabs de obstáculos |
| `_LOTBA_Game/Art` | Meshes, materiales y recursos visuales compartidos |
| `_LOTBA_Game/Prefabs` | Ciclista, UI, gemas, feedback y modelos compartidos |
| `_LOTBA_Game/Settings` | Parámetros del juego |
| `ThirdParty/TextMesh Pro` | Fuentes, shaders y configuración de texto |
| `Docs` | Guías técnicas mantenidas junto al proyecto |

## Arquitectura

El código principal usa el namespace `Lotba.Game`, con assemblies separadas para runtime y Editor.

| Componente | Responsabilidad |
|---|---|
| `RunSession` | Estado de partida, tiempo, velocidad, puntaje, gemas y racha; sin objetos de escena |
| `GameController` | Composición, prioridades de entrada y coordinación de pausa, menú y partida |
| `RunnerWorld` | Ciclista, cámara, patrones, pools y detección de encuentros |
| `GameHud` | Presentación de menús, configuración, HUD y resultados |
| `RoadStreamer` | Reciclado de módulos y mezcla ambiental según distancia |
| `ArtPreviewController` | Revisión visual en el Editor, con el mismo mundo y sin menú |
| `DeviceFeedbackAndInput` | Detección de capacidades y vibración |
| `AudioManager` | Feedback sonoro |
| `PortraitViewport` | Encuadre vertical y zona segura |

La lógica de sesión y la presentación se mantienen separadas. Gemas, obstáculos y módulos utilizan pools; los perfiles de mapas y obstáculos definen el contenido por referencias serializadas. Ver sus contratos antes de reemplazar arte o cambiar huellas jugables.

## Configuración de Unity

- Pipeline de renderizado: **Built-in**.
- Entrada: **Input System Package (New)**. Se configura en Project Settings > Player > Other Settings > Active Input Handling. Reiniciar el Editor si se modifica esta opción.
- UI: **uGUI y TextMesh Pro**.
- Orientación de Android: **vertical**.
- Plataforma prioritaria: **Android**. La integración web es una alternativa en evaluación.

No se requiere un backend ni cuentas para ejecutar la partida. Las preferencias de sonido y dispositivo se conservan localmente. El recorrido se asigna en la escena, fuera de Play. Eso no sustituye un registro de eventos para el cierre educativo.

## Autoría y mantenimiento

El balance se edita en [GameSettings](../../_LOTBA_Game/Settings/GameSettings.asset). El arte de escenarios y obstáculos se modifica en prefabs y perfiles.

La escena y los prefabs son las fuentes editables. Guardarlos es suficiente: no hay regeneración ni actualización global de mapas o UI. Los Inspectors de mapas, recorridos y módulos muestran ayuda y validaciones. Ver [prueba de arte](../Art/Preview.md).

Mover o renombrar assets desde Unity para conservar sus referencias. Versionar los `.meta`, `Assets`, `Packages` y `ProjectSettings`. Las carpetas `Library`, `Temp`, `Logs` y `UserSettings` son generadas localmente.

Mantener nombres de clases y namespaces estables, independientes del nombre comercial. Al renombrar un tipo serializado, conservar el GUID del script y completar una migración coordinada: guardar el contenido con el tipo actual, recargarlo sin el alias temporal y comparar valores y referencias. `MovedFrom` puede ayudar durante ese proceso; conservarlo a largo plazo solo cuando haya contenido distribuido o datos externos que requieran compatibilidad. Para campos serializados, aplicar el mismo criterio con `FormerlySerializedAs`.

Las claves de preferencias locales son identificadores persistentes y no deben cambiar junto con el namespace o el nombre comercial.

## Dónde implementar un cambio

| Cambio | Lugar principal y conexiones que revisar |
|---|---|
| Regla de puntaje, pérdida, racha o final de partida | `RunSession`; `GameHud` si cambia la información presentada |
| Balance de velocidad, duración o anticipación | Asset `GameSettings`; consultar sus unidades y la curva en `RunSession` |
| Prioridad de un control, gesto, pausa o cambio de estado | `GameController`; conservar la prioridad de frenar sobre la simulación |
| Botón o panel | `Game UI.prefab` para autoría; `GameHud` para referencias y eventos serializados |
| Patrón de gemas o detección de un obstáculo | `RunnerWorld`; mantener el contrato de `ObstacleDefinition` |
| Recorrido o selección de zona | Perfiles y proceso de integración de [World.md](World.md) |
| Feedback o una capacidad de dispositivo | `DeviceFeedbackAndInput`, `AudioManager` y el ciclo de pausa/foco de `GameController` |
| Nombre público | `GameIdentity`; el nombre de la aplicación lo sincroniza `GameIdentitySettings` |

Al agregar UI, completar los enlaces de los campos serializados además de crear el objeto visible. El HUD emite intenciones y el controlador coordina los cambios de estado; evitar reglas de partida dentro de un botón. Al cambiar la simulación, conservar que el estado de sesión no dependa de objetos visuales.

Los cambios de contenido se preparan fuera de Play. RoadStreamer prepara pools al inicializar: detener y volver a entrar para revisar cambios de módulos, perfiles u obstáculos. ArtPreview comparte la simulación visual, pero no sustituye la revisión del balance y los menús en Game.

Las guías deben describir el estado vigente: actualizar rutas, reglas y contratos junto con el cambio correspondiente. Usar enlaces relativos, evitar duplicar instrucciones y separar las propuestas pendientes de las funciones implementadas. Los Markdown de `Docs` no deben conectarse a recursos cargados por el juego.
