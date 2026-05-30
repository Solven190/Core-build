using Avalonia.Data.Core;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;
using System;

namespace Ryujinx.Ava.Common.Locale
{
    internal class LocaleExtension : MarkupExtension
    {
        public LocaleExtension(LocaleKeys key)
        {
            Key = key;
        }

        public LocaleKeys Key { get; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            LocaleKeys keyToUse = Key;

            CompiledBindingPathBuilder builder = new();

            builder
                .Property(new ClrPropertyInfo("Item",
                _ => (LocaleManager.Instance[keyToUse]),
                null,
                typeof(string)), (weakRef, iPropInfo) =>
                {
                    return PropertyInfoAccessorFactory.CreateInpcPropertyAccessor(weakRef, iPropInfo);
                });

            CompiledBindingPath path = builder.Build();

            CompiledBindingExtension binding = new(path)
            {
                Source = LocaleManager.Instance
            };

            return binding.ProvideValue(serviceProvider);
        }
    }
}
