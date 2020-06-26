namespace Guazu.NanoWeb
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;

    using System.Linq;

    using System.Net;
    using System.Threading;
    using System.IO;
    using Guazu.NanoWeb.AntsCode.Util;

    public class NanoWebEditorWindow : EditorWindow
    {
        [MenuItem("NanoWeb/Control...")]
        public static NanoWebEditorWindow Abrir() => GetWindow<NanoWebEditorWindow>();
        const string CONTENT_TYPE_MULTIPART = "multipart/form-data";

        public int PuertoDeServidor
        {
            get => EditorPrefs.GetInt($"NW.{name}.UsarPuerto", 50000);
            set => EditorPrefs.SetInt($"NW.{name}.UsarPuerto", value);
        }
        public string CarpetaWWW
        {
            get => EditorPrefs.GetString($"NW.{name}.CarpetaWWW", "www");
            set => EditorPrefs.SetString($"NW.{name}.CarpetaWWW", value);
        }

        static Dictionary<string, System.Action<HttpListenerContext, MultipartParser>> rutasYEfectos = new Dictionary<string, System.Action<HttpListenerContext, MultipartParser>>();
        public static void UsarRuta(string proceso, string ruta, System.Action<HttpListenerContext, MultipartParser> efecto)
        {
            rutasYEfectos.Add(proceso, efecto);
            EditorPrefs.SetString($"NW.{proceso}", CurarRuta(ruta));
        }
        static string GetRutaDeProcesoDefault(string proceso) => EditorPrefs.GetString($"NW.{proceso}", $"{proceso}");
        string GetRutaDeProceso(string proceso) => EditorPrefs.GetString($"NW.{name}.{proceso}", GetRutaDeProcesoDefault(proceso));
        void SetRutaDeProceso(string proceso, string nuevoValor)
        {
            EditorPrefs.SetString($"NW.{name}.{proceso}", CurarRuta(nuevoValor));
        }
        static string CurarRuta(string ruta)
        {
            if (ruta == null || ruta.Length == 0) ruta = "/";
            else
            {
                if (ruta.First() != '/') ruta = "/" + ruta;
                if (ruta.Last() == '/') ruta = ruta.Substring(0, ruta.Length - 1);
            }
            return ruta;
        }

        public bool HayRed => System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
        public static string DireccionIPLocal => Dns.GetHostEntry(Dns.GetHostName()).AddressList.FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToString();

        readonly object _servidorConectadoLock = new object();
        Thread _servidorConectado = null;
        Thread ServidorActual
        {
            get
            {
                Thread valor = null;
                lock (_servidorConectadoLock) valor = _servidorConectado;
                return valor;
            }
            set
            {
                lock (_servidorConectadoLock) _servidorConectado = value;
            }
        }
        public bool ServidorActualActivo
        {
            get
            {
                var _serv = ServidorActual;
                return _serv != null && _serv.IsAlive;
            }
        }
        void ActivarServidorActual()
        {
            if (ServidorActualActivo) return;
            var puerto = PuertoDeServidor;
            var carpetaWWW = Path.Combine(Application.dataPath, CarpetaWWW);
            Thread nuevoServidor = new Thread((yoMismo) => ProgramaDelServidor((Thread)yoMismo, puerto));
            nuevoServidor.Start(nuevoServidor);
            ServidorActual = nuevoServidor;
        }
        void DesactivarServidorActual()
        {
            if (!ServidorActualActivo) return;
            ServidorActual = null;
        }

        Vector2 logScroll;
        List<string> _unitySafeLog = new List<string>();
        readonly object _bridgeLogLock = new object();
        List<string> _bridgeLog = new List<string>();
        List<string> GetUpdatedSafeLog()
        {
            lock (_bridgeLogLock)
            {
                _unitySafeLog.AddRange(_bridgeLog.Skip(_unitySafeLog.Count));
            }
            return _unitySafeLog;
        }
        void ThreadSafeLog(string msj, bool debugTambien = false)
        {
            lock (_bridgeLogLock)
            {
                _bridgeLog.Add(msj);
            }
            if (debugTambien) Debug.Log(msj);
        }

        readonly object _colaDePedidosLock = new object();
        List<HttpListenerContext> _colaDePedidos = new List<HttpListenerContext>();
        Dictionary<HttpListenerContext, MultipartParser> _cargaDePedidos = new Dictionary<HttpListenerContext, MultipartParser>();
        void AgregarPedido(HttpListenerContext ctx, MultipartParser mem)
        {
            lock (_colaDePedidosLock)
            {
                _colaDePedidos.Add(ctx);
                if (mem != null) _cargaDePedidos.Add(ctx, mem);
            }
        }
        HttpListenerContext TomarPedido()
        {
            HttpListenerContext ctx = null;
            lock (_colaDePedidosLock)
            {
                if (_colaDePedidos.Count > 0)
                {
                    ctx = _colaDePedidos[0];
                    _colaDePedidos.RemoveAt(0);
                }
            }
            return ctx;
        }

        public void ResponderConArchivo(HttpListenerContext context, string pathAlArchivo)
        {
            var response = context.Response;
            var responseString = File.Exists(pathAlArchivo) ? File.ReadAllText(pathAlArchivo) : $"{pathAlArchivo} no encontrado";
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            System.IO.Stream output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();
        }

        void OnGUI()
        {
            var servidorActualActivo = ServidorActualActivo; // para no llamar locks incesantemente
            EditorGUILayout.Toggle("Hay Red", HayRed);
            EditorGUI.BeginDisabledGroup(servidorActualActivo);
            EditorGUILayout.TextField("IP Local", DireccionIPLocal);
            EditorGUI.EndDisabledGroup();
            PuertoDeServidor = EditorGUILayout.IntField("Puerto Para Usar", PuertoDeServidor);
            EditorGUILayout.TextField("Carpeta WWW", CarpetaWWW);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(servidorActualActivo);
            if (servidorActualActivo) GUI.color = Color.green;
            if (GUILayout.Button(servidorActualActivo ? "Conectado" : "Conectar", GUILayout.Width(0), GUILayout.ExpandWidth(true)))
            {
                ActivarServidorActual();
            }
            GUI.color = Color.white;
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(!servidorActualActivo);
            if (!servidorActualActivo) GUI.color = Color.red;
            if (GUILayout.Button(servidorActualActivo ? "Desconectar" : "Desconectado", GUILayout.Width(0), GUILayout.ExpandWidth(true)))
            {
                DesactivarServidorActual();
            }
            GUI.color = Color.white;
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.ExpandHeight(true));
            var log = GetUpdatedSafeLog();
            for (int i = log.Count - 1; i >= 0; i--)
            {
                GUILayout.Label(log[i]);
            }
            EditorGUILayout.EndScrollView();

            foreach (var efecto in rutasYEfectos.Keys)
            {
                EditorGUILayout.TextField(efecto, GetRutaDeProceso(efecto));
            }
        }

        protected virtual void ProcesarPedidoEnMainThread(HttpListenerContext ctx)
        {
            var efecto = rutasYEfectos.Keys.Select(k => new { efecto = k, ruta = GetRutaDeProceso(k) }).OrderByDescending(r => r.ruta.Length)
                .FirstOrDefault(r => ctx.Request.Url.AbsolutePath.IndexOf(r.ruta) == 0);
            if (efecto != null)
            {
                ThreadSafeLog($"proceso utilizado = {efecto}");
                var streamCarga = _cargaDePedidos.ContainsKey(ctx) ? _cargaDePedidos[ctx] : null;
                rutasYEfectos[efecto.efecto]?.Invoke(ctx, streamCarga);
            }
            else
            {
                var posibleArchivo = ctx.Request.Url.Segments.LastOrDefault();
                var indexUrl = Path.Combine(Application.dataPath, CarpetaWWW);
                indexUrl += ctx.Request.Url.AbsolutePath;
                if (!posibleArchivo.Contains('.')) indexUrl = Path.Combine(indexUrl, "index.html");
                var hayIndex = File.Exists(indexUrl);
                ThreadSafeLog($"enviando archivo como respuesta = {indexUrl}");
                if (hayIndex)
                {
                    ResponderConArchivo(ctx, indexUrl);
                }
                else
                {
                    ResponderQueNoHayIndex(ctx.Response);
                }
            }
        }

        protected virtual void Update()
        {
            var ctxAProcesar = TomarPedido();
            if (ctxAProcesar != null)
            {
                ProcesarPedidoEnMainThread(ctxAProcesar);
                ctxAProcesar.Response.OutputStream.Close();// cierre extra por las dudas
            }
        }

        void OnEnable()
        {
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }
        void OnDisable()
        {
            EditorApplication.update -= Update;
            DesactivarServidorActual();
        }

        // ACORDARSE ACA NADA DE ACCIONES DE UNITY OK?!
        void ProgramaDelServidor(Thread yoMismo, int puerto)
        {
            var url = $"http://+:{puerto}/";

            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            ThreadSafeLog($"Servidor iniciado en {url.Replace("+", DireccionIPLocal)}\n=== === === === ===", true);

            System.AsyncCallback alTenerContexto = null;
            alTenerContexto = (resultado) =>
            {
                try
                {
                    var ctx = listener.EndGetContext(resultado);
                    listener.BeginGetContext(alTenerContexto, null);

                    var request = ctx.Request;
                    ThreadSafeLog($"Pedido recibido de {request.RemoteEndPoint.ToString()} hacia {request.Url.ToString()}\nruta : {request.Url.AbsolutePath}\n{request.ContentType}");

                    var carga = new MultipartParser(request.InputStream, request.ContentEncoding);
                    Debug.Log($"carga.Success {carga.Success}");

                    AgregarPedido(ctx, carga);
                }
                catch (System.ObjectDisposedException) { }
            };

            // este contexto arranca la cadena de eventos
            listener.BeginGetContext(alTenerContexto, null);
            while (yoMismo == ServidorActual)
            {
                Thread.Sleep(100);
            }

            listener.Stop();
            listener.Close();
            ThreadSafeLog($"=== === === === ===\nServidor en {url} terminado", true);
        }

        static void ResponderQueNoHayIndex(HttpListenerResponse response)
        {
            string responseString = "<HTML><BODY><H3>No hay un 'index.html' en la carpeta WWW para presentar, ni una respuesta asignada a esta ruta...</H3></BODY></HTML>";
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            // Get a response stream and write the response to it.
            response.ContentLength64 = buffer.Length;
            System.IO.Stream output = response.OutputStream;
            response.StatusCode = (int)HttpStatusCode.OK;
            output.Write(buffer, 0, buffer.Length);
            // You must close the output stream.
            output.Close();
        }
        public static void ResponderString(HttpListenerResponse response, string respuesta, bool cerrarAlTerminar = true)
        {
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(respuesta);
            // Get a response stream and write the response to it.
            response.ContentLength64 = buffer.Length;
            System.IO.Stream output = response.OutputStream;
            response.StatusCode = (int)HttpStatusCode.OK;
            output.Write(buffer, 0, buffer.Length);
            // You must close the output stream.
            if (cerrarAlTerminar) output.Close();
        }
    }
}