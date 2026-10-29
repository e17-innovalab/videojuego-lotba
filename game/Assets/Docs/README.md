# Guías del proyecto

**Frená a Tiempo** es el nombre provisional del juego. El [README general](../../../README.md) explica su propósito y alcance.

La documentación se divide según la tarea que quieras hacer:

| Necesito… | Empezar por |
|---|---|
| Entender el código, cambiar reglas o integrar funciones | [Desarrollo](Development/README.md) |
| Reemplazar modelos, editar prefabs o preparar escenarios | [Arte](Art/README.md) |
| Ajustar duración, velocidad o racha | [Jugabilidad y configuración](Development/Gameplay.md) |
| Entender la repetición de calles y preparar una transición | [Zonas y transiciones](Art/ZonesAndTransitions.md) |
| Crear una zona para incorporarla al juego | [Nuevos mapas y recorridos](Art/NewMaps.md) |

## Abrir el juego

Abrí `game/` desde Unity Hub con la versión indicada en `ProjectSettings/ProjectVersion.txt`. La entrada es [Game.unity](../_LOTBA_Game/Scenes/Game.unity), accesible también desde **LOTBA > Open game**.

En estas guías, **Project** es la ventana donde se encuentran los archivos; **Hierarchy** muestra los objetos de la escena; **Inspector** muestra las propiedades de lo seleccionado. Los nombres de campos y menús se escriben como aparecen en Unity.

## Cómo mantener las guías

Las instrucciones de programación están en `Development/`; los procedimientos de autoría visual, en `Art/`. Los contratos de tamaño y montaje se explican en la guía artística y se enlazan desde la técnica. Actualizar las guías junto con los cambios del sistema y distinguir las funciones existentes de las que requieren desarrollo.
