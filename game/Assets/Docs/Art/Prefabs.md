# Reemplazar arte y editar prefabs

[Inicio de arte](README.md) · [Zonas y transiciones](ZonesAndTransitions.md) · [Crear mapas](NewMaps.md)

## 1. Elegir qué se va a cambiar

Antes de editar, decidí si el cambio debe afectar a todos los lugares que usan ese recurso o solamente a una variante.

- **Cambio compartido:** editar el prefab o material original actualiza sus usos.
- **Variante independiente:** duplicar el recurso desde Project y usar el duplicado en el módulo elegido.

Duplicar un módulo conserva sus referencias a modelos y materiales compartidos. Si después editás uno de esos originales, también cambiarán los otros módulos que lo usan. Para independizar el aspecto, duplicá el prefab visual o material correspondiente y reasignalo.

Mover o renombrar archivos desde Project conserva sus referencias. Incluir los `.meta` al compartir el trabajo con el equipo.

## 2. Reemplazar edificios o decoración de un módulo

1. En Project, buscá `Maps/LaBoca/Prefabs/Modules` o `Maps/Obelisco/Prefabs/Modules`.
2. Abrí un módulo con doble clic. Verificá que estás en Prefab Mode.
3. Buscá esta estructura:

   ```text
   Module
     Entry
     Exit
     Road
     Art
       Placeholder visual
   ```

4. Conservá el objeto principal, el componente `RoadModule`, `Entry`, `Exit`, `Road` y el contenedor `Art`.
5. Arrastrá el modelo nuevo desde Project dentro de `Art`. Colocalo y ajustá su tamaño desde ese hijo, sin mover ni escalar el módulo principal.
6. Quitá el visual que estás reemplazando. Si pertenece a un prefab anidado, abrí ese prefab para editar sus objetos internos; no desempaquetes el módulo completo para resolverlo.
7. Comprobá las medidas de la tabla siguiente y guardá el prefab.

Si el visual nuevo se exportó centrado longitudinalmente, puede necesitar una posición local Z = 6 para ocupar el tramo de Z = 0 a Z = 12. Si se exportó desde el inicio del tramo, puede quedar en Z = 0. Lo que importa es dónde queda la geometría dentro del módulo.

### Medidas del módulo

| Elemento | Medida o posición |
|---|---|
| Unidad | 1 unidad de Unity = 1 metro |
| Orientación | +Z hacia adelante; +Y hacia arriba |
| Objeto principal | Posición local (0, 0, 0), rotación (0, 0, 0), escala (1, 1, 1) |
| Entrada `Entry` | (0, 0, 0) |
| Salida `Exit` | (0, 0, 12) |
| Longitud | 12 m |
| Calzada | 9 m de ancho: desde X = −4,5 hasta X = +4,5 |
| Centros de carril | X = −2,7; 0; +2,7 |
| Superficie transitable | Y = 0 |
| Volumen libre sobre la calzada | Hasta Y = 4,2 m |

Al seleccionar el objeto con `RoadModule`, el gizmo muestra el espacio que debe quedar libre. Si no se ve, activar **Gizmos** en Scene.

`Entry` y `Exit` sirven como referencias de montaje: moverlos no hace que el sistema calcule otra longitud. La repetición sigue usando 12 metros. Edificios, ramas, carteles y balcones deben respetar el espacio libre. Los vehículos que se esquivan son obstáculos separados, no decoración colocada sobre los carriles.

## 3. Cambiar materiales o el suelo

1. Importá el modelo y sus texturas dentro de la carpeta de arte de la zona. Comprobá su escala y orientación antes de incorporarlo al módulo.
2. Usá materiales compatibles con **Built-in**, el pipeline del proyecto. Un material de otro pipeline puede verse rosa aunque el modelo esté bien importado.
3. Para un cambio exclusivo, duplicá el material y asignalo al visual correspondiente.
4. Para cambiar la calzada de una zona, seleccioná su ficha `MapDefinition` y asigná **Road Material**. Ese campo reemplaza el material de la superficie durante la generación.

`Road base.prefab` es compartido: cambiar su geometría afecta los módulos que lo usan. Para variar edificios o vegetación, trabajar dentro de `Art`. Para variar la superficie por barrio, usar **Road Material**.

## 4. Reemplazar un obstáculo

1. Abrí un prefab de `Obstacles/Prefabs/Obstacles`.
2. Conservá su objeto principal, el componente `ObstacleView` y el hijo `Art`.
3. Reemplazá el visual dentro de `Art`. Ajustá solamente ese hijo.
4. Ubicá el centro del obstáculo en X/Z = 0 y su base en Y = 0. El frente debe mirar hacia +Z. El objeto principal mantiene escala uno.
5. Buscá la ficha que lo utiliza en `Obstacles/Definitions` y compará **Width**, **Height** y **Length** con el modelo.
6. Si el modelo ocupa otro espacio, coordiná el cambio de dimensiones y de **Jumpable / Jump Clearance** con diseño o desarrollo.

El juego comprueba los impactos con esas dimensiones. Agregar o cambiar un collider en el modelo no actualiza automáticamente su espacio jugable. Un modelo más grande con una ficha más chica puede atravesar visualmente al ciclista.

### Bondis

Los bondis comparten el modelo `Obstacles/Prefabs/Art/Shared bus.prefab`. Las fichas definen número y colores. Un cambio visual compartido puede afectar a las líneas 46, 91 y 36.

Si reemplazás sus piezas, actualizá en `BusSkin` las listas **Body Panels**, **Stripe Panels** y **Numbers**. Si quedan apuntando a objetos eliminados, la personalización no tendrá dónde aplicarse. Las variantes actuales miden 2,15 × 3,25 × 8,8 m y no se pueden saltar.

## 5. Ciclista, monedas e interfaz

`Prefabs/Cyclist.prefab` conserva referencias de ruedas y pedales que el juego anima por código. Para un reemplazo visual simple, mantener esos puntos de montaje y ajustar sus hijos. Si el personaje necesita otro rig, otra jerarquía o animaciones propias, coordinar la integración con desarrollo; importar el modelo no conecta esas animaciones automáticamente.

`Coin.prefab` y `Pickup feedback.prefab` también son compartidos. Mantener sus componentes y el origen previsto al cambiar su aspecto: el juego controla su aparición, recogida y reciclado.

En `Game UI.prefab`, conservar botones, textos, componentes y referencias. Los cambios de jerarquía, posiciones y tamaños deben coordinarse con desarrollo porque `GameUiSetup` configura parte de esa estructura. Las fuentes deben seguir funcionando con TextMesh Pro y mostrar los acentos utilizados por el juego.

## 6. Revisar el montaje antes de entregar

Para mirar una unión sin modificar la escena de juego, creá una escena de revisión fuera de la lista de escenas del build. Arrastrá dos o tres módulos y colocalos en Z = 0, 12 y 24, con X/Y = 0 y escala uno.

Revisá desde Scene que el suelo no tenga huecos, que se respeten los carriles, que no haya objetos cortados o duplicados en los bordes y que no aparezcan superficies superpuestas parpadeando. Para observar lectura y escala durante el movimiento, el equipo debe revisar también el recorrido desde la cámara del juego.

Entregá el prefab, modelos, materiales y texturas necesarios, junto con sus `.meta`, e indicá qué ficha de zona u obstáculo debe usarlos. Para modificar un conjunto visual sin afectar otros usos, entregar sus variantes independientes.
