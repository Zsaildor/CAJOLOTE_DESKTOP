using System;
using System.IO;

namespace Cajolote.Services
{
    public static class EnvLoader
    {
        private static bool _loaded = false;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                // Buscar archivo .env en varias ubicaciones posibles
                string[] possiblePaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env"),
                    Path.Combine(Environment.CurrentDirectory, ".env"),
                    // Durante desarrollo (bin/Debug/net10.0-windows/ -> project root)
                    Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".env"))
                };

                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        LoadFromFile(path);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EnvLoader] Error al cargar archivo .env: {ex.Message}");
            }
        }

        private static void LoadFromFile(string filePath)
        {
            foreach (var rawLine in File.ReadAllLines(filePath))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                int equalIndex = line.IndexOf('=');
                if (equalIndex > 0)
                {
                    string key = line.Substring(0, equalIndex).Trim();
                    string value = line.Substring(equalIndex + 1).Trim();

                    // Quitar comillas si las tiene
                    if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                        (value.StartsWith("'") && value.EndsWith("'")))
                    {
                        value = value.Substring(1, value.Length - 2);
                    }

                    // Establecer variable de entorno solo si no está ya configurada a nivel sistema
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                    {
                        Environment.SetEnvironmentVariable(key, value);
                    }
                }
            }
        }
    }
}
