# ByteTrainer

<p align="center">
  <img src="assets/icon.png" width="84" height="84" alt="">
</p>

<p align="center"><strong>Un centro de trainers para juegos de un solo jugador de Steam en Windows</strong></p>

<p align="center">
  <a href="README.md">English</a> · <a href="README.pt-BR.md">Português (Brasil)</a> · <strong>Español</strong>
</p>

<p align="center">
  <a href="https://github.com/ByteTrainer/trainer-hub/releases/latest"><strong>Descargar para Windows</strong></a> ·
  <a href="https://bytetrainer.github.io/trainer-integrations/es/">Sitio oficial</a> ·
  <a href="https://discord.gg/pMSJayXFmj">Discord</a>
</p>

## Funciones

- **Detección de la biblioteca de Steam**, con búsqueda en el catálogo, favoritos y una carpeta de instalación configurable por juego.
- **Superposición dentro del juego** con los mismos controles que la aplicación. Actívala por juego y ábrela con <kbd>Alt</kbd> + <kbd>W</kbd> de forma predeterminada en modo ventana o sin bordes.
- **Atajos personalizados y perfiles de trucos** guardados por juego. Un perfil predeterminado puede activar los controles y aplicar los multiplicadores guardados al empezar una sesión. Las acciones puntuales, como fijar el saldo de monedas, se aplican manualmente.
- **Avisos personalizables** con cuatro estilos de sonido, volumen ajustable y opción de silenciar. La aplicación y la superposición pueden seguir el tema de Windows o usar el tema claro u oscuro.
- **Tres idiomas** en la aplicación, la superposición y el sitio. Elige entre inglés, portugués de Brasil y español.
- **Actualizaciones independientes** para la aplicación y las integraciones. ByteTrainer consulta GitHub Releases para actualizar la aplicación y utiliza un catálogo firmado en GitHub Pages para encontrar los paquetes de los juegos.

## Juegos compatibles

