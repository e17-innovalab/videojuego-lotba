# Obstáculos · datos, arte y catálogos por mapa

## Estructura

- `Definitions/`: huella jugable, prefab, regla de salto y aspecto de cada variante.
- `Sets/`: catálogos de obstáculos bajos y tráfico disponibles por mapa.
- `Prefabs/Obstacles/`: wrappers jugables con un hijo `Art`.
- `Prefabs/Art/`: visual compartido del bondi. Autos/conos/cajones reutilizan los modelos de `Prefabs`.

## Contrato para arte

El root del obstáculo tiene escala uno, orientación +Z y origen al nivel del suelo, centrado en X/Z. El modelo se coloca bajo `Art`. Las dimensiones se declaran en `RushObstacleDefinition` en metros: ancho, alto y largo. La detección utiliza esos datos; reemplazar el modelo no cambia automáticamente las reglas.

Mantener el componente `RushObstacleView` y el hijo `Art`. Reemplazar sus visuales, conservando el pivote. Para otra huella jugable, duplicar una definición y ajustar sus dimensiones y regla de salto. El prefab se puede compartir entre varias definiciones.

Los buses comparten `Shared bus.prefab` y `RushBusSkin`: paneles de carrocería, franjas y textos de número tienen referencias editables. Si se reemplaza su arte, reasignar esas referencias para conservar las variantes. El color se aplica con MaterialPropertyBlock, sin instanciar materiales por cada vehículo.

## Catálogos entregados

| Mapa | Tráfico |
|---|---|
| La Boca | Autos + bondi 46 azul/celeste |
| Obelisco | Autos + bondis 91 verde y 36 rojo |
| Transición | Autos; sin bondis largos |

Cada `RushMapDefinition` referencia su propio `RushObstacleSet`. El catálogo se elige por la posición donde aparecerá la fila, no únicamente por la posición actual del jugador. Los buses no se colocan cruzando fronteras de transición y no aparecen en la introducción más temprana.

Las filas principales de tráfico pueden priorizar vehículos largos cuando hay uno permitido. Dentro de cada grupo elegible la selección es uniforme; repetir referencias en el catálogo permite darles más peso.

Los bondis tienen 2,15 m de ancho, 3,25 m de alto y 8,8 m de largo. No se pueden saltar. Se comprueba el intervalo longitudinal completo y los cambios laterales del ciclista: entrar en el costado también puede provocar choque. Un mismo obstáculo no vuelve a cobrar el impacto después de haber golpeado al jugador. El vehículo continúa visible hasta pasar detrás de cámara.

La distancia de aparición contempla el frente del vehículo, para que agregar longitud no reduzca la anticipación prevista de la fila. Se mantienen carriles de salida y patrones más suaves alrededor de las transiciones.

## Cambiar obstáculos desde código

```csharp
[SerializeField] private RushWorld world;
[SerializeField] private RushObstacleSet eventTraffic;

// Cambia las próximas filas. Los obstáculos que ya están en pantalla siguen su recorrido.
world.SetObstacleOverride(eventTraffic);

// Vuelve a los catálogos de cada mapa.
world.SetObstacleOverride(null);
```

El cambio prepara los pools de definiciones nuevas una vez; no muta los assets de mapa ni de catálogo. Se debe invocar desde el hilo principal de Unity. Los catálogos y definiciones se pueden intercambiar por referencias serializadas, sin agregar nuevos casos al generador.

Para modificar únicamente la apariencia de una instancia de bondi:

```csharp
skin.Apply(new RushBusLivery("91", Color.green, Color.white));
```

Para una variante estable por mapa, usar una definición con esa livery dentro del catálogo. Identificadores actuales: `cone`, `crate`, `car_0`, `car_1`, `car_2`, `bus_46`, `bus_91`, `bus_36`. `RushObstacleSet.Find(id)` permite localizar una definición del catálogo.

## Contadores

- Monedas: valor acumulado de las recogidas, +10 cada una.
- Puntaje: +10 por multiplicador activo; los choques mantienen su penalización.
- Racha: número de recogidas consecutivas y barra de carga, independientes del valor del contador de monedas.
- Distancia: dato del recorrido; ya no suma puntos por sí sola.

El feedback muestra `+10` y, cuando corresponde, el cálculo del puntaje multiplicado en una línea secundaria. Los resultados incluyen valor acumulado y cantidad de monedas recogidas para distinguir ambos datos.

## Estado

Assets y escenas guardados; código compilado en Editor. No se ejecutaron pruebas, no se inició Play Mode y no se generó un build. El aspecto del bondi, la cámara y el balance quedan para la revisión manual del usuario.

