using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace MadLibCliente
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            Process servidor = null;
            try
            {
                // Un nombre distinto permite ejecutar varias parejas cliente-servidor.
                string nombrePipe = "MadLibPSP01_" + Process.GetCurrentProcess().Id;
                servidor = ArrancarServidor(nombrePipe);
                using (var cliente = new NamedPipeClientStream(".", nombrePipe, PipeDirection.InOut))
                {
                    Console.WriteLine("[Cliente] Esperando conexión...");
                    cliente.Connect(15000);
                    using (var reader = new StreamReader(cliente))
                    using (var writer = new StreamWriter(cliente))
                    {
                        // Esperar a que el servidor termine de anunciar la conexión.
                        if (Leer(reader) != "LISTO")
                            throw new IOException("Se esperaba la confirmación LISTO del servidor.");
                        Console.WriteLine("[Cliente] Conexión establecida. Pipe: " + nombrePipe);
                        bool continuar = true;
                        while (continuar)
                        {
                            Console.Write("Nombre del cuento (cuento1 a cuento5) o salir: ");
                            string nombre = Console.ReadLine();
                            if (nombre == null || nombre.Trim().Equals("salir", StringComparison.OrdinalIgnoreCase))
                            {
                                Enviar(writer, "SALIR");
                                Console.WriteLine("[Cliente] Recibido: " + Leer(reader));
                                break;
                            }
                            Enviar(writer, "CUENTO");
                            Enviar(writer, nombre.Trim());
                            continuar = RecibirCuento(reader, writer);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("[Cliente] Error: " + e.Message);
                Environment.ExitCode = 1;
            }
            finally
            {
                // Normalmente el servidor termina al recibir SALIR o cerrarse el pipe.
                if (servidor != null)
                {
                    if (!servidor.WaitForExit(3000)) servidor.Kill();
                    servidor.Dispose();
                }
            }
        }

        static Process ArrancarServidor(string nombrePipe)
        {
            // Ruta relativa a la carpeta de salida del cliente, independiente del terminal.
            ProcessStartInfo info = new ProcessStartInfo("dotnet");
            info.WorkingDirectory = AppContext.BaseDirectory;
            info.Arguments = "\"Servidor/MadLibServidor.dll\" " + nombrePipe;
            info.UseShellExecute = false;
            info.CreateNoWindow = false;
            Console.WriteLine("[Cliente] Arrancando el proceso servidor.");
            return Process.Start(info);
        }

        static bool RecibirCuento(StreamReader reader, StreamWriter writer)
        {
            while (true)
            {
                string tipo = Leer(reader);
                Console.WriteLine("[Cliente] Recibido: " + tipo);
                if (tipo == "HUECO")
                {
                    string descripcion = Leer(reader);
                    Console.WriteLine("[Cliente] Recibido: " + descripcion);
                    Console.Write("Introduce " + descripcion + " (puede quedar vacío): ");
                    string respuesta = Console.ReadLine();
                    // null significa fin de entrada; una cadena vacía sí es una respuesta.
                    if (respuesta == null)
                    {
                        Enviar(writer, "SALIR");
                        Console.WriteLine("[Cliente] Recibido: " + Leer(reader));
                        return false;
                    }
                    Enviar(writer, "RESPUESTA");
                    Enviar(writer, respuesta);
                }
                else if (tipo == "RESULTADO")
                {
                    int numeroLineas = int.Parse(Leer(reader));
                    // Recibir todo antes de imprimir evita mezclar el cuento con los registros.
                    string[] cuento = new string[numeroLineas];
                    for (int i = 0; i < numeroLineas; i++)
                        cuento[i] = Leer(reader);
                    Console.WriteLine("[Cliente] Cuento recibido completo (" + numeroLineas + " líneas).");
                    Console.WriteLine();
                    Console.WriteLine("*************************");
                    Console.WriteLine();
                    Console.WriteLine("El cuento creado es:");
                    Console.WriteLine();
                    Console.WriteLine("*************************");
                    Console.WriteLine();
                    foreach (string linea in cuento)
                        Console.WriteLine(linea);
                    Console.WriteLine();
                    return true;
                }
                else if (tipo == "ERROR")
                {
                    Console.WriteLine("[Cliente] Recibido: " + Leer(reader));
                    return true;
                }
                else throw new IOException("Mensaje de servidor desconocido: " + tipo);
            }
        }

        static void Enviar(StreamWriter writer, string dato)
        {
            Console.WriteLine("[Cliente] Enviado: " + (dato == "" ? "(línea vacía)" : dato));
            writer.WriteLine(dato);
            writer.Flush();
        }

        static string Leer(StreamReader reader)
        {
            string dato = reader.ReadLine();
            if (dato == null) throw new IOException("El servidor ha cerrado la conexión.");
            return dato;
        }
    }
}
