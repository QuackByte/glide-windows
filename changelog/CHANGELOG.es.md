# Registro de cambios

Novedades de cada versión de Glide para Windows. Glide muestra las entradas nuevas una sola vez tras actualizarse, y las notas de cada versión en GitHub salen de aquí. Esta es una traducción de CHANGELOG.md, que es el original.

## 0.10.2 — 2026-10-19

### Correcciones

- **En la disposición en mosaico, Win+flecha arriba y Win+flecha abajo cambian primero de pestaña dentro de un grupo de pestañas y luego pasan al mosaico en esa dirección.** Cuando no quedan más pestañas en esa dirección, pasan al mosaico que hay en ella.
- **En la disposición en mosaico, los mosaicos junto a una app con anchura mínima se encogen para caber en ella, en lugar de superponerse.** Glide aprende la anchura mínima de cada app, y cuando un cambio de tamaño se detiene en esa anchura, un mensaje explica por qué.

## 0.10.1 — 2026-10-19

### Correcciones

- **En la disposición en mosaico, Win+[ y Win+] crean grupos de pestañas.** Win+[ añade la ventana enfocada a un grupo de pestañas, una columna de pestañas, y Win+] saca una pestaña a su propio mosaico, como ya hacían al desplazarse. Arrastrar una ventana a una barra de pestañas funciona como antes.

## 0.10.0 — 2026-10-19

### Novedades

- **Mosaico ahora es una disposición en espiral.** Win+Ctrl+T cambia un escritorio a una disposición en la que todas las ventanas caben en pantalla: una ventana la llena, dos la comparten como mitades, y desde la tercera la última columna vuelve a dividirse, alternando arriba y abajo con izquierda y derecha. Usa 2 columnas, o 3 con el nuevo Win+Ctrl+G, que es también lo que Glide elige en monitores ultrapanorámicos. Al volver a Desplazable, tus ventanas conservan el mismo orden.
- **Muévete por un escritorio en mosaico según la dirección.** Win+flechas enfocan el mosaico contiguo al enfocado, y Win+Shift+flechas lo intercambian con ese vecino. Cambiar el ancho (Win+Ctrl+R) modifica cómo el mosaico enfocado reparte el espacio con los que van después, y Win+Ctrl+F amplía un mosaico para llenar la pantalla hasta que lo pulses de nuevo.
- **Elige cuántas columnas usan los escritorios en mosaico** en Configuración › General › Columnas en mosaico: Automático (3 en monitores ultrapanorámicos, si no 2), 2 o 3.

### Cambios

- **Enviar una ventana a otro escritorio mueve solo esa ventana.** Win+Shift+1…9 y Win+Ctrl+Shift+←/→ antes se llevaban toda la columna; ahora solo va la ventana enfocada, como una columna propia, y tú te quedas en el escritorio donde estás.

### Correcciones

- **Portugués de Brasil en el ajuste Disposición.** Su descripción en Configuración › General ahora está en portugués de Brasil («tela», «você») en lugar de europeo.

## 0.9.3 — 2026-10-19

### Novedades

- **Disposición en mosaico.** Win+Ctrl+T cambia el escritorio actual entre la tira desplazable y una disposición en mosaico, donde todas las columnas comparten la pantalla y caben a la vez, sin desplazarte. Cada escritorio conserva su propia disposición, incluso después de reiniciar Glide. Cambiar el ancho de una columna (Win+Ctrl+R) le da una parte mayor o menor, arrastrar su borde la redimensiona, y Win+Ctrl+F sigue poniéndola a ancho completo hasta que lo pulses de nuevo.
- **Elige la disposición con la que empiezan los escritorios nuevos** en Configuración › General › Disposición: Desplazable, como hasta ahora, o Mosaico. Los escritorios que hayas cambiado con Win+Ctrl+T conservan la suya.

## 0.9.2 — 2026-10-16

### Novedades

- **Novedades también habla tu idioma.** Las notas que Glide muestra tras una actualización, y en Novedades, ahora están en el mismo idioma que el resto de Glide.

## 0.9.1 — 2026-10-16

### Novedades

- **Glide habla tu idioma.** Glide ya está disponible en francés, alemán, japonés, chino simplificado, coreano y portugués de Brasil, además de inglés y español, instalador incluido. Sigue el idioma de visualización de Windows, o elige uno en Configuración › General › Idioma.

## 0.9.0 — 2026-10-15

### Novedades

- **Mira dónde caerá una ventana que arrastras.** Mientras arrastras una ventana, un contorno del color del borde de foco muestra la columna a la que se uniría, con su barra de pestañas incluida. Sobre otro monitor, una barra indica dónde iría entre las columnas de ese monitor. Si no hay nada resaltado, volverá a su sitio.
- **Un menú de la bandeja más completo.** Muestra los escritorios en uso con el número de ventanas de cada uno; elige uno para ir a él. En **Atajos** está cada acción con su atajo, para ejecutarla desde el menú. Cuando “Sustituir atajos de Windows” está desactivado, el menú también muestra los atajos que conserva Windows.
- **Todos los atajos en una sola página.** Win+Ctrl+/ (la tecla con ? en un teclado estadounidense) los muestra todos; púlsalo otra vez o pulsa Esc para cerrarla. También está en el menú de la bandeja, en Atajos.
- **Informar de un problema**, en el menú de la bandeja y en Configuración › General. Glide guarda su registro y su configuración en un zip, sin los títulos de las ventanas, y abre una incidencia nueva en GitHub para que describas lo ocurrido y adjuntes el zip. No se envía nada por sí solo.
- **Glide habla español.** Si Windows está en español, Glide también, instalador incluido. Novedades sigue en inglés.

