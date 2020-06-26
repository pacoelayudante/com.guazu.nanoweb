_Unity Game Engine Package_
# Nano web
#### _Paco Álvarez Lojo de [Team Guazú](https://teamguazu.com/)_

Este coso habilita un Editor Window que permite iniciar un servidor http en la red local. Puede hostear una pagina web, recibir o enviar información y archivos. Es accesible desde el menu superior de Unity.

*Advertencia*: No soy un experto de programación de servers, asi que este sistema no es muy sofisticado, y si tenés algo de experiencia en el tema, probablemente sepas hacerlo mejor que esto.

Mas abajo explico como _instalarlo_ usando el [Unity Package Manager](https://docs.unity3d.com/Manual/upm-ui.html) (versiones mayor a `2018.1`)

## Un mini servidor local
La idea de hacer esta extensión del editor se me ocurrió a partir de otra idea para extender el editor que en parte necesita poder usar un celular (o similar dispositivo) para sacar una foto y enviarla directamente al Editor (sin pasar por mas intermediarios) y luego hacer mas cosas, pero para la parte del envío me puse a hacer esta extensión.

Es una solución exagerada a un problema innecesario. Pero me gusta experimentar cosas, y creo que al hacer la herramienta de manera generalizadora (no solo un coso que reciba imagenes y ya) se habilita la posibilidad de disparar y realizar mas ideas a partir de este sistema.

En sí el sistema se basa en el [HttpListener](https://docs.microsoft.com/en-us/dotnet/api/system.net.httplistener) y esta pensado para usar dentro de una red local. Respecto al Firewall, en windows la red debe estar puesta como privada, sino vas a tener que hacer una configuración de las reglas de entrada.

## ¿Como?
Para abrir la ventana de control tenés que `NanoWeb > Control`. Ahí se abre una ventana muy simple que tiene el campo para definir el `puerto` a usar, los botones de `Conectar` y `Desconectar`, la `carpeta` de navegacion basica y las `rutas especiales` para hacer tipo APIs.

### Uso simple
Cuando digo **carpetas de navegación** me refiero a una carpeta dentro del proyecto que va a mapear las rutas al host directamente a los archivos que esten ahi (o a `index.html` si apuntas a una carpeta sin definir archivo). La carpeta raiz por defecto es `www`, es decir `<proyecto de unity>/Assets/www/`.

Mas fácil describir unos ejemplos
|url|respuesta|
|-|-|
|`http://{server ip}:{puerto}/`|`<proyecto de unity>/Assets/www/index.html`|
|`http://{server ip}{puerto}/dibujin.png`|`<proyecto de unity>/Assets/www/dibujin.png`|
|`http://{server ip}:{puerto}/subcarpeta`|`<proyecto de unity>/Assets/www/subcarpeta/index.html`|
|`http://{server ip}:{puerto}/otracarpeta/otra_pagina.html`|`<proyecto de unity>/Assets/www/otracarpeta/otra_pagina.html`|

### Hacer cosas
Cuando digo **rutas especiales** tipo APIs, me refiero a una ruta que se mapea a una funcion del editor que tenes que armar, incluyendo la respuesta (si te pinta dar una respuesta).

En algún lado de tu proyecto tenes que inicializar una función estática que defina una ruta (url) y tenga un callback que se va a ejecutar cuando se reciba un pedido a esa ruta.

Otra vez un ejemplo lo va a mostrar mas claramente...
```csharp
using UnityEditor;
using Guazu.NanoWeb;

[InitializeOnLoadMethod] //Esto le dice al unity editor que ejecute este metodo al arrancar (en realidad, al recompilar, pero bue)
static void Inicializar()
{
  //el primer parametro es un nombre identificador que te pinte (pero único)
  //el segundo parametro es una ruta por defecto, que podes cambiar luego
  //el tercer parametro es la funcion de callback
  NanoWebEditorWindow.UsarRuta("hacerEco", "/eco", (ctx, carga) =>
  {
      // ctx -> System.Net.HttpListenerContext
      // carga -> Guazu.NanoWeb.AntsCode.Util.MultipartParser
      // la carga es un archivo enviado por el cliente basicamente
      var segmentos = ctx.Request.Url.Segments;
      string respuesta = "<html><body>usar asi http://host/eco/{algun mensaje aca}<br/>ECO:<br/>";
      if (segmentos.Length > 2)
      {
          respuesta += segmentos[2];
      }
      respuesta += "</body></html>";
      NanoWebEditorWindow.ResponderString(ctx.Response, respuesta, cerrarAlTerminar: true);
  });
}
```

Lo que haría el ejemplo anterior es responderle a cualquiera que se meta en la url `http://{ip:puerto}/eco/holis` con una mini paginita que dice **ECO: holis**

### ¡Recibir archivos!

Bueno, esto es la posta de este sistema, debería estar arriba del todo incluso, pero bueno, no soy muy capo haciendo estos readmes.

Para empezar, es necesario tener un formulario para enviar un archivo, y ese form tiene que mandar las cosas encodeadas tipo `multipart/form-data`, algo asi digamos...
```html
<form action="/mandar" enctype="multipart/form-data">
  <label for="imagen">Enviar imagen a Unity</label>
  <input type="file" id="imagen" name="imagen" accept="image/png, image/jpeg" />
  <button type="submit" formmethod="post">¡Mandalo!</button>
</form>
```
Y para recibir la informacion binaria de la imagen tenemos que preparar una funcion que interprete el [MultipartParser](http://multipartparser.codeplex.com)

```csharp
NanoWebEditorWindow.UsarRuta("recibirImagen", "/mandar", (ctx, carga) =>
{
  /// carga es el MultipartParser y contiene la informacion del archivo
  var textura = new Texture2D(8, 8);//no importa este tamaño
  textura.LoadImage(carga.FileContents);//aca carga la data!

  Selection.activeObject = textura;
  var respuesta = $"Imagen de {textura.width}x{textura.height} recibida";
  Debug.Log(respuesta);
  NanoWebEditorWindow.ResponderString(ctx.Response, respuesta, cerrarAlTerminar: true);
});
```

Bueno, y con eso ya deberías tener suficiente para hacer lo que se te ocurra con este sistema. No es la gran cosa pero para hacer algun que otro experimento loco puede estar bueno. Mi idea inicial tiene que ver con habilitar una forma de enviar fotos del celu al UnityEditor y que el Unity Editor las procese y las use tipo sprites ponele. Pero que se yo... tambien podria usarse en un evento, un especie de game jam, donde la gente pueda colaborar en un proyecto en tiempo real, utilizando este sistema para enviar contenido o editar o que se yo!

#### Crédito extra a
##### [Anthony Super](http://antscode.blogspot.com) ([AntsCode](https://github.com/antscode)) por su [MultipartParser](http://multipartparser.codeplex.com) (2009)

## Instalación usando el UPM
Abrir `<project>/Packages/manifest.json` y agregar el scope para los paquetes de guazu.
```json
{
  "scopedRegistries": [
    {
      "name": "Paquetes de Guazu",
      "url": "https://registry.npmjs.org/",
      "scopes": [
        "com.guazu"
      ]
    }
    // ...
  ],
  "dependencies": {
    // ...
  }
}
```

Luego en el Unity Editor abrir `Window > Package Manager` y podras ver los paquetes de Guazu listos para ser instalados