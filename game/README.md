# Frena a Tiempo

Proyecto Unity principal del videojuego educativo de LOTBA. El juego actual es City Rush, antes ubicado en Demo2.

Abrir esta carpeta (game) desde Unity Hub con Unity 6000.3.11f1.

La escena de entrada es `Assets/_Game/Scenes/CityRush.unity`. También se puede abrir con **LOTBA > Open City Rush**. Está configurada como escena inicial de Play Mode y es la primera escena del build.

## Estructura

- `Assets/_Game/Runtime`: lógica del juego y dispositivos Android.
- `Assets/_Game/Editor`: herramientas para crear y mantener escenas y assets.
- `Assets/_Game/Scenes`: entrada del juego.
- `Assets/_Game/Maps`: barrios, transiciones, recorridos y sus escenas.
- `Assets/_Game/Obstacles`: modelos y perfiles de obstáculos por recorrido.
- `Assets/_Game/Art`, `Prefabs` y `Settings`: recursos compartidos y ajustes.
- `Assets/ThirdParty/TextMesh Pro`: fuentes, shaders y configuración de texto.

Ver [guía del juego](Assets/_Game/README.md), [contrato de mapas](Assets/_Game/Maps/README.md) y [contrato de obstáculos](Assets/_Game/Obstacles/README.md).

Los scripts, escenas, audio y demás recursos exclusivos de los prototipos anteriores se retiraron. La carpeta Demo2 ya no existe dentro del proyecto. No colocar archivos del juego junto a `Assets`: Unity importa los recursos desde esa carpeta.

Se retiraron los paquetes de inferencia, colaboración, multiplayer, Timeline, Visual Scripting y el conjunto opcional de desarrollo. Se conservan Input System, uGUI/TextMesh Pro, los módulos de Unity y las herramientas de edición Assistant y Pipeline.

Assets, incluidos sus archivos .meta, Packages y ProjectSettings forman parte del proyecto versionado. Library, Temp, Logs y UserSettings son archivos locales generados por Unity.
