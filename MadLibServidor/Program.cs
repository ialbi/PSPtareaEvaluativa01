using System;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace MadLibServidor
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            if (args.Length != 1)
            {
                Console.WriteLine("Ejecuta MadLibCliente para arrancar el servidor.");
                return;
            }
            try
            {
                using (var servidor = new NamedPipeServerStream(args[0]))
                {
                    Console.WriteLine("[Servidor] Esperando al cliente...");
                    servidor.WaitForConnection();
                    Console.WriteLine("[Servidor] Conexión establecida.");
                    using (var reader = new StreamReader(servidor))
                    using (var writer = new StreamWriter(servidor))
                    {
                        // La confirmación permite al cliente mostrar el menú después de estos registros.
                        Enviar(writer, "LISTO");
                        while (true)
                        {
                            string tipo = Recibir(reader);
                            if (tipo == null) break;
                            if (tipo == "SALIR")
                            {
                                Enviar(writer, "FIN");
                                break;
                            }
                            if (tipo != "CUENTO") throw new IOException("Se esperaba CUENTO.");
                            string nombre = Recibir(reader);
                            if (nombre == null) break;
                            if (!ProcesarCuento(nombre, reader, writer)) break;
                        }
                    }
                }
                Console.WriteLine("[Servidor] Finalizado.");
            }
            catch (Exception e)
            {
                Console.WriteLine("[Servidor] Error: " + e.Message);
                Environment.ExitCode = 1;
            }
        }

        static bool ProcesarCuento(string nombre, StreamReader reader, StreamWriter writer)
        {
            // Solo se aceptan nombres cuentoX, nunca rutas introducidas por el usuario.
            int numero;
            if (!nombre.StartsWith("cuento") ||
                !int.TryParse(nombre.Substring(6), out numero) || numero < 1 ||
                nombre != "cuento" + numero)
            {
                EnviarError(writer, "Nombre incorrecto. Utiliza cuento1, cuento2, etc.");
                return true;
            }

            string[] lineas;
            try
            {
                string ruta = Path.Combine(AppContext.BaseDirectory, "Cuentos", nombre + ".txt");
                lineas = File.ReadAllLines(ruta, Encoding.UTF8);
                Console.WriteLine("[Servidor] Apertura satisfactoria: " + nombre + ".txt");
            }
            catch (IOException e)
            {
                EnviarError(writer, "No se pudo abrir el cuento: " + e.Message);
                return true;
            }

            // Se recorre el texto original: las respuestas no se interpretan como marcadores.
            string[] resultado = new string[lineas.Length];
            for (int i = 0; i < lineas.Length; i++)
            {
                string linea = lineas[i];
                string completada = "";
                int posicion = 0;
                int inicio = linea.IndexOf('<', posicion);
                while (inicio != -1)
                {
                    int fin = linea.IndexOf('>', inicio + 1);
                    if (fin == -1)
                    {
                        EnviarError(writer, "El cuento tiene un hueco sin cerrar.");
                        return true;
                    }
                    completada += linea.Substring(posicion, inicio - posicion);
                    string descripcion = linea.Substring(inicio + 1, fin - inicio - 1);
                    Enviar(writer, "HUECO");
                    Enviar(writer, descripcion);
                    string tipo = Recibir(reader);
                    if (tipo == null) return false;
                    if (tipo == "SALIR")
                    {
                        Enviar(writer, "FIN");
                        return false;
                    }
                    if (tipo != "RESPUESTA") throw new IOException("Se esperaba RESPUESTA.");
                    string respuesta = Recibir(reader);
                    if (respuesta == null) return false;
                    completada += respuesta;
                    posicion = fin + 1;
                    inicio = linea.IndexOf('<', posicion);
                }
                resultado[i] = completada + linea.Substring(posicion);
            }

            try
            {
                string rutaResultado = Path.Combine(AppContext.BaseDirectory, "resultado.txt");
                File.WriteAllLines(rutaResultado, resultado, Encoding.UTF8);
                Console.WriteLine("[Servidor] Guardado: " + rutaResultado);
            }
            catch (IOException e)
            {
                EnviarError(writer, "No se pudo guardar el resultado: " + e.Message);
                return true;
            }
            // El número de líneas delimita el cuento, aunque contenga líneas vacías.
            // Anunciar la transmisión antes de enviar; después esperamos otra petición.
            Console.WriteLine("[Servidor] Enviando RESULTADO: " + resultado.Length + " líneas.");
            writer.WriteLine("RESULTADO");
            writer.WriteLine(resultado.Length);
            foreach (string linea in resultado) writer.WriteLine(linea);
            writer.Flush();
            return true;
        }

        static string Recibir(StreamReader reader)
        {
            string dato = reader.ReadLine();
            Console.WriteLine("[Servidor] Recibido: " +
                (dato == null ? "(conexión cerrada)" : dato == "" ? "(línea vacía)" : dato));
            return dato;
        }

        static void Enviar(StreamWriter writer, string dato)
        {
            Console.WriteLine("[Servidor] Enviado: " + dato);
            writer.WriteLine(dato);
            writer.Flush();
        }

        static void EnviarError(StreamWriter writer, string mensaje)
        {
            Enviar(writer, "ERROR");
            Enviar(writer, mensaje);
        }
    }
}
