Martes 27 de octubre: 
se prueba apk en celular Android marca xiaomi redmi note 10. 

cumple:
modalidad single player
toma de decisiones 

martes 03 de noviembre
hacer las pruebas en el Android

---

## Martes 10 de noviembre: Ejecución de Pruebas Build v0.2.0

* **Tester:** 
* **Dispositivo:** Android Móvil y Tablet
* **Documentación adjunta:** [Suite Maestra de QA v0.2.0 (Google Sheets)](https://docs.google.com/spreadsheets/d/1CAu4CTbXzIgwCQKHcNVhDnawscmtivq4/edit?usp=sharing&ouid=102848154205434914620&rtpof=true&sd=true)

### Resumen de Pruebas y Mecánicas Validadas

* **Avance y Velocidad:**
  * Curva de velocidad confirmada: inicia en 65 km/h de base y escala progresivamente hasta el tope de 100 km/h al final.
  * Al colisionar, el vehículo desacelera levemente unos pocos km/h con vibración física, pero recupera la inercia en 1 segundo y continúa empujando hacia los 100 km/h.
* **Mecánica de Palabras:**
  * Se generan aleatoriamente 2 palabras contextuales por partida; ambas son recolectables y se logran completar a lo largo de la pista.
* **Colisiones y Físicas:**
  * Ventana de inmunidad (*I-Frames*) confirmada: tras chocar contra un objeto (ej. caja), otorga unos segundos de gracia donde los obstáculos pegados detrás (ej. conos) no generan daño ni penalización doble.
* **Flujo y Cierre:**
  * Botón FRENAR verificado: detención instantánea en 0 segundos y pantalla de cierre con el mensaje: *"Vos marcaste el final"*.
  * Menú de pausa verificado: congelamiento de pantalla y opciones operativas (*Continuar*, *Configuración* y *Volver al inicio*).

### Estado de Defectos (Bugs)

* **`DEF-001` [RESUELTO]:** En v0.2.0 el texto de recolección (+10) fue reubicado en el HUD superior, liberando la visual central de la pista.
* **`DEF-002` [ABIERTO]:** Al intentar saltar autos particulares, la caja de colisión aérea registra impacto en el aire y descuenta monedas.
* **`DEF-003` [ABIERTO]:** Inconsistencia de respuesta háptica en determinados dispositivos y tablets Android.
* **`DEF-004` [ABIERTO]:** Desincronización en HUD: al alcanzar Racha 4, el multiplicador visual permanece estancado en x1.
