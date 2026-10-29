# Obstáculos: datos, generación y colisiones

[Desarrollo](README.md) · [Mundo y recorridos](World.md) · [Reemplazo visual para artistas](../Art/Prefabs.md)

## Modelo de datos

| Tipo | Responsabilidad |
|---|---|
| `ObstacleDefinition` | ID, prefab, Width/Height/Length, Jumpable, Jump Clearance, Pool Capacity y livery |
| `ObstacleView` | Contenedor visual con hijo Art; aplica el aspecto cuando corresponde |
| `ObstacleSet` | Grupos Low y Traffic, selección elegible y búsqueda por ID |
| `BusLivery` | Número y colores de una variante de bondi |
| `BusSkin` | Asignación de colores y número a renderers y textos |

Assets en `Assets/_LOTBA_Game/Obstacles/`: `Definitions`, `Sets`, `Prefabs/Obstacles` y `Prefabs/Art`. El contrato de pivote, escala y reemplazo de modelos se explica en [Prefabs](../Art/Prefabs.md).

## Catálogos y pools

Cada MapDefinition referencia un ObstacleSet. El generador toma el catálogo por la distancia futura donde se coloca la fila; una oleada visible delante del ciclista puede pertenecer a otra zona. Los perfiles del recorrido se usan para preparar los pools antes de jugar.

La selección es uniforme entre candidatos elegibles. Repetir una referencia en un grupo aumenta su peso. Las filas principales pueden priorizar vehículos largos cuando estén permitidos; las filas suaves filtran longitud y dejan paso legible.

| Zona | Tráfico configurado |
|---|---|
| La Boca | Autos y bondi 46 azul/celeste |
| Obelisco | Autos y bondis 91 verde y 36 rojo |
| Transición | Autos, sin bondis largos |

El ID debe ser estable y único dentro del catálogo para que Find(id) sea inequívoco. La identidad de una instancia visual y su regla jugable son datos separados: varias definiciones pueden utilizar el mismo prefab.

## Impactos y salto

`RunnerWorld` comprueba el desplazamiento barrido del ciclista y del encuentro usando las dimensiones declaradas. El modelo y sus colliders no redefinen por sí solos esa huella. Para un vehículo largo se contempla el intervalo longitudinal completo; cambiar de carril hacia su costado también puede producir un impacto.

Jumpable determina si se puede superar por arriba y Jump Clearance establece el umbral correspondiente. Height describe el volumen del recurso, pero el modelo no decide esa regla. Un mismo obstáculo no cobra otro impacto tras haber golpeado al jugador; la sesión también aplica su ventana de protección global.

La aparición contempla el frente del vehículo para conservar la anticipación. La instancia continúa visible mientras su longitud pasa detrás de cámara. Revisar esos aspectos al introducir otra huella, especialmente si cambia la longitud.

## Cambiar el catálogo durante una partida

```csharp
using Lotba.Game;
using UnityEngine;

public sealed class TrafficEvent : MonoBehaviour
{
    [SerializeField] private RunnerWorld world;
    [SerializeField] private ObstacleSet eventTraffic;

    public void BeginEvent() => world.SetObstacleOverride(eventTraffic);
    public void EndEvent() => world.SetObstacleOverride(null);
}
```

El override afecta las próximas filas. Los obstáculos que ya están en pantalla siguen su recorrido. Pasar null restaura los catálogos por mapa. El cambio prepara una vez los pools de definiciones nuevas; no modifica los assets de mapa o catálogo. Invocar desde el hilo principal y con RunnerWorld ya inicializado.

Para localizar una definición, usar `ObstacleSet.Find(id)`. IDs configurados: `cone`, `crate`, `car_0`, `car_1`, `car_2`, `bus_46`, `bus_91`, `bus_36`.

## Apariencia de bondis

`BusSkin.Apply(new BusLivery("91", Color.green, Color.white))` cambia una instancia. Para una variante estable del catálogo, configurar la livery en su ObstacleDefinition. Una instancia reutilizada recibe la configuración de su definición.

Los colores utilizan MaterialPropertyBlock; no se crean materiales individuales por vehículo. Los nuevos shaders deben admitir las propiedades de color utilizadas por BusSkin. Si el artista cambia las piezas, debe reasignar Body Panels, Stripe Panels y Numbers en ese componente.

## Incorporar otro obstáculo

1. Preparar el prefab según [el contrato visual](../Art/Prefabs.md).
2. Crear o duplicar una definición desde **Create > LOTBA > Obstacles > Obstacle definition**.
3. Asignar ID, prefab, dimensiones, regla de salto y capacidad de pool.
4. Incluirla en Low o Traffic del catálogo adecuado; los catálogos se crean con **Create > LOTBA > Obstacles > Obstacle set**.
5. Acordar con diseño su lectura, anticipación y elegibilidad en tramos suaves. Revisar el encuentro frontal, el lateral y el paso después de un impacto.

No es necesario agregar un caso por modelo al generador para cambiar un prefab o catálogo. Si se introduce una regla nueva, modificar la lógica de RunnerWorld y documentarla junto con el contrato. Las reglas de puntuación y pérdida pertenecen a [RunSession](Gameplay.md).
