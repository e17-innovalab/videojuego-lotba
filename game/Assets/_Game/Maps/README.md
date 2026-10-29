# Mapas y transiciones · contrato de arte

## Organización

```text
Maps/
  Shared/Prefabs/Road base.prefab
  LaBoca/
    LaBoca.asset
    Prefabs/Art/       Modelos visuales de referencia
    Prefabs/Modules/   Módulos preparados para el recorrido
    Scenes/LaBoca.unity
  Obelisco/
    Obelisco.asset
    Prefabs/Art/
    Prefabs/Modules/
    Prefabs/Landmarks/
    Scenes/Obelisco.unity
  Transitions/LaBocaToObelisco/
    Transition.asset
    Prefabs/Art/
    Prefabs/Modules/
  Routes/
    Definitions/      Orden y duración de los tramos
    Scenes/LaBocaToObelisco.unity
```

Materiales y meshes generados compartidos permanecen en `Art`. Ciclista, vehículos, monedas, obstáculos, feedback y UI permanecen en `Prefabs`; no pertenecen a un barrio.

## Medidas comunes

| Elemento | Contrato |
|---|---|
| Unidad | 1 unidad de Unity = 1 metro |
| Orientación | +Z hacia adelante, +Y hacia arriba |
| Transform del módulo | Posición local cero, rotación cero, escala uno |
| Entrada | `Entry` en (0, 0, 0) |
| Salida | `Exit` en (0, 0, 12) |
| Longitud lógica | 12 m |
| Ancho de calzada | 9 m, de X = −4,5 a X = +4,5 |
| Centros de carril | X = −2,7; 0; +2,7 |
| Suelo de circulación | Y = 0 |
| Espacio libre | Mantener libre la calzada hasta Y = 4,2 m |

El componente `RushRoadModule` muestra este volumen con un gizmo al seleccionarlo. Las dimensiones jugables están centralizadas en `RushRoadStandard`. Las dos líneas centrales, aceras y bordes pertenecen al prefab de carretera compartido. La superficie tiene un pequeño solape de borde para evitar huecos; no cambiar la longitud lógica de los conectores.

Cada módulo contiene:

```text
Module
  Entry
  Exit
  Road                 Instancia de la carretera común
  Art
    Placeholder visual Arte reemplazable
```

## Reemplazar el arte

1. Abrir el prefab correspondiente de `Prefabs/Modules`.
2. Conservar `Module`, su componente, `Entry`, `Exit` y `Road`.
3. Dentro de `Art`, reemplazar `Placeholder visual` por el modelo definitivo o por otros prefabs visuales.
4. Colocar el arte según el contrato: comienza en Z = 0 y termina en Z = 12. Si el modelo se exportó centrado, su hijo visual puede desplazarse a Z = 6, como los placeholders actuales. No desplazar el módulo padre.
5. Evitar scripts jugables, colliders y decoración que invada el volumen libre. Obstáculos y recogidas los controla el juego.

No hace falta modificar código ni reemplazar objetos en cada escena. También se pueden crear módulos nuevos duplicando un wrapper existente y asignarlos en el array **Modules** del perfil del barrio. El orden de ese array define las variantes visuales; no cambia los patrones de monedas/obstáculos. El material de suelo se cambia en **Road Material** del perfil, sin tocar las medidas del módulo.

Los modelos de `Prefabs/Art` son referencias reemplazables, no una restricción de estilo. Los veredas despejadas de la transición conservan prefabs anidados de vehículos. El Obelisco es un landmark separado que se puede reemplazar desde el perfil.

## Editar el flujo

Los perfiles de `Routes/Definitions` contienen secciones con:

- **Map**: perfil visual del tramo.
- **Module Count**: cantidad de módulos de 12 m.
- **Transition**: habilita la mezcla ambiental y patrones de paso más simples.

El último tramo se repite indefinidamente; su cantidad se deja en cero. No usar cero en tramos intermedios. El generador prepara y recicla los módulos por distancia, con un pool por variante. El avance de escenario no carga otra escena Unity ni reinicia tiempo, puntos, velocidad, racha o sensores.

El flujo entregado es:

| Tramo | Módulos | Distancia |
|---|---:|---:|
| La Boca | 30 | 360 m |
| Calle arbolada | 16 | 192 m |
| Obelisco | Continuo | Hasta terminar la partida |

La transición usa salida azul/amarilla, calle junto a una plaza, árboles otoñales, veredas despejadas y aproximación a fachadas claras. Sus dieciséis módulos recorren tres variantes en un orden fijo; pueden reemplazarse desde `Transition.asset`. La niebla se mezcla durante el tramo. El Obelisco se acerca desde la distancia y queda como fondo visual del recorrido.

Los patrones que cruzan una frontera o se ubican en la transición conservan el carril de monedas libre y no exigen salto. Esta reducción es una regla explícita del recorrido; reemplazar modelos no altera esa regla.

## Escenas y actualización

`Scenes/CityRush.unity` sigue siendo la entrada al menú. En Configuración se elige Obelisco, La Boca o **La Boca → Obelisco** antes de empezar. La tercera opción utiliza `Maps/Routes/Scenes/LaBocaToObelisco.unity` y hace el cambio visual durante la partida.

Las escenas de juego son contenedores generados. Hacer el trabajo artístico en prefabs y perfiles, no colocar decoración definitiva directamente en esas escenas. `LOTBA > Update City Rush maps and UI` conserva los módulos y perfiles ya existentes, y reconstruye los contenedores y su vista previa de Editor. Las vistas previas tienen tag `EditorOnly`, se ocultan al iniciar y no se incluyen en un build.

En pausa se puede continuar, configurar sonido/dispositivos o volver al inicio. No se muestra el selector de recorrido durante una partida. Volver al inicio descarta la partida y reinicia su presentación sin pasar por el cartel de resultados.

## Entrega

Código importado/compilado y escenas guardadas. No se ejecutaron pruebas ni Play Mode, ni se generaron builds. La sensación de transición y el presupuesto visual quedan pendientes de la revisión manual del usuario. Los antiguos prefabs de mapa generados se retiraron de la carpeta compartida; su copia temporal está en `TEMP_Demo2/retired-map-prefabs`.