La página de cada juego muestra los controles disponibles y ofrece el paquete de integración firmado más reciente. La compatibilidad depende de la versión instalada desde Steam. [Ver el catálogo actualizado](https://bytetrainer.github.io/trainer-integrations/es/catalogo.html)

| Juego | Ejemplos de controles |
| --- | --- |
| [Dungeon of the Endless](https://bytetrainer.github.io/trainer-integrations/es/jogos/249050.html) | Vida ilimitada, ajuste de recursos, investigación instantánea y habilidades activas |
| [Kawaii Deathu Desu](https://bytetrainer.github.io/trainer-integrations/es/jogos/1102880.html) | Vida ilimitada, fijar almas, especial ilimitado |
| [Last Hope Bunker: Zombie Survival](https://bytetrainer.github.io/trainer-integrations/es/jogos/2475440.html) | Filtro ilimitado, munición infinita, sin recarga, modo sigilo |
| [Moventure](https://bytetrainer.github.io/trainer-integrations/es/jogos/3826060.html) | Invencibilidad, saltos ilimitados, oxígeno ilimitado bajo el agua |
| [My Friend Pedro](https://bytetrainer.github.io/trainer-integrations/es/jogos/557340.html) | Concentración ilimitada, combo fijo, rango S, congelar el cronómetro del nivel |
| [My Little Universe](https://bytetrainer.github.io/trainer-integrations/es/jogos/2328750.html) | Ajuste de recursos, refinamiento instantáneo, rastreadores de cristales y de expansión del mapa |
| [Out of Space](https://bytetrainer.github.io/trainer-integrations/es/jogos/400080.html) | Resistencia ilimitada, fijar monedas, cubo de agua infinito, impedir la eclosión |
| [Pigeons Attack](https://bytetrainer.github.io/trainer-integrations/es/jogos/814300.html) | vida, munición y especial ilimitados, velocidad del juego, muerte de un golpe, multiplicadores de daño y recompensas |
| [Tooth and Tail](https://bytetrainer.github.io/trainer-integrations/es/jogos/286000.html) | Vida y comida ilimitadas, tropas instantáneas, construcción gratis e instantánea, multiplicadores de movimiento y daño |
| [Cooking Live: Restaurant Game](https://bytetrainer.github.io/trainer-integrations/es/jogos/1990690.html) | Fijar diamantes y dinero, suministros ilimitados, cocción instantánea, comida sin quemar, multiplicador de monedas, clientes pacientes, combos sin caducidad y finalización rápida del nivel |
| [Pumpkin Jack](https://bytetrainer.github.io/trainer-integrations/es/jogos/1186640.html) | Saltos ilimitados, velocidad de movimiento, recarga instantánea del cuervo, rastreador de coleccionables |
| [Shake it Co!](https://bytetrainer.github.io/trainer-integrations/es/jogos/3578070.html) | Pesca fácil, sacudir el árbol automáticamente, cofres perfectos, venta automática |
| [Sol Cesto](https://bytetrainer.github.io/trainer-integrations/es/jogos/2738490.html) | Fijar oro, usos ilimitados de objetos, inmunidad al veneno, ampliar inventario |
| [Tinykin](https://bytetrainer.github.io/trainer-integrations/es/jogos/1599020.html) | Planeo ilimitado, rastreadores de misiones, coleccionables y carreras |

## Primeros pasos

1. Descarga y ejecuta el instalador para Windows. [Última versión de la aplicación](https://github.com/ByteTrainer/trainer-hub/releases/latest)
2. Abre la página del juego en ByteTrainer. Si todavía no aparece, entra en **Configuración → Actualizaciones de las integraciones → Verificar en línea** y selecciona **Descargar paquete**.
3. Con el juego cerrado, elige **Instalar integración** o **Actualizar integración** en su página. Desde allí también puedes indicar la carpeta de instalación.
4. Inicia el juego desde Steam, entra en una partida individual y selecciona **Conectar trainer**. Activa los controles desde la aplicación, los atajos o la superposición.

**Uso exclusivo en partidas de un solo jugador.** Si un juego también ofrece multijugador, usa los trainers en el modo individual.

## Compatibilidad y actualizaciones

ByteTrainer comprueba los archivos relevantes antes de instalar. Una actualización de Steam puede requerir una revisión compatible de la integración. Con el juego cerrado, elige **Restaurar original** en su página para recuperar los archivos guardados durante la instalación.

La aplicación verifica la firma de cada paquete `.thub` antes de aceptarlo. Los paquetes pueden añadir o actualizar controles sin depender de una nueva versión de la aplicación para Windows. Para buscar actualizaciones de forma manual, abre **Configuración → Actualizaciones de ByteTrainer**. Las notas de la versión están disponibles desde el número de versión de la aplicación y en GitHub. [Versiones de la aplicación](https://github.com/ByteTrainer/trainer-hub/releases) · [Repositorio de integraciones](https://github.com/ByteTrainer/trainer-integrations)

## Cómo contribuir

- **Solicita un juego o informa de un problema con un trainer** en el Discord de la comunidad. La pestaña **Juegos confirmados** de la aplicación muestra las integraciones previstas. [Únete a Discord](https://discord.gg/pMSJayXFmj)
- **Mejora o añade una traducción** con los catálogos compartidos y los archivos específicos de cada juego en `locales/`. [Guía de traducción](locales/README.md)
- **Colabora con las integraciones** siguiendo el proceso de comprobación de versiones, instalación y publicación. [Guía de integraciones](INTEGRATIONS.md)

<details>
<summary>Compilar desde el código fuente</summary>

En Windows, usa Node.js 22.12 o posterior y npm.

```powershell
npm ci
npm run build
npm start
```

Ejecuta `npm test` y `npm run translations:check` antes de enviar cambios. Usa `npm run dist` para crear el instalador de Windows.

| Carpeta | Finalidad |
| --- | --- |
| `src/` | Interfaz de la aplicación y la superposición |
| `electron/` | Detección de Steam, servicios de escritorio y conexión de trainers |
| `native/` y `resources/` | Código de las integraciones y archivos de los paquetes |
| `locales/` | Traducciones compartidas y específicas de cada juego |
| `scripts/` | Herramientas de compilación, validación y publicación |

</details>