### Correcciones

- **La pantalla completa sigue siendo pantalla completa.** Un vídeo, un juego o F11 en un navegador ya no vuelve a su columna a la fuerza. Glide lo deja en paz, oculta sobre él el borde de foco, la atenuación y la barra de pestañas, y deja que la rueda horizontal y los gestos del panel táctil le lleguen.
- Si cambias a otro de los escritorios virtuales de Windows y vuelves, tus columnas conservan el mismo orden.
- Al cerrar una ventana, el foco ya no se aparta de las ventanas del propio Glide, como Novedades o Configuración.

## 0.8.1 — 2026-10-14

### Correcciones

- Al hacer clic en **Continuar** en la ventana de novedades, Glide se cerraba. Ahora, al cerrar una ventana, Glide sigue en marcha.
- El icono de la bandeja muestra todos los escritorios en uso: una cuadrícula de 3×3 con los nueve escritorios, los que están en uso iluminados y el activo más brillante. Si solo hay uno en uso, muestra su número como antes.

## 0.8.0 — 2026-10-14

### Novedades

- **Atenúa las ventanas sin foco.** Las ventanas que están detrás de la que tiene el foco en su monitor se oscurecen, para que esta destaque. Viene desactivado; actívalo, y ajusta cuánto oscurece, en Configuración › Apariencia.
- **Se listan todos los escritorios en uso.** La información emergente y el menú del icono de la bandeja muestran todos los escritorios que tienen ventanas. El icono sigue mostrando el activo.
- **Los números de escritorio en pantalla al cambiar** se pueden desactivar en Configuración › Apariencia.
- **Instalar las actualizaciones automáticamente.** Viene desactivado. Actívalo en Configuración › Actualizaciones y Glide instalará una versión nueva en cuanto la encuentre, y luego volverá a abrirse.

### Cambios

- Las versiones aún no están firmadas con código, así que Windows SmartScreen puede mostrar un aviso la primera vez que ejecutes Glide: elige **Más información › Ejecutar de todas formas**.
- La política de privacidad del instalador ya no enlaza con una política de firma de código.

## 0.7.0 — 2026-10-14

### Novedades

- **Glide se mantiene al día solo.** Dos veces al día busca una versión nueva y te la ofrece desde el icono de la bandeja. Cada descarga se comprueba antes de instalarse, y Glide devuelve tus ventanas a su sitio, se actualiza y vuelve a abrirse. Desactívalo en Configuración › Actualizaciones, o durante la instalación.
- **Novedades.** Tras una actualización, Glide te muestra qué ha cambiado, como ahora. Vuelve a encontrarlo en el menú de la bandeja.
- **Las apps vuelven donde las tienes.** Glide recuerda el monitor, el escritorio, el ancho de columna y el lugar en la tira de cada app. La ventana de una app que vuelves a abrir aparece allí, y tras un reinicio tus ventanas abiertas se organizan igual. Desactívalo, u olvida todo, en Configuración › General.
- **Glide es de código abierto**, con licencia MIT: [github.com/QuackByte/glide-windows](https://github.com/QuackByte/glide-windows).

### Cambios

- El instalador muestra la política de privacidad de Glide y te deja elegir si Glide busca actualizaciones.

## 0.6.0 — 2026-10-14

### Novedades

- **Iconos de las apps** junto a los títulos de las ventanas en la vista general y en las barras de pestañas.
- **La vista general se desplaza** cuando una tira es demasiado larga para la pantalla, en lugar de encoger los mosaicos hasta que no se pueden leer. La rueda desplaza la fila que está bajo el puntero.

### Correcciones

- Las barras de pestañas se deslizan con su columna en lugar de adelantarse a ella.
- Tras apilar o desapilar, el borde de foco podía aparecer donde estaba antes la ventana. Ahora sigue a la ventana.

## 0.5.0 — 2026-10-14

### Novedades

- **Varios monitores**: cada monitor tiene su propia tira y sus nueve escritorios. Win+Alt+←/→ mueven el foco entre monitores; Win+Alt+Shift+←/→ envían allí la columna enfocada.
- **Arrastrar y soltar**: suelta una ventana sobre una barra de pestañas para añadirla a las pestañas, sobre otra ventana para apilarla con ella, o en otro monitor para moverla.
- **Un borde de foco para todas las apps**, también las de Electron, con un grosor que puedes ajustar.
- **Los gestos de tres dedos en el panel táctil** recorren la tira.
- “Abrir Glide al iniciar sesión” en el menú de la bandeja.

## 0.4.1 — 2026-10-14

La primera versión de Glide para Windows.
