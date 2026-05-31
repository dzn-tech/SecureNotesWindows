using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace SecureNotesWin.Localization
{
    public class Loc : MarkupExtension
    {
        public string Key { get; set; }

        public Loc(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var binding = new Binding
            {
                Source = LocalizationManager.Instance,
                Path = new PropertyPath($"Item[{Key}]"),
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            return binding.ProvideValue(serviceProvider);
        }
    }
}