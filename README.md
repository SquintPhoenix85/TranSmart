# TranSmart — Cobertura de redes de transporte (corredor norte de Barranquilla)

**Proyecto:** Matemáticas Discretas (MAT 4021), Universidad del Norte  

**Integrantes:** Orlando Rafael Arroyo Escalante (200220002) y Mariana Serrato Mejía (200220124)

**Objetivo:** estudiar una heurística voraz de cubrimiento de conjuntos sobre una red peatonal real de OpenStreetMap y ofrecer una aplicación de escritorio para analizar cobertura y buscar cajeros automáticos cercanos.

## Información del proyecto

TranSmart representa la red peatonal simplificada de los alrededores de la Universidad del Norte y el corredor norte de Barranquilla como un grafo ponderado. En el modo de cubrimiento, cada vértice del grafo representa un punto de demanda con el mismo peso; no representa una persona. La aplicación selecciona vértices elegibles para cubrir los demás puntos dentro de una distancia configurable por la red peatonal.

El caso académico estudia posibles ubicaciones para puntos de acceso, recarga o transferencia del transporte. Los vértices seleccionados son ubicaciones del modelo; la aplicación no verifica servicios ni instalaciones de transporte existentes.

El modo de cajeros permite descargar puntos etiquetados como cajeros en OpenStreetMap, escoger un punto de partida en el mapa y consultar hasta diez cajeros cercanos, ordenados por distancia estimada a pie.

El área de estudio usa este rectángulo geográfico:

| Límite | Coordenada |
| --- | --- |
| Norte | 11.025° N |
| Sur | 10.995° N |
| Oeste | 74.835° O |
| Este | 74.810° O |

### Modelo del grafo y algoritmo

- **Vértices de demanda (`V`):** todos los nodos de la red peatonal incluida en los datos; todos cuentan por igual.
- **Sitios candidatos (`C ⊆ V`):** nodos con al menos tres vecinos distintos de entrada o salida en la red extraída. Esta regla sirve como aproximación reproducible a una intersección.
- **Aristas (`E`):** segmentos peatonales de OpenStreetMap con longitud en metros. El cargador interpreta la red como no dirigida y, si hay varias aristas entre los mismos extremos, conserva la de menor longitud.
- **Distancia:** longitud del camino más corto por la red; no es distancia en línea recta.
- **Heurística:** en cada paso se escoge el candidato que cubre más vértices aún pendientes. En caso de empate, se elige el nodo con menor identificador de OpenStreetMap. El proceso termina cuando cubre todos los vértices o ningún candidato restante aporta cobertura nueva.

La heurística intenta cubrir todos los vértices con el radio elegido, pero no garantiza una solución mínima. Si quedan vértices sin cubrir, la aplicación informa que la instancia no es factible para ese radio.

El porcentaje de cubrimiento es el número de vértices de demanda cubiertos dividido entre el total de vértices de demanda. La reducción de candidatos es el número de candidatos elegibles que no se seleccionaron dividido entre el total de candidatos elegibles. Estas métricas no representan cobertura de población ni confirman que un lugar sea una parada de transporte o un acceso peatonal verificado.

## Requisitos del sistema

### Para ejecutar la aplicación

- Windows compatible con WPF y **.NET 10 Desktop Runtime**.
- **Microsoft Edge WebView2 Runtime** para mostrar el mapa interactivo integrado. Si WebView2 no está disponible, aún se puede ejecutar la optimización, pero el mapa no se iniciará.
- La conexión a internet es opcional para optimizar con el grafo incluido. Se necesita para cargar los mosaicos del mapa base de OpenStreetMap.

### Para compilar el proyecto

- **.NET 10 SDK** en Windows.
- Puede requerirse internet durante la primera compilación para restaurar el paquete NuGet `Microsoft.Web.WebView2`.

### Para generar o actualizar los datos

- **Python 3** con `osmnx` y `pandas` en el mismo entorno de Python.
- Conexión a internet para descargar la red peatonal y los datos de cajeros de OpenStreetMap.

Para instalar las dependencias de extracción desde una terminal:

```powershell
py -3 -m pip install osmnx pandas
```

Si el comando `py` no está disponible, usa `python -m pip install osmnx pandas`. La aplicación busca primero la ruta configurada en `TRANSMART_PYTHON` y después intenta `py -3`, `python` y `python3`. Si Python está instalado en otra ubicación, configura `TRANSMART_PYTHON` con la ruta al ejecutable.

