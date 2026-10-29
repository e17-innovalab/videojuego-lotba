# Frená a Tiempo · LOTBA

**Nombre provisional:** Frená a Tiempo. Se utiliza en el proyecto hasta que el equipo y LOTBA definan el nombre final. Vincula el recorrido en bicicleta con la decisión de detenerse, central en la experiencia educativa.

Videojuego educativo en desarrollo para **Lotería de la Ciudad de Buenos Aires (LOTBA)**, realizado por un equipo de estudiantes en el marco de **innova.lab**, vinculado a la Agencia de Habilidades para el Futuro.

El juego propone recorrer la ciudad en bicicleta y experimentar recompensas, pérdidas, presión por continuar y la posibilidad de frenar. Su propósito es abrir una conversación sobre mecanismos presentes en los juegos de apuestas a partir de situaciones vividas durante la partida.

**Estado:** prototipo iterativo. La propuesta, el arte, el balance y el alcance de la entrega se encuentran en definición. Este README se actualizará a medida que el equipo consolide el producto.

## Público y contexto de uso

El público principal son adolescentes de escuela secundaria. La experiencia está pensada para teléfonos y tablets, con posibles usos en visitas escolares y stands de eventos, acompañada por una conversación con personal de LOTBA o educadores.

El diseño debe contemplar diferencias de habilidad, lectura, dispositivos y conectividad. Las condiciones concretas de despliegue y los materiales de facilitación deben acordarse con el cliente. Mora es la referente de LOTBA que participó en la conversación de relevamiento utilizada como insumo del proyecto.

## Objetivo de producto

La consigna plantea un runner 3D urbano para una persona, con una experiencia principal de aproximadamente **5–8 minutos**, incluidas decisiones y pausas dentro del juego. Frenar voluntariamente puede terminarla antes.

El recorrido debe permitir reconocer y conversar sobre:

- La atracción de una recompensa inmediata y la conservación de una racha.
- La sensación de haber estado cerca de conseguir algo.
- Las decisiones de continuar o aceptar más riesgo después de una pérdida.
- El aumento progresivo de velocidad, desafío y presión por jugar «una más».
- La decisión de detenerse y lo ocurrido durante la partida.

**Frenar es una acción central:** debe permanecer visible y disponible, responder de inmediato y conducir a un cierre válido, sin confirmaciones ni penalizaciones adicionales por detenerse.

El cierre debe reducir la estimulación, recuperar decisiones reales de la sesión y hacer explícita la relación con los mecanismos de las apuestas, con una conexión a **Hablar es Ganar**. El juego funciona como actividad de apertura; no reemplaza el taller ni constituye una evaluación clínica de quien juega.

## Prototipo actual

El proyecto principal está en [`game/`](game/). Su implementación actual incluye movimiento por carriles, salto, obstáculos, monedas, rachas, multiplicador, aceleración progresiva, frenado voluntario, pausa y resultados.

Los recorridos disponibles son **Obelisco**, **La Boca** y **La Boca → Obelisco**, con una transición visual continua durante la partida. La interfaz contempla controles táctiles y teclado, sonido configurable, vibración en Android y control opcional por inclinación cuando existe hardware compatible.

La configuración actual de partida es de **120 segundos**. Esa duración sirve al prototipo y no equivale al objetivo de 5–8 minutos de la experiencia completa. Las decisiones de riesgo y recuperación, la situación de casi ganar, el registro de eventos y el cierre reflexivo personalizado necesitan consolidarse y validarse con el cliente.

La base técnica permite evaluar jugabilidad y presentación. No implica que se haya validado su efecto educativo ni aprobado una versión final por parte de LOTBA.

## Tecnología y documentación

Se utiliza **Unity**, con Android como plataforma prioritaria. El acceso web se mantiene como alternativa en evaluación para facilitar pruebas y distribución. No se requiere una cuenta ni un backend para jugar; la disponibilidad sin conexión y la compatibilidad final deben comprobarse en el entorno de entrega.

La documentación está en [`game/Assets/Docs/`](game/Assets/Docs/README.md), con dos recorridos de lectura: [desarrollo](game/Assets/Docs/Development/README.md), para entender el sistema y modificarlo, y [arte](game/Assets/Docs/Art/README.md), con procedimientos para reemplazar modelos, editar prefabs y crear zonas y transiciones.

| Carpeta | Contenido |
|---|---|
| [`game/`](game/) | Proyecto Unity principal |
| [`game/Assets/Docs/`](game/Assets/Docs/README.md) | Índice de guías de desarrollo y arte |
| [`desing/`](desing/) | Espacio de trabajo de diseño |
| [`gamedesing/`](gamedesing/) | Material de diseño de juego y experimentación |
| [`tester/`](tester/) | Material de pruebas y distribución al equipo |
| [`frontend/`](frontend/), [`backend/`](backend/) | Espacios reservados; su participación en la entrega está por definir |

## Criterios para iterar

Priorizar una experiencia comprensible, fluida y mantenible. Evaluar por separado la jugabilidad, el cumplimiento de la consigna y la capacidad de producir una conversación concreta. Las pruebas técnicas no permiten inferir motivaciones, adicción ni aprendizaje de una persona.

El prototipo utiliza recompensas metafóricas. No necesita dinero real, publicidad de apuestas ni datos personales. No debe forzar una pérdida para transmitir el mensaje ni interpretar frenar o continuar como una medida de la madurez del participante.

## Definiciones pendientes

- Alcance final del ciclo de decisiones y del cierre educativo.
- Duración y balance de la experiencia completa, y posible variante para stands.
- Dispositivos objetivo, acceso web, conectividad y forma de distribución.
- Arte y textos institucionales aprobados, créditos y responsabilidades del equipo.
- Criterios de aceptación y resultados de pruebas con participantes y facilitadores.
