using System.Windows.Forms;

namespace Ketarin.Localization
{
    public static class LocalizationHelper
    {
        public static void ApplyLocalization(Form form)
        {
            ApplyLocalizationToControl(form);
        }
        
        private static void ApplyLocalizationToControl(Control control)
        {
            // Aplicar localização baseada no nome do controle ou tag
            if (control.Tag is string localizationKey)
            {
                control.Text = LocalizationManager.GetString(localizationKey, control.Text);
            }
            
            // Aplicar recursivamente aos controles filhos
            foreach (Control child in control.Controls)
            {
                ApplyLocalizationToControl(child);
            }
        }
    }
}