No hace falta Python para usar el grafo incluido en el análisis de cubrimiento.

## Iniciar la aplicación

Desde la carpeta principal del repositorio, ejecuta:

```powershell
dotnet run --project TranSmart/TranSmart.csproj
```

En el primer inicio, la aplicación copia `TranSmart/amb_graph.json` a `%LOCALAPPDATA%\TranSmart\amb_graph.json` si todavía no existe allí. En los siguientes inicios usa la copia de datos de la aplicación. Si el archivo falta o no se puede cargar, selecciona **Generate graph** cuando aparezca esa opción. La aplicación descargará una nueva red y los datos de cajeros y luego los cargará automáticamente.

## Instrucciones de uso

### Analizar el cubrimiento de la red

1. Inicia la aplicación y selecciona **Optimizar cobertura**.
2. Ajusta el radio peatonal entre 50 y 500 metros; el valor inicial es 500 metros.
3. Pulsa **Ejecutar optimización**.
4. Consulta las tarjetas de resultados y el mapa: candidatos elegibles, sitios seleccionados, vértices cubiertos, cubrimiento, reducción de candidatos, radio resuelto y tiempo de ejecución.
5. Usa los controles de reproducción —iniciar, anterior, reproducir/pausar, siguiente y finalizar—, el deslizador o una entrada del historial para revisar cada decisión. Cada paso muestra cuántos vértices nuevos cubre y cuántos quedan cubiertos en total.

Si el resultado no es factible, algunos vértices no se pueden alcanzar desde ningún candidato elegible dentro del radio indicado.

### Buscar cajeros automáticos

1. Selecciona **Buscar cajeros**.
2. Si no aparecen cajeros, pulsa **Actualizar datos OSM** y espera a que se descarguen los datos.
3. Pulsa **Elegir punto en el mapa** y luego haz clic en tu ubicación de partida.
4. Revisa la lista ordenada por distancia a pie. Selecciona un resultado para resaltarlo en el mapa.

La distancia se estima usando el vértice de la red más cercano al origen y el más cercano al cajero, conectados por el camino más corto del grafo. No es una ruta verificada hasta la ubicación exacta.

## Datos y estructura

El archivo `amb_graph.json` debe tener `schemaVersion: 2` y `networkType: "walk"`. Requiere los arreglos `nodes` y `edges`. Cada nodo contiene `id`, `lat`, `lon` e `isCandidateSite`; cada arista contiene `source`, `target` y `length`. El arreglo opcional `atms` contiene registros con `id`, `name`, `lat` y `lon`. Los archivos de grafos antiguos o de tipo conducción deben generarse de nuevo.

La aplicación ejecuta `TranSmart/scraper.py` para descargar la red peatonal simplificada y los elementos de OpenStreetMap etiquetados como cajeros (`amenity=atm`). El archivo `scraper.py` de la carpeta principal es una versión antigua que solo exporta el grafo; para actualizar los datos de la aplicación usa **Generate graph** o **Actualizar datos OSM**.

Estructura principal:

- `TranSmart/TranSmart.csproj`: aplicación de escritorio WPF para Windows.
- `TranSmart/Models/`: modelos del grafo y de los resultados.
- `TranSmart/Services/`: carga y generación del grafo, optimización y búsqueda de cajeros.
- `TranSmart/ViewModels/`: estado y comandos del panel de control.
- `TranSmart/Map/`: archivos locales de Leaflet y la página del mapa.
- `TranSmart/Tests/`: pruebas de C# y Python.
- `TranSmart/amb_graph.json`: grafo de ejemplo incluido.

## Uso sin conexión y limitaciones

La optimización, el historial de decisiones, los marcadores y los archivos locales del mapa funcionan sin conexión una vez que el grafo está disponible. Los mosaicos del mapa base y la descarga o actualización de datos sí requieren internet. El grafo incluido actualmente no contiene registros de cajeros, por lo que hay que actualizar los datos OSM para usar esa búsqueda.

La densidad de nodos no equivale a densidad de población. Los datos de OpenStreetMap pueden estar incompletos o desactualizados; la regla de candidatos es una aproximación basada en conexiones del grafo; el análisis no verifica el servicio de transporte, el estado de las aceras, restricciones de acceso ni seguridad. Los datos de OpenStreetMap están sujetos a sus [condiciones de derechos de autor y atribución](https://www.openstreetmap.org/copyright).

