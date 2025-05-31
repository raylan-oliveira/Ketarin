using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;


namespace Ketarin.Localization
{
    public static class LocalizationManager
    {
        private static Dictionary<string, string> currentLanguage = new Dictionary<string, string>();
        private static string currentCulture = "en-US";
        
        static LocalizationManager()
        {
            LoadLanguage();
        }
        
        public static void LoadLanguage(string culture = null)
        {
            if (culture == null)
            {
                culture = CultureInfo.CurrentUICulture.Name;
            }
            
            currentCulture = culture;
            currentLanguage.Clear();
            
            string languageFile = GetLanguageFilePath(culture);
            
            // Se não encontrar o arquivo do idioma específico, tenta carregar o inglês como padrão
            if (!File.Exists(languageFile))
            {
                // Para português, tentar pt-BR antes de fallback para inglês
                if (culture.StartsWith("pt"))
                {
                    languageFile = GetLanguageFilePath("pt-BR");
                }
                
                if (!File.Exists(languageFile))
                {
                    languageFile = GetLanguageFilePath("en-US");
                }
            }
            
            if (File.Exists(languageFile))
            {
                LoadLanguageFile(languageFile);
            }
        }
        
        private static string GetLanguageFilePath(string culture)
        {
            string appPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            return Path.Combine(appPath, "Languages", $"{culture}.txt");
        }
        
        private static void LoadLanguageFile(string filePath)
        {
            try
            {
                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
                
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;
                        
                    int separatorIndex = line.IndexOf('=');
                    if (separatorIndex > 0)
                    {
                        string key = line.Substring(0, separatorIndex).Trim();
                        string value = line.Substring(separatorIndex + 1).Trim();
                        
                        // Remove aspas se existirem
                        if (value.StartsWith("\"") && value.EndsWith("\""))
                        {
                            value = value.Substring(1, value.Length - 2);
                        }
                        
                        currentLanguage[key] = value;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar arquivo de idioma: {ex.Message}");
            }
        }
        
        public static string GetString(string key, string defaultValue = null)
        {
            if (currentLanguage.ContainsKey(key))
            {
                return currentLanguage[key];
            }
            
            return defaultValue ?? key;
        }
        
        public static string GetCurrentCulture()
        {
            return currentCulture;
        }
    }
}